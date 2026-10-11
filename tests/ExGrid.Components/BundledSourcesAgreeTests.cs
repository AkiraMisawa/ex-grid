using AngleSharp.Dom;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// FN-4: <c>GridSource.From</c> and <c>GridSource.Fetch</c> drive the grid down one push path, not
/// two (ADR-0001). The same rows, the same columns and the same gestures — scrolling, a fling, a
/// sort, a filter — through each, and after every step the two grids paint the same thing and
/// their rows have rendered the same number of times wherever both sources hold the rows on screen.
/// The grid's own renders differ only by the states the fetching source pushes and <c>From</c> has
/// no reason to — the dropped Window of a restart, the loading flag of a fetch (ADR-0025) — at most
/// one render each. The fetching source answers at once here, with the reference implementation
/// over the same row instances, so whatever else differed would be the path.
/// </summary>
public class BundledSourcesAgreeTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly GridColumn<TestRow>[] Columns =
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
        new("Active", ColumnType.Boolean, r => r.Active, width: Fixed100),
    ];

    private static readonly IReadOnlyList<ColumnInfo<TestRow>> Infos = [.. Columns.Select(c => c.Info)];

    /// <summary>The grid, counting its own renders: bUnit's <c>RenderCount</c> on the root also
    /// counts each render of a row beneath it, which is what <see cref="ExGridRow{TRow}"/>'s own
    /// count is for.</summary>
    internal sealed class CountedGrid : ExGrid<TestRow>
    {
        internal int Renders { get; private set; }

        protected override void OnAfterRender(bool firstRender)
        {
            Renders++;
            base.OnAfterRender(firstRender);
        }
    }

    /// <summary>A bound source, and how many states it has pushed to the grid.</summary>
    private sealed class Pushes
    {
        internal Pushes(IGridSource<TestRow> source)
        {
            Source = source;
            source.StateChanged += () => Count++;
        }

        internal IGridSource<TestRow> Source { get; }

        internal int Count { get; private set; }
    }

    private IRenderedComponent<CountedGrid> RenderGrid(IGridSource<TestRow> source)
        => Render<CountedGrid>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns)
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, 140)
            .Add(g => g.ViewportWidth, 450));

    /// <summary>A server holding the same rows, answering each Query at once with the reference
    /// implementation (ADR-0023), so the rows it hands back are the very instances <c>From</c> holds.</summary>
    private static ValueTask<GridPage<TestRow>> Answer(IReadOnlyList<TestRow> rows, GridQuery query)
    {
        var result = GridQueryEngine.Apply(rows, Infos, query.Filter, query.Sorts);
        var start = Math.Min(query.Range.Start, result.Count);
        var count = Math.Min(query.Range.Count, result.Count - start);
        return ValueTask.FromResult(new GridPage<TestRow>([.. result.Skip(start).Take(count)], start, result.Count));
    }

    [Fact] // FN-4 / ADR-0001 / ADR-0025: the two bundled sources paint the same and render the same, step by step
    public async Task FN4_from_and_fetch_paint_the_same_and_render_the_same_counts()
    {
        var rows = TestRows.Many(300);
        var inMemory = new Pushes(GridSource.From(rows));
        using var fetchingSource = GridSource.Fetch<TestRow>((query, _) => Answer(rows, query), readAheadRows: 60);
        var fetching = new Pushes(fetchingSource);

        var fromGrid = RenderGrid(inMemory.Source);
        var fetchGrid = RenderGrid(fetching.Source);

        // bothHold: whether both sources hold the rows the step lands on. From holds the whole result;
        // Fetch holds its read-ahead, so a step past it is one where the two Windows differ — the
        // rows From flings over are painted from rows in hand, as Placeholders that become rows at the
        // settle, and Fetch's are not there to paint until its answer (ADR-0004). What they paint is
        // the same; what the rows cost follows the Window each source holds, not the path.
        async Task StepAsync(string step, Func<IGridSource<TestRow>, IRenderedComponent<CountedGrid>, Task> gesture,
            bool bothHold = true)
        {
            var fromBefore = Tally.Of(fromGrid, inMemory);
            var fetchBefore = Tally.Of(fetchGrid, fetching);
            await gesture(inMemory.Source, fromGrid);
            await gesture(fetching.Source, fetchGrid);
            AssertSamePaint(step, fromGrid, fetchGrid);
            if (!bothHold)
                return;

            var from = fromBefore.Since(fromGrid, inMemory);
            var fetch = fetchBefore.Since(fetchGrid, fetching);
            // Every painted row cost the same renders through either source (ADR-0003)…
            Assert.True(from.Rows.SequenceEqual(fetch.Rows),
                $"{step}: the rows rendered differently — From {string.Join(" ", from.Rows)}, Fetch {string.Join(" ", fetch.Rows)}");
            // …and the grid rendered as often under each, but for the states Fetch pushed that From
            // did not: at most one render for each, and none it did not push for.
            Assert.True(fetch.Grid - from.Grid >= 0 && fetch.Grid - from.Grid <= fetch.Pushes - from.Pushes,
                $"{step}: the grid rendered {from.Grid} times under From for {from.Pushes} pushes, "
                + $"and {fetch.Grid} times under Fetch for {fetch.Pushes}");
        }

        AssertSamePaint("first paint", fromGrid, fetchGrid);
        await StepAsync("a scroll inside the Viewport", (_, grid) => ScrollToAsync(grid.Find(".ex-scroller"), 3 * RowHeightPx));
        await StepAsync("a sort", (source, grid) => grid.InvokeAsync(() =>
            source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)])));
        await StepAsync("a filter", (source, grid) => grid.InvokeAsync(() =>
            source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
            {
                ["Active"] = new([new FilterClause(FilterOperator.Equals, true)]),
            }))));
        await StepAsync("another scroll", (_, grid) => ScrollToAsync(grid.Find(".ex-scroller"), 7 * RowHeightPx));
        await StepAsync("a fling inside both Windows, settled", async (_, grid) =>
        {
            await ScrollToAsync(grid.Find(".ex-scroller"), 40 * RowHeightPx);
            await grid.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        });
        await StepAsync("a fling past the fetched Window, settled and answered", async (_, grid) =>
        {
            await ScrollToAsync(grid.Find(".ex-scroller"), 120 * RowHeightPx);
            await grid.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        }, bothHold: false);
        await StepAsync("the filter cleared", (source, grid) => grid.InvokeAsync(() => source.OnFilterChanged(null)));
        await StepAsync("the sort cleared", (source, grid) => grid.InvokeAsync(() => source.OnSortChanged([])));
    }

    private static void AssertSamePaint(
        string step, IRenderedComponent<CountedGrid> fromGrid, IRenderedComponent<CountedGrid> fetchGrid)
    {
        fetchGrid.WaitForAssertion(() => Assert.Equal(Painted(fromGrid), Painted(fetchGrid)));
        Assert.True(Painted(fromGrid).Count > 0, $"{step}: nothing painted");
    }

    /// <summary>What a step cost: the grid's own renders, the states its source pushed, and each
    /// painted row's renders — a row kept through the step counts the renders it made in it, and a
    /// row mounted in it all of its own (ADR-0003).</summary>
    private sealed class Tally
    {
        private readonly int _grid;
        private readonly int _pushes;
        private readonly Dictionary<object, int> _rows;

        private Tally(int grid, int pushes, Dictionary<object, int> rows) => (_grid, _pushes, _rows) = (grid, pushes, rows);

        internal static Tally Of(IRenderedComponent<CountedGrid> grid, Pushes pushes)
            => new(grid.Instance.Renders, pushes.Count, grid.FindComponents<ExGridRow<TestRow>>()
                .ToDictionary(row => (object)row.Instance, row => row.RenderCount, ReferenceEqualityComparer.Instance));

        internal (int Grid, int Pushes, List<(int Row, int Renders)> Rows) Since(IRenderedComponent<CountedGrid> grid, Pushes pushes)
            => (grid.Instance.Renders - _grid,
                pushes.Count - _pushes,
                [.. grid.FindComponents<ExGridRow<TestRow>>()
                    .Select(row => (row.Instance.RowIndex, row.RenderCount - _rows.GetValueOrDefault(row.Instance)))
                    .OrderBy(entry => entry.RowIndex)]);
    }

    /// <summary>What a grid's rows paint, without the per-instance id prefix: each row's position
    /// and classes, and each cell's classes and text.</summary>
    private static List<string> Painted(IRenderedComponent<CountedGrid> grid)
        => [.. grid.FindAll(".ex-row").Select(row =>
            $"{row.GetAttribute("aria-rowindex")} {row.ClassName} | "
            + string.Join(" | ", row.QuerySelectorAll(".ex-cell").Select(Cell)))];

    private static string Cell(IElement cell) => $"{cell.ClassName}:{cell.TextContent}";
}
