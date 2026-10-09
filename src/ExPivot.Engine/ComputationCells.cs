using System.Collections.Immutable;

namespace ExPivot.Engine;

// Immutable pages over the initial columnar cells. A new version copies only written pages;
// it points to no previous version, so collecting one version cannot keep a history chain.
// The cell index is the same: the initial cube's own, which nothing writes once that cube is
// made, under an immutable overlay of the keys the versions since added or removed. A
// computation's first version copies nothing of either.
internal sealed class ComputationCells(
    Dictionary<long, int> initial, ImmutableDictionary<long, int> changed, PartColumns[] baseline,
    ImmutableDictionary<int, PartColumns[]> pages, int count)
{
    internal const int PageSize = 256;

    // An initial key a later version removed.
    private const int Gone = -1;

    /// <summary>A computation's first version: the initial cube's cells, shared.</summary>
    public static ComputationCells Of(Dictionary<long, int> cells, PartColumns[] values)
        => new(cells, ImmutableDictionary.Create<long, int>(CellKey.Comparer), values,
            ImmutableDictionary<int, PartColumns[]>.Empty, cells.Count);

    public PartColumns[] Baseline { get; } = baseline;
    public ImmutableDictionary<int, PartColumns[]> Pages { get; } = pages;
    public int Count { get; } = count;

    /// <summary>The cell a key names in this version, if any.</summary>
    public bool TryGetCell(long key, out int cell)
        => Changed.TryGetValue(key, out cell) ? cell != Gone : Initial.TryGetValue(key, out cell);

    /// <summary>The cell a key names; it must name one.</summary>
    public int CellOf(long key)
        => TryGetCell(key, out var cell) ? cell : throw new KeyNotFoundException($"No cell has the key {key}.");

    public (PartColumns[] Columns, int Offset) At(int cell)
        => Pages.TryGetValue(cell / PageSize, out var page) ? (page, cell % PageSize) : (Baseline, cell);

    public AggregateValue Read(int row, int column, int source, PivotAggregation aggregation)
    {
        if (!TryGetCell(CellKey.Of(row, column), out var cell))
            return AggregateValue.Empty;
        var (columns, offset) = At(cell);
        return columns[source].Read(offset, aggregation);
    }

    public sealed class Writer(ComputationCells previous)
    {
        private readonly ImmutableDictionary<long, int>.Builder _changed = previous.Changed.ToBuilder();
        private readonly ImmutableDictionary<int, PartColumns[]>.Builder _pages = previous.Pages.ToBuilder();
        private readonly HashSet<int> _written = [];
        private int _count = previous.Count;

        private bool TryGetCell(long key, out int cell)
            => _changed.TryGetValue(key, out cell) ? cell != Gone : previous.Initial.TryGetValue(key, out cell);

        public int Cell(long key)
        {
            if (!TryGetCell(key, out var index))
                _changed[key] = index = _count++;
            return index;
        }

        public void Remove(long key)
        {
            if (previous.Initial.ContainsKey(key))
                _changed[key] = Gone;
            else
                _changed.Remove(key);
        }

        private PartColumns[] Write(int cell)
        {
            var number = cell / PageSize;
            if (_written.Add(number))
            {
                var page = new PartColumns[previous.Baseline.Length];
                for (var v = 0; v < page.Length; v++)
                {
                    page[v] = new PartColumns(previous.Baseline[v].Parts, PageSize);
                    if (previous.Pages.TryGetValue(number, out var held))
                        page[v].CopyRange(held[v], 0, PageSize);
                    else
                    {
                        var from = number * PageSize;
                        for (var at = from; at < Math.Min(previous.Count, from + PageSize); at++)
                            page[v].CopyCell(at - from, previous.Baseline[v], at);
                    }
                }
                _pages[number] = page;
            }
            return _pages[number];
        }

        public (PartColumns[] Columns, int Offset) At(int cell)
            => _pages.TryGetValue(cell / PageSize, out var page) ? (page, cell % PageSize) : (previous.Baseline, cell);

        public void Put(int cell, PartColumns[] values, int at)
        {
            var page = Write(cell);
            for (var v = 0; v < page.Length; v++)
                page[v].CopyCell(cell % PageSize, values[v], at);
        }

        public void Adjust(int cell, PartColumns[]? before, int from, PartColumns[]? after, int to)
        {
            var page = Write(cell);
            var offset = cell % PageSize;
            for (var v = 0; v < page.Length; v++)
            {
                var oldCount = before is null ? default : before[v].Counts[from];
                var newCount = after is null ? default : after[v].Counts[to];
                page[v].Counts[offset].Values += newCount.Values - oldCount.Values;
                page[v].Counts[offset].Numbers += newCount.Numbers - oldCount.Numbers;
                if (page[v].Sums is { } sums)
                    sums[offset].Exact += (after is null ? 0m : after[v].Sums![to].Exact)
                        - (before is null ? 0m : before[v].Sums![from].Exact);
            }
        }

        public void Reset(int cell)
        {
            foreach (var values in Write(cell))
                values.ResetCell(cell % PageSize);
        }

        public void Merge(int cell, int leaf)
        {
            var page = Write(cell);
            var (values, at) = At(leaf);
            for (var v = 0; v < page.Length; v++)
                page[v].Merge(cell % PageSize, values[v], at);
        }

        public void Finish(int cell)
        {
            foreach (var values in Write(cell))
                values.Canonicalize(cell % PageSize, cell % PageSize + 1);
        }

        public ComputationCells Freeze() => new(previous.Initial, _changed.ToImmutable(), previous.Baseline, _pages.ToImmutable(), _count);
    }

    private Dictionary<long, int> Initial { get; } = initial;
    private ImmutableDictionary<long, int> Changed { get; } = changed;
}
