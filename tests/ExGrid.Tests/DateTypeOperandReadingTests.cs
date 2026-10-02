using System.Globalization;
using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A typed date operand reads as its column's declared date type, or is refused (ADR-0023, section
/// of 2026-10-02; ticket 98; principle 1). Ticket 97's reading is unchanged; its result must then
/// fit: a DateOnly takes no time, a DateTime no offset, and a DateTimeOffset needs one, written in
/// an ISO form.
/// </summary>
public class DateTypeOperandReadingTests
{
    public static TheoryData<string> Cultures() => ["en-US", "en-GB", "de-DE", "ja-JP"];

    private static OperandReading Read(DateType declared, string text, string culture = "en-US")
        => FilterPanelChoices.ReadOperand(ColumnType.Date, text, CultureInfo.GetCultureInfo(culture), declared);

    private static string? Words(DateType declared, string text, string culture = "en-US")
        => FilterPanelChoices.RefusalText(Read(declared, text, culture), text, CultureInfo.GetCultureInfo(culture));

    private static readonly TimeSpan Tokyo = TimeSpan.FromHours(9);

    [Theory, MemberData(nameof(Cultures))] // ADR-0023: on a DateOnly column a typed day reads as a DateOnly, in every culture
    public void On_a_date_only_column_a_day_reads_as_a_date_only(string culture)
    {
        Assert.Equal(new DateOnly(2026, 10, 2), Read(DateType.DateOnly, "2026-10-02", culture).Value);
    }

    [Fact] // ADR-0023, ticket 97: a DateOnly is read in the culture's order, like any date
    public void A_date_only_reads_in_the_cultures_order()
    {
        Assert.Equal(new DateOnly(2026, 1, 5), Read(DateType.DateOnly, "05/01/2026", "en-GB").Value);
        Assert.Equal(new DateOnly(2026, 5, 1), Read(DateType.DateOnly, "05/01/2026", "en-US").Value);
        Assert.Equal(OperandRefusal.ReadsTwoWays, Read(DateType.DateOnly, "05/01/2026", "ja-JP").Refusal);
        Assert.Equal(new DateOnly(2026, 1, 5), Read(DateType.DateOnly, "5 January 2026", "en-GB").Value);
    }

