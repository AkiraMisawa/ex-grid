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

/// <summary>
/// Copies rows of Snapshots into one new segment, column by column. Rows that lie one after another
/// in one source segment are copied as a run, in bulk, which is most of what a compaction copies.
/// </summary>
internal static class Gather
{
    public static Segment Build(Shape shape, ReadOnlySpan<SourceRow> rows, SourceMap[] sources, int firstPosition, int[]? positions, bool indexKeys)
    {
        var length = rows.Length;
        var runs = Runs(rows);
        var data = new ColumnData[shape.Columns.Length];
        for (var c = 0; c < data.Length; c++)
        {
            var writer = Writers.Create(shape.Columns[c].Kind, null);
            writer.Begin(length, length);
            foreach (var run in runs)
            {
                var source = sources[run.Source];
                var column = source.Snapshot.Segments[run.Slice].Columns[source.Ordinals[c]];
                Copy(column, run.Offset, run.Length, source.Remaps[c], writer);
            }
            data[c] = writer.Seal();
        }

        RecordStore? records = null;
        if (shape.RecordType is not null && length > 0)
        {
            var first = runs[0];
            var gather = sources[first.Source].Snapshot.Segments[first.Slice].Records!.Gather(length);
            foreach (var run in runs)
                gather.AddRange(sources[run.Source].Snapshot.Segments[run.Slice].Records!, run.Offset, run.Length);
            records = gather.Seal();
        }

        KeyIndex? keys = null;
        if (indexKeys && shape.KeyOrdinal >= 0)
        {
            var keySource = KeySource.Of(data[shape.KeyOrdinal]);
            keys = new KeyIndex(keySource, length);
            for (var o = 0; o < length; o++)
            {
                if (!keys.TryAdd(o, keySource.KeyOf(o), out _))
                    throw new InvalidOperationException("A batch carried one key twice past its checks.");
            }
        }

        return new Segment(length, data, records, firstPosition, positions, keys);
    }

    /// <summary>Appends <paramref name="length"/> rows of <paramref name="source"/> from
    /// <paramref name="offset"/>, its Blanks included; a Text column's codes through
    /// <paramref name="remap"/> when it has one.</summary>
    private static void Copy(ColumnData source, int offset, int length, int[]? remap, ColumnWriter writer)
    {
        var first = writer.Count;
        switch (source)
        {
            case TextData text when remap is null:
                ((TextColumnWriter)writer).AddCodes(text.Codes.AsSpan(offset, length), text.Blanks is not null);
                return;
            case TextData text:
                var codes = (TextColumnWriter)writer;
                foreach (var code in text.Codes.AsSpan(offset, length))
                    codes.AddCode(code < 0 ? -1 : remap[code]);
                return;
            case DecimalData { Scaled: { } scaled } number:
                ((DecimalColumnWriter)writer).AddScaledRange(scaled.AsSpan(offset, length), number.Scale);
                break;
            case DecimalData number:
                var decimals = (DecimalColumnWriter)writer;
                Span<int> bits = stackalloc int[4];
                foreach (var value in number.Exact.AsSpan(offset, length))
                    decimals.Add(value, bits);
                break;
            case DoubleData number:
                ((DoubleColumnWriter)writer).AddRange(number.Values.AsSpan(offset, length));
                break;
            case IntegerData number:
                ((IntegerColumnWriter)writer).AddRange(number.Values.AsSpan(offset, length));
                break;
            case DateData date:
                ((DateColumnWriter)writer).AddRange(date.Ticks.AsSpan(offset, length), date.HasTime);
                break;
            case BooleanData flag:
                ((BooleanColumnWriter)writer).AddRange(flag.Values.AsSpan(offset, length));
                break;
        }
        if (source.Blanks is { } blanks)
            writer.MarkBlanks(first, length, blanks, offset);
    }

    /// <summary>The rows as runs: each a stretch of consecutive offsets in one source segment.</summary>
    private static List<(int Source, int Slice, int Offset, int Length)> Runs(ReadOnlySpan<SourceRow> rows)
    {
        var runs = new List<(int, int, int, int)>();
        var i = 0;
        while (i < rows.Length)
        {
            var start = rows[i];
            var length = 1;
            while (i + length < rows.Length
                && rows[i + length].Source == start.Source
                && rows[i + length].Slice == start.Slice
                && rows[i + length].Offset == start.Offset + length)
            {
                length++;
            }
            runs.Add((start.Source, start.Slice, start.Offset, length));
            i += length;
        }
        return runs;
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

    /// <param name="shape">The Snapshot's shape, which has a Record Key.</param>
    /// <param name="segments">The base segments, row r at offset r mod the segment length of segment
    /// r / the segment length.</param>
    /// <param name="rowCount">The rows to index.</param>
    /// <param name="codes">For a Text key, the dictionary's count, which every code lies below, or -1
    /// when it is not known.</param>
    public KeyIndexer(Shape shape, IReadOnlyList<Segment> segments, int rowCount, int codes = -1)
    {
        this.shape = shape;
        this.segments = segments;
        this.rowCount = rowCount;
        var key = shape.Key!;
        source = KeySource.Of(key.Kind, segments.Select(s => s.Columns[key.Ordinal]).ToArray(), shape.Tuning.SegmentShift);
        Index = key.Kind == SnapshotKind.Text && codes >= 0 ? KeyIndex.ForCodes(source, rowCount, codes) : new KeyIndex(source, rowCount);
    }

    public KeyIndex Index { get; }

    public int Rows => next;

    /// <summary>
    /// Indexes rows until they are all indexed or the clock passes <paramref name="deadline"/>, a
    /// chunk at a time, each chunk within one segment: its keys as they lie in the segment's array, up
    /// to its first Blank. A row is refused as a row-by-row index would: the first Blank or the first
    /// key carried twice, whichever comes first.
    /// </summary>
    public bool Step(long deadline)
    {
        var key = shape.Key!;
        var shift = shape.Tuning.SegmentShift;
        var mask = shape.Tuning.SegmentMask;
        var chunk = shape.Tuning.ChunkLength;
        while (next < rowCount)
        {
            var end = Math.Min(rowCount, next + chunk);
            var data = segments[next >> shift].Columns[key.Ordinal];
            var offset = next & mask;
            var blank = FirstBlank(data.Blanks, offset, end - next);
            var added = data is TextData text
                ? Index.TryAdd(next, text.Codes.AsSpan(offset, blank), out var existing)
                : Index.TryAdd(next, ((IntegerData)data).Values.AsSpan(offset, blank), out existing);
            if (added < blank)
            {
                var row = next + added;
                var value = source.KeyOf(row);
                var shown = key.Kind == SnapshotKind.Text ? (object)TextOf((int)value) : value;
                next = row;
                throw new SnapshotException(row + 1, key.Name, string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"the Record Key {Keys.Show(shown)} is already carried by row {existing + 1:N0}."))
                {
                    Key = shown,
                };
            }
            if (blank < end - next)
            {
                next += blank;
                throw new SnapshotException(next + 1, key.Name, "the Record Key is Blank.");
            }
            next = end;
            if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadline)
                break;
        }
        return next == rowCount;
    }

    /// <summary>Where the first Blank lies among <paramref name="length"/> rows from
    /// <paramref name="offset"/>, counted from <paramref name="offset"/>; <paramref name="length"/>
    /// when there is none.</summary>
    private static int FirstBlank(ulong[]? blanks, int offset, int length)
    {
        if (blanks is null)
            return length;
        for (var i = 0; i < length; i++)
        {
            if (Bits.Get(blanks, offset + i))
                return i;
        }
        return length;
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
