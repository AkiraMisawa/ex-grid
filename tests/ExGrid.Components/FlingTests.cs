using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Waiting for data and skipping paint during a fling are one mechanism, so they paint
/// the same thing (ADR-0004). 20px rows in a 100px Viewport, of which the header takes
/// the first 20: a Viewport is five rows, and a move of more than five is a fling.
/// </summary>
public class FlingTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(List<RowRange>? asked = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(1000))
              .Add(g => g.TotalCount, 1000)
              .Add(g => g.Columns, TestRows.Columns())
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, ViewportHeightPx);
            if (asked is not null)
                ps.Add(g => g.OnRangeNeeded, asked.Add);
        });

    [Fact] // ADR-0004: a move of more than a Viewport paints Placeholders, then fills them in
    public async Task A_fling_paints_placeholders_until_scrolling_settles()
    {
        var cut = RenderGrid();

        // 100 rows at once: every row changes, so the row boundaries buy nothing.
        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * RowHeightPx);

        // With nothing pinned there is nothing left to paint, so a flung row carries no
        // cells at all — the cost being skipped is exactly those cells.
        Assert.Equal(5, cut.FindAll(".ex-placeholder").Count);
        Assert.Empty(cut.FindAll(".ex-cell"));

        Clock.Advance(SettleDelay);

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-placeholder"));
            Assert.Equal(5, cut.FindComponents<ExGridRow<TestRow>>().Count);
        });
        Assert.Equal("Row 000100", cut.FindAll(".ex-row")[0].QuerySelector(".ex-cell")!.TextContent);
    }

    [Fact] // ADR-0004: the delay measures stillness — scrolling again restarts it
    public async Task Scrolling_again_before_the_delay_keeps_the_placeholders()
    {
        var cut = RenderGrid();
        await ScrollToAsync(cut.Find(".ex-scroller"), 100 * RowHeightPx);

        Clock.Advance(SettleDelay - TimeSpan.FromMilliseconds(10));
        await ScrollToAsync(cut.Find(".ex-scroller"), 101 * RowHeightPx);
        Clock.Advance(SettleDelay - TimeSpan.FromMilliseconds(10));

        Assert.NotEmpty(cut.FindAll(".ex-placeholder"));

        Clock.Advance(TimeSpan.FromMilliseconds(10));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-placeholder")));
    }

    [Fact] // ADR-0004: a move within one Viewport is ordinary scrolling, not a fling
    public async Task A_move_of_one_viewport_paints_rows_straight_away()
    {
        var cut = RenderGrid();

        await ScrollToAsync(cut.Find(".ex-scroller"), 5 * RowHeightPx);

        Assert.Empty(cut.FindAll(".ex-placeholder"));
        Assert.Equal(5, cut.FindComponents<ExGridRow<TestRow>>().Count);
    }

    [Fact] // ADR-0004/0001: the ranges a fling passes over are not asked for — only the one it lands on
    public async Task A_fling_asks_only_for_the_range_it_settles_on()
    {
        var asked = new List<RowRange>();
        // A Window of rows 0-9 only, so everything past it is uncovered and would be
        // asked for if the fling did not hold its tongue.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.TotalCount, 1000)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.OnRangeNeeded, asked.Add));

        await ScrollToAsync(cut.Find(".ex-scroller"), 50 * RowHeightPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), 200 * RowHeightPx);
        Assert.Empty(asked);

        Clock.Advance(SettleDelay);

        cut.WaitForAssertion(() => Assert.Equal([new RowRange(200, 5)], asked));
    }
}
