using ExGrid.Chrome;
using ExGrid.Finding;

namespace ExGrid.Docs.Examples.Grid.Blotter;

/// <summary>
/// The blotter's Grid Source: every trade in memory, sorted and filtered by ExGrid's reference
/// rules, and repriced in batches as quotes move. <c>GridSource.From</c> requeries once per
/// replaced row; a tick here replaces a thousand rows at once, so the source takes a whole batch
/// and requeries once — and only when the sort or the filter reads a price.
/// </summary>
public sealed class BlotterSource : IGridSource<BlotterTrade>
{
    private readonly BlotterTrade[] _rows;
    private readonly Dictionary<string, List<int>> _byInstrument;
    private readonly HashSet<string> _liveColumns;
    private IReadOnlyList<ColumnInfo<BlotterTrade>> _columns = [];

    /// <summary>Holds <paramref name="rows"/>; <paramref name="liveColumns"/> name the columns a quote moves.</summary>
    public BlotterSource(BlotterTrade[] rows, IEnumerable<string> liveColumns)
    {
        _rows = rows;
        _liveColumns = [.. liveColumns];
        _byInstrument = Enumerable.Range(0, rows.Length).GroupBy(i => rows[i].Instrument)
            .ToDictionary(g => g.Key, g => g.ToList());
        Window = rows;
    }

    /// <inheritdoc />
    public IReadOnlyList<BlotterTrade> Window { get; private set; }

    /// <inheritdoc />
    public int WindowStart => 0;

    /// <inheritdoc />
    public int? TotalCount => Window.Count;

    /// <inheritdoc />
    public bool IsLoading => false;

    /// <inheritdoc />
    public int RowSequenceVersion { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<SortSpec> Sorts { get; private set; } = [];

    /// <inheritdoc />
    public GridFilter? Filter { get; private set; }

    /// <summary>Every trade, filtered or not, as currently priced.</summary>
    public IReadOnlyList<BlotterTrade> All => _rows;

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <inheritdoc />
    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<BlotterTrade>> columns)
    {
        _columns = [.. columns];
        // The grid pushed the columns, so it already knows; it hears only if the order moved.
        Requery(notify: false);
    }

    /// <inheritdoc />
    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        if (sorts.SequenceEqual(Sorts))
            return;
        Sorts = [.. sorts];
        Requery(notify: true);
    }

    /// <inheritdoc />
    public void OnFilterChanged(GridFilter? filter)
    {
        Filter = filter;
        Requery(notify: true);
    }

    /// <summary>Reprices every trade on the instruments that moved, as one change.</summary>
    public void Reprice(IReadOnlyList<BlotterInstrument> quotes)
    {
        var moved = new HashSet<string>();
        foreach (var quote in quotes)
        {
            if (!_byInstrument.TryGetValue(quote.Symbol, out var indices))
                continue;
            moved.Add(quote.Symbol);
            foreach (var i in indices)
                _rows[i] = _rows[i] with { Bid = quote.Bid, Ask = quote.Ask };
        }
        if (moved.Count == 0)
            return;
        var readsPrices = Sorts.Any(s => _liveColumns.Contains(s.Column))
            || (Filter?.Columns.Keys.Any(_liveColumns.Contains) ?? false);
        if (readsPrices)
        {
            Requery(notify: true);
            return;
        }
        // The order cannot have moved: the same rows, each replaced by its new instance.
        Window = [.. Window.Select(t => moved.Contains(t.Instrument) ? _rows[t.Id - BlotterMarket.FirstId] : t)];
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<BlotterTrade>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        var start = Math.Min(range.Start, Window.Count);
        var count = Math.Min(range.Count, Window.Count - start);
        return Task.FromResult<IReadOnlyList<BlotterTrade>>([.. Window.Skip(start).Take(count)]);
    }

    /// <inheritdoc />
    public Task<DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        var info = _columns.First(c => c.Name == column);
        // Every filter but this column's own, so an unticked value can be ticked again.
        var others = Filter?.Columns.Where(c => c.Key != column).ToDictionary(c => c.Key, c => c.Value);
        var rows = others is { Count: > 0 } ? GridQueryEngine.Apply(_rows, _columns, new GridFilter(others), []) : _rows;
        var values = rows.Select(info.Value).Distinct().Take(1_001).ToList();
        return Task.FromResult(values.Count > 1_000 ? DistinctValues.TooMany : DistinctValues.Of(values));
    }

    /// <inheritdoc />
    public bool CanFind => true;

    /// <inheritdoc />
    public Task<GridFindResult> FindAsync(GridFindRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GridFind.Step(Window, request, name => _columns.FirstOrDefault(c => c.Name == name) is { IsQueryable: true } column
            ? column.TextOf
            : null));

    private void Requery(bool notify)
    {
        if (_columns.Count == 0)
            return;
        var next = GridQueryEngine.Apply(_rows, _columns, Filter, Sorts);
        var moved = next.Count != Window.Count || Enumerable.Range(0, next.Count).Any(i => next[i].Id != Window[i].Id);
        if (moved)
            RowSequenceVersion++;
        Window = next;
        if (notify || moved)
            StateChanged?.Invoke();
    }
}
