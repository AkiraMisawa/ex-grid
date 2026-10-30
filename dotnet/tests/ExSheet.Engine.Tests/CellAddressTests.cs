using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// CellAddress's facts that a shared case does not hold: what an address parses to, and what is refused, is in
/// <c>tests/cases/sheet/cell-address.json</c>. The walk over every column and the order stay here, mirrored in the
/// Script Core's <c>surface.test.ts</c>.
/// </summary>
public class CellAddressTests
{
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
