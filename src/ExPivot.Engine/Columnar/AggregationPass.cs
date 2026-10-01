using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>
/// One question's pass over a Snapshot (ADR-0060/0066): every held row's Items in the placed
/// fields, the rows a Hidden Item leaves out, the leaf of each included row, and the parts asked
/// for, accumulated at each leaf. It reads the Snapshot's slices column by column, a chunk of rows
/// at a time: each field's Items for the chunk, then the leaves, then each field in Values.
/// <para>
/// When it is kept (<c>keepRows</c>), it remembers each stored row's leaf, and is what the bundled
/// source holds of its answer: a Change Batch is folded into it (<see cref="Fold"/>) rather than
/// read again from a million records (ADR-0067). The rows of each leaf are then chained in slice
/// order, the first time a leaf has to be recomputed, so that a recompute reads that leaf's rows
/// and no others.
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
    private readonly Action<long>? _rowsRead;
    private Snapshot _snapshot;
    private int _slice = -1;
    private long _sliceStart;
    private long _sliceEnd;

    // The rows kept (ADR-0067), numbered across the slices in slice order: each slice's first
    // row's number, and each stored row's leaf — −1 for a row no leaf holds: left out by a Hidden
    // Item, or no longer held. A row's leaf never changes while it is held, since its values
    // never do; a removed row's number is never handed out again.
    private readonly List<int> _sliceBase = [];
    private int _numbered;
    private int[] _leafOf = [];

    // Each leaf's rows as a chain through the row numbers, in slice order: made the first time a
    // leaf is recomputed, and extended as a batch brings rows. A row a batch removed stays in its
    // chain, and a recompute skips it (its _leafOf is −1); a compaction drops the whole pass.
    private int[]? _next;
    private int[] _first = [];
    private int[] _last = [];

    // While a batch is folded in, the leaves its rows went to.
    private HashSet<int>? _added;

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

    /// <summary>How many rows the last <see cref="Fold"/> recomputed parts from, for the tests and
    /// the measurements: the rows of the leaves it could not bring up to date by subtraction.</summary>
    internal long RecomputedRows { get; private set; }

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

    /// <summary>Ends the pass: every open run of exact numbers is folded.</summary>
    public void Complete() => Slicer.Run(CompleteAsync(Slicer.Unsliced));

    /// <summary>Ends the pass in slices (PV-40): every leaf's open run of exact numbers folded, a
    /// piece of leaves at a time.</summary>
    public async ValueTask CompleteAsync(Slicer slicer)
    {
        foreach (var values in _values)
            await slicer.ForAsync(values.RunLeaves, values.Flush, weight: 2).ConfigureAwait(false);
        _slice = -1;
    }

    /// <summary>
    /// The Leaf Aggregates (ADR-0066): the leaves that have records, in the order they were made;
    /// each axis field's Items that some leaf carries, a text Item spelled by its first spelling
    /// among the records present; and each field in Values, finished.
    /// </summary>
    public PivotAnswer Answer(string sourceVersion) => Slicer.Run(AnswerAsync(sourceVersion, Slicer.Unsliced));

    /// <summary>
    /// <see cref="Answer"/> assembled in slices (ADR-0066, PV-40): each step over the leaves, the
    /// Items or the finished parts a piece at a time, yielding whenever the slice is spent. It only
    /// reads the pass, which nothing may change meanwhile: a pass held for live data defers the
    /// batches applied while it is assembled (<see cref="Defer"/>).
    /// </summary>
    public async ValueTask<PivotAnswer> AnswerAsync(string sourceVersion, Slicer slicer)
    {
        if (Refusal is not null)
            return PivotAnswer.Refused(Refusal);
        var records = _leaves.Records;
        var made = _leaves.Count;
        // The leaves that have records, in the order they were made.
        var order = new int[made];
        var leafCount = 0;
        await slicer.ForAsync(made, (from, to) =>
        {
            for (var leaf = from; leaf < to; leaf++)
            {
                if (records[leaf] > 0)
                    order[leafCount++] = leaf;
            }
        }).ConfigureAwait(false);
        var axes = new PivotAnswerAxis[_axis.Length];
        for (var level = 0; level < _axis.Length; level++)
        {
            var space = _axis[level];
            var itemOfLeaf = _leaves.ItemOfLeaf[level];
            // The Items some leaf carries, renumbered in the order they were met, each spelled.
            var renumbered = new int[space.Count];
            await slicer.ForAsync(leafCount, (from, to) =>
            {
                for (var n = from; n < to; n++)
                    renumbered[itemOfLeaf[order[n]]] = 1;
            }).ConfigureAwait(false);
            var keys = new List<PivotItemKey>();
            await slicer.ForAsync(renumbered.Length, (from, to) =>
            {
                for (var item = from; item < to; item++)
                {
                    if (renumbered[item] == 0)
                        continue;
                    renumbered[item] = keys.Count;
                    keys.Add(space.PublicKeyOf(item));
                }
            }, weight: 2).ConfigureAwait(false);
            var leaves = new int[leafCount];
            await slicer.ForAsync(leafCount, (from, to) =>
            {
                for (var n = from; n < to; n++)
                    leaves[n] = renumbered[itemOfLeaf[order[n]]];
            }).ConfigureAwait(false);
            var field = level < _query.Rows.Count ? _query.Rows[level].Field : _query.Columns[level - _query.Rows.Count].Field;
            // Every leaf names one of the Items just listed, by construction.
            axes[level] = PivotAnswerAxis.Made(field, [.. keys], leaves);
        }
        var counts = new long[leafCount];
        await slicer.ForAsync(leafCount, (from, to) =>
        {
            for (var n = from; n < to; n++)
                counts[n] = records[order[n]];
        }).ConfigureAwait(false);
        var values = new PivotAnswerValues[_values.Length];
        for (var v = 0; v < _values.Length; v++)
        {
            var finished = await _values[v].FinishedAsync(order, leafCount, slicer).ConfigureAwait(false);
            values[v] = new PivotAnswerValues(_values[v].Field, finished, leafCount);
        }
        return new PivotAnswer(sourceVersion, axes[.._query.Rows.Count], axes[_query.Rows.Count..], leafCount, counts, values);
    }

    // ---- Assembled while batches arrive (ADR-0067) ------------------------------------------------

    // The batches applied while an answer was being assembled from the pass, in order: folded in
    // once no assembly reads it, so that no answer is half a batch.
    private List<SnapshotChange>? _deferred;

    /// <summary>How many answers are being assembled from the pass now, in slices. While any is,
    /// a Change Batch waits (<see cref="Defer"/>) rather than folds; the source counts them under
    /// its lock.</summary>
    public int Assembling { get; set; }

    /// <summary>Whether batches wait to be folded in.</summary>
    public bool HasDeferred => _deferred is { Count: > 0 };

    /// <summary>Keeps a batch applied while an answer is assembled from the pass, to fold in once
    /// none is.</summary>
    public void Defer(SnapshotChange change) => (_deferred ??= []).Add(change);

    /// <summary>Folds in the batches that waited, in order (<see cref="Fold"/>). False when one
    /// cannot be, and the pass is then dropped; true when none waited.</summary>
    public bool FoldDeferred()
    {
        if (_deferred is not { } deferred)
            return true;
        _deferred = null;
        foreach (var change in deferred)
        {
            if (!Fold(change))
                return false;
        }
        return true;
    }

    // ---- Reading -------------------------------------------------------------------------------

    private void Enter(int slice)
    {
        _sliceStart = _slice < 0 ? 0 : _sliceEnd;
        _slice = slice;
        var read = _snapshot.Slice(slice);
        _sliceEnd = _sliceStart + read.Length;
        foreach (var values in _values)
            values.BeginSegment(read);
        if (_keepRows && slice == _sliceBase.Count)
            Number(read.Length);
    }

    // Numbers a slice's rows after the rows numbered so far, none of them in a leaf yet.
    private void Number(int length)
    {
        if ((long)_numbered + length > Array.MaxLength)
            throw new InvalidOperationException("A pass keeps the leaves of at most Array.MaxLength stored rows.");
        _sliceBase.Add(_numbered);
        _numbered += length;
        if (_leafOf.Length < _numbered)
        {
            var size = (int)Math.Min(Array.MaxLength, Math.Max(_numbered, (long)_leafOf.Length * 2));
            var old = _leafOf.Length;
            Array.Resize(ref _leafOf, size);
            _leafOf.AsSpan(old).Fill(-1);
            if (_next is not null)
                Array.Resize(ref _next, size);
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
                Keep(_sliceBase[slice.Index] + at, leaves);
            if (_added is { } added)
            {
                foreach (var leaf in leaves)
                {
                    if (leaf >= 0)
                        added.Add(leaf);
                }
            }
            done += n;
        }
        return true;
    }

    // Remembers the leaves of rows numbered from `first`, and chains them when the chains are made.
    private void Keep(int first, ReadOnlySpan<int> leaves)
    {
        leaves.CopyTo(_leafOf.AsSpan(first));
        if (_next is null)
            return;
        EnsureChains(_leaves.Count);
        for (var i = 0; i < leaves.Length; i++)
        {
            if (leaves[i] >= 0)
                Chain(leaves[i], first + i);
        }
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
        // the data, and so is a text Item's first spelling (ADR-0060).
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

    // ---- Folding a Change Batch in (ADR-0067) ----------------------------------------------------

    /// <summary>
    /// Brings the pass from <see cref="SnapshotChange.Before"/> to <see cref="SnapshotChange.After"/>
    /// without reading the rows the batch did not touch (ADR-0067). False when it cannot — the
    /// batch compacted the Snapshot, so rows moved, or the answer would now pass the cap on leaves
    /// — and the pass is then dropped and the question asked afresh.
    /// <list type="number">
    /// <item>Each removed row, read in Before, leaves its leaf: the records, the counts and an
    /// Integer or Decimal sum by subtraction — an integer's, which is exact in any order; a leaf
    /// whose other parts it touched is marked for recomputing. The codes it carried are counted
    /// out, so a spelling can leave.</item>
    /// <item>The batch's slices — every row the batch brought is in them, at the end of the slice
    /// order — are read as the pass reads any slice: Items that appear bring their leaves, and
    /// the parts of a leaf gain the new rows as a fresh pass would gain them, last.</item>
    /// <item>The marked leaves are recomputed from their rows, in slice order, as a fresh pass
    /// computes them — every step of a <c>double</c> counts. A leaf left with no record leaves the
    /// answer, and so do Items no leaf carries.</item>
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
        RecomputedRows = 0;

        // 1. What the batch removed, read in Before.
        foreach (var row in change.Removed)
        {
            var slice = before.Slice(row.Slice);
            foreach (var space in _axis)
                space.Unmap(slice, row.Offset);
            foreach (var filter in _filters)
                filter.Unmap(slice, row.Offset);
            var number = _sliceBase[row.Slice] + row.Offset;
            var leaf = _leafOf[number];
            _leafOf[number] = -1;
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
        var added = _added = [];
        try
        {
            for (var s = before.SliceCount; s < after.SliceCount; s++)
            {
                Enter(s);
                var slice = after.Slice(s);
                if (!Read(slice, 0, slice.Length))
                    return false;
            }
            Complete();
        }
        finally
        {
            _added = null;
        }

        // 3. The leaves whose parts cannot be subtracted, from their rows. An exact sum is an
        // integer, which subtraction and addition keep exactly; one past 128 bits is a double,
        // whose every step counts, and a leaf whose sum is one is recomputed whenever it is touched.
        for (var v = 0; v < _values.Length; v++)
        {
            var values = _values[v];
            if (values.SumsExactly)
            {
                foreach (var leaf in added)
                {
                    if (values.Columns.IsInexactSum(leaf))
                        marked[v].Add(leaf);
                }
            }
            if (marked[v].Count == 0)
                continue;
            // A pass that kept no rows' leaves cannot find a leaf's rows: it is asked afresh.
            if (!_keepRows)
                return false;
            Recompute(values, marked[v]);
        }
        return true;
    }

    // A field's marked leaves, from nothing, over the rows each holds, in slice order — the
    // operations a fresh pass performs on that leaf, in the same order, so the parts come out the
    // same to the last bit. A few leaves are read row by row along their chains; when the marked
    // leaves hold a good part of the rows, which scattered reads would cost more than a sweep, the
    // column is swept in order instead, every other leaf's rows passed over.
    private void Recompute(ValueAccumulator values, HashSet<int> leaves)
    {
        long rows = 0;
        foreach (var leaf in leaves)
            rows += _leaves.Records[leaf];
        if (rows * SweepFraction > _numbered)
        {
            Sweep(values, leaves);
            return;
        }
        if (_next is null)
            MakeChains();
        Span<int> one = stackalloc int[1];
        foreach (var leaf in leaves)
        {
            values.Reset(leaf);
            one[0] = leaf;
            var s = 0;
            var current = -1;
            var slice = default(SnapshotSlice);
            for (var number = _first[leaf]; number >= 0; number = _next![number])
            {
                if (_leafOf[number] != leaf)
                    continue;
                while (s + 1 < _sliceBase.Count && number >= _sliceBase[s + 1])
                    s++;
                if (s != current)
                {
                    slice = _snapshot.Slice(s);
                    values.BeginSegment(slice, leaf);
                    current = s;
                }
                values.Accumulate(slice, number - _sliceBase[s], one);
                RecomputedRows++;
            }
            values.Flush(leaf);
        }
    }

    /// <summary>Marked leaves holding more than one row in this many are recomputed by a sweep.</summary>
    private const int SweepFraction = 16;

    private void Sweep(ValueAccumulator values, HashSet<int> leaves)
    {
        var marked = new bool[_leaves.Count];
        foreach (var leaf in leaves)
        {
            values.Reset(leaf);
            marked[leaf] = true;
        }
        var masked = _leafBuffer;
        for (var s = 0; s < _sliceBase.Count; s++)
        {
            var slice = _snapshot.Slice(s);
            values.BeginSegment(slice);
            var first = _sliceBase[s];
            for (var at = 0; at < slice.Length; at += Chunk)
            {
                var n = Math.Min(Chunk, slice.Length - at);
                var any = false;
                for (var i = 0; i < n; i++)
                {
                    var leaf = _leafOf[first + at + i];
                    if (leaf >= 0 && marked[leaf])
                    {
                        masked[i] = leaf;
                        any = true;
                        RecomputedRows++;
                    }
                    else
                    {
                        masked[i] = -1;
                    }
                }
                if (any)
                    values.Accumulate(slice, at, masked.AsSpan(0, n));
            }
        }
        values.Flush();
    }

    // Chains every leaf's rows, in slice order, from the leaves the rows were kept with.
    private void MakeChains()
    {
        _next = new int[_leafOf.Length];
        EnsureChains(_leaves.Count);
        for (var number = 0; number < _numbered; number++)
        {
            var leaf = _leafOf[number];
            if (leaf >= 0)
                Chain(leaf, number);
        }
    }

    private void EnsureChains(int leaves)
    {
        if (_first.Length >= leaves)
            return;
        var size = Math.Max(leaves, Math.Max(16, _first.Length * 2));
        var old = _first.Length;
        Array.Resize(ref _first, size);
        Array.Resize(ref _last, size);
        _first.AsSpan(old).Fill(-1);
        _last.AsSpan(old).Fill(-1);
    }

    private void Chain(int leaf, int number)
    {
        _next![number] = -1;
        var last = _last[leaf];
        if (last < 0)
            _first[leaf] = number;
        else
            _next[last] = number;
        _last[leaf] = number;
    }
}
