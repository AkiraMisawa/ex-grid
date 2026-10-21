using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// A Workbook written to a file (ADR-0330 §6, ticket 04): read back by the reader, and checked part by part against what
/// Excel requires of a package — its parts, content types and relationships, each part's elements in the schema's order,
/// text in the shared strings, every Formula with its Value and the workbook marked for full calculation on load.
/// </summary>
public class WriterTests
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace S = Main;
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    private static CellAddress At(string at) => CellAddress.Parse(at);

    /// <summary>A Workbook of the given Sheets, each cell typed as a user types it.</summary>
    private static Workbook Typed(params (string Sheet, (string At, string Typed)[] Cells)[] sheets)
    {
        var workbook = new Workbook(EnUs, sheets.Select(s => s.Sheet));
        foreach (var (name, cells) in sheets)
        {
            foreach (var (at, typed) in cells) workbook.Do(name, SheetEdit.Enter(At(at), typed));
        }
        return workbook;
    }

    private static async Task<byte[]> SaveAsync(Workbook workbook, XlsxSaveOptions? options = null) => await Saved.WriteAsync(workbook, options);

    private static Dictionary<string, XDocument> Parts(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var stream = e.Open();
            return XDocument.Load(stream);
        });
    }

    /// <summary>The worksheet part's <c>c</c> element at <paramref name="at"/>.</summary>
    private static XElement CellElement(XDocument sheet, string at) =>
        sheet.Descendants(S + "c").Single(c => (string?)c.Attribute("r") == at);

    /// <summary>Whether the children's names come in the order the schema's sequence lists them, each one the schema has.</summary>
    private static void InSchemaOrder(XElement element, params string[] sequence)
    {
        var at = -1;
        foreach (var child in element.Elements())
        {
            var index = Array.IndexOf(sequence, child.Name.LocalName);
            Assert.True(index >= 0, $"<{element.Name.LocalName}> holds <{child.Name.LocalName}>, which its schema does not have.");
            Assert.True(index >= at, $"<{element.Name.LocalName}> holds <{child.Name.LocalName}> after an element the schema puts after it.");
            at = index;
        }
    }

    // CT_Worksheet, CT_Workbook, CT_Stylesheet, CT_Border, CT_Cell and CT_Row, in ECMA-376 Part 1's order.
    private static readonly string[] WorksheetOrder =
    [
        "sheetPr", "dimension", "sheetViews", "sheetFormatPr", "cols", "sheetData", "sheetCalcPr", "sheetProtection", "protectedRanges",
        "scenarios", "autoFilter", "sortState", "dataConsolidate", "customSheetViews", "mergeCells", "phoneticPr", "conditionalFormatting",
        "dataValidations", "hyperlinks", "printOptions", "pageMargins", "pageSetup", "headerFooter", "rowBreaks", "colBreaks", "customProperties",
        "cellWatches", "ignoredErrors", "smartTags", "drawing", "legacyDrawing", "legacyDrawingHF", "picture", "oleObjects", "controls",
        "webPublishItems", "tableParts", "extLst",
    ];

    private static readonly string[] WorkbookOrder =
    [
        "fileVersion", "fileSharing", "workbookPr", "workbookProtection", "bookViews", "sheets", "functionGroups", "externalReferences",
        "definedNames", "calcPr", "oleSize", "customWorkbookViews", "pivotCaches", "smartTagPr", "smartTagTypes", "webPublishing",
        "fileRecoveryPr", "webPublishObjects", "extLst",
    ];

    private static readonly string[] StylesheetOrder =
        ["numFmts", "fonts", "fills", "borders", "cellStyleXfs", "cellXfs", "cellStyles", "dxfs", "tableStyles", "colors", "extLst"];

    private static readonly string[] BorderOrder = ["start", "end", "left", "right", "top", "bottom", "diagonal", "vertical", "horizontal"];

    /// <summary>A Workbook that holds a little of everything a file carries: two Sheets, constants of every kind, Formulas that read across them, a spill, and formats.</summary>
    private static Workbook Everything()
    {
        var workbook = Typed(
            ("Trades",
            [
                ("A1", "Desk"), ("B1", "Notional"), ("A2", "EMEA"), ("B2", "1000000.25"), ("A3", "APAC"), ("B3", "250000"),
                ("C2", "=B2*2"), ("C3", "=XLOOKUP(\"APAC\",A2:A3,B2:B3)"), ("D2", "=SEQUENCE(3)"), ("E2", "=SUM(D2#)"),
                ("F2", "TRUE"), ("F3", "#N/A"), ("G2", "=A2&\" desk\""), ("G3", "=1/0"), ("H2", "=LET(x,B2,y,B3,x+y)"),
                ("H3", "=Desks!A1*2"), ("I2", "=SORT(B2:B3)"), ("J2", "=IFS(B2>0,\"long\",TRUE,\"short\")"),
            ]),
            ("Desks", [("A1", "21"), ("B1", "EMEA"), ("C1", "=COUNTIF(Trades!A2:A3,B1)")]));
        workbook.Do("Trades", SheetEdit.SetNumberFormat(new CellRange(At("B2"), At("C3")), NumberFormat.Parse("#,##0.00")));
        workbook.Do("Trades", SheetEdit.SetCellFormat([new CellRange(At("A1"), At("J1"))], new CellFormatChange { Bold = true }));
        workbook.Shown = workbook.Sheets[1];
        return workbook;
    }

    [Fact] // ADR-0330 §6: the package holds the parts Excel requires, each with its content type and relationship
    public async Task ADR0330_the_package_holds_its_parts_with_their_content_types_and_relationships()
    {
        var parts = Parts(await SaveAsync(Everything()));

        Assert.Equal(
            ["[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/metadata.xml", "xl/sharedStrings.xml", "xl/styles.xml", "xl/workbook.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml"],
            parts.Keys.Order(StringComparer.Ordinal));
        var types = parts["[Content_Types].xml"].Root!;
        Assert.Equal(ContentTypes + "Types", types.Name);
        Assert.Equal("application/vnd.openxmlformats-package.relationships+xml", types.Elements(ContentTypes + "Default").Single(d => (string?)d.Attribute("Extension") == "rels").Attribute("ContentType")!.Value);
        Assert.Equal("application/xml", types.Elements(ContentTypes + "Default").Single(d => (string?)d.Attribute("Extension") == "xml").Attribute("ContentType")!.Value);
        var overrides = types.Elements(ContentTypes + "Override").ToDictionary(o => o.Attribute("PartName")!.Value, o => o.Attribute("ContentType")!.Value);
        Assert.Equal(new Dictionary<string, string>
        {
            ["/xl/workbook.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            ["/xl/worksheets/sheet1.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml",
            ["/xl/worksheets/sheet2.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml",
            ["/xl/styles.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml",
            ["/xl/sharedStrings.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml",
            ["/xl/metadata.xml"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheetMetadata+xml",
        }, overrides);

        var root = parts["_rels/.rels"].Root!.Elements(PackageRelationships + "Relationship").Single();
        Assert.Equal(("http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml"), (root.Attribute("Type")!.Value, root.Attribute("Target")!.Value));
        var relationships = parts["xl/_rels/workbook.xml.rels"].Root!.Elements(PackageRelationships + "Relationship")
            .ToDictionary(r => r.Attribute("Id")!.Value, r => (Type: r.Attribute("Type")!.Value, Target: r.Attribute("Target")!.Value));
        Assert.Equal(relationships.Count, relationships.Keys.Distinct().Count());
        const string Office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        Assert.Equal(
            [(Office + "sharedStrings", "sharedStrings.xml"), (Office + "sheetMetadata", "metadata.xml"), (Office + "styles", "styles.xml"), (Office + "worksheet", "worksheets/sheet1.xml"), (Office + "worksheet", "worksheets/sheet2.xml")],
            relationships.Values.Order());
        // Each sheet names its part's relationship.
        var sheets = parts["xl/workbook.xml"].Root!.Element(S + "sheets")!.Elements(S + "sheet").ToList();
        Assert.Equal(["Trades", "Desks"], sheets.Select(s => s.Attribute("name")!.Value));
        Assert.Equal(["worksheets/sheet1.xml", "worksheets/sheet2.xml"], sheets.Select(s => relationships[s.Attribute(R + "id")!.Value].Target));
        Assert.Equal(2, sheets.Select(s => s.Attribute("sheetId")!.Value).Distinct().Count());
    }

    [Fact] // ADR-0330 §6: each part's elements come in the order its schema's sequence gives them
    public async Task ADR0330_every_part_is_written_in_its_schemas_order()
    {
        var parts = Parts(await SaveAsync(Everything()));

        Assert.Equal(S + "workbook", parts["xl/workbook.xml"].Root!.Name);
        InSchemaOrder(parts["xl/workbook.xml"].Root!, WorkbookOrder);
        Assert.Equal(S + "styleSheet", parts["xl/styles.xml"].Root!.Name);
        InSchemaOrder(parts["xl/styles.xml"].Root!, StylesheetOrder);
        foreach (var border in parts["xl/styles.xml"].Descendants(S + "border")) InSchemaOrder(border, BorderOrder);
        foreach (var xf in parts["xl/styles.xml"].Descendants(S + "xf")) InSchemaOrder(xf, "alignment", "protection", "extLst");
        foreach (var name in new[] { "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml" })
        {
            var worksheet = parts[name].Root!;
            Assert.Equal(S + "worksheet", worksheet.Name);
            InSchemaOrder(worksheet, WorksheetOrder);
            var rows = worksheet.Element(S + "sheetData")!.Elements(S + "row").ToList();
            // Rows ascend, and the cells in each ascend by column, each naming its own row.
            Assert.Equal(rows.Select(r => int.Parse(r.Attribute("r")!.Value, CultureInfo.InvariantCulture)).Order(), rows.Select(r => int.Parse(r.Attribute("r")!.Value, CultureInfo.InvariantCulture)));
            foreach (var row in rows)
            {
                foreach (var cell in row.Elements())
                {
                    Assert.Equal(S + "c", cell.Name);
                    InSchemaOrder(cell, "f", "v", "is", "extLst");
                    Assert.True(XlsxCellReference.TryParse(cell.Attribute("r")!.Value, out var r, out _));
                    Assert.Equal(row.Attribute("r")!.Value, (r + 1).ToString(CultureInfo.InvariantCulture));
                }
                var columns = row.Elements().Select(c => { XlsxCellReference.TryParse(c.Attribute("r")!.Value, out _, out var column); return column; }).ToList();
                Assert.Equal(columns.Order(), columns);
                Assert.Equal(columns.Count, columns.Distinct().Count());
            }
        }
        var styles = parts["xl/styles.xml"].Root!;
        var xfs = styles.Element(S + "cellXfs")!.Elements(S + "xf").Count();
        Assert.All(parts["xl/worksheets/sheet1.xml"].Descendants(S + "c"), c => Assert.InRange(int.Parse((string?)c.Attribute("s") ?? "0", CultureInfo.InvariantCulture), 0, xfs - 1));
        Assert.Equal(xfs.ToString(CultureInfo.InvariantCulture), styles.Element(S + "cellXfs")!.Attribute("count")!.Value);
    }

    [Fact] // ADR-0330 §6 (Q4): the workbook is marked to be calculated in full when Excel opens it
    public async Task ADR0330_the_workbook_is_marked_for_full_calculation_on_load()
    {
        var workbook = Parts(await SaveAsync(Everything()))["xl/workbook.xml"].Root!;

        Assert.Equal("1", workbook.Element(S + "calcPr")!.Attribute("fullCalcOnLoad")!.Value);
        Assert.Null(workbook.Element(S + "workbookPr")?.Attribute("date1904"));
    }

    [Fact] // ADR-0330 §3, §6: every Sheet is written, in order, and the Sheet shown is the worksheet shown
    public async Task ADR0330_every_sheet_is_written_in_order_with_the_shown_one_shown()
    {
        var parts = Parts(await SaveAsync(Everything()));

        Assert.Equal("1", parts["xl/workbook.xml"].Root!.Element(S + "bookViews")!.Element(S + "workbookView")!.Attribute("activeTab")!.Value);
        Assert.Null(parts["xl/worksheets/sheet1.xml"].Descendants(S + "sheetView").Single().Attribute("tabSelected"));
        Assert.Equal("1", parts["xl/worksheets/sheet2.xml"].Descendants(S + "sheetView").Single().Attribute("tabSelected")!.Value);
    }

    [Fact] // ADR-0330 §6 (Q4): every Formula is written with the Value it shows, so a program that does not calculate reads what the user saw
    public async Task ADR0330_every_formula_is_written_with_its_value()
    {
        var sheet = Parts(await SaveAsync(Everything()))["xl/worksheets/sheet1.xml"];

        XElement Cell(string at) => CellElement(sheet, at);
        Assert.Equal(("B2*2", "2000000.5", null), (Cell("C2").Element(S + "f")!.Value, Cell("C2").Element(S + "v")!.Value, (string?)Cell("C2").Attribute("t")));
        Assert.Equal("250000", Cell("C3").Element(S + "v")!.Value);
        Assert.Equal(("str", "EMEA desk"), ((string?)Cell("G2").Attribute("t"), Cell("G2").Element(S + "v")!.Value));
        Assert.Equal(("e", "#DIV/0!"), ((string?)Cell("G3").Attribute("t"), Cell("G3").Element(S + "v")!.Value));
        Assert.Equal("1250000.25", Cell("H2").Element(S + "v")!.Value);
        Assert.Equal("42", Cell("H3").Element(S + "v")!.Value);
        Assert.Equal("6", Cell("E2").Element(S + "v")!.Value);
        Assert.Equal(("str", "long"), ((string?)Cell("J2").Attribute("t"), Cell("J2").Element(S + "v")!.Value));
        Assert.Equal(("b", "1"), ((string?)Cell("F2").Attribute("t"), Cell("F2").Element(S + "v")!.Value));
        Assert.Equal(("e", "#N/A"), ((string?)Cell("F3").Attribute("t"), Cell("F3").Element(S + "v")!.Value));
        Assert.Equal("1000000.25", Cell("B2").Element(S + "v")!.Value);
        Assert.Null(Cell("B2").Element(S + "f"));
    }

    [Fact] // ADR-0330 §6, ADR-0180: the Values written are the ones on screen, RAND()'s included
    public async Task ADR0330_the_values_written_are_the_ones_shown_rands_included()
    {
        var workbook = new Workbook(EnUs);
        workbook.SetSeed(20261021);
        workbook.Do("Sheet1", SheetEdit.Enter(new Dictionary<CellAddress, string> { [At("A1")] = "=RAND()", [At("A2")] = "=RANDBETWEEN(1,1000000)", [At("A3")] = "=A1*2" }));
        var shown = new[] { "A1", "A2", "A3" }.Select(a => workbook.Sheets[0].GetValue(At(a))!.Value.Number.ToString("R", CultureInfo.InvariantCulture)).ToList();

        var sheet = Parts(await SaveAsync(workbook))["xl/worksheets/sheet1.xml"];

        Assert.Equal(shown, new[] { "A1", "A2", "A3" }.Select(a => CellElement(sheet, a).Element(S + "v")!.Value));
    }

    [Fact] // ADR-0330 §4, §6: text is written into the shared strings, each text once; a Formula's text result in its cell
    public async Task ADR0330_text_is_written_into_the_shared_strings()
    {
        var parts = Parts(await SaveAsync(Everything()));

        var sst = parts["xl/sharedStrings.xml"].Root!;
        var texts = sst.Elements(S + "si").Select(si => si.Element(S + "t")!.Value).ToList();
        Assert.Equal(["Desk", "Notional", "EMEA", "APAC"], texts);
        Assert.Equal("4", sst.Attribute("uniqueCount")!.Value);
        Assert.Equal("5", sst.Attribute("count")!.Value);
        var trades = parts["xl/worksheets/sheet1.xml"];
        Assert.Equal(("s", "2"), ((string?)CellElement(trades, "A2").Attribute("t"), CellElement(trades, "A2").Element(S + "v")!.Value));
        Assert.Equal(("s", "2"), ((string?)CellElement(parts["xl/worksheets/sheet2.xml"], "B1").Attribute("t"), CellElement(parts["xl/worksheets/sheet2.xml"], "B1").Element(S + "v")!.Value));
        Assert.DoesNotContain("EMEA desk", texts);
    }

    [Fact] // ADR-0330 §4: Excel's stored prefixes are put back: _xlfn. before a newer function, _xlfn._xlws. before FILTER and SORT, _xlpm. before a LET's names
    public async Task ADR0330_the_stored_prefixes_are_put_back()
    {
        var sheet = Parts(await SaveAsync(Everything()))["xl/worksheets/sheet1.xml"];

        string Stored(string at) => CellElement(sheet, at).Element(S + "f")!.Value;
        Assert.Equal("_xlfn.XLOOKUP(\"APAC\",A2:A3,B2:B3)", Stored("C3"));
        Assert.Equal("_xlfn.SEQUENCE(3)", Stored("D2"));
        Assert.Equal("SUM(_xlfn.ANCHORARRAY(D2))", Stored("E2"));
        Assert.Equal("_xlfn.LET(_xlpm.x,B2,_xlpm.y,B3,_xlpm.x+_xlpm.y)", Stored("H2"));
        Assert.Equal("_xlfn._xlws.SORT(B2:B3)", Stored("I2"));
        Assert.Equal("_xlfn.IFS(B2>0,\"long\",TRUE,\"short\")", Stored("J2"));
        Assert.Equal("Desks!A1*2", Stored("H3"));
    }

    [Fact] // ADR-0330 §4, ADR-0125: every Formula is a dynamic array over the cells it spills into, which hold their Values, so Excel computes it as the engine does
    public async Task ADR0330_every_formula_is_written_as_a_dynamic_array_over_its_spill()
    {
        var parts = Parts(await SaveAsync(Everything()));
        var sheet = parts["xl/worksheets/sheet1.xml"];

        var anchor = CellElement(sheet, "D2");
        Assert.Equal(("array", "D2:D4", "1"), ((string?)anchor.Element(S + "f")!.Attribute("t"), (string?)anchor.Element(S + "f")!.Attribute("ref"), (string?)anchor.Attribute("cm")));
        Assert.Equal(["1", "2", "3"], new[] { "D2", "D3", "D4" }.Select(a => CellElement(sheet, a).Element(S + "v")!.Value));
        Assert.Null(CellElement(sheet, "D3").Element(S + "f"));
        // A Formula that does not spill is a dynamic array of its own cell, so Excel does not intersect the range it reads.
        var single = CellElement(sheet, "C2");
        Assert.Equal(("array", "C2", "1"), ((string?)single.Element(S + "f")!.Attribute("t"), (string?)single.Element(S + "f")!.Attribute("ref"), (string?)single.Attribute("cm")));
        Assert.All(sheet.Descendants(S + "f"), f => Assert.Equal("array", (string?)f.Attribute("t")));
        // The metadata part marks cell metadata 1 as a dynamic array, as Excel 365 writes it.
        var metadata = parts["xl/metadata.xml"].Root!;
        Assert.Equal("XLDAPR", metadata.Element(S + "metadataTypes")!.Element(S + "metadataType")!.Attribute("name")!.Value);
        Assert.Equal("1", metadata.Element(S + "cellMetadata")!.Attribute("count")!.Value);
        XNamespace xda = "http://schemas.microsoft.com/office/spreadsheetml/2017/dynamicarray";
        var ext = metadata.Element(S + "futureMetadata")!.Element(S + "bk")!.Element(S + "extLst")!.Element(S + "ext")!;
        Assert.Equal("{bdbb8cdc-fa1e-496e-a857-3c3f30c029c3}", (string?)ext.Attribute("uri"));
        Assert.Equal("1", (string?)ext.Element(xda + "dynamicArrayProperties")!.Attribute("fDynamic"));
        var rc = metadata.Element(S + "cellMetadata")!.Element(S + "bk")!.Element(S + "rc")!;
        Assert.Equal(("1", "0"), ((string?)rc.Attribute("t"), (string?)rc.Attribute("v")));
    }

    [Fact] // ADR-0330 §6: the file reads back as the Workbook it was written from, every Value computed again
    public async Task ADR0330_a_written_workbook_reads_back_as_it_was()
    {
        var before = Everything();

        var (document, report) = await ReadAsync(await SaveAsync(before));

        Assert.True(report.IsEmpty, string.Join(" ", report.NotCarried.Select(n => n.Description)));
        Assert.Equal(before.ToDocument().ToJson(), document.ToJson());
        var after = Workbook.Open(document);
        for (var s = 0; s < before.Sheets.Count; s++)
        {
            foreach (var at in before.Sheets[s].ValueAddresses.Order())
                Assert.Equal((at, before.Sheets[s].GetDisplay(at)), (at, after.Sheets[s].GetDisplay(at)));
        }
    }

    [Fact] // ADR-0330 §4, §6: a file as Excel writes it — prefixes, a shared formula, a dynamic array, the 1904 date system, theme colours, formats and widths — opened, saved and opened again keeps everything carried
    public async Task ADR0330_open_save_open_keeps_everything_carried()
    {
        var normal = new XlsxFont("Calibri", 11, Color: XlsxColor.FromTheme(1));
        var styles = new XlsxStyles(
            new Dictionary<int, string> { [164] = "#,##0.00\\ \"EUR\"", [165] = "dd/mm/yyyy" },
            [normal, normal with { Bold = true, Color = XlsxColor.FromTheme(0) }, normal with { Italic = true, Strike = true, Underline = XlsxUnderline.Single, Color = XlsxColor.FromArgb(0xFFC00000) }],
            [XlsxFill.None, XlsxFill.Gray125, XlsxFill.Solid(XlsxColor.FromTheme(4, -0.249977111117893))],
            [XlsxBorder.None, new XlsxBorder(XlsxBorderEdge.None, XlsxBorderEdge.None, new XlsxBorderEdge(XlsxBorderStyle.Thin, XlsxColor.FromArgb(0xFF00B050)), new XlsxBorderEdge(XlsxBorderStyle.Double, null), XlsxBorderEdge.None)],
            [
                new XlsxCellFormat(),
                new XlsxCellFormat(0, 1, 2, 0, new XlsxAlignment(XlsxHorizontalAlignment.Center)),
                new XlsxCellFormat(164, 2, 0, 1, new XlsxAlignment(XlsxHorizontalAlignment.Right)),
                new XlsxCellFormat(165),
                new XlsxCellFormat(14),
            ]);
        var file = await WriteAsync(new XlsxWriteOptions { Styles = styles, Date1904 = true, ActiveSheet = 1 },
            new Page("Trades",
            [
                Text("A1", "Desk") with { StyleIndex = 1 }, Inline("B1", "Notional ") with { StyleIndex = 1 },
                Text("A2", "EMEA"), Number("B2", "1234.5", style: 2), Number("C2", "43539", style: 3),
                Text("A3", "APAC"), Number("B3", "99", style: 2), Number("C3", "43540", style: 4),
                Formula("D2", "B2*2") with { Formula = new XlsxFormula("B2*2", XlsxFormulaKind.Shared, "D2:D3", 0) },
                Formula("D3", "") with { Formula = new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0) },
                Formula("E2", "_xlfn.XLOOKUP(\"APAC\",A2:A3,B2:B3)"),
                Formula("F2", "1") with { Formula = new XlsxFormula("_xlfn.SEQUENCE(2)", XlsxFormulaKind.Array, "F2:F3", IsDynamicArray: true) },
                Number("F3", "2"),
                Formula("G2", "_xlfn.LET(_xlpm.x,B2,_xlpm.x*2)"),
                Formula("H2", "SUM(_xlfn.ANCHORARRAY(F2))"),
                Formula("I2", "_xlfn._xlws.FILTER(A2:A3,B2:B3>100)"),
                Boolean("J2", true), Error("J3", "#N/A"),
            ],
            [new XlsxColumn(0, 0, 14.7109375, BestFit: true), new XlsxColumn(1, 1, 16.7109375, StyleIndex: 2)],
            Rows: [new XlsxRow(0, 1)]),
            new Page("Desks", [Number("A1", "2"), Formula("B1", "Trades!B2*A1")]));

        var (first, report) = await ReadAsync(file, EnUs);
        var saved = await Saved.WriteAsync(first);
        var (second, again) = await ReadAsync(saved, EnUs);

        Assert.Equal([NotCarriedKind.ThemeColours], report.NotCarried.Select(n => n.Kind));
        Assert.True(again.IsEmpty, string.Join(" ", again.NotCarried.Select(n => n.Description)));
        Assert.Equal(first.ToJson(), second.ToJson());
        // The 1904 serials were moved once, on the first opening, and the dates stand.
        var trades = Workbook.Open(second).Find("Trades")!;
        Assert.Equal("16/03/2023", trades.GetDisplay(At("C2")).Text);
        Assert.Equal("3/17/2023", trades.GetDisplay(At("C3")).Text);
        Assert.Equal("2469", trades.GetDisplay(At("D2")).Text);
        Assert.Equal("198", trades.GetDisplay(At("D3")).Text);
        Assert.Equal("EMEA", trades.GetDisplay(At("I2")).Text);
        Assert.Equal("3", trades.GetDisplay(At("H2")).Text);
        Assert.Equal("2469", Workbook.Open(second).Find("Desks")!.GetDisplay(At("B1")).Text);
        Assert.Equal("Desks", second.Shown);
        // The saved file says 1900, and no stored prefix is lost: what was read as written is written as read.
        var parts = Parts(saved);
        Assert.Null(parts["xl/workbook.xml"].Root!.Element(S + "workbookPr")?.Attribute("date1904"));
        Assert.Equal("45001", CellElement(parts["xl/worksheets/sheet1.xml"], "C2").Element(S + "v")!.Value);
        Assert.Equal("_xlfn.LET(_xlpm.x,B2,_xlpm.x*2)", CellElement(parts["xl/worksheets/sheet1.xml"], "G2").Element(S + "f")!.Value);
        Assert.Equal("_xlfn._xlws.FILTER(A2:A3,B2:B3>100)", CellElement(parts["xl/worksheets/sheet1.xml"], "I2").Element(S + "f")!.Value);
        Assert.Equal("B2*2", CellElement(parts["xl/worksheets/sheet1.xml"], "D2").Element(S + "f")!.Value);
        Assert.Equal("B3*2", CellElement(parts["xl/worksheets/sheet1.xml"], "D3").Element(S + "f")!.Value);
    }
}
