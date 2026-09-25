using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// Why a paste is refused (ADR-0014). <see cref="SingleCellTarget"/> is Excel's
/// "range → 1 cell", refused because it would spill outside the selection — it is a
/// distinct reason so Chrome can say that reselecting a target of the same shape will
/// work. <see cref="ShapeMismatch"/>: the target is not a multiple of the copied block.
/// <see cref="DisjointTarget"/>: a block cannot be pasted into multiple ranges.
/// <see cref="EmptySelection"/>: nowhere to paste; nothing happens.
/// <see cref="TargetNotEditable"/> is the one that is not about shape (ADR-0035): the
/// target covers a column the Consumer declared non-editable, so the write may not land
/// however it is shaped. Chrome must not offer the "reselect the same shape" advice for
/// it — no reselection of that shape would be accepted.
/// <see cref="TooLarge"/> is not about shape either (ADR-0005): the clipboard is past the
/// grid's byte ceiling, so it was not read at all. Component-level, never produced by the
/// pure rules.
/// </summary>
public enum PasteRefusalReason
{
    /// <summary>Nothing is selected: nowhere to paste, and nothing happens.</summary>
    EmptySelection,

    /// <summary>A block of several cells onto one cell, which would spill outside the
    /// selection. Reselecting a target of the block's shape will work.</summary>
    SingleCellTarget,

    /// <summary>The target is not a whole multiple of the block, in rows or in columns.</summary>
    ShapeMismatch,

    /// <summary>A block of several cells onto a selection of several ranges; only a single
    /// value fills more than one range.</summary>
    DisjointTarget,

    /// <summary>The target covers a column that is not Editable (ADR-0035). No reselection
    /// of the same shape would be accepted, so Chrome must not advise one.</summary>
    TargetNotEditable,

    /// <summary>The clipboard is past the grid's <c>PasteByteCap</c>, so it was not read
    /// at all. Raised by the component, never by the pure rules (ADR-0005).</summary>
    TooLarge,
}

/// <summary>
/// The approved paste: the target ranges and how the source block tiles onto them.
/// Compact by construction — ranges plus modulo arithmetic, never a per-cell list — so a
/// whole-column target of a million rows stays one rectangle, including rows that are
/// off screen or not yet fetched (ADR-0014: that is intended). The Consumer resolves it
/// positionally — "rows N–M of the current order, this column" (ADR-0007 / 0011) — under
/// the Row Sequence Version the selection was made under.
/// </summary>
public sealed class PastePlan
{
    internal PastePlan(IReadOnlyList<SelectionRange> targets, PasteShape source)
    {
        Targets = targets;
        Source = source;
    }

    /// <summary>The ranges to fill. More than one only with a 1×1 source (ADR-0014).</summary>
    public IReadOnlyList<SelectionRange> Targets { get; }

    /// <summary>The block on the clipboard, tiled from each target range's top-left.</summary>
    public PasteShape Source { get; }

    /// <summary>
    /// Which source cell lands on a target cell: the source tiles from each range's
    /// top-left. Multi-range targets exist only with a 1×1 source, so overlapping
    /// ranges agree on the value and first match is safe. A cell outside every target
    /// range is a caller bug — refuse rather than answer something plausible.
    /// </summary>
    public SourceCell SourceCellFor(CellPosition target)
    {
        foreach (var range in Targets)
        {
            if (range.Contains(target))
                return new(
                    (target.Row - range.TopRow) % Source.Rows,
                    (target.Column - range.LeftColumn) % Source.Columns);
        }
        throw new ArgumentOutOfRangeException(nameof(target), target,
            "The cell is not inside any target range of this paste.");
    }
}

/// <summary>
/// Approved with a plan, or refused with a reason — never both, never neither
/// (ADR-0014). Reading the absent half throws rather than answering something
/// plausible.
/// </summary>
public sealed class PasteDecision
{
    private readonly PastePlan? _plan;
    private readonly PasteRefusalReason _reason;

    private PasteDecision(PastePlan? plan, PasteRefusalReason reason)
    {
        _plan = plan;
        _reason = reason;
    }

    internal static PasteDecision Approve(PastePlan plan) => new(plan, default);

    internal static PasteDecision Refuse(PasteRefusalReason reason) => new(null, reason);

    /// <summary>Whether the paste is refused: read <see cref="Reason"/> if so, and
    /// <see cref="Plan"/> if not.</summary>
    public bool IsRefused => _plan is null;

    /// <summary>Why the paste is refused. Throws when it was approved.</summary>
    public PasteRefusalReason Reason => _plan is null
        ? _reason
        : throw new InvalidOperationException("The paste was approved; there is no refusal reason.");

    /// <summary>The approved paste. Throws when it was refused.</summary>
    public PastePlan Plan => _plan
        ?? throw new InvalidOperationException(
            "The paste was refused; read Reason instead (ADR-0014: say which one when refusing).");
}
