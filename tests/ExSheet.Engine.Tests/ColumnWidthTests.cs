using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0046 (SH-22): column widths are part of the Sheet Document, in characters (ADR-0047). What a width records, refuses
/// and carries is in the shared cases (<c>tests/cases/sheet/column-width.json</c>); what stays here is the widths JSON
/// cannot write, refused as any other width outside Excel's range.
/// </summary>
public class ColumnWidthTests
{
    [Theory] // ADR-0046: a width that is no finite number is refused, as one outside Excel's range is
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_width_that_is_no_finite_number_is_refused(double width)
    {
        var sheet = NewSheet();
        var columns = CellRange.Parse("B:B");

        Assert.Throws<ArgumentOutOfRangeException>(() => SheetEdit.SetColumnWidth(columns, width));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetColumnWidth(columns, width));
        Assert.Throws<ArgumentOutOfRangeException>(() => SheetEdit.SetAutomaticColumnWidth(columns, width));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetAutomaticColumnWidth(columns, width));
        Assert.Null(sheet.GetColumnWidth(1));
    }
}
