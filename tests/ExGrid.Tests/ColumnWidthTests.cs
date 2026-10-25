using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What of a column's width is C#'s alone: the zero state and printing. Its answers are shared cases
/// (<c>tests/cases/columns/column-width.json</c>, <c>column-width-spec.json</c> and <c>auto-width.json</c>).
/// </summary>
public class ColumnWidthTests
{
    [Fact] // ADR-0016: the zero state is Auto
    public void Default_is_auto()
    {
        Assert.True(default(ColumnWidth).IsAuto);
    }

    [Fact] // ADR-0016: an Auto width prints — reading it for a message or a log must not throw
    public void Widths_and_specs_print_whether_auto_or_fixed()
    {
        Assert.Equal("Auto", ColumnWidth.Auto.ToString());
        Assert.Equal("Fixed(120px)", ColumnWidth.Fixed(120).ToString());
        Assert.Contains("Auto", new ColumnWidthSpec(ColumnWidth.Auto).ToString());
        Assert.Contains("Fixed(120px)", new ColumnWidthSpec(ColumnWidth.Fixed(120)).ToString());
    }
}
