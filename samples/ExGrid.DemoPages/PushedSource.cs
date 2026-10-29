using ExGrid.Chrome;

namespace ExGrid.DemoPages;

/// <summary>
/// A source a page pushes its rows to, as these pages pushed a Window to the C# core's grid (ADR-0350 section 16: rows
/// reach the grid only through a Grid Source). It filters what the page pushed, and sorts it too unless the page takes
/// the sorts itself through <see cref="SortChanged"/>: then a sort the user makes is held and told, and the page
/// decides what follows.
/// </summary>
public sealed class PushedSource<TRow> : IGridSource<TRow>
{
    private IReadOnlyList<ColumnInfo<TRow>> _columns = [];
    private IReadOnlyList<TRow> _pushed;

    /// <summary>Holds <paramref name="rows"/>, in the order given, under <paramref name="sorts"/> if the page holds
    /// one.</summary>
    public PushedSource(IReadOnlyList<TRow> rows, IReadOnlyList<SortSpec>? sorts = null, Func<TRow, object>? rowKey = null)
    {
        _pushed = rows;
        Window = rows;
        TotalCount = rows.Count;
        Sorts = sorts ?? [];
        RowKey = rowKey;
    }

    /// <summary>Told each sort the user made, after the source holds it. A page that sets it takes the sorts on
    /// itself: the source then leaves the rows in the order pushed.</summary>
    public Action<IReadOnlyList<SortSpec>>? SortChanged { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<TRow> Window { get; private set; }

    /// <inheritdoc />
    public int WindowStart { get; private set; }

    /// <inheritdoc />
    public int? TotalCount { get; private set; }

    /// <inheritdoc />
    public bool IsLoading { get; private set; }

    /// <inheritdoc />
    public int RowSequenceVersion { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<SortSpec> Sorts { get; private set; }

    /// <inheritdoc />
    public GridFilter? Filter { get; private set; }

    /// <inheritdoc />
    public Func<TRow, object>? RowKey { get; }

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <summary>Pushes the rows the page now holds. <paramref name="orderMoved"/> says their order moved, so the
    /// grid drops what it held by position (ADR-0011).</summary>
    public void Push(IReadOnlyList<TRow> rows, bool orderMoved = false)
    {
        _pushed = rows;
        Reorder(orderMoved);
    }

    /// <summary>Says whether the page is loading, for the grid's loading bar.</summary>
    public void Loading(bool loading)
    {
        IsLoading = loading;
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        Sorts = [.. sorts];
        Reorder(orderMoved: SortChanged is null);
        SortChanged?.Invoke(Sorts);
    }

    /// <inheritdoc />
    public void OnFilterChanged(GridFilter? filter)
    {
        Filter = filter;
        Reorder(orderMoved: true);
    }

    // The rows the grid is shown: what the page pushed, filtered, and sorted unless the page holds the sorts.
    private void Reorder(bool orderMoved)
    {
        Window = Filter is null && (SortChanged is not null || Sorts.Count == 0)
            ? _pushed
            : GridQueryEngine.Apply(_pushed, _columns, Filter, SortChanged is null ? Sorts : []);
        TotalCount = Window.Count;
        if (orderMoved)
            RowSequenceVersion++;
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TRow>> columns) => _columns = [.. columns];

    /// <inheritdoc />
    public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyList<TRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        if (range.Start < WindowStart || range.Start + range.Count > WindowStart + Window.Count)
            throw new ArgumentOutOfRangeException(nameof(range), range, "The page holds only the rows it pushed.");
        return Task.FromResult<IReadOnlyList<TRow>>([.. Window.Skip(range.Start - WindowStart).Take(range.Count)]);
    }

    /// <inheritdoc />
    public Task<DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        var info = _columns.FirstOrDefault(c => c.Name == column)
            ?? throw new ArgumentException($"The grid asked the values of '{column}', which it never declared.", nameof(column));
        return Task.FromResult(DistinctValues.Of([.. Window.Select(info.Value).Distinct()]));
    }
}
