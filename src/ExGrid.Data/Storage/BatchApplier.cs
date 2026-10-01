using System.Globalization;

namespace ExGrid.Data.Storage;

/// <summary>
/// Applies a Change Batch (ADR-0063): every check first, so a refused batch leaves nothing behind;
/// then the next version, which shares every segment of the one before, adds one segment (or a few)
/// holding the changed and added records, and masks the rows it no longer holds in removal sets of
/// its own.
/// </summary>
internal static class BatchApplier
{
    public static SnapshotChange Apply(Snapshot before, ChangeBatch batch)
    {
        var shape = before.Shape;
        var changed = batch.Changed is null ? null : Match(before, batch.Changed, "changed");
        var added = batch.Added is null ? null : Match(before, batch.Added, "added");

        if (shape.Key is not { } key)
        {
            if (batch.Changed?.RowCount > 0 || batch.RemovedKeys.Count > 0)
                throw new SnapshotException("The Snapshot has no Record Key, so it takes only a batch that adds, and this batch changes or removes records.");
            return Execute(before, [], [], added);
        }

        var (leaving, moves) = key.Kind == SnapshotKind.Text
            ? Check<string, TextKeys>(before, batch, changed, added, new TextKeys(before, key))
            : Check<long, IntegerKeys>(before, batch, changed, added, new IntegerKeys(before, key));
        return Execute(before, leaving, moves, added, changed);
    }

    /// <summary>How the batch's records meet the Snapshot's columns: by name, each of the same kind,
    /// none missing and none extra; and their records, when the Snapshot keeps its own.</summary>
    private static SourceMap Match(Snapshot target, Snapshot source, string role)
    {
        var shape = target.Shape;
        var ordinals = new int[shape.Columns.Length];
        foreach (var column in shape.Columns)
        {
            if (!source.Shape.TryGetOrdinal(column.Name, out var ordinal))
                throw new SnapshotException(null, column.Name, $"the batch's {role} records have no such column.");
            var kind = source.Shape.Columns[ordinal].Kind;
            if (kind != column.Kind)
                throw new SnapshotException(null, column.Name, $"the column is {column.Kind} in the Snapshot and {kind} in the batch's {role} records.");
            ordinals[column.Ordinal] = ordinal;
        }
        if (source.Shape.Columns.Length != shape.Columns.Length)
        {
            var extra = source.Shape.Columns.First(c => !shape.TryGetOrdinal(c.Name, out _));
            throw new SnapshotException(null, extra.Name, $"the batch's {role} records carry a column the Snapshot does not have.");
        }
        if (shape.RecordType is { } type && source.RowCount > 0 && source.Shape.RecordType != type)
        {
            throw new SnapshotException(source.Shape.RecordType is null
                ? $"The Snapshot keeps its records, and the batch's {role} records keep none; a batch for it is built from records."
                : $"The Snapshot keeps records of type {type.Name}, and the batch's {role} records are of type {source.Shape.RecordType.Name}.");
        }
        return new SourceMap(source, ordinals, new int[]?[shape.Columns.Length]);
    }

