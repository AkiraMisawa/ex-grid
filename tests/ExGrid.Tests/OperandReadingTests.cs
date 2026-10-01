using System.Globalization;
using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A filter operand reads back as itself, and a typed number reads as the user meant it, in every
/// culture (ADR-0023; ADR-0006's note of 2026-10-01; ticket 96; principle 1). Under de-DE, 1234.5
/// used to reopen as <c>1234,5</c> and read back, invariant first, as 12345: quietly wrong. A text
/// that reads two ways is refused by name, never guessed.
/// </summary>
public class OperandReadingTests
{
    public static TheoryData<string> Cultures() => ["en-US", "de-DE", "fr-FR", "ja-JP"];

    private static readonly decimal[] Numbers = [0m, 1m, 0.5m, 1.234m, 1000m, 1234.5m, -1234.5m, 1234567.89m, -0.001m, 12345678901234.5m];

    private static OperandReading Read(string text, string culture)
        => FilterPanelChoices.ReadOperand(ColumnType.Number, text, CultureInfo.GetCultureInfo(culture));

    [Theory, MemberData(nameof(Cultures))] // ADR-0023, principle 1: a number operand reopens in a text that reads back as itself
    public void A_number_operand_reads_back_as_itself(string culture)
    {
        var c = CultureInfo.GetCultureInfo(culture);
        foreach (var number in Numbers)
        {
            var text = FilterPanelChoices.OperandText(number, c);
            var reading = FilterPanelChoices.ReadOperand(ColumnType.Number, text, c);

            Assert.False(reading.IsRefused, $"{number} reopened as \"{text}\" under {culture} and was refused");
            Assert.Equal(number, reading.Value);
        }
    }

    [Fact] // ADR-0023, principle 1: under de-DE, 1234.5 reopens as 1234,5 and reads back as 1234.5, not 12345
    public void Under_de_de_a_reopened_decimal_comma_reads_back_as_itself()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("1234,5", FilterPanelChoices.OperandText(1234.5m, de));
        Assert.Equal(1234.5m, FilterPanelChoices.ReadOperand(ColumnType.Number, "1234,5", de).Value);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023 / ADR-0006: a date operand reopens in its ISO form and reads back as itself
    public void A_date_operand_reads_back_as_itself(string culture)
    {
        var c = CultureInfo.GetCultureInfo(culture);
        var date = new DateTime(2026, 1, 5, 9, 5, 7);

        var text = FilterPanelChoices.OperandText(date, c);

        Assert.Equal("2026-01-05 09:05:07", text);
        Assert.Equal(date, FilterPanelChoices.ReadOperand(ColumnType.Date, text, c).Value);
    }

