namespace ExGrid.Cells;

/// <summary>
/// Why an Action press was refused (ADR-0142, LV-12, LV-20). A press acts on the row it was
/// pressed on, as that row is now: its values changing since it was painted refuses nothing. It is
/// refused only when the grid can no longer find that row — gone, or named by a position under an
/// order that has moved — because acting on whatever row stands there now would act on a stranger
/// (ADR-0011).
/// </summary>
public enum ActionRefusalReason
{
    /// <summary>The row the press was made on is no longer in the Window: with a Row Key, no row
    /// answers its key; without one, the position it was pressed on holds no row. With a Row Key, a
    /// press whose button a render disposed before its click arrived is refused so too where the
    /// grid never had the row to go by: the row's component lives as long as its key is painted
    /// (ADR-0140).</summary>
    RowGone,

    /// <summary>The press named its row by a position, and the order that position was read under
    /// has moved since: without a Row Key, a press whose button a render disposed, and Space on the
    /// Focus, by key or not. The position names another row now.</summary>
    OrderMoved,
}

/// <summary>
/// An Action press the grid refused (ADR-0142, LV-12): the press as it would have been raised
/// through <c>OnAction</c>, and why it was not. Nothing was raised. The grid holds no string for
/// it; Chrome words it into its refusal live region (A11Y-16).
/// </summary>
/// <param name="Action">The press: the row the pressed button held — <see langword="default"/>
/// where the grid never had it, since it holds no row beyond its Window (ADR-0160) — the Action
/// Column and the action.</param>
/// <param name="Reason">Why it was refused.</param>
public readonly record struct GridActionRefusal<TRow>(GridActionEventArgs<TRow> Action, ActionRefusalReason Reason);
