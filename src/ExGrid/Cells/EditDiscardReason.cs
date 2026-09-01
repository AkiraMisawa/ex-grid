namespace ExGrid.Cells;

/// <summary>
/// Why the grid threw away text a user had typed and not yet committed (ADR-0011).
/// Both cases are the grid deciding it cannot place the value — a Refusal in the sense
/// `CONTEXT.md` gives the word, judging the operation and never the text — and neither
/// is the user's own doing, which is why they are announced rather than assumed.
/// </summary>
public enum EditDiscardReason
{
    /// <summary>The row order changed under the open editor, so the coordinates the
    /// editor floats over stopped naming the row they were opened on (ADR-0011).
    /// Committing would put the value on a stranger.</summary>
    OrderChanged,

    /// <summary>The row left the Window before the commit landed, so there is no row
    /// instance left to carry the Edit Intent's identity (ADR-0003 / 0011). A
    /// positional guess would land the value on a different row.</summary>
    RowLeftTheWindow,
}
