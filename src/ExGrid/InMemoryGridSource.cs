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

    /// <summary>Everything was pushed up front, so a Range Request needs no answer — a
    /// range beyond the data is legal (requests race with data updates), and ignoring
    /// it is the answer (ADR-0001). A malformed range never gets here: the
    /// <see cref="RowRange"/> constructor refuses it.</summary>
    public void OnRangeNeeded(RowRange range)
    {
    }

    private static GridFilter? Snapshot(GridFilter? filter)
    {
        if (filter is null)
            return null;

        var columns = new Dictionary<string, FilterSpec>(filter.Columns.Count, StringComparer.Ordinal);
        foreach (var (name, spec) in filter.Columns)
        {
            var clauses = new FilterClause[spec.Clauses.Count];
            for (var i = 0; i < clauses.Length; i++)
            {
                var clause = spec.Clauses[i];
                clauses[i] = clause.Values is null ? clause : clause with { Values = clause.Values.ToArray() };
            }
            columns[name] = new FilterSpec(clauses, spec.Combinator);
        }
        // The Opaque Filter stays by reference: only the Consumer understands it, and it
        // is passed straight through (ADR-0023).
        return filter with { Columns = columns };
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
        // A byte-identical repush changes nothing — no recompute, no event. Without
        // this, a binding layer that repushes on every render and re-renders on
        // StateChanged would spin (ADR-0023's no-op principle, applied to the commit).
        var sortsChanged = !sorts.SequenceEqual(Sorts);
        var filterChanged = !FiltersEqual(filter, Filter);
        if (!sortsChanged && !filterChanged && ReferenceEquals(columns, _columns))
            return;

        // Compute first — a refusal from the engine must leave the source usable.
        var next = GridQueryEngine.Apply(_rows, columns, filter, sorts);

        // Snapshot everything, for the same reason the constructor snapshots the rows: a
        // Consumer reusing and mutating its collections must not desync the stored query
        // from the Window it was applied to, nor be replayed by the next unrelated
        // change. The filter's read-only interfaces are typically backed by the
        // Consumer's live Dictionary/List, so it is copied structurally too.
        _columns = columns.ToArray();
        Filter = Snapshot(filter);
        Sorts = sorts.ToArray();
        var sequenceChanged = !next.SequenceEqual(Window);
        if (sequenceChanged)
            RowSequenceVersion++;
        Window = next;
        // Notify only when something observable moved. Columns alone carry no event:
        // the grid pushed them, so it already knows.
        if (sequenceChanged || sortsChanged || filterChanged)
            StateChanged?.Invoke();
    }

    /// <summary>Structural filter equality — the record's own equality compares the
    /// collections by reference, which snapshotting makes useless here.</summary>
    private static bool FiltersEqual(GridFilter? a, GridFilter? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        if (!Equals(a.Opaque, b.Opaque) || a.Columns.Count != b.Columns.Count)
            return false;
        foreach (var (name, spec) in a.Columns)
        {
            if (!b.Columns.TryGetValue(name, out var other))
                return false;
            if (spec.Combinator != other.Combinator || spec.Clauses.Count != other.Clauses.Count)
                return false;
            for (var i = 0; i < spec.Clauses.Count; i++)
            {
                var x = spec.Clauses[i];
                var y = other.Clauses[i];
                if (x.Operator != y.Operator || !Equals(x.Value, y.Value))
                    return false;
                if ((x.Values is null) != (y.Values is null))
                    return false;
                if (x.Values is not null && !x.Values.SequenceEqual(y.Values!))
                    return false;
            }
        }
        return true;
    }
}
