using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// One question's pass over a Snapshot (ADR-0059/0065): every held row's Items in the placed
/// fields, the rows a Hidden Item leaves out, the leaf of each included row, and the parts asked
/// for, accumulated at each leaf. It reads the Snapshot's slices column by column, a chunk of rows
/// at a time: each field's Items for the chunk, then the leaves, then each field in Values.
/// <para>
/// When it is kept (<c>keepRows</c>), it remembers each row's leaf, and is what the bundled source
/// holds of its answer: a Change Batch is folded into it (<see cref="Fold"/>) rather than read again
/// from a million records (ADR-0066).
/// </para>
/// </summary>
internal sealed class AggregationPass
{
    /// <summary>The most rows read column by column at once.</summary>
    internal const int Chunk = 4096;

    private readonly PivotQuery _query;
    private readonly ItemSpace[] _axis;
    private readonly ItemSpace[] _filters;
    private readonly LeafIndex _leaves;
    private readonly ValueAccumulator[] _values;
    private readonly int[][] _items;
    private readonly int[] _filterItems = new int[Chunk];
    private readonly byte[] _excluded = new byte[Chunk];
    private readonly int[] _leafBuffer = new int[Chunk];
    private readonly bool _keepRows;
    private readonly List<int[]> _leafOfRow = [];
    private readonly Action<long>? _rowsRead;
    private Snapshot _snapshot;
    private int _slice = -1;
    private long _sliceStart;
    private long _sliceEnd;

    public AggregationPass(Snapshot snapshot, IReadOnlyDictionary<string, FieldBinding> bindings, PivotQuery query, bool keepRows, Action<long>? rowsRead)
    {
        _snapshot = snapshot;
        _query = query;
        _keepRows = keepRows;
        _rowsRead = rowsRead;
        _axis = [.. query.Rows.Concat(query.Columns).Select(f => new ItemSpace(bindings[f.Field], snapshot, f.HiddenItems))];
        _filters = [.. query.Filters.Where(f => f.HiddenItems.Count > 0).Select(f => new ItemSpace(bindings[f.Field], snapshot, f.HiddenItems))];
        _values = [.. query.Values.Select(v => new ValueAccumulator(bindings[v.Field], v.Parts, 16))];
        _items = new int[_axis.Length][];
        for (var level = 0; level < _items.Length; level++)
            _items[level] = new int[Chunk];
        _leaves = new LeafIndex(_axis, query.MaxLeaves);
        RowCount = StoredRows(snapshot);
    }

    public PivotQuery Query => _query;

    /// <summary>The Snapshot the pass has read: the one it began on, or the one a batch made.</summary>
    public Snapshot Snapshot => _snapshot;

    /// <summary>The rows the Snapshot stores, held or not: what the pass steps over.</summary>
    public long RowCount { get; }

    /// <summary>The refusal, when the answer would pass the question's cap on leaves.</summary>
    public PivotSourceRefusal? Refusal { get; private set; }

    /// <summary>The leaves, for the tests.</summary>
    internal LeafIndex Leaves => _leaves;

    /// <summary>Reads stored rows [<paramref name="from"/>, <paramref name="to"/>), in slice order.
    /// False once the question is refused, which stops it at once.</summary>
    public bool Step(long from, long to)
    {
        while (from < to)
        {
            while (_slice < 0 || from >= _sliceEnd)
                Enter(_slice + 1);
            var slice = _snapshot.Slice(_slice);
            var offset = (int)(from - _sliceStart);
            var count = (int)Math.Min(to - from, _sliceEnd - from);
            if (!Read(slice, offset, count))
                return false;
            from += count;
        }
        return true;
    }

    /// <summary>Ends the pass: the last slice's exact parts are folded.</summary>
    public void Complete()
    {
        if (_slice >= 0)
        {
            foreach (var values in _values)
                values.EndSegment();
        }
        _slice = -1;
    }

