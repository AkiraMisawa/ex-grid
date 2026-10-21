using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// A save takes its snapshot of the Workbook before it first yields, then builds the file from it in slices, yielding between
/// them with progress and cancellation, as a read does (ADR-0330 §9, ticket 06 of <c>docs/specs/exsheet-xlsx</c>).
/// </summary>
public class SavePacingTests
{
    /// <summary>A Workbook of <paramref name="rows"/> numbers in column A, and a Formula over them in B1.</summary>
    private static Workbook Numbers(int rows)
    {
        var workbook = new Workbook(EnUs);
        var entries = Enumerable.Range(0, rows).Select(r => KeyValuePair.Create(new CellAddress(r, 0), (Entry?)Entry.FromValue(Value.FromNumber(r + 1))))
            .Append(KeyValuePair.Create(new CellAddress(0, 1), (Entry?)Entry.Parse($"=SUM(A1:A{rows})", EnUs)));
        workbook.Do("Sheet1", SheetEdit.SetEntries(entries));
        return workbook;
    }

    /// <summary>Progress told as it is reported, on the reporting thread.</summary>
    private sealed class Told : IProgress<XlsxSaveProgress>
    {
        public List<XlsxSaveProgress> All { get; } = [];

        public void Report(XlsxSaveProgress value) => All.Add(value);
    }

    [Fact] // ADR-0330 §9, ticket 06: the file is the Workbook as it stood at the call, though the Workbook changes while the write is under way
    public async Task ADR0330_the_file_is_the_snapshot_taken_before_the_first_yield()
    {
        var workbook = Numbers(2_000);
        var held = new TaskCompletionSource();
        var yields = 0;
        using var stream = new MemoryStream();
        var options = new XlsxSaveOptions
        {
            SliceBudget = TimeSpan.Zero,
            Yield = async () =>
            {
                if (Interlocked.Increment(ref yields) == 1) await held.Task;
            },
        };

        var writing = XlsxWorkbookWriter.WriteAsync(workbook, stream, options, Token).AsTask();
        Assert.False(writing.IsCompleted);
        workbook.Do("Sheet1", SheetEdit.SetEntries([KeyValuePair.Create(new CellAddress(0, 0), (Entry?)Entry.FromValue(Value.FromNumber(1_000_000)))]));
        workbook.Do("Sheet1", SheetEdit.SetEntries([KeyValuePair.Create(new CellAddress(5_000, 0), (Entry?)Entry.FromValue(Value.FromNumber(7)))]));
        held.SetResult();
        await writing;

        var sheet = Workbook.Open((await XlsxWorkbookReader.ReadAsync(new MemoryStream(stream.ToArray()), EnUs, null, Token)).Document).Sheets[0];
        Assert.Equal(1, sheet.GetValue(new CellAddress(0, 0))!.Value.Number);
        Assert.Null(sheet.GetValue(new CellAddress(5_000, 0)));
        Assert.Equal(2_001_000, sheet.GetValue(new CellAddress(0, 1))!.Value.Number);
    }

    [Fact] // ADR-0330 §9, ticket 06: the write yields between slices and tells its progress, ending with every element written
    public async Task ADR0330_the_write_yields_and_tells_its_progress()
    {
        var told = new Told();
        var yields = 0;
        var options = new XlsxSaveOptions
        {
            SliceBudget = TimeSpan.Zero,
            Progress = told,
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        };

        await Saved.WriteAsync(Numbers(5_000), options);

        Assert.True(yields > 10, $"{yields} yields");
        Assert.Contains(told.All, p => p is { Sheet: -1, Elements: 0, TotalElements: 0 });
        var writing = told.All.Where(p => p.Sheet >= 0).ToList();
        Assert.True(writing.Count > 10);
        Assert.All(writing, p => Assert.Equal((1, 5_001L), (p.Sheets, p.TotalElements)));
        Assert.Equal(writing.Select(p => p.Elements).Order(), writing.Select(p => p.Elements));
        Assert.Equal(new XlsxSaveProgress(0, 1, 5_001, 5_001), writing[^1]);
    }

    [Fact] // ADR-0330 §9, ticket 06: cancelled while the file is built, the write stops between slices and nothing reaches the stream
    public async Task ADR0330_a_cancelled_write_stops_between_slices_and_writes_nothing()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var told = new Told();
        using var stream = new MemoryStream();
        var options = new XlsxSaveOptions
        {
            SliceBudget = TimeSpan.Zero,
            Progress = told,
            Yield = () =>
            {
                if (told.All.Any(p => p.Elements > 1_000)) cancel.Cancel();
                return ValueTask.CompletedTask;
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await XlsxWorkbookWriter.WriteAsync(Numbers(5_000), stream, options, cancel.Token));

        Assert.Equal(0, stream.Length);
        Assert.True(told.All.Max(p => p.Elements) < 5_001);
    }

    [Fact] // ADR-0330 §9, ticket 06: the defaults pace a write as a read is paced
    public void ADR0330_the_defaults_pace_a_write_as_a_read()
    {
        var defaults = new XlsxSaveOptions();

        Assert.Equal(new ExGrid.Data.Xlsx.XlsxReadOptions().SliceBudget, defaults.SliceBudget);
        Assert.Null(defaults.Progress);
        Assert.Null(defaults.Yield);
    }
}
