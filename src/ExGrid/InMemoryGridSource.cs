namespace ExGrid;

/// <summary>
/// <c>GridSource.From</c>: everything is in hand, so the Window is the whole
/// filtered-and-sorted result and nothing is ever loading (ADR-0001).
///
/// Columns reach the source at bind time through <see cref="OnColumnsChanged"/> — the
/// markup column declaration stays the single source of truth (ADR-0023). Until they
/// arrive the Window is the input order unchanged, and a sort or filter change is
/// refused: it cannot happen through the grid, so it is a Consumer bug.
///
/// A refused change leaves the source exactly as it was: every change is computed
/// first and committed only on success.
/// </summary>
public sealed class InMemoryGridSource<TRow>
{
    private readonly IReadOnlyList<TRow> _rows;
    private IReadOnlyList<ColumnInfo<TRow>>? _columns;

    internal InMemoryGridSource(IReadOnlyList<TRow> rows)
    {
        // Snapshot: the base stays immutable even if the Consumer mutates the list it
        // passed in, so the Window and the version diff baseline cannot drift silently.
        _rows = rows.ToArray();
        Window = _rows;
    }

    public IReadOnlyList<TRow> Window { get; private set; }

    /// <summary>Post-filter count — what feeds the pager (ADR-0015).</summary>
    public int TotalCount => Window.Count;

    public IReadOnlyList<SortSpec> Sorts { get; private set; } = [];

    public GridFilter? Filter { get; private set; }

    public bool IsLoading => false;

    /// <summary>
    /// Identifies the order of the rows, not their values. Bumped only when the visible
    /// sequence actually differs after a change — a no-op change must not clear the
    /// selection (ADR-0011).
    /// </summary>
    public int RowSequenceVersion { get; private set; }

    /// <summary>Raised after any change is applied; the binding layer repushes from here.</summary>
    public event Action? StateChanged;

    public void OnColumnsChanged(IReadOnlyList<ColumnInfo<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        Commit(columns, Filter, Sorts);
    }

    public void OnSortChanged(IReadOnlyList<SortSpec> sorts)
    {
        ArgumentNullException.ThrowIfNull(sorts);
        RequireColumns();
        Commit(_columns!, Filter, sorts);
    }

    public void OnFilterChanged(GridFilter? filter)
    {
        RequireColumns();
        Commit(_columns!, filter, Sorts);
    }

    /// <summary>Everything was pushed up front, so a Range Request needs no answer.</summary>
    public void OnRangeNeeded(RowRange range)
    {
    }

    private void RequireColumns()
    {
        if (_columns is null)
            throw new InvalidOperationException(
                "Columns have not been received yet; call OnColumnsChanged first. " +
                "Bound to an ExGrid, this cannot happen — the grid pushes its columns at bind time.");
    }

    private void Commit(
        IReadOnlyList<ColumnInfo<TRow>> columns,
        GridFilter? filter,
        IReadOnlyList<SortSpec> sorts)
    {
        // Compute first — a refusal from the engine must leave the source usable.
        var next = GridQueryEngine.Apply(_rows, columns, filter, sorts);

        // Snapshot the lists for the same reason the constructor snapshots the rows: a
        // Consumer reusing and mutating its list must not desync Sorts from the Window
        // it was applied to, nor be replayed by the next unrelated change.
        _columns = columns.ToArray();
        Filter = filter;
        Sorts = sorts.ToArray();
        if (!next.SequenceEqual(Window))
            RowSequenceVersion++;
        Window = next;
        StateChanged?.Invoke();
    }
}
