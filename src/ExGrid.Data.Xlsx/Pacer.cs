using System.Diagnostics;

namespace ExGrid.Data.Xlsx;

/// <summary>
/// Paces a read in slices sized by time (ADR-0330 §9, as ExGrid.Data's loads are paced, ADR-0064): a slice works until
/// its budget is spent, then the read reports its progress and yields, so a browser — which has one thread — keeps
/// painting while a large worksheet is read. Cancellation is looked at between batches.
/// </summary>
internal sealed class Pacer
{
    private readonly long budget;
    private readonly Func<ValueTask> yield;
    private readonly IProgress<XlsxProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private long sliceStart;

    public Pacer(XlsxReadOptions options, CancellationToken cancellationToken)
    {
        budget = options.SliceBudget <= TimeSpan.Zero ? 0 : (long)(options.SliceBudget.TotalSeconds * Stopwatch.Frequency);
        yield = options.Yield ?? DefaultYield;
        progress = options.Progress;
        this.cancellationToken = cancellationToken;
        sliceStart = Stopwatch.GetTimestamp();
    }

    public async ValueTask CheckpointAsync(XlsxProgress state)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Stopwatch.GetTimestamp() < sliceStart + budget)
            return;
        progress?.Report(state);
        await yield().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        sliceStart = Stopwatch.GetTimestamp();
    }

    public void Report(XlsxProgress state) => progress?.Report(state);

    private static async ValueTask DefaultYield() => await Task.Yield();
}
