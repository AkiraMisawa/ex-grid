using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A Consumer places the Selection and the Focus (ADR-0050, item 4): the pure half. The
/// named range becomes the one range, with Anchor and Focus on the named cell; a request
/// naming cells that are not there is refused, never clamped.
/// </summary>
public class PlacementTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0050 item 4: the range replaces every other, with Anchor and Focus on the named cell
    public void Place_selects_the_range_with_anchor_and_focus_on_the_cell()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid).ToggleRange(new(5, 5), Grid)
            .Place(new SelectionRange(10, 2, 3, 4), new CellPosition(10, 2), Grid);

        Assert.Equal([new SelectionRange(10, 2, 3, 4)], selection.Ranges);
        Assert.Equal(new CellPosition(10, 2), selection.Anchor);
        Assert.Equal(new CellPosition(10, 2), selection.Focus);
    }

    [Fact] // ADR-0050/0012: a placed range is cycled by Enter like any other
    public void A_placed_range_cycles_like_any_range()
    {
        var selection = GridSelection.Empty.Place(new SelectionRange(10, 2, 2, 1), new CellPosition(10, 2), Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);

        Assert.Equal(new CellPosition(11, 2), selection.Focus);
        Assert.Equal([new SelectionRange(10, 2, 2, 1)], selection.Ranges);
    }

    [Fact] // ADR-0050 item 4: a range reaching outside the grid is refused by name
    public void A_range_outside_the_grid_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GridSelection.Empty.Place(new SelectionRange(99, 0, 2, 1), new CellPosition(99, 0), Grid));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GridSelection.Empty.Place(new SelectionRange(0, 25, 1, 2), new CellPosition(0, 25), Grid));
    }

    [Fact] // ADR-0050/0012: the Focus is placed inside its range
    public void A_focus_outside_the_range_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GridSelection.Empty.Place(new SelectionRange(4, 0, 2, 1), new CellPosition(9, 0), Grid));
    }
}
