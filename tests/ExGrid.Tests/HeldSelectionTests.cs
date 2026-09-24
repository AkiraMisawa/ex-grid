using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class HeldSelectionTests
{
    private static readonly GridExtent Extent = new(10, 5);

    private static HeldSelection SelectionAt(int version)
        => HeldSelection.Empty(version).With(GridSelection.Empty.Click(new CellPosition(2, 3), Extent));

    [Fact] // ADR-0011: the same order keeps the selection
    public void The_same_version_keeps_the_selection()
    {
        var held = SelectionAt(version: 7);

        Assert.Equal(held, held.Under(7));
        Assert.False(held.Under(7).Selection.IsEmpty);
    }

    [Fact] // ADR-0011: selection is dropped when the sort order changes — dropped, never remapped
    public void A_changed_version_drops_the_selection()
    {
        var held = SelectionAt(version: 7);

        var reconciled = held.Under(8);

        Assert.True(reconciled.Selection.IsEmpty);
        Assert.Equal(8, reconciled.RowSequenceVersion);
    }

    [Fact] // ADR-0011: versions identify orders, not points in time — any difference drops
    public void Any_version_difference_drops_regardless_of_direction()
    {
        var held = SelectionAt(version: 7);

        Assert.True(held.Under(6).Selection.IsEmpty);
    }

    [Fact] // ADR-0011: a gesture replaces the selection under the same version
    public void With_keeps_the_version()
    {
        var held = HeldSelection.Empty(3).With(GridSelection.Empty.Click(new CellPosition(0, 0), Extent));

        Assert.Equal(3, held.RowSequenceVersion);
        Assert.False(held.Selection.IsEmpty);
    }
}
