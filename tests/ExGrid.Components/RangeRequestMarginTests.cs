using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Range Request Margin (ADR-0183, FN-3a): at M rows, the grid raises its Range Request as soon
/// as the rows on screen plus M either side, clamped into the total, are not all in the Window, while
/// the rows on screen may still be held. The request names the rows on screen, and is not raised
/// again until the Window moves. At 0, the default, the grid asks as ADR-0001 has it (FN-3). Counted
/// in invocations, never in time. 20px rows in a 100px Viewport, of which the header takes the first
/// 20, so the rows on screen are five, and a scroll of more than four rows at once is a fling
/// (ADR-0004), which asks only once it settles: every scroll here moves four rows or fewer.
/// </summary>
public class RangeRequestMarginTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] window, List<RowRange> asked, int total, int margin, int windowStart = 0)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.WindowStart, windowStart)
            .Add(g => g.TotalCount, total)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.OnRangeNeeded, asked.Add)
            .Add(g => g.RangeRequestMargin, margin));

    /// <summary>Hands the grid another Window from row 0, every other parameter as rendered.</summary>
    private static void Push(IRenderedComponent<ExGrid<TestRow>> cut, TestRow[] window, List<RowRange> asked, bool loading)
        => cut.Render(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.WindowStart, 0)
            .Add(g => g.TotalCount, 1000)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.IsLoading, loading)
            .Add(g => g.OnRangeNeeded, asked.Add)
            .Add(g => g.RangeRequestMargin, 10));

    private async Task ScrollToRowAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row)
        => await ScrollToAsync(cut.Find(".ex-scroller"), row * RowHeightPx);

    private static IRenderedComponent<PushHost> Host(GridTestContext context, int readAhead, int margin)
        => context.Render<PushHost>(ps => ps
            .Add(h => h.AllRows, TestRows.Many(1000))
            .Add(h => h.RowHeight, RowHeightPx)
            .Add(h => h.ViewportHeight, ViewportHeightPx)
            .Add(h => h.ReadAhead, readAhead)
            .Add(h => h.RangeRequestMargin, margin));

    [Fact] // ADR-0183 (FN-3a): the margin is 0 unless set, and asks nothing ADR-0001 would not
    public void ADR0183_The_margin_is_0_by_default()
        => Assert.Equal(0, new ExGrid<TestRow>().RangeRequestMargin);

    [Fact] // ADR-0183 (FN-3a): at a margin of 0 the grid asks once the rows on screen leave the Window, as FN-3 has it
    public async Task ADR0183_At_a_margin_of_0_the_rows_on_screen_are_asked_for_once_they_leave_the_Window()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(10), asked, total: 1000, margin: 0);

        // Rows 4-8: every one held, with one row of the Window below them.
        await ScrollToRowAsync(cut, 2);
        await ScrollToRowAsync(cut, 4);
        Assert.Empty(asked);

        // Rows 6-10: row 10 is past the Window's end.
        await ScrollToRowAsync(cut, 6);
        Assert.Equal([new RowRange(6, 5)], asked);
    }

    [Fact] // ADR-0183 (FN-3a): at M = 10, near the Window's end, the rows on screen are asked for while every one of them is held
    public async Task ADR0183_A_margin_asks_for_the_rows_on_screen_before_the_Window_runs_out_at_its_end()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(30), asked, total: 1000, margin: 10);

        // Rows 15-19, and 10 either side: 5-29, the last of them the Window's last row.
        foreach (var row in new[] { 4, 8, 12, 15 })
            await ScrollToRowAsync(cut, row);
        Assert.Empty(asked);

        // Rows 16-20 are all held, but the tenth row below them, row 30, is not: the rows on screen
        // are asked for, and none of them is a Placeholder.
        await ScrollToRowAsync(cut, 16);
        Assert.Equal([new RowRange(16, 5)], asked);
        Assert.Empty(cut.FindAll(".ex-placeholder"));
    }

    [Fact] // ADR-0183 (FN-3a): at M = 10, near the Window's start, scrolling up asks for the rows on screen while every one of them is held
    public async Task ADR0183_A_margin_asks_for_the_rows_on_screen_before_the_Window_runs_out_at_its_start()
    {
        // A Consumer that answers at once with 20 rows either side of the rows asked for.
        var cut = Host(this, readAhead: 20, margin: 10);
        var grid = cut.FindComponent<ExGrid<TestRow>>();
        foreach (var row in new[] { 4, 8, 11, 15, 19, 22 })
            await ScrollToAsync(grid.Find(".ex-scroller"), row * RowHeightPx);
        // Asked for the first Viewport, then twice on the way down, each time ten rows short of the
        // Window's end: at row 11 (rows 11-15, and row 25 not held) and row 22 (row 36 not held).
        Assert.Equal([new RowRange(0, 5), new RowRange(11, 5), new RowRange(22, 5)], cut.Instance.Requests);
        // The answer to rows 22-26 starts at row 2.
        Assert.Equal(2, grid.Instance.WindowStart);

        // Back up to rows 12-16: 10 rows above them is row 2, the Window's first row. Nothing.
        await ScrollToAsync(grid.Find(".ex-scroller"), 18 * RowHeightPx);
        await ScrollToAsync(grid.Find(".ex-scroller"), 14 * RowHeightPx);
        await ScrollToAsync(grid.Find(".ex-scroller"), 12 * RowHeightPx);
        Assert.Equal(3, cut.Instance.Requests.Count);

        // Rows 11-15: all held, from row 2 on, but row 1 is not. Asked for, before the Window runs out.
        await ScrollToAsync(grid.Find(".ex-scroller"), 11 * RowHeightPx);
        Assert.Equal(new RowRange(11, 5), cut.Instance.Requests[^1]);
        Assert.Equal(4, cut.Instance.Requests.Count);
        Assert.Empty(grid.FindAll(".ex-placeholder"));
    }

    [Fact] // ADR-0183 (FN-3a): the margin is clamped into the total at both ends, so a Window that holds the whole result asks nothing
    public async Task ADR0183_The_margin_is_clamped_into_the_total_at_both_ends()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(30), asked, total: 30, margin: 10);

        // At the top, ten rows above row 0 are none; at the foot, ten rows below row 29 are none.
        foreach (var row in new[] { 0, 4, 8, 12, 16, 20, 24, 26 })
            await ScrollToRowAsync(cut, row);

        Assert.Empty(asked);
        Assert.Contains(cut.FindComponents<ExGridRow<TestRow>>(), r => ReferenceEquals(r.Instance.Row, cut.Instance.Window[29]));
    }

    [Fact] // ADR-0183 (FN-3a): an early request is not raised again until the Window moves, and the Window that answers it asks nothing
    public async Task ADR0183_An_early_request_is_not_repeated_until_the_Window_moves()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(30), asked, total: 1000, margin: 10);
        foreach (var row in new[] { 4, 8, 12, 16 })
            await ScrollToRowAsync(cut, row);
        Assert.Equal([new RowRange(16, 5)], asked);

        // An unrelated parameter change, the same rows in a fresh list: not an answer, not asked again.
        Push(cut, TestRows.Many(30), asked, loading: true);
        Assert.Equal([new RowRange(16, 5)], asked);

        // The answer: the rows on screen and 20 either side. Nothing is asked.
        Push(cut, TestRows.Many(41), asked, loading: false);
        await ScrollToRowAsync(cut, 20);
        Assert.Equal([new RowRange(16, 5)], asked);
    }

    [Theory] // ADR-0183 (FN-3a): a Consumer that reads ahead less than the margin is asked at every move, twice — once per Window position — which is why the margin is 0 unless set
    // Exactly the rows asked for: asked at every move at a margin of 0 too, once.
    [InlineData(0, 0, new[] { 0, 1, 2, 3, 4 })]
    // Exactly the rows asked for, at a margin of 10: asked, answered, and asked once more for the
    // same rows against the Window that answered them, at every move.
    [InlineData(0, 10, new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4 })]
    // Three rows either side: once the rows on screen leave the Window, at a margin of 0.
    [InlineData(3, 0, new[] { 0, 4 })]
    // Three rows either side never reach a margin of 10: asked twice at every move.
    [InlineData(3, 10, new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4 })]
    // Twenty rows either side reach past it: asked once, as at 0.
    [InlineData(20, 10, new[] { 0 })]
    public async Task ADR0183_A_consumer_that_reads_ahead_less_than_the_margin_is_asked_at_every_move(
        int readAhead, int margin, int[] askedStarts)
    {
        var cut = Host(this, readAhead, margin);
        var scroller = cut.FindComponent<ExGrid<TestRow>>().Find(".ex-scroller");
        foreach (var row in new[] { 1, 2, 3, 4 })
            await ScrollToAsync(scroller, row * RowHeightPx);

        Assert.Equal(askedStarts.Select(start => new RowRange(start, 5)), cut.Instance.Requests);
        Assert.Empty(cut.FindComponent<ExGrid<TestRow>>().FindAll(".ex-placeholder"));
    }

    [Fact] // ADR-0183 (FN-3a, FN-4): a Source is asked early as the push form is, by the same path
    public async Task ADR0183_A_Source_is_asked_before_the_Window_runs_out_as_the_push_form_is()
    {
        var source = new TestSource();
        source.Push(TestRows.Many(30), totalCount: 1000);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.RangeRequestMargin, 10));

        foreach (var row in new[] { 4, 8, 12, 15 })
            await ScrollToRowAsync(cut, row);
        Assert.Empty(source.Requested);
        await ScrollToRowAsync(cut, 16);
        Assert.Equal([new RowRange(16, 5)], source.Requested);
    }

    [Fact] // ADR-0183 (FN-3a): GridSource.Fetch sets no margin and keeps ADR-0025's rule: an early request its Window still covers fetches nothing
    public async Task ADR0183_GridSource_Fetch_fetches_nothing_for_an_early_request()
    {
        var all = TestRows.Many(1000);
        var fetched = new List<RowRange>();
        using var source = GridSource.Fetch<TestRow>((query, _) =>
        {
            fetched.Add(query.Range);
            var end = Math.Min(all.Length, query.Range.Start + query.Range.Count);
            return ValueTask.FromResult(new GridPage<TestRow>(all[query.Range.Start..end], query.Range.Start, all.Length));
        });
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.RangeRequestMargin, 10));
        // The first page, which the source fetches itself (ADR-0025).
        Assert.Equal([new RowRange(0, FetchingGridSource<TestRow>.DefaultPageRows)], fetched);

        // Down to rows 92-96, within ten rows of the page's end, and on to rows 95-99, its last: the
        // grid asks early, and the source's Window still covers the rows on screen.
        for (var row = 4; row <= 92; row += 4)
            await ScrollToRowAsync(cut, row);
        await ScrollToRowAsync(cut, 95);
        Assert.Single(fetched);

        // Rows 96-100: row 100 is past the Window. Fetched, widened by the source's read-ahead.
        await ScrollToRowAsync(cut, 96);
        Assert.Equal(2, fetched.Count);
        Assert.Equal(new RowRange(36, 125), fetched[^1]);
    }

    [Fact] // ADR-0183 (FN-3a): a negative margin is refused by name
    public void ADR0183_A_negative_margin_is_refused_by_name()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => RenderGrid(TestRows.Many(10), [], total: 1000, margin: -1));
        Assert.Equal("RangeRequestMargin", refusal.ParamName);
        Assert.Contains("ADR-0183", refusal.Message, StringComparison.Ordinal);
    }
}
