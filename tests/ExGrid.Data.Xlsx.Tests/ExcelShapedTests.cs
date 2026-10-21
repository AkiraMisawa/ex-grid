using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// A package laid out as Excel 365 saves one, with what Excel adds that the reader does not read — markup compatibility,
/// extension lists, named cell styles, differential formats, a calculation chain, document properties — read for what it
/// carries and nothing else (ADR-0330 §4). The parts are written from Excel's own output; whether every reading agrees
/// with Excel is checked in a Windows run (ticket 07), not assumed here.
/// </summary>
public class ExcelShapedTests
{
    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/><Override PartName="/xl/calcChain.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.calcChain+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/><Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/></Types>
        """;

    private const string Rels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
        """;

    private const string WorkbookRelsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="theme/theme1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId6" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/calcChain" Target="calcChain.xml"/><Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/><Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
        """;

    private const string WorkbookPart = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="x15 xr xr6 xr10 xr2" xmlns:x15="http://schemas.microsoft.com/office/spreadsheetml/2010/11/main" xmlns:xr="http://schemas.microsoft.com/office/spreadsheetml/2014/revision" xmlns:xr6="http://schemas.microsoft.com/office/spreadsheetml/2016/revision6" xmlns:xr10="http://schemas.microsoft.com/office/spreadsheetml/2016/revision10" xmlns:xr2="http://schemas.microsoft.com/office/spreadsheetml/2015/revision2"><fileVersion appName="xl" lastEdited="7" lowestEdited="7" rupBuild="28322"/><workbookPr defaultThemeVersion="202300"/><mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"><mc:Choice Requires="x15"><x15ac:absPath url="C:\Users\me\Documents\" xmlns:x15ac="http://schemas.microsoft.com/office/spreadsheetml/2010/11/ac"/></mc:Choice></mc:AlternateContent><xr:revisionPtr revIDLastSave="0" documentId="8_{00000000-0000-0000-0000-000000000000}" xr6:coauthVersionLast="47" xr6:coauthVersionMax="47" xr10:uidLastSave="{00000000-0000-0000-0000-000000000000}"/><bookViews><workbookView xWindow="-110" yWindow="-110" windowWidth="25820" windowHeight="15500" activeTab="1" xr2:uid="{00000000-0000-0000-0000-000000000000}"/></bookViews><sheets><sheet name="Inputs" sheetId="1" r:id="rId1"/><sheet name="Book" sheetId="3" r:id="rId2"/></sheets><definedNames><definedName name="Rate">Inputs!$B$1</definedName></definedNames><calcPr calcId="191029"/><extLst><ext uri="{140A7094-0E35-4892-8432-C4D2E57EDEB5}" xmlns:x15="http://schemas.microsoft.com/office/spreadsheetml/2010/11/main"><x15:workbookPr chartTrackingRefBase="1"/></ext></extLst></workbook>
        """;

    private const string StylesPart = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="x14ac x16r2 xr" xmlns:x14ac="http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac" xmlns:x16r2="http://schemas.microsoft.com/office/spreadsheetml/2015/02/main" xmlns:xr="http://schemas.microsoft.com/office/spreadsheetml/2014/revision"><numFmts count="1"><numFmt numFmtId="164" formatCode="&quot;$&quot;#,##0.00"/></numFmts><fonts count="2" x14ac:knownFonts="1"><font><sz val="11"/><color theme="1"/><name val="Aptos Narrow"/><family val="2"/><scheme val="minor"/></font><font><b/><sz val="11"/><color rgb="FFFF0000"/><name val="Aptos Narrow"/><family val="2"/><scheme val="minor"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor theme="4" tint="0.79998168889431442"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color indexed="64"/></left><right style="thin"><color indexed="64"/></right><top style="thin"><color indexed="64"/></top><bottom style="double"><color indexed="64"/></bottom><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center"/></xf><xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles><dxfs count="1"><dxf><font><color rgb="FF9C0006"/></font><fill><patternFill><bgColor rgb="FFFFC7CE"/></patternFill></fill></dxf></dxfs><tableStyles count="0" defaultTableStyle="TableStyleMedium2" defaultPivotStyle="PivotStyleLight16"/><extLst><ext uri="{EB79DEF2-80B8-43e5-95BD-54CBDDF9020C}" xmlns:x14="http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"><x14:slicerStyles defaultSlicerStyle="SlicerStyleLight1"/></ext></extLst></styleSheet>
        """;

    private const string SharedStringsPart = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="3" uniqueCount="3"><si><t>Rate</t></si><si><t>Notional</t></si><si><t>Total</t></si></sst>
        """;

