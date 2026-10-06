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
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.Edits.Add(i))
              .Add(g => g.OnCommitRefused, (GridCommitRefusal r) => heard.CommitRefusals.Add(r))
              .Add(g => g.OnPaste, (GridPasteIntent i) =>
              {
                  heard.Pastes.Add(i);
                  if (heard.RefusePastes)
                      i.Refuse();
              })
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

    [Fact] // ADR-0142 D3 / LV-13, ADR-0054: Delete writes too — a Clear over a painted target changed since its key is refused
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

    [Fact] // ADR-0142 D3 / LV-13, ADR-0035: Ctrl+D fills too — a fill key over a painted target changed since its key is refused
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

    [Fact] // ADR-0142 D3 / LV-13, ADR-0035: Ctrl+R fills too — a fill key over a painted target changed since its key is refused
    public async Task A_fill_right_key_over_a_changed_target_is_refused()
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

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    // ---- LV-13 (D4): a fill judges its source ----

    [Fact] // ADR-0142 D4 / LV-13: Ctrl+D writes its source's values, so a painted source cell whose text changed since the key refuses it, though its target did not change
    public async Task A_fill_down_whose_painted_source_changed_is_refused()
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

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D4 / LV-13: Ctrl+R judges its source column as Ctrl+D judges its source row
    public async Task A_fill_right_whose_painted_source_changed_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 150, 10, shift: true);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "r", ctrl: true, paint: pressedOn);

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D4 / LV-13: a fill whose source and target are as they were painted still fills, the change beside them notwithstanding
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

    [Fact] // ADR-0142 D4 / LV-13, ADR-0050 item 5: a fill-handle drag writes its source's values, so a painted source cell changed since the release's render refuses it
    public async Task A_fill_drag_whose_painted_source_changed_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        var releasedOn = Paint(cut);
        // Row 0 is the source's, not the target's (rows 2 and 3).
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, Attribute(cut, "data-ex-first-row"), 0,
            Attribute(cut, "data-ex-sequence"), Attribute(cut, "data-ex-layout"), releasedOn));
        await UpAsync(cut, 50, 70);

        Assert.Empty(heard.Fills);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    // ---- LV-11 (D2): the editor keeps what the opening gesture's render painted ----

    [Fact] // ADR-0142 D2 / LV-11: a change in the round trip between the key that opens the editor and the open was not seen; the commit is refused with the new text
    public async Task A_change_between_the_opening_key_and_the_open_refuses_the_commit()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        // Typed on the render before the change, handled after it: the editor covers the cell
        // from the moment it opens, so the user never saw the new text.
        await KeyAsync(cut, "5", paint: pressedOn);
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.Edits);
        Assert.Equal("Moved upstream", Assert.Single(heard.CommitRefusals).PaintedText);
        Assert.Equal("5", cut.Find("input.ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0142 D2 / LV-11, ADR-0010: F2 opens the editor too, and its render is the baseline
    public async Task F2_taken_before_a_change_opens_an_editor_whose_commit_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "F2", paint: pressedOn);
        await TypeAsync(cut, "Typed over");
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.Edits);
        Assert.Equal("Moved upstream", Assert.Single(heard.CommitRefusals).PaintedText);
    }

    [Fact] // ADR-0142 D2 / LV-11, ADR-0010: a double click opens the editor on the render its press was taken against
    public async Task A_double_click_taken_before_a_change_opens_an_editor_whose_commit_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        var pressedOn = Paint(cut);
        var firstRow = Attribute(cut, "data-ex-first-row");
        var sequence = Attribute(cut, "data-ex-sequence");
        var layout = Attribute(cut, "data-ex-layout");
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        // The press of the double click is told the render it was made on, as the listener tells it.
        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mousedown", 50, 10, firstRow, 0, sequence, layout, pressedOn));
        await DownAsync(cut, 50, 10);
        await UpAsync(cut, 50, 10);
        await cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 10 });
        Assert.Single(cut.FindAll("input.ex-editor"));
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.Edits);
        Assert.Equal("Moved upstream", Assert.Single(heard.CommitRefusals).PaintedText);
    }

    [Fact] // ADR-0142 D2 / LV-11, ADR-0051: a press into the Formula Bar opens the editor on the render the press was taken against
    public async Task A_press_into_the_formula_bar_taken_before_a_change_opens_an_editor_whose_commit_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, extra: ps => ps.Add(g => g.ShowFormulaBar, true));
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        // The listener tells the press's render before its focus is dispatched.
        await cut.InvokeAsync(() => cut.Instance.BarPressTakenAt(pressedOn));
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "Typed in the bar" });
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.Edits);
        Assert.Equal("Moved upstream", Assert.Single(heard.CommitRefusals).PaintedText);
    }

    [Fact] // ADR-0142 D2 / LV-11, ADR-0080: a composition in the Keyboard Field opens the editor on the render it started on
    public async Task A_composition_started_before_a_change_opens_an_editor_whose_commit_is_refused()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var startedOn = Paint(cut);
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await cut.InvokeAsync(() => cut.Instance.OnKeyFieldTextAsync("かな", startedOn));
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.Edits);
        Assert.Equal("Moved upstream", Assert.Single(heard.CommitRefusals).PaintedText);
    }

    [Fact] // ADR-0142 D2 / LV-11, principle 1: a key taken against a render no longer kept cannot say what the cell showed; the first commit is refused with the text it paints, and the next lands
    public async Task An_editor_opened_by_a_key_on_a_render_no_longer_kept_refuses_its_first_commit()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        // Far more renders than the grid keeps, none of which changes the edited cell's text.
        for (var i = 0; i < 200; i++)
        {
            rows = Changed(rows, 3);
            Push(cut, rows);
        }

        await KeyAsync(cut, "5", paint: pressedOn);
        await KeyAsync(cut, "Enter", paint: Paint(cut));

        Assert.Empty(heard.Edits);
        Assert.Equal("Row 000000", Assert.Single(heard.CommitRefusals).PaintedText);

        await KeyAsync(cut, "Enter", paint: Paint(cut));
        Assert.Equal("5", Assert.Single(heard.Edits).Value);
    }

    [Fact] // ADR-0142 D2 / LV-11: an editor opened on a render that already painted the change is judged against it, and its commit lands
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
        Assert.Equal("5", Assert.Single(heard.Edits).Value);
    }

    // ---- LV-17 (D1): the user's own writes count as seen ----

    [Fact] // ADR-0142 D1 / LV-17, LV-11: `1` Enter ↑ `2` Enter typed at once — every key taken on the render before the 1 was painted — writes both: the cell the user's own commit wrote is not compared
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

    [Fact] // ADR-0142 D1 / LV-17, LV-13: `5` Enter ↑ Ctrl+V typed at once pastes: the paste is told an earlier render than the user's own commit, and the cell that commit wrote is not compared
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
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", taken));

        Assert.Empty(heard.PasteRefusals);
        var paste = Assert.Single(heard.Pastes);
        Assert.Equal(new CellPosition(0, 0), new CellPosition(paste.Plan.Targets[0].TopRow, paste.Plan.Targets[0].LeftColumn));
    }

    [Fact] // ADR-0142 D1 / LV-17: only the cells the user wrote are let off — a change upstream beside them, in the same target, still refuses
    public async Task A_change_upstream_beside_the_users_own_write_still_refuses()
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
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", taken));

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D1 / LV-17: a write the user made before the render a gesture was taken against is in that render; a change upstream after it is compared as any change
    public async Task A_change_upstream_after_the_users_write_was_painted_refuses()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5", paint: Paint(cut));
        await KeyAsync(cut, "Enter", paint: Paint(cut));
        var written = Changed(rows, 0, book: "5");
        Push(cut, written);
        // The user sees their 5, and the paste is taken on that render; the cell then moves upstream.
        var seen = Paint(cut);
        Push(cut, Changed(written, 0, book: "Moved upstream"));
        await KeyAsync(cut, "ArrowUp", paint: seen);

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", seen));

        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D1 / LV-17, LV-13: a fill's source the user wrote is not compared either — `5` Enter, then Ctrl+D from it, at once
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

    [Fact] // ADR-0142 D1 / LV-17, LV-12: an Action press on a row the user's own commit just wrote fires, though it was taken before the write was painted
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

    [Fact] // ADR-0142 D1 / LV-17, ADR-0050 item 3: a paste the Consumer refused wrote nothing, so the cells it named are compared as any others
    public async Task A_paste_the_consumer_refused_lets_nothing_off()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard { RefusePastes = true };
        var cut = RenderGrid(rows, heard);
        await ClickAsync(cut, 50, 10);
        var taken = Paint(cut);
        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", taken));
        Push(cut, Changed(rows, 0, book: "Moved upstream"));

        await KeyAsync(cut, "Delete", paint: taken);

        Assert.Empty(heard.Clears);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    // ---- LV-16 (D5): a bound source puts out what it has gathered before a write is judged ----

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
        GatheringSource source, Heard heard, GridColumn<TestRow>[]? columns = null,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.Edits.Add(i))
              .Add(g => g.OnCommitRefused, (GridCommitRefusal r) => heard.CommitRefusals.Add(r))
              .Add(g => g.OnPaste, (GridPasteIntent i) =>
              {
                  heard.Pastes.Add(i);
                  if (heard.RefusePastes)
                      i.Refuse();
              })
              .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => heard.PasteRefusals.Add(r))
              .Add(g => g.OnAction, (GridActionEventArgs<TestRow> a) => heard.Actions.Add(a))
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
        var intent = Assert.Single(heard.Edits);
        Assert.Same(gathered[0], intent.Row);
        Assert.Equal("5", intent.Value);
        // The newest version is painted, not only judged.
        Assert.Contains(cut.FindAll(".ex-row")[0].QuerySelectorAll("[role=gridcell]"), c => c.TextContent.Replace(",", "") == "999999");
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-11: a gathered change to the edited cell itself refuses the commit, with its new text
    public async Task A_gathered_change_to_the_edited_cell_refuses_the_commit()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        source.Gather(Changed(rows, 0, book: "Gathered upstream"));

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Equal("Gathered upstream", Assert.Single(heard.CommitRefusals).PaintedText);
        Assert.Equal("5", cut.Find("input.ex-editor").GetAttribute("value"));

        // Judged again against the text the refusal showed: it lands on the newest row.
        await KeyAsync(cut, "Enter");
        Assert.Equal("Gathered upstream", Assert.Single(heard.Edits).Row.Book);
    }

    [Fact] // ADR-0142 D5 / LV-16, ADR-0011: a gathered change that moves the order discards the edit rather than committing it onto another row
    public async Task A_gathered_change_that_moves_the_order_discards_the_edit()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderSourceGrid(source, heard,
            extra: ps => ps.Add(g => g.OnEditDiscarded, (EditDiscardReason r) => discarded.Add(r)));
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        source.Gather([.. Enumerable.Reverse(rows)], version: 1);

        await KeyAsync(cut, "Enter");

        Assert.Empty(heard.Edits);
        Assert.Empty(heard.CommitRefusals);
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal([EditDiscardReason.OrderChanged], discarded);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-13: a paste asks for the gathered change first, and is judged against it
    public async Task A_paste_is_judged_against_the_gathered_change()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        var pressedOn = Paint(cut);
        source.Gather(Changed(rows, 0, book: "Gathered upstream"));

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", "<table><tr><td>x</td></tr></table>", pressedOn));

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Theory] // ADR-0142 D5 / LV-16, LV-13: Delete, Ctrl+D and Ctrl+R ask for the gathered change first, and are judged against it
    [InlineData("Delete", false)]
    [InlineData("d", true)]
    [InlineData("r", true)]
    public async Task A_write_key_is_judged_against_the_gathered_change(string key, bool ctrl)
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
        Assert.Empty(heard.Pastes);
        Assert.Empty(heard.Clears);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-13: a fill-handle drag asks for the gathered change first, and is judged against it
    public async Task A_fill_drag_is_judged_against_the_gathered_change()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, extra: ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await ClickAsync(cut, 50, 30, shift: true);
        await DownAsync(cut, 100, 40);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 70 });
        var releasedOn = Paint(cut);
        source.Gather(Changed(rows, 3, book: "Gathered upstream"));

        await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mouseup", 50, 70, Attribute(cut, "data-ex-first-row"), 0,
            Attribute(cut, "data-ex-sequence"), Attribute(cut, "data-ex-layout"), releasedOn));
        await UpAsync(cut, 50, 70);

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.Fills);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-13: a Ctrl+Enter fill asks for the gathered change first, and is judged against it
    public async Task A_ctrl_enter_fill_is_judged_against_the_gathered_change()
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
        Assert.Empty(heard.Pastes);
        Assert.Equal([PasteRefusalReason.TargetChanged], heard.PasteRefusals);
        Assert.Equal("z", cut.Find("input.ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-12: an Action press asks for the gathered change first, and is judged against it
    public async Task An_action_press_is_judged_against_the_gathered_change()
    {
        var rows = TestRows.Many(50);
        var source = new GatheringSource(rows);
        var heard = new Heard();
        var cut = RenderSourceGrid(source, heard, WithAction());
        var pressedOn = Paint(cut);
        source.Gather(Changed(rows, 0, amount: 777m));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(1, source.Asked);
        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RowChanged, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 D5 / LV-16, LV-12, ADR-0011: Space on an action names its row by position; a gathered change that moves the order leaves it naming another, so it is refused rather than fired on that one
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
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 D5 / LV-16: with nothing gathered, a write is judged and raised as before, and asks once
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

    // ---- LV-12 (D, the press paired by key) ----

    private static readonly Func<TestRow, object> ByBook = static row => row.Book;

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

    [Fact] // ADR-0142 (press paired by key) / LV-12, ADR-0140: with a Row Key, a press whose row moved under a new order is paired with its row by key, and fires when its painted cells show what they showed
    public async Task With_a_row_key_a_press_whose_row_moved_is_paired_by_key_and_fires()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        var moved = Moved(rows, 0, 2);
        cut.Render(ps => ps.Add(g => g.Window, moved).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        // The row's kept component, now painted third.
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.ActionRefusals);
        Assert.Same(moved[2], Assert.Single(heard.Actions).Row);
    }

    [Fact] // ADR-0142 (press paired by key) / LV-12: paired by key, a row whose painted cells changed as it moved is refused because it changed, not because the render is gone
    public async Task With_a_row_key_a_press_whose_row_moved_and_changed_is_refused_as_changed()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction(), extra: ps => ps.Add(g => g.RowKey, ByBook));
        var pressedOn = Paint(cut);
        cut.Render(ps => ps.Add(g => g.Window, Moved(rows, 0, 2, amount: 777m)).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RowChanged, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 (press paired by key) / LV-12: without a Row Key, a press whose row moved as a new instance cannot be paired, and is refused as taken against a render no longer kept, as before
    public async Task Without_a_row_key_a_press_whose_row_moved_is_refused_as_no_longer_kept()
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(rows, heard, WithAction());
        var pressedOn = Paint(cut);
        cut.Render(ps => ps.Add(g => g.Window, Moved(rows, 0, 2)).Add(g => g.RowSequenceVersion, 1));

        await cut.InvokeAsync(() => cut.Instance.ActionPressTakenAt(pressedOn));
        await cut.FindAll(".ex-action")[2].ClickAsync(new MouseEventArgs());

        Assert.Empty(heard.Actions);
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
    }

    [Fact] // ADR-0142 (press paired by key) / LV-12: paired by key, a press whose row has left the Window cannot be judged, and is refused
    public async Task With_a_row_key_a_press_whose_row_left_the_window_is_refused()
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
        Assert.Equal(ActionRefusalReason.RenderNoLongerKept, Assert.Single(heard.ActionRefusals).Reason);
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
        // A press into the Formula Bar carries it too, ahead of the focus that opens the editor
        // (ADR-0142 D2).
        Assert.Matches(new Regex(@"'BarPressTakenAt', paintNow\(\)"), script);
    }
}