    [Theory] // ADR-0023, principle 1: a typed number reads as the culture writes it
    [InlineData("en-US", "1,234.5", "1234.5")]
    [InlineData("en-US", "1234.5", "1234.5")]
    [InlineData("en-US", "1,234", "1234")]
    [InlineData("en-US", "-1,234,567.25", "-1234567.25")]
    [InlineData("de-DE", "1.234,5", "1234.5")]
    [InlineData("de-DE", "1234,5", "1234.5")]
    [InlineData("de-DE", "1,234", "1.234")]          // the decimal comma: de-DE's own reading
    [InlineData("de-DE", "1.234.567", "1234567")]   // two dots can only group
    [InlineData("de-DE", "-1.234,5", "-1234.5")]
    [InlineData("fr-FR", "1 234,5", "1234.5")]      // a space for fr-FR's narrow no-break space
    [InlineData("fr-FR", "1 234,5", "1234.5")]
    [InlineData("fr-FR", "1 234,5", "1234.5")]
    [InlineData("fr-FR", "1234,5", "1234.5")]
    [InlineData("ja-JP", "1,234.5", "1234.5")]
    [InlineData("ja-JP", "1234.5", "1234.5")]
    public void A_typed_number_reads_as_the_culture_writes_it(string culture, string typed, string expected)
    {
        var reading = Read(typed, culture);

        Assert.Equal(OperandRefusal.None, reading.Refusal);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), reading.Value);
    }

    [Theory] // ADR-0023, principle 1: a text that reads as two numbers is refused by name, never guessed
    [InlineData("de-DE", "1.234", "1234", "1.234")]
    [InlineData("de-DE", "1.000", "1000", "1")]
    [InlineData("de-DE", "-12.345", "-12345", "-12.345")]
    public void A_text_that_reads_two_ways_is_refused(string culture, string typed, string asCulture, string asPoint)
    {
        var reading = Read(typed, culture);

        Assert.Equal(OperandRefusal.ReadsTwoWays, reading.Refusal);
        Assert.Null(reading.Value);
        Assert.Equal((decimal.Parse(asCulture, CultureInfo.InvariantCulture), decimal.Parse(asPoint, CultureInfo.InvariantCulture)),
            ((decimal)reading.OtherValue!.Value.CultureReading, (decimal)reading.OtherValue!.Value.OtherReading));
    }

    [Theory] // ADR-0023, principle 1: another culture's separators, or a grouping the culture does not write, is not read
    [InlineData("en-US", "1234,5")]   // read as 12345 before ticket 96
    [InlineData("en-US", "12,34")]    // read as 1234
    [InlineData("de-DE", "1234.5")]   // read as 12345
    [InlineData("de-DE", "12.34")]    // read as 1234
    [InlineData("fr-FR", "1.234")]
    [InlineData("fr-FR", "1234.5")]
    [InlineData("ja-JP", "1234,5")]
    [InlineData("ja-JP", "abc")]
    [InlineData("en-US", "1.234,5")]
    public void Another_cultures_separators_are_not_read(string culture, string typed)
    {
        var reading = Read(typed, culture);

        Assert.Equal(OperandRefusal.NotReadable, reading.Refusal);
        Assert.Null(reading.Value);
    }

    [Fact] // ADR-0023: nothing typed is no operand, not a refusal
    public void Nothing_typed_is_no_operand()
    {
        var reading = Read("", "de-DE");

        Assert.False(reading.IsRefused);
        Assert.Null(reading.Value);
    }

    [Fact] // ADR-0023, principle 1: the refusal says what was typed, and for two readings, both of them and how to type each
    public void The_refusal_names_what_was_typed()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");

        var twoWays = FilterPanelChoices.RefusalText(Read("1.234", "de-DE"), "1.234", de)!;
        var notRead = FilterPanelChoices.RefusalText(Read("1234.5", "de-DE"), "1234.5", de)!;

        Assert.Contains("“1.234” reads two ways", twoWays);
        Assert.Contains("1234", twoWays);
        Assert.Contains("1,234", twoWays);
        Assert.Contains("“1234.5” is not a value of this column", notRead);
        Assert.Null(FilterPanelChoices.RefusalText(Read("1234,5", "de-DE"), "1234,5", de));
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023: ParseOperand reads in the current culture, as ReadOperand does
    public void Parse_operand_reads_in_the_current_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var text = FilterPanelChoices.OperandText(1234.5m, CultureInfo.CurrentCulture);
            Assert.Equal(1234.5m, FilterPanelChoices.ParseOperand(ColumnType.Number, text));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact] // ADR-0006: a substituted panel lists a value as the cells show it — the Format, or a date's ISO form
    public void Value_text_is_what_the_cells_show()
    {
        Assert.Equal("2026-01-05 00:00:00", FilterPanelChoices.ValueText(new DateTime(2026, 1, 5), null));
        Assert.Equal("5 Jan", FilterPanelChoices.ValueText(new DateTime(2026, 1, 5), _ => "5 Jan"));
        Assert.Equal("Alpha", FilterPanelChoices.ValueText("Alpha", null));
    }
}
