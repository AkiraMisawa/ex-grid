using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// What a workbook's read takes from the application beside the file (ADR-0330 §7, §9): the culture and the read options.
/// What a workbook carries is held by the shared cases (<c>tests/cases/sheet-xlsx</c>, ADR-0370 section 2).
/// </summary>
public class CarriedTests
{
    [Fact] // ADR-0330 §7: a culture the engine cannot name is refused before the file is read
    public async Task ADR0330_a_culture_the_engine_cannot_name_is_refused()
    {
        var file = await WriteAsync(new Page("Sheet1", [Number("A1", "1")]));

        var refused = await Assert.ThrowsAsync<ArgumentException>(async () => await ReadAsync(file, new CultureInfo("zz-ZZ")));

        Assert.Equal("culture", refused.ParamName);
    }

    [Fact] // ADR-0330 §9: the file layer's options pass through: progress is reported, and a cancelled read opens nothing
    public async Task ADR0330_progress_and_cancellation_pass_through()
    {
        var file = await WriteAsync(new Page("Sheet1", [.. Enumerable.Range(0, 5000).Select(i => Number($"A{i + 1}", i.ToString(CultureInfo.InvariantCulture)))]));
        var reports = new List<XlsxProgress>();

        await ReadAsync(file, options: new XlsxReadOptions { Progress = new Synchronous(reports.Add), SliceBudget = TimeSpan.Zero });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Assert.Contains(reports, r => r.Cells == 5000);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await XlsxWorkbookReader.ReadAsync(new MemoryStream(file), EnUs, null, cancelled.Token));
    }

    private sealed class Synchronous(Action<XlsxProgress> report) : IProgress<XlsxProgress>
    {
        public void Report(XlsxProgress value) => report(value);
    }
}
