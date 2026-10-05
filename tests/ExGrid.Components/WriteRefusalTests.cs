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
/// A write is refused when what the user saw of its target changed before it lands (ADR-0142,
/// LV-11 to LV-14). What the user saw is the painted text of the target's painted cells, in the
/// render the gesture was taken against: the Viewport names that render (<c>data-ex-paint</c>),
/// the browser tells it with each gesture, and the core keeps what it needs to recompute the text
/// of the cells it painted for its last few renders. Here a test tells a gesture an earlier render,
/// as ED-31's tests tell a press an earlier layout; what the browser reads is layer 3's.
///
/// 20px rows in a 120px Viewport (five rows painted), 350px wide: Book 0–100 and Amount 100–200,
/// both editable, and an Action Column 200–300 where a test asks for one.
/// </summary>
public class WriteRefusalTests : GridTestContext
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
        public List<GridPasteIntent> Pastes { get; } = [];
        public List<PasteRefusalReason> PasteRefusals { get; } = [];
        public List<GridActionEventArgs<TestRow>> Actions { get; } = [];
        public List<GridActionRefusal<TestRow>> ActionRefusals { get; } = [];
        public List<GridFillIntent> Fills { get; } = [];
        public List<GridClearIntent> Clears { get; } = [];
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
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.Edits.Add(i))
              .Add(g => g.OnCommitRefused, (GridCommitRefusal r) => heard.CommitRefusals.Add(r))
              .Add(g => g.OnPaste, (GridPasteIntent i) => heard.Pastes.Add(i))
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) => heard.Actions.Add(a))
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

    private static void Push(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] rows)
        => cut.Render(ps => ps.Add(g => g.Window, rows));

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

    [Fact] // ADR-0142 / LV-14: a render that changes no painted cell keeps the name, so a gesture taken on it is judged as painted
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

    [Fact] // ADR-0142 / LV-11: a commit whose cell paints other text than when the editor opened is refused: no Edit Intent, the editor stays with the typing, and the reason carries the new text
    public async Task A_commit_over_a_cell_that_changed_under_the_editor_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        await TypeAsync(cut, "5x");

        Push(cut, Changed(rows, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        var refusal = Assert.Single(heard.CommitRefusals);
        Assert.Equal(new CellPosition(0, 0), refusal.Cell);
        Assert.Equal("Book", refusal.Column);
        Assert.Equal("Moved upstream", refusal.PaintedText);
        Assert.Equal("5x", cut.Find("input.ex-editor").GetAttribute("value"));
        // The Focus did not move away from the editor that still stands.
        Assert.Equal(new CellPosition(0, 0), cut.Instance.ReadSelection().Selection.Focus);
    }

    [Fact] // ADR-0142 / LV-11: a second commit is judged against the value the refusal showed, and raises the intent
    public async Task A_second_commit_is_judged_against_the_text_the_refusal_showed()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var changed = Changed(rows, 0, book: "Moved upstream");
        Push(cut, changed);
        await KeyAsync(cut, "Enter");
        Assert.Single(heard.CommitRefusals);

        await KeyAsync(cut, "Enter");

        var intent = Assert.Single(heard.Edits);
        Assert.Equal("5", intent.Value);
        Assert.Same(changed[0], intent.Row);
        Assert.Single(heard.CommitRefusals);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11: a cell that changes again after the refusal refuses the next commit too, with the newer text
    public async Task A_change_after_the_refusal_refuses_again_with_the_newer_text()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        var once = Changed(rows, 0, book: "Once");
        Push(cut, once);
        await KeyAsync(cut, "Enter");

        Push(cut, Changed(once, 0, book: "Twice"));
        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Equal(["Once", "Twice"], heard.CommitRefusals.Select(r => r.PaintedText));
    }

    [Fact] // ADR-0142 / LV-11: Escape after a refused commit leaves without writing
    public async Task Escape_after_a_refused_commit_writes_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));
        await KeyAsync(cut, "Enter");

        await KeyAsync(cut, "Escape");

        Assert.Empty(heard.Edits);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11: a change to another cell of the row refuses nothing, and the commit lands on the row as it is now
    public async Task A_change_to_another_cell_of_the_row_refuses_nothing()
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
        Assert.Single(cut.FindAll("input.ex-editor"));
    }

    [Fact] // ADR-0142 / LV-11, ADR-0010: a press elsewhere that would commit over a changed cell is refused, and the press keeps no meaning of its own
    public async Task A_press_that_commits_over_a_changed_cell_is_refused_and_moves_nothing()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await DownAsync(cut, 150, 50);

        Assert.Empty(heard.Edits);
        Assert.Single(heard.CommitRefusals);
        Assert.Single(cut.FindAll("input.ex-editor"));
        Assert.Equal(new CellPosition(0, 0), cut.Instance.ReadSelection().Selection.Focus);
    }

    // ---- LV-12: an Action press ----

    [Fact] // ADR-0142 / LV-12, ADR-0140: a press taken on an earlier render of a row that has changed since is refused, though the button that hears it already holds the newest row — as a row kept by its Row Key does
    public async Task An_action_press_on_a_row_changed_since_its_render_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        var changed = Changed(rows, 0, amount: 777m);
        Push(cut, changed);

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        // The button painted now, whose row is the newest instance: the judgement is the paint's,
        // never the handling component's.
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(ActionRefusalReason.RowChanged, refusal.Reason);
        Assert.Same(changed[0], refusal.Action.Row);
        Assert.Equal("Do", refusal.Action.ColumnName);
        Assert.Equal("approve", refusal.Action.ActionName);
    }

    [Fact] // ADR-0142 / LV-12, ADR-0003: keyed by instance, a press can reach the button of the row as it was painted, holding the older row; it is refused all the same
    public async Task An_action_press_heard_by_the_row_as_painted_is_refused_all_the_same()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        // The press is made on the button painted then. On a circuit, the renderer keeps a removed
        // handler until the browser has applied the render that removed it, so the click reaches the
        // older row's own button; this fires that handler as the old component held it.
        var paintedRow = cut.FindComponents<ExGridRow<TestRow>>().Single(r => ReferenceEquals(r.Instance.Row, rows[0]));
        var olderHandler = paintedRow.Instance.OnAction!;
        Push(cut, Changed(rows, 0, amount: 777m));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.InvokeAsync(() => olderHandler(new GridActionEventArgs<TestRow>(rows[0], "Do", "approve")));

        Assert.Empty(heard.Actions);
        var refusal = Assert.Single(heard.ActionRefusals);
        Assert.Equal(ActionRefusalReason.RowChanged, refusal.Reason);
        Assert.Same(rows[0], refusal.Action.Row);
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

    [Fact] // ADR-0142 / LV-12: a press taken on a render no longer kept is refused, because the grid can no longer tell what the user saw
    public async Task An_action_press_on_a_render_no_longer_kept_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        // Far more renders than the grid keeps, each a new instance of a painted row that paints
        // the same text: no row changed, but the render the press names is gone.
        for (var i = 0; i < 200; i++)
        {
            rows = Changed(rows, 1);
            Push(cut, rows);
        }

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 / LV-12: a press nobody told of is judged against the newest render, so a row whose change is painted fires
    public async Task An_action_press_nobody_told_of_is_judged_against_the_newest_render()
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

    [Fact] // ADR-0142 / LV-12, LV-14, ADR-0037: Space fires an action by key, and the key carries its render: a row changed since is refused
    public async Task Space_on_an_action_taken_on_an_earlier_render_of_a_changed_row_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        await ClickAsync(cut, 250, 10);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, " ", paint: pressedOn);

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RowChanged, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 / LV-12, ADR-0037: Space on an action whose row the view has scrolled away from saw nothing of it, and fires as before
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

    [Fact] // ADR-0142 / LV-12: a cell that was not painted at the press was not seen, and its change refuses nothing
    public async Task A_change_in_a_column_not_painted_at_the_press_refuses_nothing()
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
        Assert.DoesNotContain(cut.FindAll(".ex-row")[0].QuerySelectorAll("[role=gridcell]"),
            c => c.GetAttribute("aria-colindex") == "8");
        var pressedOn = Paint(cut);
        // Only Far shows Amount.
        Push(cut, Changed(rows, 0, amount: 4242m));
        Assert.Contains(cut.FindAll(".ex-row")[0].QuerySelectorAll("[role=gridcell]"), c => c.TextContent == "4242");

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        // Amount (column 1) is painted and changed, so this one is refused…
        Assert.Single(heard.ActionRefusals);

        // …while with Amount out of the painted columns, only Far shows it, and nothing is refused.
        var heardFar = new Heard();
        GridColumn<TestRow>[] farOnly =
        [
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
            GridColumn<TestRow>.ActionColumn("Do", [new GridAction("approve", "Approve")], width: Fixed100),
            new("Pad1", ColumnType.Text, _ => "", width: Fixed100),
            new("Pad2", ColumnType.Text, _ => "", width: Fixed100),
            new("Pad3", ColumnType.Text, _ => "", width: Fixed100),
            new("Pad4", ColumnType.Text, _ => "", width: Fixed100),
            new("Far", ColumnType.Number, r => r.Amount, width: Fixed100),
        ];
        var far = RenderGrid(rows, heardFar, farOnly);
        var farPressedOn = Paint(far);
        Push(far, Changed(rows, 0, amount: 4242m));

        await far.InvokeAsync(() => far.Instance.ActionPressTakenAt(farPressedOn));
        await far.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Single(heardFar.Actions);
        Assert.Empty(heardFar.ActionRefusals);
    }

    // ---- LV-13: a paste, a Ctrl+Enter fill and a fill-handle drag ----

    [Fact] // ADR-0142 / LV-13: a paste whose painted target changed after the render it was taken against is refused
    public async Task A_paste_whose_painted_target_changed_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", pressedOn));

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13: a change beside the target refuses nothing
    public async Task A_paste_whose_painted_target_did_not_change_is_written()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, amount: 31m));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", pressedOn));

        Assert.Single(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0014: the target's cells that were not painted are not compared
    public async Task Cells_of_the_target_that_were_not_painted_are_not_compared()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        // The whole Book column, rows 0 to 49: five of them painted.
        await KeyAsync(cut, "ArrowDown", ctrl: true, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 40, book: "Moved upstream"));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", pressedOn));

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

    [Fact] // ADR-0142 / LV-13: a paste taken on a render no longer kept is refused, with its own reason
    public async Task A_paste_taken_on_a_render_no_longer_kept_is_refused()
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

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.RenderNoLongerKept], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13, LV-14: a Ctrl+Enter fill whose painted target changed after the key's render is refused; the editor stays with the typing
    public async Task A_ctrl_enter_fill_over_a_changed_target_is_refused_and_the_editor_stays()
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

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
        Assert.Equal("z", cut.Find("input.ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0142 / LV-11, LV-13: a Ctrl+Enter fill whose edited cell changed under the editor is refused as a commit, with the cell's new text
    public async Task A_ctrl_enter_fill_whose_edited_cell_changed_is_refused_with_its_new_text()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await KeyAsync(cut, "z");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Enter", ctrl: true, paint: Paint(cut));

        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.PasteRefusals);
        Assert.Equal("Moved upstream", Assert.Single(heard.CommitRefusals).PaintedText);
        Assert.Equal("z", cut.Find("input.ex-editor").GetAttribute("value"));

        // Judged again against the text the refusal showed: the fill goes.
        await KeyAsync(cut, "Enter", ctrl: true, paint: Paint(cut));
        Assert.Single(heard.Pastes);
    }

    [Fact] // ADR-0142 / LV-13, ADR-0050 item 5: a fill-handle drag released on a render whose target has changed since is refused
    public async Task A_fill_drag_released_on_an_earlier_render_of_a_changed_target_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        // The handle's corner is at (100, 40); dragged down to row 3.
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        var releasedOn = Paint(cut);
        Push(cut, Changed(rows, 3, book: "Moved upstream"));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, Attribute(cut, "data-ex-first-row"), 0,
            Attribute(cut, "data-ex-sequence"), Attribute(cut, "data-ex-layout"), releasedOn));
        await UpAsync(cut, 50, 70);

        Assert.Empty(heard.Fills);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 / LV-13: a fill-handle drag whose target did not change raises its one intent, as before
    public async Task A_fill_drag_over_an_unchanged_target_raises_its_intent()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        var releasedOn = Paint(cut);
        Push(cut, Changed(rows, 3, amount: 1m));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, Attribute(cut, "data-ex-first-row"), 0,
            Attribute(cut, "data-ex-sequence"), Attribute(cut, "data-ex-layout"), releasedOn));
        await UpAsync(cut, 50, 70);

        Assert.Single(heard.Fills);
        Assert.Empty(heard.PasteRefusals);
    }

    [Fact] // ADR-0142 (the rule), ADR-0054: Delete writes too — a Clear over a painted target changed since its key is refused
    public async Task A_clear_over_a_changed_target_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 1, book: "Moved upstream"));

        await KeyAsync(cut, "Delete", paint: pressedOn);

        Assert.Empty(heard.Clears);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 (the rule), ADR-0035: Ctrl+D fills too — a fill key over a painted target changed since its key is refused
    public async Task A_fill_key_over_a_changed_target_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 50, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 2, book: "Moved upstream"));

        await KeyAsync(cut, "d", ctrl: true, paint: pressedOn);

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    // ---- LV-14: what the browser reads ----

    [Fact] // ADR-0142 / LV-14, ADR-0021 (2026-10-05): the key listener and the clipboard read carry the render from the Viewport's attribute; nothing is measured and nothing per cell crosses
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
        // A press on the rows, and on an action, carry it with what they were taken against.
        Assert.Contains("paint: number('data-ex-paint')", script);
        Assert.Contains("'ActionPressTakenAt'", script);
    }
}
