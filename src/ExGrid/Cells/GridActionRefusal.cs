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
    /// answers its key; without one, the position it was pressed on holds no row.</summary>
    RowGone,

    /// <summary>No Row Key is in force, and the press named its row by a position whose order has
    /// moved since — a press whose button a render disposed, or Space on the Focus. The position
    /// names another row now.</summary>
    OrderMoved,
}

/// <summary>
/// An Action press the grid refused (ADR-0142, LV-12): what it would have been raised with through
/// <c>OnAction</c>, and why it was not. Nothing was raised. The grid holds no string for it; Chrome
/// words it into its refusal live region (A11Y-16).
/// </summary>
/// <param name="Row">The row the press was made on, while the Window still holds it; null where it
/// does not, since the grid holds no row beyond its Window (ADR-0160) — a row that is gone, or one
/// the grid never resolved because its order moved before the press was heard.</param>
/// <param name="ColumnName">The Action Column's name.</param>
/// <param name="ActionName">The <see cref="Columns.GridAction.Name"/> that was pressed.</param>
/// <param name="Reason">Why it was refused.</param>
public readonly record struct GridActionRefusal<TRow>(TRow? Row, string ColumnName, string ActionName, ActionRefusalReason Reason);
