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
        Assert.True(sheet.GetNumberFormat(a1).IsDate);
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
        Assert.Equal("d-mmm", sheet.GetNumberFormat(a1).Code);
    }

    [Fact] // ADR-0047: a typed date records Excel's built-in short date, which shows in each culture's own pattern
    public void The_built_in_short_date_shows_in_the_sheets_culture()
    {
        var shown = new[] { "en-US", "en-GB", "de-DE", "ja-JP" }.Select(culture =>
        {
            var sheet = In(culture);
            var a1 = CellAddress.Parse("A1");
            sheet.Enter(a1, "=46291");
            sheet.SetNumberFormat(a1, NumberFormat.Parse("m/d/yyyy"));
            return sheet.GetDisplay(a1).Text;
        });

        Assert.Equal(["9/26/2026", "26/09/2026", "26.09.2026", "2026/09/26"], shown);
    }

    [Theory] // ADR-0071, SH-42 (the eleventh Windows run, case 20): Ctrl+Shift+$ records Excel's built-in currency, 8 or 6 as the culture's currency has decimals
    [InlineData("en-US", "$#,##0.00_);[Red]($#,##0.00)")]
    [InlineData("en-GB", "$#,##0.00_);[Red]($#,##0.00)")]
    [InlineData("ja-JP", "$#,##0_);[Red]($#,##0)")]
    public void The_built_in_currency_is_recorded_in_its_invariant_code(string culture, string code)
    {
        Assert.Equal(code, NumberFormat.BuiltInCurrency(CultureInfo.GetCultureInfo(culture)).Code);
    }

    [Theory] // ADR-0071, SH-42 (case 20): the built-in currency shows in the Sheet culture's own currency, as the built-in short date shows in its date, its negative section red
    [InlineData("en-US", 1234.5, "$1,234.50 ", null)]
    [InlineData("en-US", -1234.5, "($1,234.50)", NumberFormatColour.Red)]
    [InlineData("en-GB", 1234.5, "£1,234.50", null)]
    [InlineData("en-GB", -1234.5, "-£1,234.50", NumberFormatColour.Red)]
    [InlineData("ja-JP", 1234.5, "¥1,235", null)]
    [InlineData("ja-JP", -1234.5, "-¥1,235", NumberFormatColour.Red)]
    public void The_built_in_currency_shows_in_the_sheets_culture(string culture, double number, string shown, NumberFormatColour? colour)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");
        sheet.Enter(a1, "=" + number.ToString(CultureInfo.InvariantCulture));
        var currency = NumberFormat.BuiltInCurrency(sheet.Culture);

        sheet.SetNumberFormat(a1, currency);

        Assert.Equal(shown, sheet.GetDisplay(a1).Text);
        Assert.Equal(colour, sheet.GetDisplay(a1).Colour);
        // Recorded as the built-in, not as the form it shows in.
        Assert.Equal(currency.Code, sheet.GetNumberFormat(a1).Code);
        Assert.Contains(JsonEncoded(currency.Code), sheet.ToDocument().ToJson(), StringComparison.Ordinal);
    }

    [Fact] // ADR-0071: a currency whose symbol is letters is quoted, so it shows rather than being read as format codes
    public void A_lettered_currency_symbol_shows_as_text()
    {
        var sheet = In("sv-SE");
        var a1 = CellAddress.Parse("A1");
        sheet.Enter(a1, "=5");

        sheet.SetNumberFormat(a1, NumberFormat.BuiltInCurrency(sheet.Culture));

        Assert.Contains("kr", sheet.GetDisplay(a1).Text, StringComparison.Ordinal);
        Assert.Contains("5", sheet.GetDisplay(a1).Text, StringComparison.Ordinal);
    }

    [Theory] // ADR-0071 case 19 (the twelfth Windows run): Excel's built-ins 15 and 20 show in the Sheet culture's own form, and the AM/PM built-in as it is spelled
    [InlineData("en-GB", "d-mmm-yy", "05-Jan-26")]
    [InlineData("en-US", "d-mmm-yy", "5-Jan-26")]
    [InlineData("ja-JP", "d-mmm-yy", "05-1-26")]   // mmm is the month's number there (the fourteenth run, case 10)
    [InlineData("en-GB", "h:mm", "09:05")]
    [InlineData("en-US", "h:mm", "9:05")]
    [InlineData("ja-JP", "h:mm", "9:05")]
    [InlineData("en-US", "h:mm AM/PM", "9:05 AM")]
    public void The_built_in_date_and_time_show_in_the_sheets_culture(string culture, string code, string shown)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");
        // 5 January 2026 at 09:05: the date reads the day, the times the time of day.
        sheet.Enter(a1, "=" + (Serial(2026, 1, 5) + 545.0 / 1440).ToString(CultureInfo.InvariantCulture));

        sheet.SetNumberFormat(a1, NumberFormat.Parse(code));

        Assert.Equal(shown, sheet.GetDisplay(a1).Text);
        // Recorded as the built-in, not as the form it shows in.
        Assert.Equal(code, sheet.GetNumberFormat(a1).Code);
        Assert.Contains($"\"{JsonEncoded(code)}\"", sheet.ToDocument().ToJson(), StringComparison.Ordinal);
    }

    [Fact] // ADR-0071 case 19: the built-ins open under another culture in that culture's form, as Excel's do
    public void The_built_in_date_and_time_follow_the_culture_a_document_is_opened_in()
    {
        var sheet = In("en-US");
        sheet.Enter(CellAddress.Parse("A1"), "=" + Serial(2026, 1, 5).ToString(CultureInfo.InvariantCulture));
        sheet.Enter(CellAddress.Parse("A2"), "=" + (545.0 / 1440).ToString(CultureInfo.InvariantCulture));
        sheet.SetNumberFormat(CellAddress.Parse("A1"), NumberFormat.Parse("d-mmm-yy"));
        sheet.SetNumberFormat(CellAddress.Parse("A2"), NumberFormat.Parse("h:mm"));
        var json = sheet.ToDocument().ToJson();

        var british = Sheet.Open(SheetDocument.FromJson(json.Replace("\"culture\":\"en-US\"", "\"culture\":\"en-GB\"", StringComparison.Ordinal)));

        Assert.Equal("en-GB", british.Culture.Name);
        Assert.Equal("5-Jan-26", sheet.GetDisplay(CellAddress.Parse("A1")).Text);
        Assert.Equal("05-Jan-26", british.GetDisplay(CellAddress.Parse("A1")).Text);
        Assert.Equal("09:05", british.GetDisplay(CellAddress.Parse("A2")).Text);
    }

    [Theory] // ADR-0071 case 19, ADR-0047: a date typed with a month name and a year records built-in 15, so it too shows in the culture's form
    [InlineData("en-GB", "05-Jan-26")]
    [InlineData("en-US", "5-Jan-26")]
    public void A_date_typed_with_a_month_name_shows_built_in_15_in_the_cultures_form(string culture, string shown)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");

        sheet.Enter(a1, "5-Jan-2026");

        Assert.Equal("d-mmm-yy", sheet.GetNumberFormat(a1).Code);
        Assert.Equal(shown, sheet.GetDisplay(a1).Text);
    }

    [Theory] // ADR-0071 case 17, ADR-0047: the width a number needs is read in the culture's form of its built-in, so a key widens its column to the text it shows
    [InlineData("en-GB", "d-mmm-yy", 9)]   // 05-Jan-26
    [InlineData("en-US", "d-mmm-yy", 8)]   // 5-Jan-26
    [InlineData("en-GB", "h:mm", 5)]       // 09:05
    [InlineData("ja-JP", "h:mm", 4)]       // 9:05
    public void The_width_a_built_in_needs_is_its_text_in_the_culture(string culture, string code, int characters)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");
        sheet.Enter(a1, "=" + (Serial(2026, 1, 5) + 545.0 / 1440).ToString(CultureInfo.InvariantCulture));

        sheet.SetNumberFormat(a1, NumberFormat.Parse(code));

        Assert.Equal(characters, sheet.GetWidthOnEntry(a1));
    }

    private static string JsonEncoded(string text) => System.Text.Json.JsonSerializer.Serialize(text)[1..^1];

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

    [Fact] // ADR-0071 (the eleventh Windows run, case 20): ja-JP's symbol is Windows' yen sign, U+00A5, whatever ICU data the machine holds
    public void The_ja_JP_currency_symbol_is_the_yen_sign_on_every_platform()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ja-JP").Clone();
        culture.NumberFormat.CurrencySymbol = "\uFFE5";

        Assert.Equal("\u00A5", NumberFormat.CurrencySymbolOf(culture));
    }

    [Fact] // ADR-0071 (the twelfth Windows run, case 19): September abbreviates as Windows writes it, Sep, whatever ICU data the machine holds
    public void September_abbreviates_as_Sep_on_every_platform()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-GB").Clone();
        var names = (string[])culture.DateTimeFormat.AbbreviatedMonthNames.Clone();
        names[8] = "Sept";
        culture.DateTimeFormat.AbbreviatedMonthNames = names;

        Assert.Equal("Sep", NumberFormat.AbbreviatedMonthNamesOf(culture)[8]);
        Assert.Equal("Jan", NumberFormat.AbbreviatedMonthNamesOf(culture)[0]);
    }

    // ---- mmm under ja-JP, and a code typed into Format Cells (the fourteenth Windows run, cases 10 and 11) ----

    /// <summary>5 January 2026, as the run's cells held it.</summary>
    private static Sheet FifthOfJanuaryIn(string culture)
    {
        var sheet = In(culture);
        sheet.Enter(CellAddress.Parse("A1"), "=" + Serial(2026, 1, 5).ToString(CultureInfo.InvariantCulture));
        return sheet;
    }

    private static string Shown(Sheet sheet, NumberFormat format)
    {
        var a1 = CellAddress.Parse("A1");
        sheet.SetNumberFormat(a1, format);
        return sheet.GetDisplay(a1).Text;
    }

    [Theory] // ADR-0071, case 14-10: under ja-JP mmm is the month as a number with no leading zero in every code, and mmmm is 1月 — Windows' month names, which Excel shows
    [InlineData("dd-mmm-yy", "05-1-26")]
    [InlineData("d-mmm-yy", "05-1-26")]      // built-in 15, set in the invariant codes, shows in ja-JP's form
    [InlineData("mmm d, yyyy", "1 5, 2026")]
    [InlineData("mmmm", "1月")]
    [InlineData("yyyy/mmm/dd", "2026/1/05")]
    public void Under_ja_jp_mmm_is_the_month_as_a_number_case_14_10(string code, string shown)
    {
        Assert.Equal(shown, Shown(FifthOfJanuaryIn("ja-JP"), NumberFormat.Parse(code)));
    }

    [Theory] // ADR-0071, case 14-10: en-GB and en-US keep their month names
    [InlineData("en-GB", "d-mmm-yy", "05-Jan-26")]
    [InlineData("en-US", "d-mmm-yy", "5-Jan-26")]
    [InlineData("en-GB", "mmm d, yyyy", "Jan 5, 2026")]
    [InlineData("en-US", "mmmm", "January")]
    public void En_gb_and_en_us_keep_their_month_names_case_14_10(string culture, string code, string shown)
    {
        Assert.Equal(shown, Shown(FifthOfJanuaryIn(culture), NumberFormat.Parse(code)));
    }

    [Fact] // ADR-0071, case 14-10: a Japanese month abbreviates as Windows writes it, the number alone, whatever ICU data the machine holds
    public void A_japanese_month_abbreviates_as_its_number_on_every_platform()
    {
        var icu = (CultureInfo)CultureInfo.GetCultureInfo("ja-JP").Clone();
        icu.DateTimeFormat.AbbreviatedMonthNames = [.. Enumerable.Range(1, 12).Select(m => $"{m}月"), ""];
        var windows = (CultureInfo)CultureInfo.GetCultureInfo("ja-JP").Clone();
        windows.DateTimeFormat.AbbreviatedMonthNames = [.. Enumerable.Range(1, 12).Select(m => $"{m}"), ""];

        Assert.Equal("1", NumberFormat.AbbreviatedMonthNamesOf(icu)[0]);
        Assert.Equal("12", NumberFormat.AbbreviatedMonthNamesOf(icu)[11]);
        Assert.Equal("1", NumberFormat.AbbreviatedMonthNamesOf(windows)[0]);
        Assert.Equal("1", NumberFormat.AbbreviatedMonthNamesOf(CultureInfo.GetCultureInfo("ja-JP"))[0]);
        Assert.Equal("Jan", NumberFormat.AbbreviatedMonthNamesOf(CultureInfo.GetCultureInfo("en-GB"))[0]);
    }

    [Theory] // ADR-0071, case 14-11: a code typed into Format Cells is a built-in only when it spells that built-in's code under the Sheet's culture — under ja-JP dd-mmm-yy is built-in 15 and shows 05-1-26, and d-mmm-yy is a code of its own and shows 5-1-26
    [InlineData("ja-JP", "dd-mmm-yy", "d-mmm-yy", "05-1-26")]
    [InlineData("ja-JP", "d-mmm-yy", null, "5-1-26")]
    [InlineData("ja-JP", "DD-MMM-YY", "d-mmm-yy", "05-1-26")]
    [InlineData("en-US", "d-mmm-yy", "d-mmm-yy", "5-Jan-26")]
    [InlineData("en-GB", "dd-mmm-yy", "d-mmm-yy", "05-Jan-26")]
    [InlineData("en-GB", "d-mmm-yy", null, "5-Jan-26")]
    public void A_typed_code_is_a_built_in_only_where_it_spells_it_case_14_11(string culture, string typed, string? builtIn, string shown)
    {
        Assert.True(NumberFormat.TryParseLocal(typed, CultureInfo.GetCultureInfo(culture), out var format, out _));

        if (builtIn is not null) Assert.Equal(builtIn, format.Code);
        else Assert.NotEqual("d-mmm-yy", format.Code, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(shown, Shown(FifthOfJanuaryIn(culture), format));
    }

    [Theory] // ADR-0071, case 14-11: the same rule holds for every built-in ExSheet shows in the culture's own form — each local spelling is the built-in, and its invariant code, where the culture spells it otherwise, is a code of its own shown as it is spelled
    [InlineData("en-GB", "dd/mm/yyyy", "m/d/yyyy", "05/01/2026", "1/5/2026")]
    [InlineData("ja-JP", "yyyy/mm/dd", "m/d/yyyy", "2026/01/05", "1/5/2026")]
    [InlineData("en-GB", "dd/mm/yyyy h:mm", "m/d/yyyy h:mm", "05/01/2026 9:05", "1/5/2026 9:05")]
    [InlineData("en-GB", "dd-mmm", "d-mmm", "05-Jan", "5-Jan")]
    [InlineData("en-GB", "hh:mm", "h:mm", "09:05", "9:05")]
    [InlineData("en-GB", "£#,##0.00;[Red]-£#,##0.00", "$#,##0.00_);[Red]($#,##0.00)", "£46,027.38", "$46,027.38 ")]
    [InlineData("ja-JP", "¥#,##0;[Red]-¥#,##0", "$#,##0_);[Red]($#,##0)", "¥46,027", "$46,027 ")]
    public void Every_localised_built_in_follows_the_rule_case_14_11(string culture, string local, string invariant, string asBuiltIn, string asOwn)
    {
        var info = CultureInfo.GetCultureInfo(culture);

        Assert.True(NumberFormat.TryParseLocal(local, info, out var builtIn, out _));
        Assert.True(NumberFormat.TryParseLocal(invariant, info, out var own, out _));

        Assert.Equal(invariant, builtIn.Code);
        Assert.Equal(local, builtIn.LocalCode(info));
        Assert.NotEqual(invariant, own.Code);
        Assert.Equal(invariant, own.LocalCode(info));
        // 5 January 2026 at 09:05, or 46,027.38 for a currency.
        var sheet = In(culture);
        sheet.Enter(CellAddress.Parse("A1"), "=" + (Serial(2026, 1, 5) + 545.0 / 1440).ToString(CultureInfo.InvariantCulture));
        Assert.Equal(asBuiltIn, Shown(sheet, builtIn));
        Assert.Equal(asOwn, Shown(sheet, own));
        // Each reads back as itself from the way the culture spells it.
        Assert.True(NumberFormat.TryParseLocal(builtIn.LocalCode(info), info, out var again, out _));
        Assert.Equal(builtIn, again);
        Assert.True(NumberFormat.TryParseLocal(own.LocalCode(info), info, out again, out _));
        Assert.Equal(own, again);
    }

    [Fact] // ADR-0071, case 14-11 / ADR-0048: a code of its own that spells a built-in is kept apart from it in the Sheet Document, and shows as spelled when reopened
    public void A_code_of_its_own_survives_the_sheet_document_case_14_11()
    {
        var sheet = FifthOfJanuaryIn("ja-JP");
        Assert.True(NumberFormat.TryParseLocal("d-mmm-yy", sheet.Culture, out var own, out _));
        sheet.SetNumberFormat(CellAddress.Parse("A1"), own);

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));

        Assert.Equal(own, reopened.GetNumberFormat(CellAddress.Parse("A1")));
        Assert.Equal("5-1-26", reopened.GetDisplay(CellAddress.Parse("A1")).Text);
    }

    [Fact] // ADR-0071, case 14-11: a code that spells no built-in is read as it is written, and a code ExSheet does not read is refused by name
    public void Any_other_typed_code_is_read_as_written()
    {
        var japanese = CultureInfo.GetCultureInfo("ja-JP");

        Assert.True(NumberFormat.TryParseLocal("#,##0.00", japanese, out var format, out _));
        Assert.Equal("#,##0.00", format.Code);
        Assert.True(NumberFormat.TryParseLocal("General", japanese, out format, out _));
        Assert.True(format.IsGeneral);
        Assert.False(NumberFormat.TryParseLocal("[<0]0", japanese, out _, out var reason));
        Assert.NotNull(reason);
    }
}
