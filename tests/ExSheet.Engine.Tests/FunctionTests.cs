using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047 admits a function only with Excel's result, edge cases included (SH-7). What each
/// function answers is in the Excel case corpus (<c>ExcelCases/*.json</c>, run by
/// <see cref="ExcelCaseTests"/> and by the Excel oracle); what stays here is the engine's own
/// surface: dependencies, entry-time refusals and the declared list.
/// </summary>
public class FunctionTests
{
    [Fact] // ADR-0047: a range argument is a dependency — changing a cell inside it recomputes, outside it does not
    public void A_range_is_a_dependency()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("B1", "=SUM(A1:A3)");

        var inside = sheet.Enter("A3", "4");
        var outside = sheet.Enter("A4", "8");

        Assert.Equal(7, sheet.Number("B1"));
        Assert.Equal(["B1"], inside.Recalculated.Addresses());
        Assert.Empty(outside.Recalculated);
    }

    [Theory] // ADR-0047: Excel refuses a call with the wrong number of arguments when it is entered
    [InlineData("=ROUND(1)")]
    [InlineData("=ROUND(1,2,3)")]
    [InlineData("=IF(TRUE)")]
    [InlineData("=IFERROR(1)")]
    [InlineData("=ISERROR()")]
    [InlineData("=XLOOKUP(1,A1:A2)")]
    [InlineData("=SUM()")]
    public void A_call_with_the_wrong_number_of_arguments_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }

    [Fact] // ADR-0047/0051: the declared list is exposed for completion and hints, and is exactly ADR-0047's set
    public void The_declared_functions_are_exactly_adr_0047s_set()
    {
        Assert.Equal(
            ["AVERAGE", "COUNT", "COUNTA", "IF", "IFERROR", "ISERROR", "MAX", "MIN", "ROUND", "SUM", "XLOOKUP"],
            DeclaredFunction.All.Select(f => f.Name));
        Assert.Equal("SUM(number1, [number2], ...)", DeclaredFunction.Find("sum")!.Signature);
        Assert.Equal(
            "XLOOKUP(lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode])",
            DeclaredFunction.Find("XLOOKUP")!.Signature);
        Assert.Null(DeclaredFunction.Find("VLOOKUP"));
        Assert.All(DeclaredFunction.All, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));
    }
}
