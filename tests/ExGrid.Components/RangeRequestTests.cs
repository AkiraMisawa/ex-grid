using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The grid asks and keeps painting; it never fetches and never waits (ADR-0001).
/// 20px rows in a 100px Viewport, of which the header takes the first 20, so a Viewport is five rows.
/// </summary>
public class RangeRequestTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] window, List<RowRange> asked, int windowStart = 0, int? total = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.WindowStart, windowStart)
            .Add(g => g.TotalCount, total)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.OnRangeNeeded, asked.Add));

    [Fact] // ADR-0001: with a total but no rows yet, the grid asks for the first Viewport
    public void An_empty_window_with_a_total_asks_for_the_first_viewport()
    {
        var asked = new List<RowRange>();

        RenderGrid([], asked, total: 1000);

        Assert.Equal([new RowRange(0, 5)], asked);
    }

    [Fact] // ADR-0001: a Viewport the Window covers needs nothing
    public void A_covered_viewport_asks_for_nothing()
    {
        var asked = new List<RowRange>();

        RenderGrid(TestRows.Many(50), asked, total: 1000);

        Assert.Empty(asked);
    }

    [Fact] // ADR-0001: nothing to show is not a range — an empty Range Request is refused at construction
    public void A_total_of_zero_asks_for_nothing()
    {
        var asked = new List<RowRange>();

        RenderGrid([], asked, total: 0);

        Assert.Empty(asked);
    }

    [Fact] // ADR-0001: scrolling within the Window is silent; leaving it asks for what is on screen
    public async Task Scrolling_past_the_window_asks_for_the_visible_range()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(10), asked, total: 1000);

        // Rows 2-6 — still inside the Window's 0-9.
        await ScrollToAsync(cut.Find(".ex-scroller"), 2 * RowHeightPx);
        Assert.Empty(asked);

        // Rows 6-10 — row 10 is past the Window's end.
        await ScrollToAsync(cut.Find(".ex-scroller"), 6 * RowHeightPx);
        Assert.Equal([new RowRange(6, 5)], asked);
    }

    [Fact] // ADR-0001: the grid does not wait — an unanswered range is asked for once, not on every render
    public async Task An_unanswered_range_is_not_asked_for_again()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(10), asked, total: 1000);
        // Two ordinary scrolls rather than one jump: crossing a whole Viewport at once
        // is a fling, and a fling deliberately asks for nothing (ADR-0004).
        await ScrollToAsync(cut.Find(".ex-scroller"), 2 * RowHeightPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), 6 * RowHeightPx);

        // An unrelated parameter change, with the Window rebuilt inline as a Consumer
        // naturally would: a fresh list instance holding the same rows is not an answer,
        // and re-asking for it is what would spin.
        cut.Render(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, 1000)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.IsLoading, true)
            .Add(g => g.OnRangeNeeded, asked.Add));

        Assert.Equal([new RowRange(6, 5)], asked);
    }

    [Fact] // ADR-0001: a Consumer answering in the callback settles — the loop is finite by construction
    public void A_consumer_that_answers_immediately_is_asked_once()
    {
        var cut = Render<PushHost>(ps => ps
            .Add(h => h.AllRows, TestRows.Many(1000))
            .Add(h => h.RowHeight, RowHeightPx)
            .Add(h => h.ViewportHeight, ViewportHeightPx));

        Assert.Equal([new RowRange(0, 5)], cut.Instance.Requests);
        Assert.Equal(5, cut.FindComponents<ExGridRow<TestRow>>().Count);
        Assert.Empty(cut.FindAll(".ex-placeholder"));
    }

    [Fact] // ADR-0001: read-ahead is the Consumer's job — a wider answer keeps later scrolling silent
    public async Task A_consumer_answering_with_read_ahead_is_not_asked_again_while_inside_it()
    {
        var cut = Render<PushHost>(ps => ps
            .Add(h => h.AllRows, TestRows.Many(1000))
            .Add(h => h.RowHeight, RowHeightPx)
            .Add(h => h.ViewportHeight, ViewportHeightPx)
            .Add(h => h.ReadAhead, 20));

        await ScrollToAsync(cut.Find(".ex-scroller"), 5 * RowHeightPx);

        Assert.Equal([new RowRange(0, 5)], cut.Instance.Requests);
    }
}
