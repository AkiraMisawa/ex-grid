using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// Cell Formats and column widths cross both ways (ADR-0330 §4, ticket 03): a Workbook Document's formats at every level,
/// written through the mapping and read back, show as they did; a file's, read and written and read again, are the same
/// document. Nothing written is listed as not carried.
/// </summary>
public class FormatRoundTripTests
{
    private static readonly CultureInfo EnGb = CultureInfo.GetCultureInfo("en-GB");

    private static CellAddress At(string at) => CellAddress.Parse(at);

    /// <summary>A Sheet that records every part at every level, over and under one another.</summary>
    private static WorkbookDocument Document()
    {
        var red = CellColour.FromRgb(0xC00000);
        var yellow = CellFill.Solid(CellColour.FromRgb(0xFFFF00));
        var blue = CellFill.Solid(CellColour.FromRgb(0x4472C4));
        var boxed = new CellBorders(new BorderLine(BorderLineStyle.Thick, red), new BorderLine(BorderLineStyle.Double), new BorderLine(BorderLineStyle.SlantedDashDot, CellColour.FromRgb(0x00B050)), new BorderLine(BorderLineStyle.Hair));
        var trades = new SheetDocument(EnGb, "Trades",
        [
            new(At("A1"), Entry.FromValue(Value.FromText("Desk")), null, HorizontalAlignment.Center, new CellFont(Bold: true), null, null),
            new(At("B1"), Entry.FromValue(Value.FromText("Notional")), null, HorizontalAlignment.Right, new CellFont(red, Italic: true, Underline: true, Strikethrough: true), blue, boxed),
            new(At("B2"), Entry.FromValue(Value.FromNumber(1_000_000.25)), NumberFormat.Parse("#,##0.00;[Red]-#,##0.00"), null, null, null, null),
            new(At("C2"), Entry.FromValue(Value.FromNumber(45000)), NumberFormat.Parse("m/d/yyyy"), null, null, CellFill.None, null),
            new(At("C3"), Entry.FromValue(Value.FromNumber(0.125)), NumberFormat.General, HorizontalAlignment.General, CellFont.Default, null, CellBorders.None),
            new(At("D4"), null, NumberFormat.Parse("d\\-mmm\\-yy"), HorizontalAlignment.Left, null, yellow, null),
            new(At("E5"), Entry.FromValue(Value.FromBoolean(true)), null, null, null, null, new CellBorders(Bottom: new BorderLine(BorderLineStyle.MediumDashDotDot, red))),
            new(At("F6"), Entry.Parse("=B2*2", EnGb), NumberFormat.Parse("$#,##0.00_);[Red]($#,##0.00)"), null, null, null, null),
        ])
        {
            Columns =
            [
                new SheetDocumentAxisFormat(2, 3, NumberFormat.Parse("0.00%"), null, new CellFont(Bold: true), null, null),
                new SheetDocumentAxisFormat(5, 5, null, HorizontalAlignment.Right, null, yellow, null),
            ],
            Rows =
            [
                new SheetDocumentAxisFormat(2, 2, null, null, null, blue, null),
                new SheetDocumentAxisFormat(4, 5, NumberFormat.Parse("yyyy-mm-dd"), null, new CellFont(red), null, new CellBorders(Top: new BorderLine(BorderLineStyle.Thin))),
            ],
            ColumnWidths =
            [
                new SheetDocumentColumnWidth(0, 0, 18.57),
                new SheetDocumentColumnWidth(1, 2, 12, SheetColumnWidthKind.WidenedByEntry),
                new SheetDocumentColumnWidth(5, 5, 3.5),
            ],
        };
        var desks = new SheetDocument(EnGb, "Desks", [new(At("A1"), Entry.FromValue(Value.FromNumber(1)), NumberFormat.Parse("0.0"), null, null, null, null)])
        {
            Columns = [new SheetDocumentAxisFormat(0, 16_383, null, null, null, CellFill.Solid(CellColour.FromRgb(0xF2F2F2)), null)],
        };
        return new WorkbookDocument(EnGb, "Desks", [trades, desks]);
    }

    [Fact] // ADR-0330 §4: a Workbook Document's formats and widths, written and read back, show as they did, cell by cell
    public async Task ADR0330_a_documents_formats_and_widths_written_read_back_as_they_were()
    {
        var document = Document();

        var (read, report) = await ReadAsync(await Saved.WriteAsync(document), EnGb);

        Assert.True(report.IsEmpty);
        Assert.Equal("Desks", read.Shown);
        var before = Workbook.Open(document);
        var after = Workbook.Open(read);
        for (var s = 0; s < before.Sheets.Count; s++)
        {
            for (var row = 0; row < 9; row++)
            {
                for (var column = 0; column < 9; column++)
                {
                    var at = new CellAddress(row, column);
                    Assert.Equal((at, before.Sheets[s].GetCellFormat(at)), (at, after.Sheets[s].GetCellFormat(at)));
                    Assert.Equal((at, before.Sheets[s].GetDisplay(at)), (at, after.Sheets[s].GetDisplay(at)));
                }
                Assert.Equal(before.Sheets[s].GetColumnWidth(row), after.Sheets[s].GetColumnWidth(row));
            }
            Assert.Equal(before.Sheets[s].GetCellFormat(new CellAddress(1_048_575, 16_383)), after.Sheets[s].GetCellFormat(new CellAddress(1_048_575, 16_383)));
        }
        Assert.Equal(document.Sheet("Trades").Cells.Where(c => c.Entry is not null).Select(c => (c.Address, c.Entry)),
            read.Sheet("Trades").Cells.Where(c => c.Entry is not null).Select(c => (c.Address, c.Entry)));
    }

