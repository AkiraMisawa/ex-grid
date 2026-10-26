using System.Globalization;
using ExSheet.Engine;
using ExSheet.Engine.Formulas;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What a reader of another format needs from the engine (ADR-0330 §3, §4) that a shared case does not hold: a Formula
/// moved, the names it reads, an error cell's text that is none, and the date serials are in
/// <c>tests/cases/sheet/document-from-parts.json</c>. What stays here, mirrored in the Script Core's
/// <c>document-from-parts.test.ts</c> where its surface has the same call, is a constant moved being the same Entry,
/// the walk over every Error Value, the engine's own nodes, and documents built from their parts, which the two
/// surfaces build differently.
/// </summary>
public class DocumentFromPartsTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static Entry Formula(string text) => Entry.Parse(text, CultureInfo.InvariantCulture)!;

    [Fact] // ADR-0330 §4: a constant moved is itself
    public void ADR0330_a_constant_moved_is_itself()
    {
        var constant = Entry.FromValue(Value.FromNumber(1));

        Assert.Same(constant, constant.MovedBy(5, 5));
    }

    [Fact] // ADR-0330 §4: a LAMBDA's parameters are bound by its name alone, so declaring LAMBDA one day leaves them bound
    public void ADR0330_a_declared_LAMBDAs_parameters_are_bound()
    {
        var declared = Assert.IsType<FunctionDefinition>(FunctionLibrary.Find("SUM"));
        var lambda = new FunctionNode("LAMBDA", [new NameNode("v", 0, null), new BinaryNode("*", new NameNode("v", 0, null), new NumberNode(2))], declared);

        Assert.Empty(UnboundNameWalk.Of(lambda));
    }

    [Fact] // ADR-0330 §4: an error cell's text is read by the engine's own table, exactly as written: every Error Value a cell can hold, and nothing else
    public void ADR0330_error_text_is_read_by_the_engines_table()
    {
        foreach (var error in Enum.GetValues<ErrorValue>().Where(e => e is not (ErrorValue.GettingData or ErrorValue.Circ)))
        {
            var text = Value.FromError(error).ToString();
            Assert.Equal(Value.FromError(error), Entry.FromErrorText(text)!.Constant);
            Assert.Null(Entry.FromErrorText(text.ToLowerInvariant()));
        }
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
