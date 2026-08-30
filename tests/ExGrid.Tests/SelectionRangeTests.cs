using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class SelectionRangeTests
{
    [Fact] // ADR-0012: FromCorners normalizes any pair of opposite corners
    public void FromCorners_normalizes_any_corner_pair()
    {
        var expected = new SelectionRange(1, 2, 3, 3);

        Assert.Equal(expected, SelectionRange.FromCorners(new(1, 2), new(3, 4)));
        Assert.Equal(expected, SelectionRange.FromCorners(new(3, 4), new(1, 2)));
        Assert.Equal(expected, SelectionRange.FromCorners(new(1, 4), new(3, 2)));
        Assert.Equal(expected, SelectionRange.FromCorners(new(3, 2), new(1, 4)));
    }

    [Fact] // ADR-0012: the same cell twice is a 1×1 range
    public void FromCorners_of_one_cell_is_a_single_cell_range()
    {
        Assert.Equal(new SelectionRange(5, 7, 1, 1), SelectionRange.FromCorners(new(5, 7), new(5, 7)));
    }

    [Fact] // ADR-0011: a range covers at least one cell; an empty rectangle is refused, not normalized away
    public void An_empty_or_negative_range_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectionRange(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectionRange(0, 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectionRange(-1, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectionRange(0, -1, 1, 1));
    }

    [Fact] // ADR-0011: Contains is inclusive of all four bounds
    public void Contains_is_inclusive_of_its_bounds()
    {
        var range = new SelectionRange(1, 1, 2, 2);

        Assert.True(range.Contains(new(1, 1)));
        Assert.True(range.Contains(new(2, 2)));
        Assert.False(range.Contains(new(0, 1)));
        Assert.False(range.Contains(new(3, 2)));
        Assert.False(range.Contains(new(1, 3)));
        Assert.False(range.Contains(new(2, 0)));
    }

    [Fact] // ADR-0014: the count is the area, needs no data, and does not overflow int
    public void CellCount_is_the_area_and_is_long()
    {
        Assert.Equal(5_000_000_000L, new SelectionRange(0, 0, 1_000_000, 5_000).CellCount);
    }

    [Fact] // ADR-0005: the spans are the shape facts the copy alignment check compares
    public void RowSpan_and_ColumnSpan_expose_the_shape()
    {
        var range = new SelectionRange(2, 3, 4, 5);

        Assert.Equal(new RowRange(2, 4), range.RowSpan);
        Assert.Equal(new ColumnRange(3, 5), range.ColumnSpan);
    }

    [Fact] // ADR-0012: toggling off an interior cell splits the rectangle into four
    public void Subtracting_an_interior_cell_splits_into_four()
    {
        var pieces = new SelectionRange(0, 0, 3, 3).Subtract(new(1, 1));

        Assert.Equal(
        [
            new SelectionRange(0, 0, 1, 3), // band above
            new SelectionRange(2, 0, 1, 3), // band below
            new SelectionRange(1, 0, 1, 1), // left of the cell
            new SelectionRange(1, 2, 1, 1), // right of the cell
        ], pieces);
    }

    [Fact] // ADR-0012: an edge cell leaves three pieces
    public void Subtracting_an_edge_cell_splits_into_three()
    {
        var pieces = new SelectionRange(0, 0, 3, 3).Subtract(new(0, 1));

        Assert.Equal(
        [
            new SelectionRange(1, 0, 2, 3),
            new SelectionRange(0, 0, 1, 1),
            new SelectionRange(0, 2, 1, 1),
        ], pieces);
    }

    [Fact] // ADR-0012: a corner cell leaves two pieces
    public void Subtracting_a_corner_cell_splits_into_two()
    {
        var pieces = new SelectionRange(0, 0, 2, 2).Subtract(new(0, 0));

        Assert.Equal(
        [
            new SelectionRange(1, 0, 1, 2),
            new SelectionRange(0, 1, 1, 1),
        ], pieces);
    }

    [Fact] // ADR-0012: subtracting the only cell of a 1×1 range yields nothing
    public void Subtracting_the_only_cell_yields_nothing()
    {
        Assert.Empty(new SelectionRange(4, 4, 1, 1).Subtract(new(4, 4)));
    }

    [Fact] // ADR-0012: a cell outside the range leaves it unchanged
    public void Subtracting_an_outside_cell_leaves_the_range_unchanged()
    {
        var range = new SelectionRange(0, 0, 2, 2);

        Assert.Equal([range], range.Subtract(new(5, 5)));
    }
}
