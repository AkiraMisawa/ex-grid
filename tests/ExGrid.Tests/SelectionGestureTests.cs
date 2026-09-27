using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class SelectionGestureTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0012/0052: click — the Focus is that cell, and so is the Extent; the selection collapses to it
    public void Click_collapses_to_one_cell_with_focus_and_extent_on_it()
    {
        var selection = GridSelection.Empty.Click(new(5, 3), Grid);

        Assert.Equal([new SelectionRange(5, 3, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 3), selection.Focus);
        Assert.Equal(new CellPosition(5, 3), selection.Extent);
    }

    [Fact] // ADR-0052: shift+click — the Focus stays; the Extent moves to the clicked cell and the range is redrawn
    public void Shift_click_keeps_the_focus_and_moves_the_extent()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ExtendTo(new(3, 4), Grid);

        Assert.Equal([new SelectionRange(1, 1, 3, 4)], selection.Ranges);
        Assert.Equal(new CellPosition(1, 1), selection.Focus);
        Assert.Equal(new CellPosition(3, 4), selection.Extent);
    }

    [Fact] // ADR-0052: extending past the far side flips the range around the Focus
    public void Shift_click_on_the_far_side_flips_the_range_around_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 5), Grid)
            .ExtendTo(new(7, 7), Grid)
            .ExtendTo(new(3, 3), Grid);

        Assert.Equal([new SelectionRange(3, 3, 3, 3)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 5), selection.Focus);
        Assert.Equal(new CellPosition(3, 3), selection.Extent);
    }

    [Fact] // ADR-0052 case 2: a drag's Focus is where the button went down, whichever way it moves — D5 → B2 leaves D5
    public void A_drag_up_and_left_keeps_the_focus_where_it_began()
    {
        // Mouse down is Click and each move is ExtendTo (the holder's mapping).
        var selection = GridSelection.Empty
            .Click(new(4, 3), Grid)        // D5
            .ExtendTo(new(3, 2), Grid)
            .ExtendTo(new(1, 1), Grid);    // B2

        Assert.Equal([new SelectionRange(1, 1, 4, 3)], selection.Ranges); // B2:D5
        Assert.Equal(new CellPosition(4, 3), selection.Focus);           // D5
        Assert.Equal(new CellPosition(1, 1), selection.Extent);
    }

    [Fact] // ADR-0012/0052: ctrl+click adds a new range holding the Focus
    public void Ctrl_click_adds_a_range_holding_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid);

        Assert.Equal([new SelectionRange(1, 1, 1, 1), new SelectionRange(5, 5, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 5), selection.Focus);
        Assert.Equal(new SelectionRange(5, 5, 1, 1), selection.FocusRange);
    }

    [Fact] // ADR-0052 case 5: Ctrl+click D5 onto A1:B2, then Shift+Down gives A1:B2,D5:D6
    public void Shift_arrow_after_ctrl_click_grows_the_range_holding_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(1, 1), Grid)      // A1:B2
            .ToggleRange(new(4, 3), Grid)   // D5
            .Extend(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(0, 0, 2, 2), new SelectionRange(4, 3, 2, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(4, 3), selection.Focus);
    }

    [Fact] // ADR-0011/0052: Ctrl+A is one rectangle covering every row and every visible column, and the Focus does not move
    public void Select_all_is_one_rectangle_over_the_whole_grid()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 5), Grid)
            .SelectAll(Grid);

        Assert.Equal([new SelectionRange(0, 0, 100, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 5), selection.Focus);
    }

    [Fact] // ADR-0011: Ctrl+A from an empty selection puts the Focus at the origin
    public void Select_all_from_empty_puts_the_focus_at_the_origin()
    {
        var selection = GridSelection.Empty.SelectAll(Grid);

        Assert.Equal([new SelectionRange(0, 0, 100, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
    }

    [Fact] // ADR-0012/0052: ctrl+click on a selected cell takes it out of every containing range, and the Focus stays inside
    public void Toggle_off_subtracts_the_cell_from_every_containing_range()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)      // range 1: (0,0)-(2,2)
            .ToggleRange(new(4, 4), Grid)
            .ExtendTo(new(1, 1), Grid)      // range 2: (1,1)-(4,4), overlapping range 1; Focus (4,4)
            .ToggleRange(new(1, 1), Grid);  // take out a cell inside both

        Assert.False(selection.Contains(new(1, 1)));
        Assert.Equal(
        [
            new SelectionRange(0, 0, 1, 3), // range 1 minus (1,1)
            new SelectionRange(2, 0, 1, 3),
            new SelectionRange(1, 0, 1, 1),
            new SelectionRange(1, 2, 1, 1),
            new SelectionRange(2, 1, 3, 4), // range 2 minus its top-left corner
            new SelectionRange(1, 2, 1, 3),
        ], selection.Ranges);
        Assert.Equal(new CellPosition(4, 4), selection.Focus); // not the removed cell: it stays
        Assert.Equal(new SelectionRange(2, 1, 3, 4), selection.FocusRange); // the fragment of its own range
    }

    [Fact] // ADR-0052 case 6: B2 out of A1:C3 (A1 active) keeps A1 active, and Shift+Down extends A1:C1 to A1:C2
    public void Taking_out_a_cell_keeps_the_focus_and_its_fragment_extends()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)      // A1:C3, A1 active
            .ToggleRange(new(1, 1), Grid);  // B2

        Assert.Equal(4, selection.Ranges.Count);
        Assert.False(selection.Contains(new(1, 1)));
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
        Assert.Equal(new SelectionRange(0, 0, 1, 3), selection.FocusRange); // A1:C1

        var extended = selection.Extend(GridDirection.Down, Grid);

        Assert.Contains(new SelectionRange(0, 0, 2, 3), extended.Ranges); // A1:C2
        Assert.Equal(4, extended.Ranges.Count);
        Assert.Equal(new CellPosition(0, 0), extended.Focus);
    }

    [Fact] // ADR-0052 case 6: A1 out of A1:C3 moves the Focus to B1, the next cell in Tab order, and Shift+Down extends B1:C1
    public void Taking_out_the_focus_cell_moves_the_focus_to_the_next_cell_in_tab_order()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)      // A1:C3, A1 active
            .ToggleRange(new(0, 0), Grid);  // A1

        Assert.Equal([new SelectionRange(1, 0, 2, 3), new SelectionRange(0, 1, 1, 2)], selection.Ranges); // A2:C3, B1:C1
        Assert.Equal(new CellPosition(0, 1), selection.Focus); // B1
        Assert.Equal(new SelectionRange(0, 1, 1, 2), selection.FocusRange);

        var extended = selection.Extend(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(1, 0, 2, 3), new SelectionRange(0, 1, 2, 2)], extended.Ranges); // B1:C2
        Assert.Equal(new CellPosition(0, 1), extended.Focus);
    }

    [Fact] // ADR-0052: taking out the Focus's cell when it is the last in Tab order of its range goes on into the next range
    public void Taking_out_the_last_cell_of_the_focus_range_goes_on_to_the_next_range()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(1, 1), Grid)      // A1:B2
            .ToggleRange(new(5, 5), Grid)   // F6, then taken out again below
            .ToggleRange(new(5, 5), Grid);

        Assert.Equal([new SelectionRange(0, 0, 2, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus); // wrapped to the first range's first cell
    }

    [Fact] // ADR-0052 case 6: the only selected cell cannot be taken out
    public void The_only_selected_cell_cannot_be_taken_out()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid);

        var toggled = selection.ToggleRange(new(1, 1), Grid);

        Assert.Equal(selection, toggled);
        Assert.Equal([new SelectionRange(1, 1, 1, 1)], toggled.Ranges);
        Assert.Equal(new CellPosition(1, 1), toggled.Focus);
    }

    [Fact] // ADR-0012: an empty selection has no Focus or Extent — refuse rather than answer (0,0)
    public void Empty_has_no_focus_or_extent()
    {
        Assert.Throws<InvalidOperationException>(() => GridSelection.Empty.Focus);
        Assert.Throws<InvalidOperationException>(() => GridSelection.Empty.Extent);
        Assert.Throws<InvalidOperationException>(() => GridSelection.Empty.FocusRange);
    }

    [Fact] // ADR-0012: shift+click with no selection behaves as a plain click — there is no Focus
    public void Shift_click_from_empty_behaves_as_a_click()
    {
        var selection = GridSelection.Empty.ExtendTo(new(2, 3), Grid);

        Assert.Equal([new SelectionRange(2, 3, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(2, 3), selection.Focus);
    }

    [Fact] // ADR-0012: ctrl+click with no selection selects the cell
    public void Ctrl_click_from_empty_selects_the_cell()
    {
        var selection = GridSelection.Empty.ToggleRange(new(2, 2), Grid);

        Assert.Equal([new SelectionRange(2, 2, 1, 1)], selection.Ranges);
    }
}
