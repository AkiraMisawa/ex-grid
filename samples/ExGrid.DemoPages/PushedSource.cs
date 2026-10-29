using ExGrid.Chrome;

namespace ExGrid.DemoPages;

/// <summary>
/// A source a page pushes its rows to, in the order and with the sort it holds, as these pages pushed a Window to the
/// C# core's grid (ADR-0350 section 16: rows reach the grid only through a Grid Source). The page sorts and filters
/// nothing through it: a sort the user makes is held and told, and the page decides what follows.
/// </summary>
public sealed class PushedSource<TRow> : IGridSource<TRow>
{
    private IReadOnlyList<ColumnInfo<TRow>> _columns = [];

    /// <summary>Holds <paramref name="window"/>, the whole result unless <see cref="Push"/> says otherwise.</summary>
    public PushedSource(IReadOnlyList<TRow> window, IReadOnlyList<SortSpec>? sorts = null, Func<TRow, object>? rowKey = null)
    {
        Window = window;
        TotalCount = window.Count;
        Sorts = sorts ?? [];
        RowKey = rowKey;
    }

    /// <summary>Told each sort the user made, after the source holds it.</summary>
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

    /// <summary>Pushes the rows the page now holds. A new <paramref name="rowSequenceVersion"/> says their order moved
    /// (ADR-0011); left out, the version stands.</summary>
    public void Push(IReadOnlyList<TRow> window, int? totalCount = null, int? rowSequenceVersion = null)
    {
        Window = window;
        TotalCount = totalCount ?? window.Count;
        if (rowSequenceVersion is { } version)
            RowSequenceVersion = version;
        StateChanged?.Invoke();
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
        StateChanged?.Invoke();
        SortChanged?.Invoke(Sorts);
    }

    /// <inheritdoc />
    public void OnFilterChanged(GridFilter? filter)
    {
        Filter = filter;
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
