namespace ExGrid.Data.Storage;

/// <summary>A row to copy into a new segment: which source, and where that source stores it.</summary>
internal readonly record struct SourceRow(int Source, int Slice, int Offset);

/// <summary>
/// A Snapshot rows are copied from, and how its columns meet the target's: the source ordinal of
/// each target column, and for each Text column the target code of each source code (built as the
/// copy meets them), or none when the codes are already the target's.
/// </summary>
internal sealed class SourceMap(Snapshot snapshot, int[] ordinals, int[]?[] remaps)
{
    public Snapshot Snapshot { get; } = snapshot;

    public int[] Ordinals { get; } = ordinals;

    public int[]?[] Remaps { get; } = remaps;

    /// <summary>The same lineage's rows, codes and all, as a compaction copies them.</summary>
    public static SourceMap Identity(Snapshot snapshot)
        => new(snapshot, Enumerable.Range(0, snapshot.Shape.Columns.Length).ToArray(), new int[]?[snapshot.Shape.Columns.Length]);
}

/// <summary>Copies rows of Snapshots into one new segment, column by column.</summary>
internal static class Gather
{
    public static Segment Build(Shape shape, ReadOnlySpan<SourceRow> rows, SourceMap[] sources, int firstPosition, int[]? positions, bool indexKeys)
    {
        var length = rows.Length;
        var data = new ColumnData[shape.Columns.Length];
        Span<int> bits = stackalloc int[4];
        for (var c = 0; c < data.Length; c++)
        {
            switch (shape.Columns[c].Kind)
            {
                case SnapshotKind.Text:
                {
                    var writer = new TextColumnWriter(null);
                    writer.Begin(length, length);
                    foreach (var row in rows)
                    {
                        var (source, column) = Source(sources, row, c);
                        var code = ((TextData)column).Codes[row.Offset];
                        writer.AddCode(code < 0 || source.Remaps[c] is not { } remap ? code : remap[code]);
                    }
                    data[c] = writer.Seal();
                    break;
                }
                case SnapshotKind.Decimal:
                {
                    var writer = new DecimalColumnWriter();
                    writer.Begin(length, length);
                    foreach (var row in rows)
                    {
                        var column = (DecimalData)Source(sources, row, c).Column;
                        if (column.IsBlank(row.Offset))
                            writer.AddBlank();
                        else if (column.Scaled is { } scaled)
                            writer.AddScaled(scaled[row.Offset], column.Scale);
                        else
                            writer.Add(column.Exact![row.Offset], bits);
                    }
                    data[c] = writer.Seal();
                    break;
                }
                case SnapshotKind.Double:
                {
                    var writer = new DoubleColumnWriter();
                    writer.Begin(length, length);
                    foreach (var row in rows)
                    {
                        var column = (DoubleData)Source(sources, row, c).Column;
                        if (column.IsBlank(row.Offset))
                            writer.AddBlank();
                        else
                            writer.Add(column.Values[row.Offset]);
                    }
                    data[c] = writer.Seal();
                    break;
                }
                case SnapshotKind.Integer:
                {
                    var writer = new IntegerColumnWriter();
                    writer.Begin(length, length);
                    foreach (var row in rows)
                    {
                        var column = (IntegerData)Source(sources, row, c).Column;
                        if (column.IsBlank(row.Offset))
                            writer.AddBlank();
                        else
                            writer.Add(column.Values[row.Offset]);
                    }
                    data[c] = writer.Seal();
                    break;
                }
                case SnapshotKind.Date:
                {
                    var writer = new DateColumnWriter();
                    writer.Begin(length, length);
                    foreach (var row in rows)
                    {
                        var column = (DateData)Source(sources, row, c).Column;
                        if (column.IsBlank(row.Offset))
                            writer.AddBlank();
                        else
                            writer.Add(column.Ticks[row.Offset]);
                    }
                    data[c] = writer.Seal();
                    break;
                }
                case SnapshotKind.Boolean:
                {
                    var writer = new BooleanColumnWriter();
                    writer.Begin(length, length);
                    foreach (var row in rows)
                    {
                        var column = (BooleanData)Source(sources, row, c).Column;
                        if (column.IsBlank(row.Offset))
                            writer.AddBlank();
                        else
                            writer.Add(column.Values[row.Offset]);
                    }
                    data[c] = writer.Seal();
                    break;
                }
            }
        }

        RecordStore? records = null;
        if (shape.RecordType is not null && length > 0)
        {
            var first = rows[0];
            var gather = sources[first.Source].Snapshot.Segments[first.Slice].Records!.Gather(length);
            foreach (var row in rows)
                gather.Add(sources[row.Source].Snapshot.Segments[row.Slice].Records!, row.Offset);
            records = gather.Seal();
        }

        KeyIndex? keys = null;
        if (indexKeys && shape.KeyOrdinal >= 0)
        {
            var keyData = data[shape.KeyOrdinal];
            var source = KeySource.Of(keyData);
            keys = new KeyIndex(source, length);
            for (var o = 0; o < length; o++)
            {
                if (!keys.TryAdd(o, source.KeyOf(o), out _))
                    throw new InvalidOperationException("A batch carried one key twice past its checks.");
            }
        }

        return new Segment(length, data, records, firstPosition, positions, keys);
    }

