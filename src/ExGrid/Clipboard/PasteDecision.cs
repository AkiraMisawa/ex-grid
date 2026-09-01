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
/// </summary>
public enum PasteRefusalReason
{
    EmptySelection,
    SingleCellTarget,
    ShapeMismatch,
    DisjointTarget,
    TargetNotEditable,
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

    public bool IsRefused => _plan is null;

    public PasteRefusalReason Reason => _plan is null
        ? _reason
        : throw new InvalidOperationException("The paste was approved; there is no refusal reason.");

    public PastePlan Plan => _plan
        ?? throw new InvalidOperationException(
            "The paste was refused; read Reason instead (ADR-0014: say which one when refusing).");
}
