using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0058, "Not in the first version, and what watches for it" (ticket 40, SH-37). A range of
/// columns cannot be pointed at through a Pointing Scope, because the engine refuses what it would
/// write. The test passes while it refuses, and fails the day it stops: that day needs a decision,
/// not a new expectation. A key of several columns was watched the same way until spilled arrays
/// (ADR-0125) made the pair readable; it was then decided with the user (ADR-0058, amended
/// 2026-10-03), and what a Scope writes for it is pinned here instead.
/// </summary>
public class PointingScopeWatchTests
{
    private static string Gone(string refusal, string pointedAt) =>
        $"This refusal has gone: {refusal}. Open ADR-0058, \"Not in the first version, and what watches for it\", "
        + $"and decide with the user how {pointedAt} is pointed at through a Pointing Scope. "
        + "Do not make this test pass by changing its expectation.";

    private static Sheet WithCds()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["Entity", "Tenor"]);
        sheet.PushLinkedTable("Cds",
        [
            [Value.FromText("ACME"), Value.FromText("5Y"), Value.FromNumber(120)],
            [Value.FromText("ACME"), Value.FromText("10Y"), Value.FromNumber(150)],
        ]);
        return sheet;
    }

    [Fact] // ADR-0058 (amended 2026-10-03): what a Scope writes for a key of several columns reads the row whose parts all match
    public void ADR0058_a_key_of_several_columns_is_read_by_every_part()
    {
        var sheet = WithCds();
        var written = FormulaEntry.LookupText("Cds", ["Entity", "Tenor"], [Value.FromText("ACME"), Value.FromText("10Y")], "Spread");

        Assert.Equal("XLOOKUP(1, (Cds[Entity]=\"ACME\")*(Cds[Tenor]=\"10Y\"), Cds[Spread])", written);
        Assert.Equal(150, sheet.Evaluate("=" + written).Number);
    }

    [Fact] // ADR-0058 (amended 2026-10-03): the lookup reads the same row after the table's rows change order
    public void ADR0058_a_key_of_several_columns_follows_its_row()
    {
        var sheet = WithCds();
        sheet.Enter("A1", "=" + FormulaEntry.LookupText("Cds", ["Entity", "Tenor"], [Value.FromText("acme"), Value.FromText("5y")], "Spread"));
        Assert.Equal(120, sheet.Number("A1"));

        sheet.PushLinkedTable("Cds",
        [
            [Value.FromText("ACME"), Value.FromText("10Y"), Value.FromNumber(151)],
            [Value.FromText("ACME"), Value.FromText("5Y"), Value.FromNumber(121)],
        ]);

        Assert.Equal(121, sheet.Number("A1"));
    }

    [Fact] // ADR-0058 (amended 2026-10-03): a row that is not there is #N/A, never another row's Value
    public void ADR0058_a_key_of_several_columns_that_is_not_there_is_NA()
    {
        var sheet = WithCds();

        Assert.Equal(ErrorValue.NA, sheet.Evaluate("=" + FormulaEntry.LookupText("Cds", ["Entity", "Tenor"], [Value.FromText("ACME"), Value.FromText("7Y")], "Spread")).Error);
    }

    [Fact] // ADR-0058 (amended 2026-10-03): a table of one row is read too, though its comparison is one Value
    public void ADR0058_a_key_of_several_columns_over_one_row()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Cds", ["Entity", "Tenor", "Spread"], ["Entity", "Tenor"]);
        sheet.PushLinkedTable("Cds", [[Value.FromText("ACME"), Value.FromText("5Y"), Value.FromNumber(120)]]);

        Assert.Equal(120, sheet.Evaluate("=" + FormulaEntry.LookupText("Cds", ["Entity", "Tenor"], [Value.FromText("ACME"), Value.FromText("5Y")], "Spread")).Number);
    }

    [Theory] // ADR-0058 (amended 2026-10-03): the Scope's dashes find the row as the written lookup does
    [InlineData("ACME", "5Y", true)]
    [InlineData("acme", "5y", true)]
    [InlineData("ACME", "10Y", false)]
    [InlineData("ACME", null, false)]
    public void ADR0058_the_dashes_find_the_row_the_lookup_reads(string entity, string? tenor, bool found)
    {
        Value?[] candidate = [Value.FromText(entity), tenor is null ? null : Value.FromText(tenor)];

        Assert.Equal(found, FormulaEntry.LookupFinds([Value.FromText("ACME"), Value.FromText("5Y")], candidate));
    }

    [Fact] // ADR-0058 (amended 2026-10-03): numbers in a key of several columns are one when the = operator finds them equal
    public void ADR0058_numbers_equal_at_fifteen_digits_repeat()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("T", ["A", "B", "V"], ["A", "B"]);

        Assert.Throws<RepeatedKeyException>(() => sheet.PushLinkedTable("T",
        [
            [Value.FromText("x"), Value.FromNumber(0.1 + 0.2), Value.FromNumber(1)],
            [Value.FromText("X"), Value.FromNumber(0.3), Value.FromNumber(2)],
        ]));
        Assert.True(FormulaEntry.LookupFinds([Value.FromText("x"), Value.FromNumber(0.3)], [Value.FromText("x"), Value.FromNumber(0.1 + 0.2)]));
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
