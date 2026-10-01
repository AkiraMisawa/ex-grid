using System.Diagnostics;

namespace ExGrid.Data.Storage;

/// <summary>
/// Paces a load in slices sized by time (ADR-0063): a slice works until its budget is spent, then
/// the load reports its progress and yields, so a browser — which has one thread — keeps painting
/// while a million rows load. Cancellation is looked at between slices.
/// </summary>
internal sealed class Pacer
{
    private readonly long budget;
    private readonly Func<ValueTask> yield;
    private readonly IProgress<SnapshotProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private long sliceStart;

    public Pacer(SnapshotLoadOptions? options, CancellationToken cancellationToken)
    {
        var slice = (options ?? Defaults).SliceBudget;
        budget = slice <= TimeSpan.Zero ? 0 : (long)(slice.TotalSeconds * Stopwatch.Frequency);
        yield = options?.Yield ?? DefaultYield;
        progress = options?.Progress;
        this.cancellationToken = cancellationToken;
        sliceStart = Stopwatch.GetTimestamp();
    }

    private static SnapshotLoadOptions Defaults { get; } = new();

    /// <summary>The clock reading at which the current slice has spent its budget.</summary>
    public long Deadline => sliceStart + budget;

    /// <summary>Whether the current slice has spent its budget.</summary>
    public bool Due => Stopwatch.GetTimestamp() >= Deadline;

    public CancellationToken CancellationToken => cancellationToken;

    /// <summary>Ends the slice: reports <paramref name="state"/>, yields, and starts the next slice,
    /// unless the load was cancelled meanwhile.</summary>
    public async ValueTask EndSliceAsync(SnapshotProgress state)
    {
        progress?.Report(state);
        await yield().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        sliceStart = Stopwatch.GetTimestamp();
    }

    public void Report(SnapshotProgress state) => progress?.Report(state);

    /// <summary>A browser gets a delay of 1 ms, which lets it paint; elsewhere the load lets other
    /// work run and carries on.</summary>
    internal static ValueTask DefaultYield()
        => OperatingSystem.IsBrowser() ? new ValueTask(Task.Delay(1)) : YieldOnce();

    private static async ValueTask YieldOnce() => await Task.Yield();
}