    /// <summary>
    /// Every key rule, checked against the Snapshot before the batch: a removed or changed key is held,
    /// an added key is not (unless the batch removes it), no key comes twice in one role or in two
    /// roles that contradict, and no key is Blank. Yields the rows Before holds that After will not,
    /// and each changed record's place.
    /// </summary>
    private static (List<SnapshotRow> Leaving, List<Move> Moves) Check<TKey, TKind>(
        Snapshot before, ChangeBatch batch, SourceMap? changed, SourceMap? added, TKind kind)
        where TKey : notnull
        where TKind : struct, IKeyKind<TKey>
    {
        var keyName = before.Shape.Key!.Name;
        var leaving = new List<SnapshotRow>(batch.RemovedKeys.Count + (changed?.Snapshot.RowCount ?? 0));
        var removed = new HashSet<TKey>(batch.RemovedKeys.Count, kind.Comparer);
        foreach (var raw in batch.RemovedKeys)
        {
            if (!kind.TryNormalize(raw, out var key))
                throw Refuse(keyName, raw, $"the batch removes the key {Keys.Show(raw)}, which is not of the Record Key's kind, {before.Shape.Key!.Kind}.");
            if (!removed.Add(key))
                throw Refuse(keyName, raw, $"the batch removes the key {Keys.Show(raw)} twice.");
            if (!kind.TryFind(key, out var row))
                throw Refuse(keyName, raw, $"the batch removes the key {Keys.Show(raw)}, which the Snapshot does not hold.");
            leaving.Add(row);
        }

        var moves = new List<Move>(changed?.Snapshot.RowCount ?? 0);
        var changedKeys = new HashSet<TKey>(changed?.Snapshot.RowCount ?? 0, kind.Comparer);
        if (changed is not null)
        {
            var rows = changed.Snapshot.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                if (!kind.TryRead(changed, rows[i], out var key))
                    throw new SnapshotException(i + 1L, keyName, "the Record Key of this changed record is Blank.");
                var shown = kind.Box(key);
                if (removed.Contains(key))
                    throw Refuse(keyName, shown, $"the batch both changes and removes the key {Keys.Show(shown)}.");
                if (!changedKeys.Add(key))
                    throw Refuse(keyName, shown, $"the batch changes the key {Keys.Show(shown)} twice.");
                if (!kind.TryFind(key, out var row))
                    throw Refuse(keyName, shown, $"the batch changes the key {Keys.Show(shown)}, which the Snapshot does not hold.");
                leaving.Add(row);
                moves.Add(new Move(before.Segments[row.Slice].PositionOf(row.Offset), rows[i]));
            }
        }

