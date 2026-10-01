using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// How the bundled source shares the thread (ADR-0065): a pass is run in steps of
/// <see cref="PivotSlicing.RecordsPerCheck"/> rows, the clock is read after each, a slice ends once it
/// has run for <see cref="PivotSlicing.Budget"/>, and the thread is yielded between slices. A
/// cancelled question throws at the next slice; a step that answers false has stopped it.
/// </summary>
internal static class SlicedRun
{
    public static async ValueTask RunAsync(long count, Func<long, long, bool> step, PivotSlicing slicing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clock = slicing.TimeProvider;
        long at = 0;
        while (true)
        {
            var sliceStart = clock.GetTimestamp();
            while (at < count)
            {
                var end = Math.Min(count, at + slicing.RecordsPerCheck);
                if (!step(at, end))
                    return;
                at = end;
                if (clock.GetElapsedTime(sliceStart) >= slicing.Budget)
                    break;
            }
            if (at >= count)
                break;
            await slicing.YieldAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>A field's Items over all the data a Snapshot holds (ADR-0059/0065): every held row's
/// Item, not narrowed by any Hidden Item, a text Item spelled by its first spelling among the
/// records present.</summary>
internal sealed class ItemsPass
{
    private readonly Snapshot _snapshot;
    private readonly ItemSpace _space;
    private readonly int[] _buffer = new int[AggregationPass.Chunk];
    private readonly Action<long>? _rowsRead;
    private long[] _rows = new long[16];
    private int _slice = -1;
    private long _sliceStart;
    private long _sliceEnd;

    public ItemsPass(Snapshot snapshot, FieldBinding binding, Action<long>? rowsRead)
    {
        _snapshot = snapshot;
        _space = new ItemSpace(binding, snapshot, null);
        _rowsRead = rowsRead;
        for (var s = 0; s < snapshot.SliceCount; s++)
            RowCount += snapshot.Slice(s).Length;
    }

    public long RowCount { get; }

    public bool Step(long from, long to)
    {
        _rowsRead?.Invoke(to - from);
        while (from < to)
        {
            while (_slice < 0 || from >= _sliceEnd)
            {
                _sliceStart = _slice < 0 ? 0 : _sliceEnd;
                _slice++;
                _sliceEnd = _sliceStart + _snapshot.Slice(_slice).Length;
            }
            var slice = _snapshot.Slice(_slice);
            var removed = slice.Removed;
            var offset = (int)(from - _sliceStart);
            var count = (int)Math.Min(to - from, _sliceEnd - from);
            for (var done = 0; done < count;)
            {
                var n = Math.Min(_buffer.Length, count - done);
                var at = offset + done;
                var items = _buffer.AsSpan(0, n);
                _space.Map(slice, at, items, removed);
                if (_rows.Length < _space.Count)
                    Array.Resize(ref _rows, Math.Max(_space.Count, _rows.Length * 2));
                for (var i = 0; i < n; i++)
                {
                    if (removed.IsEmpty || !Exactly.IsSet(removed, at + i))
                        _rows[items[i]]++;
                }
                done += n;
            }
            from += count;
        }
        return true;
    }

    /// <summary>The Items some held row carries, in the order they were met, each spelled.</summary>
    public List<ItemKey> Keys()
    {
        var keys = new List<ItemKey>();
        for (var item = 0; item < _space.Count; item++)
        {
            if (item < _rows.Length && _rows[item] > 0)
                keys.Add(_space.SpelledKeyOf(item));
        }
        return keys;
    }

    /// <summary>A page of the Items (ADR-0065): those whose invariant text holds the search, ignoring
    /// case, in the source's invariant order, at most as many as asked, with how many there are.</summary>
    public PivotItemPage Page(PivotItemsQuery query, string sourceVersion)
    {
        IEnumerable<ItemKey> matches = Keys();
        if (query.Search is { } search)
            matches = matches.Where(key => key.ToPublic().Value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        var sorted = matches.ToList();
        sorted.Sort(ItemKey.CompareInvariant);
        return new PivotItemPage(sourceVersion, [.. sorted.Take(query.Max).Select(key => key.ToPublic())], sorted.Count);
    }
}

/// <summary>The rows behind a cell (ADR-0062/0065): those carrying every Item of the cell's paths
/// and no Hidden Item of any placed field, in the Snapshot's order.</summary>
internal sealed class DetailsPass
{
    private readonly Snapshot _snapshot;
    private readonly IReadOnlyList<SnapshotRow> _order;
    private readonly (FieldBinding Binding, ItemKey Item)[] _conditions;
    private readonly (FieldBinding Binding, HashSet<ItemKey> Hidden)[] _hidden;
    private readonly int _start;
    private readonly int _count;
    private readonly Action<long>? _rowsRead;

    public DetailsPass(Snapshot snapshot, IReadOnlyDictionary<string, FieldBinding> bindings, PivotDetailsQuery query, Action<long>? rowsRead)
    {
        _snapshot = snapshot;
        _order = snapshot.Rows;
        _conditions = [.. query.RowItems.Concat(query.ColumnItems).Select(step => (bindings[step.Field], ItemKey.FromPublic(step.Item)))];
        _hidden = [.. query.HiddenItems.Where(h => h.HiddenItems.Count > 0)
            .Select(h => (bindings[h.Field], h.HiddenItems.Select(ItemKey.FromPublic).ToHashSet()))];
        _start = query.Start;
        _count = query.Count;
        _rowsRead = rowsRead;
    }

    /// <summary>The rows the Snapshot holds, in its order: what the pass steps over.</summary>
    public long RowCount => _order.Count;

    /// <summary>The page's rows, each with its position in the Snapshot's order.</summary>
    public List<(int Position, SnapshotRow Row)> Matches { get; } = [];

    /// <summary>How many rows are behind the cell.</summary>
    public long Total { get; private set; }

    public bool Step(long from, long to)
    {
        _rowsRead?.Invoke(to - from);
        for (var position = (int)from; position < to; position++)
        {
            var row = _order[position];
            if (!Behind(row))
                continue;
            if (Total >= _start && Total - _start < _count)
                Matches.Add((position, row));
            Total++;
        }
        return true;
    }

    private bool Behind(SnapshotRow row)
    {
        var slice = _snapshot.Slice(row.Slice);
        foreach (var (binding, item) in _conditions)
        {
            if (!RowValues.KeyAt(binding, _snapshot, slice, row.Offset).Equals(item))
                return false;
        }
        foreach (var (binding, hidden) in _hidden)
        {
            if (hidden.Contains(RowValues.KeyAt(binding, _snapshot, slice, row.Offset)))
                return false;
        }
        return true;
    }
}

/// <summary>One row's value of one field, read from a Snapshot's slices.</summary>
internal static class RowValues
{
    /// <summary>The Item a row's value belongs to (ADR-0059), as <see cref="ItemSpace"/> maps it.</summary>
    public static ItemKey KeyAt(FieldBinding binding, Snapshot snapshot, in SnapshotSlice slice, int offset)
    {
        foreach (var bound in binding.Columns)
        {
            switch (bound.Role)
            {
                case ValueRole.Text:
                {
                    var code = slice.Codes((TextColumn)bound.Column)[offset];
                    if (code >= 0)
                        return ItemKey.OfText(((TextColumn)snapshot.Columns[bound.Ordinal]).Dictionary[code]);
                    continue;
                }
                case ValueRole.Exact:
                {
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    var values = slice.Decimals((DecimalColumn)bound.Column);
                    return ItemKey.OfNumber(values.Scale >= 0 ? Exactly.ToDouble(values.Scaled[offset], values.Scale) : (double)values.Exact[offset]);
                }
                case ValueRole.Integer:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return ItemKey.OfNumber(slice.Integers((IntegerColumn)bound.Column)[offset]);
                case ValueRole.Double:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return ItemKey.OfNumber(slice.Doubles((DoubleColumn)bound.Column)[offset]);
                case ValueRole.Date:
                {
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    var date = new DateTime(slice.Ticks((DateColumn)bound.Column)[offset]);
                    return binding.Part is { } part ? ItemKey.OfNumber(PivotDateWords.Of(part, date)) : ItemKey.OfDate(date);
                }
                default:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return ItemKey.OfBoolean(slice.Booleans((BooleanColumn)bound.Column)[offset]);
            }
        }
        return ItemKey.Blank;
    }

    /// <summary>A row's value as a record behind a cell carries it (<see cref="PivotDetailRecord"/>):
    /// text, an exact number as a <c>decimal</c>, a <c>double</c>, a date by its clock value, a
    /// Boolean, or null for a Blank. A date part is its number.</summary>
    public static object? DetailAt(FieldBinding binding, Snapshot snapshot, in SnapshotSlice slice, int offset)
    {
        foreach (var bound in binding.Columns)
        {
            switch (bound.Role)
            {
                case ValueRole.Text:
                {
                    var code = slice.Codes((TextColumn)bound.Column)[offset];
                    if (code >= 0)
                        return ((TextColumn)snapshot.Columns[bound.Ordinal]).Dictionary[code];
                    continue;
                }
                case ValueRole.Exact:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return slice.Decimals((DecimalColumn)bound.Column)[offset];
                case ValueRole.Integer:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return (decimal)slice.Integers((IntegerColumn)bound.Column)[offset];
                case ValueRole.Double:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return slice.Doubles((DoubleColumn)bound.Column)[offset];
                case ValueRole.Date:
                {
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    var date = new DateTime(slice.Ticks((DateColumn)bound.Column)[offset]);
                    return binding.Part is { } part ? PivotDateWords.Of(part, date) : date;
                }
                default:
                    if (Exactly.IsSet(slice.Blanks(bound.Column), offset))
                        continue;
                    return slice.Booleans((BooleanColumn)bound.Column)[offset];
            }
        }
        return null;
    }
}
