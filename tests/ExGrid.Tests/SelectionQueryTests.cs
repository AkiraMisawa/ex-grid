using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class SelectionQueryTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0008: one overlay per range — Ranges come normalized, in creation order
    public void Ranges_enumerate_normalized_rectangles_in_creation_order()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 5), Grid)
            .ExtendTo(new(2, 2), Grid)      // drawn upward: still arrives normalized
            .ToggleRange(new(8, 8), Grid);

        Assert.Equal([new SelectionRange(2, 2, 4, 4), new SelectionRange(8, 8, 1, 1)], selection.Ranges);
    }

    [Fact] // ADR-0014: the selected-cell count is the sum of rectangle areas and needs no data
    public void CellCount_needs_no_data()
    {
        var selection = GridSelection.Empty.SelectAll(new GridExtent(1_000_000, 50));

        Assert.Equal(50_000_000L, selection.CellCount);
    }

    [Fact] // ADR-0014: overlapping ranges double-count, as Excel's status bar does
    public void CellCount_sums_areas_so_overlaps_double_count()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)      // 9 cells
            .ToggleRange(new(4, 4), Grid)
            .ExtendTo(new(1, 1), Grid);     // 16 cells, 4 of them shared

        Assert.Equal(25L, selection.CellCount);
    }

    [Fact] // ADR-0005: the spans expose what the copy alignment check will compare
    public void Disjoint_ranges_expose_their_row_and_column_spans()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 2), Grid)
            .ExtendTo(new(2, 3), Grid)
            .ToggleRange(new(5, 2), Grid)
            .ExtendTo(new(6, 3), Grid);

        // Same column span, disjoint row spans — the aligned-copy shape.
        Assert.Equal(new ColumnRange(2, 2), selection.Ranges[0].ColumnSpan);
        Assert.Equal(new ColumnRange(2, 2), selection.Ranges[1].ColumnSpan);
        Assert.Equal(new RowRange(1, 2), selection.Ranges[0].RowSpan);
        Assert.Equal(new RowRange(5, 2), selection.Ranges[1].RowSpan);
    }

    [Fact] // ADR-0011: Contains answers across disjoint ranges
    public void Contains_answers_across_disjoint_ranges()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 2), Grid)
            .ExtendTo(new(2, 3), Grid)
            .ToggleRange(new(5, 2), Grid)
            .ExtendTo(new(6, 3), Grid);

        Assert.True(selection.Contains(new(1, 2)));
        Assert.True(selection.Contains(new(6, 3)));
        Assert.False(selection.Contains(new(4, 2)));
    }

    [Fact] // ADR-0003: memoisation needs value equality — identical gestures give equal selections
    public void Identical_gesture_sequences_produce_equal_selections()
    {
        GridSelection Build() => GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ExtendTo(new(3, 3), Grid)
            .ToggleRange(new(5, 5), Grid);

        Assert.Equal(Build(), Build());
        Assert.Equal(Build().GetHashCode(), Build().GetHashCode());
        Assert.NotEqual(Build(), Build().Extend(GridDirection.Down, Grid));
        Assert.Equal(GridSelection.Empty, GridSelection.Empty.Click(new(0, 0), Grid).ToggleRange(new(0, 0), Grid));
    }

    [Fact] // ADR-0011: the empty selection selects nothing and counts nothing
    public void Empty_contains_nothing()
    {
        Assert.Equal(0L, GridSelection.Empty.CellCount);
        Assert.False(GridSelection.Empty.Contains(new(0, 0)));
        Assert.Empty(GridSelection.Empty.Ranges);
    }
}
