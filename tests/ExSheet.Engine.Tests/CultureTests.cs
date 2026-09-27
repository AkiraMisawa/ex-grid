using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

public class CultureTests
{
    private static Sheet In(string culture) => new(CultureInfo.GetCultureInfo(culture));

    private static double Serial(int year, int month, int day) =>
        (new DateTime(year, month, day) - new DateTime(1899, 12, 30)).TotalDays;

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

    [Theory] // ADR-0047/0048: a day and a month name without a year is that day of the current year, as Excel reads it (TYPED-022)
    [InlineData("en-US", "26-Sep")]
    [InlineData("en-US", "Sep 26")]
    [InlineData("en-US", "9/26")]
    [InlineData("de-DE", "26-Sep")]
    public void A_date_typed_without_a_year_is_in_the_current_year(string culture, string typed)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");

        sheet.Enter(a1, typed);

        Assert.Equal(Serial(DateTime.Today.Year, 9, 26), sheet.GetValue(a1)!.Value.Number);
        Assert.Equal("d-mmm", sheet.GetFormat(a1).Code);
    }

    [Fact] // ADR-0047: a typed date records Excel's built-in short date, which shows in each culture's own pattern
    public void The_built_in_short_date_shows_in_the_sheets_culture()
    {
        var shown = new[] { "en-US", "en-GB", "de-DE", "ja-JP" }.Select(culture =>
        {
            var sheet = In(culture);
            var a1 = CellAddress.Parse("A1");
            sheet.Enter(a1, "=46291");
            sheet.SetFormat(a1, NumberFormat.Parse("m/d/yyyy"));
            return sheet.GetDisplay(a1).Text;
        });

        Assert.Equal(["9/26/2026", "26/09/2026", "26.09.2026", "2026/09/26"], shown);
    }

    [Fact] // ADR-0048: a percentage typed reopens in the Cell Editor as a percentage (its Value is in ExcelCases/typed-constants.json)
    public void A_typed_percentage_reopens_as_a_percentage()
    {
        var sheet = In("en-US");
        var a1 = CellAddress.Parse("A1");

        sheet.Enter(a1, "50%");

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
    [InlineData("en-US", "'+A1")]    // text that typed without its apostrophe would be a Formula
    [InlineData("en-US", "'-abc")]
    [InlineData("en-US", "26-Sep")]
    [InlineData("en-US", "1E15")]    // the editor writes it in full, 1000000000000000
    [InlineData("en-US", "1E-10")]
    [InlineData("de-DE", "26-Okt")]
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
}
