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

    private sealed class Inline(Action<XlsxProgress> report) : IProgress<XlsxProgress>
    {
        public void Report(XlsxProgress value) => report(value);
    }
}
