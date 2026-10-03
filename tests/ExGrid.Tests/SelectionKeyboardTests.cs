using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class SelectionKeyboardTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0012/0052: arrows collapse the selection to one cell and move from the Focus, not the Extent
    public void Arrow_collapses_the_selection_and_moves_from_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .ExtendTo(new(4, 4), Grid)
            .Move(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(3, 2, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(3, 2), selection.Focus);
        Assert.Equal(new CellPosition(3, 2), selection.Extent);
    }

    [Fact] // ADR-0012: an arrow at the grid edge clamps — the Focus stays put
    public void Arrow_at_each_edge_clamps()
    {
        var small = new GridExtent(3, 3);
        var topLeft = GridSelection.Empty.Click(new(0, 0), small);
        var bottomRight = GridSelection.Empty.Click(new(2, 2), small);

        Assert.Equal(new CellPosition(0, 0), topLeft.Move(GridDirection.Up, small).Focus);
        Assert.Equal(new CellPosition(0, 0), topLeft.Move(GridDirection.Left, small).Focus);
        Assert.Equal(new CellPosition(2, 2), bottomRight.Move(GridDirection.Down, small).Focus);
        Assert.Equal(new CellPosition(2, 2), bottomRight.Move(GridDirection.Right, small).Focus);
    }

    [Fact] // ADR-0052: shift+arrow moves the Extent with the Focus fixed, and shrinking back through the Focus flips
    public void Shift_arrow_grows_shrinks_and_flips_around_the_focus()
    {
        var grown = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .Extend(GridDirection.Down, Grid);
        Assert.Equal([new SelectionRange(2, 2, 2, 1)], grown.Ranges);

        var shrunk = grown.Extend(GridDirection.Up, Grid);
        Assert.Equal([new SelectionRange(2, 2, 1, 1)], shrunk.Ranges);

        var flipped = shrunk.Extend(GridDirection.Up, Grid);
        Assert.Equal([new SelectionRange(1, 2, 2, 1)], flipped.Ranges);
        Assert.Equal(new CellPosition(2, 2), flipped.Focus);
        Assert.Equal(new CellPosition(1, 2), flipped.Extent);
    }

    [Fact] // ADR-0011: Ctrl+Down jumps to the last row — not a block edge, which needs data the grid does not have
    public void Ctrl_arrow_jumps_to_the_grid_edge()
    {
        var start = GridSelection.Empty.Click(new(5, 3), Grid);

        Assert.Equal(new CellPosition(99, 3), start.MoveToEdge(GridDirection.Down, Grid).Focus);
        Assert.Equal(new CellPosition(0, 3), start.MoveToEdge(GridDirection.Up, Grid).Focus);
        Assert.Equal(new CellPosition(5, 25), start.MoveToEdge(GridDirection.Right, Grid).Focus);
        Assert.Equal(new CellPosition(5, 0), start.MoveToEdge(GridDirection.Left, Grid).Focus);
        Assert.Equal([new SelectionRange(99, 3, 1, 1)], start.MoveToEdge(GridDirection.Down, Grid).Ranges);
    }

    [Fact] // ADR-0012/0052: shift+ctrl+arrow runs the Extent to the edge; the Focus stays
    public void Shift_ctrl_arrow_extends_to_the_edge()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 3), Grid)
            .ExtendToEdge(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(5, 3, 95, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 3), selection.Focus);
        Assert.Equal(new CellPosition(99, 3), selection.Extent);
    }

    [Fact] // ADR-0012: Ctrl+Shift+Down from the first row is effectively a whole-column selection
    public void Ctrl_shift_down_from_the_first_row_selects_the_whole_column()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 3), Grid)
            .ExtendToEdge(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(0, 3, 100, 1)], selection.Ranges);
    }

    [Fact] // ADR-0012/0052: Ctrl+Space expands the range holding the Focus to every row, keeping its column span; the Focus stays
    public void Ctrl_space_selects_whole_columns()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 3), Grid)
            .ExtendTo(new(7, 4), Grid)
            .SelectWholeColumns(Grid);

        Assert.Equal([new SelectionRange(0, 3, 100, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 3), selection.Focus);
    }

    [Fact] // ADR-0012: Shift+Space expands the last range to every visible column, keeping its row span
    public void Shift_space_selects_whole_rows()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 3), Grid)
            .ExtendTo(new(7, 4), Grid)
            .SelectWholeRows(Grid);

        Assert.Equal([new SelectionRange(5, 0, 3, 26)], selection.Ranges);
    }

    [Fact] // ADR-0052: after a cell is taken out, Ctrl+Space grows the fragment holding the Focus — there is no detached state
    public void Ctrl_space_after_a_toggle_off_grows_the_fragment_holding_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .ExtendTo(new(4, 4), Grid)
            .ToggleRange(new(3, 3), Grid)   // four fragments, bottom to top; the Focus on (2,2), the first remaining cell by rows
            .SelectWholeColumns(Grid);

        Assert.Equal(4, selection.Ranges.Count);
        Assert.Equal(new SelectionRange(0, 2, 100, 3), selection.Ranges[3]); // the top band, (2,2)-(2,4), listed last, made whole columns
        Assert.Equal(new CellPosition(2, 2), selection.Focus);
    }

    [Fact] // ADR-0052 case 9: Ctrl+Space and Shift+Space act on the range holding the Focus, which Enter moved — B3 in B2:D4 gives B:D and 2:4
    public void The_space_pair_selects_the_focus_range_and_the_focus_does_not_move()
    {
        var b2d4 = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ExtendTo(new(3, 3), Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid); // Enter: B3

        var columns = b2d4.SelectWholeColumns(Grid);
        Assert.Equal([new SelectionRange(0, 1, 100, 3)], columns.Ranges);
        Assert.Equal(new CellPosition(2, 1), columns.Focus);

        var rows = b2d4.SelectWholeRows(Grid);
        Assert.Equal([new SelectionRange(1, 0, 3, 26)], rows.Ranges);
        Assert.Equal(new CellPosition(2, 1), rows.Focus);
    }

    [Fact] // ADR-0012: keyboard needs a Focus to start from — on an empty selection it is a no-op
    public void Keyboard_on_an_empty_selection_is_a_no_op()
    {
        Assert.True(GridSelection.Empty.Move(GridDirection.Down, Grid).IsEmpty);
        Assert.True(GridSelection.Empty.Extend(GridDirection.Down, Grid).IsEmpty);
        Assert.True(GridSelection.Empty.MoveToEdge(GridDirection.Down, Grid).IsEmpty);
        Assert.True(GridSelection.Empty.ExtendToEdge(GridDirection.Down, Grid).IsEmpty);
        Assert.True(GridSelection.Empty.SelectWholeColumns(Grid).IsEmpty);
        Assert.True(GridSelection.Empty.SelectWholeRows(Grid).IsEmpty);
        Assert.True(GridSelection.Empty.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid).IsEmpty);
    }

    [Fact] // ADR-0012/0052: PageDown collapses and moves the Focus — not the Extent — by the rows the caller passed
    public void Move_by_viewport_steps_the_focus_by_the_given_rows()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .ExtendTo(new(4, 4), Grid)
            .MoveByViewport(20, Grid);

        Assert.Equal([new SelectionRange(22, 2, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(22, 2), selection.Focus);

        Assert.Equal(new CellPosition(2, 2), selection.MoveByViewport(-20, Grid).Focus);
    }

    [Fact] // ADR-0012: at the first and last row the step clamps — the Focus stays inside
    public void Move_by_viewport_clamps_at_both_ends()
    {
        var nearTop = GridSelection.Empty.Click(new(3, 5), Grid);
        var nearBottom = GridSelection.Empty.Click(new(95, 5), Grid);

        Assert.Equal(new CellPosition(0, 5), nearTop.MoveByViewport(-20, Grid).Focus);
        Assert.Equal(new CellPosition(99, 5), nearBottom.MoveByViewport(20, Grid).Focus);
    }

    [Fact] // ADR-0052 case 11: Shift+PageDown moves the Extent a Viewport of rows and the Focus stays
    public void Extend_by_viewport_moves_the_extent_and_keeps_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(10, 3), Grid)
            .ExtendByViewport(20, Grid);

        Assert.Equal([new SelectionRange(10, 3, 21, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(10, 3), selection.Focus);
        Assert.Equal(new CellPosition(30, 3), selection.Extent);

        var back = selection.ExtendByViewport(-20, Grid);
        Assert.Equal([new SelectionRange(10, 3, 1, 1)], back.Ranges);
    }

    [Fact] // ADR-0011: a grid with no rows (or no columns) has nothing to select
    public void A_degenerate_extent_yields_empty_from_every_transition()
    {
        var noRows = new GridExtent(0, 5);
        var selection = GridSelection.Empty.Click(new(5, 3), Grid);

        Assert.True(GridSelection.Empty.Click(new(0, 0), noRows).IsEmpty);
        Assert.True(selection.Move(GridDirection.Down, noRows).IsEmpty);
        Assert.True(selection.SelectAll(noRows).IsEmpty);
    }

    [Fact] // ADR-0011: a selection that no longer fits the grid is a missed drop — refuse, do not mis-map
    public void A_state_outside_the_extent_is_refused()
    {
        var selection = GridSelection.Empty.Click(new(50, 3), Grid);
        var shrunk = new GridExtent(10, 26);

        Assert.Throws<InvalidOperationException>(() => selection.Move(GridDirection.Down, shrunk));
        Assert.Throws<InvalidOperationException>(() => selection.Extend(GridDirection.Down, shrunk));
        Assert.Throws<InvalidOperationException>(() => selection.CycleFocus(CycleOrder.ColumnMajor, backward: false, shrunk));
    }

    [Fact] // ADR-0011: a cell outside the grid is refused
    public void A_cell_outside_the_extent_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridSelection.Empty.Click(new(100, 0), Grid));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridSelection.Empty.Click(new(0, 26), Grid));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridSelection.Empty.Click(new(-1, 0), Grid));
    }


    [Fact] // ADR-0012/0015/0052: the page-context SelectAll names a region — the Focus stays
    public void Select_all_in_a_context_keeps_the_focus()
    {
        var selection = GridSelection.Empty.Click(new(52, 3), Grid);

        var paged = selection.SelectAll(Grid, 50, 25);

        Assert.Equal([new SelectionRange(50, 0, 25, 26)], paged.Ranges);
        Assert.Equal(new CellPosition(52, 3), paged.Focus);
    }

    [Fact] // ADR-0015: from Empty — or from another page — the Focus lands on the context's first cell
    public void Select_all_in_a_context_relocates_a_foreign_focus()
    {
        var fromEmpty = GridSelection.Empty.SelectAll(Grid, 50, 25);
        Assert.Equal(new CellPosition(50, 0), fromEmpty.Focus);

        var elsewhere = GridSelection.Empty.Click(new(10, 3), Grid).SelectAll(Grid, 50, 25);
        Assert.Equal([new SelectionRange(50, 0, 25, 26)], elsewhere.Ranges);
        Assert.Equal(new CellPosition(50, 0), elsewhere.Focus);
    }
}
