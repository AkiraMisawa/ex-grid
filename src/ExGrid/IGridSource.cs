namespace ExGrid;

/// <summary>
/// What the grid needs from a Grid Source: the Window to paint, where it sits, and a way
/// to say "I need these rows" (ADR-0001). A Source is the bundled convenience layer that
/// takes over holding the Window and answering Range Requests; it drives the push form
/// internally, so binding one changes nothing about how the grid behaves.
///
/// <para>Sorting and filtering joined this interface with the header click — the first
/// gesture that changes them from inside the grid (ADR-0012). The source holds the Sort
/// and Filter state, applies a change by requerying, and bumps
/// <see cref="RowSequenceVersion"/> when the visible sequence actually moved; the grid
/// reads <see cref="Sorts"/> back for the header's indicator and <c>aria-sort</c>.</para>
///
/// <para>Implementations raise <see cref="StateChanged"/> only when something the grid
/// paints actually moved: the grid re-runs its whole pipeline on it, and an event for a
/// no-op is a wasted repaint of every row (ADR-0023's no-op principle).</para>
/// </summary>
public interface IGridSource<TRow>
{
    /// <summary>The rows to paint right now — never null, possibly empty.</summary>
    IReadOnlyList<TRow> Window { get; }

    /// <summary>The position of <see cref="Window"/>[0] in the whole result. Every offset
    /// on screen depends on it (ADR-0001).</summary>
    int WindowStart { get; }

    /// <summary>Rows after filtering, or null to claim the Window <em>is</em> the whole
    /// result. Null alongside a non-zero <see cref="WindowStart"/> contradicts itself and
    /// the grid refuses it.</summary>
    int? TotalCount { get; }

    /// <summary>Whether an answer is in flight. Left false while one is, the grid shows a
    /// stale result as if it were current (ADR-0001).</summary>
    bool IsLoading { get; }

    /// <summary>Identifies the <em>order</em> of the rows. The grid drops the selection
    /// when it changes (ADR-0011).</summary>
    int RowSequenceVersion { get; }

    /// <summary>The Sort in force, outermost first; empty for the source's own order.
    /// What the header indicator and <c>aria-sort</c> are painted from (ADR-0023).</summary>
    IReadOnlyList<SortSpec> Sorts { get; }

    /// <summary>The Filter in force, or null for none (ADR-0002).</summary>
    GridFilter? Filter { get; }

    /// <summary>Raised after a change the grid should repaint for.</summary>
    event Action? StateChanged;

    /// <summary>A header click, or a Consumer changing the sort directly. A list equal
    /// to the one in force is a no-op — no requery, no event (ADR-0023).</summary>
    void OnSortChanged(IReadOnlyList<SortSpec> sorts);

    /// <summary>The filter UI applying, or a Consumer changing the filter directly. An
    /// equal filter is likewise a no-op (ADR-0023).</summary>
    void OnFilterChanged(GridFilter? filter);

    /// <summary>The columns the grid is bound to, pushed at bind time and whenever they
    /// change: the markup declaration stays the single source of truth (ADR-0023).</summary>
    void OnColumnsChanged(IReadOnlyList<ColumnInfo<TRow>> columns);

    /// <summary>
    /// "I need these rows." Awaited by the grid after a render, never during one, so a
    /// source answering synchronously cannot re-enter the render it was called from — and
    /// a failure surfaces through the ordinary exception path instead of vanishing into
    /// an unobserved task.
    /// </summary>
    Task OnRangeNeededAsync(RowRange range);

    /// <summary>
    /// Rows for a copy whose selection runs beyond the Window (ADR-0005): gathered,
    /// handed over, and stored nowhere — the Window this source paints from is not
    /// touched. May answer fewer rows than asked when the result ends inside the range;
    /// the copy layer refuses rather than emit a partial block.
    /// </summary>
    Task<IReadOnlyList<TRow>> GetRowsAsync(RowRange range, CancellationToken cancellationToken);

    /// <summary>
    /// The filter panel's value list (ADR-0009): the distinct values of one column
    /// under all applied filters <b>except that column's own</b> — honouring it would
    /// be a dead end from which an unticked value can never come back. Pull,
    /// deliberately: transient UI data that cannot break quietly when it is wrong.
    /// <see cref="Chrome.DistinctValues.TooMany"/> is the honest answer where the list
    /// cannot or should not be enumerated.
    /// </summary>
    Task<Chrome.DistinctValues> GetDistinctValuesAsync(string column, CancellationToken cancellationToken);

    /// <summary>
    /// The Consumer's half of Row Marks, when this source keeps them (ADR-0043), or null.
    /// A Mark Column bound to a source that answers null, with no marks handed to the grid
    /// directly, is refused by name.
    /// </summary>
    Rows.IRowMarks<TRow>? Marks => null;
}
