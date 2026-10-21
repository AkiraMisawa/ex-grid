using System.IO.Compression;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// A workbook written and read back (ADR-0330 §1, §4, §9): its sheets in order with the one shown, the date system, every
/// cell type, every kind of Formula, the column widths and the styles §4 carries come back as they were written.
/// </summary>
public class RoundTripTests
{
    private static readonly XlsxStyles Styles = new(
        new Dictionary<int, string> { [164] = "#,##0.00;[Red]-#,##0.00", [165] = "yyyy-mm-dd" },
        [
            new XlsxFont("Calibri", 11, Color: XlsxColor.FromTheme(1)),
            new XlsxFont("Arial", 10, Bold: true, Italic: true, Underline: XlsxUnderline.Double, Strike: true, Color: XlsxColor.FromArgb(0xFFC00000)),
            new XlsxFont(Underline: XlsxUnderline.SingleAccounting, Color: XlsxColor.FromTheme(4, -0.249977111117893)),
        ],
        [XlsxFill.None, XlsxFill.Gray125, XlsxFill.Solid(XlsxColor.FromTheme(0, -0.149998474074526)), XlsxFill.Solid(XlsxColor.FromIndexed(13))],
        [
            XlsxBorder.None,
            new XlsxBorder(
                new XlsxBorderEdge(XlsxBorderStyle.Thin, XlsxColor.Automatic),
                new XlsxBorderEdge(XlsxBorderStyle.MediumDashDotDot, XlsxColor.FromArgb(0xFF00B050)),
                new XlsxBorderEdge(XlsxBorderStyle.Double),
                new XlsxBorderEdge(XlsxBorderStyle.SlantDashDot, XlsxColor.FromTheme(5, 0.399975585192419)),
                new XlsxBorderEdge(XlsxBorderStyle.Hair), DiagonalUp: true),
        ],
        [
            new XlsxCellFormat(),
            new XlsxCellFormat(164, 1, 2, 1, new XlsxAlignment(XlsxHorizontalAlignment.Right)),
            new XlsxCellFormat(165, 2, 3, 0, new XlsxAlignment(XlsxHorizontalAlignment.CenterContinuous, XlsxVerticalAlignment.Top, WrapText: true, Indent: 2, TextRotation: 45)),
            new XlsxCellFormat(10),
        ]);

    private static async Task<byte[]> Workbook() => await WriteAsync(async book =>
    {
        var first = book.AddSheet("Trades", [new XlsxColumn(0, 0, 18.5), new XlsxColumn(2, 4, 9.140625, Hidden: true, StyleIndex: 3)]);
        first.Write(new XlsxCell(0, 0, XlsxCellType.SharedString, null, "Desk"));
        first.Write(new XlsxCell(0, 1, XlsxCellType.SharedString, null, "Notional", StyleIndex: 1));
        first.Write(new XlsxCell(1, 0, XlsxCellType.SharedString, null, "EMEA"));
        first.Write(new XlsxCell(1, 1, XlsxCellType.Number, "1000000.25", StyleIndex: 1));
        first.Write(new XlsxCell(1, 2, XlsxCellType.Boolean, "1"));
        first.Write(new XlsxCell(1, 3, XlsxCellType.Error, "#DIV/0!", Formula: new XlsxFormula("1/0")));
        first.Write(new XlsxCell(2, 0, XlsxCellType.SharedString, null, "EMEA"));
        first.Write(new XlsxCell(2, 1, XlsxCellType.Number, "-0.5", Formula: new XlsxFormula("B2*-0.0000005", XlsxFormulaKind.Shared, "B3:B5", 0)));
        first.Write(new XlsxCell(3, 1, XlsxCellType.Number, "2", Formula: new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0)));
        first.Write(new XlsxCell(4, 1, XlsxCellType.Number, null, Formula: new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0), StyleIndex: 2));
        first.Write(new XlsxCell(5, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("_xlfn.SEQUENCE(3)", XlsxFormulaKind.Array, "A6:A8", IsDynamicArray: true)));
        first.Write(new XlsxCell(5, 3, XlsxCellType.Number, "6", Formula: new XlsxFormula("SUM(A6:A8*1)", XlsxFormulaKind.Array, "D6")));
        first.Write(new XlsxCell(6, 0, XlsxCellType.Number, "2"));
        first.Write(new XlsxCell(7, 0, XlsxCellType.Number, "3"));
        first.Write(new XlsxCell(9, 4, XlsxCellType.FormulaString, "a\rb", Formula: new XlsxFormula("CHAR(13)")));
        first.Write(new XlsxCell(10, 16_383, XlsxCellType.InlineString, null, "  far right  "));
        first.Write(new XlsxCell(1_048_575, 0, XlsxCellType.Number, "1E+308", StyleIndex: 3));

