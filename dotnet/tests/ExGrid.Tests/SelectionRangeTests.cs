using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What of a range is C#'s alone: the spans the copy rules compare. The rest is in the shared cases
/// (tests/cases/selection/range.json, ADR-0350).
/// </summary>
public class SelectionRangeTests
{
    [Fact] // ADR-0005: the spans are the shape facts the copy alignment check compares
    public void RowSpan_and_ColumnSpan_expose_the_shape()
    {
        var range = new SelectionRange(2, 3, 4, 5);

        Assert.Equal(new RowRange(2, 4), range.RowSpan);
        Assert.Equal(new ColumnRange(3, 5), range.ColumnSpan);
    }
}
