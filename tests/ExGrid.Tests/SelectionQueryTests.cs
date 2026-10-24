using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What of the selection's queries is C#'s alone: the spans the copy rules compare, and a list that
/// cannot be cast back to a mutable one. The rest is in the shared cases
/// (tests/cases/selection/queries.json, ADR-0350).
/// </summary>
public class SelectionQueryTests
{
    private static readonly GridExtent Grid = new(100, 26);

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

    [Fact] // ADR-0008: Ranges cannot be cast back to a mutable array — the selection stays immutable
    public void Ranges_is_not_a_castable_mutable_array()
    {
        var extended = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid);
        Assert.IsNotType<SelectionRange[]>(extended.Ranges);

        var toggled = extended.ToggleRange(new(5, 5), Grid).ToggleRange(new(1, 1), Grid);
        Assert.IsNotType<SelectionRange[]>(toggled.Ranges);
        Assert.IsNotType<List<SelectionRange>>(toggled.Ranges);
    }
}
