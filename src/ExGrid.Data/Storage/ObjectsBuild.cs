using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExGrid.Data.Storage;

/// <summary>
/// One build of a Snapshot from the Consumer's records, done in steps so that an asynchronous build
/// can yield between slices. The records are read a chunk at a time, column by column, into
/// segments whose arrays are allocated at their final size; the records themselves are kept, by
/// reference and in order, beside them.
/// </summary>
internal sealed class ObjectsBuild<T>
{
    private readonly IReadOnlyList<T> records;
    private readonly Shape shape;
    private readonly ObjectColumn<T>[] columns;
    private readonly ColumnWriter[] writers;
    private readonly TextInterner?[] interners;
    private readonly List<Segment> segments = [];
    private readonly T[]? buffer;
    private T[]? segmentRecords;
    private int segmentStart;
    private int segmentLength;
    private int row;
    private KeyIndexer? keys;

    public ObjectsBuild(Shape shape, ObjectColumn<T>[] columns, IReadOnlyList<T> records)
    {
        this.shape = shape;
        this.columns = columns;
        this.records = records;
        Total = records.Count;
        interners = new TextInterner?[columns.Length];
        writers = new ColumnWriter[columns.Length];
        for (var c = 0; c < columns.Length; c++)
        {
            if (columns[c].Kind == SnapshotKind.Text)
                interners[c] = new TextInterner();
            writers[c] = Writers.Create(columns[c].Kind, interners[c]);
        }
        if (records is not T[] && records is not List<T>)
            buffer = new T[shape.Tuning.ChunkLength];
    }

    public int Total { get; }

    public int Rows => row;

    /// <summary>Reads records until all are read or the clock passes <paramref name="deadline"/>.</summary>
    public bool ReadStep(long deadline)
    {
        while (row < Total)
        {
            if (records.Count != Total)
                throw new InvalidOperationException("The records changed while a Snapshot was being built from them.");
            if (segmentLength == 0)
                BeginSegment();
            var end = Math.Min(Math.Min(row + shape.Tuning.ChunkLength, Total), segmentStart + segmentLength);
            var chunk = Chunk(row, end);
            for (var c = 0; c < columns.Length; c++)
                columns[c].Read(chunk, row, writers[c]);
            if (segmentRecords is not null)
                chunk.CopyTo(segmentRecords.AsSpan(row - segmentStart));
            row = end;
            if (row == segmentStart + segmentLength)
                SealSegment();
            if (Stopwatch.GetTimestamp() >= deadline)
                break;
        }
        return row == Total;
    }

    /// <summary>Indexes the Record Keys, refusing a Blank key or one carried twice, until done or the
    /// clock passes <paramref name="deadline"/>.</summary>
    public bool IndexStep(long deadline)
    {
        if (shape.Key is not { } key)
            return true;
        keys ??= new KeyIndexer(shape, segments, Total, interners[key.Ordinal]?.Count ?? -1) { TextOf = code => interners[key.Ordinal]![code] };
        return keys.Step(deadline);
    }

    public Snapshot Finish()
    {
        var stores = new TextStore?[columns.Length];
        var counts = new int[columns.Length];
        for (var c = 0; c < columns.Length; c++)
        {
            if (interners[c] is { } interner)
            {
                counts[c] = interner.Count;
                stores[c] = interner.ToStore();
            }
        }
        return Writers.Base(shape, segments, stores, counts, keys?.Index, Total, version: 0);
    }

    private void BeginSegment()
    {
        segmentStart = row;
        segmentLength = Math.Min(shape.Tuning.SegmentLength, Total - row);
        foreach (var writer in writers)
            writer.Begin(segmentLength, segmentLength);
        segmentRecords = new T[segmentLength];
    }

    private void SealSegment()
    {
        var data = new ColumnData[writers.Length];
        for (var c = 0; c < writers.Length; c++)
            data[c] = writers[c].Seal();
        segments.Add(new Segment(segmentLength, data, new RecordStore<T>(segmentRecords!), segmentStart, null, null));
        segmentRecords = null;
        segmentLength = 0;
    }

    private ReadOnlySpan<T> Chunk(int start, int end)
    {
        // A read-only span over the array itself, so an array of a derived type is read as it is.
        if (records is T[] array)
            return new ReadOnlySpan<T>(array, start, end - start);
        if (records is List<T> list)
            return CollectionsMarshal.AsSpan(list).Slice(start, end - start);
        for (var i = start; i < end; i++)
            buffer![i - start] = records[i];
        return buffer.AsSpan(0, end - start);
    }
}

/// <summary>Makes writers, and a first version from segments.</summary>
internal static class Writers
{
    public static ColumnWriter Create(SnapshotKind kind, TextInterner? interner) => kind switch
    {
        SnapshotKind.Text => new TextColumnWriter(interner),
        SnapshotKind.Decimal => new DecimalColumnWriter(),
        SnapshotKind.Double => new DoubleColumnWriter(),
        SnapshotKind.Integer => new IntegerColumnWriter(),
        SnapshotKind.Date => new DateColumnWriter(),
        SnapshotKind.Boolean => new BooleanColumnWriter(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind."),
    };

    /// <summary>The first version of a lineage: every segment is the base, and holds every row.</summary>
    public static Snapshot Base(Shape shape, IReadOnlyList<Segment> segments, TextStore?[] stores, int[] counts, KeyIndex? keys, int rowCount, long version)
        => new(shape, [.. segments], new RemovalSet?[segments.Count], segments.Count, keys, stores, counts, rowCount, rowCount, version);
}