        if (added is not null)
        {
            var rows = added.Snapshot.Rows;
            var addedKeys = new HashSet<TKey>(rows.Count, kind.Comparer);
            for (var i = 0; i < rows.Count; i++)
            {
                if (!kind.TryRead(added, rows[i], out var key))
                    throw new SnapshotException(i + 1L, keyName, "the Record Key of this added record is Blank.");
                var shown = kind.Box(key);
                if (!addedKeys.Add(key))
                    throw Refuse(keyName, shown, $"the batch adds the key {Keys.Show(shown)} twice.");
                if (changedKeys.Contains(key) || (!removed.Contains(key) && kind.TryFind(key, out _)))
                    throw Refuse(keyName, shown, $"the batch adds the key {Keys.Show(shown)}, which the Snapshot already holds.");
            }
        }
        return (leaving, moves);
    }

    private static SnapshotChange Execute(Snapshot before, List<SnapshotRow> leaving, List<Move> moves, SourceMap? added, SourceMap? changed = null)
    {
        var shape = before.Shape;
        var tuning = shape.Tuning;

        // The new rows, in the order their positions give: the changed records at the places of the
        // records they replace, then the added records past every position so far.
        moves.Sort(static (a, b) => a.Position.CompareTo(b.Position));
        var sources = new List<SourceMap>(2);
        var changedSource = changed is null || moves.Count == 0 ? -1 : Add(sources, changed);
        var addedRows = added?.Snapshot.Rows;
        var addedCount = addedRows?.Count ?? 0;
        var addedSource = addedCount == 0 ? -1 : Add(sources, added!);
        var total = moves.Count + addedCount;
        var rows = new SourceRow[total];
        var positions = new int[total];
        for (var i = 0; i < moves.Count; i++)
        {
            rows[i] = new SourceRow(changedSource, moves[i].Source.Slice, moves[i].Source.Offset);
            positions[i] = moves[i].Position;
        }
        for (var j = 0; j < addedCount; j++)
        {
            var row = addedRows![j];
            rows[moves.Count + j] = new SourceRow(addedSource, row.Slice, row.Offset);
            positions[moves.Count + j] = before.NextPosition + j;
        }

        var (stores, counts) = before.TextState();
        RemapTexts(before, rows, sources, stores, counts);

        // One segment, or a few when the batch is larger than a segment, each with its own key index.
        var newSegments = new List<Segment>();
        for (var start = 0; start < total; start += tuning.SegmentLength)
        {
            var length = Math.Min(tuning.SegmentLength, total - start);
            newSegments.Add(Gather.Build(shape, rows.AsSpan(start, length), [.. sources], 0, positions[start..(start + length)], indexKeys: true));
        }

        var segments = new Segment[before.Segments.Length + newSegments.Count];
        before.Segments.CopyTo(segments, 0);
        newSegments.CopyTo(segments, before.Segments.Length);

        // The rows Before holds and After does not, masked in removal sets of After's own.
        var removals = new RemovalSet?[segments.Length];
        before.Removals.CopyTo(removals, 0);
        leaving.Sort(static (a, b) => a.Slice != b.Slice ? a.Slice.CompareTo(b.Slice) : a.Offset.CompareTo(b.Offset));
        var offsets = new List<int>();
        for (var i = 0; i < leaving.Count;)
        {
            var slice = leaving[i].Slice;
            offsets.Clear();
            for (; i < leaving.Count && leaving[i].Slice == slice; i++)
                offsets.Add(leaving[i].Offset);
            removals[slice] = RemovalSet.With(removals[slice], segments[slice].Length, offsets);
        }

        var after = new Snapshot(
            shape,
            segments,
            removals,
            before.BaseSegmentCount,
            before.BaseKeys,
            stores,
            counts,
            before.RowCount - leaving.Count + total,
            before.NextPosition + addedCount,
            before.Version + 1);

        // The rows this batch brought are what After holds in the slices it added — unless a
        // compaction moves them, and then they are wherever it put them.
        var compaction = Compaction.Needed(after);
        if (compaction != Compaction.Kind.None)
        {
            var addresses = new List<SnapshotRow>(total);
            var compacted = compaction == Compaction.Kind.Whole
                ? Compaction.Whole(after, before.Segments.Length, addresses)
                : Compaction.Batches(after, before.Segments.Length, addresses);
            return new SnapshotChange(before, compacted, [.. leaving], [.. addresses], compacted: true);
        }

        var addedAt = new SnapshotRow[total];
        for (var i = 0; i < total; i++)
            addedAt[i] = new SnapshotRow(before.Segments.Length + (i >> tuning.SegmentShift), i & tuning.SegmentMask);
        return new SnapshotChange(before, after, [.. leaving], addedAt, compacted: false);
    }

    /// <summary>
    /// Gives each Text column's source codes the lineage's codes, appending text the lineage does not
    /// hold yet in the order the new rows bring it; a code keeps its meaning in every version.
    /// </summary>
    private static void RemapTexts(Snapshot before, SourceRow[] rows, List<SourceMap> sources, TextStore?[] stores, int[] counts)
    {
        var shape = before.Shape;
        for (var c = 0; c < shape.Columns.Length; c++)
        {
            if (shape.Columns[c].Kind != SnapshotKind.Text)
                continue;
            var dictionary = ((TextColumn)before.Columns[c]).Dictionary;
            foreach (var source in sources)
            {
                var remap = new int[((TextColumn)source.Snapshot.Columns[source.Ordinals[c]]).Dictionary.Count];
                remap.AsSpan().Fill(-2);
                source.Remaps[c] = remap;
            }
            List<string>? pending = null;
            Dictionary<string, int>? pendingCodes = null;
            foreach (var row in rows)
            {
                var source = sources[row.Source];
                var ordinal = source.Ordinals[c];
                var code = ((TextData)source.Snapshot.Segments[row.Slice].Columns[ordinal]).Codes[row.Offset];
                if (code < 0 || source.Remaps[c]![code] != -2)
                    continue;
                var text = ((TextColumn)source.Snapshot.Columns[ordinal]).Dictionary[code];
                if (!dictionary.TryGetCode(text, out var target))
                {
                    pending ??= [];
                    pendingCodes ??= new Dictionary<string, int>(StringComparer.Ordinal);
                    if (!pendingCodes.TryGetValue(text, out target))
                    {
                        target = dictionary.Count + pending.Count;
                        pending.Add(text);
                        pendingCodes.Add(text, target);
                    }
                }
                source.Remaps[c]![code] = target;
            }
            if (pending is not null)
            {
                stores[c] = dictionary.Store.Append(dictionary.Count, pending);
                counts[c] = dictionary.Count + pending.Count;
            }
        }
    }

    private static int Add(List<SourceMap> sources, SourceMap source)
    {
        sources.Add(source);
        return sources.Count - 1;
    }

    private static SnapshotException Refuse(string keyColumn, object key, string reason)
        => new(null, keyColumn, reason) { Key = key };

    /// <summary>A changed record: the position it takes, and where the batch holds it.</summary>
    private readonly record struct Move(int Position, SnapshotRow Source);

    private interface IKeyKind<TKey>
        where TKey : notnull
    {
        IEqualityComparer<TKey> Comparer { get; }

        /// <summary>The key of a batch record; false when it is Blank.</summary>
        bool TryRead(SourceMap source, SnapshotRow row, out TKey key);

        /// <summary>A removed key as this kind holds it; false when it is of the other kind.</summary>
        bool TryNormalize(object raw, out TKey key);

        /// <summary>The row Before holds under the key.</summary>
        bool TryFind(TKey key, out SnapshotRow row);

        object Box(TKey key);
    }

    private readonly struct TextKeys(Snapshot before, ColumnShape column) : IKeyKind<string>
    {
        public IEqualityComparer<string> Comparer => StringComparer.Ordinal;

        public bool TryRead(SourceMap source, SnapshotRow row, out string key)
        {
            var ordinal = source.Ordinals[column.Ordinal];
            var code = ((TextData)source.Snapshot.Segments[row.Slice].Columns[ordinal]).Codes[row.Offset];
            key = code < 0 ? string.Empty : ((TextColumn)source.Snapshot.Columns[ordinal]).Dictionary[code];
            return code >= 0;
        }

        public bool TryNormalize(object raw, out string key)
        {
            key = raw as string ?? string.Empty;
            return raw is string;
        }

        public bool TryFind(string key, out SnapshotRow row)
        {
            if (((TextColumn)before.Columns[column.Ordinal]).Dictionary.TryGetCode(key, out var code))
                return before.TryFind(code, out row);
            row = default;
            return false;
        }

        public object Box(string key) => key;
    }

    private readonly struct IntegerKeys(Snapshot before, ColumnShape column) : IKeyKind<long>
    {
        public IEqualityComparer<long> Comparer => EqualityComparer<long>.Default;

        public bool TryRead(SourceMap source, SnapshotRow row, out long key)
        {
            var data = (IntegerData)source.Snapshot.Segments[row.Slice].Columns[source.Ordinals[column.Ordinal]];
            key = data.Values[row.Offset];
            return !data.IsBlank(row.Offset);
        }

        public bool TryNormalize(object raw, out long key)
        {
            key = raw is long value ? value : 0;
            return raw is long;
        }

        public bool TryFind(long key, out SnapshotRow row) => before.TryFind(key, out row);

        public object Box(long key) => key;
    }
}

