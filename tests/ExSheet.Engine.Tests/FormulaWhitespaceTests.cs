using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class FormulaWhitespaceTests
{
    private static string Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))!.Formula!;

    [Theory] // ADR-0047: a Formula keeps the whitespace it was typed with, before, between and after its tokens
    [InlineData("= 1 + 2")]
    [InlineData("=A1 + B1")]
    [InlineData("=SUM( A1:A3 , 4 )")]
    [InlineData("=IF(A1 > 0,\n  A1,\n  0)")]
    [InlineData("=\tA1\t*\t2")]
    [InlineData("=A1+1  ")]
    [InlineData("=  -A1%")]
    [InlineData("=\"a  b\" & \" c\"")]
    [InlineData("= Positions[PV] ")]
    public void Whitespace_round_trips(string formula)
    {
        Assert.Equal(formula, Entry.FromFormula(formula).Formula);

        var sheet = NewSheet();
        sheet.Enter("C5", formula);
        Assert.Equal(formula, sheet.GetEntryText(CellAddress.Parse("C5")));

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));
        Assert.Equal(formula, reopened.GetEntryText(CellAddress.Parse("C5")));
    }

    [Fact] // ADR-0047/0048: undoing a structural edit puts back the Formula's text exactly
    public void Undo_restores_the_text_exactly()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "= A5 + 1 ");

        var step = sheet.Do(SheetEdit.DeleteRows(4));
        Assert.Equal("= #REF! + 1 ", Formula(sheet, "B1"));

        step.Undo();
        Assert.Equal("= A5 + 1 ", Formula(sheet, "B1"));
    }

    [Fact] // ADR-0047: two Formulas that differ only in whitespace are different Entries
    public void Whitespace_is_part_of_the_entry()
    {
        Assert.NotEqual(Entry.FromFormula("=A1+1"), Entry.FromFormula("= A1+1"));
    }
}
