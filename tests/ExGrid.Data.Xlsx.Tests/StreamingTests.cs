using System.Globalization;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// A worksheet read as a stream (ADR-0330 §9): a batch at a time, never held as a document; the read reports its progress
/// and yields between slices, and is cancelled between them.
/// </summary>
public class StreamingTests
{
    private const int Rows = 100_000;

    /// <summary>A sheet of <see cref="Rows"/> rows of three numbers and a Formula: some 12 MiB of XML.</summary>
    private static async Task<byte[]> Large() => await WriteAsync(async book =>
    {
        var sheet = book.AddSheet("Large");
        await sheet.WriteAsync(Cells(), Token);
    });

    private static IEnumerable<XlsxCell> Cells()
    {
        for (var r = 0; r < Rows; r++)
        {
            var row = (r + 1).ToString(CultureInfo.InvariantCulture);
            yield return new XlsxCell(r, 0, XlsxCellType.Number, row);
            yield return new XlsxCell(r, 1, XlsxCellType.Number, (r * 1.25).ToString("R", CultureInfo.InvariantCulture));
            yield return new XlsxCell(r, 2, XlsxCellType.Number, (-r).ToString(CultureInfo.InvariantCulture));
            yield return new XlsxCell(r, 3, XlsxCellType.Number, (r * 2.25 + 1).ToString("R", CultureInfo.InvariantCulture), Formula: new XlsxFormula($"A{row}+B{row}"));
        }
    }

    [Fact] // ADR-0330 §9: the cells come a batch at a time, in the file's order, every one of them
    public async Task A_large_sheet_is_read_in_batches()
    {
        using var reader = await OpenAsync(await Large());
        using var sheet = reader.OpenSheet(0);

        var batches = 0;
        long cells = 0;
        var last = (Row: -1, Column: 3);
        while (await sheet.ReadAsync())
        {
            batches++;
            Assert.InRange(sheet.Cells.Count, 1, XlsxSheetReader.BatchSize);
            foreach (var cell in sheet.Cells)
            {
                Assert.True(cell.Row > last.Row || (cell.Row == last.Row && cell.Column > last.Column));
                last = (cell.Row, cell.Column);
                cells++;
            }
        }

        Assert.Equal(Rows * 4L, cells);
        Assert.Equal(Rows * 4L, reader.CellsRead);
        Assert.True(batches >= Rows * 4 / XlsxSheetReader.BatchSize);
        Assert.False(await sheet.ReadAsync());
    }

    [Fact] // ADR-0330 §9: a worksheet is never held as a document — reading half of a large one keeps a small part of its XML in memory
    public async Task A_large_sheet_is_not_held_as_a_document()
    {
        var file = await Large();
        using var reader = await OpenAsync(file);
        var xmlBytes = reader.SheetBytes;
        using var sheet = reader.OpenSheet(0);
        await sheet.ReadAsync();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        while (reader.CellsRead < Rows * 2 && await sheet.ReadAsync())
        {
        }
        var after = GC.GetTotalMemory(forceFullCollection: true);

        Assert.True(xmlBytes > 8 << 20, $"The sheet is {xmlBytes:N0} bytes of XML.");
        Assert.True(after - before < xmlBytes / 16, $"Reading half of {xmlBytes:N0} bytes of XML kept {after - before:N0} bytes more.");
        GC.KeepAlive(file);
    }

    [Fact] // ADR-0330 §9: the read reports its progress and yields between slices, and its last report is the whole sheet
    public async Task The_read_reports_progress_and_yields()
    {
        var reports = new List<XlsxProgress>();
        var yields = 0;
        var options = new XlsxReadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Progress = new Inline(reports.Add),
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        };
        using var reader = await OpenAsync(await Large(), options);

        await reader.ReadSheetAsync(0);

