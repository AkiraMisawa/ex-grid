using Xunit;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>A cell's place as <c>B7</c>, written and read within Excel's grid (ADR-0330 §4: a refusal names the cell).</summary>
public class CellReferenceTests
{
    [Theory] // ADR-0330 §4: places are written as Excel names them, A1 to XFD1048576
    [InlineData(0, 0, "A1")]
    [InlineData(6, 1, "B7")]
    [InlineData(0, 25, "Z1")]
    [InlineData(0, 26, "AA1")]
    [InlineData(0, 701, "ZZ1")]
    [InlineData(0, 702, "AAA1")]
    [InlineData(1_048_575, 16_383, "XFD1048576")]
    public void A_place_is_written_and_read_back(int row, int column, string reference)
    {
        Assert.Equal(reference, XlsxCellReference.Format(row, column));
        Assert.True(XlsxCellReference.TryParse(reference, out var r, out var c));
        Assert.Equal((row, column), (r, c));
    }

    [Theory] // ADR-0330 §4: text that is not a cell inside the grid is not read as one
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("A0")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    [InlineData("AAAA1")]
    [InlineData("A1B")]
    [InlineData("A 1")]
    public void Text_outside_the_grid_is_not_a_place(string text)
    {
        Assert.False(XlsxCellReference.TryParse(text, out _, out _));
    }

    [Fact] // ADR-0330 §4: absolute marks are read through
    public void Absolute_marks_are_read_through()
    {
        Assert.True(XlsxCellReference.TryParse("$C$3", out var r, out var c));
        Assert.Equal((2, 2), (r, c));
    }
}
