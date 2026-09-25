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
public sealed class InMemoryGridSource<TRow> : IGridSource<TRow>, IBindsToOneCircuit
{
    CircuitBinding IBindsToOneCircuit.Binding { get; } = new();

    private readonly TRow[] _rows;
    private IReadOnlyList<ColumnInfo<TRow>>? _columns;

    internal InMemoryGridSource(IReadOnlyList<TRow> rows)
    {
        // Snapshot: the base stays immutable even if the Consumer mutates the list it
        // passed in, so the Window and the version diff baseline cannot drift silently.
        _rows = rows.ToArray();
        Window = _rows;
    }

    public IReadOnlyList<TRow> Window { get; private set; }

    /// <summary>Always 0: everything is in hand, so the Window is the whole result and
    /// starts at its beginning (ADR-0001).</summary>
    public int WindowStart => 0;

    /// <summary>Post-filter count — what feeds the pager (ADR-0015). Stated rather than
    /// left null even though the Window is the whole result: a Consumer reading it for a
    /// count display should not have to know that convention.</summary>
    public int? TotalCount => Window.Count;

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

    /// <summary>
    /// The Consumer's half of an Edit Intent (ADR-0007), provided by the library so
    /// nobody hand-writes it wrong: the edited row is replaced by a <b>new
    /// instance</b> — identity, not mutation, is the change signal (ADR-0003) — and
    /// the result requeries under the Filter and Sorts in force. The Row Sequence
    /// Version moves only when the visible sequence actually changed (an edit to the
    /// sorted column can move the row; ADR-0011 then drops the selection, correctly),
    /// so an ordinary value edit keeps the selection and the continuous-entry flow.
    /// </summary>
    public void ReplaceRow(TRow row, TRow replacement)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(replacement);
        if (ReferenceEquals(row, replacement))
        {
            throw new ArgumentException(
                "The replacement is the same instance as the row. An in-place rewrite does not reach the " +
                "screen — hand over a new instance; identity is the change signal (ADR-0003/0007).",
                nameof(replacement));
        }
        // Located by identity, never by value: with record rows, value equality would
        // land the edit on the first value-equal duplicate — the wrong row — and let a
        // stale equal instance pass the refusal below (ADR-0003/0007).
        var index = -1;
        for (var i = 0; i < _rows.Length; i++)
        {
            if (ReferenceEquals(_rows[i], row))
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            throw new ArgumentException(
                "The row is not in this source. An Edit Intent carries the instance the grid painted; a " +
                "different or stale instance cannot be resolved onto the base (ADR-0007).", nameof(row));
        }
        RequireColumns();

        _rows[index] = replacement;
        var next = GridQueryEngine.Apply(_rows, _columns!, Filter, Sorts);
        var sequenceChanged = next.Count != Window.Count;
        if (!sequenceChanged)
        {
            // The sequence compares by identity, with the one edit mapped across: at
            // the edited row's position, the old instance standing where the new one
            // now stands is the same sequence, not a reorder.
            for (var i = 0; i < next.Count; i++)
            {
                var same = ReferenceEquals(next[i], Window[i])
                    || (ReferenceEquals(next[i], replacement) && ReferenceEquals(Window[i], row));
                if (!same)
                {
                    sequenceChanged = true;
                    break;
                }
            }
        }
        if (sequenceChanged)
            RowSequenceVersion++;
        Window = next;
        StateChanged?.Invoke();
    }

    /// <summary>Everything was pushed up front, so a Range Request needs no answer — a
    /// range beyond the data is legal (requests race with data updates), and ignoring
    /// it is the answer (ADR-0001). A malformed range never gets here: the
    /// <see cref="RowRange"/> constructor refuses it.</summary>
    public Task OnRangeNeededAsync(RowRange range) => Task.CompletedTask;

    /// <summary>
    /// Past this many distinct values the answer is TooMany (ADR-0009): the panel
    /// degrades to a search-and-condition form, which is Excel's own shape for a large
    /// domain. Provisional the way ADR-0004's thresholds are.
    /// </summary>
    public const int DistinctValueCap = 1000;

    /// <summary>
    /// The reference semantics of the value list (ADR-0009): distinct values of the
    /// column under all applied filters except its own, in order of first appearance.
    /// Blanks appear as a null entry, so the panel can offer them (ADR-0023).
    /// </summary>
    public Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        RequireColumns();
        var info = _columns!.FirstOrDefault(c => c.Name == column)
            ?? throw new InvalidOperationException($"No column is named '{column}'.");

        var others = GridFilters.Without(Filter, column);
        var rows = others is null ? _rows : GridQueryEngine.Apply(_rows, _columns!, others, []);

        var seen = new HashSet<object?>();
        var values = new List<object?>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = info.Value(row);
            if (seen.Add(value))
            {
                values.Add(value);
                if (values.Count > DistinctValueCap)
                    return Task.FromResult(Chrome.DistinctValues.TooMany);
            }
        }
        return Task.FromResult(Chrome.DistinctValues.Of(values));
    }

    /// <summary>Everything is in hand, so a copy beyond the Window cannot arise — but
    /// the answer is honest anyway: the slice of the current result, clamped to what
    /// exists (ADR-0005).</summary>
    public Task<IReadOnlyList<TRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken)
    {
        var start = Math.Min(range.Start, Window.Count);
        var count = Math.Min(range.Count, Window.Count - start);
        var rows = new TRow[count];
        for (var i = 0; i < count; i++)
            rows[i] = Window[start + i];
        return Task.FromResult<IReadOnlyList<TRow>>(rows);
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
        // Columns compare element-wise, never by list reference: _columns is a snapshot
        // (a different instance by construction), while ColumnInfo's record equality —
        // name, type, and the accessor delegate's identity — is exactly what "the same
        // columns repushed" means for a grid that caches its column objects (ADR-0003).
        var sortsChanged = !sorts.SequenceEqual(Sorts);
        var filterChanged = !GridFilters.Equal(filter, Filter);
        var columnsChanged = _columns is null || !columns.SequenceEqual(_columns);
        if (!sortsChanged && !filterChanged && !columnsChanged)
            return;

        // Compute first — a refusal from the engine must leave the source usable.
        var next = GridQueryEngine.Apply(_rows, columns, filter, sorts);

        // Snapshot everything, for the same reason the constructor snapshots the rows: a
        // Consumer reusing and mutating its collections must not desync the stored query
        // from the Window it was applied to, nor be replayed by the next unrelated
        // change. The filter's read-only interfaces are typically backed by the
        // Consumer's live Dictionary/List, so it is copied structurally too.
        _columns = columns.ToArray();
        Filter = GridFilters.Snapshot(filter);
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
}
