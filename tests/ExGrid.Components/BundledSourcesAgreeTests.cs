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
/// <para>The grid's own renders are not the same, and FN-4 as restated on 2026-10-11 does not ask
/// them to be: they exceed <c>From</c>'s by the states the fetching source pushes and <c>From</c>
/// has no reason to — the dropped Window of a restart, the loading flag of a fetch (ADR-0025) —
/// and by nothing else, which
/// <see cref="FN4_the_grid_renders_more_under_fetch_by_exactly_the_states_fetch_pushes"/> holds.
/// The rows' renders are compared wherever both sources hold the rows: across a fling past the
/// fetched Window the two hold different Windows.</para>
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

    // ---- FN-4 as restated on 2026-10-11: the grid's own renders ----------------------------------

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

    /// <summary>The states a source pushed to a grid: each push noted with the grid's render count
    /// at that moment, so pushes no render separates — a fetch answered in the call that asked for
    /// it — are the one state the next render takes in.</summary>
    private sealed class PushedStates
    {
        private readonly List<int> _rendersAtPush = [];

        internal PushedStates(IGridSource<TestRow> source, Func<int> renders)
            => source.StateChanged += () => _rendersAtPush.Add(renders());

        internal int Count => _rendersAtPush.Count;

        /// <summary>The states pushed since <paramref name="pushesBefore"/> pushes had been noted.</summary>
        internal int StatesSince(int pushesBefore) => _rendersAtPush.Skip(pushesBefore).Distinct().Count();
    }

    [Fact] // FN-4 / ADR-0001 / ADR-0025: the same painting at every step, the same row renders where both sources hold the rows, and the grid renders more under Fetch only for the states Fetch pushes that From does not
    public async Task FN4_the_grid_renders_more_under_fetch_by_exactly_the_states_fetch_pushes()
    {
        // FN-4 as restated on 2026-10-11: one push path, so the grids paint alike at every step and
        // their rows cost alike wherever both sources hold them; the grid's own renders may exceed
        // From's by the states Fetch pushes that From does not — the loading state of a sort and a
        // filter, the restart when either is cleared, a fetch past its read-ahead (ADR-0025) — and by
        // nothing else. The messages give every count.
        var rows = TestRows.Many(300);
        var inMemory = GridSource.From(rows);
        using var fetching = GridSource.Fetch<TestRow>((query, _) => Answer(rows, query), readAheadRows: 60);

        IRenderedComponent<CountedGrid> Counted(IGridSource<TestRow> source)
            => Render<CountedGrid>(ps => ps
                .Add(g => g.Source, source)
                .Add(g => g.Columns, Columns)
                .Add(g => g.RowHeight, RowHeightPx)
                .Add(g => g.ViewportHeight, 140)
                .Add(g => g.ViewportWidth, 450));
        var fromGrid = Counted(inMemory);
        var fetchGrid = Counted(fetching);
        var fromPushed = new PushedStates(inMemory, () => fromGrid.Instance.Renders);
        var fetchPushed = new PushedStates(fetching, () => fetchGrid.Instance.Renders);
        AssertSamePaint("first paint", fromGrid, fetchGrid);
        var failures = new List<string>();
        var extraRenders = 0;

        async Task StepAsync(string step, Func<IGridSource<TestRow>, IRenderedComponent<CountedGrid>, Task> gesture,
            bool bothHold = true)
        {
            var (fromGridBefore, fromPushesBefore, fromRowsBefore) = (fromGrid.Instance.Renders, fromPushed.Count, RowRenders.Of(fromGrid));
            var (fetchGridBefore, fetchPushesBefore, fetchRowsBefore) = (fetchGrid.Instance.Renders, fetchPushed.Count, RowRenders.Of(fetchGrid));
            await gesture(inMemory, fromGrid);
            await gesture(fetching, fetchGrid);
            AssertSamePaint(step, fromGrid, fetchGrid);

            // The grid's own renders: more under Fetch by exactly the states Fetch pushed beyond From's.
            var fromRenders = fromGrid.Instance.Renders - fromGridBefore;
            var fetchRenders = fetchGrid.Instance.Renders - fetchGridBefore;
            var (fromStates, fetchStates) = (fromPushed.StatesSince(fromPushesBefore), fetchPushed.StatesSince(fetchPushesBefore));
            extraRenders += fetchRenders - fromRenders;
            if (fetchRenders - fromRenders != fetchStates - fromStates)
                failures.Add($"{step}: the grid rendered {fromRenders} times under From for {fromStates} states pushed, "
                    + $"and {fetchRenders} times under Fetch for {fetchStates}");

            // Every painted row cost the same renders through either source (ADR-0003), wherever both hold
            // it: past Fetch's read-ahead the two Windows differ — From's rows paint as Placeholders that
            // become rows at the settle, and Fetch's are not there to paint until its answer (ADR-0004).
            if (!bothHold)
                return;
            var fromRows = fromRowsBefore.Since(fromGrid);
            var fetchRows = fetchRowsBefore.Since(fetchGrid);
            if (!fromRows.SequenceEqual(fetchRows))
                failures.Add($"{step}: the rows rendered differently — From {string.Join(" ", fromRows)}, Fetch {string.Join(" ", fetchRows)}");
        }

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

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        // The fetching source's own states are there to count: a run in which it pushed nothing more
        // than From would assert nothing about them.
        Assert.True(extraRenders > 0, "Fetch pushed no state that From did not, so nothing was compared.");
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