    [Theory] // ADR-0023, principle 1: on a DateOnly column a time is refused, never cut to its day
    [InlineData("2026-10-02 13:00:00", "en-US")]
    [InlineData("2026-10-02 00:00:00", "en-US")]   // midnight is a time typed, not a day
    [InlineData("05/01/2026 09:05", "en-GB")]
    [InlineData("05/01/2026 00:00", "en-GB")]
    [InlineData("5 January 2026 00:00", "en-GB")]
    [InlineData("2026-10-02 13:00:00 +09:00", "ja-JP")]
    [InlineData("2026-10-02T13:00:00Z", "de-DE")]
    public void On_a_date_only_column_a_time_or_an_offset_is_refused(string typed, string culture)
    {
        var reading = Read(DateType.DateOnly, typed, culture);

        Assert.Equal(OperandRefusal.NotTheColumnsDateForm, reading.Refusal);
        Assert.Null(reading.Value);
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023: on a DateTime column a date and time read as a DateTime
    public void On_a_date_time_column_a_date_and_time_read_as_a_date_time(string culture)
    {
        Assert.Equal(new DateTime(2026, 10, 2), Read(DateType.DateTime, "2026-10-02", culture).Value);
        Assert.Equal(new DateTime(2026, 10, 2, 13, 0, 0), Read(DateType.DateTime, "2026-10-02 13:00:00", culture).Value);
    }

    [Theory] // ADR-0023, principle 1: on a DateTime column an offset is refused, never dropped or converted to the server's clock
    [InlineData("2026-10-02 13:00:00 +09:00", "en-US")]
    [InlineData("2026-10-02T13:00:00+09:00", "en-GB")]
    [InlineData("2026-10-02T04:00:00Z", "ja-JP")]
    [InlineData("10/2/2026 1:00 PM +09:00", "en-US")]
    public void On_a_date_time_column_an_offset_is_refused(string typed, string culture)
    {
        var reading = Read(DateType.DateTime, typed, culture);

        Assert.Equal(OperandRefusal.NotTheColumnsDateForm, reading.Refusal);
        Assert.Null(reading.Value);
    }

    [Theory] // ADR-0023, ISO 8601 / RFC 3339: on a DateTimeOffset column an explicit offset reads, in an ISO form or ticket 94's
    [InlineData("2026-10-02T13:00:00+09:00", 13, 9)]
    [InlineData("2026-10-02T13:00+09:00", 13, 9)]
    [InlineData("2026-10-02T04:00:00Z", 4, 0)]
    [InlineData("2026-10-02T04:00:00-05:30", 4, -5.5)]
    [InlineData("2026-10-02T13:00:00.000+09:00", 13, 9)]   // RFC 3339's fractional seconds
    [InlineData("2026-10-02 13:00:00 +09:00", 13, 9)]
    public void On_a_date_time_offset_column_an_explicit_offset_reads(string typed, int hour, double offsetHours)
    {
        foreach (var culture in new[] { "en-US", "en-GB", "de-DE", "ja-JP" })
        {
            var reading = Read(DateType.DateTimeOffset, typed, culture);

            Assert.Equal(OperandRefusal.None, reading.Refusal);
            var value = Assert.IsType<DateTimeOffset>(reading.Value);
            Assert.Equal(new DateTimeOffset(2026, 10, 2, hour, 0, 0, TimeSpan.FromHours(offsetHours)), value);
            Assert.Equal(TimeSpan.FromHours(offsetHours), value.Offset);
        }
    }

    [Theory] // ADR-0023, ISO 8601: a time with no offset names no instant, so on a DateTimeOffset column it is refused, never given one
    [InlineData("2026-10-02", "en-US")]
    [InlineData("2026-10-02 13:00:00", "ja-JP")]
    [InlineData("2026-10-02T13:00:00", "en-GB")]
    [InlineData("05/01/2026 09:05", "en-GB")]
    [InlineData("10/2/2026 1:00 PM +09:00", "en-US")]   // an offset outside the ISO forms is not read either
    public void On_a_date_time_offset_column_a_text_without_an_iso_offset_is_refused(string typed, string culture)
    {
        var reading = Read(DateType.DateTimeOffset, typed, culture);

        Assert.Equal(OperandRefusal.NotTheColumnsDateForm, reading.Refusal);
        Assert.Null(reading.Value);
    }

    [Theory] // ADR-0023: a text that is no date at all is still NotReadable, whatever the declared type
    [InlineData(DateType.DateTime)]
    [InlineData(DateType.DateOnly)]
    [InlineData(DateType.DateTimeOffset)]
    public void A_text_that_is_no_date_is_not_readable(DateType declared)
        => Assert.Equal(OperandRefusal.NotReadable, Read(declared, "abc").Refusal);

    [Fact] // ADR-0023: a refusal's words name the form to type
    public void The_refusal_names_the_form_to_type()
    {
        Assert.Equal("“2026-10-02 13:00:00” has a time, and this column holds days. Type the day alone, as 2026-10-02.",
            Words(DateType.DateOnly, "2026-10-02 13:00:00"));
        Assert.Equal("“2026-10-02 13:00:00 +09:00” has an offset, and this column holds days. Type the day alone, as 2026-10-02.",
            Words(DateType.DateOnly, "2026-10-02 13:00:00 +09:00"));
        Assert.Equal("“2026-10-02T13:00:00+09:00” has an offset, and this column's dates have none. Type it without one, as 2026-10-02 13:00:00.",
            Words(DateType.DateTime, "2026-10-02T13:00:00+09:00"));
        Assert.Equal("“2026-10-02 13:00:00” has no offset, and this column holds moments. Type it with one, as 2026-10-02T13:00:00+hh:mm, or with Z for UTC.",
            Words(DateType.DateTimeOffset, "2026-10-02 13:00:00"));
        Assert.Equal("“10/2/2026 1:00 PM +09:00” is not written in an ISO form. Type it as 2026-10-02T13:00:00+09:00.",
            Words(DateType.DateTimeOffset, "10/2/2026 1:00 PM +09:00"));
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0023, principle 1: a DateOnly or DateTimeOffset operand reopens in a text that reads back as itself
    public void A_declared_date_operand_reads_back_as_itself(string culture)
    {
        var c = CultureInfo.GetCultureInfo(culture);
        var day = new DateOnly(2026, 1, 5);
        var moment = new DateTimeOffset(2026, 5, 1, 21, 5, 7, Tokyo);

        Assert.Equal(day, FilterPanelChoices.ReadOperand(ColumnType.Date, FilterPanelChoices.OperandText(day, c), c, DateType.DateOnly).Value);
        var reread = FilterPanelChoices.ReadOperand(ColumnType.Date, FilterPanelChoices.OperandText(moment, c), c, DateType.DateTimeOffset).Value;
        Assert.Equal(moment, reread);
        Assert.Equal(Tokyo, ((DateTimeOffset)reread!).Offset);
    }
}
