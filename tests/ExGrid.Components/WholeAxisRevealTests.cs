using System.Globalization;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// An axis the range spans end to end is not scrolled for (ADR-0052, "What the user's run
/// settled", 2026-09-29; SR-2c). While extending, the grid keeps the Extent in view only on
/// an axis the range holding the Focus does not span end to end, judged after the move. The
/// Extent itself is unchanged: a whole column selected from its first row has its Extent on
/// the last row, and Shift+→ moves it to the last row of the next column without the view
/// going there.
///
/// 50 rows of 20px under a 20px header in a 350 × 200 Viewport, so rows 0-8 are on screen; six
/// 100px columns, so column 3 is the first that does not fit. Where Row Headings are declared
/// they are 40px wide.
/// </summary>
public class WholeAxisRevealTests : GridTestContext
{
    private const double RowHeightPx = 20;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection>? onSelection = null,
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, TestRows.Wide(6))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, onSelection ?? (_ => { }))
              .Add(g => g.OnSortChanged, _ => { });
            extra?.Invoke(ps);
        });

    private static void Declared(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.HeaderClickSelects, true);

    private static void WithRowHeadings(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.RowHeadings, row => (row + 1).ToString(CultureInfo.InvariantCulture))
             .Add(g => g.RowHeadingWidth, 40d);

    private static Task ClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, bool shift = false)
        => cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, ShiftKey = shift, OffsetX = x, OffsetY = 10 });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task ReleaseAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y });

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: a Column Heading click with the view at the top, then Shift+→, never scrolls down
    public async Task Shift_right_over_a_whole_column_never_scrolls_down()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, Declared);
        await ClickHeaderAsync(cut, 150);                           // column 1, the Focus on row 0

        await KeyAsync(cut, "ArrowRight", shift: true);

        Assert.Equal([new SelectionRange(0, 1, 50, 2)], selection!.Ranges);
        // The Extent is the last row of the next column, as before: only the view is judged.
        Assert.Equal(new CellPosition(49, 2), selection.Extent);
        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: whole columns still scroll sideways to keep the Extent's column in view
    public async Task Shift_right_past_the_edge_scrolls_sideways_and_never_down()
    {
        var cut = RenderGrid(extra: Declared);
        await ClickHeaderAsync(cut, 250);                           // column 2, ending at 300 of 350

        await KeyAsync(cut, "ArrowRight", shift: true);             // column 3 ends at 400

        var (top, left) = Assert.Single(Js.ScrolledTo);
        Assert.Equal(0, top);
        Assert.Equal(50, left);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: a header Shift+click on a plain grid reaches the same state, and takes the same rule
    public async Task A_header_shift_click_then_shift_right_never_scrolls_down()
    {
        var cut = RenderGrid();
        await ClickHeaderAsync(cut, 150, shift: true);              // from nothing: column 1, the Focus on row 0

        await KeyAsync(cut, "ArrowRight", shift: true);

        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: Ctrl+Space on row 1 then Shift+→ never scrolls down
    public async Task Ctrl_space_on_the_first_row_then_shift_right_never_scrolls_down()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressAsync(cut, 150, 10);                              // (0, 1)
        await ReleaseAsync(cut, 150, 10);

        await KeyAsync(cut, " ", ctrl: true);
        await KeyAsync(cut, "ArrowRight", shift: true);

        Assert.Equal([new SelectionRange(0, 1, 50, 2)], selection!.Ranges);
        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: Ctrl+Shift+→ over whole columns runs sideways only
    public async Task Ctrl_shift_right_over_whole_columns_scrolls_sideways_only()
    {
        var cut = RenderGrid(extra: Declared);
        await ClickHeaderAsync(cut, 150);

        await KeyAsync(cut, "ArrowRight", ctrl: true, shift: true);

        // Column 5, the last, right-aligned: it ends at 600, and 600 − 350 = 250.
        Assert.Equal((0d, 250d), Js.ScrolledTo[^1]);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: whole rows scroll down and never sideways
    public async Task Shift_down_over_a_whole_row_never_scrolls_sideways()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, WithRowHeadings);
        await PressAsync(cut, 10, 45);                               // row 2's heading, with the view at the left
        await ReleaseAsync(cut, 10, 45);
        Assert.Equal(new CellPosition(2, 5), selection!.Extent);   // the last column, off screen at 540..640

        for (var i = 0; i < 7; i++)
            await KeyAsync(cut, "ArrowDown", shift: true);         // the Extent onto row 9, the first below the view

        Assert.Equal([new SelectionRange(2, 0, 8, 6)], selection.Ranges);
        Assert.NotEmpty(Js.ScrolledTo);
        Assert.All(Js.ScrolledTo, scroll => Assert.Equal(0, scroll.Left));
        Assert.Equal(RowHeightPx, Js.ScrolledTo[^1].Top);           // row 9 brought to the bottom edge
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: Shift+PageDown over whole rows moves the view down a page, and never sideways
    public async Task Shift_pagedown_over_a_whole_row_never_scrolls_sideways()
    {
        var cut = RenderGrid(extra: WithRowHeadings);
        await PressAsync(cut, 10, 45);
        await ReleaseAsync(cut, 10, 45);

        await KeyAsync(cut, "PageDown", shift: true);

        var (top, left) = Js.ScrolledTo[^1];
        Assert.Equal(9 * RowHeightPx, top);
        Assert.Equal(0, left);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: over the whole grid, neither axis scrolls
    public async Task Over_the_whole_grid_neither_axis_scrolls()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressAsync(cut, 50, 10);                               // (0, 0)
        await ReleaseAsync(cut, 50, 10);
        await KeyAsync(cut, " ", ctrl: true);                        // column 0, every row

        await KeyAsync(cut, "ArrowRight", ctrl: true, shift: true);  // every column: the whole grid

        Assert.Equal([new SelectionRange(0, 0, 50, 6)], selection!.Ranges);
        Assert.Equal(new CellPosition(49, 5), selection.Extent);
        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: a whole column that stops being whole scrolls as any range does
    public async Task Shift_up_from_a_whole_column_scrolls_to_the_extent()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, Declared);
        await ClickHeaderAsync(cut, 250);                           // column 2, the Focus on row 0

        await KeyAsync(cut, "ArrowUp", shift: true);

        Assert.Equal([new SelectionRange(0, 2, 49, 1)], selection!.Ranges);
        Assert.Equal(new CellPosition(48, 2), selection.Extent);
        // Row 48 brought to the bottom edge: (48 + 1) × 20 − 180.
        Assert.Equal((800d, 0d), Js.ScrolledTo[^1]);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: a whole row that stops being whole scrolls sideways to show the Extent
    public async Task Shift_left_from_a_whole_row_scrolls_to_the_extent()
    {
        var cut = RenderGrid(extra: WithRowHeadings);
        await PressAsync(cut, 10, 45);
        await ReleaseAsync(cut, 10, 45);

        await KeyAsync(cut, "ArrowLeft", shift: true);              // the Extent onto column 4, 440..540

        Assert.Equal((0d, 190d), Js.ScrolledTo[^1]);
    }

    [Fact] // ADR-0052 (2026-09-29) / ADR-0012: Shift+Home names the start of the row, and still goes there over whole rows
    public async Task Shift_home_making_whole_rows_still_goes_to_the_start_of_the_row()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressAsync(cut, 50, 10);
        await ReleaseAsync(cut, 50, 10);
        await KeyAsync(cut, "End", ctrl: true);                      // (49, 5), the view at the far corner
        await ScrollToAsync(cut.Find(".ex-scroller"), Js.ScrolledTo[^1].Top, Js.ScrolledTo[^1].Left);

        await KeyAsync(cut, "Home", shift: true);                   // every column of row 49

        Assert.Equal([new SelectionRange(49, 0, 1, 6)], selection!.Ranges);
        Assert.Equal(0d, Js.ScrolledTo[^1].Left);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: the Focus reveal is unchanged — Ctrl+Backspace brings back a whole column's Focus
    public async Task Ctrl_backspace_still_reveals_the_focus_of_a_whole_column()
    {
        var cut = RenderGrid(extra: Declared);
        await ClickHeaderAsync(cut, 150);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 400);

        await KeyAsync(cut, "Backspace", ctrl: true);

        Assert.Equal((0d, 0d), Js.ScrolledTo[^1]);
    }

    [Fact] // ADR-0052 (2026-09-29) / SR-2c: a non-extending move from a whole column still reveals the Focus
    public async Task A_plain_arrow_from_a_whole_column_still_reveals_the_focus()
    {
        var cut = RenderGrid(extra: Declared);
        await ClickHeaderAsync(cut, 150);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 400);

        await KeyAsync(cut, "ArrowDown");                            // collapses onto (1, 1)

        Assert.Equal((RowHeightPx, 0d), Js.ScrolledTo[^1]);
    }
}
