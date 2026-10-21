using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>The Open Report: what was not carried, and the cells naming functions ExSheet does not support (ADR-0330 §4, §5).</summary>
public class OpenReportTests
{
    private const string Sheet1 = "xl/worksheets/sheet1.xml";

    [Fact] // ADR-0330 §5: a workbook carried whole has an empty Open Report
    public async Task ADR0330_a_workbook_carried_whole_has_an_empty_report()
    {
        var file = await WriteAsync(new Page("Sheet1", [Number("A1", "1"), Formula("A2", "A1+1")]));

        var report = (await ReadAsync(file)).Report;

        Assert.True(report.IsEmpty);
        Assert.Empty(report.NotCarried);
        Assert.Empty(report.UnsupportedFunctions);
    }

    [Fact] // ADR-0330 §4, §5: a Formula naming a function ExSheet does not support is carried, shows #NAME?, and is listed with its Sheet and cell
    public async Task ADR0330_cells_naming_unsupported_functions_are_carried_and_listed()
    {
        var file = await WriteAsync(new Page("Trades",
        [
            Number("A1", "1"),
            Formula("B2", "CUBEVALUE(\"x\",A1)+SUM(A1)+_xlfn.FIELDVALUE(A1,\"Price\")"),
            Formula("C2", "") with { Formula = new XlsxFormula("MYADDIN(A1)", XlsxFormulaKind.Shared, "C2:C3", 0) },
            Formula("C3", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
        ]));

        var (document, report) = await ReadAsync(file);

        Assert.Equal(["Trades!B2: CUBEVALUE, FIELDVALUE", "Trades!C2: MYADDIN", "Trades!C3: MYADDIN"],
            report.UnsupportedFunctions.Select(u => $"{u.Sheet}!{u.Cell}: {string.Join(", ", u.Functions)}"));
        Assert.Equal("=MYADDIN(A2)", document.Sheet("Trades").EntryAt("C3")!.Formula);
        Assert.Equal(Value.FromError(ErrorValue.Name), Workbook.Open(document).Sheets[0].GetValue(CellAddress.Parse("C2")));
        Assert.False(report.IsEmpty);
    }

    [Fact] // ADR-0330 §4: a hidden Sheet, a chart sheet, hidden columns, and defined names no Formula reads are listed
    public async Task ADR0330_hidden_sheets_columns_and_defined_names_are_listed()
    {
        var file = await WriteAsync(new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Rate", "Trades!$A$1"), new XlsxDefinedName("_xlnm.Print_Area", "Trades!$A$1:$B$2", 0)] },
            new Page("Trades", [Number("A1", "1")], [new XlsxColumn(1, 2, 10, Hidden: true)]),
            new Page("Back", [Number("A1", "1")], Visibility: XlsxSheetVisibility.Hidden));

        var report = (await ReadAsync(file)).Report;

        Assert.Contains(report.NotCarried, n => n is { Kind: NotCarriedKind.HiddenColumns, Sheet: "Trades" });
        Assert.Contains(report.NotCarried, n => n is { Kind: NotCarriedKind.HiddenSheet, Sheet: "Back" });
        Assert.Contains(report.NotCarried, n => n is { Kind: NotCarriedKind.DefinedName, Sheet: null, Detail: "Rate" });
        Assert.Contains(report.NotCarried, n => n is { Kind: NotCarriedKind.DefinedName, Detail: "_xlnm.Print_Area" });
        Assert.All(report.NotCarried, n => Assert.False(string.IsNullOrWhiteSpace(n.Description)));
    }

    [Fact] // ADR-0330 §4: what a worksheet holds beside its cells is listed, once per Sheet and kind, in the file's order
    public async Task ADR0330_what_a_worksheet_holds_beside_its_cells_is_listed()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1"), Number("B2", "2"), Number("A3", "3")]));
        var edited = Edit(file, Sheet1, xml => AfterCells(
                """<sheetProtection sheet="1"/><autoFilter ref="A1:B2"/><mergeCells count="2"><mergeCell ref="A1:B1"/><mergeCell ref="A3:B3"/></mergeCells>"""
                + """<customSheetViews><customSheetView guid="{00000000-0000-0000-0000-000000000001}"/></customSheetViews><conditionalFormatting sqref="A1"><cfRule type="expression" priority="1"><formula>TRUE</formula></cfRule></conditionalFormatting>"""
                + """<dataValidations count="1"><dataValidation sqref="A1"/></dataValidations><hyperlinks><hyperlink ref="A1" r:id="rId9"/></hyperlinks>"""
                + """<printOptions gridLines="1"/><pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/><pageSetup orientation="landscape"/>"""
                + """<drawing r:id="rId1"/><legacyDrawing r:id="rId2"/><tableParts count="1"><tablePart r:id="rId3"/></tableParts>""")(xml)
            .Replace("workbookViewId=\"0\" />", "workbookViewId=\"0\"><pane xSplit=\"1\" topLeftCell=\"B1\" activePane=\"topRight\" state=\"frozen\"/></sheetView>", StringComparison.Ordinal)
            .Replace("<row r=\"2\"", "<row r=\"2\" hidden=\"1\"", StringComparison.Ordinal)
            .Replace("<row r=\"3\"", "<row r=\"3\" ht=\"30\" customHeight=\"1\"", StringComparison.Ordinal));

        var report = (await ReadAsync(edited)).Report;

        Assert.Equal(
        [
            NotCarriedKind.RowHeights, NotCarriedKind.HiddenRows, NotCarriedKind.FrozenPanes, NotCarriedKind.Protection, NotCarriedKind.AutoFilter,
            NotCarriedKind.MergedCells, NotCarriedKind.Other, NotCarriedKind.ConditionalFormatting, NotCarriedKind.DataValidation,
            NotCarriedKind.Hyperlinks, NotCarriedKind.PrintSettings, NotCarriedKind.PicturesAndCharts, NotCarriedKind.NotesAndComments,
            NotCarriedKind.ExcelTable,
        ], report.NotCarried.Select(n => n.Kind));
        Assert.All(report.NotCarried, n => Assert.Equal("Trades", n.Sheet));
        Assert.Equal("customSheetViews", report.NotCarried.Single(n => n.Kind == NotCarriedKind.Other).Detail);
    }

