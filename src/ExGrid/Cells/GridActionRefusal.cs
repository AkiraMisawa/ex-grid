namespace ExGrid.Cells;

/// <summary>
/// Why an Action press was refused (ADR-0142, LV-12). The press is judged on the painted text of
/// its row's painted cells, in the render it was taken against and when it is handled: an action
/// may write, and one resolved on values the user did not see would act on them. Every action is
/// judged, with no declaration; one refused in a race costs the user a second press, and the
/// reason is given.
/// </summary>
public enum ActionRefusalReason
{
    /// <summary>A painted cell of the row shows other text than it did in the render the press was
    /// taken against: the row changed between what the user saw and the press being handled.</summary>
    RowChanged,

    /// <summary>The render the press was taken against is no longer kept, or showed the rows in
    /// another order with no Row Key to pair the press with its row by, or the row has left the
    /// Window, so the grid can no longer tell what the user saw of the row against what it holds.
    /// With a Row Key, a press whose row only moved is judged on that row instead (ADR-0140/0142).
    /// Chrome must not say the row changed: it may not have.</summary>
    RenderNoLongerKept,
}

/// <summary>
/// An Action press the grid refused (ADR-0142, LV-12): the press as it would have been raised
/// through <c>OnAction</c>, and why it was not. Nothing was raised. The grid holds no string for
/// it; Chrome words it into its refusal live region (A11Y-16).
/// </summary>
/// <param name="Action">The press: the row the pressed button was painted on, the Action Column
/// and the action.</param>
/// <param name="Reason">Why it was refused.</param>
public readonly record struct GridActionRefusal<TRow>(GridActionEventArgs<TRow> Action, ActionRefusalReason Reason);
