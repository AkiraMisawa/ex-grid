namespace ExGrid.Data.Storage;

/// <summary>
/// A run of rows stored together, one <see cref="ColumnData"/> per column. Immutable, and shared by
/// every version that holds any of its rows: a Change Batch adds segments and masks rows, and never
/// rewrites one.
/// <para>
/// Every row has a position, which orders the rows a version holds. A base segment's rows sit at
/// consecutive positions from <see cref="FirstPosition"/>; a segment a batch made carries each row's
/// position — a changed record's is the one its old version had, so it keeps its place, and an added
/// record's is past every position before it, so it goes at the end. A batch writes its rows in
/// position order; a merge of batch segments keeps each row's position but not that order.
/// </para>
/// </summary>
internal sealed class Segment(
    int length,
    ColumnData[] columns,
    RecordStore? records,
    int firstPosition,
    int[]? positions,
    KeyIndex? keys)
{
    public int Length { get; } = length;

    public ColumnData[] Columns { get; } = columns;

    public RecordStore? Records { get; } = records;

    public int FirstPosition { get; } = firstPosition;

    /// <summary>Each row's position, for a segment batches made; <see langword="null"/> for a base segment.</summary>
    public int[]? Positions { get; } = positions;

    /// <summary>Key to offset, for a segment a batch made; a base segment's keys are in the base's index.</summary>
    public KeyIndex? Keys { get; } = keys;

    public int PositionOf(int offset) => Positions is null ? FirstPosition + offset : Positions[offset];
}