    [Fact] // ADR-0330 §4: a document read from a file, written and read again, is the same document
    public async Task ADR0330_a_files_formats_read_written_and_read_again_are_the_same_document()
    {
        var first = (await ReadAsync(await Saved.WriteAsync(Document()), EnGb)).Document;

        var second = (await ReadAsync(await Saved.WriteAsync(first), EnGb)).Document;

        Assert.Equal(first.ToJson(), second.ToJson());
    }

    [Fact] // ADR-0330 §4, §8: a file as Excel styles it — theme colours, Text 1, a column, a row and cells — reads, writes and reads again the same
    public async Task ADR0330_an_Excel_styled_file_crosses_both_ways()
    {
        var normal = new XlsxFont("Calibri", 11, Color: XlsxColor.FromTheme(1));
        var styles = new XlsxStyles(
            new Dictionary<int, string> { [164] = "#,##0.00\\ \"EUR\"", [165] = "dd/mm/yyyy" },
            [normal, normal with { Bold = true, Color = XlsxColor.FromTheme(0) }, normal with { Color = XlsxColor.FromArgb(0xFFFF0000), Underline = XlsxUnderline.Single }],
            [XlsxFill.None, XlsxFill.Gray125, new XlsxFill("solid", XlsxColor.FromTheme(4, -0.249977111117893), XlsxColor.FromIndexed(64)), XlsxFill.Solid(XlsxColor.FromTheme(9, 0.799981688894314))],
            [XlsxBorder.None, new XlsxBorder(XlsxBorderEdge.None, XlsxBorderEdge.None, XlsxBorderEdge.None, new XlsxBorderEdge(XlsxBorderStyle.Double, XlsxColor.FromIndexed(64)), XlsxBorderEdge.None)],
            [
                new XlsxCellFormat(),
                new XlsxCellFormat(0, 1, 2, 0, new XlsxAlignment(XlsxHorizontalAlignment.Center)),
                new XlsxCellFormat(164, 0, 0, 1),
                new XlsxCellFormat(165, 2, 3, 0),
                new XlsxCellFormat(14),
            ]);
        var file = await WriteAsync(new XlsxWriteOptions { Styles = styles },
            new Page("Trades",
                [Text("A1", "Desk") with { StyleIndex = 1 }, Text("B1", "PV") with { StyleIndex = 1 }, Number("B2", "1234.5", style: 2), Number("C2", "45000", style: 3), Number("D9", "45001", style: 4)],
                [new XlsxColumn(0, 0, 14.7109375, BestFit: true), new XlsxColumn(1, 1, 16.7109375, StyleIndex: 2), new XlsxColumn(3, 3, 9.140625, StyleIndex: 4)],
                Rows: [new XlsxRow(0, 1)]));

        var (first, report) = await ReadAsync(file, EnGb);
        var second = (await ReadAsync(await Saved.WriteAsync(first), EnGb)).Document;

        Assert.Equal([NotCarriedKind.ThemeColours], report.NotCarried.Select(n => n.Kind));
        Assert.Equal(first.ToJson(), second.ToJson());
        var sheet = Workbook.Open(first).Sheets[0];
        Assert.Equal(CellFill.Solid(CellColour.FromRgb((int)(XlsxTheme.Office.Resolve(XlsxColor.FromTheme(4, -0.249977111117893))!.Value & 0xFFFFFF))), sheet.GetCellFormat(At("Z1")).Fill);
        Assert.Equal(new CellFont(CellColour.FromRgb(0xFFFFFF), Bold: true), sheet.GetCellFormat(At("A1")).Font);
        Assert.Equal(CellFont.Default, sheet.GetCellFormat(At("B2")).Font);
        Assert.Equal(new BorderLine(BorderLineStyle.Double), sheet.GetCellFormat(At("B9")).Borders.Bottom);
        Assert.Equal("1,234.50 EUR", sheet.GetDisplay(At("B2")).Text);
        Assert.Equal("15/03/2023", sheet.GetDisplay(At("C2")).Text);
        Assert.Equal("16/03/2023", sheet.GetDisplay(At("D9")).Text);
        Assert.Equal(new SheetColumnWidth(14, SheetColumnWidthKind.WidenedByEntry), sheet.GetColumnWidth(0));
        Assert.Equal(new SheetColumnWidth(16, SheetColumnWidthKind.SetByUser), sheet.GetColumnWidth(1));
        Assert.Null(sheet.GetColumnWidth(3));
    }
}
