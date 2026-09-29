using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A drag across Headings (ADR-0050, item 1, and ADR-0012, both 2026-09-29; DC-42, DC-43, SR-2d)
/// and the Size Tip (ADR-0052, 2026-09-29; DC-44). A press on a Column Heading or a Row Heading
/// selects at once where the header selects; each move puts the Extent on the column or row
/// under the pointer, over the Headings or over the cells; the edge band scrolls along the
/// Heading's axis only. On a plain ExGrid the header's click sorts, so a press there selects
/// nothing until the pointer reaches another column.
///
/// 50 rows of 20px under a 20px header in a 350 × 200 Viewport, so rows 0-8 are on screen; six
/// 100px columns. Where Row Headings are declared they are 40px wide, and the columns start after
/// them.
/// </summary>
public class HeadingDragTests : GridTestContext
{
    private const double RowHeightPx = 20;

    private static readonly Func<SelectionRange, string?> SizeLabel =
        range => FormattableString.Invariant($"{range.RowCount}R x {range.ColumnCount}C");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection>? onSelection = null,
        Action<IReadOnlyList<SortSpec>>? onSort = null,
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
              .Add(g => g.OnSortChanged, onSort ?? (_ => { }));
            extra?.Invoke(ps);
        });

    private static void Declared(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.HeaderClickSelects, true);

    private static void WithRowHeadings(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.RowHeadings, row => (row + 1).ToString(CultureInfo.InvariantCulture))
             .Add(g => g.RowHeadingWidth, 40d);

    private static void WithSizeTip(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.ShowFormulaBar, true)
             .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"))
             .Add(g => g.NameBoxSizeLabel, SizeLabel);

    private static Task PressHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, bool shift = false)
        => cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, ShiftKey = shift, OffsetX = x, OffsetY = 10 });

    private static Task MoveOverHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, long buttons = 1)
        => cut.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = buttons, OffsetX = x, OffsetY = 10 });

    /// <summary>A release over the header, and the click the browser then fires on the header —
    /// the common ancestor of the press and the release, wherever along the band each was.</summary>
    private static async Task ReleaseOverHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x)
    {
        await cut.Find(".ex-header").MouseUpAsync(new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = 10 });
        await ClickHeaderAsync(cut, x);
    }

    private static Task ClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x)
        => cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = 10 });

    private static Task PressViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, ShiftKey = shift, OffsetX = x, OffsetY = y });

    private static Task MoveOverViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, long buttons = 1)
        => cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = buttons, OffsetX = x, OffsetY = y });

    private static Task ReleaseOverViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, Buttons = 0, OffsetX = x, OffsetY = y });

    private async Task TickAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(50)));

    private static string NameBox(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find("input.ex-name-box").GetAttribute("value") ?? "";

    // ---- DC-42: declared, a press selects at once and the moves extend whole columns or rows ----

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: declared, a press on a Column Heading selects the column at once
    public async Task Declared_a_press_on_a_column_heading_selects_at_once()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);

        await PressHeaderAsync(cut, 150);

        Assert.Equal([new SelectionRange(0, 1, 50, 1)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 1), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: each move puts the Extent on the column under the pointer; the Focus stays
    public async Task Declared_each_move_extends_whole_columns_to_the_pointer()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await PressHeaderAsync(cut, 150);

        await MoveOverHeaderAsync(cut, 320);
        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(49, 3), selection.Extent);

        await MoveOverHeaderAsync(cut, 20);                          // back past the pressed column
        Assert.Equal([new SelectionRange(0, 0, 50, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 1), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: the pointer over the cells keeps it a Heading drag: only its column counts
    public async Task Declared_the_pointer_over_the_cells_keeps_it_a_heading_drag()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await PressHeaderAsync(cut, 150);

        await MoveOverViewportAsync(cut, 320, 105);                  // row 5 of column 3

        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 1), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: Shift+press extends from the Focus's column, and the drag goes on; the Focus stays
    public async Task Declared_shift_press_extends_from_the_focus_column_and_the_drag_goes_on()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await PressViewportAsync(cut, 50, 45);                        // (2, 0)
        await ReleaseOverViewportAsync(cut, 50, 45);

        await PressHeaderAsync(cut, 250, shift: true);
        Assert.Equal([new SelectionRange(0, 0, 50, 3)], selection!.Ranges);

        await MoveOverHeaderAsync(cut, 320);
        Assert.Equal([new SelectionRange(0, 0, 50, 4)], selection.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: the release and the click it brings select nothing more, and nothing sorts
    public async Task Declared_the_release_and_its_click_change_nothing()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s, Declared);
        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 320);

        await ReleaseOverHeaderAsync(cut, 320);

        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        Assert.Null(sorted);
        // The drag is over: nothing listens for the moves any more.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => MoveOverHeaderAsync(cut, 20));
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: a drag across Row Headings selects whole rows, the pointer over the cells included
    public async Task A_drag_across_row_headings_selects_whole_rows()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: WithRowHeadings);
        await PressViewportAsync(cut, 10, 45);                        // row 2's heading

        await MoveOverViewportAsync(cut, 10, 105);                    // row 5's heading
        Assert.Equal([new SelectionRange(2, 0, 4, 6)], selection!.Ranges);

        await MoveOverViewportAsync(cut, 250, 145);                   // row 7, over the cells of column 2
        Assert.Equal([new SelectionRange(2, 0, 6, 6)], selection.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Focus);
        Assert.Equal(new CellPosition(7, 5), selection.Extent);

        await ReleaseOverViewportAsync(cut, 250, 145);
        Assert.Equal([new SelectionRange(2, 0, 6, 6)], selection.Ranges);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: Shift+press on a Row Heading extends from the Focus's row, and the drag goes on
    public async Task Shift_press_on_a_row_heading_extends_and_the_drag_goes_on()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: WithRowHeadings);
        await PressViewportAsync(cut, 150, 25);                       // (1, 1)
        await ReleaseOverViewportAsync(cut, 150, 25);

        await PressViewportAsync(cut, 10, 65, shift: true);           // row 3's heading
        Assert.Equal([new SelectionRange(1, 0, 3, 6)], selection!.Ranges);

        await MoveOverViewportAsync(cut, 10, 125);                    // row 6
        Assert.Equal([new SelectionRange(1, 0, 6, 6)], selection.Ranges);
        Assert.Equal(new CellPosition(1, 1), selection.Focus);
    }

    [Fact] // ADR-0016 / ticket 21: a drag on a column's resize grip still resizes, and is not a Heading drag
    public async Task A_drag_on_a_resize_grip_resizes_and_selects_nothing()
    {
        GridSelection? selection = null;
        ColumnWidthChange? change = null;
        var cut = RenderGrid(s => selection = s, extra: ps =>
        {
            Declared(ps);
            ps.Add(g => g.OnColumnWidthChanged, c => change = c);
        });

        await cut.FindAll(".ex-resize-grip")[1].MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, ClientX = 200 });
        await cut.Find(".ex-grid").MouseMoveAsync(new MouseEventArgs { Buttons = 1, ClientX = 260 });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 260 });

        Assert.Equal(new ColumnWidthChange(TestRows.ColumnName(1), 160), change);
        Assert.Null(selection);
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => MoveOverHeaderAsync(cut, 320));
    }

    // ---- DC-43: the edge band scrolls along the Heading's axis only ----

    [Fact] // ADR-0050 item 1 / ADR-0008 / DC-43: a Column Heading drag at the right edge scrolls sideways, and the Extent follows
    public async Task A_column_heading_drag_scrolls_sideways_at_the_edge()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await PressHeaderAsync(cut, 150);

        await MoveOverHeaderAsync(cut, 349);                          // the very edge of the band
        await TickAsync(cut);

        var (top, left) = Assert.Single(Js.ScrolledTo);
        Assert.Equal(0, top);
        Assert.True(left > 0);
        // The column under the unmoved pointer, now further along: 349 + left.
        Assert.Equal((int)((349 + left) / 100), selection!.Extent.Column);
        Assert.Equal(1, selection.Ranges[0].LeftColumn);
    }

    [Fact] // ADR-0050 item 1 / ADR-0008 / DC-43: over the cells at the bottom edge, a Column Heading drag never scrolls down
    public async Task A_column_heading_drag_never_scrolls_down()
    {
        var cut = RenderGrid(extra: Declared);
        await PressHeaderAsync(cut, 150);

        await MoveOverViewportAsync(cut, 250, 179);                   // the bottom band, the middle of the width
        await TickAsync(cut);
        await TickAsync(cut);

        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0050 item 1 / ADR-0008 / DC-43: a Row Heading drag scrolls down at the bottom edge, and never sideways
    public async Task A_row_heading_drag_scrolls_down_and_never_sideways()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: WithRowHeadings);
        await PressViewportAsync(cut, 10, 45);

        await MoveOverViewportAsync(cut, 345, 179);                   // the bottom-right corner: both bands
        await TickAsync(cut);
        await TickAsync(cut);

        Assert.NotEmpty(Js.ScrolledTo);
        Assert.All(Js.ScrolledTo, scroll => Assert.Equal(0, scroll.Left));
        Assert.True(Js.ScrolledTo[^1].Top > 0);
        Assert.Equal(0, selection!.Ranges[0].LeftColumn);
        Assert.Equal(6, selection.Ranges[0].ColumnCount);
        Assert.True(selection.Extent.Row > 8);
    }

    // ---- SR-2d: a plain ExGrid's header, where the click sorts ----

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: a press on a plain header selects nothing by itself
    public async Task Plain_a_press_on_a_header_selects_nothing()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);

        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 180);                          // still column 1

        Assert.Null(selection);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d, SR-1: released on the same column, having crossed no other, it is a click and sorts
    public async Task Plain_released_on_the_same_column_it_sorts()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 180);
        await ReleaseOverHeaderAsync(cut, 180);

        Assert.Equal([new SortSpec(TestRows.ColumnName(1), SortDirection.Ascending)], sorted);
        Assert.Null(selection);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: reaching another column's header makes it a drag of whole columns, the Focus on the first visible row
    public async Task Plain_reaching_another_header_makes_it_a_drag()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 320);

        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 1), selection.Focus);
        Assert.Null(sorted);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: reaching another column's cells makes it a drag too
    public async Task Plain_reaching_another_columns_cells_makes_it_a_drag()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);

        await PressHeaderAsync(cut, 150);
        await MoveOverViewportAsync(cut, 150, 105);                   // column 1's own cells: still a click
        Assert.Null(selection);
        await MoveOverViewportAsync(cut, 250, 105);

        Assert.Equal([new SelectionRange(0, 1, 50, 2)], selection!.Ranges);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: a press that became a drag never sorts, even released back over the pressed column
    public async Task Plain_a_drag_released_back_on_the_pressed_column_does_not_sort()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 320);
        await MoveOverHeaderAsync(cut, 150);
        await ReleaseOverHeaderAsync(cut, 150);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 1, 50, 1)], selection!.Ranges);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: the click after a press and a release on different headers does not sort, with no move between
    public async Task Plain_the_click_after_a_release_on_another_header_does_not_sort()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await PressHeaderAsync(cut, 150);
        await ReleaseOverHeaderAsync(cut, 320);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: the same when the release was not heard — on a circuit it can outrun the handler that hears it
    public async Task Plain_a_click_on_another_header_ends_the_press_as_a_drag()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await PressHeaderAsync(cut, 150);
        await ClickHeaderAsync(cut, 320);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => MoveOverHeaderAsync(cut, 20));
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2d: Shift+press then a drag on a plain header extends from the Focus's column and never sorts
    public async Task Plain_shift_press_and_drag_extends_whole_columns()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);
        await PressViewportAsync(cut, 50, 45);                        // (2, 0)
        await ReleaseOverViewportAsync(cut, 50, 45);

        await PressHeaderAsync(cut, 150, shift: true);
        await MoveOverHeaderAsync(cut, 250);
        await ReleaseOverHeaderAsync(cut, 250);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(0, 0, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Focus);
    }

    [Fact] // ADR-0012/0011: where a reorder is wired, a header drag reorders, as before — the Heading drag is not built there
    public async Task Where_a_reorder_is_wired_a_header_drag_still_reorders()
    {
        GridSelection? selection = null;
        IReadOnlyList<string>? order = null;
        var cut = RenderGrid(s => selection = s, extra: ps => ps.Add(g => g.OnColumnOrderChanged, o => order = o));

        await cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, ClientX = 150, OffsetX = 150, OffsetY = 10 });
        await cut.Find(".ex-grid").MouseMoveAsync(new MouseEventArgs { Buttons = 1, ClientX = 330 });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 330 });

        Assert.NotNull(order);
        Assert.Null(selection);
    }

    // ---- DC-44: the Size Tip ----

    [Fact] // ADR-0052 (2026-09-29) / DC-44: over one column there is no Size Tip, and the Name Box names the Focus
    public async Task Over_one_column_there_is_no_size_tip()
    {
        var cut = RenderGrid(extra: ps => { Declared(ps); WithSizeTip(ps); });

        await PressHeaderAsync(cut, 150);

        Assert.Empty(cut.FindAll(".ex-size-tip"));
        Assert.Equal("R1C2", NameBox(cut));
    }

    [Fact] // ADR-0052 (2026-09-29) / DC-44: over more than one column the Size Tip shows the label at the Extent's Heading, and the Name Box is empty
    public async Task Over_several_columns_the_size_tip_shows_and_the_name_box_is_empty()
    {
        var cut = RenderGrid(extra: ps => { Declared(ps); WithSizeTip(ps); });
        await PressHeaderAsync(cut, 150);

        await MoveOverHeaderAsync(cut, 250);

        var tip = cut.Find(".ex-size-tip");
        Assert.Equal("50R x 2C", tip.TextContent);
        Assert.Equal("", NameBox(cut));
        var style = Style(tip);
        // At column 2's left edge, just under the header band, below the Formula Bar.
        Assert.Equal(200, style["left"]);
        Assert.Equal(ExGrid<TestRow>.DefaultCellMetrics.EstimatePx("50R x 2C"), style["width"]);
        Assert.Equal(FormulaBarPx(cut) + RowHeightPx, style["top"]);
    }

    [Fact] // ADR-0052 (2026-09-29) / ADR-0040 / DC-44: at the right edge the Size Tip is kept inside the grid's box
    public async Task The_size_tip_stays_inside_the_grids_box()
    {
        var cut = RenderGrid(extra: ps => { Declared(ps); WithSizeTip(ps); });
        await PressHeaderAsync(cut, 150);

        await MoveOverHeaderAsync(cut, 340);                          // column 3, whose left edge is 300 of 350

        var style = Style(cut.Find(".ex-size-tip"));
        Assert.True(style["left"] + style["width"] <= 350);
        Assert.True(style["left"] >= 0);
    }

    [Fact] // ADR-0052 (2026-09-29) / DC-44: a Row Heading drag's Size Tip stands beside the Extent's Row Heading
    public async Task A_row_heading_drag_shows_the_size_tip_at_the_extents_row()
    {
        var cut = RenderGrid(extra: ps => { WithRowHeadings(ps); WithSizeTip(ps); });
        await PressViewportAsync(cut, 10, 45);                        // row 2
        Assert.Empty(cut.FindAll(".ex-size-tip"));
        Assert.Equal("R3C1", NameBox(cut));

        await MoveOverViewportAsync(cut, 10, 105);                    // row 5

        var tip = cut.Find(".ex-size-tip");
        Assert.Equal("4R x 6C", tip.TextContent);
        Assert.Equal("", NameBox(cut));
        var style = Style(tip);
        Assert.Equal(40, style["left"]);                              // clear of the Row Headings
        Assert.Equal(FormulaBarPx(cut) + RowHeightPx + (5 * RowHeightPx), style["top"]);
    }

    [Fact] // ADR-0052 (2026-09-29) / DC-44: without a size label there is no Size Tip, and the Name Box names the Focus throughout
    public async Task Without_a_size_label_there_is_no_size_tip()
    {
        var cut = RenderGrid(extra: ps =>
        {
            Declared(ps);
            ps.Add(g => g.ShowFormulaBar, true)
              .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"));
        });
        await PressHeaderAsync(cut, 150);

        await MoveOverHeaderAsync(cut, 250);

        Assert.Empty(cut.FindAll(".ex-size-tip"));
        Assert.Equal("R1C2", NameBox(cut));
    }

    [Fact] // ADR-0052 (2026-09-29) / DC-44: the release takes the Size Tip away, and the Name Box names the Focus again
    public async Task The_release_takes_the_size_tip_away()
    {
        var cut = RenderGrid(extra: ps => { Declared(ps); WithSizeTip(ps); });
        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 250);

        await ReleaseOverViewportAsync(cut, 250, 50);

        Assert.Empty(cut.FindAll(".ex-size-tip"));
        Assert.Equal("R1C2", NameBox(cut));
    }

    [Fact] // ADR-0052 (2026-09-29) / DC-44: a drag over cells is unchanged — the size is in the Name Box, and there is no Size Tip
    public async Task A_drag_over_cells_shows_no_size_tip()
    {
        var cut = RenderGrid(extra: ps => { Declared(ps); WithSizeTip(ps); });
        await PressViewportAsync(cut, 50, 10);

        await MoveOverViewportAsync(cut, 250, 50);

        Assert.Empty(cut.FindAll(".ex-size-tip"));
        Assert.Equal("3R x 3C", NameBox(cut));
    }

    [Fact] // ADR-0052 (2026-09-29) / DC-44 / ADR-0003: the Size Tip is redrawn only when the Extent moves to another Heading
    public async Task The_size_tip_is_redrawn_once_per_heading_crossed()
    {
        var cut = RenderGrid(extra: ps => { Declared(ps); WithSizeTip(ps); });
        await PressHeaderAsync(cut, 150);
        await MoveOverHeaderAsync(cut, 250);
        var tipStyle = cut.Find(".ex-size-tip").GetAttribute("style");
        var renders = RootRenders(cut);

        // Along column 2, over its heading and over its cells: nothing to redraw.
        await MoveOverHeaderAsync(cut, 260);
        await MoveOverHeaderAsync(cut, 290);
        await MoveOverViewportAsync(cut, 270, 60);
        await MoveOverViewportAsync(cut, 230, 120);
        Assert.Equal(renders, RootRenders(cut));

        // Onto column 3: one render, and the tip moves with the Extent.
        await MoveOverViewportAsync(cut, 320, 120);
        Assert.Equal(renders + 1, RootRenders(cut));
        Assert.Equal("50R x 3C", cut.Find(".ex-size-tip").TextContent);
        Assert.NotEqual(tipStyle, cut.Find(".ex-size-tip").GetAttribute("style"));
    }

    [Fact] // ADR-0029 / ADR-0027 / DC-44: the Size Tip is painted from its Visual Tokens, and the stylesheet sizes nothing about it
    public void The_stylesheet_paints_the_size_tip_from_its_tokens()
    {
        var css = ShippedStylesheet();
        var rule = Regex.Match(css, @"\.ex-size-tip\s*\{([^}]*)\}");

        Assert.True(rule.Success, "no .ex-size-tip rule in ex-grid.css");
        var body = rule.Groups[1].Value;
        Assert.Contains("var(--ex-size-tip-background", body);
        Assert.Contains("var(--ex-size-tip-color", body);
        Assert.Contains("var(--ex-size-tip-outline", body);
        Assert.Contains("pointer-events: none", body);
        // Its width is the C# estimate, written inline; no literal to pair with it (ADR-0027).
        Assert.DoesNotMatch(@"(^|[;\s])(min-|max-)?width\s*:", body);
    }

    /// <summary>How many times the root has rendered. bUnit's RenderCount on the root adds its
    /// children's renders; the rows skip every render here (ADR-0003), and no other child
    /// component is painted, so the two agree.</summary>
    private static int RootRenders(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.RenderCount - cut.FindComponents<ExGridRow<TestRow>>().Sum(r => r.RenderCount);

    private static double FormulaBarPx(IRenderedComponent<ExGrid<TestRow>> cut)
        => Style(cut.Find(".ex-formula-bar"))["height"];

    /// <summary>The pixel lengths of an inline style, by property.</summary>
    private static Dictionary<string, double> Style(AngleSharp.Dom.IElement element)
        => Regex.Matches(element.GetAttribute("style") ?? "", @"([a-z-]+):\s*(-?[\d.]+)px")
            .ToDictionary(m => m.Groups[1].Value, m => double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));

    private static string ShippedStylesheet()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ExGrid.staticwebassets.runtime.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        return document.RootElement.GetProperty("ContentRoots").EnumerateArray()
            .Select(root => root.GetString()!)
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "ex-grid.css", SearchOption.AllDirectories))
            .Select(File.ReadAllText)
            .First();
    }
}
