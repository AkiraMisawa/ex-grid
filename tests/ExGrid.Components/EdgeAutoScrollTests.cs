using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The edge-band auto-scroll (ADR-0008 / SL-12..15): a drag whose pointer sits in the
/// Viewport's inner 20px scrolls on a timer, faster the deeper in, capped under the
/// fling threshold, and the pointer leaving the element stops it.
///
/// 200 rows of 20px in a 120px Viewport: the rows band is 100px, so RowsPerViewport is
/// 6 and the band's ceiling is 5 rows per tick.
/// </summary>
public class EdgeAutoScrollTests : GridTestContext
{
    private const double RowHeightPx = 20;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Action<GridSelection>? onSelectionChanged = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(3))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
        });

    private static Task MouseDownAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task MouseMoveAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, long buttons = 1)
        => cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = buttons, OffsetX = x, OffsetY = y });

    private async Task TickAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(50)));

    [Theory] // ADR-0008 / SL-12: rows per tick rise monotonically with depth
    [InlineData(81, 1)]   // just inside the band's inner lip
    [InlineData(90, 4)]   // halfway in
    [InlineData(100, 5)]  // at the very edge — the ceiling, one short of the fling threshold
    public async Task The_rate_rises_with_depth(double pointerY, int expectedRows)
    {
        var cut = RenderGrid();
        await MouseDownAsync(cut, 50, 10);

        await MouseMoveAsync(cut, 50, pointerY);
        await TickAsync(cut);

        Assert.Equal(expectedRows * RowHeightPx, Js.ScrolledTo[^1].Top);
    }

    [Fact] // ADR-0008 / SL-12: the ticks compound, and the selection's far edge tracks the scroll
    public async Task Ticks_compound_and_the_selection_follows()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await MouseDownAsync(cut, 50, 10);

        await MouseMoveAsync(cut, 50, 100);   // the very edge: 5 rows per tick
        await TickAsync(cut);
        await TickAsync(cut);
        await TickAsync(cut);

        Assert.Equal(15 * RowHeightPx, Js.ScrolledTo[^1].Top);
        // The pointer sits at the bottom edge; after 15 rows of scroll the cell under
        // it is row (300 + 100) / 20 = 20 — the selection's far edge has tracked it.
        Assert.NotNull(selection);
        Assert.Equal(0, selection!.Ranges[^1].TopRow);
        Assert.Equal(20, selection.Ranges[^1].BottomRow);
    }

    [Fact] // ADR-0008 / SL-13: no Placeholder appears while selecting — the rate stays under the fling
    public async Task The_auto_scroll_never_flings()
    {
        var cut = RenderGrid();
        await MouseDownAsync(cut, 50, 10);
        await MouseMoveAsync(cut, 50, 100);

        for (var i = 0; i < 10; i++)
            await TickAsync(cut);

        Assert.Empty(cut.FindAll(".ex-placeholder"));
    }

    [Fact] // ADR-0008 / SL-14: the pointer leaving the element stops the auto-scroll
    public async Task Leaving_the_grid_stops_the_scroll()
    {
        var cut = RenderGrid();
        await MouseDownAsync(cut, 50, 10);
        await MouseMoveAsync(cut, 50, 100);
        await TickAsync(cut);
        var scrolls = Js.ScrolledTo.Count;

        await cut.Find(".ex-grid").MouseLeaveAsync(new MouseEventArgs());
        await TickAsync(cut);
        await TickAsync(cut);

        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0008 / SL-15: returning with the button held resumes; released, the drag ends
    public async Task Returning_resumes_or_ends_by_the_button()
    {
        var cut = RenderGrid();
        await MouseDownAsync(cut, 50, 10);
        await MouseMoveAsync(cut, 50, 100);
        await TickAsync(cut);
        await cut.Find(".ex-grid").MouseLeaveAsync(new MouseEventArgs());

        // Back with the button still held: the drag resumes and so does the band.
        await MouseMoveAsync(cut, 50, 100, buttons: 1);
        var scrolls = Js.ScrolledTo.Count;
        await TickAsync(cut);
        Assert.Equal(scrolls + 1, Js.ScrolledTo.Count);

        // Back with it released: the drag ends and the band stays quiet.
        await MouseMoveAsync(cut, 50, 100, buttons: 0);
        scrolls = Js.ScrolledTo.Count;
        await TickAsync(cut);
        await TickAsync(cut);
        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0008: a pointer in the middle of the Viewport arms nothing
    public async Task The_middle_of_the_viewport_scrolls_nothing()
    {
        var cut = RenderGrid();
        await MouseDownAsync(cut, 50, 10);

        await MouseMoveAsync(cut, 50, 50);
        await TickAsync(cut);
        await TickAsync(cut);

        Assert.Empty(Js.ScrolledTo);
    }
}
