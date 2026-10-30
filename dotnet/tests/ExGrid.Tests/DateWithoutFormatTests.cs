using System.Globalization;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0006's note of 2026-10-01 (ticket 94): without a <c>Format</c>, a date or a time shows in one
/// ISO form by its type, written in the invariant culture, so the same value reads the same under
/// every culture. A declared Format still wins, and numbers, text and booleans are unchanged.
/// </summary>
public class DateWithoutFormatTests
{
    private static readonly ColumnInfo<object> NoFormat = new("X", ColumnType.Date, v => v);

    private static readonly DateTime When = new(2026, 10, 1, 9, 5, 7, 250);

    private static readonly string[] CultureNames = ["en-US", "en-GB", "ja-JP"];

    public static TheoryData<string> Cultures() => [.. CultureNames];

    private static T Under<T>(string culture, Func<T> read)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try { return read(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi); }
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 (2026-10-01): each type has one ISO form, whatever the culture
    public void Each_date_and_time_type_shows_its_iso_form(string culture)
    {
        Assert.Equal("2026-10-01", Under(culture, () => NoFormat.TextFor(DateOnly.FromDateTime(When))));
        Assert.Equal("2026-10-01 09:05:07", Under(culture, () => NoFormat.TextFor(When)));
        Assert.Equal("2026-10-01 00:00:00", Under(culture, () => NoFormat.TextFor(When.Date)));
        Assert.Equal("2026-10-01 09:05:07 +09:00", Under(culture, () => NoFormat.TextFor(new DateTimeOffset(When, TimeSpan.FromHours(9)))));
        Assert.Equal("2026-10-01 09:05:07 -05:30", Under(culture, () => NoFormat.TextFor(new DateTimeOffset(When, new TimeSpan(-5, -30, 0)))));
        Assert.Equal("21:05:07", Under(culture, () => NoFormat.TextFor(TimeOnly.FromDateTime(When.AddHours(12)))));
    }

    [Fact] // ADR-0006 (2026-10-01): the text is the same under en-US, en-GB and ja-JP, where ToString() was not
    public void The_text_is_the_same_under_every_culture()
    {
        var cultures = CultureNames;
        object[] values = [When, DateOnly.FromDateTime(When), new DateTimeOffset(When, TimeSpan.FromHours(9)), TimeOnly.FromDateTime(When)];

        foreach (var value in values)
        {
            Assert.Single(cultures.Select(c => Under(c, () => NoFormat.TextFor(value))).Distinct());
        }
        // ToString() alone read three ways: 10/1/2026 9:05:07 AM, 01/10/2026 09:05:07, 2026/10/01 9:05:07.
        Assert.Equal(3, cultures.Select(c => Under(c, () => When.ToString())).Distinct().Count());
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006: a declared Format still wins over the ISO form
    public void A_declared_format_still_wins(string culture)
    {
        var dayOnly = new ColumnInfo<object>("X", ColumnType.Date, v => v, Format: v => ((DateTime)v).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var local = new ColumnInfo<object>("X", ColumnType.Date, v => v, Format: v => ((DateTime)v).ToString("d", CultureInfo.CurrentCulture));

        Assert.Equal("2026-10-01", Under(culture, () => dayOnly.TextFor(When)));
        Assert.Equal(Under(culture, () => When.ToString("d", CultureInfo.CurrentCulture)), Under(culture, () => local.TextFor(When)));
    }

    [Theory, MemberData(nameof(Cultures))] // ADR-0006 (2026-10-01): numbers, text and booleans are unchanged — their own ToString()
    public void Numbers_text_and_booleans_are_unchanged(string culture)
    {
        Assert.Equal(Under(culture, () => 1234.5m.ToString()), Under(culture, () => NoFormat.TextFor(1234.5m)));
        Assert.Equal(Under(culture, () => 0.25d.ToString()), Under(culture, () => NoFormat.TextFor(0.25d)));
        Assert.Equal("Alpha", Under(culture, () => NoFormat.TextFor("Alpha")));
        Assert.Equal(true.ToString(), Under(culture, () => NoFormat.TextFor(true)));
        Assert.Equal("", NoFormat.TextFor(null));
    }
}
