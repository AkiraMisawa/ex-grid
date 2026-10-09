using System.Globalization;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The user's own writes count as seen, for the Overwrite Notice (ADR-0142 D1, LV-17), by a rule that
/// never looks at a time or compares painted text. A write the grid raises for the user's gesture is
/// followed, by position, until it settles: at the first new Window taken in after its intent's handler
/// completed — for a bound source, the first it publishes, which the grid asks for as the handler
/// completes (D5). An editor opened over one of its cells before it settles takes, when it settles, what
/// the cell paints then as what the user saw; after that, any change under it is told. The Edit Intent
/// carries the same two texts the notice compares, and the notice is raised exactly when they differ.
///
/// 20px rows in a 120px Viewport, 350px wide: Book 0–100 and Amount 100–200, both editable.
/// </summary>
public class OwnWritesSettleTests : GridTestContext
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(Func<object, string>? amountFormat = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true, format: amountFormat),
    ];

    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public List<GridOverwriteNotice> Notices { get; } = [];
        public List<GridCommitRefusal> CommitRefusals { get; } = [];
        public List<GridPasteIntent> Pastes { get; } = [];

        /// <summary>What the Consumer does with an Edit Intent beyond hearing it, if anything.</summary>
        public Action<GridEditIntent<TestRow>>? OnEdit { get; set; }
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] rows, Heard heard, GridColumn<TestRow>[]? columns = null, Func<TestRow, object>? rowKey = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, rows.Length)
            .Add(g => g.RowKey, rowKey)
            .Add(g => g.Columns, columns ?? Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) =>
            {
                heard.Edits.Add(i);
                heard.OnEdit?.Invoke(i);
            })
            .Add(g => g.OnCommitRefused, (GridCommitRefusal r) => heard.CommitRefusals.Add(r))
            .Add(g => g.OnOverwriteNotice, (GridOverwriteNotice n) => heard.Notices.Add(n))
            .Add(g => g.OnPaste, (GridPasteIntent i) => heard.Pastes.Add(i)));

    private IRenderedComponent<ExGrid<TestRow>> RenderSourceGrid(IGridSource<TestRow> source, Heard heard)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) =>
            {
                heard.Edits.Add(i);
                heard.OnEdit?.Invoke(i);
            })
            .Add(g => g.OnCommitRefused, (GridCommitRefusal r) => heard.CommitRefusals.Add(r))
            .Add(g => g.OnOverwriteNotice, (GridOverwriteNotice n) => heard.Notices.Add(n)));

    /// <summary>The rows with the one at <paramref name="index"/> replaced by a new instance.</summary>
    private static TestRow[] Changed(IReadOnlyList<TestRow> rows, int index, string? book = null, decimal? amount = null)
    {
        var next = rows.ToArray();
        var old = rows[index];
        next[index] = new TestRow { Book = book ?? old.Book, Amount = amount ?? old.Amount, AsOf = old.AsOf, Active = old.Active };
        return next;
    }

    private static void Push(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] rows)
        => cut.Render(ps => ps.Add(g => g.Window, rows));

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> grid, string key)
        => grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> grid, double x, double y)
    {
        await grid.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });
        await grid.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });
    }

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> grid, string text)
        => grid.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    private static string PaintedText(IRenderedComponent<ExGrid<TestRow>> grid, int row, int column)
        => grid.FindAll(".ex-row")[row].QuerySelectorAll("[role=gridcell]")[column].TextContent;

    // ---- (a) A write that leaves the painted text as it was settles all the same ----

    [Fact] // ADR-0142 D1 / LV-17: retyping the value a cell shows, written back as a new row with the same text, settles — a later change under an editor there is told (it was not, when only a different text let the write go)
    public async Task Retyping_the_shown_value_settles_and_a_later_change_is_told()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        // Row 1's Amount paints "1".
        await ClickAsync(cut, 150, 30);
        await KeyAsync(cut, "1");
        await KeyAsync(cut, "Enter");
        var written = Changed(rows, 1, amount: 1m);
        Push(cut, written);

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        Push(cut, Changed(written, 1, amount: 2_000_000m));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("1", notice.SeenText);
        Assert.Equal("2000000", notice.ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17: F2 and Enter with nothing changed still raise an intent, and its write settles with the Window after it
    public async Task F2_enter_unchanged_settles_and_a_later_change_is_told()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "F2");
        await KeyAsync(cut, "Enter");
        Assert.Equal("Row 000000", Assert.Single(heard.Edits).Value);
        var written = Changed(rows, 0);
        Push(cut, written);

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        Push(cut, Changed(written, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("Row 000000", notice.SeenText);
        Assert.Equal("Moved upstream", notice.ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17: a value the format paints as the old one — 1.4 under a format of whole numbers — settles with the Window that writes it
    public async Task A_value_the_format_paints_alike_settles_and_a_later_change_is_told()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, Columns(amountFormat: v => ((decimal)v).ToString("0", Invariant)));
        await ClickAsync(cut, 150, 30);
        await KeyAsync(cut, "1");
        await TypeAsync(cut, "1.4");
        await KeyAsync(cut, "Enter");
        var written = Changed(rows, 1, amount: 1.4m);
        Push(cut, written);
        Assert.Equal("1", PaintedText(cut, 1, 1));

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        Push(cut, Changed(written, 1, amount: 9m));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("1", notice.SeenText);
        Assert.Equal("9", notice.ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17: a write the Consumer let pass without writing, and without Refuse, settles with the next new Window taken in — a live tick elsewhere — and a change under an editor opened after it is told
    public async Task A_write_declined_without_refuse_settles_with_the_next_window()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        // The Consumer writes nothing back. A tick to another row is the next new Window.
        var ticked = Changed(rows, 3, amount: 33m);
        Push(cut, ticked);

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        Push(cut, Changed(ticked, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("Row 000000", notice.SeenText);
        Assert.Equal("Moved upstream", notice.ReplacedText);
    }

    // ---- (b), (c) The same keys give the same outcome in either order of events ----

    [Theory] // ADR-0142 D1 / LV-11, LV-17, principle 6: `5` Enter ↑ `7`, then a change upstream to 9, then Enter — told once, the same notice whether the user's 5 is written back before the `7` opens the editor (WebAssembly) or under it (a circuit at machine speed); the intent carries the notice's two texts
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_change_after_the_users_own_write_is_told_alike_in_either_order(bool writtenBackUnderTheEditor)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        var written = Changed(rows, 0, book: "5");
        if (!writtenBackUnderTheEditor)
            Push(cut, written);
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        if (writtenBackUnderTheEditor)
            Push(cut, written);

        Push(cut, Changed(written, 0, book: "9"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal(new Selection.CellPosition(0, 0), notice.Cell);
        Assert.Equal("5", notice.SeenText);
        Assert.Equal("9", notice.ReplacedText);
        var commit = heard.Edits[1];
        Assert.Equal("7", commit.Value);
        Assert.Equal(notice.SeenText, commit.SeenText);
        Assert.Equal(notice.ReplacedText, commit.ReplacedText);
    }

    [Theory] // ADR-0142 D1 / LV-17, principle 6: `5` Enter ↑ `7` Enter tells nothing in either order, and the second intent carries the user's 5 as both what was seen and what was replaced — a Consumer comparing them never finds its user's own value given as a change
    [InlineData(false)]
    [InlineData(true)]
    public async Task Five_enter_up_seven_enter_tells_nothing_in_either_order(bool writtenBackUnderTheEditor)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        var written = Changed(rows, 0, book: "5");
        if (!writtenBackUnderTheEditor)
            Push(cut, written);
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        if (writtenBackUnderTheEditor)
            Push(cut, written);

        await KeyAsync(cut, "Enter");

        Assert.Equal(["5", "7"], heard.Edits.Select(e => e.Value));
        Assert.Empty(heard.Notices);
        Assert.Equal("5", heard.Edits[1].SeenText);
        Assert.Equal("5", heard.Edits[1].ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17, principle 6: a Window taken in while the write's handler is still running settles nothing yet — the handler may not have written — and the write settles as its handler completes, with what the handler wrote
    public async Task A_window_during_the_handler_settles_the_write_only_as_the_handler_completes()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var answered = new TaskCompletionSource();
        var ticked = Changed(rows, 3, amount: 33m);
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, rows.Length)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.Edits.Add(i))
            .Add(g => g.OnOverwriteNotice, (GridOverwriteNotice n) => heard.Notices.Add(n))
            // A Consumer that writes the paste once its server has answered, inside its handler.
            .Add(g => g.OnPaste, async (GridPasteIntent i) =>
            {
                heard.Pastes.Add(i);
                await answered.Task;
                Push(cut!, Changed(ticked, 0, book: "x"));
            }));
        await ClickAsync(cut, 50, 10);

        var paste = cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>"));
        // While the handler runs: a tick elsewhere, and the user types over the pasted cell.
        Push(cut, ticked);
        await KeyAsync(cut, "7");
        answered.SetResult();
        await paste;
        await KeyAsync(cut, "Enter");

        Assert.Single(heard.Pastes);
        var commit = Assert.Single(heard.Edits);
        Assert.Equal("7", commit.Value);
        Assert.Empty(heard.Notices);
        Assert.Equal("x", commit.SeenText);
        Assert.Equal("x", commit.ReplacedText);
    }

    // ---- A write the Consumer refused, and an order move ----

    [Fact] // ADR-0142 D1 / LV-19, ADR-0050 item 3: an edit the Consumer refused wrote nothing — it is taken back, and a change under the next editor on the cell is told
    public async Task An_edit_the_consumer_refused_lets_nothing_off()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { OnEdit = intent => intent.Refuse("Closed.") };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "Escape");
        heard.OnEdit = null;

        await KeyAsync(cut, "7");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("Row 000000", notice.SeenText);
        Assert.Equal("Moved upstream", notice.ReplacedText);
    }

    [Fact] // ADR-0142 D1 / ADR-0011: an order move drops the user's own writes — their positions name other rows — so a change under an editor opened after it is told
    public async Task An_order_move_drops_the_users_own_writes()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        // Rows 3 and 4 trade places: a new order, the 5 not in it.
        TestRow[] reordered = [.. rows[..3], rows[4], rows[3], .. rows[5..]];
        cut.Render(ps => ps.Add(g => g.Window, reordered).Add(g => g.RowSequenceVersion, 1));

        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "7");
        cut.Render(ps => ps.Add(g => g.Window, Changed(reordered, 0, book: "Moved upstream")));
        await KeyAsync(cut, "Enter");

        Assert.Equal("Moved upstream", Assert.Single(heard.Notices).ReplacedText);
    }

    // ---- The baseline is read from the edited row, or kept ----
    //
    // A write the open editor awaited is let go while the edited row is out of the Window: the baseline is
    // owed a reading then ("D1 settles at a point in the grid's order of events"), and the editor's position
    // holds another row, or none. The baseline is only ever read from the edited row itself, by its Row Key;
    // with the row away, the editor keeps the baseline it had. That errs towards a notice — the commit may
    // tell the user's own write as a change — as ADR-0142's accepted limits do, and never hides one.

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

    [Fact] // ADR-0142 D1 / LV-11, LV-17, LV-20: with a Row Key, an order move that lets go of the write the open editor awaited, and takes the edited row out of the Window with another row at its place, leaves the baseline as it was — the notice the commit raises once the row is back never carries the other row's text
    public async Task A_baseline_owed_while_the_edited_row_is_out_of_the_window_is_never_read_from_another_row()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, rowKey: ByBook);
        // The user's own 5 into row 0's Amount, not written back yet: an editor opened over it awaits it.
        await ClickAsync(cut, 150, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        Assert.Equal("0", PaintedText(cut, 0, 1));

        // The order moves: row 0 leaves the Window, and a row painting 99 stands at its place.
        var other = new TestRow { Book = "Another row", Amount = 99m, AsOf = rows[0].AsOf, Active = rows[0].Active };
        cut.Render(ps => ps.Add(g => g.Window, [other, .. rows[1..]]).Add(g => g.RowSequenceVersion, 1));
        // Row 0 is back, with the user's 5 written into it.
        var back = Changed(rows, 0, amount: 5m);
        cut.Render(ps => ps.Add(g => g.Window, back).Add(g => g.RowSequenceVersion, 2));
        await KeyAsync(cut, "Enter");

        var commit = heard.Edits[1];
        Assert.Equal("7", commit.Value);
        Assert.Same(back[0], commit.Row);
        var notice = Assert.Single(heard.Notices);
        Assert.Equal("0", notice.SeenText);
        Assert.Equal("5", notice.ReplacedText);
        Assert.Equal((notice.SeenText, notice.ReplacedText), (commit.SeenText, commit.ReplacedText));
    }

    [Fact] // ADR-0142 D1 / LV-11, LV-17, principle 1: with a Row Key, a Window that moves past the edited row lets go of the write the open editor awaited while no row stands at the editor's place — the editor keeps the baseline it had, never none, so a change made upstream while the row was away is told when the commit lands over it
    public async Task A_baseline_owed_while_no_row_stands_at_the_editors_place_is_kept_and_a_change_is_told()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, rowKey: ByBook);
        await ClickAsync(cut, 150, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");

        // The Window moves on to row 10, under the same order: row 0 is held no more.
        cut.Render(ps => ps.Add(g => g.Window, rows[10..]).Add(g => g.WindowStart, 10));
        // Row 0 is back, written upstream to 9 meanwhile, under the editor.
        var back = Changed(rows, 0, amount: 9m);
        cut.Render(ps => ps.Add(g => g.Window, back).Add(g => g.WindowStart, 0));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("0", notice.SeenText);
        Assert.Equal("9", notice.ReplacedText);
        Assert.Equal("0", heard.Edits[1].SeenText);
    }

    // ---- A pushed-Window Consumer that applies the write in its handler ----

    [Fact] // ADR-0142 D1 / LV-17: a Consumer that applies the write in its handler hands the grid the new Window as the handler completes, so the write has settled before the next key: `5` Enter ↑ `7` Enter tells nothing, and a change after it is told with the 5 as what was seen
    public async Task A_consumer_applying_in_its_handler_settles_the_write_as_the_handler_completes()
    {
        var host = Render<ApplyingHost>(ps => ps.Add(h => h.Rows, TestRows.Many(50)));
        var grid = host.FindComponent<ExGrid<TestRow>>();
        await ClickAsync(grid, 50, 10);
        await KeyAsync(grid, "5");
        await KeyAsync(grid, "Enter");
        Assert.Equal("5", PaintedText(grid, 0, 0));

        await KeyAsync(grid, "ArrowUp");
        await KeyAsync(grid, "7");
        await KeyAsync(grid, "Enter");
        Assert.Empty(host.Instance.Notices);
        Assert.Equal("5", host.Instance.Edits[1].SeenText);

        await KeyAsync(grid, "ArrowUp");
        await KeyAsync(grid, "8");
        await host.InvokeAsync(() => host.Instance.Upstream(0, "9"));
        await KeyAsync(grid, "Enter");
        var notice = Assert.Single(host.Instance.Notices);
        Assert.Equal("7", notice.SeenText);
        Assert.Equal("9", notice.ReplacedText);
    }

    // ---- Bound sources ----

    [Fact] // ADR-0142 D1, D5 / LV-16, LV-17: GridSource.From gathering the Consumer's write (the store's list handed back inside an interval) puts it out as the handler completes — it is painted before the next key — and `5` Enter ↑ `7` Enter tells nothing, the 5 seen and replaced
    public async Task A_gathering_source_puts_out_the_users_write_as_the_handler_completes()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = GridSource.From(rows, r => r.Book, Clock);
        heard.OnEdit = intent => source.Apply(new GridChangeBatch<TestRow>(changed:
        [
            new TestRow { Book = intent.Row.Book, Amount = decimal.Parse(intent.Value, Invariant), AsOf = intent.Row.AsOf, Active = intent.Row.Active },
        ]));
        var cut = RenderSourceGrid(source, heard);
        // A tick a moment ago: the next change within the interval is gathered, not shown.
        source.Apply(new GridChangeBatch<TestRow>(changed: [Changed(rows, 3, amount: 33m)[3]]));
        await ClickAsync(cut, 150, 10);

        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        Assert.Equal("5", PaintedText(cut, 0, 1));

        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        await KeyAsync(cut, "Enter");

        Assert.Equal(["5", "7"], heard.Edits.Select(e => e.Value));
        Assert.Empty(heard.Notices);
        Assert.Equal("5", heard.Edits[1].SeenText);
        Assert.Equal("5", heard.Edits[1].ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17: a source whose answer is a question to a server settles the user's write with the first Window it publishes after the handler — under the open editor — so `5` Enter ↑ `7` Enter tells nothing
    public async Task A_fetching_source_settles_the_users_write_when_its_answer_comes()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new AnsweringSource(rows);
        heard.OnEdit = intent => source.Ask(0, intent.Value);
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");

        await cut.InvokeAsync(source.Answer);
        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Notices);
        Assert.Equal("5", heard.Edits[1].SeenText);
        Assert.Equal("5", heard.Edits[1].ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-11: a commit made while the user's own write is still unanswered compares what the editor opened over with what the cell paints now, as any commit does — nothing changed, nothing told
    public async Task A_commit_before_the_answer_compares_as_usual()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new AnsweringSource(rows);
        heard.OnEdit = intent => source.Ask(0, intent.Value);
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Notices);
        Assert.Equal("Row 000000", heard.Edits[1].SeenText);
        Assert.Equal("Row 000000", heard.Edits[1].ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-11: once the answer carrying the user's write has settled it, a change upstream under the same editor is told, with the user's own value as what was seen
    public async Task A_change_after_the_answer_under_the_same_editor_is_told()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = new AnsweringSource(rows);
        heard.OnEdit = intent => source.Ask(0, intent.Value);
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        await cut.InvokeAsync(source.Answer);

        await cut.InvokeAsync(() => source.Upstream(0, "9"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("5", notice.SeenText);
        Assert.Equal("9", notice.ReplacedText);
        Assert.Equal("5", heard.Edits[1].SeenText);
        Assert.Equal("9", heard.Edits[1].ReplacedText);
    }

    /// <summary>A Consumer that pushes its Window and applies each edit in its handler, as a page
    /// holding its rows does: the new list reaches the grid as the handler completes, when Blazor
    /// renders the receiver of the callback.</summary>
    private sealed class ApplyingHost : ComponentBase
    {
        private static readonly GridColumn<TestRow>[] HostColumns = Columns();
        private TestRow[] _rows = [];
        private EventCallback<GridEditIntent<TestRow>> _onEdit;
        private EventCallback<GridOverwriteNotice> _onNotice;

        [Parameter] public TestRow[] Rows { get; set; } = [];

        public List<GridEditIntent<TestRow>> Edits { get; } = [];

        public List<GridOverwriteNotice> Notices { get; } = [];

        protected override void OnInitialized()
        {
            _rows = Rows;
            _onEdit = EventCallback.Factory.Create<GridEditIntent<TestRow>>(this, Apply);
            _onNotice = EventCallback.Factory.Create<GridOverwriteNotice>(this, Notices.Add);
        }

        /// <summary>A change upstream to row <paramref name="row"/>'s Book.</summary>
        public void Upstream(int row, string book)
        {
            _rows = Changed(_rows, row, book: book);
            StateHasChanged();
        }

        private void Apply(GridEditIntent<TestRow> intent)
        {
            Edits.Add(intent);
            var at = Array.IndexOf(_rows, intent.Row);
            _rows = intent.Column == "Book"
                ? Changed(_rows, at, book: intent.Value)
                : Changed(_rows, at, amount: decimal.Parse(intent.Value, Invariant));
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ExGrid<TestRow>>(0);
            builder.AddComponentParameter(1, nameof(ExGrid<TestRow>.Window), (IReadOnlyList<TestRow>)_rows);
            builder.AddComponentParameter(2, nameof(ExGrid<TestRow>.TotalCount), (int?)_rows.Length);
            builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)HostColumns);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.ViewportWidth), (ViewportSize)350);
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.OnEdit), _onEdit);
            builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.OnOverwriteNotice), _onNotice);
            builder.CloseComponent();
        }
    }

    /// <summary>A source whose writes are questions to a server: <see cref="Ask"/> records the write
    /// and publishes nothing, and <see cref="Answer"/> publishes the Window the server answers with.</summary>
    private sealed class AnsweringSource(IReadOnlyList<TestRow> rows) : IGridSource<TestRow>
    {
        private TestRow[]? _asked;

        public IReadOnlyList<TestRow> Window { get; private set; } = rows;

        public int WindowStart => 0;

        public int? TotalCount => Window.Count;

        public bool IsLoading => _asked is not null;

        public int RowSequenceVersion => 0;

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public event Action? StateChanged;

        /// <summary>The user's write to row <paramref name="row"/>'s Book goes to the server.</summary>
        public void Ask(int row, string book) => _asked = Changed(Window, row, book: book);

        /// <summary>The server's answer lands, carrying the write.</summary>
        public void Answer()
        {
            Window = _asked!;
            _asked = null;
            StateChanged?.Invoke();
        }

        /// <summary>A change upstream lands.</summary>
        public void Upstream(int row, string book)
        {
            Window = Changed(Window, row, book: book);
            StateChanged?.Invoke();
        }

        public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
        {
        }

        public void OnFilterChanged(GridFilter? filter)
        {
        }

        public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TestRow>> columns)
        {
        }

        public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

        public Task<IReadOnlyList<TestRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TestRow>>([.. Window.Skip(range.Start).Take(range.Count)]);

        public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
            => Task.FromResult(Chrome.DistinctValues.Of([]));
    }
}
