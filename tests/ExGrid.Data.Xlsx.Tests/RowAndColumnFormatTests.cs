using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// What a row and a column record beside their cells (ADR-0330 §4): a row's own cell format (<c>customFormat</c>), which its
/// empty cells take in place of their columns', and a column's fitted width (<c>bestFit</c>), as Excel marks a width a typed
/// entry widened. Both are read and written back.
/// </summary>
public class RowAndColumnFormatTests
{
    private static readonly XlsxStyles Styles = XlsxStyles.Default with
    {
        CellFormats = [new XlsxCellFormat(), new XlsxCellFormat(10), new XlsxCellFormat(2)],
    };

    [Fact] // ADR-0330 §4: a formatted row comes back with its format, with or without cells, the last one included
    public async Task ADR0330_a_rows_format_round_trips()
    {
        var file = await WriteAsync(book =>
        {
            var sheet = book.AddSheet("Sheet1");
            sheet.Write(new XlsxCell(0, 0, XlsxCellType.Number, "1"));
            sheet.WriteRow(new XlsxRow(2, 1));
            sheet.Write(new XlsxCell(2, 1, XlsxCellType.Number, "2", StyleIndex: 2));
            sheet.WriteRow(new XlsxRow(4, 0));
            sheet.WriteRow(new XlsxRow(1_048_575, 2));
            return Task.CompletedTask;
        }, new XlsxWriteOptions { Styles = Styles });

        using var reader = await OpenAsync(file);
        var sheet = await reader.ReadSheetAsync(0);

        Assert.Equal([new XlsxRow(2, 1), new XlsxRow(4, 0), new XlsxRow(1_048_575, 2)], sheet.Rows);
        Assert.Equal([new XlsxCell(0, 0, XlsxCellType.Number, "1"), new XlsxCell(2, 1, XlsxCellType.Number, "2", StyleIndex: 2)], sheet.Cells);
    }

    [Fact] // ADR-0330 §4: a row's s counts only beside customFormat (ECMA-376 §18.3.1.73)
    public async Task ADR0330_a_rows_style_without_customFormat_records_nothing()
    {
        var file = Book(Sheet("""<row r="1" s="1"><c r="A1"><v>1</v></c></row><row r="2" s="1" customFormat="0"/><row r="3" customFormat="1"/>"""),
            styles: StylesXml);

        using var reader = await OpenAsync(file);
        var sheet = await reader.ReadSheetAsync(0);

        Assert.Equal([new XlsxRow(2, 0)], sheet.Rows);
    }

    [Fact] // ADR-0330 §4, §9: rows formatted without a cell count toward a batch, so they are read a batch at a time
    public async Task ADR0330_formatted_rows_are_read_in_batches()
    {
        var rows = string.Concat(Enumerable.Range(1, XlsxSheetReader.BatchSize * 2 + 10).Select(r => $"""<row r="{r}" s="1" customFormat="1"/>"""));
        using var reader = await OpenAsync(Book(Sheet(rows), styles: StylesXml));
        using var sheet = reader.OpenSheet(0);

        var batches = new List<int>();
        while (await sheet.ReadAsync())
            batches.Add(sheet.Rows.Count);

        Assert.Equal([XlsxSheetReader.BatchSize, XlsxSheetReader.BatchSize, 10], batches);
    }

    [Fact] // ADR-0330 §4: a row naming a cell format the styles do not hold is refused by name
    public async Task ADR0330_a_row_naming_no_cell_format_is_refused()
    {
        var refused = await RefusalAsync(Book(Sheet("""<row r="7" s="9" customFormat="1"/>"""), styles: StylesXml));

        Assert.Equal(XlsxRefusal.Malformed, refused.Reason);
        Assert.Contains("Row 7", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §4: a row is begun in order, before its cells, and with a cell format the styles hold
    public async Task ADR0330_a_row_out_of_order_is_refused_when_written()
    {
        await WriteAsync(book =>
        {
            var sheet = book.AddSheet("Sheet1");
            sheet.Write(new XlsxCell(3, 0, XlsxCellType.Number, "1"));
            Assert.Throws<ArgumentException>(() => sheet.WriteRow(new XlsxRow(3, 1)));
            Assert.Throws<ArgumentException>(() => sheet.WriteRow(new XlsxRow(2, 1)));
            Assert.Throws<ArgumentException>(() => sheet.WriteRow(new XlsxRow(5, 3)));
            sheet.WriteRow(new XlsxRow(4, 1));
            Assert.Throws<ArgumentException>(() => sheet.Write(new XlsxCell(3, 1, XlsxCellType.Number, "2")));
            return Task.CompletedTask;
        }, new XlsxWriteOptions { Styles = Styles });
    }

    [Fact] // ADR-0330 §4, ADR-0046: a width fitted to an entry comes back marked so; one set by hand does not
    public async Task ADR0330_a_columns_fitted_width_round_trips()
    {
        var file = await WriteAsync(book =>
        {
            book.AddSheet("Sheet1", [new XlsxColumn(0, 0, 12.7109375, BestFit: true), new XlsxColumn(1, 2, 20.7109375), new XlsxColumn(3, 3, null, StyleIndex: 1, BestFit: true)]);
            return Task.CompletedTask;
        }, new XlsxWriteOptions { Styles = Styles });

        using var reader = await OpenAsync(file);
        var sheet = await reader.ReadSheetAsync(0);

        // A fit with no width says nothing, and is not written.
        Assert.Equal([new XlsxColumn(0, 0, 12.7109375, BestFit: true), new XlsxColumn(1, 2, 20.7109375), new XlsxColumn(3, 3, null, StyleIndex: 1)], sheet.Columns);
    }

    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
          <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/><xf numFmtId="10" fontId="0" fillId="0" borderId="0"/></cellXfs>
        </styleSheet>
        """;
}
