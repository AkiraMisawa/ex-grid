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

    /// <summary>The visible columns changed under the open editor, so the coordinates
    /// stopped naming the column they were opened on (ADR-0011). Distinct from
    /// <see cref="OrderChanged"/> because telling a user their rows were reordered when
    /// a column was hidden is a wrong reason, which is worse than none.</summary>
    ColumnsChanged,

    /// <summary>The row left the Window before the commit landed, so there is no row
    /// instance left to carry the Edit Intent's identity (ADR-0003 / 0011). A
    /// positional guess would land the value on a different row.</summary>
    RowLeftTheWindow,

    /// <summary>The column stopped being Editable while its editor was open — a
    /// Consumer revoking a permission mid-edit. `Editable` means a write may land here
    /// (ADR-0035), and it is asked again at the commit, not only when the editor
    /// opened.</summary>
    ColumnNoLongerEditable,
}
