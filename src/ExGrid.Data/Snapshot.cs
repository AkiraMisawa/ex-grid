using System.Collections;
using System.Diagnostics.CodeAnalysis;
using ExGrid.Data.Storage;

namespace ExGrid.Data;

/// <summary>
/// An immutable copy of the Consumer's tabular data at one version, held column by column
/// (ADR-0064). It is data, not a query engine: it does not sort, filter, group or aggregate — the
/// sources that read it do, each to its own semantics.
/// <para>
/// It is read slice by slice (<see cref="Slice(int)"/>), each slice a storage segment whose columns
/// are plain spans. A change to the data is a new Snapshot, made by <see cref="Apply"/>, which shares
/// every segment the Change Batch did not touch; this one stays exactly as it was. A Snapshot is safe
/// to read from several threads at once.
/// </para>
/// </summary>
public sealed class Snapshot
{
    private readonly SnapshotColumn[] columns;
    private readonly bool hasRemovals;
    private SnapshotRows? rows;

    internal Snapshot(
        Shape shape,
        Segment[] segments,
        RemovalSet?[] removals,
        int baseSegmentCount,
        KeyIndex? baseKeys,
        TextStore?[] stores,
        int[] textCounts,
        int rowCount,
        int nextPosition,
        long version)
    {
        Shape = shape;
        Segments = segments;
        Removals = removals;
        BaseSegmentCount = baseSegmentCount;
        BaseKeys = baseKeys;
        RowCount = rowCount;
        NextPosition = nextPosition;
        Version = version;
        hasRemovals = Array.Exists(removals, r => r is not null);
        columns = new SnapshotColumn[shape.Columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            var column = shape.Columns[i];
            columns[i] = column.Kind switch
            {
                SnapshotKind.Text => new TextColumn(column, stores[i]!, textCounts[i]),
                SnapshotKind.Decimal => new DecimalColumn(column),
                SnapshotKind.Double => new DoubleColumn(column),
                SnapshotKind.Integer => new IntegerColumn(column),
                SnapshotKind.Date => new DateColumn(column, this),
                SnapshotKind.Boolean => new BooleanColumn(column),
                _ => throw new InvalidOperationException($"Unknown kind {column.Kind}."),
            };
        }
        Columns = Array.AsReadOnly(columns);
    }

    /// <summary>The rows this version holds.</summary>
    public int RowCount { get; }

    /// <summary>The version: each Change Batch applied moves it on by one.</summary>
    public long Version { get; }

    /// <summary>The columns, in the order they were declared.</summary>
    public IReadOnlyList<SnapshotColumn> Columns { get; }

    /// <summary>The column named <paramref name="name"/>, matched ordinally.</summary>
    /// <exception cref="KeyNotFoundException">The Snapshot has no such column; the message names it.</exception>
    public SnapshotColumn this[string name]
        => TryGetColumn(name, out var column)
            ? column
            : throw new KeyNotFoundException($"The Snapshot has no column named '{name}'.");

