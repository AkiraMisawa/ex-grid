using System.Globalization;
using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A typed date operand reads in the culture, never invariant first (ADR-0023's note of 2026-10-01;
/// ticket 97; principle 1). Ticket 94's ISO forms read exactly, anything else in the form's culture
/// alone, and a text that reads two ways, or not at all, is refused by name. Under en-GB,
/// <c>05/01/2026</c> used to read invariant first, as 1 May.
/// </summary>
public class DateOperandReadingTests
{
    public static TheoryData<string> Cultures() => ["en-US", "en-GB", "de-DE", "ja-JP"];

    private static OperandReading Read(string text, string culture)
        => FilterPanelChoices.ReadOperand(ColumnType.Date, text, CultureInfo.GetCultureInfo(culture));

    private static DateTime At(string iso) => DateTime.ParseExact(iso, ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture);

    [Fact] // ADR-0023, principle 1: under en-GB, 05/01/2026 is 5 January — invariant first, it was 1 May
    public void Under_en_gb_a_slashed_date_is_day_first()
    {
        var reading = Read("05/01/2026", "en-GB");

        Assert.Equal(OperandRefusal.None, reading.Refusal);
        Assert.Equal(new DateTime(2026, 1, 5), reading.Value);
    }

    [Fact] // ADR-0023, principle 1: the same text under en-US is 1 May, as en-US writes it
    public void Under_en_us_the_same_text_is_month_first()
        => Assert.Equal(new DateTime(2026, 5, 1), Read("05/01/2026", "en-US").Value);

    [Theory] // ADR-0023, principle 1: a typed date reads in the culture's own order, a two-digit year and a time with it included
    [InlineData("en-US", "05/01/2026", "2026-05-01")]
    [InlineData("en-US", "05/01/26", "2026-05-01")]
    [InlineData("en-US", "05/01/2026 9:05 PM", "2026-05-01 21:05")]
    [InlineData("en-US", "2026/01/05", "2026-01-05")]
    [InlineData("en-US", "Jan 5, 2026", "2026-01-05")]
    [InlineData("en-GB", "05/01/2026", "2026-01-05")]
    [InlineData("en-GB", "05/01/26", "2026-01-05")]
    [InlineData("en-GB", "05/01/2026 09:05", "2026-01-05 09:05")]
    [InlineData("en-GB", "5 January 2026", "2026-01-05")]
    [InlineData("de-DE", "05.01.2026", "2026-01-05")]
    [InlineData("de-DE", "05.01.26", "2026-01-05")]
    [InlineData("de-DE", "05.01.2026 21:05:07", "2026-01-05 21:05:07")]
    [InlineData("de-DE", "05/01/2026", "2026-01-05")]
    [InlineData("ja-JP", "2026/01/05", "2026-01-05")]
    [InlineData("ja-JP", "26/01/05", "2026-01-05")]
    [InlineData("ja-JP", "2026/01/05 9:05:07", "2026-01-05 09:05:07")]
    [InlineData("ja-JP", "01/13/2026", "2026-01-13")]   // only month first is a date
    public void A_typed_date_reads_in_the_cultures_order(string culture, string typed, string expected)
    {
        var reading = Read(typed, culture);

        Assert.Equal(OperandRefusal.None, reading.Refusal);
        Assert.Equal(At(expected), reading.Value);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023 / ticket 94: the ISO forms read exactly, the same in every culture
    public void The_iso_forms_read_exactly_in_every_culture(string culture)
    {
        Assert.Equal(new DateTime(2026, 1, 5), Read("2026-01-05", culture).Value);
        Assert.Equal(new DateTime(2026, 1, 5, 9, 5, 7), Read("2026-01-05 09:05:07", culture).Value);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 9, 5, 7, TimeSpan.FromHours(9)), Read("2026-01-05 09:05:07 +09:00", culture).Value);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023, principle 1: a date operand reopens in a text that reads back as itself
    public void A_date_operand_reads_back_as_itself(string culture)
    {
        var c = CultureInfo.GetCultureInfo(culture);
        foreach (var date in new[] { new DateTime(2026, 1, 5), new DateTime(2026, 5, 1, 21, 5, 7), new DateTime(1999, 12, 31, 23, 59, 59) })
        {
            var text = FilterPanelChoices.OperandText(date, c);
            Assert.Equal(date, FilterPanelChoices.ReadOperand(ColumnType.Date, text, c).Value);
        }
    }

    [Fact] // ADR-0023, principle 1: under a culture that writes the year first, a date with its year last reads two ways, and is refused
    public void Under_ja_jp_a_year_last_date_reads_two_ways()
    {
        var reading = Read("05/01/2026", "ja-JP");

        Assert.Equal(OperandRefusal.ReadsTwoWays, reading.Refusal);
        Assert.Null(reading.Value);
        Assert.Equal((new DateTime(2026, 5, 1), new DateTime(2026, 1, 5)),
            ((DateTime)reading.OtherValue!.Value.CultureReading, (DateTime)reading.OtherValue!.Value.OtherReading));
        Assert.Equal(new DateTime(2026, 5, 5), Read("05/05/2026", "ja-JP").Value); // one date either way
    }

    [Theory] // ADR-0023, principle 1: a text the culture does not read as a date is refused, never read in another culture
    [InlineData("en-US", "13/01/2026")]   // en-GB's 13 January, not en-US's
    [InlineData("en-GB", "01/13/2026")]   // en-US's 13 January, not en-GB's
    [InlineData("de-DE", "01/13/2026")]
    [InlineData("ja-JP", "13/01/2026")]
    [InlineData("en-GB", "2026/13/01")]
    [InlineData("de-DE", "abc")]
    public void A_text_the_culture_does_not_read_is_refused(string culture, string typed)
    {
        var reading = Read(typed, culture);

        Assert.Equal(OperandRefusal.NotReadable, reading.Refusal);
        Assert.Null(reading.Value);
    }

    [Fact] // ADR-0023, principle 1: a date that reads two ways is refused naming both, in forms that read back exactly
    public void The_refusal_names_both_dates()
    {
        var ja = CultureInfo.GetCultureInfo("ja-JP");

        var text = FilterPanelChoices.RefusalText(Read("05/01/2026", "ja-JP"), "05/01/2026", ja)!;

        Assert.Equal("“05/01/2026” reads two ways: as 2026-05-01, and as 2026-01-05. Type the one you mean as 2026-05-01 or 2026-01-05.", text);
        Assert.Contains("is not a value of this column", FilterPanelChoices.RefusalText(Read("13/01/2026", "en-US"), "13/01/2026", CultureInfo.GetCultureInfo("en-US")));
    }
}
