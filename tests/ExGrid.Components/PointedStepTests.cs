using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A grid pointed at from outside answers where one step from a cell lands, and scrolls a cell into
/// view when asked (ADR-0058, "The keyboard", as the ninth Windows run settled it; DC-55): up or down
/// by one row in its current order, or left or right to the nearest of the columns the Consumer
/// names; no cell at an edge; a row that has not arrived answered as such. Neither request moves the
/// Selection or the Focus, and the step scrolls nothing. No JavaScript is added: the module stand-in
/// is strict, so a new call would fail these tests. 50 rows of 20px in a 350 × 200 Viewport under a
/// 20px header, 9 rows painted; Book, Note and Amount are 100px each.
/// </summary>
public class PointedStepTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private readonly List<GridSelection> _selections = [];

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private static GridPointedAt<TestRow> Declaration(bool pointedAt = true)
        => new(_ => Task.CompletedTask) { IsPointedAt = pointedAt };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        GridPointedAt<TestRow> pointedAt, TestRow[] rows, int? total = null, Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows)
              .Add(g => g.TotalCount, total ?? rows.Length)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, (GridSelection s) => _selections.Add(s))
              .Add(g => g.PointedAt, pointedAt);
            more?.Invoke(ps);
        });

    private static Task<GridPointedStep<TestRow>> StepAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, GridPointedAt<TestRow> declared, TestRow from, string column, GridDirection direction,
        Func<string, bool>? isColumn = null)
        => cut.InvokeAsync(() => declared.StepAsync(row => ReferenceEquals(row, from), column, direction, isColumn ?? (_ => true)));

    private static Task<bool> RevealAsync(IRenderedComponent<ExGrid<TestRow>> cut, GridPointedAt<TestRow> declared, TestRow row, string column)
        => cut.InvokeAsync(() => declared.RevealAsync(candidate => ReferenceEquals(candidate, row), column));

    /// <summary>The rows painted, by their position in the whole result.</summary>
    private static List<int> PaintedRows(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-row").Select(r => int.Parse(r.GetAttribute("aria-rowindex")!, System.Globalization.CultureInfo.InvariantCulture) - 1)];

    [Fact] // ADR-0058 / DC-55: up and down step one row in the grid's current order, in the same column, and move nothing
    public async Task Up_and_down_step_one_row_in_the_current_order()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        var scrolls = Js.ScrolledTo.Count;

        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[4], "Amount"), await StepAsync(cut, declared, rows[3], "Amount", GridDirection.Down));
        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[2], "Amount"), await StepAsync(cut, declared, rows[3], "Amount", GridDirection.Up));

        // The Consumer shows another order: the step follows it, not the row's old position.
        TestRow[] reversed = [.. rows.Reverse()];
        cut.Render(ps => ps.Add(g => g.Window, reversed));
        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[2], "Book"), await StepAsync(cut, declared, rows[3], "Book", GridDirection.Down));

        // Nothing moved: no Selection, no Focus, no scroll.
        Assert.Empty(_selections);
        Assert.Empty(cut.FindAll(".ex-focus, .ex-range"));
        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0058 / DC-55: up from the first row and down from the last there is no cell
    public async Task No_cell_is_past_the_first_or_the_last_row()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);

        Assert.Equal(GridPointedStepKind.Edge, (await StepAsync(cut, declared, rows[0], "Book", GridDirection.Up)).Kind);
        Assert.Equal(GridPointedStepKind.Edge, (await StepAsync(cut, declared, rows[49], "Book", GridDirection.Down)).Kind);
    }

    [Fact] // ADR-0058 / DC-55: a step onto a row that has not arrived is answered as such, with the column and no row
    public async Task A_row_that_has_not_arrived_is_answered_as_such()
    {
        var rows = TestRows.Many(10);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows, total: 50);

        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.RowNotArrived, Column: "Note"), await StepAsync(cut, declared, rows[9], "Note", GridDirection.Down));
    }

    [Fact] // ADR-0058 / DC-55: left and right reach the nearest of the columns the Consumer names, passing over the others; none that way is an edge
    public async Task Left_and_right_reach_the_nearest_column_the_consumer_names()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        static bool NotNote(string name) => name != "Note";

        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[5], "Amount"), await StepAsync(cut, declared, rows[5], "Book", GridDirection.Right, NotNote));
        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[5], "Book"), await StepAsync(cut, declared, rows[5], "Amount", GridDirection.Left, NotNote));
        Assert.Equal(GridPointedStepKind.Edge, (await StepAsync(cut, declared, rows[5], "Amount", GridDirection.Right, NotNote)).Kind);
        Assert.Equal(GridPointedStepKind.Edge, (await StepAsync(cut, declared, rows[5], "Book", GridDirection.Left, NotNote)).Kind);
        Assert.Equal(GridPointedStepKind.Edge, (await StepAsync(cut, declared, rows[5], "Book", GridDirection.Right, _ => false)).Kind);
        // Every column named: the next one.
        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[5], "Note"), await StepAsync(cut, declared, rows[5], "Book", GridDirection.Right));
    }

    [Fact] // ADR-0058 / DC-55: the row to step from is found in the Window, painted or not
    public async Task The_row_to_step_from_is_found_beyond_the_painted_rows()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        Assert.DoesNotContain(30, PaintedRows(cut));

        Assert.Equal(new GridPointedStep<TestRow>(GridPointedStepKind.Cell, rows[31], "Book"), await StepAsync(cut, declared, rows[30], "Book", GridDirection.Down));
    }

    [Fact] // ADR-0058 / DC-55: no step is taken from a cell the grid does not hold, while it is not pointed at, or for a declaration given no grid
    public async Task Nothing_is_stepped_from_a_cell_the_grid_does_not_hold_or_while_not_pointed_at()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);

        Assert.Equal(GridPointedStepKind.NotHeld, (await StepAsync(cut, declared, new TestRow(), "Book", GridDirection.Down)).Kind);
        Assert.Equal(GridPointedStepKind.NotHeld, (await StepAsync(cut, declared, rows[3], "Nope", GridDirection.Down)).Kind);

        await cut.InvokeAsync(() => declared.IsPointedAt = false);
        Assert.Equal(GridPointedStepKind.NotHeld, (await StepAsync(cut, declared, rows[3], "Book", GridDirection.Down)).Kind);

        var alone = Declaration();
        Assert.Equal(GridPointedStepKind.NotHeld, (await alone.StepAsync(_ => true, "Book", GridDirection.Down, _ => true)).Kind);
        Assert.False(await alone.RevealAsync(_ => true, "Book"));
    }

    [Fact] // ADR-0058 / DC-55: a declaration another grid takes is answered by that grid, and one a disposed grid had is answered by none
    public async Task The_grid_given_the_declaration_answers_for_it()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        var other = Declaration();

        cut.Render(ps => ps.Add(g => g.PointedAt, other));

        Assert.Equal(GridPointedStepKind.NotHeld, (await declared.StepAsync(r => ReferenceEquals(r, rows[3]), "Book", GridDirection.Down, _ => true)).Kind);
        Assert.Equal(GridPointedStepKind.Cell, (await StepAsync(cut, other, rows[3], "Book", GridDirection.Down)).Kind);

        await DisposeComponentsAsync();
        Assert.Equal(GridPointedStepKind.NotHeld, (await other.StepAsync(r => ReferenceEquals(r, rows[3]), "Book", GridDirection.Down, _ => true)).Kind);
    }

    [Fact] // ADR-0058 / DC-55: asked to, the grid scrolls a cell into view, with no Selection of its own and moving none; a cell in view scrolls nothing
    public async Task A_cell_is_scrolled_into_view_when_asked()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows);
        Assert.DoesNotContain(20, PaintedRows(cut));
        var scrolls = Js.ScrolledTo.Count;

        Assert.True(await RevealAsync(cut, declared, rows[20], "Amount"));

        Assert.Equal(scrolls + 1, Js.ScrolledTo.Count);
        Assert.True(Js.ScrolledTo[^1].Top > 0);
        Assert.Contains(20, PaintedRows(cut));
        Assert.Empty(_selections);
        Assert.Empty(cut.FindAll(".ex-focus, .ex-range"));

        // Already in view: nothing more is written.
        Assert.True(await RevealAsync(cut, declared, rows[20], "Amount"));
        Assert.Equal(scrolls + 1, Js.ScrolledTo.Count);

        // A cell the grid does not hold is not revealed.
        Assert.False(await RevealAsync(cut, declared, new TestRow(), "Amount"));
        Assert.False(await RevealAsync(cut, declared, rows[20], "Nope"));
        Assert.Equal(scrolls + 1, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0058 / DC-55 / ADR-0015: under a PageSize, revealing a cell on another page turns the page
    public async Task Revealing_a_cell_on_another_page_turns_the_page()
    {
        var rows = TestRows.Many(50);
        var declared = Declaration();
        var cut = RenderGrid(declared, rows, more: ps => ps.Add(g => g.PageSize, 5));
        Assert.DoesNotContain(12, PaintedRows(cut));

        Assert.True(await RevealAsync(cut, declared, rows[12], "Book"));

        Assert.Contains(12, PaintedRows(cut));
        Assert.DoesNotContain(0, PaintedRows(cut));
        Assert.Empty(_selections);
    }
}
