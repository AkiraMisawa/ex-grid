namespace ExGrid.Data;

/// <summary>
/// How a load paces itself (ADR-0063): it works in slices sized by time, yields between them so
/// that a browser keeps painting while a million rows load, and reports its progress after each.
/// </summary>
public sealed class SnapshotLoadOptions
{
    /// <summary>Told how far the load has come after every slice, and once more at the end.</summary>
    public IProgress<SnapshotProgress>? Progress { get; init; }

    /// <summary>
    /// How long one slice works before the load yields. A slice always does some work, so a zero
    /// budget yields after every smallest step. The default is 30 ms.
    /// </summary>
    public TimeSpan SliceBudget { get; init; } = TimeSpan.FromMilliseconds(30);

    /// <summary>
    /// What the load awaits between slices. By default it is <see cref="Task.Yield"/>, in a browser
    /// too, where it gives the page a turn of its event loop to paint in before the next slice.
    /// </summary>
    public Func<ValueTask>? Yield { get; init; }
}

/// <summary>How far a load has come.</summary>
/// <param name="Rows">The rows read so far.</param>
/// <param name="TotalRows">The rows there are to read, when that is known.</param>
/// <param name="Bytes">The bytes read so far, for a load that reads bytes.</param>
/// <param name="TotalBytes">The bytes there are to read, when that is known.</param>
public readonly record struct SnapshotProgress(long Rows, long? TotalRows, long? Bytes = null, long? TotalBytes = null);
