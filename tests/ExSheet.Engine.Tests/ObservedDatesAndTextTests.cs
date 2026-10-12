using System.Globalization;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The dates before 1 March 1900, SUBSTITUTE's instances, and TEXT's General and empty code, as the
/// seventeenth Windows run observed Excel to answer them (2026-10-11,
/// <c>verification/2026-10-11-windows-17/</c>), and serial 0 and 60 under EOMONTH and EDATE, and
/// TEXT's General with a minus sign and under a lone <c>@</c>, as the area passes did (2026-10-12,
/// <c>verification/2026-10-12-windows-area/</c> and <c>-area-2/</c>; ADR-0047, ADR-0120, ADR-0202).
/// </summary>
public class ObservedDatesAndTextTests
{
    private static Value Number(double number) => Value.FromNumber(number);

    private static Value Text(string text) => Value.FromText(text);

    private static Value Error(ErrorValue error) => Value.FromError(error);

    // ---- EDATE and EOMONTH ------------------------------------------------------------------------

    [Theory] // ADR-0047: EOMONTH-008 and EDATE-008, observed
    [InlineData("=EOMONTH(31,1)", 59)]
    [InlineData("=EDATE(DATE(1900,3,15),-1)", 46)]
    [InlineData("=EDATE(31,1)", 59)]
    [InlineData("=EOMONTH(DATE(1900,3,1),-1)", 59)]
    [InlineData("=EOMONTH(1,0)", 31)]
    [InlineData("=EDATE(46,1)", 75)]
    [InlineData("=EDATE(59,0)", 59)]
    [InlineData("=EOMONTH(59.9,1)", 91)]
    public void Adr0047_EDate_and_EOMonth_count_months_on_the_real_calendar_before_March_1900(string formula, double serial) =>
        Assert.Equal(Number(serial), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: EOMONTH-010 and EDATE-010, observed; the start is read as YEAR, MONTH and DAY read it (EOMONTH-012 to 015, EDATE-013 to 015, observed)
    [InlineData("=EOMONTH(0,1)", 59)]
    [InlineData("=EOMONTH(0,0)", 31)]
    [InlineData("=EOMONTH(0.75,2)", 91)]
    [InlineData("=EOMONTH(60,0)", 59)]
    [InlineData("=EOMONTH(60,1)", 91)]
    [InlineData("=EOMONTH(60.5,-1)", 31)]
    [InlineData("=EDATE(60,1)", 89)]
    [InlineData("=EDATE(60,0)", 59)]
    [InlineData("=EDATE(60,12)", 425)]
    [InlineData("=EDATE(60,48)", 1521)]
    [InlineData("=EDATE(60,-1)", 29)]
    [InlineData("=EDATE(60.9,1.5)", 89)]
    public void Adr0047_EDate_and_EOMonth_read_serial_0_and_the_day_that_never_existed_as_Day_and_Month_do(string formula, double serial) =>
        Assert.Equal(Number(serial), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: EOMONTH from serial 0 to before 1900 is no date, as EOMONTH-005's after 9999 is not (EOMONTH-015, observed)
    [InlineData("=EOMONTH(0,-1)")]
    [InlineData("=EOMONTH(0.5,-13)")]
    public void Adr0047_EOMonth_from_serial_0_to_before_1900_is_NUM(string formula) =>
        Assert.Equal(Error(ErrorValue.Num), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: EDATE-016, observed; from serial 0, day 0 of the month reached is the day before its 1st, where every reading that fits agrees (EDATE-021, 022, to be asked)
    [InlineData("=EDATE(0,1)", 31)]
    [InlineData("=EDATE(0.99,1)", 31)]
    [InlineData("=EDATE(0,3)", 91)]
    [InlineData("=EDATE(0,4)", 121)]
    [InlineData("=EDATE(0.5,12)", 366)]
    [InlineData("=EDATE(0,14)", 425)]
    [InlineData("=EDATE(0,97199)", 2958434)]
    public void Adr0047_EDate_from_serial_0_is_the_day_before_the_first_of_the_month_it_reaches(string formula, double serial) =>
        Assert.Equal(Number(serial), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: EDATE from serial 0 to before 1900 or past 9999 is no date, under every reading that fits
    [InlineData("=EDATE(0,-1)")]
    [InlineData("=EDATE(0.5,-12)")]
    [InlineData("=EDATE(0,97201)")]
    [InlineData("=EDATE(0,97202)")]
    public void Adr0047_EDate_from_serial_0_before_1900_or_past_9999_is_NUM(string formula) =>
        Assert.Equal(Error(ErrorValue.Num), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047, ADR-0202: the readings that give every observed answer part at January 1900, March 1900 and January 10000; refused until Excel is asked (EDATE-018 to 020)
    [InlineData("=EDATE(0,0)")]
    [InlineData("=EDATE(0,2)")]
    [InlineData("=EDATE(0,97200)")]
    [InlineData("=EDATE(0.5,2)")]
    [InlineData("=EDATE(0.9,0)")]
    [InlineData("=EDATE(0.25,97200)")]
    public void Adr0047_EDate_from_serial_0_is_refused_where_the_readings_that_fit_part(string formula) =>
        Assert.Equal(Error(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Fact] // ADR-0047: EOMONTH from serial 0 is not refused where EDATE's readings part; it reads no day
    public void Adr0047_EOMonth_from_serial_0_answers_where_EDates_readings_part()
    {
        var sheet = NewSheet();
        Assert.Equal(Number(31), sheet.Evaluate("=EOMONTH(0,0)"));
        Assert.Equal(Number(91), sheet.Evaluate("=EOMONTH(0,2)"));
        Assert.Equal(Error(ErrorValue.Num), sheet.Evaluate("=EOMONTH(0,97200)"));
    }

    [Theory] // ADR-0047: a result before 1900 is no date, as a result after 9999 is not (EOMONTH-005)
    [InlineData("=EDATE(15,-1)")]
    [InlineData("=EDATE(31,-1)")]
    [InlineData("=EOMONTH(59,-2)")]
    public void Adr0047_EDate_and_EOMonth_before_1900_are_NUM(string formula) =>
        Assert.Equal(Error(ErrorValue.Num), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: EOMONTH-016 and 017 and EDATE-017, observed; a start between -1 and 0 is cut down to its day, -1, as YEAR-008 reads a serial
    [InlineData("=EOMONTH(-0.5,0)")]
    [InlineData("=EOMONTH(-0.5,1)")]
    [InlineData("=EOMONTH(-0.0001,12)")]
    [InlineData("=EOMONTH(-0.5,-1)")]
    [InlineData("=EDATE(-0.5,1)")]
    [InlineData("=EDATE(-0.9999,0)")]
    [InlineData("=EDATE(-0.5,-1)")]
    public void Adr0047_EDate_and_EOMonth_cut_a_start_between_minus_1_and_0_down_to_minus_1_which_is_NUM(string formula) =>
        Assert.Equal(Error(ErrorValue.Num), NewSheet().Evaluate(formula));

    [Fact] // ADR-0047: every function that reads a date cuts a serial between -1 and 0 down to -1 (YEAR-008, MONTH-005, DAY-005, WEEKDAY-008, DAYS-005, NETWORKDAYS-009, WORKDAY-010, EOMONTH-016, EDATE-017, observed)
    public void Adr0047_A_serial_between_minus_1_and_0_is_NUM_under_every_function_that_reads_a_date()
    {
        var sheet = NewSheet();
        foreach (var formula in new[] { "=YEAR(-0.5)", "=MONTH(-0.5)", "=DAY(-0.5)", "=WEEKDAY(-0.5)", "=DAYS(1,-0.5)", "=DAYS(-0.5,1)", "=NETWORKDAYS(-0.5,6)", "=NETWORKDAYS(6,-0.5)", "=WORKDAY(-0.5,1)", "=EOMONTH(-0.5,0)", "=EDATE(-0.5,0)", "=EDATE(-0.5,1)" })
            Assert.Equal(Error(ErrorValue.Num), sheet.Evaluate(formula));
    }

    [Theory] // ADR-0047: a start of -1 is #NUM! (EOMONTH-004, EDATE-007), and one below it is below 0 truncated or cut down
    [InlineData("=EOMONTH(-1,0)")]
    [InlineData("=EDATE(-1,1)")]
    [InlineData("=EOMONTH(-1.5,1)")]
    [InlineData("=EDATE(-1.0001,12)")]
    public void Adr0047_EDate_and_EOMonth_from_minus_1_or_below_are_NUM(string formula) =>
        Assert.Equal(Error(ErrorValue.Num), NewSheet().Evaluate(formula));

    // ---- NETWORKDAYS and WORKDAY ------------------------------------------------------------------

    [Theory] // ADR-0047: NETWORKDAYS-005, observed; each serial takes WEEKDAY's day (WEEKDAY-005, 006)
    [InlineData("=NETWORKDAYS(30,100)", 51)]
    [InlineData("=NETWORKDAYS(100,30)", -51)]
    [InlineData("=NETWORKDAYS(0,0)", 0)]
    [InlineData("=NETWORKDAYS(1,1)", 0)]
    [InlineData("=NETWORKDAYS(6,6)", 1)]
    [InlineData("=NETWORKDAYS(60,60)", 1)]
    [InlineData("=NETWORKDAYS(0,61)", 44)]
    [InlineData("=NETWORKDAYS(30,100,60)", 50)]
    public void Adr0047_NetworkDays_counts_the_days_before_March_1900(string formula, double days) =>
        Assert.Equal(Number(days), NewSheet().Evaluate(formula));

    [Fact] // ADR-0047: a day is a workday exactly when WEEKDAY puts it from Monday to Friday
    public void Adr0047_NetworkDays_of_one_day_agrees_with_Weekday_from_serial_0()
    {
        var sheet = NewSheet();
        for (var serial = 0; serial <= 130; serial++)
        {
            var weekday = sheet.Evaluate($"=WEEKDAY({serial},2)").Number;
            Assert.Equal(Number(weekday <= 5 ? 1 : 0), sheet.Evaluate($"=NETWORKDAYS({serial},{serial})"));
        }
    }

    [Theory] // ADR-0047: WORKDAY counts as NETWORKDAYS does (WORKDAY-006 to 009, observed)
    [InlineData("=WORKDAY(30,10)", 44)]
    [InlineData("=WORKDAY(30,4)", 34)]
    [InlineData("=WORKDAY(55,5)", 62)]
    [InlineData("=WORKDAY(62,-5)", 55)]
    [InlineData("=WORKDAY(3,-1)", 2)]
    public void Adr0047_Workday_counts_the_days_before_March_1900_as_NetworkDays_does(string formula, double serial) =>
        Assert.Equal(Number(serial), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: a result before serial 0 is no date
    [InlineData("=WORKDAY(2,-1)")]
    [InlineData("=WORKDAY(30,-30)")]
    public void Adr0047_Workday_before_serial_0_is_NUM(string formula) =>
        Assert.Equal(Error(ErrorValue.Num), NewSheet().Evaluate(formula));

    // ---- SUBSTITUTE -----------------------------------------------------------------------------

    [Theory] // ADR-0047: SUBSTITUTE-005, observed
    [InlineData("=SUBSTITUTE(\"aaaa\",\"aa\",\"b\",2)", "aba")]
    [InlineData("=SUBSTITUTE(\"aaaa\",\"aa\",\"b\",3)", "aab")]
    [InlineData("=SUBSTITUTE(\"aaaa\",\"aa\",\"b\",4)", "aaaa")]
    [InlineData("=SUBSTITUTE(\"ababab\",\"abab\",\"x\",2)", "abx")]
    [InlineData("=SUBSTITUTE(\"Quarter 1, 2011\",\"1\",\"2\",3)", "Quarter 1, 2012")]
    public void Adr0047_Substitute_counts_the_instances_at_every_character(string formula, string expected) =>
        Assert.Equal(Text(expected), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: without instance_num, every occurrence left to right, none overlapping (SUBSTITUTE-001)
    [InlineData("=SUBSTITUTE(\"aaaa\",\"aa\",\"b\")", "bb")]
    [InlineData("=SUBSTITUTE(\"aaa\",\"aa\",\"b\")", "ba")]
    public void Adr0047_Substitute_without_an_instance_replaces_without_overlapping(string formula, string expected) =>
        Assert.Equal(Text(expected), NewSheet().Evaluate(formula));

    // ---- TEXT -----------------------------------------------------------------------------------

    [Theory] // ADR-0120: TEXTFN-017, observed; General fitted to 11 characters as a cell's General is
    [InlineData("=TEXT(1/3,\"General\")", "0.333333333")]
    [InlineData("=TEXT(22/7,\"General\")", "3.142857143")]
    [InlineData("=TEXT(123456789012,\"General\")", "1.23457E+11")]
    [InlineData("=TEXT(12345678901,\"General\")", "12345678901")]
    [InlineData("=TEXT(0.00001,\"General\")", "0.00001")]
    [InlineData("=TEXT(5,\"General\")", "5")]
    [InlineData("=TEXT(-5,\"General\")", "-5")]
    [InlineData("=TEXT(-0.5,\"General\")", "-0.5")]
    [InlineData("=TEXT(0.5,\"@\")", "0.5")]
    public void Adr0120_Text_fits_General_to_11_characters(string formula, string expected) =>
        Assert.Equal(Text(expected), NewSheet().Evaluate(formula));

    [Fact] // ADR-0120: the fitted General takes the Sheet culture's decimal separator
    public void Adr0120_Text_General_takes_the_cultures_separator() =>
        Assert.Equal(Text("0,333333333"), new Sheet(CultureInfo.GetCultureInfo("de-DE")).Evaluate("=TEXT(1/3,\"General\")"));

    [Theory] // ADR-0120: TEXTFN-025, 029 and 030, observed; the minus sign is not one of General's 11 characters
    [InlineData("=TEXT(-1/3,\"General\")", "-0.333333333")]
    [InlineData("=TEXT(-22/7,\"General\")", "-3.142857143")]
    [InlineData("=TEXT(-12345678901,\"General\")", "-12345678901")]
    [InlineData("=TEXT(-123456789012,\"General\")", "-1.23457E+11")]
    [InlineData("=TEXT(-0.00001,\"General\")", "-0.00001")]
    [InlineData("=TEXT(-1E-20,\"General\")", "-1E-20")]
    public void Adr0120_Text_General_leaves_the_minus_sign_out_of_its_11_characters(string formula, string expected) =>
        Assert.Equal(Text(expected), NewSheet().Evaluate(formula));

    [Theory] // ADR-0120: TEXTFN-028 and 031 to 034, observed; a code of a text section alone shows a number as TEXT's General does
    [InlineData("=TEXT(1/3,\"@\")", "0.333333333")]
    [InlineData("=TEXT(-1/3,\"@\")", "-0.333333333")]
    [InlineData("=TEXT(123456789012,\"@\")", "1.23457E+11")]
    [InlineData("=TEXT(0.00001,\"@\")", "0.00001")]
    [InlineData("=TEXT(1/3,\"\"\"x\"\"@\")", "0.333333333")]
    public void Adr0120_Text_under_a_text_section_alone_shows_a_number_as_its_General_does(string formula, string expected) =>
        Assert.Equal(Text(expected), NewSheet().Evaluate(formula));

    [Fact] // ADR-0120: every number TEXT's General shows under General, it shows under @ alone
    public void Adr0120_Text_General_and_a_lone_at_sign_agree()
    {
        var sheet = NewSheet();
        foreach (var number in new[] { "1/3", "-1/3", "2/3", "-2/3", "1E-5", "-1E-5", "12345678901", "-12345678901", "123456789012", "-123456789012", "1E+300", "-1E-300", "0", "0.1+0.2" })
        {
            var general = sheet.Evaluate($"=TEXT({number},\"General\")");
            Assert.Equal(ValueKind.Text, general.Kind);
            Assert.Equal(general, sheet.Evaluate($"=TEXT({number},\"@\")"));
        }
    }

    [Fact] // ADR-0120: a negative number shows as its magnitude does, with the minus sign before it
    public void Adr0120_Text_General_of_a_negative_number_is_its_magnitudes_with_a_minus_sign()
    {
        var sheet = NewSheet();
        foreach (var number in new[] { "1/3", "2/3", "1/7", "22/7", "1E-5", "1.5E-9", "12345678901", "123456789012", "99999999999.5", "1E+300", "0.1+0.2" })
        {
            var positive = sheet.Evaluate($"=TEXT({number},\"General\")").Text;
            Assert.Equal(Text("-" + positive), sheet.Evaluate($"=TEXT(-({number}),\"General\")"));
        }
    }

    [Theory] // ADR-0120: TEXTFN-018, observed; text and booleans as under a code without a text section (TEXTFN-023, 024)
    [InlineData("=TEXT(5,\"\")", "")]
    [InlineData("=TEXT(A1,\"\")", "")]
    [InlineData("=TEXT(\"12\",\"\")", "")]
    [InlineData("=TEXT(\"abc\",\"\")", "abc")]
    [InlineData("=TEXT(TRUE,\"\")", "TRUE")]
    public void Adr0120_Text_with_an_empty_code_shows_a_number_as_empty_text(string formula, string expected) =>
        Assert.Equal(Text(expected), NewSheet().Evaluate(formula));

    [Fact] // ADR-0120: an Error Value is the result, whatever the code
    public void Adr0120_Text_with_an_empty_code_gives_an_Error_Value_back() =>
        Assert.Equal(Error(ErrorValue.Div0), NewSheet().Evaluate("=TEXT(1/0,\"\")"));

    [Theory] // ADR-0120, ADR-0202: fractions and conditions stay outside the cell formats' subset (TEXTFN-019, 020)
    [InlineData("=TEXT(4.34,\"# ?/?\")")]
    [InlineData("=TEXT(5,\"[<=9]0;0.0\")")]
    public void Adr0120_Adr0202_Text_refuses_fractions_and_conditions(string formula) =>
        Assert.Equal(Error(ErrorValue.Value), NewSheet().Evaluate(formula));
}
