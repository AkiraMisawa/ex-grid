using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// LAB ONLY, never merged (<c>lab/remote-read-ahead</c>, ticket 14 of the live-data spec): the grid
/// raising its Range Request before its Window runs out, once the rows on screen and a margin either
/// side are not all in it, as ag-grid's <c>rowBuffer</c> rows ask for their block. 20px rows in a
/// 100px Viewport, of which the header takes the first 20, so a Viewport is five rows.
/// </summary>
public class LabRangeMarginTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(TestRow[] window, List<RowRange> asked, int total, int margin)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.TotalCount, total)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.OnRangeNeeded, asked.Add)
            .Add(g => g.RangeMarginRows, margin));

    [Fact] // Ticket 14, lab: with a margin, the rows on screen are asked for once the margin leaves the Window
    public async Task A_margin_asks_for_the_rows_on_screen_before_the_Window_runs_out()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(10), asked, total: 1000, margin: 2);

        // Rows 2-6, and 2 either side: 0-8, inside the Window's 0-9.
        await ScrollToAsync(cut.Find(".ex-scroller"), 2 * RowHeightPx);
        Assert.Empty(asked);

        // Rows 4-8 are in the Window, but 2 below them are not: asked for, and nothing is a Placeholder.
        await ScrollToAsync(cut.Find(".ex-scroller"), 4 * RowHeightPx);
        Assert.Equal([new RowRange(4, 5)], asked);
        Assert.Empty(cut.FindAll(".ex-placeholder"));
    }

    [Fact] // Ticket 14, lab: without a margin, the grid asks once the rows on screen leave the Window, as ADR-0001 has it
    public async Task No_margin_asks_once_the_rows_on_screen_leave_the_Window()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(10), asked, total: 1000, margin: 0);

        await ScrollToAsync(cut.Find(".ex-scroller"), 2 * RowHeightPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), 4 * RowHeightPx);
        Assert.Empty(asked);

        await ScrollToAsync(cut.Find(".ex-scroller"), 6 * RowHeightPx);
        Assert.Equal([new RowRange(6, 5)], asked);
    }

    [Fact] // Ticket 14, lab: the margin is clamped into the total, so a Window that holds the last rows asks nothing
    public async Task The_margin_is_clamped_into_the_total()
    {
        var asked = new List<RowRange>();
        var cut = RenderGrid(TestRows.Many(12), asked, total: 12, margin: 2);

        // Two scrolls, not one jump: crossing a whole Viewport at once is a fling (ADR-0004).
        await ScrollToAsync(cut.Find(".ex-scroller"), 3 * RowHeightPx);
        await ScrollToAsync(cut.Find(".ex-scroller"), 7 * RowHeightPx);

        Assert.Empty(asked);
    }
}
