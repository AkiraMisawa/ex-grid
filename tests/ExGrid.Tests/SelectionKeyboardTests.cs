using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class SelectionKeyboardTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0012: arrows collapse the selection to one cell and move
    public void Arrow_collapses_the_selection_and_moves_from_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .ExtendTo(new(4, 4), Grid)
            .Move(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(5, 4, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 4), selection.Anchor);
        Assert.Equal(new CellPosition(5, 4), selection.Focus);
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

    [Fact] // ADR-0012: shift+arrow grows the range with the Anchor fixed, and shrinking back through it flips
    public void Shift_arrow_grows_shrinks_and_flips_around_the_anchor()
    {
        var grown = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .Extend(GridDirection.Down, Grid);
        Assert.Equal([new SelectionRange(2, 2, 2, 1)], grown.Ranges);

        var shrunk = grown.Extend(GridDirection.Up, Grid);
        Assert.Equal([new SelectionRange(2, 2, 1, 1)], shrunk.Ranges);

        var flipped = shrunk.Extend(GridDirection.Up, Grid);
        Assert.Equal([new SelectionRange(1, 2, 2, 1)], flipped.Ranges);
        Assert.Equal(new CellPosition(2, 2), flipped.Anchor);
        Assert.Equal(new CellPosition(1, 2), flipped.Focus);
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

    [Fact] // ADR-0012: shift+ctrl+arrow extends the range to the edge
    public void Shift_ctrl_arrow_extends_to_the_edge()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 3), Grid)
            .ExtendToEdge(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(5, 3, 95, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 3), selection.Anchor);
        Assert.Equal(new CellPosition(99, 3), selection.Focus);
    }

    [Fact] // ADR-0012: Ctrl+Shift+Down from the first row is effectively a whole-column selection
    public void Ctrl_shift_down_from_the_first_row_selects_the_whole_column()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 3), Grid)
            .ExtendToEdge(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(0, 3, 100, 1)], selection.Ranges);
    }

    [Fact] // ADR-0012: Ctrl+Space expands the last range to every row, keeping its column span
    public void Ctrl_space_selects_whole_columns()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 3), Grid)
            .ExtendTo(new(7, 4), Grid)
            .SelectWholeColumns(Grid);

        Assert.Equal([new SelectionRange(0, 3, 100, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 3), selection.Anchor);
        Assert.Equal(new CellPosition(7, 4), selection.Focus);
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

    [Fact] // ADR-0012: Ctrl+Space from a detached Anchor starts a whole-column range at the deselected cell
    public void Ctrl_space_after_a_toggle_off_starts_a_column_range_at_the_anchor()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .ExtendTo(new(4, 4), Grid)
            .ToggleRange(new(3, 3), Grid)   // detaches on (3,3); four fragments remain
            .SelectWholeColumns(Grid);

        Assert.Equal(5, selection.Ranges.Count);
        Assert.Equal(new SelectionRange(0, 3, 100, 1), selection.Ranges[^1]); // the Anchor's column, not a fragment's
        Assert.Equal(new CellPosition(3, 3), selection.Anchor);
    }

    [Fact] // ADR-0012: Shift+Space from a detached Anchor starts a whole-row range at the deselected cell
    public void Shift_space_after_a_toggle_off_starts_a_row_range_at_the_anchor()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)
            .ToggleRange(new(1, 2), Grid)
            .SelectWholeRows(Grid);

        Assert.Equal(new SelectionRange(1, 0, 1, 26), selection.Ranges[^1]);
        Assert.Equal(new CellPosition(1, 2), selection.Anchor);
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
}
