using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// Column widths cross both ways in Excel's own unit (ADR-0330 §4, ADR-0046, ADR-0047, ticket 03): the engine's characters
/// of the default font's digit, the file's with the cell's padding, truncated to 1/256 as Excel stores them.
/// </summary>
public class ColumnWidthTests
{
    [Theory] // ADR-0330 §4: the widths Excel writes for the widths it shows (Calibri 11: 7 px a digit, 5 px of padding)
    [InlineData(8.43, 9.140625)]
    [InlineData(12, 12.7109375)]
    [InlineData(20, 20.7109375)]
    [InlineData(1, 1.7109375)]
    [InlineData(0.71, 1.421875)]
    [InlineData(255, 255.7109375)]
    [InlineData(10.57, 11.28125)]
    public void ADR0330_a_width_is_Excels_own_unit_both_ways(double characters, double file)
    {
        Assert.Equal(file, ColumnWidthUnit.ToFile(characters));
        Assert.Equal(characters, ColumnWidthUnit.FromFile(file));
    }

    [Fact] // ADR-0330 §4: every width to the hundredth, from 0.01 to 255, reads back as it was written
    public void ADR0330_every_width_to_the_hundredth_round_trips()
    {
        var wrong = new List<double>();
        for (var hundredths = 1; hundredths <= 25_500; hundredths++)
        {
            var characters = hundredths / 100.0;
            if (ColumnWidthUnit.FromFile(ColumnWidthUnit.ToFile(characters)) != characters) wrong.Add(characters);
        }

        Assert.Empty(wrong);
    }

    [Fact] // ADR-0330 §4, ADR-0046: widths are read into runs, one a typed entry fitted (bestFit) as widened by entry, any other as the user's
    public async Task ADR0330_widths_are_read_with_their_kind()
    {
        var file = await WriteAsync(new Page("Trades", [Number("A1", "1")],
        [
            new XlsxColumn(0, 0, 12.7109375, BestFit: true), new XlsxColumn(1, 1, 20.7109375), new XlsxColumn(2, 3, 20.7109375),
            new XlsxColumn(5, 5, 9.140625), new XlsxColumn(6, 6, 0, Hidden: true), new XlsxColumn(7, 7, 300), new XlsxColumn(8, 8, null, StyleIndex: 0),
        ]));

        var (document, report) = await ReadAsync(file);

        Assert.Equal(
        [
            new SheetDocumentColumnWidth(0, 0, 12, SheetColumnWidthKind.WidenedByEntry),
            new SheetDocumentColumnWidth(1, 3, 20, SheetColumnWidthKind.SetByUser),
            new SheetDocumentColumnWidth(7, 7, Sheet.MaxColumnWidth, SheetColumnWidthKind.SetByUser),
        ], document.Sheet("Trades").ColumnWidths);
        // The default width records nothing; a hidden column of no width opens shown at the default, and is listed.
        Assert.Equal([NotCarriedKind.HiddenColumns], report.NotCarried.Select(n => n.Kind));
        var sheet = Workbook.Open(document).Sheets[0];
        Assert.Null(sheet.GetColumnWidth(5));
        Assert.Equal(new SheetColumnWidth(12, SheetColumnWidthKind.WidenedByEntry), sheet.GetColumnWidth(0));
    }

    [Theory] // ADR-0330 §4: a Sheet's own default width is not carried, and is listed; Excel's own base of 8 is not
    [InlineData("""<sheetFormatPr defaultColWidth="12.5" defaultRowHeight="15"/>""", true)]
    [InlineData("""<sheetFormatPr baseColWidth="10" defaultRowHeight="16"/>""", true)]
    [InlineData("""<sheetFormatPr baseColWidth="8" defaultRowHeight="15"/>""", false)]
    public async Task ADR0330_a_default_column_width_of_its_own_is_listed(string element, bool listed)
    {
        var file = Edit(await WriteAsync(new Page("Trades", [Number("A1", "1")])), "xl/worksheets/sheet1.xml",
            xml => System.Text.RegularExpressions.Regex.Replace(xml, "<sheetFormatPr[^>]*/>", element));

        var report = (await ReadAsync(file)).Report;

        Assert.Equal(listed, report.NotCarried.Any(n => n is { Kind: NotCarriedKind.DefaultColumnWidth, Sheet: "Trades" }));
    }

    [Fact] // ADR-0330 §4, ADR-0046: a width is written in the file's unit, fitted where an entry widened it, and reads back with its kind
    public async Task ADR0330_widths_are_written_and_read_back()
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var sheet = new SheetDocument(culture, "Trades", [])
        {
            ColumnWidths =
            [
                new SheetDocumentColumnWidth(0, 0, 12.5, SheetColumnWidthKind.WidenedByEntry),
                new SheetDocumentColumnWidth(1, 2, 3.14),
                new SheetDocumentColumnWidth(16_383, 16_383, 255),
            ],
        };
        var file = await Saved.WriteAsync(new WorkbookDocument(culture, "Trades", [sheet]));

        using var reader = await XlsxReader.OpenAsync(new MemoryStream(file), cancellationToken: Token);
        Assert.Equal(
            [new XlsxColumn(0, 0, ColumnWidthUnit.ToFile(12.5), BestFit: true), new XlsxColumn(1, 2, ColumnWidthUnit.ToFile(3.14)), new XlsxColumn(16_383, 16_383, 255.7109375)],
            (await reader.ReadSheetAsync(0)).Columns);
        Assert.Equal(sheet.ColumnWidths, (await ReadAsync(file)).Document.Sheet("Trades").ColumnWidths);
    }
}
