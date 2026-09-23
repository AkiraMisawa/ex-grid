using AngleSharp.Dom;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Selection is painted by an overlay and driven by the mouse (ADR-0008/0011/0012). The
/// meaning of every gesture belongs to <see cref="GridSelection"/> and is pinned in the
/// pure suite; what is pinned here is the wiring — that a pointer position becomes the
/// right cell, that the rectangles land where the cells are, and that none of it reaches
/// the rows.
///
/// 100 columns of 100px in a 350px Viewport, 20px rows in a 100px Viewport of which the
/// header takes the first 20: five rows painted, and cell (r, c) is at
/// (c × 100 + 50, (r − first painted row) × 20 + 10).
/// </summary>
public class SelectionTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const double ViewportHeightPx = 100;
    private const double ViewportWidthPx = 350;
    private const int RowsPerViewport = 5;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        int pinnedColumnCount = 0,
        int windowCount = 200,
        Action<GridSelection>? onSelectionChanged = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(windowCount))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(100))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, ViewportHeightPx)
              .Add(g => g.ViewportWidth, ViewportWidthPx)
              .Add(g => g.PinnedColumnCount, pinnedColumnCount);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false, bool ctrl = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            OffsetX = x,
            OffsetY = y,
            Button = 0,
            Buttons = 1,
            ShiftKey = shift,
            CtrlKey = ctrl,
        });

    private static Task DragAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, long buttons = 1)
        => cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { OffsetX = x, OffsetY = y, Buttons = buttons });

    /// <summary>The centre of cell (row, column) as the browser would report it, with the
    /// row measured from the first painted one.</summary>
    private static (double X, double Y) Cell(int row, int column, int firstPaintedRow = 0)
        => ((column * 100) + 50, ((row - firstPaintedRow) * RowHeightPx) + 10);

    private static string[] Rects(IRenderedComponent<ExGrid<TestRow>> cut, string layer = ".ex-selection")
        => [.. cut.FindAll($"{layer} > .ex-range").Select(r => r.GetAttribute("style")!)];

    private static string Rect(double left, double top, double width, double height)
        => FormattableString.Invariant($"left: {left}px; top: {top}px; width: {width}px; height: {height}px");

    [Fact] // ADR-0008: selection is an overlay rectangle, not a class on the selected cells
    public async Task A_click_paints_one_rectangle_over_the_cell_it_names()
    {
        var cut = RenderGrid();
        var (x, y) = Cell(row: 1, column: 1);

        await PressAsync(cut, x, y);

        Assert.Equal([Rect(100, 20, 100, 20)], Rects(cut));
        Assert.Equal(Rect(100, 20, 100, 20), cut.Find(".ex-focus").GetAttribute("style"));
        // Nothing was painted on the cells themselves: a row's markup is data only.
        Assert.Empty(cut.FindAll(".ex-cell.ex-selected"));
    }

    [Fact] // ADR-0012: Shift+click keeps the Anchor and redraws its range to the cell pointed at
    public async Task Shift_click_redraws_the_range_from_the_anchor()
    {
        var cut = RenderGrid();

        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        await PressAsync(cut, Cell(2, 2).X, Cell(2, 2).Y, shift: true);

        Assert.Equal([Rect(0, 0, 300, 60)], Rects(cut));
    }

    [Fact] // ADR-0011/0012: Ctrl+click on an unselected cell adds a range — scattered is the natural case
    public async Task Ctrl_click_adds_a_second_range()
    {
        var cut = RenderGrid();

        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        await PressAsync(cut, Cell(2, 2).X, Cell(2, 2).Y, ctrl: true);

        Assert.Equal([Rect(0, 0, 100, 20), Rect(200, 40, 100, 20)], Rects(cut));
    }

    [Fact] // ADR-0012: Ctrl+click on a selected cell deselects it — a rectangle splits into at most four
    public async Task Ctrl_click_on_a_selected_cell_punches_a_hole()
    {
        var cut = RenderGrid();

        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        await PressAsync(cut, Cell(2, 2).X, Cell(2, 2).Y, shift: true);
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y, ctrl: true);

        // The band above, the band below, then what is left of the middle row either
        // side of the hole.
        Assert.Equal(
            [Rect(0, 0, 300, 20), Rect(0, 40, 300, 20), Rect(0, 20, 100, 20), Rect(200, 20, 100, 20)],
            Rects(cut));
    }

    [Fact] // ADR-0011: a drag is mousedown = Click and mousemove = ExtendTo, not a third path
    public async Task Dragging_grows_the_range_from_where_it_started()
    {
        var cut = RenderGrid();

        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);
        await DragAsync(cut, Cell(3, 3).X, Cell(3, 3).Y);

        Assert.Equal([Rect(100, 20, 300, 60)], Rects(cut));
    }

    [Fact] // The button released outside the grid delivers no mouseup, and this is what ends the drag
    public async Task A_move_with_the_button_no_longer_held_ends_the_drag()
    {
        var cut = RenderGrid();

        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);
        // Without the Buttons check this would keep extending a gesture the user finished
        // somewhere else, the moment the pointer came back over the grid.
        await DragAsync(cut, Cell(2, 2).X, Cell(2, 2).Y, buttons: 0);

        Assert.Equal([Rect(100, 20, 100, 20)], Rects(cut));
        // And the gesture being over, the grid has stopped listening for moves again.
        await Assert.ThrowsAsync<MissingEventHandlerException>(
            () => DragAsync(cut, Cell(4, 4).X, Cell(4, 4).Y));
    }

    [Fact] // A pointer merely crossing the grid must not cost an event per frame (a wire round trip on Server)
    public async Task The_grid_does_not_listen_for_moves_until_a_drag_begins()
    {
        var cut = RenderGrid();

        await Assert.ThrowsAsync<MissingEventHandlerException>(
            () => DragAsync(cut, Cell(1, 1).X, Cell(1, 1).Y));

        // And it does listen once one has.
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);
        await DragAsync(cut, Cell(2, 2).X, Cell(2, 2).Y);
        Assert.Equal([Rect(100, 20, 200, 40)], Rects(cut));
    }

    [Fact] // A click that selects nothing new still has to attach the drag handler
    public async Task Pressing_the_cell_already_selected_still_begins_a_drag()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);

        // The same cell again: the selection is unchanged, so the render that would carry
        // the handler onto the element is the one there is no other reason to do.
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);
        await DragAsync(cut, Cell(3, 3).X, Cell(3, 3).Y);

        Assert.Equal([Rect(100, 20, 300, 60)], Rects(cut));
    }

    [Fact] // ADR-0004: pin every column and the strip beside them still has to name a cell
    public async Task A_click_past_the_last_column_when_everything_is_pinned_selects_the_last_one()
    {
        // Three 100px columns, all pinned, in a 350px Viewport: the row Viewport is wider
        // than the columns, so there is empty space to the right of them to click in.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(200))
            .Add(g => g.TotalCount, 200)
            .Add(g => g.Columns, TestRows.Wide(3))
            .Add(g => g.RowHeight, RowHeightPx)
            .Add(g => g.ViewportHeight, ViewportHeightPx)
            .Add(g => g.ViewportWidth, ViewportWidthPx)
            .Add(g => g.PinnedColumnCount, 3));

        await PressAsync(cut, 340, 10);

        Assert.Equal([Rect(200, 0, 100, 20)], Rects(cut, ".ex-selection-pinned"));
    }

    [Fact] // ADR-0008's non-performance reason: the row knows nothing about selection, so it does not repaint
    public async Task Selecting_does_not_repaint_a_single_row()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        var painted = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        // Every visible row changes membership at once — the operation that made the
        // class-on-cells approach miss a frame (ADR-0008, measured).
        await PressAsync(cut, Cell(4, 3).X, Cell(4, 3).Y, shift: true);

        Assert.Equal([Rect(0, 0, 400, 100)], Rects(cut));
        Assert.Equal(painted, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount));
    }

    [Fact] // PF-5 / ADR-0008: painting a selection costs per rectangle, never per cell or per area
    public async Task Selecting_the_whole_result_costs_what_eleven_cells_cost()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelectionChanged: s => selection = s);
        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        // Past the five painted rows, so both selections reach off screen and anything
        // painted for that reason (ADR-0015) is painted for both.
        for (var i = 0; i < 10; i++)
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        Assert.Equal(11, selection!.CellCount);
        var elements = cut.FindAll(".ex-grid *").Count;
        var rows = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToArray();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", true, false, false, false, false));

        Assert.Equal(200 * 100, selection!.CellCount);
        Assert.Single(cut.FindAll(".ex-selection > .ex-range"));
        Assert.Equal(elements, cut.FindAll(".ex-grid *").Count);
        Assert.Equal(rows, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount));
    }

    [Fact] // A drag that stays inside one cell arrives once a frame and changes nothing
    public async Task Dragging_within_one_cell_renders_nothing()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);
        var renders = cut.RenderCount;

        await DragAsync(cut, Cell(1, 1).X + 10, Cell(1, 1).Y + 2);
        await DragAsync(cut, Cell(1, 1).X - 10, Cell(1, 1).Y - 2);

        Assert.Equal(renders, cut.RenderCount);
    }

    [Fact] // ADR-0011: positions point at different rows once the order changes, so the selection goes
    public async Task The_selection_is_dropped_when_the_row_sequence_version_changes()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        Assert.Empty(Rects(cut));
    }

    [Fact] // ADR-0011: values updating under an unchanged order is the frequent case, and it keeps the selection
    public async Task The_selection_survives_a_window_pushed_under_the_same_order()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);

        cut.Render(ps => ps.Add(g => g.Window, TestRows.Many(200)));

        Assert.Equal([Rect(100, 20, 100, 20)], Rects(cut));
    }

    [Fact] // ADR-0011: the column axis re-maps exactly as the row axis does
    public async Task The_selection_is_dropped_when_the_visible_columns_change()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);

        cut.Render(ps => ps.Add(g => g.Columns, TestRows.Wide(99)));

        Assert.Empty(Rects(cut));
    }

    [Fact] // The trap: a Consumer building its columns inline hands over a fresh array every render
    public async Task The_selection_survives_an_equivalent_column_list_built_again()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);

        // Same columns, same order, different array. Judging by identity would clear the
        // selection on every render and make selecting anything impossible.
        cut.Render(ps => ps.Add(g => g.Columns, TestRows.Wide(100)));

        Assert.Equal([Rect(100, 20, 100, 20)], Rects(cut));
    }

    [Fact] // ADR-0004: a Pinned Column covers the Viewport's edge, so its part of a range travels with it
    public async Task A_range_across_the_pinned_boundary_is_painted_in_two_pieces()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);

        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        await PressAsync(cut, Cell(0, 2).X, Cell(0, 2).Y, shift: true);

        Assert.Equal([Rect(0, 0, 200, 20)], Rects(cut, ".ex-selection-pinned"));
        Assert.Equal([Rect(200, 0, 100, 20)], Rects(cut));
    }

    [Fact] // ADR-0004: the pixels under the pinned block belong to it, not to what has scrolled beneath
    public async Task A_click_under_the_pinned_block_names_the_pinned_column()
    {
        var cut = RenderGrid(pinnedColumnCount: 2);
        // Two ordinary moves rather than one jump, so this is not a fling.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 300);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 0, left: 600);

        // Content x 650 is column 6's territory, but at this offset the pinned pair is
        // drawn over it: the pointer is on column 0.
        await PressAsync(cut, 650, 10);

        Assert.Equal([Rect(0, 0, 100, 20)], Rects(cut, ".ex-selection-pinned"));
        Assert.Empty(Rects(cut));
    }

    [Fact] // ADR-0011: selection is positions, so a row with no data behind it can still be selected
    public async Task A_placeholder_row_can_be_selected()
    {
        var cut = RenderGrid(windowCount: 3);

        Assert.Equal(2, cut.FindAll(".ex-row.ex-placeholder").Count);
        await PressAsync(cut, Cell(4, 0).X, Cell(4, 0).Y);

        Assert.Equal([Rect(0, 80, 100, 20)], Rects(cut));
    }

    [Fact] // The pointer's offsets are relative to the painted rows, so the slice has to go back on
    public async Task A_click_after_scrolling_names_the_row_that_is_actually_there()
    {
        var cut = RenderGrid();
        // Two rows down, still not a fling.
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 2 * RowHeightPx);

        // The pointer's y is measured from the first painted row, which is now row 2.
        await PressAsync(cut, Cell(3, 0, firstPaintedRow: 2).X, Cell(3, 0, firstPaintedRow: 2).Y);

        // Row 3, painted second in a slice starting at row 2 — so the rectangle's top is
        // one row down inside the Viewport, and the cell it names is row 3.
        Assert.Equal([Rect(0, 20, 100, 20)], Rects(cut));
    }

    [Fact] // ADR-0008 refined: the overlay lives inside the transform, so its top follows the painted slice
    public async Task The_overlay_moves_with_the_painted_slice()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(3, 0).X, Cell(3, 0).Y);
        Assert.Equal([Rect(0, 60, 100, 20)], Rects(cut));

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 2 * RowHeightPx);

        // The same cell, two rows further up the Viewport.
        Assert.Equal([Rect(0, 20, 100, 20)], Rects(cut));
    }

    [Fact] // ADR-0014: the Consumer is told, and the count needs no data to compute
    public async Task The_consumer_is_told_what_is_selected()
    {
        var reported = new List<GridSelection>();
        var cut = RenderGrid(onSelectionChanged: reported.Add);

        await PressAsync(cut, Cell(0, 0).X, Cell(0, 0).Y);
        await PressAsync(cut, Cell(2, 3).X, Cell(2, 3).Y, shift: true);

        Assert.Equal(2, reported.Count);
        Assert.Equal(1, reported[0].CellCount);
        Assert.Equal(12, reported[1].CellCount);
        Assert.Equal(new CellPosition(2, 3), reported[1].Focus);
    }

    [Fact] // Nothing is selected until something is pointed at, and nothing is painted either
    public void Nothing_is_selected_to_begin_with()
    {
        var cut = RenderGrid();

        Assert.Empty(cut.FindAll(".ex-range"));
        Assert.Empty(cut.FindAll(".ex-focus"));
    }

    [Fact] // A secondary button does not move a selection: what it should do is not specified yet
    public async Task A_secondary_click_leaves_the_selection_alone()
    {
        var cut = RenderGrid();
        await PressAsync(cut, Cell(1, 1).X, Cell(1, 1).Y);

        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            OffsetX = Cell(3, 3).X,
            OffsetY = Cell(3, 3).Y,
            Button = 2,
            Buttons = 2,
        });

        Assert.Equal([Rect(100, 20, 100, 20)], Rects(cut));
    }
}