/// <summary>
/// Compaction, which copies rows into new segments, in order, keeping every code and every value;
/// only addresses move. Two kinds, by what has grown:
/// <list type="bullet">
/// <item>When the segments batches made pile up, their rows — only those still held — are merged
/// into as few segments as hold them. The base is shared as it is, so this costs what the batches
/// brought, not the records.</item>
/// <item>When the rows batches made grow large beside the base, or most stored rows are no longer
/// held, the whole version is copied into a new base.</item>
/// </list>
/// </summary>
internal static class Compaction
{
    public enum Kind
    {
        None,
        Batches,
        Whole,
    }

    public static Kind Needed(Snapshot version)
    {
        var tuning = version.Shape.Tuning;
        long baseRows = 0;
        long batchRows = 0;
        for (var s = 0; s < version.Segments.Length; s++)
        {
            if (s < version.BaseSegmentCount)
                baseRows += version.Segments[s].Length;
            else
                batchRows += version.Segments[s].Length;
        }
        var stored = baseRows + batchRows;
        var dead = stored - version.RowCount;
        if (batchRows > Math.Max(baseRows / tuning.BatchRowsDivisor, tuning.SegmentLength)
            || dead > Math.Max(stored / 2, tuning.SegmentLength))
        {
            return Kind.Whole;
        }
        return version.Segments.Length - version.BaseSegmentCount > tuning.MaxBatchSegments ? Kind.Batches : Kind.None;
    }

