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

    [Theory] // ADR-0047: tokens are still written in Excel's spelling; only the whitespace is kept as typed
    [InlineData("= sum( a1 , $b$2 )", "= SUM( A1 , $B$2 )")]
    [InlineData("= b2:a1 ", "= A1:B2 ")]
    [InlineData("= true + 1.50", "= TRUE + 1.5")]
    [InlineData("= #n/a", "= #N/A")]
    public void Tokens_are_spelled_as_excel_writes_them(string typed, string recorded)
    {
        Assert.Equal(recorded, Entry.FromFormula(typed).Formula);
    }

    [Fact] // ADR-0047: whitespace does not change a Formula's Value
    public void Whitespace_does_not_change_the_value()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "3");
        sheet.Enter("B1", "4");

        Assert.Equal(7, sheet.Evaluate("= A1 +\n B1 ").Number);
    }

    [Fact] // ADR-0047: an insertion rewrites only the Reference tokens, keeping the whitespace around them
    public void An_insertion_keeps_the_whitespace()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "= A5 +  SUM( A1:A5 )\n* 2 ");

        sheet.InsertRows(2);

        Assert.Equal("= A6 +  SUM( A1:A6 )\n* 2 ", Formula(sheet, "B1"));
    }

    [Fact] // ADR-0047: a deletion writes #REF! in place of the Reference token alone
    public void A_deletion_keeps_the_whitespace()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "=  A5  *  C1 ");

        sheet.DeleteRows(4);

        Assert.Equal("=  #REF!  *  C1 ", Formula(sheet, "B1"));
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

    [Fact] // ADR-0047/0048: a copy pasted elsewhere shifts its relative References and keeps the whitespace
    public void A_paste_keeps_the_whitespace()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "= A1 * $A$1 ");
        var block = sheet.Copy(CellRange.Parse("B1")).Block!;

        sheet.Do(SheetEdit.Paste(block, CellAddress.Parse("C3")));

        Assert.Equal("= B3 * $A$1 ", Formula(sheet, "C3"));
    }

    [Fact] // ADR-0047/0050: a fill shifts its relative References and keeps the whitespace
    public void A_fill_keeps_the_whitespace()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=A1 + 1");

        sheet.Do(SheetEdit.Fill(CellRange.Parse("B1"), CellRange.Parse("B2:B3"), FillDirection.Down));

        Assert.Equal("=A2 + 1", Formula(sheet, "B2"));
        Assert.Equal("=A3 + 1", Formula(sheet, "B3"));
    }

    [Fact] // ADR-0046/0047: a rename rewrites the qualifier and keeps the whitespace
    public void A_rename_keeps_the_whitespace()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "= Sheet1!A1 + 1 ");

        sheet.Rename("My Sheet");

        Assert.Equal("= 'My Sheet'!A1 + 1 ", Formula(sheet, "B1"));
    }

    [Fact] // ADR-0047: two Formulas that differ only in whitespace are different Entries
    public void Whitespace_is_part_of_the_entry()
    {
        Assert.NotEqual(Entry.FromFormula("=A1+1"), Entry.FromFormula("= A1+1"));
    }
}
