namespace ExGrid.Rows;

/// <summary>
/// What the gestures on a Mark Column mean (ADR-0043). The core owns the meaning; the
/// marks themselves are the Consumer's, which is why these rules take counts rather than
/// rows — a Consumer resolving a gesture over rows the grid has never seen applies the
/// same rule to what it holds.
/// </summary>
public static class RowMarkRules
{
    /// <summary>
    /// The state a gesture over several rows brings them all to: marked if any of them
    /// was unmarked, unmarked only when every one already was marked. Rows are brought
    /// into line, never flipped one by one — flipping would leave a mixed block scrambled,
    /// and the result could not be predicted from the gesture (ADR-0043).
    /// </summary>
    /// <param name="marked">How many of the rows are marked now.</param>
    /// <param name="rows">How many rows the gesture covers.</param>
    /// <returns>True to mark them all; false to unmark them all.</returns>
    public static bool LineUp(int marked, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(marked);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(marked, rows);
        return marked < rows;
    }

    /// <summary>
    /// The header's state, from the counts the Consumer answers: none, some or all of
    /// the current result's Detail rows. Never from the Window — a fully marked Window
    /// inside a larger result is "some" (ADR-0043). Marks outside the current result do
    /// not count towards it; they are said aloud beside the count instead. An empty
    /// result is "none".
    /// </summary>
    public static RowMarkHeaderState HeaderState(RowMarkCounts counts)
        => counts.MarkedInResult == 0 ? RowMarkHeaderState.None
            : counts.MarkedInResult < counts.RowsInResult ? RowMarkHeaderState.Some
            : RowMarkHeaderState.All;

    /// <summary>What pressing the header does from a given state: mark all, unless all
    /// are already marked — pressing "some" completes the marking, and only "all"
    /// clears it (ADR-0043).</summary>
    /// <returns>True to mark all; false to unmark all.</returns>
    public static bool HeaderPress(RowMarkHeaderState state) => state != RowMarkHeaderState.All;
}
