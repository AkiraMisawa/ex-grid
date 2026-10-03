using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0121: <c>TODAY()</c> answers the Sheet Day, which the engine is given as data and never reads
/// from a clock. Until it is given, a Formula that calls <c>TODAY</c> waits.
/// </summary>
public class SheetDayTests
{
    private static readonly DateOnly Day = new(2026, 10, 3);

    /// <summary>The serial of 3 October 2026 in Excel's 1900 date system.</summary>
    private const double DaySerial = 46298;

    [Fact] // ADR-0121: before the Sheet Day is known, TODAY waits, and IFERROR does not stand in for it
    public void Today_waits_until_the_day_is_known()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=TODAY()");
        sheet.Enter("A2", "=IFERROR(TODAY(),0)");
        sheet.Enter("A3", "=A1+1");

        Assert.Null(sheet.Today);
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A3"));
    }

    [Fact] // ADR-0121: a Formula that calls TODAY waits as a whole, whichever branch it takes, as one reading a waiting Linked Table does
    public void A_branch_not_taken_still_waits()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=IF(TRUE,1,TODAY())");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));

        sheet.SetToday(Day);

        Assert.Equal(1, sheet.Number("A1"));
    }

    [Fact] // ADR-0121: TODAY is the Sheet Day's serial
    public void Today_is_the_sheet_days_serial()
    {
        var sheet = NewSheet();
        sheet.SetToday(Day);

        Assert.Equal(DaySerial, sheet.Evaluate("=TODAY()").Number);
        Assert.Equal(2026, sheet.Evaluate("=YEAR(TODAY())").Number);
        Assert.Equal(DaySerial, sheet.Evaluate("=DATE(2026,10,3)").Number);
    }

    [Fact] // ADR-0121: setting the day recalculates the Formulas that call TODAY and what reads them, and nothing else
    public void Setting_the_day_recalculates_only_its_readers()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=TODAY()");
        sheet.Enter("B1", "=A1+1");
        sheet.Enter("C1", "=1+1");

        var change = sheet.SetToday(Day);

        Assert.Equal(["A1", "B1"], change.Recalculated.Addresses());
        Assert.Equal(DaySerial + 1, sheet.Number("B1"));

        var next = sheet.SetToday(Day.AddDays(1));

        Assert.Equal(["A1", "B1"], next.Recalculated.Addresses());
        Assert.Equal(DaySerial + 2, sheet.Number("B1"));
    }

    [Fact] // ADR-0121: the day the Sheet already has changes nothing
    public void The_same_day_changes_nothing()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=TODAY()");
        sheet.SetToday(Day);

        var change = sheet.SetToday(Day);

        Assert.Empty(change.Recalculated);
        Assert.Empty(change.ValueChanges);
    }

    [Fact] // ADR-0121: a Formula that no longer calls TODAY is no longer recalculated by the day
    public void A_formula_rewritten_without_today_leaves_the_readers()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=TODAY()");
        sheet.Enter("A1", "=5");

        Assert.Empty(sheet.SetToday(Day).Recalculated);
    }

    [Fact] // ADR-0121: clearing the day makes TODAY wait again
    public void Clearing_the_day_makes_today_wait_again()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=TODAY()");
        sheet.SetToday(Day);

        sheet.SetToday(null);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
    }

    [Fact] // ADR-0121 / ADR-0048: the Sheet Day is not part of a Sheet Document, so a reopened Sheet waits for its day
    public void A_reopened_sheet_waits_for_its_day()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=TODAY()");
        sheet.SetToday(Day);

        var reopened = Sheet.Open(sheet.ToDocument());

        Assert.Null(reopened.Today);
        Assert.Equal(ErrorValue.GettingData, reopened.Error("A1"));
    }

    [Fact] // ADR-0121 / ADR-0047: Excel's dates begin in 1900, and a Sheet Day before that is refused
    public void A_day_before_1900_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewSheet().SetToday(new DateOnly(1899, 12, 31)));
    }

    [Theory] // ADR-0121: TODAY takes no argument, as Excel's does
    [InlineData("=TODAY(1)")]
    [InlineData("=TODAY(,)")]
    public void Today_with_an_argument_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }
}
