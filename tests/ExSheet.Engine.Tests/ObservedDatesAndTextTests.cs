using System.Globalization;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The dates before 1 March 1900, SUBSTITUTE's instances, and TEXT's General and empty code, as the
/// seventeenth Windows run observed Excel to answer them (2026-10-11,
/// <c>verification/2026-10-11-windows-17/</c>; ADR-0047, ADR-0120, ADR-0202).
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

    [Theory] // ADR-0047: a start that is no day of the real calendar is refused until Excel is asked (EDATE-010, EOMONTH-010)
    [InlineData("=EDATE(60,1)")]
    [InlineData("=EDATE(0,1)")]
    [InlineData("=EOMONTH(60,0)")]
    [InlineData("=EOMONTH(60.5,-1)")]
    [InlineData("=EOMONTH(0,1)")]
    public void Adr0047_EDate_and_EOMonth_refuse_serial_0_and_the_day_that_never_existed(string formula) =>
        Assert.Equal(Error(ErrorValue.Value), NewSheet().Evaluate(formula));

    [Theory] // ADR-0047: a result before 1900 is no date, as a result after 9999 is not (EOMONTH-005)
    [InlineData("=EDATE(15,-1)")]
    [InlineData("=EOMONTH(59,-2)")]
    public void Adr0047_EDate_and_EOMonth_before_1900_are_NUM(string formula) =>
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

    [Theory] // ADR-0047: WORKDAY counts as NETWORKDAYS does (WORKDAY-006 to 009, to be asked)
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

    [Theory] // ADR-0120: refused until Excel is asked whether the minus sign takes one of the 11 characters (TEXTFN-025), and what @ alone shows (TEXTFN-028)
    [InlineData("=TEXT(-1/3,\"General\")")]
    [InlineData("=TEXT(-12345678901,\"General\")")]
    [InlineData("=TEXT(1/3,\"@\")")]
    [InlineData("=TEXT(0.00001,\"@\")")]
    public void Adr0120_Text_General_refuses_where_the_unobserved_readings_disagree(string formula) =>
        Assert.Equal(Error(ErrorValue.Value), NewSheet().Evaluate(formula));

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
