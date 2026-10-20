using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class WorkbookDocumentTests
{
    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    private static Workbook Reopened(Workbook workbook) =>
        Workbook.Open(WorkbookDocument.FromJson(workbook.ToDocument().ToJson()));

    [Fact] // ADR-0331: a Workbook Document carries its version, culture, Sheet shown and Sheets in order
    public void The_document_carries_version_culture_shown_and_sheets()
    {
        var workbook = new Workbook(EnUs, ["Inputs", "Totals"]);
        workbook.Shown = workbook.Sheets[1];

        var json = workbook.ToDocument().ToJson();

        Assert.Equal("""{"workbookVersion":1,"culture":"en-US","shown":"Totals","sheets":[{"name":"Inputs","cells":[]},{"name":"Totals","cells":[]}]}""", json);
    }

    [Fact] // ADR-0331, ADR-0048: Entries, Cell Formats, widths and the Sheet shown come back; every Value is computed again
    public void A_workbook_comes_back_from_its_document()
    {
        var workbook = new Workbook(CultureInfoOf("de-DE"), ["Rates", "Deposit"]);
        Enter(workbook, "Rates", "A1", "4,5%");
        Enter(workbook, "Deposit", "A1", "2500000");
        Enter(workbook, "Deposit", "A2", "=ROUND(A1*Rates!A1*92/360,2)");
        workbook.Do("Deposit", SheetEdit.SetNumberFormat([CellAddress.Parse("A2")], NumberFormat.Parse("#,##0.00")));
        workbook.Do("Rates", SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 20));
        workbook.Shown = workbook.Sheets[1];

        var reopened = Reopened(workbook);

        Assert.Equal("de-DE", reopened.Culture.Name);
        Assert.Equal(["Rates", "Deposit"], reopened.Sheets.Select(s => s.Name));
        Assert.Same(reopened.Sheets[1], reopened.Shown);
        Assert.Equal(28750, reopened.Sheets[1].Number("A2"));
        Assert.Equal("28.750,00", reopened.Sheets[1].GetDisplay(CellAddress.Parse("A2")).Text);
        Assert.Equal(20, reopened.Sheets[0].GetColumnWidth(0)!.Value.Width);
        Assert.Equal(0, reopened.Sheets[0].Changes);
        Assert.DoesNotContain("28750", workbook.ToDocument().ToJson());
    }

    [Fact] // ADR-0331, ADR-0049: the Linked Tables are recorded once, on the Workbook, and declared again waiting
    public void Linked_tables_are_recorded_on_the_workbook()
    {
        var workbook = new Workbook(EnUs, ["Sheet1", "Sheet2"]);
        workbook.DeclareLinkedTable("Trades", ["Id", "Amount"], ["Id"]);
        Enter(workbook, "Sheet2", "A1", "=SUM(Trades[Amount])");

        var document = workbook.ToDocument();
        var reopened = Workbook.Open(WorkbookDocument.FromJson(document.ToJson()));

        Assert.All(document.Sheets, s => Assert.Empty(s.LinkedTables));
        Assert.Equal(["Trades"], reopened.LinkedTables.Select(t => t.Name));
        Assert.Equal(["Id"], reopened.LinkedTables[0].KeyColumns);
        Assert.Equal(ErrorValue.GettingData, reopened.Sheets[1].Error("A1"));
    }

    [Theory] // ADR-0331, ADR-0048: an unknown version is refused by name, not guessed at
    [InlineData("""{"workbookVersion":2,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","cells":[]}]}""", 2)]
    [InlineData("""{"workbookVersion":0,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","cells":[]}]}""", 0)]
    public void An_unknown_version_is_refused(string json, int version)
    {
        var refusal = Assert.Throws<SheetDocumentException>(() => WorkbookDocument.FromJson(json));

        Assert.Equal(version, refusal.DocumentVersion);
        Assert.Contains($"version {version}", refusal.Message);
    }

    [Theory] // ADR-0331, ADR-0048: anything a Workbook Document does not define is refused by name
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","cells":[]}],"values":[]}""", "'values'")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","culture":"de-DE","cells":[]}]}""", "'culture'")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","version":9,"cells":[]}]}""", "'version'")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","linkedTables":[],"cells":[]}]}""", "'linkedTables'")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"name":"Sheet1","cells":[]},{"name":"SHEET1","cells":[]}]}""", "'SHEET1'")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet9","sheets":[{"name":"Sheet1","cells":[]}]}""", "'Sheet9'")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[]}""", "no Sheets")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","sheets":[{"name":"Sheet1","cells":[]}]}""", "no Sheet shown")]
    [InlineData("""{"workbookVersion":1,"shown":"Sheet1","sheets":[{"name":"Sheet1","cells":[]}]}""", "no culture")]
    [InlineData("""{"workbookVersion":1,"culture":"en-US","shown":"Sheet1","sheets":[{"cells":[]}]}""", "no name")]
    [InlineData("""{"workbookVersion":"1","culture":"en-US","shown":"Sheet1","sheets":[]}""", "version")]
    public void Anything_not_understood_is_refused_by_name(string json, string named)
    {
        var refusal = Assert.Throws<SheetDocumentException>(() => WorkbookDocument.FromJson(json));

        Assert.Contains(named, refusal.Message);
    }

    [Theory] // ADR-0331: a Sheet Document of every readable version opens as a Workbook of one Sheet
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void A_sheet_document_opens_as_a_workbook_of_one_sheet(int version)
    {
        var name = version >= 2 ? ""","name":"Positions" """.Trim() : "";
        var json = $$"""{"version":{{version}},"culture":"en-US"{{name}},"cells":[{"at":"A1","number":2},{"at":"A2","formula":"=A1*3"}]}""";

        var workbook = Workbook.Open(WorkbookDocument.FromJson(json));

        var sheet = Assert.Single(workbook.Sheets);
        Assert.Equal(version >= 2 ? "Positions" : Sheet.DefaultName, sheet.Name);
        Assert.Same(sheet, workbook.Shown);
        Assert.Equal(6, sheet.Number("A2"));
    }

    [Fact] // ADR-0331, ADR-0049: a Sheet Document's Linked Tables become the Workbook's
    public void A_sheet_documents_linked_tables_become_the_workbooks()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Trades", ["Id", "Amount"], "Id");

        var document = WorkbookDocument.FromJson(sheet.ToDocument().ToJson());

        Assert.Equal(["Trades"], document.LinkedTables.Select(t => t.Name));
        Assert.Empty(document.Sheets[0].LinkedTables);
        Assert.Equal(["Trades"], Workbook.Open(document).LinkedTables.Select(t => t.Name));
    }

    [Fact] // ADR-0331, ADR-0048: a Sheet Document of an unknown version is refused as a Sheet Document refuses it
    public void A_sheet_document_of_an_unknown_version_is_refused()
    {
        var refusal = Assert.Throws<SheetDocumentException>(() => WorkbookDocument.FromJson("""{"version":10,"culture":"en-US","name":"Sheet1","cells":[]}"""));

        Assert.Equal(10, refusal.DocumentVersion);
    }

    [Fact] // ADR-0331: the Sheet shown is one of the Workbook's
    public void Only_a_sheet_of_the_workbook_is_shown()
    {
        var workbook = new Workbook(EnUs, ["Sheet1", "Sheet2"]);

        Assert.Same(workbook.Sheets[0], workbook.Shown);
        Assert.Throws<ArgumentException>(() => workbook.Shown = NewSheet());
    }

    private static System.Globalization.CultureInfo CultureInfoOf(string name) => System.Globalization.CultureInfo.GetCultureInfo(name);
}