    /// <summary>
    /// The Leaf Aggregates (ADR-0065): the leaves that have records, in the order they were made;
    /// each axis field's Items that some leaf carries, a text Item spelled by its first spelling
    /// among the records present; and each field in Values, finished.
    /// </summary>
    public PivotAnswer Answer(string sourceVersion)
    {
        if (Refusal is not null)
            return PivotAnswer.Refused(Refusal);
        var records = _leaves.Records;
        var order = new List<int>(_leaves.Count);
        for (var leaf = 0; leaf < _leaves.Count; leaf++)
        {
            if (records[leaf] > 0)
                order.Add(leaf);
        }
        var leafCount = order.Count;
        var axes = new PivotAnswerAxis[_axis.Length];
        for (var level = 0; level < _axis.Length; level++)
        {
            var space = _axis[level];
            var itemOfLeaf = _leaves.ItemOfLeaf[level];
            var renumbered = new int[space.Count];
            foreach (var leaf in order)
                renumbered[itemOfLeaf[leaf]] = 1;
            var keys = new List<PivotItemKey>();
            for (var item = 0; item < renumbered.Length; item++)
            {
                if (renumbered[item] == 0)
                    continue;
                renumbered[item] = keys.Count;
                keys.Add(space.PublicKeyOf(item));
            }
            var leaves = new int[leafCount];
            for (var n = 0; n < leafCount; n++)
                leaves[n] = renumbered[itemOfLeaf[order[n]]];
            var field = level < _query.Rows.Count ? _query.Rows[level].Field : _query.Columns[level - _query.Rows.Count].Field;
            axes[level] = new PivotAnswerAxis(field, [.. keys], leaves, leafCount);
        }
        var counts = new long[leafCount];
        for (var n = 0; n < leafCount; n++)
            counts[n] = records[order[n]];
        var orderSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(order);
        var values = new PivotAnswerValues[_values.Length];
        for (var v = 0; v < _values.Length; v++)
            values[v] = new PivotAnswerValues(_values[v].Field, _values[v].Finished(orderSpan), leafCount);
        return new PivotAnswer(sourceVersion, axes[.._query.Rows.Count], axes[_query.Rows.Count..], leafCount, counts, values);
    }

    // ---- Reading -------------------------------------------------------------------------------

    private void Enter(int slice)
    {
        if (_slice >= 0)
        {
            foreach (var values in _values)
                values.EndSegment();
        }
        _sliceStart = _slice < 0 ? 0 : _sliceEnd;
        _slice = slice;
        var read = _snapshot.Slice(slice);
        _sliceEnd = _sliceStart + read.Length;
        foreach (var values in _values)
            values.BeginSegment(read);
        if (_keepRows)
        {
            while (_leafOfRow.Count <= slice)
                _leafOfRow.Add([]);
            if (_leafOfRow[slice].Length != read.Length)
                _leafOfRow[slice] = new int[read.Length];
        }
    }

    // Rows [offset, +count) of a slice, a chunk at a time.
    private bool Read(in SnapshotSlice slice, int offset, int count)
    {
        var removed = slice.Removed;
        for (var done = 0; done < count;)
        {
            var n = Math.Min(Chunk, count - done);
            var at = offset + done;
            var leaves = _leafBuffer.AsSpan(0, n);
            var consumed = Assign(slice, at, leaves, removed, out var refused);
            _rowsRead?.Invoke(refused ? consumed + 1 : consumed);
            if (refused)
            {
                Refusal = PivotSourceRefusal.TooManyLeaves(_query.MaxLeaves);
                return false;
            }
            foreach (var values in _values)
            {
                values.EnsureCapacity(_leaves.Count);
                values.Accumulate(slice, at, leaves);
            }
            if (_keepRows)
                leaves.CopyTo(_leafOfRow[slice.Index].AsSpan(at));
            done += n;
        }
        return true;
    }

    // The leaves of a chunk's rows: each placed field's Items, the rows a Hidden Item or the
    // version leaves out, and the leaf of every other row.
    private int Assign(in SnapshotSlice slice, int at, Span<int> leaves, ReadOnlySpan<ulong> removed, out bool refused)
    {
        var n = leaves.Length;
        var excluded = _excluded.AsSpan(0, n);
        if (removed.IsEmpty)
        {
            excluded.Clear();
        }
        else
        {
            for (var i = 0; i < n; i++)
                excluded[i] = Exactly.IsSet(removed, at + i) ? (byte)1 : (byte)0;
        }
        // Every axis field's Items are taken for every row, left out or not: Items are over all
        // the data, and so is a text Item's first spelling (ADR-0059).
        for (var level = 0; level < _axis.Length; level++)
        {
            var space = _axis[level];
            var items = _items[level].AsSpan(0, n);
            space.Map(slice, at, items, removed);
            if (space.HidesAny)
                Exclude(space.Hidden, items, excluded);
        }
        foreach (var filter in _filters)
        {
            var items = _filterItems.AsSpan(0, n);
            filter.Map(slice, at, items, removed);
            Exclude(filter.Hidden, items, excluded);
        }
        return _leaves.Assign(_items, excluded, leaves, out refused);
    }

