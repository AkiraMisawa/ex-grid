using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A write lands as the user entered it, on the row the user aimed it at (ADR-0142, rewritten
/// 2026-10-07; LV-11 to LV-14, LV-16, LV-17, LV-20). A gesture carries the render it was taken
/// against: the Viewport names that render (<c>data-ex-paint</c>), the browser tells it with each
/// gesture, and the core uses it for where the gesture lands and which order it was aimed under,
/// never to judge a value. Here a test tells a gesture an earlier render, as ED-31's tests tell a
/// press an earlier layout; what the browser reads is layer 3's.
///
/// 20px rows in a 120px Viewport (five rows painted), 350px wide: Book 0–100 and Amount 100–200,
/// both editable, and an Action Column 200–300 where a test asks for one.
/// </summary>
public class WritesLandAsEnteredTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    private static GridColumn<TestRow>[] WithAction() =>
    [
        .. Columns(),
        GridColumn<TestRow>.ActionColumn("Do", [new GridAction("approve", "Approve")], width: Fixed100),
    ];

    /// <summary>What the grid raised, in order of kind.</summary>
    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];
        public List<GridCommitRefusal> CommitRefusals { get; } = [];

        /// <summary>What the Consumer does with an Edit Intent beyond hearing it, if anything.</summary>
        public Action<GridEditIntent<TestRow>>? OnEdit { get; set; }

        /// <summary>What the Consumer does with an Action beyond hearing it, if anything.</summary>
        public Action<GridActionEventArgs<TestRow>>? OnAction { get; set; }
        public List<GridPasteIntent> Pastes { get; } = [];
        public List<PasteRefusalReason> PasteRefusals { get; } = [];
        public List<GridActionEventArgs<TestRow>> Actions { get; } = [];
        public List<GridActionRefusal<TestRow>> ActionRefusals { get; } = [];
        public List<GridFillIntent> Fills { get; } = [];
        public List<GridClearIntent> Clears { get; } = [];
        public List<GridOverwriteNotice> Notices { get; } = [];
        public List<EditDiscardReason> Discards { get; } = [];

        /// <summary>Whether the Consumer refuses every paste it hears (ADR-0050, item 3).</summary>
        public bool RefusePastes { get; set; }
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] rows, Heard heard, GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows)
              .Add(g => g.TotalCount, rows.Length)
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
              .Add(g => g.OnEditDiscarded, (EditDiscardReason r) => heard.Discards.Add(r))
              .Add(g => g.OnPaste, (GridPasteIntent i) =>
              {
                  heard.Pastes.Add(i);
                  if (heard.RefusePastes)
                      i.Refuse();
              })
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) =>
              {
                  heard.Actions.Add(a);
                  heard.OnAction?.Invoke(a);
              })
              .Add(g => g.OnActionRefused, (GridActionRefusal<TestRow> r) => heard.ActionRefusals.Add(r))
              .Add(g => g.OnFill, (GridFillIntent i) => heard.Fills.Add(i))
              .Add(g => g.OnClear, (GridClearIntent i) => heard.Clears.Add(i));
            extra?.Invoke(ps);
        });

    /// <summary>The rows with the one at <paramref name="index"/> replaced by a new instance, as a
    /// live source hands over a changed row (ADR-0003: a new instance is the change signal).</summary>
    private static TestRow[] Changed(TestRow[] rows, int index, string? book = null, decimal? amount = null)
    {
        var next = (TestRow[])rows.Clone();
        var old = rows[index];
        next[index] = new TestRow { Book = book ?? old.Book, Amount = amount ?? old.Amount, AsOf = old.AsOf, Active = old.Active };
        return next;
    }

    /// <summary>The rows with the one at <paramref name="from"/> moved to <paramref name="to"/>, as
    /// a new instance with <paramref name="amount"/> or its own values.</summary>
    private static TestRow[] Moved(TestRow[] rows, int from, int to, decimal? amount = null)
    {
        var list = rows.ToList();
        var old = list[from];
        list.RemoveAt(from);
        list.Insert(to, new TestRow { Book = old.Book, Amount = amount ?? old.Amount, AsOf = old.AsOf, Active = old.Active });
        return [.. list];
    }

    private static void Push(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] rows)
        => cut.Render(ps => ps.Add(g => g.Window, rows));

    /// <summary>Hands the grid <paramref name="rows"/> in a new order: the Row Sequence Version
    /// moves, and the Selection goes with the old order (ADR-0011).</summary>
    private static void Reorder(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] rows, int version = 1)
        => cut.Render(ps => ps.Add(g => g.Window, rows).Add(g => g.RowSequenceVersion, version));

    private static int Attribute(IRenderedComponent<ExGrid<TestRow>> cut, string name)
        => int.Parse(cut.Find(".ex-viewport").GetAttribute(name)!, CultureInfo.InvariantCulture);

    /// <summary>The render the Viewport says it was painted by.</summary>
    private static int Paint(IRenderedComponent<ExGrid<TestRow>> cut) => Attribute(cut, "data-ex-paint");

    private static Task KeyAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false, int paint = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false, paint: paint));

    private static Task DownAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift });

    private static Task UpAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
    {
        await DownAsync(cut, x, y, shift);
        await UpAsync(cut, x, y);
    }

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    private static Task PasteAsync(IRenderedComponent<ExGrid<TestRow>> cut, int paint)
        => cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", paint));

    /// <summary>Releases a fill-handle drag at (50, 70) — row 3 of Book — told the order and layout
    /// it was taken under, as the listener tells them (ED-31).</summary>
    private static async Task ReleaseFillAsync(IRenderedComponent<ExGrid<TestRow>> cut, int sequence, int layout)
    {
        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, Attribute(cut, "data-ex-first-row"), 0,
            sequence, layout));
        await UpAsync(cut, 50, 70);
    }

    /// <summary>Selects Book rows 0 and 1 and drags the fill handle down to row 3 (ADR-0050, item
    /// 5), short of the release: the handle's corner is at (100, 40).</summary>
    private static async Task DragFillAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
    }

    // ---- The render a gesture was taken against (LV-14's layer-2 half) ----

    [Fact] // ADR-0142 / LV-14: the Viewport names the render it was painted by, beside its order and layout, and a new row instance on screen is a new render
    public void The_viewport_names_its_render_and_a_changed_painted_row_names_a_new_one()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(rows, new Heard());
        var first = Paint(cut);

        Push(cut, Changed(rows, 1, book: "Moved"));

        Assert.NotEqual(first, Paint(cut));
    }

    [Fact] // ADR-0142 / LV-14: a render that changes no painted cell keeps the name
    public async Task A_render_that_changes_no_painted_cell_keeps_its_name()
    {
        var rows = TestRows.Many(50);
        var cut = RenderGrid(rows, new Heard());
        var first = Paint(cut);

        // A Selection is paint over the rows, never a cell's text (ADR-0008).
        await ClickAsync(cut, 50, 10);
        // A row the Viewport does not paint was not seen.
        Push(cut, Changed(rows, 40, book: "Moved"));

        Assert.Equal(first, Paint(cut));
    }

    // ---- LV-11: the Cell Editor ----

    [Fact] // ADR-0142 / LV-11: a commit whose cell paints other text than when the editor opened lands with the text typed, and raises one Overwrite Notice naming the cell and both texts, which the Edit Intent carries too
    public async Task A_commit_over_a_cell_that_changed_under_the_editor_lands_with_an_overwrite_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await TypeAsync(cut, "5x");
        var changed = Changed(rows, 0, book: "Moved upstream");

        Push(cut, changed);
        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.CommitRefusals);
        var intent = Assert.Single(heard.Edits);
        Assert.Equal("5x", intent.Value);
        Assert.Same(changed[0], intent.Row);
        Assert.Equal("Row 000000", intent.SeenText);
        Assert.Equal("Moved upstream", intent.ReplacedText);
        Assert.Equal(new GridOverwriteNotice(new CellPosition(0, 0), "Book", "Row 000000", "Moved upstream"), Assert.Single(heard.Notices));
        Assert.Empty(cut.FindAll(".ex-editor"));
        // The commit moved on as Enter does: the commit landed.
        Assert.Equal(new CellPosition(1, 0), cut.Instance.ReadSelection().Selection.Focus);
    }

    [Fact] // ADR-0142 / LV-11: a commit over an unchanged cell raises no notice, and its intent carries the same text as seen and as replaced
    public async Task A_commit_over_an_unchanged_cell_raises_no_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        await KeyAsync(cut, "Enter");

        var intent = Assert.Single(heard.Edits);
        Assert.Equal("Row 000000", intent.SeenText);
        Assert.Equal("Row 000000", intent.ReplacedText);
        Assert.Empty(heard.Notices);
        Assert.Empty(heard.CommitRefusals);
    }

    [Fact] // ADR-0142 / LV-11: two changes under the editor are told once, by the text the editor opened over and the text the commit replaced
    public async Task Two_changes_under_the_editor_are_told_once_by_the_text_they_left()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var once = Changed(rows, 0, book: "Once");
        Push(cut, once);
        Push(cut, Changed(once, 0, book: "Twice"));

        await KeyAsync(cut, "Enter");

        Assert.Single(heard.Edits);
        var notice = Assert.Single(heard.Notices);
        Assert.Equal("Row 000000", notice.SeenText);
        Assert.Equal("Twice", notice.ReplacedText);
    }

    [Fact] // ADR-0142 / LV-11: Escape over a cell that changed under the editor writes nothing, and tells nothing
    public async Task Escape_over_a_changed_cell_writes_nothing_and_raises_no_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Escape");

        Assert.Empty(heard.Edits);
        Assert.Empty(heard.Notices);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11: a change to another cell of the row is on screen: the commit lands on the row as it is now, and tells nothing
    public async Task A_change_to_another_cell_of_the_row_raises_no_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var changed = Changed(rows, 0, amount: 999_999m);
        Push(cut, changed);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        var intent = Assert.Single(heard.Edits);
        Assert.Same(changed[0], intent.Row);
    }

    [Fact] // ADR-0142 / LV-11, ADR-0034: the Edit Verdict still judges the row as it is at the commit
    public async Task The_edit_verdict_judges_the_row_as_it_is_at_the_commit()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var judged = new List<TestRow>();
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true,
                validate: (row, _) =>
                {
                    judged.Add(row);
                    return row.Amount > 1000 ? EditVerdict.Reject("too large to rename") : EditVerdict.Accept;
                }),
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        ];
        var cut = RenderGrid(rows, heard, columns);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var changed = Changed(rows, 0, amount: 5000m);
        Push(cut, changed);

        await KeyAsync(cut, "Enter");

        Assert.Same(changed[0], Assert.Single(judged));
        Assert.Empty(heard.Edits);
        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Single(cut.FindAll("input.ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11, ADR-0010: a press elsewhere commits over a changed cell, tells it, and keeps its own meaning
    public async Task A_press_that_commits_over_a_changed_cell_lands_with_the_notice_and_moves()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await DownAsync(cut, 150, 50);

        Assert.Equal("5", Assert.Single(heard.Edits).Value);
        Assert.Equal("Moved upstream", Assert.Single(heard.Notices).ReplacedText);
        Assert.Empty(cut.FindAll("input.ex-editor"));
        Assert.Equal(new CellPosition(2, 1), cut.Instance.ReadSelection().Selection.Focus);
    }

    // ---- LV-11: the editor keeps what the cell paints when it opens ----

    [Fact] // ADR-0142 / LV-11: a change in the round trip between the key that opens the editor and the open is not told — the editor opens over the new text, and the commit lands with no notice
    public async Task A_change_before_the_editor_opens_is_not_compared_and_the_commit_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, book: "Moved upstream");
        Push(cut, changed);

        // Typed on the render before the change, handled after it: the round trip the rewritten
        // rule accepts.
        await KeyAsync(cut, "5", paint: pressedOn);
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        var intent = Assert.Single(heard.Edits);
        Assert.Equal("5", intent.Value);
        Assert.Same(changed[0], intent.Row);
    }

    [Fact] // ADR-0142 / LV-11, ADR-0010: F2 taken before a change opens on the text it finds, and its commit lands
    public async Task F2_taken_before_a_change_opens_on_the_text_it_finds_and_its_commit_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "F2", paint: pressedOn);
        Assert.Equal("Moved upstream", cut.Find("input.ex-editor").GetAttribute("value"));
        await TypeAsync(cut, "Typed over");
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Equal("Typed over", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0142 / LV-11, ADR-0010: a double click taken before a change opens on the text it finds, and its commit lands
    public async Task A_double_click_taken_before_a_change_opens_on_the_text_it_finds_and_its_commit_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        var firstRow = Attribute(cut, "data-ex-first-row");
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        // The press of the double click is told the render it was made on, as the listener tells it.
        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mousedown", 50, 10, firstRow, 0, sequence, layout));
        await DownAsync(cut, 50, 10);
        await UpAsync(cut, 50, 10);
        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });
        Assert.Equal("Moved upstream", cut.Find("input.ex-editor").GetAttribute("value"));
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Single(heard.Edits);
    }

    [Fact] // ADR-0142 / LV-11, ADR-0051: a press into the Formula Bar taken before a change opens on the text it finds, and its commit lands
    public async Task A_press_into_the_formula_bar_taken_before_a_change_opens_on_the_text_it_finds_and_its_commit_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "Typed in the bar" });
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Equal("Typed in the bar", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0142 / LV-11, ADR-0080: a composition started before a change opens over the text it finds, and its commit lands
    public async Task A_composition_started_before_a_change_commits()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな"));
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Equal("かな", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0142 / LV-11: a key taken on a render many renders old opens the editor as any key does, and its commit lands at once — no render is kept to be lost
    public async Task An_editor_opened_by_a_key_on_a_render_long_replaced_commits_at_once()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        // Far more renders than the grid ever kept, none of which changes the edited cell's text.
        for (var i = 0; i < 200; i++)
        {
            rows = Changed(rows, 3);
            Push(cut, rows);
        }

        await KeyAsync(cut, "5", paint: pressedOn);
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Equal("5", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0142 / LV-11: an editor opened on a render that already painted the change commits
    public async Task A_key_taken_on_the_render_that_painted_the_change_commits()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "5", paint: Paint(cut));
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Equal("5", Assert.Single(heard.Edits).Value);
    }

    // ---- LV-12: an Action press acts on the row it was pressed on ----

    [Fact] // ADR-0142 / LV-12: a press taken on an earlier render of a row whose values changed since acts on that row as it is now, once
    public async Task An_action_press_on_a_row_changed_since_its_render_acts_on_it_as_it_is_now()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        var action = Assert.Single(heard.Actions);
        Assert.Same(changed[0], action.Row);
        Assert.Equal("Do", action.ColumnName);
        Assert.Equal("approve", action.ActionName);
    }

    [Fact] // ADR-0142 / LV-12, ADR-0003: without a Row Key, a told press whose button a render disposed acts on the row now at its position — once, even when the disposed component still delivers its click
    public async Task A_told_press_whose_button_was_disposed_acts_once_on_the_row_now_at_its_position()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        // The handler of the button painted then, whose component the new instance replaces.
        var olderHandler = cut.FindComponents<ExGridRow<TestRow>>().Single(r => ReferenceEquals(r.Instance.Row, rows[0])).Instance.OnAction!;
        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        await cut.InvokeAsync(() => olderHandler(new GridActionEventArgs<TestRow>(rows[0], "Do", "approve")));

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 / LV-12, ADR-0020: on a row that did not change, a press taken on an earlier render fires once, as before
    public async Task An_action_press_on_an_unchanged_row_fires_once()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Another row moved"));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        var action = Assert.Single(heard.Actions);
        Assert.Same(rows[0], action.Row);
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0142 / LV-12: a press told a render older than every render the grid keeps a serial for waits for its click, and acts on its row
    public async Task An_action_press_told_a_render_long_replaced_waits_for_its_click_and_acts()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        // Far more renders than are kept, each a new instance of a painted row.
        for (var i = 0; i < 200; i++)
        {
            rows = Changed(rows, 1);
            Push(cut, rows);
        }

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        Assert.Empty(heard.Actions);
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(rows[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 / LV-12: a press nobody told of acts on the row its button holds now
    public async Task An_action_press_nobody_told_of_acts_on_the_row_its_button_holds()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0142 / LV-12, LV-14, ADR-0037: Space fires an action by key, and the key carries its render: on a row changed since, under the same order, it acts on that row as it is now
    public async Task Space_taken_on_an_earlier_render_of_a_changed_row_acts_on_it()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, book: "Moved upstream");
        Push(cut, changed);

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
    }

    [Theory] // ADR-0142 / LV-20, LV-14, ADR-0011: Space names its row by the Focus, a position, so one taken under an order that has moved since is refused as OrderMoved, with a Row Key or without
    [InlineData(false)]
    [InlineData(true)]
    public async Task Space_taken_under_an_order_that_has_moved_since_is_refused(bool rowKey)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps =>
        {
            if (rowKey)
                ps.Add(g => g.RowKey, ByBook);
        });
        await ClickAsync(cut, 250, 10);
        var pressedOn = Paint(cut);
        Reorder(cut, Moved(rows, 0, 2));
        // A Focus placed again, under the new order: not the one the Space was aimed at.
        await ClickAsync(cut, 250, 10);

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.OrderMoved, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 / LV-12, ADR-0037: Space on an action whose row the view has scrolled away from fires on that row as it is now
    public async Task Space_on_an_action_whose_row_is_not_painted_fires()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        await ScrollToAsync(cut.Find(".ex-scroller"), 30 * 20);
        Assert.NotEqual(0, Attribute(cut, "data-ex-first-row"));
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, book: "Moved upstream");
        Push(cut, changed);

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0142 / LV-12: a change to any cell of the pressed row, painted or virtualised away, refuses nothing
    public async Task A_change_to_any_cell_of_the_pressed_row_refuses_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        // Off the right edge of a 350px Viewport: virtualised away, never painted.
        GridColumn<TestRow>[] columns =
        [
            .. WithAction(),
            new("Pad1", ColumnType.Text, _ => "", width: Fixed100),
            new("Pad2", ColumnType.Text, _ => "", width: Fixed100),
            new("Pad3", ColumnType.Text, _ => "", width: Fixed100),
            new("Pad4", ColumnType.Text, _ => "", width: Fixed100),
            new("Far", ColumnType.Number, r => r.Amount, width: Fixed100),
        ];
        var cut = RenderGrid(rows, heard, columns);
        var pressedOn = Paint(cut);
        // Amount is painted, Far is not; both change.
        var changed = Changed(rows, 0, book: "Moved upstream", amount: 4242m);
        Push(cut, changed);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
    }

    // ---- LV-13: a paste, a Ctrl+Enter fill, a fill-handle drag, Delete and the fill keys ----

    [Fact] // ADR-0142 / LV-13: a paste whose target changed after the render it was taken against lands as entered
    public async Task A_paste_whose_target_changed_since_its_render_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        await PasteAsync(cut, pressedOn);

        Assert.Empty(heard.PasteRefusals);
        var paste = Assert.Single(heard.Pastes);
        Assert.Equal(SelectionRange.FromCorners(new CellPosition(0, 0), new CellPosition(1, 0)), Assert.Single(paste.Plan.Targets));
    }

    [Fact] // ADR-0142 / LV-13: a change beside the target refuses nothing either
    public async Task A_paste_beside_a_change_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, amount: 31m));

        await PasteAsync(cut, pressedOn);

        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0014: a paste over a whole column lands whatever changed in it, painted or not
    public async Task A_paste_over_a_whole_column_lands_whatever_changed_in_it()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        // The whole Book column, rows 0 to 49: five of them painted.
        await KeyAsync(cut, "ArrowDown", ctrl: true, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(Changed(rows, 40, book: "Moved upstream"), 1, book: "Moved too"));

        await PasteAsync(cut, pressedOn);

        Assert.Equal(50, Assert.Single(heard.Pastes).CellCount);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0014: the existing refusals still hold, and say their own reason
    public async Task A_shape_refusal_still_says_its_own_reason()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        // A 2×1 block into three rows: not a whole multiple.
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("a\r\nb\r\n", null, pressedOn));

        Assert.Equal([PasteRefusalReason.ShapeMismatch], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, LV-14: a paste taken on a render that many newer renders replaced, under the same order, lands where it was aimed
    public async Task A_paste_taken_on_a_render_long_replaced_under_the_same_order_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        for (var i = 0; i < 200; i++)
        {
            rows = Changed(rows, 3);
            Push(cut, rows);
        }

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null, pressedOn));

        Assert.Empty(heard.PasteRefusals);
        var paste = Assert.Single(heard.Pastes);
        Assert.Equal(SelectionRange.FromCorners(new CellPosition(0, 0), new CellPosition(0, 0)), Assert.Single(paste.Plan.Targets));
    }

    [Fact] // ADR-0142 / LV-13, LV-14, ADR-0011: a paste aimed under an order that has moved since is refused: the Selection it was aimed with went with that order
    public async Task A_paste_aimed_under_an_order_that_has_moved_since_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        Reorder(cut, Moved(rows, 0, 2));
        // A Selection made again, under the new order: not the one the paste was aimed with.
        await ClickAsync(cut, 50, 10);

        await PasteAsync(cut, pressedOn);

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.EmptySelection], heard.PasteRefusals);

        // One taken under the order in force lands.
        await PasteAsync(cut, Paint(cut));
        Assert.Single(heard.Pastes);
    }

    [Fact] // ADR-0142 / LV-13: a Ctrl+Enter fill whose target changed since the key's render lands; only the edited cell, under the editor, is the editor's to judge
    public async Task A_ctrl_enter_fill_over_a_changed_target_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await KeyAsync(cut, "z");
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        await KeyAsync(cut, "Enter", ctrl: true, paint: pressedOn);

        Assert.Empty(heard.PasteRefusals);
        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        Assert.Equal("z", Assert.Single(Assert.Single(Assert.Single(heard.Pastes).Values)));
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11, LV-13: a Ctrl+Enter fill whose edited cell changed under the editor lands, and tells the change
    public async Task A_ctrl_enter_fill_whose_edited_cell_changed_lands_with_the_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await KeyAsync(cut, "z");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Enter", ctrl: true, paint: Paint(cut));

        Assert.Empty(heard.PasteRefusals);
        Assert.Empty(heard.CommitRefusals);
        Assert.Equal("z", Assert.Single(Assert.Single(Assert.Single(heard.Pastes).Values)));
        Assert.Equal(new GridOverwriteNotice(new CellPosition(0, 0), "Book", "Row 000000", "Moved upstream"), Assert.Single(heard.Notices));
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11, ADR-0050 item 3: a Ctrl+Enter fill the Consumer refused wrote nothing, and tells nothing
    public async Task A_ctrl_enter_fill_the_consumer_refused_raises_no_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { RefusePastes = true };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await KeyAsync(cut, "z");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Enter", ctrl: true, paint: Paint(cut));

        Assert.Single(heard.Pastes);
        Assert.Empty(heard.Notices);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0050 item 5: a fill-handle drag released on a render whose target has changed since lands
    public async Task A_fill_drag_released_on_an_earlier_render_of_a_changed_target_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await DragFillAsync(cut);
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        Push(cut, Changed(rows, 3, book: "Moved upstream"));

        await ReleaseFillAsync(cut, sequence, layout);

        Assert.Single(heard.Fills);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0050 item 5: a fill-handle drag whose source changed since the release's render lands, and writes the source as it is now
    public async Task A_fill_drag_whose_source_changed_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await DragFillAsync(cut);
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        // Row 0 is the source's, not the target's (rows 2 and 3).
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await ReleaseFillAsync(cut, sequence, layout);

        var fill = Assert.Single(heard.Fills);
        Assert.Equal(SelectionRange.FromCorners(new CellPosition(2, 0), new CellPosition(3, 0)), fill.Target);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0011: a fill-handle drag released after the order moved raises nothing, as before
    public async Task A_fill_drag_released_after_the_order_moved_raises_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await DragFillAsync(cut);
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        Reorder(cut, Moved(rows, 0, 2));

        await ReleaseFillAsync(cut, sequence, layout);

        Assert.Empty(heard.Fills);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0054: Delete writes too — a Clear over a target changed since its key lands
    public async Task A_clear_over_a_changed_target_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        await KeyAsync(cut, "Delete", paint: pressedOn);

        Assert.Single(heard.Clears);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0035: Ctrl+D fills too — a fill key over a target changed since its key lands
    public async Task A_fill_key_over_a_changed_target_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 2, book: "Moved upstream"));

        await KeyAsync(cut, "d", ctrl: true, paint: pressedOn);

        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0035: Ctrl+R fills too — a fill key over a target changed since its key lands
    public async Task A_fill_right_key_over_a_changed_target_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        // Book and Amount of row 0: Ctrl+R reads Book and writes Amount.
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 150, 10, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, amount: 31m));

        await KeyAsync(cut, "r", ctrl: true, paint: pressedOn);

        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13: Ctrl+D whose source changed since the key fills with the source as it is now
    public async Task A_fill_down_whose_source_changed_fills_with_the_source_as_it_is_now()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        // Book, rows 0 to 2: Ctrl+D reads row 0 and writes rows 1 and 2.
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "d", ctrl: true, paint: pressedOn);

        Assert.Empty(heard.PasteRefusals);
        Assert.Equal("Moved upstream", Assert.Single(Assert.Single(Assert.Single(heard.Pastes).Values)));
    }

    [Fact] // ADR-0142 / LV-13: Ctrl+R whose source changed since the key fills with the source as it is now, as Ctrl+D does
    public async Task A_fill_right_whose_source_changed_fills_with_the_source_as_it_is_now()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 150, 10, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "r", ctrl: true, paint: pressedOn);

        Assert.Empty(heard.PasteRefusals);
        Assert.Equal("Moved upstream", Assert.Single(Assert.Single(Assert.Single(heard.Pastes).Values)));
    }

    [Fact] // ADR-0142 / LV-13: a fill whose source and target are as they were painted still fills, the change beside them notwithstanding
    public async Task A_fill_down_whose_source_and_target_did_not_change_is_written()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, amount: 31m));

        await KeyAsync(cut, "d", ctrl: true, paint: pressedOn);

        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
    }

    [Theory] // ADR-0142 / LV-13, LV-14, ADR-0011: Delete, Ctrl+D and Ctrl+R taken under an order that has moved since are refused, as a paste is
    [InlineData("Delete", false)]
    [InlineData("d", true)]
    [InlineData("r", true)]
    public async Task A_write_key_aimed_under_an_order_that_has_moved_since_is_refused(string key, bool ctrl)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 30);
        await ClickAsync(cut, 150, 50, shift: true);
        var pressedOn = Paint(cut);
        Reorder(cut, Moved(rows, 0, 4));
        await ClickAsync(cut, 50, 30);
        await ClickAsync(cut, 150, 50, shift: true);

        await KeyAsync(cut, key, ctrl: ctrl, paint: pressedOn);

        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Clears);
        Assert.Equal([PasteRefusalReason.EmptySelection], heard.PasteRefusals);
    }

    // ---- Writes typed at once, before the user's own write is painted (LV-17's bulk half) ----

    [Fact] // ADR-0142 / LV-17, LV-11: `1` Enter ↑ `2` Enter typed at once — every key taken on the render before the 1 was painted — writes both
    public async Task One_enter_up_two_enter_typed_at_once_commits_both()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "1", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        // The Consumer writes the 1: a new instance, painted by a render none of the keys after the
        // Enter were taken on.
        Push(cut, Changed(rows, 0, book: "1"));
        Assert.NotEqual(taken, Paint(cut));
        await KeyAsync(cut, "ArrowUp", paint: taken);
        await KeyAsync(cut, "2", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);

        Assert.Empty(heard.CommitRefusals);
        Assert.Equal(["1", "2"], heard.Edits.Select(e => e.Value));
    }

    [Fact] // ADR-0142 / LV-13, LV-17: `5` Enter ↑ Ctrl+V typed at once pastes on the cell it was aimed at
    public async Task Five_enter_up_paste_typed_at_once_pastes()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "5", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        Push(cut, Changed(rows, 0, book: "5"));
        await KeyAsync(cut, "ArrowUp", paint: taken);
        await PasteAsync(cut, taken);

        Assert.Empty(heard.PasteRefusals);
        var paste = Assert.Single(heard.Pastes);
        Assert.Equal(new CellPosition(0, 0), new CellPosition(paste.Plan.Targets[0].TopRow, paste.Plan.Targets[0].LeftColumn));
    }

    [Fact] // ADR-0142 / LV-13: a paste over the user's own write and a change upstream beside it lands over both
    public async Task A_paste_over_the_users_own_write_and_a_change_beside_it_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "5", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        var written = Changed(rows, 0, book: "5");
        Push(cut, written);
        Push(cut, Changed(written, 1, book: "Moved upstream"));
        await KeyAsync(cut, "ArrowUp", paint: taken);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: taken);
        await PasteAsync(cut, taken);

        Assert.Empty(heard.PasteRefusals);
        Assert.Equal(2, Assert.Single(heard.Pastes).CellCount);
    }

    [Fact] // ADR-0142 / LV-13: a paste taken after the user's write was painted lands over a change upstream that came after it
    public async Task A_paste_over_a_change_after_the_users_write_was_painted_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5", paint: Paint(cut));
        await KeyAsync(cut, "Enter", paint: Paint(cut));
        var written = Changed(rows, 0, book: "5");
        Push(cut, written);
        var seen = Paint(cut);
        Push(cut, Changed(written, 0, book: "Moved upstream"));
        await KeyAsync(cut, "ArrowUp", paint: seen);

        await PasteAsync(cut, seen);

        Assert.Empty(heard.PasteRefusals);
        Assert.Single(heard.Pastes);
    }

    [Fact] // ADR-0142 / LV-13, LV-17: a fill from a cell the user just wrote fills with what they wrote — `5` Enter, then Ctrl+D from it, at once
    public async Task A_fill_from_a_cell_the_user_just_wrote_fills()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "5", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        Push(cut, Changed(rows, 0, book: "5"));
        await KeyAsync(cut, "ArrowUp", paint: taken);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: taken);
        await KeyAsync(cut, "ArrowDown", shift: true, paint: taken);
        await KeyAsync(cut, "d", ctrl: true, paint: taken);

        Assert.Empty(heard.PasteRefusals);
        Assert.Equal("5", Assert.Single(Assert.Single(Assert.Single(heard.Pastes).Values)));
    }

    [Fact] // ADR-0142 / LV-12, LV-17: an Action press on a row the user's own commit just wrote fires on the row as written
    public async Task An_action_press_on_a_row_the_user_just_wrote_fires()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        await ClickAsync(cut, 150, 10);
        var taken = Paint(cut);

        await KeyAsync(cut, "5", paint: taken);
        await KeyAsync(cut, "Enter", paint: taken);
        var written = Changed(rows, 0, amount: 5m);
        Push(cut, written);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(taken));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(written[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0050 item 3: after a paste the Consumer refused, a Delete over a cell changed since lands
    public async Task A_delete_after_a_paste_the_consumer_refused_lands()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { RefusePastes = true };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);
        await PasteAsync(cut, taken);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Delete", paint: taken);

        Assert.Single(heard.Clears);
        Assert.Empty(heard.PasteRefusals);
    }

    // ---- LV-17 (D1): the user's own writes count as seen for the Overwrite Notice ----

    [Fact] // ADR-0142 D1 / LV-17: an editor opened before the user's own write was painted tells nothing when that write paints under it — `5` Enter ↑ `7` Enter, typed at once
    public async Task An_editor_opened_before_the_users_own_write_was_painted_raises_no_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);

        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");
        // The Consumer's store writes the 5 back only now, under the open editor.
        Push(cut, Changed(rows, 0, book: "5"));
        await KeyAsync(cut, "Enter");

        Assert.Equal(["5", "7"], heard.Edits.Select(e => e.Value));
        Assert.Empty(heard.Notices);
        Assert.Empty(heard.CommitRefusals);
    }

    [Fact] // ADR-0142 D1 / LV-17: once the user's write is painted, an editor opened over it tells a change upstream that comes after
    public async Task An_editor_opened_after_the_users_write_was_painted_tells_a_change_after_it()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        var written = Changed(rows, 0, book: "5");
        Push(cut, written);
        await KeyAsync(cut, "ArrowUp");
        await KeyAsync(cut, "7");

        Push(cut, Changed(written, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal("5", notice.SeenText);
        Assert.Equal("Moved upstream", notice.ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17: only the cells the user wrote are let off — an editor opened on the cell beside tells its change
    public async Task Only_the_cells_the_users_own_write_covered_are_let_off()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        // Enter moves to row 1, which the 5 did not write.
        await KeyAsync(cut, "Enter");
        await KeyAsync(cut, "7");

        Push(cut, Changed(Changed(rows, 0, book: "5"), 1, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        var notice = Assert.Single(heard.Notices);
        Assert.Equal(new CellPosition(1, 0), notice.Cell);
    }

    [Fact] // ADR-0142 D1 / LV-17, ADR-0050 item 3: a paste the Consumer refused wrote nothing, so an editor opened over its cell tells a change there
    public async Task A_write_the_consumer_refused_lets_nothing_off()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { RefusePastes = true };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await PasteAsync(cut, Paint(cut));
        await KeyAsync(cut, "7");

        Push(cut, Changed(rows, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        Assert.Equal("Moved upstream", Assert.Single(heard.Notices).ReplacedText);
    }

    [Fact] // ADR-0142 D1 / LV-17: a paste of the user's own, not yet painted, counts as seen too
    public async Task An_editor_opened_before_the_users_own_paste_was_painted_raises_no_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await PasteAsync(cut, Paint(cut));
        await KeyAsync(cut, "7");

        Push(cut, Changed(rows, 0, book: "x"));
        await KeyAsync(cut, "Enter");

        Assert.Single(heard.Edits);
        Assert.Empty(heard.Notices);
    }

    // ---- LV-19: the Consumer may refuse an edit ----

    [Fact] // ADR-0142 / LV-19, ADR-0034: Refuse(message) before the handler completes holds the editor with the typing, shows the message at the editor and in the live region, and the gesture moves nothing
    public async Task A_refused_intent_holds_the_editor_with_the_typing_and_the_message()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { OnEdit = intent => intent.Refuse("The book is closed for edits.") };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await TypeAsync(cut, "5x");

        await KeyAsync(cut, "Enter");

        Assert.True(Assert.Single(heard.Edits).IsRefused);
        var editor = cut.Find("input.ex-editor");
        Assert.Equal("5x", editor.GetAttribute("value"));
        Assert.Equal("true", editor.GetAttribute("aria-invalid"));
        Assert.Equal("The book is closed for edits.", cut.Find(".ex-announce").TextContent);
        Assert.Equal(new CellPosition(0, 0), cut.Instance.ReadSelection().Selection.Focus);
    }

    [Fact] // ADR-0142 / LV-19: after a refusal, a later commit raises the intent again, and a handler that completes without refusing has accepted
    public async Task A_later_commit_after_a_refusal_raises_the_intent_again()
    {
        var rows = TestRows.Many(50);
        var refuse = true;
        var heard = new Heard();
        heard.OnEdit = intent =>
        {
            if (refuse)
                intent.Refuse("Not yet.");
        };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");
        Assert.Single(cut.FindAll("input.ex-editor"));

        refuse = false;
        await KeyAsync(cut, "Enter");

        Assert.Equal(2, heard.Edits.Count);
        Assert.False(heard.Edits[1].IsRefused);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-19: a refusal made after the handler awaited, before it completed, still holds the editor
    public async Task A_refusal_after_the_handler_awaited_still_holds_the_editor()
    {
        var rows = TestRows.Many(50);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, rows.Length)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, async (GridEditIntent<TestRow> intent) =>
            {
                await Task.Yield();
                intent.Refuse("The server holds a newer version.");
            }));
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        await KeyAsync(cut, "Enter");

        cut.WaitForAssertion(() => Assert.Equal("The server holds a newer version.", cut.Find(".ex-announce").TextContent));
        Assert.Equal("5", cut.Find("input.ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0142 / LV-19, LV-11: a refused commit over a changed cell landed nothing, and tells nothing
    public async Task A_refused_commit_raises_no_overwrite_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { OnEdit = intent => intent.Refuse("Changed upstream; look again.") };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Enter");

        var intent = Assert.Single(heard.Edits);
        Assert.Equal("Row 000000", intent.SeenText);
        Assert.Equal("Moved upstream", intent.ReplacedText);
        Assert.Empty(heard.Notices);
        Assert.Single(cut.FindAll("input.ex-editor"));
    }

    [Fact] // ADR-0142 / LV-19: Escape after a refused intent writes nothing
    public async Task Escape_after_a_refused_intent_writes_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { OnEdit = intent => intent.Refuse("No.") };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await KeyAsync(cut, "Enter");

        await KeyAsync(cut, "Escape");

        Assert.Single(heard.Edits);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-19, ADR-0034: a refusal that says nothing is not one
    public void A_refusal_without_a_message_is_refused()
    {
        var intent = new GridEditIntent<TestRow>(TestRows.Many(1)[0], "Book", "5");

        Assert.Throws<ArgumentException>(() => intent.Refuse(" "));
        Assert.False(intent.IsRefused);
    }

    // ---- LV-20: a commit goes to the row the editor was opened on ----

    [Fact] // ADR-0142 / LV-20, ADR-0011 / ED-21: without a Row Key, a row that left the Window under the same order is not RowGone — that is a key no longer in the Window — and takes the typing with it, announced as RowLeftTheWindow
    public async Task Without_a_row_key_a_commit_whose_row_left_the_window_is_discarded_as_before()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        // The Window slides past row 0, the order unmoved.
        cut.Render(ps => ps.Add(g => g.Window, rows[1..]).Add(g => g.WindowStart, 1));

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Empty(heard.CommitRefusals);
        Assert.Equal([EditDiscardReason.RowLeftTheWindow], heard.Discards);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-20, ADR-0011: an order move that lands while the editor is open, outside a commit, still discards the edit, as ADR-0011 has it
    public async Task An_order_move_outside_a_commit_still_discards_the_edit()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        Reorder(cut, Moved(rows, 0, 2));

        Assert.Empty(cut.FindAll(".ex-editor"));
        cut.WaitForAssertion(() => Assert.Equal([EditDiscardReason.OrderChanged], heard.Discards));
        Assert.Empty(heard.Edits);
    }

    // ---- LV-16 (D5): a bound source puts out what it has gathered before a write is handled ----

    /// <summary>A source that holds a change it has gathered until it is asked to put it out, as
    /// <c>GridSource.From</c> holds the changes of a gather interval (ADR-0141), and takes a
    /// Consumer's write at once, as its <c>ReplaceRow</c> does.</summary>
    private sealed class GatheringSource(TestRow[] rows) : IGridSource<TestRow>
    {
        private IReadOnlyList<TestRow>? _gathered;
        private int? _gatheredVersion;

        public IReadOnlyList<TestRow> Window { get; private set; } = rows;

        public int WindowStart => 0;

        public int? TotalCount => Window.Count;

        public bool IsLoading => false;

        public int RowSequenceVersion { get; private set; }

        public IReadOnlyList<SortSpec> Sorts => [];

        public GridFilter? Filter => null;

        public int Asked { get; private set; }

        public event Action? StateChanged;

        /// <summary>Holds <paramref name="window"/> as gathered and not yet published.</summary>
        public void Gather(IReadOnlyList<TestRow> window, int? version = null)
        {
            _gathered = window;
            _gatheredVersion = version;
        }

        public void PublishGathered()
        {
            Asked++;
            if (_gathered is not { } next)
                return;
            _gathered = null;
            Window = next;
            RowSequenceVersion = _gatheredVersion ?? RowSequenceVersion;
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

    private IRenderedComponent<ExGrid<TestRow>> RenderSourceGrid(
        IGridSource<TestRow> source, Heard heard, GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
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
              .Add(g => g.OnEditDiscarded, (EditDiscardReason r) => heard.Discards.Add(r))
              .Add(g => g.OnPaste, (GridPasteIntent i) =>
              {
                  heard.Pastes.Add(i);
                  if (heard.RefusePastes)
                      i.Refuse();
              })
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) =>
              {
                  heard.Actions.Add(a);
                  heard.OnAction?.Invoke(a);
              })
              .Add(g => g.OnActionRefused, (GridActionRefusal<TestRow> r) => heard.ActionRefusals.Add(r))
              .Add(g => g.OnFill, (GridFillIntent i) => heard.Fills.Add(i))
              .Add(g => g.OnClear, (GridClearIntent i) => heard.Clears.Add(i));
            extra?.Invoke(ps);
        });

    [Fact] // ADR-0142 D5 / LV-16: a commit made while the source holds a gathered change to another cell asks for it first, lands, and its Edit Intent carries the newest row
    public async Task A_commit_inside_a_gather_interval_lands_with_the_newest_row()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var gathered = Changed(rows, 0, amount: 999_999m);
        source.Gather(gathered);

        await KeyAsync(cut, "Enter");

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Notices);
        var intent = Assert.Single(heard.Edits);
        Assert.Same(gathered[0], intent.Row);
        Assert.Equal("5", intent.Value);
        // The newest version is painted, not only judged.
        Assert.Contains(cut.FindAll(".ex-row")[0].QuerySelectorAll("[role=gridcell]"), c => c.TextContent.Replace(",", "") == "999999");
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-11: a gathered change to the edited cell itself lands the commit on the newest row, and raises the notice with the new text
    public async Task A_gathered_change_to_the_edited_cell_lands_the_commit_with_the_notice()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var gathered = Changed(rows, 0, book: "Gathered upstream");
        source.Gather(gathered);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.CommitRefusals);
        var intent = Assert.Single(heard.Edits);
        Assert.Same(gathered[0], intent.Row);
        Assert.Equal("5", intent.Value);
        var notice = Assert.Single(heard.Notices);
        Assert.Equal("Row 000000", notice.SeenText);
        Assert.Equal("Gathered upstream", notice.ReplacedText);
    }

    // The bundled source behind LV-16, end to end: GridSource.From keyed by Book, gathering on the
    // test's clock, and a Consumer whose OnEdit writes the commit back through ReplaceRow, as the
    // reference Consumer does. Before D5 the grid's check passed on the painted row and ReplaceRow
    // then threw, because a gathered change had replaced the row the intent carried.
    private static (InMemoryGridSource<TestRow> Source, List<Exception> Thrown) WritingBack(TestRow[] rows, Heard heard, Microsoft.Extensions.Time.Testing.FakeTimeProvider clock)
    {
        var source = GridSource.From(rows, r => r.Book, clock);
        var thrown = new List<Exception>();
        heard.OnEdit = intent =>
        {
            try
            {
                var row = intent.Row;
                source.ReplaceRow(row, new TestRow
                {
                    Book = row.Book, Amount = decimal.Parse(intent.Value, CultureInfo.InvariantCulture), AsOf = row.AsOf, Active = row.Active,
                });
            }
            catch (Exception error)
            {
                thrown.Add(error);
            }
        };
        return (source, thrown);
    }

    [Fact] // ADR-0141/0142 D5 / LV-16: with GridSource.From gathering, a commit lands on the newest row, and the gathered change to another field survives the write
    public async Task A_commit_inside_GridSource_Froms_gather_interval_lands_and_keeps_the_gathered_change()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var (source, thrown) = WritingBack(rows, heard, Clock);
        var cut = RenderSourceGrid(source, heard);
        source.Apply(new(changed: [Changed(rows, 5, amount: 55m)[5]]));
        await ClickAsync(cut, 150, 10);
        await KeyAsync(cut, "5");
        var later = new DateTime(2030, 1, 1);
        source.Apply(new(changed: [new TestRow { Book = rows[0].Book, Amount = rows[0].Amount, AsOf = later, Active = rows[0].Active }]));
        Assert.NotEqual(later, source.Window[0].AsOf);

        await KeyAsync(cut, "Enter");

        Assert.Empty(thrown);
        Assert.Empty(heard.CommitRefusals);
        Assert.Equal(later, Assert.Single(heard.Edits).Row.AsOf);
        Assert.Equal(5m, source.Window[0].Amount);
        Assert.Equal(later, source.Window[0].AsOf);
    }

    [Fact] // ADR-0141/0142 D5 / LV-16, LV-11: with GridSource.From gathering, a gathered change to the edited cell lands the commit over it, written back through ReplaceRow, and raises the notice with the new text
    public async Task A_gathered_change_to_the_edited_cell_in_GridSource_From_lands_with_the_notice()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var (source, thrown) = WritingBack(rows, heard, Clock);
        var cut = RenderSourceGrid(source, heard);
        source.Apply(new(changed: [Changed(rows, 5, amount: 55m)[5]]));
        await ClickAsync(cut, 150, 10);
        await KeyAsync(cut, "5");
        source.Apply(new(changed: [Changed(rows, 0, amount: 777m)[0]]));

        await KeyAsync(cut, "Enter");

        Assert.Empty(thrown);
        Assert.Empty(heard.CommitRefusals);
        Assert.Single(heard.Edits);
        Assert.Equal(5m, source.Window[0].Amount);
        var notice = Assert.Single(heard.Notices);
        Assert.Equal("0", notice.SeenText);
        Assert.Equal("777", notice.ReplacedText);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-20: without a Row Key, a gathered change that moves the order refuses the commit as OrderMoved: no intent, the editor stays with the typing, and Escape writes nothing
    public async Task Without_a_row_key_a_gathered_change_that_moves_the_order_refuses_the_commit()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        source.Gather([.. Enumerable.Reverse(rows)], version: 1);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Equal(CommitRefusalReason.OrderMoved, Assert.Single(heard.CommitRefusals).Reason);
        Assert.Equal("5", cut.Find("input.ex-editor").GetAttribute("value"));
        Assert.Empty(heard.Discards);

        // Held from then on: a commit is refused again, and a further move discards nothing.
        await KeyAsync(cut, "Enter");
        Assert.Equal(2, heard.CommitRefusals.Count);
        source.Gather([.. rows], version: 2);
        await KeyAsync(cut, "Enter");
        Assert.Empty(heard.Edits);
        Assert.Empty(heard.Discards);
        Assert.Single(cut.FindAll("input.ex-editor"));

        await KeyAsync(cut, "Escape");
        Assert.Empty(heard.Edits);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-20, ADR-0140: with a Row Key, a commit whose row a gathered change only moved lands on that row
    public async Task With_a_row_key_a_commit_whose_row_only_moved_lands_on_that_row()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, extra: ps => ps.Add(g => g.RowKey, ByBook));
        await ClickAsync(cut, 50, 30);
        await KeyAsync(cut, "5");
        var reversed = Enumerable.Reverse(rows).ToArray();
        source.Gather(reversed, version: 1);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(heard.Discards);
        var intent = Assert.Single(heard.Edits);
        Assert.Same(rows[1], intent.Row);
        Assert.Equal("5", intent.Value);
        Assert.Empty(heard.Notices);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-20: with a Row Key, a commit whose row a gathered change took out of the Window is refused as RowGone, the editor held, and every later commit too
    public async Task With_a_row_key_a_commit_whose_row_left_the_window_is_refused_as_row_gone()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, extra: ps => ps.Add(g => g.RowKey, ByBook));
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        source.Gather(rows[1..], version: 1);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Equal(CommitRefusalReason.RowGone, Assert.Single(heard.CommitRefusals).Reason);
        Assert.Equal("5", cut.Find("input.ex-editor").GetAttribute("value"));

        // The row's key went with the refusal: a commit is refused again, whatever stands there now.
        await KeyAsync(cut, "Enter");
        Assert.Empty(heard.Edits);
        Assert.Equal([CommitRefusalReason.RowGone, CommitRefusalReason.RowGone], heard.CommitRefusals.Select(r => r.Reason));
        Assert.Empty(heard.Discards);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-13: a paste asks for the gathered change first, and lands on the newest version
    public async Task A_paste_asks_for_the_gathered_change_first_and_lands()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        source.Gather(Changed(rows, 0, book: "Gathered upstream"));

        await PasteAsync(cut, pressedOn);

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.PasteRefusals);
        Assert.Single(heard.Pastes);
        Assert.Equal("Gathered upstream", source.Window[0].Book);
    }

    [Theory] // ADR-0142 D5 / LV-16, LV-13: Delete, Ctrl+D and Ctrl+R ask for the gathered change first, and land
    [InlineData("Delete", false)]
    [InlineData("d", true)]
    [InlineData("r", true)]
    public async Task A_write_key_asks_for_the_gathered_change_first_and_lands(string key, bool ctrl)
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        // Book and Amount of rows 1 and 2: each key writes into row 2's Amount.
        await ClickAsync(cut, 50, 30);
        await ClickAsync(cut, 150, 50, shift: true);
        var pressedOn = Paint(cut);
        source.Gather(Changed(rows, 2, amount: 31m));

        await KeyAsync(cut, key, ctrl: ctrl, paint: pressedOn);

        Assert.Equal(1, source.Asked);
        Assert.Equal(1, heard.Pastes.Count + heard.Clears.Count);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-13: a fill-handle drag asks for the gathered change first, and lands
    public async Task A_fill_drag_asks_for_the_gathered_change_first_and_lands()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await DragFillAsync(cut);
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        source.Gather(Changed(rows, 3, book: "Gathered upstream"));

        await ReleaseFillAsync(cut, sequence, layout);

        Assert.Equal(1, source.Asked);
        Assert.Single(heard.Fills);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-13: a Ctrl+Enter fill asks for the gathered change first, and lands
    public async Task A_ctrl_enter_fill_asks_for_the_gathered_change_first_and_lands()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await KeyAsync(cut, "z");
        var pressedOn = Paint(cut);
        source.Gather(Changed(rows, 1, book: "Gathered upstream"));

        await KeyAsync(cut, "Enter", ctrl: true, paint: pressedOn);

        Assert.Equal(1, source.Asked);
        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    // Blazor does not deliver an event whose attribute a since-disposed component rendered. Without
    // a Row Key, a row whose instance a render replaced has its component disposed, and the click on
    // its button never arrives: the core answers the press it was told of (ActionPressTakenAt).

    [Fact] // ADR-0142 / LV-12, "No press is lost to Blazor": a told press whose row component is gone is answered by the core, on the row now at its position, with no click to wait for
    public async Task A_told_press_whose_row_component_is_gone_is_answered_on_the_row_at_its_position()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        Assert.Empty(heard.ActionRefusals);
        var action = Assert.Single(heard.Actions);
        Assert.Same(changed[0], action.Row);
        Assert.Equal("approve", action.ActionName);
    }

    [Fact] // ADR-0142 / LV-12: a told press whose row is still rendered waits for its click, and fires once
    public async Task A_told_press_whose_row_is_still_rendered_waits_for_its_click()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        Assert.Empty(heard.Actions);
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());
        // A render after the click answers nothing more.
        Push(cut, Changed(rows, 0, amount: 777m));

        Assert.Same(rows[0], Assert.Single(heard.Actions).Row);
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0142 / LV-12: a told press whose row component a later render disposes before its click is answered after that render, on the row now at its position
    public async Task A_told_press_whose_row_a_later_render_replaces_is_answered_after_it()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        Assert.Empty(heard.Actions);

        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        cut.WaitForAssertion(() => Assert.Same(changed[0], Assert.Single(heard.Actions).Row));
        Assert.Empty(heard.ActionRefusals);
    }

    [Fact] // ADR-0142 / LV-12: a told press whose row came back as an equal new instance is fired by the core, with the newest row
    public async Task A_told_press_on_a_row_replaced_by_an_equal_one_is_fired_by_the_core()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        var same = Changed(rows, 0);
        Push(cut, same);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(same[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 / LV-20, LV-12: without a Row Key, a told press whose component is gone and whose order moved is refused as OrderMoved
    public async Task Without_a_row_key_a_told_press_whose_order_moved_is_refused_as_order_moved()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        // Row 0 moved as a new instance: its component is gone, and position 0 names another row.
        Reorder(cut, Moved(rows, 0, 2));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(ActionRefusalReason.OrderMoved, refusal.Reason);
        Assert.Equal("approve", refusal.Action.ActionName);
    }

    [Fact] // ADR-0142 / LV-20, LV-12: without a Row Key, a told press whose row left the Window, under the same order, is refused as RowGone after the render that took it away
    public async Task Without_a_row_key_a_told_press_whose_row_left_the_window_is_refused_as_row_gone()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));

        // The Window slides past row 0, the order unmoved.
        cut.Render(ps => ps.Add(g => g.Window, rows[1..]).Add(g => g.WindowStart, 1));

        cut.WaitForAssertion(() => Assert.Equal(ActionRefusalReason.RowGone, Assert.Single(heard.ActionRefusals).Reason));
        Assert.Same(rows[0], heard.ActionRefusals[0].Action.Row);
        Assert.Empty(heard.Actions);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-12: an Action press asks for the gathered change first, and acts on the row as it gathered
    public async Task An_action_press_asks_for_the_gathered_change_first_and_acts_on_it()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        var pressedOn = Paint(cut);
        var gathered = Changed(rows, 0, amount: 777m);
        source.Gather(gathered);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.ActionRefusals);
        Assert.Same(gathered[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-12: a press fires with the newest version, as an Edit Intent carries it
    public async Task An_action_press_fires_with_the_gathered_row()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        var pressedOn = Paint(cut);
        // A change to a field no column paints.
        var gathered = (TestRow[])rows.Clone();
        gathered[0] = new TestRow { Book = rows[0].Book, Amount = rows[0].Amount, AsOf = new DateTime(2030, 1, 1), Active = rows[0].Active };
        source.Gather(gathered);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(gathered[0], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0141/0142 D5 / LV-16: with GridSource.From gathering, an Action whose handler writes its row back is not refused as stale
    public async Task An_action_on_GridSource_From_writes_back_after_a_gathered_change()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var source = GridSource.From(rows, r => r.Book, Clock);
        var thrown = new List<Exception>();
        heard.OnAction = action =>
        {
            try
            {
                var row = action.Row;
                source.ReplaceRow(row, new TestRow { Book = row.Book, Amount = 0m, AsOf = row.AsOf, Active = row.Active });
            }
            catch (Exception error)
            {
                thrown.Add(error);
            }
        };
        var cut = RenderSourceGrid(source, heard, WithAction());
        source.Apply(new(changed: [Changed(rows, 5, amount: 55m)[5]]));
        var pressedOn = Paint(cut);
        var later = new DateTime(2030, 1, 1);
        source.Apply(new(changed: [new TestRow { Book = rows[0].Book, Amount = rows[0].Amount, AsOf = later, Active = rows[0].Active }]));
        Assert.NotEqual(later, source.Window[0].AsOf);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Empty(thrown);
        Assert.Equal(later, Assert.Single(heard.Actions).Row.AsOf);
        Assert.Equal(0m, source.Window[0].Amount);
        Assert.Equal(later, source.Window[0].AsOf);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-20, ADR-0011: Space on an action names its row by position; a gathered change that moves the order leaves it naming another, so it is refused as OrderMoved rather than fired on that one
    public async Task Space_on_an_action_whose_order_the_gathered_change_moved_is_refused()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        var pressedOn = Paint(cut);
        // Every row a new instance, in another order.
        source.Gather([.. rows.Select(r => new TestRow { Book = r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active }).Reverse()], version: 1);

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.OrderMoved, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 D5 / LV-16: with nothing gathered, a write is raised as before, and asks once
    public async Task A_write_with_nothing_gathered_is_raised_as_before()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        await KeyAsync(cut, "Enter");

        Assert.Equal(1, source.Asked);
        Assert.Same(rows[0], Assert.Single(heard.Edits).Row);
    }

    // ---- LV-12, LV-20: with a Row Key, a press acts on the row under its key ----

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

    [Fact] // ADR-0142 / LV-12, ADR-0140: with a Row Key, a press whose row moved under a new order acts on the row under its key
    public async Task With_a_row_key_a_press_whose_row_moved_acts_on_it_by_key()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        var moved = Moved(rows, 0, 2);
        Reorder(cut, moved);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        // The row's kept component, now painted third.
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(moved[2], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 / LV-12: with a Row Key, a press whose row moved and changed acts on it as it is now
    public async Task With_a_row_key_a_press_whose_row_moved_and_changed_acts_on_it_as_it_is_now()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        var moved = Moved(rows, 0, 2, amount: 777m);
        Reorder(cut, moved);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(moved[2], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 / LV-20, LV-12: with a Row Key, a press whose row has left the Window is refused as RowGone
    public async Task With_a_row_key_a_press_whose_row_left_the_window_is_refused_as_row_gone()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        // The handler of row 0's button as it was painted, before the row leaves.
        var olderHandler = cut.FindComponents<ExGridRow<TestRow>>().Single(r => ReferenceEquals(r.Instance.Row, rows[0])).Instance.OnAction!;
        cut.Render(ps => ps.Add(g => g.Window, rows[1..]).Add(g => g.TotalCount, rows.Length - 1).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.InvokeAsync(() => olderHandler(new GridActionEventArgs<TestRow>(rows[0], "Do", "approve")));

        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(ActionRefusalReason.RowGone, refusal.Reason);
        Assert.Same(rows[0], refusal.Action.Row);
    }

    [Fact] // ADR-0142 / LV-20, LV-12, "No press is lost to Blazor": with a Row Key, a told press whose row a later render takes out of the Window is answered after that render, as RowGone
    public async Task With_a_row_key_a_told_press_whose_row_a_later_render_removes_is_refused_as_row_gone()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        Assert.Empty(heard.ActionRefusals);

        cut.Render(ps => ps.Add(g => g.Window, rows[1..]).Add(g => g.TotalCount, rows.Length - 1).Add(g => g.RowSequenceVersion, 1));

        cut.WaitForAssertion(() => Assert.Equal(ActionRefusalReason.RowGone, Assert.Single(heard.ActionRefusals).Reason));
        Assert.Empty(heard.Actions);
    }

    [Fact] // ADR-0142 / LV-12, ADR-0140: with a Row Key, a told press whose row only changed keeps its component, which hears the click and acts on the row as it is now
    public async Task With_a_row_key_a_told_press_on_a_changed_row_is_heard_by_its_kept_component()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn, row: 0, column: 2, action: 0));
        Assert.Empty(heard.Actions);
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(changed[0], Assert.Single(heard.Actions).Row);
    }

    // ---- LV-21: Chrome words the notice; the grid holds no string, and adds nothing to a row ----

    [Fact] // ADR-0142 / LV-21, ADR-0013: the grid announces no string of its own for an Overwrite Notice, and the row it landed on paints as any written row does
    public async Task The_grid_holds_no_string_for_the_notice_and_adds_nothing_to_the_row()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        var plainRow = cut.FindAll(".ex-row")[0].GetAttribute("class");
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Enter");
        Push(cut, Changed(rows, 0, book: "5"));

        Assert.Single(heard.Notices);
        Assert.Equal("", cut.Find(".ex-announce").TextContent);
        Assert.Equal(plainRow, cut.FindAll(".ex-row")[0].GetAttribute("class"));
        Assert.DoesNotContain(cut.FindAll(".ex-row")[0].QuerySelectorAll("*"), e => e.ClassName?.Contains("overwrite", StringComparison.OrdinalIgnoreCase) == true);
    }

    // ---- LV-14: what the browser reads ----

    [Fact] // ADR-0142 / LV-14, ADR-0021 (2026-10-05, 2026-10-07): the key listener and the clipboard read carry the render from the Viewport's attribute; nothing is measured and nothing per cell crosses
    public void The_script_reads_the_render_a_key_or_a_paste_was_taken_against_from_the_viewport()
    {
        var script = AssetSources.Read("ExGrid", "ex-grid.js");

        // One reader: the attribute the painting render wrote on this grid's own Viewport.
        var reader = Regex.Match(script, @"const paintNow = \(\) => \{(?<body>.*?)\n    \};", RegexOptions.Singleline);
        Assert.True(reader.Success, "paintNow is defined");
        Assert.Contains("getAttribute('data-ex-paint')", reader.Groups["body"].Value);
        Assert.DoesNotMatch(new Regex(@"getBoundingClientRect|offsetWidth|offsetHeight|getComputedStyle|addEventListener"),
            reader.Groups["body"].Value);
        // A key carries it from its keydown, held or not.
        Assert.Matches(new Regex(@"const snapshot = \(event\) => \(\{[^}]*paint: paintNow\(\),"), script);
        Assert.Matches(new Regex(@"'OnKeyAsync',[^;]*k\.paint\)"), script);
        // A paste carries it from its event, held or not.
        Assert.Matches(new Regex(@"sendPaste\(event\.clipboardData\.getData\('text/plain'\), event\.clipboardData\.getData\('text/html'\), paintNow\(\)\)"), script);
        Assert.Matches(new Regex(@"clipboard: 'paste',[^}]*paint: paintNow\(\),"), script);
        Assert.Contains("'OnPasteStreamsAsync', stream(plain), stream(markup), paint)", script);
        // A press on an action carries it with the row, column and action it pressed.
        Assert.Contains("'ActionPressTakenAt'", script);
    }
}
