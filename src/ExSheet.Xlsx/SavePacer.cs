using System.Diagnostics;

namespace ExSheet.Xlsx;

/// <summary>
/// Paces a write in slices sized by time (ADR-0330 §9, as a read is paced): a slice works until its budget is spent, then the
/// write reports its progress and yields, so a browser — which has one thread — keeps painting while a large Workbook is
/// written. Cancellation is looked at between batches of elements.
/// </summary>
internal sealed class SavePacer
{
    /// <summary>The elements written between two looks at the clock and the token.</summary>
    private const int Batch = 256;

    private readonly long budget;
    private readonly Func<ValueTask> yield;
    private readonly IProgress<XlsxSaveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private long sliceStart;
    private int sinceLook;

    public SavePacer(XlsxSaveOptions options, CancellationToken cancellationToken)
    {
        budget = options.SliceBudget <= TimeSpan.Zero ? 0 : (long)(options.SliceBudget.TotalSeconds * Stopwatch.Frequency);
        yield = options.Yield ?? DefaultYield;
        progress = options.Progress;
        this.cancellationToken = cancellationToken;
        sliceStart = Stopwatch.GetTimestamp();
    }

    /// <summary>The worksheets the file holds.</summary>
    public int Sheets { get; set; }

    /// <summary>The elements the file writes; 0 until the snapshot is planned.</summary>
    public long Total { get; set; }

    /// <summary>The worksheet being written; -1 while the snapshot is planned.</summary>
    public int Sheet { get; set; } = -1;

    /// <summary>The elements written so far.</summary>
    public long Written { get; private set; }

    /// <summary>
    /// One step of work, which wrote <paramref name="elements"/> (none while the snapshot is planned); every batch of steps
    /// looks at the token and, past the budget, yields.
    /// </summary>
    public ValueTask StepAsync(long elements = 1)
    {
        Written += elements;
        if (++sinceLook < Batch) return ValueTask.CompletedTask;
        sinceLook = 0;
        return CheckpointAsync();
    }

    /// <summary>Looks at the token and, past the budget, reports the progress and yields.</summary>
    public async ValueTask CheckpointAsync()
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Stopwatch.GetTimestamp() < sliceStart + budget) return;
        Report();
        await yield().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        sliceStart = Stopwatch.GetTimestamp();
    }

    /// <summary>Tells the progress as it stands.</summary>
    public void Report() => progress?.Report(new XlsxSaveProgress(Sheet, Sheets, Sheet < 0 ? 0 : Written, Sheet < 0 ? 0 : Total));

    private static async ValueTask DefaultYield() => await Task.Yield();
}
