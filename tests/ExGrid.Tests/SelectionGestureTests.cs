using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class SelectionGestureTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0012: click — Anchor = Focus = that cell; the selection collapses to one cell
    public void Click_collapses_to_one_cell_with_anchor_and_focus_on_it()
    {
        var selection = GridSelection.Empty.Click(new(5, 3), Grid);

        Assert.Equal([new SelectionRange(5, 3, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 3), selection.Anchor);
        Assert.Equal(new CellPosition(5, 3), selection.Focus);
    }

    [Fact] // ADR-0012: shift+click — Anchor stays; Focus moves and the range is redrawn
    public void Shift_click_keeps_the_anchor_and_redraws_the_range()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ExtendTo(new(3, 4), Grid);

        Assert.Equal([new SelectionRange(1, 1, 3, 4)], selection.Ranges);
        Assert.Equal(new CellPosition(1, 1), selection.Anchor);
        Assert.Equal(new CellPosition(3, 4), selection.Focus);
    }

    [Fact] // ADR-0012: extending past the far side flips the range around the Anchor
    public void Shift_click_on_the_far_side_flips_the_range_around_the_anchor()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 5), Grid)
            .ExtendTo(new(7, 7), Grid)
            .ExtendTo(new(3, 3), Grid);

        Assert.Equal([new SelectionRange(3, 3, 3, 3)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 5), selection.Anchor);
    }

    [Fact] // ADR-0012: ctrl+click adds a new range and moves Anchor and Focus into it
    public void Ctrl_click_adds_a_range_and_moves_anchor_and_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid);

        Assert.Equal([new SelectionRange(1, 1, 1, 1), new SelectionRange(5, 5, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 5), selection.Anchor);
        Assert.Equal(new CellPosition(5, 5), selection.Focus);
    }

    [Fact] // ADR-0012: the most recently created range is the one that grows
    public void Shift_arrow_after_ctrl_click_grows_only_the_newest_range()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid)
            .Extend(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(1, 1, 1, 1), new SelectionRange(5, 5, 2, 1)], selection.Ranges);
    }

    [Fact] // ADR-0011: Ctrl+A is one rectangle covering every row and every visible column
    public void Select_all_is_one_rectangle_over_the_whole_grid()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 5), Grid)
            .SelectAll(Grid);

        Assert.Equal([new SelectionRange(0, 0, 100, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 5), selection.Anchor); // Anchor and Focus stay put
        Assert.Equal(new CellPosition(5, 5), selection.Focus);
    }

    [Fact] // ADR-0011: Ctrl+A from an empty selection anchors at the origin
    public void Select_all_from_empty_anchors_at_the_origin()
    {
        var selection = GridSelection.Empty.SelectAll(Grid);

        Assert.Equal([new SelectionRange(0, 0, 100, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Anchor);
    }

    [Fact] // ADR-0012: ctrl+click on a selected cell toggles it off in every range containing it
    public void Toggle_off_subtracts_the_cell_from_every_containing_range()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)      // range 1: (0,0)-(2,2)
            .ToggleRange(new(4, 4), Grid)
            .ExtendTo(new(1, 1), Grid)      // range 2: (1,1)-(4,4), overlapping range 1
            .ToggleRange(new(1, 1), Grid);  // toggle a cell inside both

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
        Assert.Equal(new CellPosition(1, 1), selection.Anchor); // detached on the deselected cell
        Assert.Equal(new CellPosition(1, 1), selection.Focus);
    }

    [Fact] // ADR-0012: shift+arrow from a detached Anchor starts a new range
    public void Shift_arrow_after_a_toggle_off_starts_a_new_range()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 2), Grid)
            .ExtendTo(new(4, 4), Grid)
            .ToggleRange(new(3, 3), Grid)   // Anchor and Focus detach on (3,3)
            .Extend(GridDirection.Right, Grid);

        Assert.Equal(5, selection.Ranges.Count); // four fragments plus the new range
        Assert.Equal(new SelectionRange(3, 3, 1, 2), selection.Ranges[^1]);
        Assert.Equal(new CellPosition(3, 3), selection.Anchor);
        Assert.Equal(new CellPosition(3, 4), selection.Focus);
    }

    [Fact] // ADR-0012: toggling off the last cell leaves the empty selection
    public void Toggling_off_the_last_cell_yields_empty()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ToggleRange(new(1, 1), Grid);

        Assert.True(selection.IsEmpty);
    }

    [Fact] // ADR-0012: an empty selection has no Anchor or Focus — refuse rather than answer (0,0)
    public void Empty_has_no_anchor_or_focus()
    {
        Assert.Throws<InvalidOperationException>(() => GridSelection.Empty.Anchor);
        Assert.Throws<InvalidOperationException>(() => GridSelection.Empty.Focus);
    }

    [Fact] // ADR-0012: shift+click with no selection behaves as a plain click — there is no Anchor
    public void Shift_click_from_empty_behaves_as_a_click()
    {
        var selection = GridSelection.Empty.ExtendTo(new(2, 3), Grid);

        Assert.Equal([new SelectionRange(2, 3, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(2, 3), selection.Anchor);
    }

    [Fact] // ADR-0012: ctrl+click with no selection selects the cell
    public void Ctrl_click_from_empty_selects_the_cell()
    {
        var selection = GridSelection.Empty.ToggleRange(new(2, 2), Grid);

        Assert.Equal([new SelectionRange(2, 2, 1, 1)], selection.Ranges);
    }
}