    private const string Sheet1 = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="x14ac xr xr2 xr3" xmlns:x14ac="http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac" xmlns:xr="http://schemas.microsoft.com/office/spreadsheetml/2014/revision" xmlns:xr2="http://schemas.microsoft.com/office/spreadsheetml/2015/revision2" xmlns:xr3="http://schemas.microsoft.com/office/spreadsheetml/2016/revision3" xr:uid="{00000000-0001-0000-0000-000000000000}"><dimension ref="A1:B1"/><sheetViews><sheetView workbookViewId="0"/></sheetViews><sheetFormatPr defaultRowHeight="14.5" x14ac:dyDescent="0.35"/><sheetData><row r="1" spans="1:2" x14ac:dyDescent="0.35"><c r="A1" t="s"><v>0</v></c><c r="B1"><v>0.0425</v></c></row></sheetData><pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/></worksheet>
        """;

    private const string Sheet2 = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="x14ac xr xr2 xr3" xmlns:x14ac="http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac" xmlns:xr="http://schemas.microsoft.com/office/spreadsheetml/2014/revision" xmlns:xr2="http://schemas.microsoft.com/office/spreadsheetml/2015/revision2" xmlns:xr3="http://schemas.microsoft.com/office/spreadsheetml/2016/revision3" xr:uid="{00000000-0001-0000-0100-000000000000}"><dimension ref="A1:C4"/><sheetViews><sheetView tabSelected="1" workbookViewId="0"><selection activeCell="C4" sqref="C4"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight="14.5" x14ac:dyDescent="0.35"/><cols><col min="1" max="1" width="12.36328125" customWidth="1"/><col min="2" max="3" width="10.90625" style="3" customWidth="1"/></cols><sheetData><row r="1" spans="1:3" x14ac:dyDescent="0.35"><c r="A1" s="2" t="s"><v>1</v></c><c r="B1" s="3"><v>46022</v></c></row><row r="2" spans="1:3" x14ac:dyDescent="0.35"><c r="A2" s="1"><v>1000</v></c><c r="B2" s="1"><f>A2*Rate</f><v>42.5</v></c></row><row r="3" spans="1:3" x14ac:dyDescent="0.35"><c r="A3" s="1"><v>2000</v></c><c r="B3" s="1"><f>A3*Rate</f><v>85</v></c></row><row r="4" spans="1:3" x14ac:dyDescent="0.35"><c r="A4" s="2" t="s"><v>2</v></c><c r="B4" s="1"><f>SUM(B2:B3)</f><v>127.5</v></c><c r="C4" t="str"><f>_xlfn.CONCAT(A4,":",B4)</f><v>Total:127.5</v></c></row></sheetData><conditionalFormatting sqref="B2:B3"><cfRule type="cellIs" dxfId="0" priority="1" operator="greaterThan"><formula>50</formula></cfRule></conditionalFormatting><pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/></worksheet>
        """;

