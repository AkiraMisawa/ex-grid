using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0058, "Not in the first version, and what watches for it" (ticket 40, SH-37). A key of
/// several columns and a range of columns cannot be pointed at through a Pointing Scope, because the
/// engine refuses what either would write. These tests pass while it refuses, and fail the day it
/// stops: that day needs a decision, not a new expectation.
/// </summary>
public class PointingScopeWatchTests
{
    private static string Gone(string refusal, string pointedAt) =>
        $"This refusal has gone: {refusal}. Open ADR-0058, \"Not in the first version, and what watches for it\", "
        + $"and decide with the user how {pointedAt} is pointed at through a Pointing Scope. "
        + "Do not make this test pass by changing its expectation.";

    [Fact] // ADR-0058 (SH-37): watches for array operations — a table column compared with a value, over two rows, is #VALUE!
    public void ADR0058_a_table_column_compared_with_a_value_over_two_rows_is_VALUE()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"]);
        sheet.PushLinkedTable("Cds",
        [
            [Value.FromText("ACME"), Value.FromText("5Y"), Value.FromNumber(120)],
            [Value.FromText("ACME"), Value.FromText("10Y"), Value.FromNumber(150)],
        ]);

        var compared = sheet.Evaluate("=(Cds[Entity]=\"ACME\")");
        // What a Scope would write for a key of two columns, were there array operations.
        var paired = sheet.Evaluate("=XLOOKUP(1, (Cds[Entity]=\"ACME\")*(Cds[Tenor]=\"5Y\"), Cds[Spread])");

        Assert.True(compared == Value.FromError(ErrorValue.Value),
            Gone($"(Cds[Entity]=\"ACME\") over two rows is {compared}, not #VALUE!", "a key of several columns"));
        Assert.True(paired == Value.FromError(ErrorValue.Value),
            Gone($"XLOOKUP(1, (Cds[Entity]=\"ACME\")*(Cds[Tenor]=\"5Y\"), Cds[Spread]) is {paired}, not #VALUE!", "a key of several columns"));
    }

    [Theory] // ADR-0058 (SH-37): watches for column ranges — a range of columns is refused by the grammar
    [InlineData("=Xva[[A]:[B]]")]
    [InlineData("=SUM(Xva[[CVA Before]:[CVA Diff]])")]
    public void ADR0058_a_range_of_columns_is_refused_by_the_grammar(string formula)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Xva", ["A", "B", "CVA Before", "CVA Diff"]);

        var read = Record.Exception(() => Entry.FromFormula(formula));
        var entered = Record.Exception(() => sheet.Enter("A1", formula));

        Assert.True(read is FormulaSyntaxException && entered is FormulaSyntaxException,
            Gone($"{formula} is read as a Formula", "a range of columns"));
    }
}
