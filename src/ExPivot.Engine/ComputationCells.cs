using System.Collections.Immutable;

namespace ExPivot.Engine;

// Immutable pages over the initial columnar cells. A new version copies only written pages;
// it points to no previous version, so collecting one version cannot keep a history chain.
internal sealed class ComputationCells(
    ImmutableDictionary<long, int> cells, PartColumns[] baseline,
    ImmutableDictionary<int, PartColumns[]> pages, int count)
{
    internal const int PageSize = 256;
    public ImmutableDictionary<long, int> Cells { get; } = cells;
    public PartColumns[] Baseline { get; } = baseline;
    public ImmutableDictionary<int, PartColumns[]> Pages { get; } = pages;
    public int Count { get; } = count;

    public (PartColumns[] Columns, int Offset) At(int cell)
        => Pages.TryGetValue(cell / PageSize, out var page) ? (page, cell % PageSize) : (Baseline, cell);

    public AggregateValue Read(int row, int column, int source, PivotAggregation aggregation)
    {
        if (!Cells.TryGetValue(CellKey.Of(row, column), out var cell))
            return AggregateValue.Empty;
        var (columns, offset) = At(cell);
        return columns[source].Read(offset, aggregation);
    }

    public sealed class Writer(ComputationCells previous)
    {
        private readonly ImmutableDictionary<long, int>.Builder _cells = previous.Cells.ToBuilder();
        private readonly ImmutableDictionary<int, PartColumns[]>.Builder _pages = previous.Pages.ToBuilder();
        private readonly HashSet<int> _written = [];
        private int _count = previous.Count;

        public int Cell(long key)
        {
            if (!_cells.TryGetValue(key, out var index))
                _cells[key] = index = _count++;
            return index;
        }

        public void Remove(long key) => _cells.Remove(key);

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

        public ComputationCells Freeze() => new(_cells.ToImmutable(), previous.Baseline, _pages.ToImmutable(), _count);
    }
}
