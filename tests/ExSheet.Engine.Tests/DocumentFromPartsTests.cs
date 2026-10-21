using System.Globalization;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>What a reader of another format needs from the engine (ADR-0330 §3, §4): a document built from its parts, a Formula moved as a copy moves it, and the names a Formula reads that ExSheet does not know.</summary>
public class DocumentFromPartsTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static Entry Formula(string text) => Entry.Parse(text, CultureInfo.InvariantCulture)!;

    [Theory] // ADR-0330 §4, ADR-0048: a Formula moved as a copy moves it: relative parts move, absolute ones stay, off the Sheet is #REF!
    [InlineData("=A1*$A$1+A$1+$A1", 2, 1, "=B3*$A$1+B$1+$A3")]
    [InlineData("=SUM(A:A)+SUM(1:1)+SUM($B:$B)", 3, 2, "=SUM(C:C)+SUM(4:4)+SUM($B:$B)")]
    [InlineData("=Sheet2!B2+A1#", 1, 0, "=Sheet2!B3+A2#")]
    [InlineData("=A2+1", -1, 0, "=A1+1")]
    [InlineData("=A1+1", -1, 0, "=#REF!+1")]
    [InlineData("=SUM(A1:B2)", 0, 0, "=SUM(A1:B2)")]
    public void ADR0330_a_Formula_moved_as_a_copy_moves_it(string formula, int rows, int columns, string moved)
    {
        Assert.Equal(moved, Formula(formula).MovedBy(rows, columns).Formula);
    }

    [Fact] // ADR-0330 §4: a constant moved is itself
    public void ADR0330_a_constant_moved_is_itself()
    {
        var constant = Entry.FromValue(Value.FromNumber(1));

        Assert.Same(constant, constant.MovedBy(5, 5));
    }

    [Fact] // ADR-0330 §4, ADR-0047: the functions a Formula calls outside the declared set, each once, in its order
    public void ADR0330_undeclared_functions_are_named()
    {
        Assert.Equal(["CUBEVALUE", "myAddIn"], Formula("=SUM(CUBEVALUE(\"x\"),myAddIn(1),cubevalue(2),IF(A1,1,2))").UndeclaredFunctions);
        Assert.Empty(Formula("=SUM(A1)+LET(x,1,x)").UndeclaredFunctions);
        Assert.Empty(Entry.FromValue(Value.FromText("SUM")).UndeclaredFunctions);
    }

    [Fact] // ADR-0330 §4, ADR-0181: the names a Formula reads that nothing in it binds; a LET's names are bound
    public void ADR0330_unbound_names_are_named()
    {
        Assert.Equal(["Rate", "Other"], Formula("=Rate*A1+LET(x,rate,x+Other)+TRUE").UnboundNames);
        Assert.Empty(Formula("=LET(x,1,y,x+1,x*y)").UnboundNames);
        Assert.Empty(Formula("=SUM(A1:B2)").UnboundNames);
        Assert.Empty(Entry.FromValue(Value.FromNumber(1)).UnboundNames);
    }

    [Fact] // ADR-0330 §3: a Workbook Document built from its Sheets opens as the Workbook it describes
    public void ADR0330_a_Workbook_Document_built_from_its_Sheets_opens()
    {
        var first = new SheetDocument(EnUs, "Trades",
        [
            new SheetDocumentCell(CellAddress.Parse("A2"), Formula("=A1*2"), null, null, null, null, null),
            new SheetDocumentCell(CellAddress.Parse("A1"), Entry.FromValue(Value.FromNumber(21)), null, null, null, null, null),
        ]);
        var second = new SheetDocument(EnUs, "Desks", [new SheetDocumentCell(CellAddress.Parse("B1"), Formula("=Trades!A2"), null, null, null, null, null)]);

        var document = new WorkbookDocument(EnUs, "desks", [first, second]);
        var workbook = Workbook.Open(WorkbookDocument.FromJson(document.ToJson()));

        Assert.Equal(["A1", "A2"], first.Cells.Select(c => c.Address.ToString()));
        Assert.Equal("Desks", document.Shown);
        Assert.Equal("en-US", document.Culture);
        Assert.Equal("Desks", workbook.Shown.Name);
        Assert.Equal(Value.FromNumber(42), workbook.Sheets[1].GetValue(CellAddress.Parse("B1")));
    }

    [Fact] // ADR-0330 §3, ADR-0048: a document built from parts is refused as one read from JSON would be: nothing is guessed
    public void ADR0330_a_document_built_from_parts_is_refused_where_it_cannot_be_one()
    {
        var cell = new SheetDocumentCell(CellAddress.Parse("A1"), Entry.FromValue(Value.FromNumber(1)), null, null, null, null, null);
        var sheet = new SheetDocument(EnUs, "Sheet1", []);

        Assert.Throws<ArgumentException>(() => new SheetDocument(EnUs, "Bad[name]", []));
        Assert.Throws<ArgumentException>(() => new SheetDocument(EnUs, "Sheet1", [cell, cell]));
        Assert.Throws<ArgumentException>(() => new SheetDocument(new CultureInfo("zz-ZZ"), "Sheet1", []));
        Assert.Throws<ArgumentException>(() => new WorkbookDocument(EnUs, "Sheet1", []));
        Assert.Throws<ArgumentException>(() => new WorkbookDocument(EnUs, "Sheet1", [sheet, new SheetDocument(EnUs, "SHEET1", [])]));
        Assert.Throws<ArgumentException>(() => new WorkbookDocument(EnUs, "Other", [sheet]));
        Assert.Throws<ArgumentException>(() => new WorkbookDocument(CultureInfo.GetCultureInfo("de-DE"), "Sheet1", [sheet]));
    }
}