    private static byte[] Package() => Zip(
        ("[Content_Types].xml", ContentTypesXml),
        ("_rels/.rels", Rels),
        ("docProps/app.xml", "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"/>"),
        ("docProps/core.xml", "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\"/>"),
        ("xl/workbook.xml", WorkbookPart),
        ("xl/_rels/workbook.xml.rels", WorkbookRelsXml),
        ("xl/styles.xml", StylesPart),
        ("xl/sharedStrings.xml", SharedStringsPart),
        ("xl/theme/theme1.xml", "<a:theme xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:themeElements><a:clrScheme name=\"Office\"><a:dk1><a:sysClr val=\"windowText\" lastClr=\"000000\"/></a:dk1><a:lt1><a:sysClr val=\"window\" lastClr=\"FFFFFF\"/></a:lt1><a:dk2><a:srgbClr val=\"0E2841\"/></a:dk2><a:lt2><a:srgbClr val=\"E8E8E8\"/></a:lt2><a:accent1><a:srgbClr val=\"156082\"/></a:accent1><a:accent2><a:srgbClr val=\"E97132\"/></a:accent2><a:accent3><a:srgbClr val=\"196B24\"/></a:accent3><a:accent4><a:srgbClr val=\"0F9ED5\"/></a:accent4><a:accent5><a:srgbClr val=\"A02B93\"/></a:accent5><a:accent6><a:srgbClr val=\"4EA72E\"/></a:accent6><a:hlink><a:srgbClr val=\"467886\"/></a:hlink><a:folHlink><a:srgbClr val=\"96607D\"/></a:folHlink></a:clrScheme></a:themeElements></a:theme>"),
        ("xl/calcChain.xml", "<calcChain xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><c r=\"B2\" i=\"2\"/></calcChain>"),
        ("xl/worksheets/sheet1.xml", Sheet1),
        ("xl/worksheets/sheet2.xml", Sheet2));

    [Fact] // ADR-0330 §3, §9: Excel's workbook part — markup compatibility, revision marks, an extension list — gives its sheets, the one shown and its names
    public async Task Excels_workbook_part_is_read()
    {
        using var reader = await OpenAsync(Package());

        Assert.Equal(["Inputs", "Book"], reader.Sheets.Select(s => s.Name));
        Assert.Equal([1, 3], reader.Sheets.Select(s => s.SheetId));
        Assert.Equal(["xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml"], reader.Sheets.Select(s => s.Part));
        Assert.Equal(1, reader.ActiveSheet);
        Assert.False(reader.Date1904);
        Assert.Equal([new XlsxDefinedName("Rate", "Inputs!$B$1")], reader.DefinedNames);
        Assert.Contains("xl/calcChain.xml", reader.Parts);
    }

    [Fact] // ADR-0330 §4, §9: Excel's cells — spans, descents, a stored Value beside each Formula — give their places, values, Formulas and styles
    public async Task Excels_cells_are_read()
    {
        using var reader = await OpenAsync(Package());

        var book = await reader.ReadSheetAsync(1);

        Assert.Equal(
        [
            new XlsxCell(0, 0, XlsxCellType.SharedString, "1", "Notional", StyleIndex: 2),
            new XlsxCell(0, 1, XlsxCellType.Number, "46022", StyleIndex: 3),
            new XlsxCell(1, 0, XlsxCellType.Number, "1000", StyleIndex: 1),
            new XlsxCell(1, 1, XlsxCellType.Number, "42.5", Formula: new XlsxFormula("A2*Rate"), StyleIndex: 1),
            new XlsxCell(2, 0, XlsxCellType.Number, "2000", StyleIndex: 1),
            new XlsxCell(2, 1, XlsxCellType.Number, "85", Formula: new XlsxFormula("A3*Rate"), StyleIndex: 1),
            new XlsxCell(3, 0, XlsxCellType.SharedString, "2", "Total", StyleIndex: 2),
            new XlsxCell(3, 1, XlsxCellType.Number, "127.5", Formula: new XlsxFormula("SUM(B2:B3)"), StyleIndex: 1),
            new XlsxCell(3, 2, XlsxCellType.FormulaString, "Total:127.5", Formula: new XlsxFormula("_xlfn.CONCAT(A4,\":\",B4)")),
        ], book.Cells);
        Assert.Equal([new XlsxColumn(0, 0, 12.36328125), new XlsxColumn(1, 2, 10.90625, StyleIndex: 3)], book.Columns);
        Assert.Equal(["conditionalFormatting"], book.OtherElements);
    }

    [Fact] // ADR-0330 §4, §8: Excel's styles part — named styles, differential formats, extension lists — gives what §4 carries, and its theme resolves the fill
    public async Task Excels_styles_are_read()
    {
        using var reader = await OpenAsync(Package());
        var styles = reader.Styles;

        Assert.Equal("\"$\"#,##0.00", styles.NumberFormats[164]);
        Assert.Equal(new XlsxFont("Aptos Narrow", 11, Bold: true, Color: XlsxColor.FromArgb(0xFFFF0000)), styles.Fonts[1]);
        Assert.Equal(XlsxFill.Solid(XlsxColor.FromTheme(4, 0.79998168889431442)) with { Background = XlsxColor.FromIndexed(64) }, styles.Fills[2]);
        Assert.Equal(XlsxBorderStyle.Double, styles.Borders[1].Bottom.Style);
        Assert.Equal(XlsxColor.FromIndexed(64), styles.Borders[1].Left.Color);
        Assert.Equal(new XlsxCellFormat(0, 1, 2, 1, new XlsxAlignment(XlsxHorizontalAlignment.Center)), styles.CellFormats[2]);
        Assert.Equal(new XlsxCellFormat(14), styles.CellFormats[3]);
        Assert.Equal(4, styles.CellFormats.Count);
        Assert.Equal(0xFF156082u, reader.Theme.SlotColor(4));
        Assert.Equal(XlsxTheme.ApplyTint(0xFF156082, 0.79998168889431442), reader.Theme.Resolve(styles.Fills[2].Foreground!.Value));
    }
}
