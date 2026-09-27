using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

public class CultureTests
{
    private static Sheet In(string culture) => new(CultureInfo.GetCultureInfo(culture));

    private static double Serial(int year, int month, int day) =>
        (new DateTime(year, month, day) - new DateTime(1899, 12, 30)).TotalDays;

    [Theory] // ADR-0048 (SH-11): a typed number is read with the Sheet's culture's separators
    [InlineData("en-US", "1,234.5", 1234.5)]
    [InlineData("en-US", "1234.5", 1234.5)]
    [InlineData("en-US", "12,345,678", 12_345_678)]
    [InlineData("en-US", "-7", -7)]
    [InlineData("en-US", "(7)", -7)]
    [InlineData("en-US", "1E3", 1000)]
    [InlineData("en-US", ".5", 0.5)]
    [InlineData("de-DE", "1.234,5", 1234.5)]
    [InlineData("de-DE", "1,5", 1.5)]
    [InlineData("ja-JP", "1,234.5", 1234.5)]
    public void A_typed_number_is_read_under_the_culture(string culture, string typed, double expected)
    {
        var sheet = In(culture);

        sheet.Enter(CellAddress.Parse("A1"), typed);

        Assert.Equal(expected, sheet.GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }

    [Fact] // ADR-0048 (SH-11): 1,234.5 under en-US is recorded as the number, not as the text typed
    public void A_constant_is_recorded_parsed()
    {
        var sheet = In("en-US");

        sheet.Enter(CellAddress.Parse("A1"), "1,234.5");

        var entry = sheet.GetEntry(CellAddress.Parse("A1"))!;
        Assert.Equal(1234.5, entry.Constant!.Value.Number);
        Assert.Contains("\"number\":1234.5", sheet.ToDocument().ToJson());
        Assert.Equal("1234.5", sheet.GetEntryText(CellAddress.Parse("A1")));
    }

    [Fact] // ADR-0048 (SH-11): 2026/9/26 under ja-JP is that date's serial number, shown as a date
    public void A_typed_date_under_ja_jp_is_a_serial()
    {
        var sheet = In("ja-JP");
        var a1 = CellAddress.Parse("A1");

        sheet.Enter(a1, "2026/9/26");

        Assert.Equal(Serial(2026, 9, 26), sheet.GetValue(a1)!.Value.Number);
        Assert.True(sheet.GetFormat(a1).IsDate);
        Assert.True(sheet.GetDisplay(a1).IsNumber);
    }

    [Theory] // ADR-0048: a date is typed in the culture's day order
    [InlineData("en-US", "9/26/2026")]
    [InlineData("en-US", "9-26-2026")]
    [InlineData("en-US", "2026-09-26")]
    [InlineData("en-US", "2026/9/26")]
    [InlineData("en-US", "9/26/26")]
    [InlineData("de-DE", "26.9.2026")]
    [InlineData("de-DE", "26.09.2026")]
    [InlineData("de-DE", "2026-09-26")]
    [InlineData("ja-JP", "2026/9/26")]
    [InlineData("ja-JP", "2026-9-26")]
    [InlineData("en-GB", "26/9/2026")]
    public void A_date_is_read_in_the_cultures_order(string culture, string typed)
    {
        var sheet = In(culture);

        sheet.Enter(CellAddress.Parse("A1"), typed);

        Assert.Equal(Serial(2026, 9, 26), sheet.GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }

    [Fact] // ADR-0048: a two-digit year is 2000–2029 for 00–29 and 1930–1999 for 30–99, Excel's default window
    public void A_two_digit_year_follows_excels_window()
    {
        var sheet = In("en-US");

        sheet.Enter(CellAddress.Parse("A1"), "1/1/29");
        sheet.Enter(CellAddress.Parse("A2"), "1/1/30");

        Assert.Equal(Serial(2029, 1, 1), sheet.GetValue(CellAddress.Parse("A1"))!.Value.Number);
        Assert.Equal(Serial(1930, 1, 1), sheet.GetValue(CellAddress.Parse("A2"))!.Value.Number);
    }

    [Theory] // ADR-0048: a day that does not exist is not a date; it stays the text typed
    [InlineData("2/30/2026")]
    [InlineData("13/1/2026")]
    [InlineData("2/29/2025")]
    [InlineData("12/31/1899")]
    public void An_impossible_date_is_text(string typed)
    {
        var sheet = In("en-US");

        sheet.Enter(CellAddress.Parse("A1"), typed);

        Assert.Equal(ValueKind.Text, sheet.GetValue(CellAddress.Parse("A1"))!.Value.Kind);
    }

    [Fact] // ADR-0048: a time is a fraction of a day, and a date with a time is both
    public void A_typed_time_is_a_fraction_of_a_day()
    {
        var sheet = In("en-US");

        sheet.Enter(CellAddress.Parse("A1"), "13:45");
        sheet.Enter(CellAddress.Parse("A2"), "6:00 PM");
        sheet.Enter(CellAddress.Parse("A3"), "9/26/2026 12:00");

        Assert.Equal((13 * 60 + 45) / 1440.0, sheet.GetValue(CellAddress.Parse("A1"))!.Value.Number, 12);
        Assert.Equal(0.75, sheet.GetValue(CellAddress.Parse("A2"))!.Value.Number, 12);
        Assert.Equal(Serial(2026, 9, 26) + 0.5, sheet.GetValue(CellAddress.Parse("A3"))!.Value.Number, 9);
        Assert.Equal("13:45", sheet.GetDisplay(CellAddress.Parse("A1")).Text);
        Assert.Equal("6:00 PM", sheet.GetDisplay(CellAddress.Parse("A2")).Text);
    }

    [Fact] // ADR-0047: Excel keeps 15 significant digits of what is typed; the rest become zeros
    public void Digits_past_the_fifteenth_become_zeros()
    {
        var sheet = In("en-US");

        sheet.Enter(CellAddress.Parse("A1"), "1234567890123456789");

        Assert.Equal(1234567890123450000d, sheet.GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }

    [Fact] // ADR-0048: a percentage typed is the fraction, shown as a percentage
    public void A_typed_percentage_is_a_fraction()
    {
        var sheet = In("en-US");
        var a1 = CellAddress.Parse("A1");

        sheet.Enter(a1, "50%");

        Assert.Equal(0.5, sheet.GetValue(a1)!.Value.Number);
        Assert.Equal("50%", sheet.GetDisplay(a1).Text);
        Assert.Equal("50%", sheet.GetEntryText(a1));
    }

    [Fact] // ADR-0048: the Cell Editor reopens a date as the culture's short date, which types back to the same Entry
    public void A_date_reopens_as_the_cultures_short_date()
    {
        var sheet = In("en-US");
        var a1 = CellAddress.Parse("A1");
        sheet.Enter(a1, "2026-09-26");
        var entry = sheet.GetEntry(a1);

        var text = sheet.GetEntryText(a1);
        sheet.Enter(a1, text);

        Assert.Equal("9/26/2026", text);
        Assert.Equal(entry, sheet.GetEntry(a1));
    }

    [Theory] // ADR-0048: every constant's editor text types back to the same Entry, in several cultures
    [InlineData("en-US", "1,234.5")]
    [InlineData("en-US", "9/26/2026 1:45 PM")]
    [InlineData("en-US", "0.000123")]
    [InlineData("en-US", "12.5%")]
    [InlineData("de-DE", "1.234,5")]
    [InlineData("de-DE", "26.9.2026")]
    [InlineData("ja-JP", "2026/9/26")]
    [InlineData("ja-JP", "13:45:10")]
    [InlineData("en-US", "'1,234")]
    public void The_entry_text_types_back_to_the_same_entry(string culture, string typed)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");
        sheet.Enter(a1, typed);
        var entry = sheet.GetEntry(a1);

        sheet.Enter(a1, sheet.GetEntryText(a1));

        Assert.Equal(entry, sheet.GetEntry(a1));
    }

    [Fact] // ADR-0048 (SH-11): a document saved under en-US and opened under de-DE shows the same numbers
    public void A_document_opened_under_another_culture_keeps_its_numbers()
    {
        var sheet = In("en-US");
        sheet.Enter(CellAddress.Parse("A1"), "1,234.5");
        sheet.Enter(CellAddress.Parse("A2"), "9/26/2026");
        sheet.Enter(CellAddress.Parse("A3"), "=A1*2");
        sheet.Enter(CellAddress.Parse("A4"), "=\"1,5\"+1");
        var json = sheet.ToDocument().ToJson();

        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var reopened = Sheet.Open(SheetDocument.FromJson(json));

            Assert.Equal("en-US", reopened.Culture.Name);
            foreach (var address in sheet.EntryAddresses)
            {
                Assert.Equal(sheet.GetValue(address), reopened.GetValue(address));
                Assert.Equal(sheet.GetDisplay(address), reopened.GetDisplay(address));
            }
            Assert.Equal(1234.5, reopened.GetValue(CellAddress.Parse("A1"))!.Value.Number);
            Assert.Equal(2469, reopened.GetValue(CellAddress.Parse("A3"))!.Value.Number);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact] // ADR-0048: the culture is recorded in the document, because it is part of what the Entries meant
    public void The_culture_is_recorded()
    {
        var sheet = In("de-DE");
        sheet.Enter(CellAddress.Parse("A1"), "1,5");

        var document = SheetDocument.FromJson(sheet.ToDocument().ToJson());
        var reopened = Sheet.Open(document);

        Assert.Equal("de-DE", document.Culture);
        Assert.Equal(1.5, reopened.GetValue(CellAddress.Parse("A1"))!.Value.Number);
        Assert.Equal("1,5", reopened.GetDisplay(CellAddress.Parse("A1")).Text);
    }

    [Fact] // ADR-0048: numeric text in arithmetic is read as a typed number would be, under the Sheet's culture
    public void Numeric_text_in_arithmetic_follows_the_culture()
    {
        var english = In("en-US");
        var german = In("de-DE");

        english.Enter(CellAddress.Parse("A1"), "=\"1,000\"+1");
        german.Enter(CellAddress.Parse("A1"), "=\"1,5\"+1");

        Assert.Equal(1001, english.GetValue(CellAddress.Parse("A1"))!.Value.Number);
        Assert.Equal(2.5, german.GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }
}
