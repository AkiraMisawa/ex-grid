using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>ERROR.TYPE</c> (ADR-0047's note of 2026-10-10, docs/specs/exsheet-functions ticket 17): what
/// the Excel case corpus cannot state. <c>#GETTING_DATA</c> and <c>#CIRC!</c> are states of a
/// computation, given back and never numbered, and an Error Value outside Microsoft's table is
/// refused (SH-68). Microsoft's table itself is in <c>ExcelCases/error-type.json</c>.
/// </summary>
public class ErrorTypeTests
{
    [Fact] // ADR-0047 (2026-10-10), ADR-0049: a Formula that calls ERROR.TYPE over a waiting Linked Table waits with it
    public void ADR0047_error_type_waits_with_a_linked_table()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        sheet.Enter("A1", "=ERROR.TYPE(INDEX(Rates[Mid],1))");
        sheet.Enter("A2", "=IF(TRUE,1,ERROR.TYPE(INDEX(Rates[Mid],1)))");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));

        sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromError(ErrorValue.NA)]]);

        Assert.Equal(7, sheet.Number("A1"));
        Assert.Equal(1, sheet.Number("A2"));
    }

    [Fact] // ADR-0047 (2026-10-10), ADR-0121: ERROR.TYPE never numbers a day that is not known yet
    public void ADR0047_error_type_waits_for_the_sheet_day()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=ERROR.TYPE(TODAY())");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
    }

    [Fact] // ADR-0047 (2026-10-10): a Formula that reads a cycle through ERROR.TYPE shows #CIRC!, never a number
    public void ADR0047_error_type_gives_a_cycle_back()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=B1");
        sheet.Enter("B1", "=A1");
        sheet.Enter("C1", "=ERROR.TYPE(A1)");
        // A Reference computed while the Formula is evaluated reaches its own cell (ADR-0124).
        sheet.Enter("D1", "=ERROR.TYPE(OFFSET(D1,0,0))");

        Assert.Equal(ErrorValue.Circ, sheet.Error("C1"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("D1"));
    }

    [Theory] // ADR-0047 (2026-10-10), SH-68: an Error Value outside Microsoft's table is #VALUE! until Excel is asked
    [InlineData("#SPILL!")]
    [InlineData("#CALC!")]
    [InlineData("#FIELD!")]
    [InlineData("#BLOCKED!")]
    [InlineData("#CONNECT!")]
    [InlineData("#UNKNOWN!")]
    public void ADR0047_error_type_refuses_an_error_value_outside_microsofts_table(string typed)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", typed);

        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=ERROR.TYPE(A1)").Error);
    }

    [Theory] // ADR-0047 (2026-10-10): Microsoft's table, #NULL! 1 to #N/A 7
    [InlineData("#NULL!", 1)]
    [InlineData("#DIV/0!", 2)]
    [InlineData("#VALUE!", 3)]
    [InlineData("#REF!", 4)]
    [InlineData("#NAME?", 5)]
    [InlineData("#NUM!", 6)]
    [InlineData("#N/A", 7)]
    public void ADR0047_error_type_numbers_microsofts_table(string typed, double number)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", typed);

        Assert.Equal(number, sheet.Evaluate("=ERROR.TYPE(A1)").Number);
    }
}
