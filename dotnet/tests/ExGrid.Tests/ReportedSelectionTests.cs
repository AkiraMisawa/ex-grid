using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0201's note of 2026-10-27: a Selection the Script Core reported, read back by an adapter over it. It is
/// what was reported, and a reading whose Focus range does not hold the Focus is refused rather than read as some
/// other Selection.
/// </summary>
public class ReportedSelectionTests
{
    [Fact] // ADR-0201: the ranges keep their order, and the named range holds the Focus
    public void ADR0201_a_reported_selection_is_what_was_reported()
    {
        var selection = GridSelection.Reported([new SelectionRange(0, 0, 3, 2), new SelectionRange(5, 1, 1, 1)], new CellPosition(1, 1), 0);

        Assert.Equal([new SelectionRange(0, 0, 3, 2), new SelectionRange(5, 1, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(1, 1), selection.Focus);
        Assert.Equal(new SelectionRange(0, 0, 3, 2), selection.FocusRange);
    }

    [Fact] // ADR-0201: no range is no Selection
    public void ADR0201_no_range_is_the_empty_selection()
        => Assert.Same(GridSelection.Empty, GridSelection.Reported([], new CellPosition(0, 0), 0));

    [Theory] // ADR-0052, ADR-0201: a Focus range index that does not name the range holding the Focus is refused
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    public void ADR0201_a_focus_outside_the_named_range_is_refused(int focusRangeIndex)
        => Assert.Throws<ArgumentException>(() => GridSelection.Reported(
            [new SelectionRange(0, 0, 3, 2), new SelectionRange(5, 1, 1, 1)], new CellPosition(1, 1), focusRangeIndex));

    [Fact] // ADR-0011, ADR-0201: a range that covers no cell, which only default makes, is refused wherever it stands
    public void ADR0201_a_range_of_no_cell_is_refused()
        => Assert.Throws<ArgumentException>(() => GridSelection.Reported(
            [default, new SelectionRange(0, 0, 1, 1)], new CellPosition(0, 0), 1));
}
