using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests;

/// <summary>
/// What writing a workbook does that the shared cases cannot hold (ADR-0330 §1, §9; the cases are
/// <c>tests/cases/xlsx/write.json</c>): a stream that refuses synchronous writes, the size limit met as the writer's buffer is
/// flushed, which differs between ExGrid.Data.Xlsx and <c>@ex-grid/data-xlsx</c>, and a completion that fails.
/// </summary>
public class RoundTripTests
{
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

    [Fact] // ADR-0330 §9: a workbook whose parts would hold more uncompressed bytes than the limit is refused by name, naming the part and the sheet it met, and nothing is handed to the stream
    public async Task Writing_past_the_uncompressed_limit_is_refused_by_name()
    {
        using var output = new MemoryStream();
        var writer = XlsxWriter.Create(output, new XlsxWriteOptions { MaxUncompressedBytes = 2_000 });
        var refused = Assert.Throws<XlsxException>(() =>
        {
            var sheet = writer.AddSheet("One");
            for (var row = 0; row < 1_000; row++)
                sheet.Write(new XlsxCell(row, 0, XlsxCellType.Number, "1"));
        });
        await writer.DisposeAsync();

        Assert.Equal((XlsxRefusal.TooLarge, "xl/worksheets/sheet1.xml", "One"), (refused.Reason, refused.Part, refused.Sheet));
        Assert.Equal(0, output.Length);
    }

    [Fact] // ADR-0330 §1: a workbook whose completion fails still closes its package when it is disposed
    public async Task A_failed_completion_closes_the_package()
    {
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
