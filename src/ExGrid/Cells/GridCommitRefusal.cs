using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>
/// A Cell Editor commit the grid refused because it can no longer find the row the editor was
/// opened on (ADR-0142, LV-20): its Row Key no longer answers a row in the Window, or, with no Row
/// Key, the order moved, so the position the editor stands at names another row. (Without a Row
/// Key, a row that left the Window under the same order takes the typing with it instead, announced
/// as <see cref="EditDiscardReason.RowLeftTheWindow"/>: ADR-0011, ED-21.) A commit is never refused
/// because the cell's value changed under the editor: it lands, and an Overwrite Notice tells it. No Edit Intent was
/// raised; the editor stays open with what was typed, and Escape leaves without writing. A Refusal:
/// it judged the operation, never the typed text (as against an Edit Verdict's Reject, ADR-0034).
///
/// <para>The grid holds no string for it. Chrome words it into its refusal live region
/// (A11Y-16).</para>
/// </summary>
/// <param name="Cell">The position the editor stands at.</param>
/// <param name="Column">The edited column's name.</param>
/// <param name="Reason">Why the row could not be found. Chrome words the two apart: a wrong reason
/// is worse than none.</param>
public readonly record struct GridCommitRefusal(CellPosition Cell, string Column, CommitRefusalReason Reason);

/// <summary>Why a Cell Editor commit was refused (ADR-0142, LV-20).</summary>
public enum CommitRefusalReason
{
    /// <summary>A Row Key is in force, and no row in the Window answers the key of the row the
    /// editor was opened on: the row is gone. The editor holds the key while it is open (ADR-0160),
    /// so a later commit lands if the row comes back.</summary>
    RowGone,

    /// <summary>No Row Key is in force, and the Row Sequence Version moved while the editor was
    /// open: the position the editor stands at names another row now (ADR-0011's note of
    /// 2026-10-07). Escape is the way out.</summary>
    OrderMoved,
}