        Assert.True(yields >= Rows * 4 / XlsxSheetReader.BatchSize, $"{yields} yields");
        Assert.True(reports.Count > yields);
        Assert.All(reports, r => Assert.Equal((0, 1), (r.Sheet, r.Sheets)));
        Assert.Equal(reports.Select(r => r.Cells).Order(), reports.Select(r => r.Cells));
        Assert.Equal(Rows * 4L, reports[^1].Cells);
        Assert.Equal(reports[^1].TotalBytes, reports[^1].Bytes);
    }

    [Fact] // ADR-0330 §9: a read is cancelled between slices
    public async Task A_read_is_cancelled_between_slices()
    {
        using var cancel = new CancellationTokenSource();
        var options = new XlsxReadOptions { SliceBudget = TimeSpan.Zero, Yield = () => { cancel.Cancel(); return ValueTask.CompletedTask; } };
        using var reader = await XlsxReader.OpenAsync(new MemoryStream(await Large()), options, cancel.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadSheetAsync(0));
        Assert.True(reader.CellsRead < Rows * 4L);
    }

    /// <summary>A sheet of 20,000 cells, each its own text: a shared strings part of 20,000 items.</summary>
    private static async Task<byte[]> ManyTexts() => await WriteAsync(async book =>
    {
        var sheet = book.AddSheet("Texts");
        await sheet.WriteAsync(Enumerable.Range(0, 20_000).Select(i => new XlsxCell(i, 0, XlsxCellType.SharedString, null, "Text " + i.ToString(CultureInfo.InvariantCulture))), Token);
    });

    [Fact] // ADR-0330 §9: the shared strings, read whole as the workbook opens, are read in slices too: progress reported and a yield between them
    public async Task The_shared_strings_are_read_in_slices()
    {
        var reports = new List<XlsxProgress>();
        var yields = 0;
        var options = new XlsxReadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Progress = new Inline(reports.Add),
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        };

        using var reader = await OpenAsync(await ManyTexts(), options);

        Assert.Equal(20_000, reader.SharedStrings.Count);
        Assert.Equal("Text 19999", reader.SharedStrings[^1]);
        Assert.True(yields >= 20_000 / XlsxReader.SharedStringSlice, $"{yields} yields");
        Assert.All(reports, r => Assert.Equal((-1, 1, 0L), (r.Sheet, r.Sheets, r.Cells)));
        Assert.Equal(reports.Select(r => r.Bytes).Order(), reports.Select(r => r.Bytes));
        Assert.True(reports[^1].TotalBytes > 0);
        Assert.Equal(reports[^1].TotalBytes, reports[^1].Bytes);
    }

    [Fact] // ADR-0330 §9: opening is cancelled between the shared strings' slices
    public async Task Opening_is_cancelled_while_the_shared_strings_are_read()
    {
        using var cancel = new CancellationTokenSource();
        var stream = new MemoryStream(await ManyTexts());
        var options = new XlsxReadOptions { SliceBudget = TimeSpan.Zero, Yield = () => { cancel.Cancel(); return ValueTask.CompletedTask; } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await XlsxReader.OpenAsync(stream, options, cancel.Token));
        Assert.False(stream.CanRead);
    }

    [Fact] // ADR-0330 §9: what a worksheet holds beside its cells is listed, not read: merged cells, notes, print settings, and rows and panes that are not carried
    public async Task What_else_a_sheet_holds_is_listed()
    {
        var sheet = Sheet(
            """<row r="1" ht="30" customHeight="1"><c r="A1"><v>1</v></c></row><row r="3" hidden="1"><c r="A3"><v>2</v></c></row>""",
            head: """<sheetPr><tabColor rgb="FFFF0000"/></sheetPr><dimension ref="A1:A3"/><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" state="frozen"/></sheetView></sheetViews>""",
            tail: """<mergeCells count="1"><mergeCell ref="A1:B1"/></mergeCells><pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/><legacyDrawing r:id="rId1"/>""");
        using var reader = await OpenAsync(Book(sheet));

        var read = await reader.ReadSheetAsync(0);

        Assert.Equal(["sheetPr", "mergeCells", "pageMargins", "legacyDrawing"], read.OtherElements);
        Assert.True(read.CustomRowHeights);
        Assert.True(read.HiddenRows);
        Assert.True(read.FrozenPanes);
        Assert.Equal(2, read.Cells.Count);
    }

    [Fact] // ADR-0330 §9: a cell or row that names no place takes the one after the last
    public async Task Cells_without_a_reference_take_the_next_place()
    {
        var sheet = Sheet("""<row><c><v>1</v></c><c><v>2</v></c></row><row><c r="C2"><v>3</v></c><c><v>4</v></c></row>""");
        using var reader = await OpenAsync(Book(sheet));

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal(["A1", "B1", "C2", "D2"], cells.Select(c => c.Reference));
    }

    [Fact] // ADR-0330 §4: a dynamic array is told from a legacy array formula by the workbook's cell metadata
    public async Task A_dynamic_array_is_told_by_its_metadata()
    {
        const string metadata = $"""
            <metadata xmlns="{Main}" xmlns:xda="http://schemas.microsoft.com/office/spreadsheetml/2017/dynamicarray">
              <metadataTypes count="2"><metadataType name="XLMDX" minSupportedVersion="120000"/><metadataType name="XLDAPR" minSupportedVersion="120000" copy="1"/></metadataTypes>
              <futureMetadata name="XLDAPR" count="1"><bk><extLst><ext uri="{"{bdbb8cdc-fa1e-496e-a857-3c3f30c029c3}"}"><xda:dynamicArrayProperties fDynamic="1" fCollapsed="0"/></ext></extLst></bk></futureMetadata>
              <cellMetadata count="2"><bk><rc t="1" v="0"/></bk><bk><rc t="2" v="0"/></bk></cellMetadata>
            </metadata>
            """;
        var sheet = Sheet("""
            <row r="1">
              <c r="A1" cm="2"><f t="array" ref="A1:A3">_xlfn.SEQUENCE(3)</f><v>1</v></c>
              <c r="B1"><f t="array" ref="B1:B3">A1:A3*2</f><v>2</v></c>
              <c r="C1" cm="1"><f t="array" ref="C1">CUBEVALUE("x")</f><v>3</v></c>
            </row>
            """);
        using var reader = await OpenAsync(Book(sheet, metadata: metadata));

        var cells = (await reader.ReadSheetAsync(0)).Cells;

        Assert.Equal([true, false, false], cells.Select(c => c.Formula!.IsDynamicArray));
        Assert.Equal("_xlfn.SEQUENCE(3)", cells[0].Formula!.Text);
    }

    private sealed class Inline(Action<XlsxProgress> report) : IProgress<XlsxProgress>
    {
        public void Report(XlsxProgress value) => report(value);
    }
}
