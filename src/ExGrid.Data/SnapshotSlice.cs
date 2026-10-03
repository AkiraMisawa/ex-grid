using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// One storage segment of a Snapshot as one version sees it: every column's values for the segment's
/// rows as plain spans, so that reading a whole column is a loop over each slice's span.
/// <para>
/// A segment is shared by every version that holds any of its rows; which of them this version holds
/// is <see cref="Removed"/>, which belongs to the version. A row the version does not hold still has
/// its values in the spans, and a reader skips it. The slot of a Blank holds the kind's zero — 0,
/// false, code -1, or ticks 0 — so a plain sum over a span is not changed by Blanks; a Blank is told
/// apart by <see cref="Blanks"/>.
/// </para>
/// </summary>
public readonly struct SnapshotSlice
{
    private readonly Snapshot snapshot;
    private readonly Segment segment;
    private readonly RemovalSet? removed;

    internal SnapshotSlice(Snapshot snapshot, int index, Segment segment, RemovalSet? removed)
    {
        this.snapshot = snapshot;
        Index = index;
        this.segment = segment;
        this.removed = removed;
    }

    /// <summary>The slice's index in its Snapshot (<see cref="SnapshotRow.Slice"/>).</summary>
    public int Index { get; }

    /// <summary>The rows the segment stores, whether this version holds them or not; every span is this long.</summary>
    public int Length => segment.Length;

    /// <summary>The rows of the segment this version holds.</summary>
    public int HeldCount => segment.Length - (removed?.Count ?? 0);

    /// <summary>
    /// One bit per row, least significant bit first, set for a row this version does not hold — one
    /// removed, or the old version of a changed record. Empty when the version holds every row.
    /// </summary>
    public ReadOnlySpan<ulong> Removed => removed is null ? default : removed.Bitmap();

    /// <summary>The codes of a Text column into its <see cref="TextColumn.Dictionary"/>; -1 is a Blank.</summary>
    public ReadOnlySpan<int> Codes(TextColumn column) => ((TextData)Data(column)).Codes;

    /// <summary>The values of a Decimal column, as this slice holds them.</summary>
    public DecimalValues Decimals(DecimalColumn column) => new((DecimalData)Data(column));

    /// <summary>The values of a Double column, exactly as they came.</summary>
    public ReadOnlySpan<double> Doubles(DoubleColumn column) => ((DoubleData)Data(column)).Values;

    /// <summary>The values of an Integer column.</summary>
    public ReadOnlySpan<long> Integers(IntegerColumn column) => ((IntegerData)Data(column)).Values;

    /// <summary>The values of a Date column, as clock-value ticks (<see cref="DateTime.Ticks"/>).</summary>
    public ReadOnlySpan<long> Ticks(DateColumn column) => ((DateData)Data(column)).Ticks;

    /// <summary>The values of a Boolean column.</summary>
    public ReadOnlySpan<bool> Booleans(BooleanColumn column) => ((BooleanData)Data(column)).Values;

    /// <summary>
    /// One bit per row, least significant bit first, set for a Blank; empty when the slice holds no
    /// Blank in the column. For a Text column it says what the codes of -1 say.
    /// </summary>
    public ReadOnlySpan<ulong> Blanks(SnapshotColumn column) => Data(column).Blanks;

    private ColumnData Data(SnapshotColumn column) => segment.Columns[snapshot.OrdinalOf(column)];
}

/// <summary>
/// A Decimal column's values in one slice, held exactly (ADR-0064). When <see cref="Scale"/> is 0 or
/// more, <see cref="Scaled"/> holds each value × 10^<see cref="Scale"/>, so a sum is an integer
/// addition; when it is -1, some value did not fit a long at one power of ten, and
/// <see cref="Exact"/> holds the values. Each slice has its own scale: the largest number of decimal
/// places among its values, trailing zeros not counted.
/// </summary>
public readonly ref struct DecimalValues
{
    internal DecimalValues(DecimalData data)
    {
        Scale = data.Scale;
        Scaled = data.Scaled;
        Exact = data.Exact;
    }

    /// <summary>The power of ten <see cref="Scaled"/> holds the values at, or -1 when the slice holds
    /// <see cref="Exact"/>.</summary>
    public int Scale { get; }

    /// <summary>Each value × 10^<see cref="Scale"/>; empty when <see cref="Scale"/> is -1.</summary>
    public ReadOnlySpan<long> Scaled { get; }

    /// <summary>Each value, when <see cref="Scale"/> is -1; empty otherwise.</summary>
    public ReadOnlySpan<decimal> Exact { get; }

    /// <summary>The number of values, one per row of the slice.</summary>
    public int Length => Scale >= 0 ? Scaled.Length : Exact.Length;

    /// <summary>The value at <paramref name="offset"/>, without trailing zeros, however the slice holds it.</summary>
    public decimal this[int offset] => Scale >= 0 ? DecimalMath.FromScaled(Scaled[offset], Scale) : Exact[offset];
}
