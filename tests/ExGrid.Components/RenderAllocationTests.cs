using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// PF-3 (ADR-0027 P5): a per-cell string is interned, or cached alongside what produced
/// it, and never composed in the render loop. Measured as a slope rather than a total: the
/// same re-render at two column counts, over values the Consumer already holds as strings,
/// so a byte that grows with the number of painted cells can only be the grid's own. The
/// total is not zero and is not meant to be — a render has fixed costs — but none of them
/// may scale with the cells.
///
/// <para><b>What P5 does not cover, and these tests therefore hold still:</b> the value's
/// own text. It is the Consumer's — its accessor, its Format, its value's ToString — and
/// the grid composes it from the data, not from geometry or metadata, so every column here
/// hands over a value that is already a string or a Format that returns one held. Whether
/// that text should be cached per row as well is a separate question, not one P5
/// answers.</para>
/// </summary>
public class RenderAllocationTests : GridTestContext
{
    private const double ColumnWidthPx = 60;

    // Two lookups that answer the same thing, handed over alternately: the delegate's
    // identity is the row's change signal (ADR-0006), so each swap re-renders every
    // visible row — and the root — while painting exactly what was painted before.
    private static readonly CellStateOf<TestRow> NormalA = static (_, _) => CellState.Normal;
    private static readonly CellStateOf<TestRow> NormalB = static (_, _) => CellState.Normal;

    private static readonly ColumnWidthSpec Fixed = new(ColumnWidth.Fixed(ColumnWidthPx));

    private static GridColumn<TestRow>[] TextColumns(int count)
    {
        var columns = new GridColumn<TestRow>[count];
        for (var i = 0; i < count; i++)
            columns[i] = new GridColumn<TestRow>($"C{i:D2}", ColumnType.Text, r => r.Book, width: Fixed);
        return columns;
    }

    // A number too wide for its column, boxed once and formatted to a string held once, so
    // the only string left for a render to compose is the run of hashes (ADR-0016).
    private static readonly object TooWide = 1234567890m;

    private static GridColumn<TestRow>[] HashedColumns(int count)
    {
        var columns = new GridColumn<TestRow>[count];
        for (var i = 0; i < count; i++)
            columns[i] = new GridColumn<TestRow>(
                $"N{i:D2}", ColumnType.Number, static _ => TooWide, width: Fixed,
                format: static _ => "1234567890");
        return columns;
    }

    // Two icon actions per cell — a Consumer's class on each, so the button's class
    // attribute is more than the core's own vocabulary (ADR-0020).
    private static readonly GridAction[] IconActions =
        [new("open", "Open", "icon-open"), new("delete", "Delete", "icon-delete")];

    private static readonly GridAction[] PlainActions =
        [new("open", "Open"), new("delete", "Delete")];

    private static GridColumn<TestRow>[] ActionColumns(int count) => ActionColumns(count, IconActions);

    private static GridColumn<TestRow>[] ActionColumns(int count, GridAction[] actions)
    {
        var columns = new GridColumn<TestRow>[count];
        for (var i = 0; i < count; i++)
            columns[i] = GridColumn<TestRow>.ActionColumn($"A{i:D2}", actions, width: Fixed);
        return columns;
    }

