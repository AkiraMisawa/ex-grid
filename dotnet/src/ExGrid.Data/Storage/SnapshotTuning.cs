namespace ExGrid.Data.Storage;

/// <summary>
/// The sizes a Snapshot's storage works with. Fixed for a lineage: every version made from a
/// Snapshot by Change Batches keeps its tuning. The tests shrink it so that a handful of rows
/// spans several segments and compacts.
/// </summary>
/// <param name="SegmentShift">A base segment holds 2^shift rows (the last may hold fewer).</param>
/// <param name="MaxBatchSegments">A version holding more segments made by batches is compacted.</param>
/// <param name="BatchRowsDivisor">A version whose batch segments hold more than the base's rows
/// divided by this (and more than one segment's worth) is compacted.</param>
internal sealed record SnapshotTuning(int SegmentShift = 16, int MaxBatchSegments = 64, int BatchRowsDivisor = 4)
{
    public static readonly SnapshotTuning Default = new();

    public int SegmentLength => 1 << SegmentShift;

    public int SegmentMask => SegmentLength - 1;

    /// <summary>The rows a build reads between two looks at the clock.</summary>
    public int ChunkLength => Math.Min(1024, SegmentLength);
}
