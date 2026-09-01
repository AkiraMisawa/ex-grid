using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The pager (ADR-0015): another driver for Range Requests, not another architecture.
/// 200 rows, pages of 50, 20px rows in a 120px Viewport, 350px wide, 3 columns.
/// </summary>
public class PagerTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection>? onSelectionChanged = null,
        Action<RowRange>? onRangeNeeded = null,
        int windowCount = 200)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(windowCount))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(3))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.PageSize, 50);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
            if (onRangeNeeded is not null)
                ps.Add(g => g.OnRangeNeeded, onRangeNeeded);
        });

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task NextPageAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-pager button")[1].ClickAsync(new MouseEventArgs());

    [Fact] // ADR-0015 / FN-16: a pager exists when PageSize is passed, and not otherwise
    public void The_pager_exists_only_under_a_page_size()
    {
        var paged = RenderGrid();
        Assert.NotNull(paged.Find(".ex-pager"));
        Assert.Contains("1 / 4", paged.Find(".ex-pager").TextContent);

        var scrolling = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns()));
        Assert.Empty(scrolling.FindAll(".ex-pager"));
    }

    [Fact] // ADR-0015 / FN-16: a page click raises the same notification shape as a scroll
    public async Task A_page_click_raises_a_range_request()
    {
        var requests = new List<RowRange>();
        // The Window holds only the first page, so page 2 is uncovered.
        var cut = RenderGrid(onRangeNeeded: requests.Add, windowCount: 50);

        await NextPageAsync(cut);

        var request = Assert.Single(requests);
        Assert.Equal(50, request.Start);
        // The geometry answers the page's visible slice, exactly as scrolling would.
        Assert.True(request.Count > 0);
    }

    [Fact] // ADR-0015: the scrollbar spans the page, not the whole result
    public void The_spacer_spans_the_page()
    {
        var cut = RenderGrid();

        // 20px header + 50 rows × 20px, never 200 rows.
        Assert.Contains("height: 1020px", cut.Find(".ex-spacer").GetAttribute("style"));
    }

    [Fact] // ADR-0015 / SL-9: Ctrl+A selects the page; the offer to go past it is explicit
    public async Task Ctrl_a_selects_the_page_and_offers_the_whole_result()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", true, false, false, false, false));

        Assert.Equal([new SelectionRange(0, 0, 50, 3)], selection!.Ranges);
        Assert.Equal(150, selection.CellCount);

        // The offer is present, and taking it selects every row (ADR-0011's Ctrl+A).
        var offer = cut.FindAll(".ex-status button").Single(b => b.TextContent.Contains("Select all"));
        await offer.ClickAsync(new MouseEventArgs());
        Assert.Equal([new SelectionRange(0, 0, 200, 3)], selection!.Ranges);
    }

    [Fact] // ADR-0015 / SL-10: turning the page keeps the selection, and being off screen is shown
    public async Task Turning_the_page_keeps_the_selection_and_shows_the_indicator()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickCellAsync(cut, 50, 10); // row 0 on page 1
        Assert.DoesNotContain(cut.FindAll(".ex-status button"), b => b.TextContent.Contains("Go to"));

        await NextPageAsync(cut);

        Assert.False(selection!.IsEmpty);
        Assert.Contains("outside the visible range", cut.Find(".ex-status").TextContent);

        // The way back reveals it again.
        var back = cut.FindAll(".ex-status button").Single(b => b.TextContent.Contains("Go to"));
        await back.ClickAsync(new MouseEventArgs());
        Assert.DoesNotContain("outside the visible range", cut.Find(".ex-status").TextContent);
    }

    [Fact] // ADR-0015 / SL-11: a drag never turns the page — the geometry clamps at its boundary
    public async Task A_drag_at_the_page_boundary_stays_on_the_page()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickCellAsync(cut, 50, 10);

        // Park the pointer hard against the bottom edge and let the band tick away.
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = 50, OffsetY = 100 });
        for (var i = 0; i < 40; i++)
            await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(50)));

        // The selection reached the page's last row and stopped; the page did not turn.
        Assert.Equal(49, selection!.Ranges[^1].BottomRow);
        Assert.Contains("1 / 4", cut.Find(".ex-pager").TextContent);
    }

    [Fact] // ADR-0015: the keyboard crosses pages — extension continues onto the next page
    public async Task Shift_arrow_turns_the_page_and_continues()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickCellAsync(cut, 50, 10);
        // To the page's last row, then one more.
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", true, true, false, false, false)); // Ctrl+Shift+Down: extend to edge
        Assert.Equal(199, selection!.Focus.Row);

        // Ctrl+Shift+Down went to the whole result's edge, so the page turned to the last.
        Assert.Contains("4 / 4", cut.Find(".ex-pager").TextContent);
        Assert.Equal(200, selection.Ranges[^1].RowCount);
    }


    [Fact] // ADR-0012/0015: Ctrl+A names a region — the Anchor and Focus stay where they stand
    public async Task Ctrl_a_keeps_the_anchor_and_focus_where_they_stand()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickCellAsync(cut, 150, 50); // row 2, column 1

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", true, false, false, false, false));

        Assert.Equal([new SelectionRange(0, 0, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(2, 1), selection.Focus);
        Assert.Equal(new CellPosition(2, 1), selection.Anchor);
    }

    [Fact] // ADR-0028: the re-anchor is page-local — a density change must not teleport the page
    public async Task A_row_height_change_re_anchors_inside_the_page()
    {
        var cut = RenderGrid();
        await NextPageAsync(cut); // page 2: rows 50-99, page-local scroll at 0
        await ScrollToAsync(cut.Find(".ex-scroller"), 100); // first visible local row 5

        cut.Render(ps => ps.Add(g => g.RowHeight, 30d));

        // Local row 5 at the new height — never the absolute row 55, which would land
        // past the page's own scroll range and clamp the view to its end.
        Assert.Equal((5 * 30d, 0d), Js.ScrolledTo[^1]);
    }
}