    /// <summary>The column named <paramref name="name"/>, matched ordinally; false when there is none.</summary>
    public bool TryGetColumn(string name, [NotNullWhen(true)] out SnapshotColumn? column)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Shape.TryGetOrdinal(name, out var ordinal))
        {
            column = columns[ordinal];
            return true;
        }
        column = null;
        return false;
    }

    /// <summary>The Record Key — the Text or Integer column whose value tells each record from every
    /// other — or <see langword="null"/> when the Snapshot has none, and so takes only batches that add.</summary>
    public SnapshotColumn? RecordKey => Shape.KeyOrdinal < 0 ? null : columns[Shape.KeyOrdinal];

    /// <summary>Whether the Snapshot keeps the Consumer's records, by reference and in order, behind
    /// its rows (<see cref="RecordAt"/>). It does when it was built from objects.</summary>
    public bool KeepsRecords => Shape.RecordType is not null;

    /// <summary>The number of slices (<see cref="Slice(int)"/>).</summary>
    public int SliceCount => Segments.Length;

    /// <summary>
    /// The rows this version holds, in order: the order the Snapshot was built in, with a changed
    /// record at the place of the one it replaced and an added record at the end. Indexed in constant
    /// time; the index is made on first read, and costs nothing for a Snapshot no batch has changed.
    /// </summary>
    public IReadOnlyList<SnapshotRow> Rows
    {
        get
        {
            var made = Volatile.Read(ref rows);
            if (made is not null)
                return made;
            made = !hasRemovals && BaseSegmentCount == Segments.Length
                ? new SnapshotRows(RowCount, Shape.Tuning.SegmentShift)
                : new SnapshotRows(BuildOrder(), RowCount);
            return Interlocked.CompareExchange(ref rows, made, null) ?? made;
        }
    }

    internal Shape Shape { get; }

    internal Segment[] Segments { get; }

    /// <summary>Per segment, the rows this version does not hold; <see langword="null"/> when it holds all.</summary>
    internal RemovalSet?[] Removals { get; }

    /// <summary>Segments [0, BaseSegmentCount) are the base: built, or compacted, together.</summary>
    internal int BaseSegmentCount { get; }

    /// <summary>The Record Key of every base row, by base row number.</summary>
    internal KeyIndex? BaseKeys { get; }

    /// <summary>The first position no row of this lineage has had since the base was made.</summary>
    internal int NextPosition { get; }

    /// <summary>One storage segment as this version sees it.</summary>
    public SnapshotSlice Slice(int index)
    {
        if ((uint)index >= (uint)Segments.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"The Snapshot has {Segments.Length} slices.");
        return new SnapshotSlice(this, index, Segments[index], Removals[index]);
    }

    /// <summary>Whether this version holds <paramref name="row"/>: a row of another version, removed or
    /// replaced since, is still stored but no longer held.</summary>
    public bool Holds(SnapshotRow row)
        => (uint)row.Slice < (uint)Segments.Length
            && (uint)row.Offset < (uint)Segments[row.Slice].Length
            && IsHeld(row.Slice, row.Offset);

    /// <summary>The Consumer's record behind <paramref name="row"/> — the object itself — or
    /// <see langword="null"/> when the Snapshot keeps no records.</summary>
    /// <exception cref="ArgumentException">This version does not hold the row.</exception>
    public object? RecordAt(SnapshotRow row) => SegmentOf(row).Records?.Get(row.Offset);

    /// <summary>Whether <paramref name="column"/> is a Blank at <paramref name="row"/>.</summary>
    /// <exception cref="ArgumentException">This version does not hold the row, or the column is another Snapshot's.</exception>
    public bool IsBlank(SnapshotRow row, SnapshotColumn column)
    {
        var ordinal = OrdinalOf(column);
        return SegmentOf(row).Columns[ordinal].IsBlank(row.Offset);
    }

    /// <summary>
    /// One value, boxed, for small reads: a <see cref="string"/>, a <see cref="decimal"/> without
    /// trailing zeros, a <see cref="double"/>, a <see cref="long"/>, a <see cref="DateTime"/> holding
    /// the clock value with <see cref="DateTimeKind.Unspecified"/>, or a <see cref="bool"/>; and
    /// <see langword="null"/> for a Blank. Reading many values is a loop over a slice's spans.
    /// </summary>
    /// <exception cref="ArgumentException">This version does not hold the row, or the column is another Snapshot's.</exception>
    public object? ValueAt(SnapshotRow row, SnapshotColumn column)
    {
        var ordinal = OrdinalOf(column);
        var data = SegmentOf(row).Columns[ordinal];
        var offset = row.Offset;
        if (data.IsBlank(offset))
            return null;
        return data switch
        {
            TextData text => ((TextColumn)columns[ordinal]).Dictionary[text.Codes[offset]],
            DecimalData number => number.ValueAt(offset),
            DoubleData number => number.Values[offset],
            IntegerData number => number.Values[offset],
            DateData date => new DateTime(date.Ticks[offset], DateTimeKind.Unspecified),
            BooleanData flag => flag.Values[offset],
            _ => throw new InvalidOperationException("Unknown column data."),
        };
    }

    /// <summary>
    /// Makes the next Snapshot from a Change Batch (ADR-0064), whole or not at all. The batch is
    /// refused, naming the key, when it changes or removes a key this version does not hold, adds one
    /// it does, names a key twice, or carries a Blank key; a Snapshot without a Record Key takes only
    /// a batch that adds. Nothing of a refused batch is applied, and this Snapshot is never changed.
    /// </summary>
    /// <returns>The next Snapshot, with what it no longer holds and what it newly holds, so that a
    /// reader can fold the batch into what it computed rather than start again.</returns>
    /// <exception cref="SnapshotException">The batch is refused.</exception>
    public SnapshotChange Apply(ChangeBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return BatchApplier.Apply(this, batch);
    }

    internal int OrdinalOf(SnapshotColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var ordinal = column.Ordinal;
        if ((uint)ordinal >= (uint)Shape.Columns.Length || !ReferenceEquals(Shape.Columns[ordinal], column.Shape))
            throw new ArgumentException($"The column '{column.Name}' is not a column of this Snapshot.", nameof(column));
        return ordinal;
    }

    internal bool IsHeld(int slice, int offset) => Removals[slice] is not { } removed || !removed.Contains(offset);

    /// <summary>Each Text column's store and the entries this version sees of it, by ordinal.</summary>
    internal (TextStore?[] Stores, int[] Counts) TextState()
    {
        var stores = new TextStore?[columns.Length];
        var counts = new int[columns.Length];
        for (var i = 0; i < columns.Length; i++)
        {
            if (columns[i] is TextColumn text)
            {
                stores[i] = text.Dictionary.Store;
                counts[i] = text.Dictionary.Count;
            }
        }
        return (stores, counts);
    }

    /// <summary>The row this version holds under <paramref name="key"/> — a Text key's code, or an
    /// Integer key's value.</summary>
    internal bool TryFind(long key, out SnapshotRow row)
    {
        // Newest first: a key's current record is in the newest segment that holds it.
        for (var s = Segments.Length - 1; s >= BaseSegmentCount; s--)
        {
            if (Segments[s].Keys!.TryGet(key, out var offset) && IsHeld(s, offset))
            {
                row = new SnapshotRow(s, offset);
                return true;
            }
        }
        if (BaseKeys is not null && BaseKeys.TryGet(key, out var baseRow))
        {
            var slice = baseRow >> Shape.Tuning.SegmentShift;
            var offset = baseRow & Shape.Tuning.SegmentMask;
            if (IsHeld(slice, offset))
            {
                row = new SnapshotRow(slice, offset);
                return true;
            }
        }
        row = default;
        return false;
    }

    /// <summary>Whether a Date column holds any value this version holds that is not a midnight.</summary>
    internal bool AnyTime(int ordinal)
    {
        for (var s = 0; s < Segments.Length; s++)
        {
            var data = (DateData)Segments[s].Columns[ordinal];
            if (!data.HasTime)
                continue;
            if (Removals[s] is not { } removed)
                return true;
            var skip = removed.Bitmap();
            var ticks = data.Ticks;
            for (var o = 0; o < ticks.Length; o++)
            {
                if (ticks[o] % TimeSpan.TicksPerDay != 0 && !Bits.Get(skip, o))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The rows this version holds, in order, in the first <see cref="RowCount"/> entries: each held
    /// row is put at its position, and the gaps left by rows no longer held are closed — linear in the
    /// positions handed out since the base was made.
    /// </summary>
    internal SnapshotRow[] BuildOrder()
    {
        var byPosition = new SnapshotRow[Math.Max(NextPosition, 1)];
        byPosition.AsSpan().Fill(new SnapshotRow(-1, -1));
        for (var s = 0; s < Segments.Length; s++)
        {
            var segment = Segments[s];
            var skip = Removals[s]?.Bitmap();
            for (var o = 0; o < segment.Length; o++)
            {
                if (skip is not null && Bits.Get(skip, o))
                    continue;
                byPosition[segment.PositionOf(o)] = new SnapshotRow(s, o);
            }
        }
        var written = 0;
        for (var p = 0; p < NextPosition; p++)
        {
            if (byPosition[p].Slice >= 0)
                byPosition[written++] = byPosition[p];
        }
        if (written != RowCount)
            throw new InvalidOperationException($"The Snapshot holds {written} rows by position and counts {RowCount}.");
        return byPosition;
    }

    private Segment SegmentOf(SnapshotRow row)
    {
        if (!Holds(row))
            throw new ArgumentException($"This version of the Snapshot does not hold the row {row}.", nameof(row));
        return Segments[row.Slice];
    }
}

/// <summary>A version's rows in order: computed from the row number for a Snapshot no batch has
/// changed, read from an index otherwise.</summary>
internal sealed class SnapshotRows : IReadOnlyList<SnapshotRow>
{
    private readonly SnapshotRow[]? order;
    private readonly int shift;
    private readonly int mask;

    public SnapshotRows(int count, int shift)
    {
        Count = count;
        this.shift = shift;
        mask = (1 << shift) - 1;
    }

    public SnapshotRows(SnapshotRow[] order, int count)
    {
        this.order = order;
        Count = count;
    }

    public int Count { get; }

    public SnapshotRow this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"The Snapshot holds {Count} rows.");
            return order is null ? new SnapshotRow(index >> shift, index & mask) : order[index];
        }
    }

    public IEnumerator<SnapshotRow> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
            yield return order is null ? new SnapshotRow(i >> shift, i & mask) : order[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
