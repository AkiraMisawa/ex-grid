using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What a Sheet's culture reads and shows (ADR-0047, ADR-0048, ADR-0071) is in the shared cases
/// (<c>tests/cases/sheet/culture.json</c>). What stays here: a date typed without a year, which takes the year from the
/// machine's clock, which a case does not set; Format Cells' reading of a typed code (<see cref="NumberFormat.TryParseLocal"/>)
/// and the built-in currency's code, which no Sheet Edit makes; sv-SE, a culture the Script Core holds no data for; and
/// the corrections of a machine's ICU data, which only a .NET culture carries.
/// </summary>
public class CultureTests
{
    private static Sheet In(string culture) => new(CultureInfo.GetCultureInfo(culture));

    private static double Serial(int year, int month, int day) =>
        (new DateTime(year, month, day) - new DateTime(1899, 12, 30)).TotalDays;

    [Theory] // ADR-0047/0048: a day and a month name without a year is that day of the current year, as Excel reads it (TYPED-022)
    [InlineData("en-US", "26-Sep")]
    [InlineData("en-US", "Sep 26")]
    [InlineData("en-US", "9/26")]
    [InlineData("de-DE", "26-Sep")]
    public void A_date_typed_without_a_year_is_in_the_current_year(string culture, string typed)
    {
        var sheet = In(culture);
        var a1 = CellAddress.Parse("A1");

        // The year is read on both sides of the entry, so a run across New Year's midnight holds either way.
        var before = DateTime.Today.Year;
        sheet.Enter(a1, typed);
        var after = DateTime.Today.Year;

        Assert.Contains(sheet.GetValue(a1)!.Value.Number, new[] { Serial(before, 9, 26), Serial(after, 9, 26) });
        Assert.Equal("d-mmm", sheet.GetNumberFormat(a1).Code);
    }

    [Theory] // ADR-0071, SH-42 (the eleventh Windows run, case 20): Ctrl+Shift+$ records Excel's built-in currency, 8 or 6 as the culture's currency has decimals
    [InlineData("en-US", "$#,##0.00_);[Red]($#,##0.00)")]
    [InlineData("en-GB", "$#,##0.00_);[Red]($#,##0.00)")]
    [InlineData("ja-JP", "$#,##0_);[Red]($#,##0)")]
    public void The_built_in_currency_is_recorded_in_its_invariant_code(string culture, string code)
    {
        Assert.Equal(code, NumberFormat.BuiltInCurrency(CultureInfo.GetCultureInfo(culture)).Code);
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

    // ---- Codes typed into Custom, asked of Excel (the seventeenth Windows run, h9) ----

    /// <summary>The date and time built-ins ExSheet shows in the culture's own form (14, 22, 16, 15 and 20), by their invariant codes.</summary>
    private static readonly NumberFormat[] DateAndTimeBuiltIns =
        [NumberFormat.ShortDate, NumberFormat.ShortDateTime, NumberFormat.Parse("d-mmm"), NumberFormat.Parse("d-mmm-yy"), NumberFormat.Parse("h:mm")];

    [Theory] // ADR-0071, ticket 103 (observed in Excel, the seventeenth Windows run, h9): codes typed into Custom show as Excel showed them on 12 October 2026 13:30 (46307.5625) and on 13:30 alone (0.5625) — under en-GB d-mmm-yy, m/d/yyyy and h:mm, and under ja-JP mmmmm, the first character of 10月 and of 1月. d-mmm-yy and m/d/yyyy are observed as codes of their own. d-mmm-yy showed 0-Jan-00 on 13:30 alone, where built-in 15, which Excel spells dd-mmm-yy under en-GB (the twelfth run's case 19), would show 00-Jan-00 by that code. m/d/yyyy put the month first, 10/12/2026 and 1/0/1900, where built-in 14 shows the day first under en-GB (26/09/2026, the second Windows run). h:mm's 13:30 does not settle it, since built-in 20 (hh:mm) would show the same: there h9 leaves it ExSheet's reading, and R7 below observes it
    [InlineData("en-GB", "d-mmm-yy", 46307.5625, "12-Oct-26")]
    [InlineData("en-GB", "d-mmm-yy", 0.5625, "0-Jan-00")]
    [InlineData("en-GB", "m/d/yyyy", 46307.5625, "10/12/2026")]
    [InlineData("en-GB", "m/d/yyyy", 0.5625, "1/0/1900")]
    [InlineData("en-GB", "h:mm", 46307.5625, "13:30")]
    [InlineData("en-GB", "h:mm", 0.5625, "13:30")]
    [InlineData("ja-JP", "mmmmm", 46307.5625, "1")]
    [InlineData("ja-JP", "mmmmm", 0.5625, "1")]
    public void A_code_typed_into_custom_shows_as_excel_showed_it_run_17_h9(string culture, string typed, double value, string shown)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        var sheet = In(culture);
        sheet.Enter(CellAddress.Parse("A1"), "=" + value.ToString(CultureInfo.InvariantCulture));

        // Read as Format Cells' Custom box reads it.
        Assert.True(NumberFormat.TryParseLocal(typed, info, out var format, out _));

        // A code of its own, no built-in's, which Format Cells spells back as it was typed.
        Assert.DoesNotContain(format, DateAndTimeBuiltIns);
        Assert.Equal(typed, format.LocalCode(info));
        Assert.Equal(shown, Shown(sheet, format));
    }

    [Theory] // ADR-0071, ticket 103 (observed in Excel, the readings of 2026-10-12 (R7)): under en-GB, h:mm typed into Custom shows 9:05 on 0.37847222, not built-in 20's 09:05, and d-mmm-yy shows 5-Jan-26 on 46027, not built-in 15's 05-Jan-26: each is a code of its own
    [InlineData("h:mm", 0.37847222, "9:05")]
    [InlineData("d-mmm-yy", 46027, "5-Jan-26")]
    public void A_code_typed_into_custom_is_no_built_in_under_en_gb_r7(string typed, double value, string shown)
    {
        var info = CultureInfo.GetCultureInfo("en-GB");
        var sheet = In("en-GB");
        sheet.Enter(CellAddress.Parse("A1"), "=" + value.ToString(CultureInfo.InvariantCulture));

        Assert.True(NumberFormat.TryParseLocal(typed, info, out var format, out _));

        Assert.DoesNotContain(format, DateAndTimeBuiltIns);
        Assert.Equal(typed, format.LocalCode(info));
        Assert.Equal(shown, Shown(sheet, format));
    }
}