    /// <summary>
    /// The least a run of <paramref name="renders"/> re-renders allocated, over
    /// <paramref name="runs"/> runs on one grid — and how many cells, body and header, each
    /// render painted. The least, because what a render allocates per cell it allocates on
    /// every render, so it is in every run. What the least filters out arrives now and then
    /// on the measuring thread and belongs to no render: in the full suite, a few kilobytes
    /// landed in one run of the larger grid about one time in four, and never with tiered
    /// compilation off — the JIT replacing a hot loop's code on the thread running it.
    /// </summary>
    private (long Bytes, int Cells) LeastAllocatedByReRenders(
        GridColumn<TestRow>[] columns, IReadOnlyList<SortSpec>? sorts, IReadOnlyList<HeaderGroup>? groups,
        bool stripes = false, int renders = 10, int runs = 10)
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, columns)
            .Add(g => g.Sorts, sorts)
            .Add(g => g.HeaderGroups, groups)
            .Add(g => g.StripeRows, stripes)
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            // Every column painted, so the cell count is rows × columns exactly.
            .Add(g => g.ViewportWidth, columns.Length * ColumnWidthPx + 100));

        // Warm both of every row's render buffers, so growing them is off the measurement.
        cut.Render(ps => ps.Add(g => g.CellState, NormalA));
        cut.Render(ps => ps.Add(g => g.CellState, NormalB));

        var least = long.MaxValue;
        for (var run = 0; run < runs; run++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < renders; i++)
                cut.Render(ps => ps.Add(g => g.CellState, i % 2 == 0 ? NormalA : NormalB));
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return (least, cut.FindAll(".ex-cell, .ex-header-cell").Count);
    }

    /// <summary>The bytes a re-render allocates for each cell it paints: the slope
    /// between a grid of 4 columns and one of 16.</summary>
    private double PerCellPerRender(
        Func<int, GridColumn<TestRow>[]> columns, IReadOnlyList<SortSpec>? sorts = null,
        Func<int, HeaderGroup[]>? groups = null, bool stripes = false)
    {
        const int renders = 10;
        // One throwaway pass at each size: the JIT's first compilations land on whoever
        // goes first.
        LeastAllocatedByReRenders(columns(4), sorts, groups?.Invoke(4), stripes, renders);
        LeastAllocatedByReRenders(columns(16), sorts, groups?.Invoke(16), stripes, renders);

        var few = LeastAllocatedByReRenders(columns(4), sorts, groups?.Invoke(4), stripes, renders);
        var many = LeastAllocatedByReRenders(columns(16), sorts, groups?.Invoke(16), stripes, renders);
        return (double)(many.Bytes - few.Bytes) / ((many.Cells - few.Cells) * renders);
    }

    private void AssertNothingPerCell(
        Func<int, GridColumn<TestRow>[]> columns, IReadOnlyList<SortSpec>? sorts = null,
        Func<int, HeaderGroup[]>? groups = null, bool stripes = false)
    {
        var perCellPerRender = PerCellPerRender(columns, sorts, groups, stripes);
        Assert.True(
            perCellPerRender < 1,
            $"a re-render allocated {perCellPerRender:N1} bytes per painted cell (PF-3).");
    }

    [Fact] // PF-3 / ADR-0027 P5: re-rendering the rows allocates nothing per painted cell
    public void Re_rendering_every_row_allocates_nothing_per_cell()
        => AssertNothingPerCell(TextColumns);

    // Every two columns under one rectangle, so the groups grow with the columns (ADR-0032).
    private static HeaderGroup[] Pairs(int columnCount)
    {
        var groups = new HeaderGroup[columnCount / 2];
        for (var g = 0; g < groups.Length; g++)
            groups[g] = new HeaderGroup($"G{g}", [$"C{2 * g:D2}", $"C{2 * g + 1:D2}"]);
        return groups;
    }

    [Fact] // PF-3 / ADR-0032: tiered headers — a leaf's height, a rectangle's box and span — are cached, not composed
    public void Re_rendering_under_header_groups_allocates_nothing_per_cell()
        => AssertNothingPerCell(TextColumns, groups: Pairs);

    [Fact] // PF-3 / RR-13 / ADR-0038: a Row Stripe is one interned class on the row, never composed per render
    public void Re_rendering_striped_rows_allocates_nothing_per_cell()
        => AssertNothingPerCell(TextColumns, stripes: true);

    [Fact] // PF-3 / ADR-0033: a header's aria-sort is answered without allocating, sorted or not
    public void Re_rendering_a_sorted_grid_allocates_nothing_per_cell()
        => AssertNothingPerCell(TextColumns, [new SortSpec("C00", SortDirection.Ascending)]);

    [Fact] // PF-3 / ADR-0016: a run of #### is geometry's string, cached, not composed per render
    public void Re_rendering_hashed_cells_allocates_nothing_per_cell()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, HashedColumns(1))
            .Add(g => g.ViewportWidth, 200));
        // The premise: these cells really paint hashes, or this measures nothing new.
        Assert.StartsWith("#", cut.Find(".ex-cell").TextContent);

        AssertNothingPerCell(HashedColumns);
    }

    [Fact] // PF-3 / ADR-0020: a Consumer's class on an action is composed once, not per render
    public void An_actions_class_costs_nothing_per_render()
    {
        // Measured as a difference, because an action cell is never free: the event
        // directives it carries (@onmousedown:stopPropagation and the like) have Blazor
        // compose each attribute's name on every render, and that cost is the framework's,
        // outside P5 (ADR-0027). Two columns alike in every directive, one with a class on
        // its actions and one without, leave nothing between them but the class.
        var withClass = PerCellPerRender(ActionColumns);
        var plain = PerCellPerRender(count => ActionColumns(count, PlainActions));

        Assert.True(
            withClass - plain < 1,
            $"an action's class cost {withClass - plain:N1} bytes per painted cell per render " +
            $"({withClass:N1} with it, {plain:N1} without) (PF-3).");
    }
}