        var second = book.AddSheet("Données 'é' 2", visibility: XlsxSheetVisibility.Hidden);
        second.Write(new XlsxCell(0, 0, XlsxCellType.Number, "45000", Formula: new XlsxFormula("Trades!B2+'Données ''é'' 2'!A2")));

        book.AddSheet("Empty");
    }, new XlsxWriteOptions
    {
        Styles = Styles,
        ActiveSheet = 1,
        Date1904 = true,
        FullCalculationOnLoad = true,
        DefinedNames = [new XlsxDefinedName("Rate", "Trades!$B$2"), new XlsxDefinedName("_xlnm.Print_Area", "Trades!$A$1:$B$5", 0, Hidden: true)],
    });

    [Fact] // ADR-0330 §3: the sheets come back in order, with their names, the one shown, and the date system
    public async Task The_workbook_part_round_trips()
    {
        using var reader = await OpenAsync(await Workbook());

        Assert.Equal(["Trades", "Données 'é' 2", "Empty"], reader.Sheets.Select(s => s.Name));
        Assert.Equal([XlsxSheetVisibility.Visible, XlsxSheetVisibility.Hidden, XlsxSheetVisibility.Visible], reader.Sheets.Select(s => s.Visibility));
        Assert.Equal(1, reader.ActiveSheet);
        Assert.True(reader.Date1904);
        Assert.Equal(
            [new XlsxDefinedName("Rate", "Trades!$B$2"), new XlsxDefinedName("_xlnm.Print_Area", "Trades!$A$1:$B$5", 0, true)],
            reader.DefinedNames);
        Assert.Equal("xl/workbook.xml", reader.WorkbookPartName);
        Assert.False(reader.HasTheme);
    }

    [Fact] // ADR-0330 §4, §9: every cell comes back with its place, type, value, Formula and style
    public async Task Every_cell_round_trips()
    {
        using var reader = await OpenAsync(await Workbook());
        var sheets = await ReadAllAsync(reader);

        Assert.Equal(
        [
            new XlsxCell(0, 0, XlsxCellType.SharedString, "0", "Desk"),
            new XlsxCell(0, 1, XlsxCellType.SharedString, "1", "Notional", StyleIndex: 1),
            new XlsxCell(1, 0, XlsxCellType.SharedString, "2", "EMEA"),
            new XlsxCell(1, 1, XlsxCellType.Number, "1000000.25", StyleIndex: 1),
            new XlsxCell(1, 2, XlsxCellType.Boolean, "1"),
            new XlsxCell(1, 3, XlsxCellType.Error, "#DIV/0!", Formula: new XlsxFormula("1/0")),
            new XlsxCell(2, 0, XlsxCellType.SharedString, "2", "EMEA"),
            new XlsxCell(2, 1, XlsxCellType.Number, "-0.5", Formula: new XlsxFormula("B2*-0.0000005", XlsxFormulaKind.Shared, "B3:B5", 0)),
            new XlsxCell(3, 1, XlsxCellType.Number, "2", Formula: new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0)),
            new XlsxCell(4, 1, XlsxCellType.Number, null, Formula: new XlsxFormula("", XlsxFormulaKind.Shared, SharedIndex: 0), StyleIndex: 2),
            new XlsxCell(5, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("_xlfn.SEQUENCE(3)", XlsxFormulaKind.Array, "A6:A8", IsDynamicArray: true)),
            new XlsxCell(5, 3, XlsxCellType.Number, "6", Formula: new XlsxFormula("SUM(A6:A8*1)", XlsxFormulaKind.Array, "D6")),
            new XlsxCell(6, 0, XlsxCellType.Number, "2"),
            new XlsxCell(7, 0, XlsxCellType.Number, "3"),
            new XlsxCell(9, 4, XlsxCellType.FormulaString, "a\rb", Formula: new XlsxFormula("CHAR(13)")),
            new XlsxCell(10, 16_383, XlsxCellType.InlineString, null, "  far right  "),
            new XlsxCell(1_048_575, 0, XlsxCellType.Number, "1E+308", StyleIndex: 3),
        ], sheets[0]);
        Assert.Equal([new XlsxCell(0, 0, XlsxCellType.Number, "45000", Formula: new XlsxFormula("Trades!B2+'Données ''é'' 2'!A2"))], sheets[1]);
        Assert.Empty(sheets[2]);
        Assert.Equal("XFD11", sheets[0][15].Reference);
        Assert.Equal("A1048576", sheets[0][16].Reference);
    }

    [Fact] // ADR-0330 §4: column widths come back in Excel's own unit, with what else a column says
    public async Task Column_widths_round_trip()
    {
        using var reader = await OpenAsync(await Workbook());

        var sheet = await reader.ReadSheetAsync(0);

        Assert.Equal([new XlsxColumn(0, 0, 18.5), new XlsxColumn(2, 4, 9.140625, true, 3)], sheet.Columns);
        Assert.Equal(["sheetViews", "sheetFormatPr"], sheet.OtherElements.Select(e => e.Name));
    }

    [Fact] // ADR-0330 §4: Number Formats, fonts, fills, borders and alignment come back as written
    public async Task The_styles_round_trip()
    {
        using var reader = await OpenAsync(await Workbook());

        var styles = reader.Styles;

        Assert.Equal(Styles.NumberFormats.OrderBy(p => p.Key), styles.NumberFormats.OrderBy(p => p.Key));
        Assert.Equal(Styles.Fonts, styles.Fonts);
        Assert.Equal(Styles.Fills, styles.Fills);
        Assert.Equal(Styles.Borders, styles.Borders);
        Assert.Equal(Styles.CellFormats, styles.CellFormats);
        Assert.Null(styles.IndexedColors);
    }

    [Fact] // ADR-0330 §6: the workbook asks to be calculated in full when Excel opens it, and is a package Excel's parts make
    public async Task The_package_holds_the_parts_Excel_reads()
    {
        var file = await Workbook();
        using var zip = new ZipArchive(new MemoryStream(file));

        Assert.Equal(
            ["[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "xl/metadata.xml", "xl/sharedStrings.xml", "xl/styles.xml",
             "xl/workbook.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/worksheets/sheet3.xml"],
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        using var workbook = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open());
        Assert.Contains("""<calcPr calcId="191029" fullCalcOnLoad="1" />""", await workbook.ReadToEndAsync(Token));
        using var shared = new StreamReader(zip.GetEntry("xl/sharedStrings.xml")!.Open());
        Assert.Contains("""count="4" uniqueCount="3" """, await shared.ReadToEndAsync(Token));
    }

    [Fact] // ADR-0330 §1: a workbook is written to a stream that refuses synchronous writes, as a response body does, and read from one that cannot seek
    public async Task Writing_is_asynchronous_and_reading_needs_no_seek()
    {
        var output = new AsyncOnly();
        await using (var writer = XlsxWriter.Create(output))
        {
            var sheet = writer.AddSheet("One");
            await sheet.WriteAsync(Enumerable.Range(0, 10_000).Select(i => new XlsxCell(i, 0, XlsxCellType.Number, i.ToString(System.Globalization.CultureInfo.InvariantCulture))), Token);
            await writer.CompleteAsync(Token);
        }

        using var reader = await XlsxReader.OpenAsync(new Forward(output.ToArray()), cancellationToken: Token);
        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(10_000, cells.Count);
        Assert.Equal("9999", cells[^1].Value);
    }

    [Fact] // ADR-0330 §1: a sheet's cells are written in the sheet's order, each once; a name Excel refuses is refused
    public async Task Writing_refuses_what_Excel_would_not_read()
    {
        await using var writer = XlsxWriter.Create(new MemoryStream());
        var sheet = writer.AddSheet("One");
        sheet.Write(new XlsxCell(1, 1, XlsxCellType.Number, "1"));

        Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(1, 1, XlsxCellType.Number, "2")));
        Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(1, 0, XlsxCellType.Number, "2")));
        Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 5, XlsxCellType.Number, "2")));
        Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(2, 0, XlsxCellType.Number, "2", StyleIndex: 1)));
        Assert.Throws<ArgumentException>(() => writer.AddSheet("one"));
        Assert.Throws<ArgumentException>(() => writer.AddSheet("a/b"));
        Assert.Throws<ArgumentException>(() => writer.AddSheet(new string('x', 32)));
        Assert.Throws<ArgumentException>(() => writer.AddSheet("'quoted'"));
        Assert.Throws<ArgumentException>(() => XlsxWriter.Create(new MemoryStream(), new XlsxWriteOptions { Styles = Styles with { Fills = [XlsxFill.None] } }));
    }

    [Fact] // ADR-0330 §1: a cell is checked whole before anything of it is written, so a refused one leaves the sheet writable and the package well-formed
    public async Task A_refused_cell_leaves_the_sheet_as_it_was()
    {
        var file = await WriteAsync(book =>
        {
            var sheet = book.AddSheet("One");
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("A2", XlsxFormulaKind.Shared))));
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 0, XlsxCellType.Number, "1\u0001")));
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 0, XlsxCellType.SharedString, null, "half \uD800 a pair")));
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 0, XlsxCellType.InlineString, null, "\uDC00")));
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("\uD800"))));
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(0, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("1", XlsxFormulaKind.Array, "A1\u0002"))));
            sheet.Write(new XlsxCell(0, 0, XlsxCellType.Number, "1", Formula: new XlsxFormula("A2", XlsxFormulaKind.Shared, "A1:A2", 0)));
            sheet.Write(new XlsxCell(0, 1, XlsxCellType.SharedString, null, "a \u0001 \U0001F600 pair"));
            return Task.CompletedTask;
        });
        using var reader = await OpenAsync(file);

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(["A1", "B1"], cells.Select(c => c.Reference));
        Assert.Equal(0, cells[0].Formula!.SharedIndex);
        Assert.Equal("a \u0001 \U0001F600 pair", cells[1].Text);
    }

    [Fact] // ADR-0330 §4: column ranges that overlap, or name a cell format the styles do not hold, are refused before the sheet begins
    public async Task Overlapping_columns_are_refused()
    {
        await using var writer = XlsxWriter.Create(new MemoryStream());

        Assert.Throws<ArgumentException>(() => writer.AddSheet("A", [new XlsxColumn(0, 2, 10), new XlsxColumn(2, 4, 12)]));
        Assert.Throws<ArgumentException>(() => writer.AddSheet("A", [new XlsxColumn(3, 3, 10), new XlsxColumn(1, 5, 12)]));
        Assert.Throws<ArgumentException>(() => writer.AddSheet("A", [new XlsxColumn(1, 1, 10), new XlsxColumn(1, 1, 10)]));
        Assert.Throws<ArgumentException>(() => writer.AddSheet("A", [new XlsxColumn(1, 1, 10, StyleIndex: 1)]));
        var sheet = writer.AddSheet("A", [new XlsxColumn(2, 3, 12), new XlsxColumn(0, 1, 10)]);
        Assert.Equal("A", sheet.Name);
    }

    [Fact] // ADR-0330 §1: a name XML cannot carry is refused where it is given, and a workbook whose completion fails still closes its package
    public async Task Names_xml_cannot_carry_are_refused_and_a_failed_completion_closes_the_package()
    {
        await using (var writer = XlsxWriter.Create(new MemoryStream()))
        {
            Assert.Throws<ArgumentException>(() => writer.AddSheet("Tab\u0001"));
            Assert.Throws<ArgumentException>(() => writer.AddSheet("Tab\tTwo"));
            Assert.Throws<ArgumentException>(() => writer.AddSheet("Half \uD800"));
            Assert.Equal("Tab \U0001F600", writer.AddSheet("Tab \U0001F600").Name);
        }
        Assert.Throws<ArgumentException>(() => XlsxWriter.Create(new MemoryStream(), new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Rate\u0001", "1")] }));
        Assert.Throws<ArgumentException>(() => XlsxWriter.Create(new MemoryStream(), new XlsxWriteOptions { DefinedNames = [new XlsxDefinedName("Rate", "\"\uDC00\"")] }));
        Assert.Throws<ArgumentException>(() => XlsxWriter.Create(new MemoryStream(), new XlsxWriteOptions { Styles = Styles with { Fonts = [new XlsxFont("Cal\u0001ibri")] } }));

        // A border style no name stands for makes the styles part fail as it is written, after the sheets have ended.
        var broken = XlsxStyles.Default with { Borders = [XlsxBorder.None with { Left = new XlsxBorderEdge((XlsxBorderStyle)99) }] };
        var failed = XlsxWriter.Create(new MemoryStream(), new XlsxWriteOptions { Styles = broken });
        failed.AddSheet("One").Write(new XlsxCell(0, 0, XlsxCellType.Number, "1"));

        await Assert.ThrowsAnyAsync<Exception>(async () => await failed.CompleteAsync(Token));
        Assert.False(failed.PackageClosed);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await failed.CompleteAsync(Token));
        await failed.DisposeAsync();
        Assert.True(failed.PackageClosed);
    }
}
