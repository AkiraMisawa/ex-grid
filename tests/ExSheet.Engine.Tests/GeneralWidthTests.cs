using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 (SH-20): the API around General fitting its column. What Excel shows at each width is
/// in the case corpus (<c>ExcelCases/general-width.json</c>); these pin what is the engine's own.
/// </summary>
public class GeneralWidthTests
{
    private static readonly CellAddress A1 = CellAddress.Parse("A1");

    [Fact] // ADR-0047: the width changes only the text; the Value, and the unfitted display, stay whole
    public void The_width_changes_the_text_only()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=1/3");

        Assert.Equal("0.333333", sheet.GetDisplay(A1, Sheet.DefaultColumnWidth).Text);
        Assert.Equal("0.333333333333333", sheet.GetDisplay(A1).Text);
        Assert.Equal(1.0 / 3, sheet.Number("A1"));
    }

    [Fact] // ADR-0016/0047: what General cannot fit is ####, reported as a number that cannot show
    public void A_number_no_form_fits_cannot_show()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "123456789");

        var display = sheet.GetDisplay(A1, 4);

        Assert.True(display.CannotShow);
        Assert.True(display.IsNumber);
        Assert.Equal("", display.Text);
        Assert.Equal(HorizontalAlignment.Right, display.Alignment);
    }

    [Theory] // ADR-0047: a width is characters, a fraction of one holds no more; a round trip through pixels may land a hair below a whole number
    [InlineData(8.43, "1.23E+08")]
    [InlineData(8.99, "1.23E+08")]
    [InlineData(8.9999999999, "123456789")]
    [InlineData(9, "123456789")]
    public void A_column_holds_its_whole_characters(double width, string shown)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "123456789");

        Assert.Equal(shown, sheet.GetDisplay(A1, width).Text);
    }

    [Theory] // ADR-0047: a width is a finite number of characters
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_width_that_is_not_one_is_refused(double width)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");

        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetDisplay(A1, width));
    }

    [Fact] // ADR-0047: text, booleans and Error Values are never fitted
    public void Only_numbers_are_fitted()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "TRUE");
        sheet.Enter("A2", "=1/0");

        Assert.Equal("TRUE", sheet.GetDisplay(A1, 1).Text);
        Assert.False(sheet.GetDisplay(A1, 1).CannotShow);
        Assert.Equal("#DIV/0!", sheet.GetDisplay(CellAddress.Parse("A2"), 1).Text);
    }

    [Fact] // ADR-0047: a blank cell needs no width and shows nothing at any width
    public void A_blank_cell_needs_no_width()
    {
        var sheet = NewSheet();

        Assert.Null(sheet.GetWidthOnEntry(A1));
        Assert.Equal("", sheet.GetDisplay(A1, 0).Text);
    }
}
