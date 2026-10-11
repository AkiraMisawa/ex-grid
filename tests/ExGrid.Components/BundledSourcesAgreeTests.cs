using AngleSharp.Dom;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// FN-4: <c>GridSource.From</c> and <c>GridSource.Fetch</c> drive the grid down one push path, not
/// two (ADR-0001). The same rows, the same columns and the same gestures — scrolling, a fling, a
/// sort, a filter — through each, and after every step the two grids paint the same thing, and
/// their rows have rendered the same number of times wherever both sources hold the rows on screen.
/// The fetching source answers at once here, with the reference implementation over the same row
/// instances, so whatever else differed would be the path.
///
/// <para>Not asserted here, and not met as the Definition of Done words it ("identical render
/// counts"): the grid's own renders, which differ by the states the fetching source pushes and
/// <c>From</c> has no reason to — the dropped Window of a restart, the loading flag of a fetch
/// (ADR-0025) — and the rows' renders across a fling past the fetched Window, where the two sources
/// hold different Windows. Whether FN-4's counts are read to allow either is a decision no ADR
/// records; the test that is its pass condition waits for it.</para>
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

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(IGridSource<TestRow> source)
        => Render<ExGrid<TestRow>>(ps => ps
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

    [Fact] // FN-4 / ADR-0001 / ADR-0003: the two bundled sources paint the same at every step, and their rows render the same where both hold them
    public async Task FN4_from_and_fetch_paint_the_same_and_their_rows_render_the_same()
    {
        var rows = TestRows.Many(300);
        var inMemory = GridSource.From(rows);
        using var fetching = GridSource.Fetch<TestRow>((query, _) => Answer(rows, query), readAheadRows: 60);

        var fromGrid = RenderGrid(inMemory);
        var fetchGrid = RenderGrid(fetching);

        // bothHold: whether both sources hold the rows the step lands on. From holds the whole result;
        // Fetch holds its read-ahead, so a step past it is one where the two Windows differ — the
        // rows From flings over are painted from rows in hand, as Placeholders that become rows at the
        // settle, and Fetch's are not there to paint until its answer (ADR-0004). What they paint is
        // the same there too; what the rows cost there is the part of FN-4 this test leaves open.
        async Task StepAsync(string step, Func<IGridSource<TestRow>, IRenderedComponent<ExGrid<TestRow>>, Task> gesture,
            bool bothHold = true)
        {
            var fromBefore = RowRenders.Of(fromGrid);
            var fetchBefore = RowRenders.Of(fetchGrid);
            await gesture(inMemory, fromGrid);
            await gesture(fetching, fetchGrid);
            AssertSamePaint(step, fromGrid, fetchGrid);
            if (!bothHold)
                return;

            // Every painted row cost the same renders through either source (ADR-0003).
            var from = fromBefore.Since(fromGrid);
            var fetch = fetchBefore.Since(fetchGrid);
            Assert.True(from.SequenceEqual(fetch),
                $"{step}: the rows rendered differently — From {string.Join(" ", from)}, Fetch {string.Join(" ", fetch)}");
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
        string step, IRenderedComponent<ExGrid<TestRow>> fromGrid, IRenderedComponent<ExGrid<TestRow>> fetchGrid)
    {
        fetchGrid.WaitForAssertion(() => Assert.Equal(Painted(fromGrid), Painted(fetchGrid)));
        Assert.True(Painted(fromGrid).Count > 0, $"{step}: nothing painted");
    }

    /// <summary>What a step cost the rows: each painted row's renders — a row kept through the step
    /// counts the renders it made in it, and a row mounted in it all of its own (ADR-0003).</summary>
    private sealed class RowRenders
    {
        private readonly Dictionary<object, int> _rows;

        private RowRenders(Dictionary<object, int> rows) => _rows = rows;

        internal static RowRenders Of(IRenderedComponent<ExGrid<TestRow>> grid)
            => new(grid.FindComponents<ExGridRow<TestRow>>()
                .ToDictionary(row => (object)row.Instance, row => row.RenderCount, ReferenceEqualityComparer.Instance));

        internal List<(int Row, int Renders)> Since(IRenderedComponent<ExGrid<TestRow>> grid)
            => [.. grid.FindComponents<ExGridRow<TestRow>>()
                .Select(row => (row.Instance.RowIndex, row.RenderCount - _rows.GetValueOrDefault(row.Instance)))
                .OrderBy(entry => entry.RowIndex)];
    }

    /// <summary>What a grid's rows paint, without the per-instance id prefix: each row's position
    /// and classes, and each cell's classes and text.</summary>
    private static List<string> Painted(IRenderedComponent<ExGrid<TestRow>> grid)
        => [.. grid.FindAll(".ex-row").Select(row =>
            $"{row.GetAttribute("aria-rowindex")} {row.ClassName} | "
            + string.Join(" | ", row.QuerySelectorAll(".ex-cell").Select(Cell)))];

    private static string Cell(IElement cell) => $"{cell.ClassName}:{cell.TextContent}";
}
