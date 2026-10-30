namespace ExGrid.Data;

/// <summary>
/// Where a row of a Snapshot is stored: the slice that holds it and its offset in that slice. A row
/// keeps its address from one version to the next until a compaction moves every row
/// (<see cref="SnapshotChange.Compacted"/>).
/// </summary>
/// <param name="Slice">The index of the slice that stores the row (<see cref="Snapshot.Slice(int)"/>).</param>
/// <param name="Offset">The row's offset within that slice.</param>
public readonly record struct SnapshotRow(int Slice, int Offset);
