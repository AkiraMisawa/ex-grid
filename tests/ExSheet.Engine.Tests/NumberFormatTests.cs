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

    [Theory] // ADR-0046/0047: number, percent and thousands codes render as Excel's
    [InlineData(1234.567, "0.00", "1234.57")]
    [InlineData(1234.567, "#,##0.00", "1,234.57")]
    [InlineData(1234567, "#,##0", "1,234,567")]
    [InlineData(-1234.5, "#,##0.00", "-1,234.50")]
    [InlineData(0.256, "0%", "26%")]
    [InlineData(0.256, "0.0%", "25.6%")]
    [InlineData(5, "000", "005")]
    [InlineData(0.5, "#.##", ".5")]
    [InlineData(1.5, "0.###", "1.5")]
    [InlineData(1.5, "0.000", "1.500")]
    [InlineData(1234567, "#,##0,", "1,235")]
    [InlineData(1234567890, "0.0,,", "1234.6")]
    [InlineData(12345, "0.00E+00", "1.23E+04")]
    [InlineData(0.000123, "0.00E+00", "1.23E-04")]
    [InlineData(0.5, "0", "1")]
    [InlineData(2.5, "0", "3")]
    [InlineData(-2.5, "0", "-3")]
    [InlineData(123, "0.00\" kg\"", "123.00 kg")]
    [InlineData(5, "\\$0.00", "$5.00")]
    [InlineData(-5, "$#,##0.00", "-$5.00")]
    public void Number_codes_render_as_excels(double number, string code, string expected)
    {
        Assert.Equal(expected, Show(number, code).Text);
    }

    [Theory] // ADR-0046: sections are positive; negative; zero; text
    [InlineData(5, "0.00;(0.00)", "5.00")]
    [InlineData(-5, "0.00;(0.00)", "(5.00)")]
    [InlineData(0, "0;-0;\"zero\"", "zero")]
    [InlineData(-3, "0;-0;\"zero\"", "-3")]
    [InlineData(0, "0.00;(0.00)", "0.00")]
    public void Sections_choose_by_sign(double number, string code, string expected)
    {
        Assert.Equal(expected, Show(number, code).Text);
    }

    [Theory] // ADR-0046: text shows as it is, or through the text section's @
    [InlineData("abc", "0.00", "abc")]
    [InlineData("abc", "@", "abc")]
    [InlineData("x", "0;0;0;\"[\"@\"]\"", "[x]")]
    public void Text_uses_the_text_section(string text, string code, string expected)
    {
        Assert.Equal(expected, ShowText(text, code).Text);
    }

    [Theory] // ADR-0046/0048: the culture's separators, over the invariant code
    [InlineData("de-DE", "1.234,50")]
    [InlineData("ja-JP", "1,234.50")]
    [InlineData("en-US", "1,234.50")]
    public void Separators_are_the_cultures(string culture, string expected)
    {
        Assert.Equal(expected, Show(1234.5, "#,##0.00", culture).Text);
    }

    [Theory] // ADR-0047 (SH-10): General shows at most 15 significant digits
    [InlineData(1234.5, "1234.5")]
    [InlineData(1.0 / 3, "0.333333333333333")]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(-42, "-42")]
    [InlineData(123456789012345, "123456789012345")]
    public void General_shows_fifteen_significant_digits(double number, string expected)
    {
        Assert.Equal(expected, Show(number, "General").Text);
    }

    [Fact] // ADR-0047 (SH-10): past fifteen significant digits a number shows zeros, as Excel's does
    public void Digits_past_fifteen_show_as_zeros()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        sheet.SetFormat(a1, NumberFormat.Parse("0"));

        sheet.Enter(a1, "1234567890123456789");

        Assert.Equal("1234567890123450000", sheet.GetDisplay(a1).Text);
        Assert.Equal("0.666666666666667", Show(2.0 / 3, "General").Text);
    }

    [Fact] // ADR-0048: General uses the culture's decimal separator
    public void General_uses_the_cultures_decimal_separator()
    {
        Assert.Equal("1,5", Show(1.5, "General", "de-DE").Text);
    }

    [Theory] // ADR-0047 (SH-10): date serials match Excel's 1900 system around its 29 February 1900
    [InlineData(1, "1900-01-01")]
    [InlineData(59, "1900-02-28")]
    [InlineData(60, "1900-02-29")]
    [InlineData(61, "1900-03-01")]
    [InlineData(45292, "2024-01-01")]
    [InlineData(2958465, "9999-12-31")]
    public void Date_serials_match_excel(double serial, string expected)
    {
        Assert.Equal(expected, Show(serial, "yyyy-mm-dd").Text);
    }

    [Fact] // ADR-0047: serial 0 is 0 January 1900, as Excel writes it
    public void Serial_zero_is_january_zero()
    {
        Assert.Equal("1/0/1900", Show(0, "m/d/yyyy").Text);
    }

    [Theory] // ADR-0047 (SH-10): typing a date around 29 February 1900 gives Excel's serial
    [InlineData("1/1/1900", 1)]
    [InlineData("2/28/1900", 59)]
    [InlineData("2/29/1900", 60)]
    [InlineData("3/1/1900", 61)]
    [InlineData("1/1/2024", 45292)]
    public void Typed_dates_give_excels_serials(string typed, double serial)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));

        sheet.Enter(CellAddress.Parse("A1"), typed);

        Assert.Equal(serial, sheet.GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }

    [Theory] // ADR-0046: date and time codes render as Excel's under en-US
    [InlineData(45292, "mmm d, yyyy", "Jan 1, 2024")]
    [InlineData(45292, "mmmm", "January")]
    [InlineData(45292, "dddd", "Monday")]
    [InlineData(45292, "ddd dd/mm/yy", "Mon 01/01/24")]
    [InlineData(1, "dddd", "Sunday")]          // Excel counts 1 January 1900 as a Sunday
    [InlineData(45292.5, "yyyy-mm-dd hh:mm:ss", "2024-01-01 12:00:00")]
    [InlineData(0.5, "h:mm", "12:00")]
    [InlineData(0.75, "h:mm AM/PM", "6:00 PM")]
    [InlineData(0, "h:mm AM/PM", "12:00 AM")]
    [InlineData(0.5, "h:mm AM/PM", "12:00 PM")]
    [InlineData(0.25 + 5.0 / 1440, "hh:mm", "06:05")]
    [InlineData(45292, "mm/dd", "01/01")]
    [InlineData(0.25 + 5.0 / 1440 + 7.0 / 86400, "m:ss", "5:07")]
    public void Date_codes_render_as_excels(double serial, string code, string expected)
    {
        Assert.Equal(expected, Show(serial, code).Text);
    }

    [Fact] // ADR-0046/0048: month names are the culture's
    public void Month_names_are_the_cultures()
    {
        Assert.Equal("Januar", Show(45292, "mmmm", "de-DE").Text);
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

    [Fact] // ADR-0046: typing a number into a date cell keeps the date format, as Excel does
    public void A_number_typed_into_a_date_cell_shows_as_a_date()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        sheet.Enter(a1, "9/26/2026");

        sheet.Enter(a1, "45292");

        Assert.Equal("1/1/2024", sheet.GetDisplay(a1).Text);
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
