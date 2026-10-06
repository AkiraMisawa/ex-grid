using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>
/// A Cell Editor commit the grid refused because the edited cell paints other text than it did
/// when the editor opened (ADR-0142, LV-11): the value changed upstream while the user typed, and
/// the editor covers the cell, so the user never saw the change. No Edit Intent was raised; the
/// editor stays open with what was typed. A Refusal: it judged the operation, never the typed text
/// (as against an Edit Verdict's Reject, ADR-0034).
///
/// <para>The grid holds no string for it. Chrome words it into its refusal live region (A11Y-16),
/// with <see cref="PaintedText"/>, because the editor covers the cell and the notice is where the
/// user sees the new value. A second commit is judged against that text and lands; Escape leaves
/// without writing.</para>
/// </summary>
/// <param name="Cell">The edited cell's position, in the order the editor was opened under.</param>
/// <param name="Column">The edited column's name.</param>
/// <param name="PaintedText">The text the cell paints now — the value the notice shows, and the
/// one a second commit is judged against.</param>
/// <param name="Reason">Whether the cell changed, or the grid can no longer tell what the user saw
/// of it. Chrome words the two apart: a wrong reason is worse than none.</param>
public readonly record struct GridCommitRefusal(CellPosition Cell, string Column, string PaintedText, CommitRefusalReason Reason);

/// <summary>Why a Cell Editor commit was refused (ADR-0142, LV-11; decided with the user as P2,
/// 2026-10-06).</summary>
public enum CommitRefusalReason
{
    /// <summary>The cell paints other text than it did in the render the gesture that opened the
    /// editor was taken against, or than the last refusal showed: it changed upstream while the
    /// user typed.</summary>
    CellChanged,

    /// <summary>The render the opening gesture was taken against is no longer kept, or showed the
    /// rows in another order, so the grid can no longer tell what the user saw of the cell. The cell
    /// may not have changed; Chrome must not say it did.</summary>
    RenderNoLongerKept,
}