    private static void Exclude(bool[] hidden, ReadOnlySpan<int> items, Span<byte> excluded)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (hidden[items[i]])
                excluded[i] = 1;
        }
    }

    private static long StoredRows(Snapshot snapshot)
    {
        long rows = 0;
        for (var s = 0; s < snapshot.SliceCount; s++)
            rows += snapshot.Slice(s).Length;
        return rows;
    }

    // ---- Folding a Change Batch in (ADR-0066) ----------------------------------------------------

    /// <summary>
    /// Brings the pass from <see cref="SnapshotChange.Before"/> to <see cref="SnapshotChange.After"/>
    /// without reading the rows the batch did not touch (ADR-0066). False when it cannot — the
    /// batch compacted the Snapshot, so rows moved, or the answer would now pass the cap on leaves
    /// — and the pass is then dropped and the question asked afresh.
    /// <list type="number">
    /// <item>Each removed row leaves its leaf: the records and the exact parts by subtraction; a
    /// leaf whose other parts it touched is marked for recomputing. The codes it carried are
    /// counted out, so a spelling can leave.</item>
    /// <item>The batch's slices — every row the batch brought is in them, at the end of the slice
    /// order — are read as the pass reads any slice: Items that appear bring their leaves, and
    /// the parts of a leaf gain the new rows as a fresh pass would gain them, last.</item>
    /// <item>The marked leaves are recomputed from their rows, in slice order, as a fresh pass
    /// computes them. A leaf left with no record leaves the answer, and so do Items no leaf
    /// carries.</item>
    /// </list>
    /// </summary>
    public bool Fold(SnapshotChange change)
    {
        if (change.Compacted || !ReferenceEquals(change.Before, _snapshot))
            return false;
        // A pass that kept no rows' leaves — over a Snapshot without a Record Key, which takes only
        // batches that add — folds additions alone.
        if (!_keepRows && change.Removed.Count > 0)
            return false;
        var before = change.Before;
        var after = change.After;
        var marked = new HashSet<int>[_values.Length];
        for (var v = 0; v < _values.Length; v++)
            marked[v] = [];

        // 1. What the batch removed, read in Before.
        foreach (var row in change.Removed)
        {
            var slice = before.Slice(row.Slice);
            foreach (var space in _axis)
                space.Unmap(slice, row.Offset);
            var leafOfRow = _leafOfRow[row.Slice];
            var leaf = leafOfRow[row.Offset];
            leafOfRow[row.Offset] = -1;
            if (leaf < 0)
                continue;
            _leaves.Records[leaf]--;
            for (var v = 0; v < _values.Length; v++)
            {
                if (!marked[v].Contains(leaf) && !_values[v].TrySubtract(leaf, slice, row.Offset))
                    marked[v].Add(leaf);
            }
        }

        // 2. What the batch added: the slices it made, at the end of the order.
        _snapshot = after;
        foreach (var space in _axis)
            space.Refresh(after);
        foreach (var filter in _filters)
            filter.Refresh(after);
        // A leaf that would pass the cap — empty leaves a batch left behind count too, until the
        // question is asked afresh — drops the pass, and the question is asked again.
        for (var s = before.SliceCount; s < after.SliceCount; s++)
        {
            Enter(s);
            var slice = after.Slice(s);
            if (!Read(slice, 0, slice.Length))
                return false;
        }
        Complete();

        // 3. The leaves whose parts cannot be subtracted, from their rows.
        for (var v = 0; v < _values.Length; v++)
        {
            if (marked[v].Count > 0)
                Recompute(_values[v], marked[v]);
        }
        return true;
    }

    // A field's marked leaves, from nothing, over every row they hold, in slice order.
    private void Recompute(ValueAccumulator values, HashSet<int> leaves)
    {
        var mark = new bool[_leaves.Count];
        foreach (var leaf in leaves)
        {
            values.Columns.ResetCell(leaf);
            mark[leaf] = true;
        }
        var masked = _leafBuffer;
        for (var s = 0; s < _snapshot.SliceCount; s++)
        {
            var slice = _snapshot.Slice(s);
            var leafOfRow = _leafOfRow[s];
            var any = false;
            for (var o = 0; o < leafOfRow.Length && !any; o++)
                any = leafOfRow[o] >= 0 && mark[leafOfRow[o]];
            if (!any)
                continue;
            values.BeginSegment(slice);
            for (var at = 0; at < slice.Length; at += Chunk)
            {
                var n = Math.Min(Chunk, slice.Length - at);
                for (var i = 0; i < n; i++)
                {
                    var leaf = leafOfRow[at + i];
                    masked[i] = leaf >= 0 && mark[leaf] ? leaf : -1;
                }
                values.Accumulate(slice, at, masked.AsSpan(0, n));
            }
            values.EndSegment();
        }
    }
}