    /// <summary>
    /// A new base holding every row <paramref name="version"/> holds, in order. The new addresses of
    /// the rows held in slices from <paramref name="firstNew"/> on are added to
    /// <paramref name="moved"/>.
    /// </summary>
    public static Snapshot Whole(Snapshot version, int firstNew, List<SnapshotRow> moved)
    {
        var shape = version.Shape;
        var tuning = shape.Tuning;
        var order = version.BuildOrder();
        SourceMap[] source = [SourceMap.Identity(version)];
        var segments = new List<Segment>();
        var rows = new SourceRow[Math.Min(tuning.SegmentLength, version.RowCount)];
        for (var start = 0; start < version.RowCount; start += tuning.SegmentLength)
        {
            var length = Math.Min(tuning.SegmentLength, version.RowCount - start);
            for (var i = 0; i < length; i++)
            {
                var row = order[start + i];
                rows[i] = new SourceRow(0, row.Slice, row.Offset);
                if (row.Slice >= firstNew)
                    moved.Add(new SnapshotRow(segments.Count, i));
            }
            segments.Add(Gather.Build(shape, rows.AsSpan(0, length), source, start, null, indexKeys: false));
        }
        KeyIndex? keys = null;
        if (shape.Key is not null)
        {
            var indexer = new KeyIndexer(shape, segments, version.RowCount);
            indexer.Step(long.MaxValue);
            keys = indexer.Index;
        }
        var (stores, counts) = version.TextState();
        return Writers.Base(shape, segments, stores, counts, keys, version.RowCount, version.Version);
    }

    /// <summary>
    /// The same version with the rows its batch segments still hold merged into as few segments as
    /// hold them — each segment's rows in turn, so most are copied in runs — each with its key index;
    /// every row keeps its position. The base segments, their removal sets and the base's key index
    /// are shared as they are. The new addresses of the rows held in slices from
    /// <paramref name="firstNew"/> on are added to <paramref name="moved"/>.
    /// </summary>
    public static Snapshot Batches(Snapshot version, int firstNew, List<SnapshotRow> moved)
    {
        var shape = version.Shape;
        var tuning = shape.Tuning;
        var held = new List<(int Position, int Slice, int Offset)>();
        for (var s = version.BaseSegmentCount; s < version.Segments.Length; s++)
        {
            var segment = version.Segments[s];
            var skip = version.Removals[s]?.Bitmap();
            for (var o = 0; o < segment.Length; o++)
            {
                if (skip is null || !Bits.Get(skip, o))
                    held.Add((segment.PositionOf(o), s, o));
            }
        }

        SourceMap[] source = [SourceMap.Identity(version)];
        var segments = new List<Segment>(version.Segments.Take(version.BaseSegmentCount));
        for (var start = 0; start < held.Count; start += tuning.SegmentLength)
        {
            var length = Math.Min(tuning.SegmentLength, held.Count - start);
            var rows = new SourceRow[length];
            var positions = new int[length];
            for (var i = 0; i < length; i++)
            {
                var (position, slice, offset) = held[start + i];
                rows[i] = new SourceRow(0, slice, offset);
                positions[i] = position;
                if (slice >= firstNew)
                    moved.Add(new SnapshotRow(segments.Count, i));
            }
            segments.Add(Gather.Build(shape, rows, source, 0, positions, indexKeys: true));
        }
        var removals = new RemovalSet?[segments.Count];
        Array.Copy(version.Removals, removals, version.BaseSegmentCount);
        var (stores, counts) = version.TextState();
        return new Snapshot(
            shape,
            [.. segments],
            removals,
            version.BaseSegmentCount,
            version.BaseKeys,
            stores,
            counts,
            version.RowCount,
            version.NextPosition,
            version.Version);
    }
}
