using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

public class NumberFormatTests
{
    private static CellDisplay Show(double number, string code, string culture = "en-US")
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo(culture));
        var a1 = CellAddress.Parse("A1");
        sheet.SetEntry(a1, Entry.FromValue(Value.FromNumber(number)));
        sheet.SetFormat(a1, NumberFormat.Parse(code));
        return sheet.GetDisplay(a1);
    }

    private static CellDisplay ShowText(string text, string code)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        sheet.SetEntry(a1, Entry.FromValue(Value.FromText(text)));
        sheet.SetFormat(a1, NumberFormat.Parse(code));
        return sheet.GetDisplay(a1);
    }

    [Theory] // ADR-0016/0047: a date that cannot exist cannot be shown, and shows ####
    [InlineData(-1)]
    [InlineData(2958466)]
    public void An_impossible_date_cannot_show(double serial)
    {
        var display = Show(serial, "yyyy-mm-dd");

        Assert.True(display.CannotShow);
        Assert.Equal("", display.Text);
        Assert.True(display.IsNumber);
    }

    [Fact] // ADR-0016 (SH-10): a number reports itself as a number, so a grid can show #### when it does not fit
    public void Numbers_and_dates_are_numbers_to_the_grid()
    {
        Assert.True(Show(1, "0.00").IsNumber);
        Assert.True(Show(45292, "yyyy-mm-dd").IsNumber);
        Assert.False(ShowText("1", "@").IsNumber);
    }

    [Fact] // ADR-0046: General alignment puts numbers right, text left, booleans and errors in the centre
    public void General_alignment_follows_the_value()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "1");
        sheet.Enter(CellAddress.Parse("A2"), "text");
        sheet.Enter(CellAddress.Parse("A3"), "TRUE");
        sheet.Enter(CellAddress.Parse("A4"), "=1/0");

        Assert.Equal(HorizontalAlignment.Right, sheet.GetDisplay(CellAddress.Parse("A1")).Alignment);
        Assert.Equal(HorizontalAlignment.Left, sheet.GetDisplay(CellAddress.Parse("A2")).Alignment);
        Assert.Equal(HorizontalAlignment.Center, sheet.GetDisplay(CellAddress.Parse("A3")).Alignment);
        Assert.Equal(HorizontalAlignment.Center, sheet.GetDisplay(CellAddress.Parse("A4")).Alignment);
        Assert.Equal("#DIV/0!", sheet.GetDisplay(CellAddress.Parse("A4")).Text);
        Assert.Equal("TRUE", sheet.GetDisplay(CellAddress.Parse("A3")).Text);
    }

    [Fact] // ADR-0046: alignment is settable per cell, and changing it repaints the row without recomputing
    public void Alignment_is_settable_per_cell()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var b3 = CellAddress.Parse("B3");
        sheet.Enter(b3, "1");

        var change = sheet.SetAlignment(b3, HorizontalAlignment.Center);

        Assert.Equal(HorizontalAlignment.Center, sheet.GetDisplay(b3).Alignment);
        Assert.Equal([2], change.Rows);
        Assert.Empty(change.Recalculated);
        Assert.Empty(change.ValueChanges);
    }

    [Fact] // ADR-0046: a format set on a blank cell applies to what is typed there later
    public void A_format_on_a_blank_cell_applies_later()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");

        var change = sheet.SetFormat(a1, NumberFormat.Parse("0.00"));
        sheet.Enter(a1, "3");

        Assert.Equal([0], change.Rows);
        Assert.Equal("3.00", sheet.GetDisplay(a1).Text);
    }

    [Theory] // ADR-0047: a format code outside the supported subset is refused, not shown some other way
    [InlineData("[Red]0")]
    [InlineData("0.00_);[Red](0.00)")]
    [InlineData("# ?/?")]
    [InlineData("*-0")]
    [InlineData("[h]:mm")]
    [InlineData("hh:mm:ss.00")]
    [InlineData("abc")]
    [InlineData("0;0;0;0;0")]
    [InlineData("\"open")]
    [InlineData("##0.0E+0")]
    [InlineData("0@")]
    public void An_unsupported_code_is_refused(string code)
    {
        Assert.False(NumberFormat.TryParse(code, out _, out var reason));
        Assert.False(string.IsNullOrEmpty(reason));
        Assert.Throws<FormatException>(() => NumberFormat.Parse(code));
    }
}
