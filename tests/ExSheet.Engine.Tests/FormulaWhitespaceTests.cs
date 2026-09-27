using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class FormulaWhitespaceTests
{
    private static string Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))!.Formula!;

    [Theory] // ADR-0047: a Formula keeps the whitespace it was typed with before its tokens, as Excel does
    [InlineData("= 1 + 2")]
    [InlineData("=A1 + B1")]
    [InlineData("=SUM( A1:A3, 4 )")]
    [InlineData("=IF(A1 > 0,\n  A1,\n  0)")]
    [InlineData("=  -A1%")]
    [InlineData("=\"a  b\" & \" c\"")]
    [InlineData("= Positions[PV]")]
    public void Whitespace_round_trips(string formula)
    {
        Assert.Equal(formula, Entry.FromFormula(formula).Formula);

        var sheet = NewSheet();
        sheet.Enter("C5", formula);
        Assert.Equal(formula, sheet.GetEntryText(CellAddress.Parse("C5")));

        var reopened = Sheet.Open(SheetDocument.FromJson(sheet.ToDocument().ToJson()));
        Assert.Equal(formula, reopened.GetEntryText(CellAddress.Parse("C5")));
    }

    [Theory] // ADR-0047 (observed in Excel): whitespace at the end, and before a comma, is dropped on entry
    [InlineData("=A1+1  ", "=A1+1")]
    [InlineData("= Positions[PV] ", "= Positions[PV]")]
    [InlineData("=SUM( A1:A3 , 4 )", "=SUM( A1:A3, 4 )")]
    [InlineData("= sum( a1 , $b$2 )", "= SUM( A1, $B$2 )")]
    [InlineData("=A1+1\n", "=A1+1")]
    [InlineData("=IF(A1\n, 1, 0)", "=IF(A1, 1, 0)")]
    public void Whitespace_Excel_drops_is_dropped_on_entry(string typed, string stored)
    {
        Assert.Equal(stored, Entry.FromFormula(typed).Formula);
    }

    [Theory] // ADR-0047 (observed in Excel): a tab between tokens is refused, as are the other space characters
    [InlineData("=\tA1\t*\t2")]
    [InlineData("=A1 +1")]
    public void A_tab_between_tokens_is_refused(string typed)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(typed));
    }

    [Fact] // ADR-0047: a tab inside quoted text is the text's own
    public void A_tab_inside_text_is_kept()
    {
        Assert.Equal("=\"a\tb\"", Entry.FromFormula("=\"a\tb\"").Formula);
    }

    [Fact] // ADR-0047 (observed in Excel): a Reference a deletion makes #REF! takes the whitespace before it
    public void A_reference_made_ref_takes_its_whitespace()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "=  A5  *  C1 ");

        sheet.DeleteRows(4);

        Assert.Equal("=#REF!  *  C1", Formula(sheet, "B1"));
    }

    [Fact] // ADR-0047/0048: undoing a structural edit puts back the Formula's text exactly
    public void Undo_restores_the_text_exactly()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "1");
        sheet.Enter("B1", "= A5 + 1");

        var step = sheet.Do(SheetEdit.DeleteRows(4));
        Assert.Equal("=#REF! + 1", Formula(sheet, "B1"));

        step.Undo();
        Assert.Equal("= A5 + 1", Formula(sheet, "B1"));
    }

    [Fact] // ADR-0047: two Formulas that differ only in whitespace are different Entries
    public void Whitespace_is_part_of_the_entry()
    {
        Assert.NotEqual(Entry.FromFormula("=A1+1"), Entry.FromFormula("= A1+1"));
    }
}
