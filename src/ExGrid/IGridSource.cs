namespace ExGrid;

/// <summary>
/// What the grid needs from a Grid Source: the Window to paint, where it sits, and a way
/// to say "I need these rows" (ADR-0001). A Source is the bundled convenience layer that
/// takes over holding the Window and answering Range Requests; it drives the push form
/// internally, so binding one changes nothing about how the grid behaves.
///
/// <para><b>Sorting and filtering are deliberately absent.</b> The grid has no gesture
/// that changes either yet — no header click, no column menu (ADR-0010) — so putting
/// them here would publish methods nothing calls. A Consumer changes them on the concrete
/// source it holds; when the grid grows the gesture, the notification joins this
/// interface with the code that raises it.</para>
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

    /// <summary>Raised after a change the grid should repaint for.</summary>
    event Action? StateChanged;

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
}
