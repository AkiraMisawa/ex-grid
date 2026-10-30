using Xunit;

namespace ExSheet.Engine.Tests.Cases;

/// <summary>
/// The shared cases of kind <c>documentChanges</c> (<c>tests/cases/sheet/document-changes.json</c>; ADR-0370 section 1's
/// note of 2026-10-30): <see cref="DocumentChangesReader"/> reads each document as the Script Core's
/// <c>DocumentChanges</c> writes it, against the one read before, as the document raised. Vitest holds the writer to
/// the same text.
/// </summary>
public class DocumentChangesCaseTests
{
    private const string One = """{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","cells":[{"at":"A1","number":1}]}]}""";

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (file, name) in SheetCases.Of("documentChanges"))
            rows.Add(file, name);
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0370: what changed in a document raised is read as the document raised
    public void Document_changes_case(string file, string name)
    {
        var (_, entry) = SheetCases.Find(file, name);
        var reader = new DocumentChangesReader();
        var at = 0;
        foreach (var step in entry["steps"]!.AsArray())
        {
            // The writer forgets; the reader is handed every Sheet whole next, which it reads whatever it read before.
            if (step!["forget"] is null)
            {
                var read = reader.Read(step["written"]!.ToJsonString());
                Assert.True(
                    WorkbookDocument.FromJson(step["raise"]!.ToJsonString()).ToJson() == read.ToJson(),
                    $"sheet/{file}, \"{name}\", step {at}: read as {read.ToJson()}");
            }
            at++;
        }
    }

    private static string Changes(int number, string sheet, string cells, string emptied) =>
        $$"""{"number":{{number}},"workbook":{"workbookVersion":1,"culture":"en-US","shown":"{{sheet}}","sheets":[{"name":"{{sheet}}","cells":[{{cells}}]}]},"emptied":[{{emptied}}]}""";

    [Fact] // ADR-0370: changes written against a document the reader did not read last, or to what it does not hold, are refused
    public void Changes_to_what_the_reader_did_not_read_last_are_refused()
    {
        DocumentChangesReader Opened()
        {
            var reader = new DocumentChangesReader();
            reader.Read($$"""{"number":1,"workbook":{{One}},"emptied":[null]}""");
            return reader;
        }
        // A Sheet the document before does not hold; a cell it does not hold; a number skipped, as when one was missed.
        Assert.Throws<FormatException>(() => Opened().Read(Changes(2, "Other", "", "[]")));
        Assert.Throws<FormatException>(() => Opened().Read(Changes(2, "Sheet1", "", "[\"B2\"]")));
        Assert.Throws<FormatException>(() => Opened().Read(Changes(3, "Sheet1", "", "[]")));
        Assert.Throws<FormatException>(() => Opened().Read(Changes(2, "Sheet1", "", "")));
        // Nothing read before.
        Assert.Throws<FormatException>(() => new DocumentChangesReader().Read(Changes(1, "Sheet1", "", "[]")));

        // Once refused, changes are refused until a document comes whole.
        var reader = Opened();
        Assert.Throws<FormatException>(() => reader.Read(Changes(3, "Sheet1", "", "[]")));
        Assert.Throws<FormatException>(() => reader.Read(Changes(4, "Sheet1", "", "[]")));
        var whole = reader.Read(Changes(5, "Sheet1", """{"at":"C3","number":3}""", "null"));
        Assert.Equal("C3", whole.Sheets[0].Cells.Single().Address.ToString());
        Assert.Empty(reader.Read(Changes(6, "Sheet1", "", "[\"C3\"]")).Sheets[0].Cells);
    }

    [Fact] // ADR-0331: a Workbook of one Sheet is that Sheet's Sheet Document, its Linked Tables declared on it; of several, none
    public void A_workbook_of_one_sheet_is_its_sheet_document_and_of_several_is_refused()
    {
        var one = WorkbookDocument.FromJson("""{"workbookVersion":1,"culture":"en-US","shown":"Prices","linkedTables":[{"name":"Positions","columns":["Id","PV"],"key":"Id"}],"sheets":[{"name":"Prices","columnWidths":[{"at":"B:B","width":20,"kind":"setByUser"}],"cells":[{"at":"A1","number":1}]}]}""");
        Assert.Equal(
            """{"version":9,"culture":"en-US","name":"Prices","linkedTables":[{"name":"Positions","columns":["Id","PV"],"key":"Id"}],"columnWidths":[{"at":"B:B","width":20,"kind":"setByUser"}],"cells":[{"at":"A1","number":1}]}""",
            one.ToSheetDocument().ToJson());
        var several = WorkbookDocument.FromJson("""{"workbookVersion":1,"culture":"en-US","shown":"A","sheets":[{"name":"A","cells":[]},{"name":"B","cells":[]}]}""");
        Assert.Throws<InvalidOperationException>(() => several.ToSheetDocument());
    }
}
