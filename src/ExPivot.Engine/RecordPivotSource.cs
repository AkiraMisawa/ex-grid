using System.Runtime.InteropServices;

namespace ExPivot.Engine;

/// <summary>
/// <c>PivotSource.From(records, fields)</c>: the bundled source over records in memory, and the
/// reference implementation (ADR-0065). It reads each record through the fields' accessors and
/// answers by the engine's rules (ADR-0059): Items told apart as ADR-0059 tells them, a record
/// carrying a Hidden Item of any placed field left out, one leaf per combination of row and column
/// Items that has records, and only the parts asked for accumulated.
///
/// <para>Its Source Version is fixed for the instance: the records are one state of the data, and
/// a new list is a new source. It works in slices (<see cref="PivotSlicing"/>).</para>
/// </summary>
internal sealed class RecordPivotSource<TRecord> : PivotSource
{
    private readonly IReadOnlyList<TRecord> _records;
    private readonly PivotField<TRecord>[] _fields;
    private readonly Dictionary<string, PivotField<TRecord>> _declared;
    private readonly PivotSlicing _slicing;

    public RecordPivotSource(IReadOnlyList<TRecord> records, IReadOnlyList<PivotField<TRecord>> fields, PivotSlicing slicing)
    {
        ArgumentNullException.ThrowIfNull(records);
        _declared = Declared(fields);
        _fields = [.. fields];
        _records = records;
        _slicing = slicing ?? throw new ArgumentNullException(nameof(slicing));
        SourceVersion = Guid.NewGuid().ToString("N");
    }

    /// <summary>The one state of the data this source answers under.</summary>
    public string SourceVersion { get; }

    public override IReadOnlyList<PivotField> Fields => _fields;

    public override PivotSourceFeatures Features => PivotSourceFeatures.All;