    private static (SourceMap Map, ColumnData Column) Source(SourceMap[] sources, SourceRow row, int ordinal)
    {
        var map = sources[row.Source];
        return (map, map.Snapshot.Segments[row.Slice].Columns[map.Ordinals[ordinal]]);
    }
}

/// <summary>
/// Indexes a new base's Record Keys, a chunk of rows at a time so a load can yield between slices,
/// refusing a Blank key and a key carried twice by name (ADR-0063).
/// </summary>
internal sealed class KeyIndexer
{
    private readonly Shape shape;
    private readonly IReadOnlyList<Segment> segments;
    private readonly int rowCount;
    private readonly KeySource source;
    private int next;

    public KeyIndexer(Shape shape, IReadOnlyList<Segment> segments, int rowCount)
    {
        this.shape = shape;
        this.segments = segments;
        this.rowCount = rowCount;
        var key = shape.Key!;
        source = KeySource.Of(key.Kind, segments.Select(s => s.Columns[key.Ordinal]).ToArray(), shape.Tuning.SegmentShift);
        Index = new KeyIndex(source, rowCount);
    }

    public KeyIndex Index { get; }

    public int Rows => next;

    /// <summary>Indexes rows until they are all indexed or the clock passes <paramref name="deadline"/>.</summary>
    public bool Step(long deadline)
    {
        var key = shape.Key!;
        var shift = shape.Tuning.SegmentShift;
        var mask = shape.Tuning.SegmentMask;
        do
        {
            var end = Math.Min(rowCount, next + shape.Tuning.ChunkLength);
            for (; next < end; next++)
            {
                var data = segments[next >> shift].Columns[key.Ordinal];
                if (data.IsBlank(next & mask))
                    throw new SnapshotException(next + 1, key.Name, "the Record Key is Blank.");
                var value = source.KeyOf(next);
                if (!Index.TryAdd(next, value, out var existing))
                {
                    var shown = key.Kind == SnapshotKind.Text ? (object)TextOf((int)value) : value;
                    throw new SnapshotException(next + 1, key.Name, string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"the Record Key {Keys.Show(shown)} is already carried by row {existing + 1:N0}."))
                    {
                        Key = shown,
                    };
                }
            }
        }
        while (next < rowCount && System.Diagnostics.Stopwatch.GetTimestamp() < deadline);
        return next == rowCount;
    }

    /// <summary>A Text key's text, for a refusal. Set by the build, which still holds the dictionary.</summary>
    public Func<int, string> TextOf { get; init; } = code => code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>How a refusal shows a Record Key.</summary>
internal static class Keys
{
    public static string Show(object key)
        => key is string text
            ? $"'{text}'"
            : Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
}
