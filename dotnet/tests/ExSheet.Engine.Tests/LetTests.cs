using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>LET</c> (ADR-0181, docs/specs/exsheet-functions ticket 14, SH-67). What a LET binds, refuses and computes is in the
/// shared cases (<c>tests/cases/sheet/let.json</c>) and Excel's answers in <c>ExcelCases/let.json</c>; what stays here
/// reads the function catalogue and the declared set, which a sheet case cannot.
/// </summary>
public class LetTests
{
    [Fact] // ADR-0181 (run 17), SH-67: every one of Excel's function names that is a name in shape, and no Reference or boolean, is a LET name, declared by the engine or not, as the keyboard took SUM, IRR and N (LET-101, LET-111, LET-112)
    public void ADR0181_every_excel_function_name_that_is_a_name_is_a_let_name()
    {
        var names = FunctionCatalogueTests.ExcelFunctions()
            .Where(name => !name.Contains('.') && name is not ("TRUE" or "FALSE") && !CellAddress.TryParse(name, out _))
            .ToList();
        Assert.Contains("SUM", names);
        Assert.Contains("IRR", names);
        Assert.Contains("N", names);
        Assert.Contains("LET", names);
        var sheet = NewSheet();

        Assert.All(names, name => Assert.Equal(2, sheet.Evaluate($"=LET({name},1,{name}+1)").Number));
    }

    [Theory] // ADR-0181 (run 17), SH-67: the pairs let.json binds side by side are an Excel function the engine does not declare and one it does (LET-111 beside LET-101)
    [InlineData("IRR", "SUM")]
    [InlineData("NPER", "RAND")]
    [InlineData("xirr", "rows")]
    [InlineData("N", "PI")]
    public void ADR0181_the_pairs_bound_side_by_side_are_undeclared_and_declared(string undeclared, string declared)
    {
        Assert.Null(DeclaredFunction.Find(undeclared));
        Assert.NotNull(DeclaredFunction.Find(declared));
    }
}
