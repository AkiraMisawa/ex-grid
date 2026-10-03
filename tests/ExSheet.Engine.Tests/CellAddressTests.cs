using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

public class CellAddressTests
{
    [Theory] // ADR-0046: a Sheet is Excel's extent, addressed A1 to XFD1048576
    [InlineData("A1", 0, 0)]
    [InlineData("B7", 6, 1)]
    [InlineData("Z1", 0, 25)]
    [InlineData("AA1", 0, 26)]
    [InlineData("AZ1", 0, 51)]
    [InlineData("BA1", 0, 52)]
    [InlineData("ZZ1", 0, 701)]
    [InlineData("AAA1", 0, 702)]
    [InlineData("XFD1048576", 1_048_575, 16_383)]
    [InlineData("xfd1048576", 1_048_575, 16_383)]
    public void An_address_parses_to_its_zero_based_position(string text, int row, int column)
    {
        var address = CellAddress.Parse(text);

        Assert.Equal(new CellAddress(row, column), address);
        Assert.Equal(text.ToUpperInvariant(), address.ToString());
    }

    [Theory] // ADR-0046: nothing outside the extent is an address; it is refused, never clamped
    [InlineData("XFE1")]
    [InlineData("A0")]
    [InlineData("A1048577")]
    [InlineData("A01")]
    [InlineData("$A$1")]
    [InlineData("1A")]
    [InlineData("AAAA1")]
    [InlineData("")]
    [InlineData("A")]
    public void Text_outside_the_extent_is_not_an_address(string text)
    {
        Assert.False(CellAddress.TryParse(text, out _));
        Assert.Throws<FormatException>(() => CellAddress.Parse(text));
    }

    [Fact] // ADR-0046: the extent is 1,048,576 rows by 16,384 columns
    public void A_position_outside_the_extent_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellAddress(Sheet.RowCount, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellAddress(0, Sheet.ColumnCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellAddress(-1, 0));
    }

    [Fact] // ADR-0046: Columns A to XFD are the Column Headings
    public void Every_column_name_reads_back_as_its_column()
    {
        for (var column = 0; column < Sheet.ColumnCount; column++)
        {
            var name = CellAddress.ColumnName(column);
            Assert.True(CellAddress.TryParseColumn(name, out var back));
            Assert.Equal(column, back);
        }
        Assert.Equal("XFD", CellAddress.ColumnName(Sheet.ColumnCount - 1));
    }

    [Fact] // ADR-0046: addresses order row by row, as the rows a change repaints
    public void Addresses_order_row_major()
    {
        var sorted = new[] { "B2", "A2", "C1", "A1" }.Select(CellAddress.Parse).Order().Addresses();

        Assert.Equal(["A1", "C1", "A2", "B2"], sorted);
    }
}
