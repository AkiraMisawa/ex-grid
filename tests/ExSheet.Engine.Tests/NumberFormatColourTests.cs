using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

public class NumberFormatColourTests
{
    private static CellDisplay Show(Value value, string code, double? width = null)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        sheet.SetEntry(a1, Entry.FromValue(value));
        sheet.SetNumberFormat(a1, NumberFormat.Parse(code));
        return width is { } w ? sheet.GetDisplay(a1, w) : sheet.GetDisplay(a1);
    }

    [Theory] // ADR-0071, SH-40: formatting a number answers the colour of the section it used, with the text
    [InlineData("[Red]0", 5, "5", NumberFormatColour.Red)]
    [InlineData("[red]0", 5, "5", NumberFormatColour.Red)]
    [InlineData("[MAGENTA]0", 5, "5", NumberFormatColour.Magenta)]
    [InlineData("[Cyan]0", -7, "-7", NumberFormatColour.Cyan)]   // one section shows negatives too, with a minus sign
    [InlineData("0.00_);[Red](0.00)", -1.5, "(1.50)", NumberFormatColour.Red)]
    [InlineData("0.00_);[Red](0.00)", 1.5, "1.50 ", null)]
    [InlineData("[Blue]0;[Magenta]-0;[Green]\"zero\"", 3, "3", NumberFormatColour.Blue)]
    [InlineData("[Blue]0;[Magenta]-0;[Green]\"zero\"", -3, "-3", NumberFormatColour.Magenta)]
    [InlineData("[Blue]0;[Magenta]-0;[Green]\"zero\"", 0, "zero", NumberFormatColour.Green)]
    [InlineData("[Blue]0;[Magenta]-0", 0, "0", NumberFormatColour.Blue)]   // two sections: zero is the first's
    [InlineData("0;[Yellow]-0", -7, "-7", NumberFormatColour.Yellow)]
    [InlineData("[White]0.00", 1, "1.00", NumberFormatColour.White)]
    [InlineData("[Black]yyyy-mm-dd", 45292, "2024-01-01", NumberFormatColour.Black)]
    [InlineData("[Green]0%", 0.5, "50%", NumberFormatColour.Green)]
    [InlineData("0.00", 1, "1.00", null)]
    [InlineData("General", 1, "1", null)]
    public void A_number_answers_the_colour_of_its_section(string code, double number, string text, NumberFormatColour? colour)
    {
        var display = Show(Value.FromNumber(number), code);

        Assert.Equal(text, display.Text);
        Assert.Equal(colour, display.Colour);
    }

    [Theory] // ADR-0071, SH-40: text shown by a text section takes that section's colour
    [InlineData("0;0;0;[Red]@", "Q3", "Q3", NumberFormatColour.Red)]
    [InlineData("[Blue]@", "Q3", "Q3", NumberFormatColour.Blue)]
    [InlineData("0;[Red]-0;0;\"total \"@", "Q3", "total Q3", null)]
    public void Text_answers_the_colour_of_its_text_section(string code, string entered, string text, NumberFormatColour? colour)
    {
        var display = Show(Value.FromText(entered), code);

        Assert.Equal(text, display.Text);
        Assert.Equal(colour, display.Colour);
    }

    [Fact] // ADR-0071 reading, SH-40: text in a format with no text section uses no section, so no section's colour
    public void Text_without_a_text_section_has_no_colour()
    {
        var display = Show(Value.FromText("Q3"), "[Red]0");

        Assert.Equal("Q3", display.Text);
        Assert.Null(display.Colour);
    }

    [Fact] // ADR-0071 reading, SH-40: a number shown as General beside a lone text section uses no section, so no section's colour
    public void A_number_beside_a_lone_text_section_has_no_colour()
    {
        var display = Show(Value.FromNumber(5), "[Red]@");

        Assert.Equal("5", display.Text);
        Assert.Null(display.Colour);
    }

    [Fact] // ADR-0071 reading, SH-40: booleans and Error Values show as themselves, through no section, so no section's colour
    public void Booleans_and_Error_Values_have_no_colour()
    {
        Assert.Null(Show(Value.FromBoolean(true), "[Red]0;[Red]-0;[Red]0;[Red]@").Colour);
        Assert.Null(Show(Value.FromError(ErrorValue.Div0), "[Red]0;[Red]-0;[Red]0;[Red]@").Colour);
    }

    [Fact] // ADR-0071, SH-40: a blank cell shows nothing, so nothing is coloured
    public void A_blank_cell_has_no_colour()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        sheet.SetNumberFormat(a1, NumberFormat.Parse("[Red]0"));

        Assert.Null(sheet.GetDisplay(a1).Colour);
        Assert.Null(sheet.GetDisplay(a1, 10).Colour);
    }

    [Theory] // ADR-0071, SH-40: the display at a column's width, which the component paints, carries the colour too
    [InlineData("0;[Red]-0", -5, 10, "-5", NumberFormatColour.Red)]
    [InlineData("0;[Red]-0", 5, 10, "5", null)]
    [InlineData("[Blue]0;[Magenta]-0;[Green]\"zero\"", 0, 10, "zero", NumberFormatColour.Green)]
    public void The_display_at_a_width_carries_the_colour(string code, double number, double width, string text, NumberFormatColour? colour)
    {
        var display = Show(Value.FromNumber(number), code, width);

        Assert.False(display.CannotShow);
        Assert.Equal(text, display.Text);
        Assert.Equal(colour, display.Colour);
    }

    [Theory] // ADR-0071 reading, SH-40, ADR-0016: a number its section cannot show in the width (####) keeps that section's colour
    [InlineData("0;[Red]-0", -123456, 3, NumberFormatColour.Red)]
    [InlineData("[Blue]0.00", 123456, 3, NumberFormatColour.Blue)]
    public void A_number_that_does_not_fit_keeps_its_colour(string code, double number, double width, NumberFormatColour colour)
    {
        var display = Show(Value.FromNumber(number), code, width);

        Assert.True(display.CannotShow);
        Assert.Equal("", display.Text);
        Assert.Equal(colour, display.Colour);
    }

    [Fact] // ADR-0071 reading, SH-40, ADR-0016: a date that cannot exist cannot show at any width, and keeps its section's colour
    public void An_impossible_date_keeps_its_colour()
    {
        var display = Show(Value.FromNumber(-1), "[Red]yyyy-mm-dd");

        Assert.True(display.CannotShow);
        Assert.Equal(NumberFormatColour.Red, display.Colour);
    }

    [Fact] // ADR-0071, SH-40: General fitted to a width uses no section, so no colour
    public void General_at_a_width_has_no_colour()
    {
        Assert.Null(Show(Value.FromNumber(1.0 / 3), "General", 8).Colour);
        Assert.Null(Show(Value.FromNumber(123456789), "General", 1).Colour);
    }

    [Fact] // ADR-0071, SH-40, ADR-0047 (TYPED-052): $-5 typed takes Excel's currency format, and its negative section is red
    public void A_typed_negative_dollar_amount_is_red()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        var b1 = CellAddress.Parse("B1");

        sheet.Enter(a1, "$-5");
        sheet.Enter(b1, "$5");

        Assert.Equal("($5)", sheet.GetDisplay(a1).Text);
        Assert.Equal(NumberFormatColour.Red, sheet.GetDisplay(a1).Colour);
        Assert.Null(sheet.GetDisplay(b1).Colour);
    }

    [Theory] // ADR-0071, SH-40: a reading until the eleventh Windows run, case 1 — the eight names are Excel's legacy palette
    [InlineData(NumberFormatColour.Black, 0x000000)]
    [InlineData(NumberFormatColour.Blue, 0x0000FF)]
    [InlineData(NumberFormatColour.Cyan, 0x00FFFF)]
    [InlineData(NumberFormatColour.Green, 0x00FF00)]
    [InlineData(NumberFormatColour.Magenta, 0xFF00FF)]
    [InlineData(NumberFormatColour.Red, 0xFF0000)]
    [InlineData(NumberFormatColour.White, 0xFFFFFF)]
    [InlineData(NumberFormatColour.Yellow, 0xFFFF00)]
    public void Each_name_is_painted_in_Excels_legacy_palette(NumberFormatColour colour, int rgb)
    {
        Assert.Equal(rgb, colour.Rgb());
    }

    [Fact] // ADR-0071, SH-40: every one of the eight names is read, and each answers itself
    public void Every_name_is_read()
    {
        foreach (var colour in Enum.GetValues<NumberFormatColour>())
        {
            Assert.Equal(colour, Show(Value.FromNumber(1), $"[{colour}]0").Colour);
        }
    }

    [Fact] // ADR-0071, SH-40: a reading until the eleventh Windows run, case 2 — the engine's half: -5 answers Red, which wins over the Font colour; 5 answers none, so the Font colour shows
    public void The_format_colour_is_answered_only_where_its_section_names_one()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        var a1 = CellAddress.Parse("A1");
        var b1 = CellAddress.Parse("B1");
        sheet.Enter(a1, "-5");
        sheet.Enter(b1, "5");
        sheet.SetNumberFormat(new CellRange(a1, b1), NumberFormat.Parse("0;[Red]-0"));

        Assert.Equal(NumberFormatColour.Red, sheet.GetDisplay(a1).Colour);
        Assert.Null(sheet.GetDisplay(b1).Colour);
    }

    [Theory] // ADR-0071, ADR-0047 (FMT-075..077), SH-40: a numbered colour stays refused, by name
    [InlineData("[Color10]0")]
    [InlineData("[color3]0")]
    [InlineData("0;[Color10]-0")]
    [InlineData("[Color 3]0")]
    public void A_numbered_colour_is_refused_by_name(string code)
    {
        Assert.False(NumberFormat.TryParse(code, out _, out var reason));
        Assert.Contains("numbered colour", reason, StringComparison.Ordinal);
        Assert.Contains("[Color n]", reason, StringComparison.Ordinal);
    }
}