    public override async ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var aggregation = new Aggregation(this, query);
        await RunAsync(aggregation.Step, cancellationToken);
        return aggregation.Answer();
    }

    /// <summary>The same answer, in one pass on the calling thread — for the engine's own
    /// synchronous entry points.</summary>
    public PivotAnswer Aggregate(PivotQuery query)
    {
        if (Refusal(query) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var aggregation = new Aggregation(this, query);
        aggregation.Step(0, _records.Count);
        return aggregation.Answer();
    }

    public override async ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotItemPage.Refused(refusal);
        if (query.SourceVersion != SourceVersion)
            return PivotItemPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
        var listing = new ItemListing(_declared[query.Field].Value, _records);
        await RunAsync(listing.Step, cancellationToken);
        return listing.Page(query, SourceVersion);
    }

    /// <summary>Every Item of a field over all the records, first spellings kept, in the order
    /// first seen — for the engine's own synchronous entry points.</summary>
    public IReadOnlyList<ItemKey> AllItems(string field)
    {
        var listing = new ItemListing(_declared[field].Value, _records);
        listing.Step(0, _records.Count);
        return listing.Keys;
    }

    public override async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Refusal(query) is { } refusal)
            return PivotDetailPage.Refused(refusal);
        if (query.SourceVersion != SourceVersion)
            return PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
        var search = new DetailSearch(this, query);
        await RunAsync(search.Step, cancellationToken);
        return search.Page();
    }

    /// <summary>The records behind a cell, all of them, on the calling thread, whatever the
    /// question's version — for the engine's own synchronous entry points, which hand it the
    /// records they mean.</summary>
    public IReadOnlyList<TRecord> RecordsBehind(PivotDetailsQuery query)
    {
        if (Refusal(query) is { } refusal)
            throw new InvalidOperationException(refusal.Message);
        var search = new DetailSearch(this, query);
        search.Step(0, _records.Count);
        return search.Matches;
    }

    /// <summary>Nothing to refresh: the records are one state of the data, and a new list is a
    /// new source (ADR-0065).</summary>
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Runs <paramref name="step"/> over the records in slices (ADR-0065): the clock is read every
    /// <see cref="PivotSlicing.RecordsPerCheck"/> records, a slice ends once it has run for
    /// <see cref="PivotSlicing.Budget"/>, and the thread is yielded between slices. A cancelled
    /// question throws at the next slice. A step that answers false has stopped the question.
    /// </summary>
    private async ValueTask RunAsync(Func<int, int, bool> step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = _records.Count;
        var clock = _slicing.TimeProvider;
        var at = 0;
        while (true)
        {
            var sliceStart = clock.GetTimestamp();
            while (at < count)
            {
                var end = (int)Math.Min(count, (long)at + _slicing.RecordsPerCheck);
                if (!step(at, end))
                    return;
                at = end;
                if (clock.GetElapsedTime(sliceStart) >= _slicing.Budget)
                    break;
            }
            if (at >= count)
                break;
            await _slicing.YieldAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>A field's Items as the records carry them: each Item once, keyed as ADR-0059 tells
    /// Items apart, and kept with the first value that carried it — a text Item's first spelling,
    /// which labels it.</summary>
    private sealed class ItemRegistry(HashSet<ItemKey>? hidden)
    {
        private readonly Dictionary<ItemKey, int> _index = [];
        private bool[] _hidden = new bool[16];

        public List<ItemKey> Keys { get; } = [];

        public int Register(object? value, out bool isHidden)
        {
            var key = ItemKey.Of(value);
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_index, key, out var exists);
            if (!exists)
            {
                slot = Keys.Count;
                Keys.Add(key);
                if (slot == _hidden.Length)
                    Array.Resize(ref _hidden, slot * 2);
                _hidden[slot] = hidden?.Contains(key) == true;
            }
            isHidden = _hidden[slot];
            return slot;
        }
    }

    /// <summary>One question's pass over the records: its leaves and their parts.</summary>
    private sealed class Aggregation
    {
        private readonly RecordPivotSource<TRecord> _source;
        private readonly PivotQuery _query;
        private readonly int _levels;
        private readonly Func<TRecord, object?>[] _axis;
        private readonly ItemRegistry[] _registries;
        private readonly Func<TRecord, object?>[] _filters;
        private readonly HashSet<ItemKey>[] _filterHidden;
        private readonly Func<TRecord, object?>[] _valueAccessors;
        private readonly PartColumns[] _values;
        private readonly int[] _path;

        // The leaves: a trie over the levels' Item indexes, keyed by (parent node, Item index), whose
        // last level's entries are leaf indexes. Its keys are mixed before they are hashed (CellKey).
        private readonly Dictionary<long, int> _trie = new(CellKey.Comparer);
        private int _nextNode = 1;
        private int _leafCount;
        private readonly int[][] _itemOfLeaf;
        private long[] _records = new long[16];
        private PivotSourceRefusal? _refusal;

        public Aggregation(RecordPivotSource<TRecord> source, PivotQuery query)
        {
            _source = source;
            _query = query;
            var axis = query.Rows.Concat(query.Columns).ToArray();
            _levels = axis.Length;
            _axis = axis.Select(f => source._declared[f.Field].Value).ToArray();
            _registries = axis.Select(f => new ItemRegistry(HiddenSet(f))).ToArray();
            var hidingFilters = query.Filters.Where(f => f.HiddenItems.Count > 0).ToArray();
            _filters = hidingFilters.Select(f => source._declared[f.Field].Value).ToArray();
            _filterHidden = hidingFilters.Select(f => HiddenSet(f)!).ToArray();
            _valueAccessors = query.Values.Select(v => source._declared[v.Field].Value).ToArray();
            _values = query.Values.Select(v => new PartColumns(v.Parts, 16)).ToArray();
            _path = new int[_levels];
            _itemOfLeaf = new int[_levels][];
            for (var level = 0; level < _levels; level++)
                _itemOfLeaf[level] = new int[16];
        }

        private static HashSet<ItemKey>? HiddenSet(PivotQueryField field)
            => field.HiddenItems.Count == 0 ? null : field.HiddenItems.Select(ItemKey.FromPublic).ToHashSet();

        /// <summary>Reads records [<paramref name="from"/>, <paramref name="to"/>); false once the
        /// question is refused for its leaves, which stops it at once.</summary>
        public bool Step(int from, int to)
        {
            var records = _source._records;
            for (var r = from; r < to; r++)
            {
                var record = records[r];
                // Every row and column field's Item is registered, whether or not the record is
                // left out: Items are taken over all the data, and so is a text Item's first
                // spelling (ADR-0059).
                var excluded = false;
                for (var level = 0; level < _levels; level++)
                {
                    _path[level] = _registries[level].Register(_axis[level](record), out var hidden);
                    excluded |= hidden;
                }
                for (var f = 0; !excluded && f < _filters.Length; f++)
                    excluded = _filterHidden[f].Contains(ItemKey.Of(_filters[f](record)));
                if (excluded)
                    continue;

                var leaf = LeafOf();
                if (leaf < 0)
                {
                    _refusal = PivotSourceRefusal.TooManyLeaves(_query.MaxLeaves);
                    return false;
                }
                _records[leaf]++;
                for (var v = 0; v < _values.Length; v++)
                    _values[v].Add(leaf, _valueAccessors[v](record));
            }
            return true;
        }

        // The leaf of the record whose Items are in _path, made when it is new; −1 when a new leaf
        // would pass the question's cap (ADR-0065).
        private int LeafOf()
        {
            if (_levels == 0)
                return _leafCount == 0 ? NewLeaf() : 0;
            var node = 0;
            for (var level = 0; level < _levels; level++)
            {
                ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_trie, CellKey.Of(node, _path[level]), out var exists);
                if (!exists)
                {
                    if (level < _levels - 1)
                    {
                        slot = _nextNode++;
                    }
                    else
                    {
                        slot = NewLeaf();
                        if (slot < 0)
                            return -1;
                    }
                }
                node = slot;
            }
            return node;
        }

        private int NewLeaf()
        {
            if (_leafCount == _query.MaxLeaves)
                return -1;
            var leaf = _leafCount++;
            if (leaf == _records.Length)
            {
                Array.Resize(ref _records, leaf * 2);
                for (var level = 0; level < _levels; level++)
                    Array.Resize(ref _itemOfLeaf[level], leaf * 2);
            }
            for (var level = 0; level < _levels; level++)
                _itemOfLeaf[level][leaf] = _path[level];
            foreach (var values in _values)
                values.EnsureCapacity(_leafCount);
            return leaf;
        }

        public PivotAnswer Answer()
        {
            if (_refusal is not null)
                return PivotAnswer.Refused(_refusal);
            foreach (var parts in _values)
                parts.Finish(_leafCount);

            // Each field's Items are those some leaf carries, in the order the data first carried
            // them; a leaf's Item is renumbered into that list.
            var axes = new PivotAnswerAxis[_levels];
            for (var level = 0; level < _levels; level++)
            {
                var keys = _registries[level].Keys;
                var renumbered = new int[keys.Count];
                Array.Fill(renumbered, -1);
                var itemOfLeaf = _itemOfLeaf[level];
                for (var leaf = 0; leaf < _leafCount; leaf++)
                    renumbered[itemOfLeaf[leaf]] = 0;
                var items = new List<PivotItemKey>();
                for (var i = 0; i < keys.Count; i++)
                {
                    if (renumbered[i] == 0)
                    {
                        renumbered[i] = items.Count;
                        items.Add(keys[i].ToPublic());
                    }
                }
                var leaves = new int[_leafCount];
                for (var leaf = 0; leaf < _leafCount; leaf++)
                    leaves[leaf] = renumbered[itemOfLeaf[leaf]];
                var field = level < _query.Rows.Count ? _query.Rows[level].Field : _query.Columns[level - _query.Rows.Count].Field;
                axes[level] = new PivotAnswerAxis(field, [.. items], leaves, _leafCount);
            }
            var values = _values.Select((parts, v) => new PivotAnswerValues(_query.Values[v].Field, parts, _leafCount)).ToArray();
            return new PivotAnswer(
                _source.SourceVersion, axes[.._query.Rows.Count], axes[_query.Rows.Count..], _leafCount, _records, values);
        }
    }

    /// <summary>One field's Items over all the records.</summary>
    private sealed class ItemListing(Func<TRecord, object?> accessor, IReadOnlyList<TRecord> records)
    {
        private readonly ItemRegistry _registry = new(null);

        public List<ItemKey> Keys => _registry.Keys;

        public bool Step(int from, int to)
        {
            for (var r = from; r < to; r++)
                _registry.Register(accessor(records[r]), out _);
            return true;
        }

        public PivotItemPage Page(PivotItemsQuery query, string sourceVersion)
        {
            IEnumerable<ItemKey> matches = Keys;
            if (query.Search is { } search)
                matches = matches.Where(key => key.ToPublic().Value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
            var sorted = matches.ToList();
            sorted.Sort(ItemKey.CompareInvariant);
            return new PivotItemPage(sourceVersion, sorted.Take(query.Max).Select(key => key.ToPublic()).ToArray(), sorted.Count);
        }
    }

    /// <summary>One question's search for the records behind a cell: those carrying every Item of
    /// the cell's paths and no Hidden Item of any placed field, in the data's order.</summary>
    private sealed class DetailSearch
    {
        private readonly RecordPivotSource<TRecord> _source;
        private readonly PivotDetailsQuery _query;
        private readonly (Func<TRecord, object?> Value, ItemKey Item)[] _conditions;
        private readonly (Func<TRecord, object?> Value, HashSet<ItemKey> Hidden)[] _hidden;
        private long _total;

        public DetailSearch(RecordPivotSource<TRecord> source, PivotDetailsQuery query)
        {
            _source = source;
            _query = query;
            _conditions = query.RowItems.Concat(query.ColumnItems)
                .Select(step => (source._declared[step.Field].Value, ItemKey.FromPublic(step.Item)))
                .ToArray();
            _hidden = query.HiddenItems.Where(h => h.HiddenItems.Count > 0)
                .Select(h => (source._declared[h.Field].Value, h.HiddenItems.Select(ItemKey.FromPublic).ToHashSet()))
                .ToArray();
        }

        public List<TRecord> Matches { get; } = [];

        public bool Step(int from, int to)
        {
            var records = _source._records;
            for (var r = from; r < to; r++)
            {
                var record = records[r];
                if (!Behind(record))
                    continue;
                if (_total >= _query.Start && _total - _query.Start < _query.Count)
                    Matches.Add(record);
                _total++;
            }
            return true;
        }

        private bool Behind(TRecord record)
        {
            foreach (var (value, item) in _conditions)
            {
                if (!ItemKey.Of(value(record)).Equals(item))
                    return false;
            }
            foreach (var (value, hidden) in _hidden)
            {
                if (hidden.Contains(ItemKey.Of(value(record))))
                    return false;
            }
            return true;
        }

        public PivotDetailPage Page()
        {
            var fields = _source._fields;
            var records = new PivotDetailRecord[Matches.Count];
            var values = new object?[fields.Length];
            for (var i = 0; i < records.Length; i++)
            {
                var record = Matches[i];
                for (var f = 0; f < fields.Length; f++)
                    values[f] = fields[f].Value(record);
                records[i] = new PivotDetailRecord(values, record);
            }
            return new PivotDetailPage(_source.SourceVersion, fields, _query.Start, _total, records);
        }
    }
}