    [Fact] // ADR-0330 §4, §5: what every file Excel writes carries and no Sheet shows is not listed, so a plain file's report is empty
    public async Task ADR0330_what_every_file_carries_and_no_Sheet_shows_is_not_listed()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1"), Text("A2", "0042")]));
        var edited = Edit(file, Sheet1, xml => AfterCells(
                """<phoneticPr fontId="1"/><printOptions gridLinesSet="1"/><pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/>"""
                + """<pageSetup paperSize="9" orientation="portrait" horizontalDpi="300" verticalDpi="300"/><headerFooter/>"""
                + """<ignoredErrors><ignoredError sqref="A2" numberStoredAsText="1"/></ignoredErrors>""")(xml)
            .Replace("<sheetData>", """<sheetFormatPr defaultRowHeight="14.5"/><sheetData>""", StringComparison.Ordinal));

        var report = (await ReadAsync(edited)).Report;

        Assert.Empty(report.NotCarried);
        Assert.True(report.IsEmpty);
    }

    [Fact] // ADR-0330 §4: a tab colour, a footer and an extension's conditional formatting are listed; each changes how the Sheet looks or prints
    public async Task ADR0330_a_tab_colour_a_footer_and_extension_formatting_are_listed()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1")]));
        var edited = Edit(file, Sheet1, xml => AfterCells(
                """<headerFooter><oddFooter>&amp;P</oddFooter></headerFooter>"""
                + """<extLst><ext uri="{78C0D931-6437-407d-A8EE-F0AAD7539E65}" xmlns:x14="http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"><x14:conditionalFormattings/></ext></extLst>""")(xml)
            .Replace("<sheetViews>", """<sheetPr><tabColor rgb="FFFF0000"/></sheetPr><sheetViews>""", StringComparison.Ordinal));

        var report = (await ReadAsync(edited)).Report;

        Assert.Equal([NotCarriedKind.SheetProperties, NotCarriedKind.PrintSettings, NotCarriedKind.ConditionalFormatting], report.NotCarried.Select(n => n.Kind));
    }

    [Fact] // ADR-0330 §5: the workbook's entries come first, its defined names among them, then each Sheet's
    public async Task ADR0330_the_workbooks_entries_come_before_each_Sheets()
    {
        var file = await WriteAsync(new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Rate", "Trades!$A$1")] },
            new Page("Trades", [Number("A1", "1")], [new XlsxColumn(1, 2, 10, Hidden: true)]));
        var edited = Add(file, "xl/pivotTables/pivotTable1.xml", "<x/>");

        var report = (await ReadAsync(edited)).Report;

        Assert.Equal([NotCarriedKind.PivotTables, NotCarriedKind.DefinedName, NotCarriedKind.HiddenColumns], report.NotCarried.Select(n => n.Kind));
    }

    [Fact] // ADR-0330 §4: rich-text runs are listed; their text is carried
    public async Task ADR0330_rich_text_is_carried_as_its_text_and_listed()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1"), Inline("B2", "Rich")]));
        var edited = Edit(file, Sheet1, xml => System.Text.RegularExpressions.Regex.Replace(xml, "<is><t[^>]*>Rich</t></is>",
            "<is><r><rPr><b/></rPr><t>Ri</t></r><r><t>ch</t></r></is>"));

        var (document, report) = await ReadAsync(edited);

        Assert.Equal(Value.FromText("Rich"), document.Sheet("Trades").EntryAt("B2")!.Constant);
        var rich = Assert.Single(report.NotCarried);
        Assert.Equal((NotCarriedKind.RichText, "Trades", "B2"), (rich.Kind, rich.Sheet, rich.Detail));
    }

    [Fact] // ADR-0330 §4: what the workbook holds beside its sheets is listed from its parts
    public async Task ADR0330_what_the_workbook_holds_beside_its_sheets_is_listed()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1")]));
        var edited = Add(Add(Add(file, "xl/pivotTables/pivotTable1.xml", "<x/>"), "xl/externalLinks/externalLink1.xml", "<x/>"), "xl/connections.xml", "<x/>");

        var report = (await ReadAsync(edited)).Report;

        Assert.Equal([NotCarriedKind.PivotTables, NotCarriedKind.ExternalLinks, NotCarriedKind.DataConnections], report.NotCarried.Select(n => n.Kind));
        Assert.All(report.NotCarried, n => Assert.Null(n.Sheet));
    }
}
