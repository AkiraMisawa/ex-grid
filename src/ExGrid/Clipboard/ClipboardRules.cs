using ExGrid.Selection;

namespace ExGrid.Clipboard;

/// <summary>
/// The copy and paste decision rules — pure and stateless, the reference for what the
/// clipboard layer may do. Copy refuses rather than truncates (ADR-0005); paste never
/// spills outside the selection (ADR-0014); a refusal always says which rule it hit.
///
/// No <see cref="GridExtent"/> appears here on purpose: a bounds check could only catch
/// a shrunken grid, never the dangerous case — a same-sized reorder. The guard against
/// stale positions is the holder dropping the selection when the Row Sequence Version
/// changes (ADR-0011) — a rule the holder must enforce itself;
/// <see cref="GridSelection"/> can only refuse a selection that no longer fits its
/// extent, and a same-sized reorder is invisible to it.
/// </summary>
public static class ClipboardRules
{
    /// <summary>
    /// Default copy cap, in cells — the same unit as the status display's count
    /// (ADR-0014), because rows vary in width. A safety valve, not a daily path
    /// (ADR-0005): a block this size runs to tens of megabytes of TSV. The Consumer can
    /// set it higher. Exactly at the cap still copies — refusal is strictly past it.
    /// </summary>
    public const long DefaultCellCap = 1_000_000;

    /// <summary>
    /// The copy rules (ADR-0005/0011): an empty selection is refused, then a misaligned
    /// one, then one past the cap. Otherwise the plan names the rectangles to emit and
    /// how they combine into one block.
    /// </summary>
    /// <param name="selection">What is selected, in positions of the current order.</param>
    /// <param name="cellCap">The most cells a copy may carry; at least one. Exactly at the
    /// cap still copies.</param>
    /// <param name="withHeaders">Whether a header row will be emitted above the block
    /// (ADR-0005). It counts against the cap, because the cap is applied to what is
    /// actually copied and not to the selection: a selection sitting exactly on the cap
    /// copies plainly and refuses with headers, and the reason is that the payload is
    /// bigger.</param>
    public static CopyDecision PlanCopy(
        GridSelection selection, long cellCap = DefaultCellCap, bool withHeaders = false)
    {
        ArgumentNullException.ThrowIfNull(selection);
        // A cap below one cell is a misconfiguration, not a refusal.
        ArgumentOutOfRangeException.ThrowIfLessThan(cellCap, 1);

        if (selection.IsEmpty)
            return CopyDecision.Refuse(CopyRefusalReason.EmptySelection);

        // Alignment before the cap: overlapping ranges double-count the cell figure, so
        // until the shapes line up there is no well-formed block for the cap to judge —
        // and "use export instead" is equally ill-defined for a misaligned selection.
        var plan = TryAlign(selection.Ranges);
        if (plan is null)
            return CopyDecision.Refuse(CopyRefusalReason.MisalignedShape);
        var rows = (long)plan.TotalRows + (withHeaders ? 1 : 0);
        if (rows * plan.TotalColumns > cellCap)
            return CopyDecision.Refuse(CopyRefusalReason.TooLarge);
        return CopyDecision.Approve(plan);
    }

    /// <summary>
    /// The paste rules (ADR-0014), plus the Editable declaration (ADR-0035).
    /// <paramref name="columnIsEditable"/> is asked about each column the target covers,
    /// by position in the current order. It has no overload without it on purpose: an
    /// entry point that skips the declaration is one that walks past it.
    /// </summary>
    public static PasteDecision PlanPaste(
        GridSelection target, PasteShape source, Func<int, bool> columnIsEditable)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(columnIsEditable);
        // default(PasteShape) sidesteps the constructor's validation; refuse it here by
        // name rather than let the tiling arithmetic divide by zero.
        if (source.Rows < 1 || source.Columns < 1)
            throw new ArgumentOutOfRangeException(nameof(source), source,
                "A paste shape covers at least one cell; default(PasteShape) is not a valid shape.");

        if (target.IsEmpty)
            return PasteDecision.Refuse(PasteRefusalReason.EmptySelection);

        // Before every shape rule (ADR-0035). A shape refusal carries the advice
        // "reselect a target of the same shape and it will work"; on a target that
        // covers a non-editable column that advice is a lie, so the declaration has to
        // be reported first. Refused whole, never column by column: a partial write
        // would disagree with the displayed cell count, and PastePlan holds ranges and
        // modulo arithmetic, never a per-cell list (ADR-0014).
        if (!EveryColumnIsEditable(target.Ranges, columnIsEditable))
            return PasteDecision.Refuse(PasteRefusalReason.TargetNotEditable);

        // A single value fills every selected cell — the bulk-entry shape, and the only
        // paste a disjoint target accepts (ADR-0011 / 0014).
        if (source.IsSingleCell)
            return PasteDecision.Approve(new PastePlan(target.Ranges, source));

        // Checked before the single-cell case: a multi-range target containing a 1×1
        // range gets the multi-selection refusal, as in Excel.
        if (target.Ranges.Count > 1)
            return PasteDecision.Refuse(PasteRefusalReason.DisjointTarget);

        var only = target.Ranges[0];
        if (only.CellCount == 1)
            return PasteDecision.Refuse(PasteRefusalReason.SingleCellTarget);

        return only.RowCount % source.Rows == 0 && only.ColumnCount % source.Columns == 0
            ? PasteDecision.Approve(new PastePlan(target.Ranges, source))
            : PasteDecision.Refuse(PasteRefusalReason.ShapeMismatch);
    }

    /// <summary>Every column the target covers, across every range. Columns, not cells:
    /// the row half of editability is the Window's, and a paste target legitimately
    /// covers rows that are off screen or not yet fetched (ADR-0014 / 0035).</summary>
    private static bool EveryColumnIsEditable(
        IReadOnlyList<SelectionRange> ranges, Func<int, bool> columnIsEditable)
    {
        foreach (var range in ranges)
        {
            for (var column = range.LeftColumn; column <= range.RightColumn; column++)
            {
                if (!columnIsEditable(column))
                    return false;
            }
        }
        return true;
    }

    private static CopyPlan? TryAlign(IReadOnlyList<SelectionRange> ranges)
    {
        if (ranges.Count == 1)
        {
            var only = ranges[0];
            return new CopyPlan([only], CopyOrientation.Vertical, only.RowCount, only.ColumnCount);
        }

        // A shared column span with pairwise-disjoint row spans stacks vertically; the
        // transpose concatenates horizontally (ADR-0011). For two or more ranges the
        // branches are mutually exclusive — equal spans cannot also be disjoint.
        var vertical = ranges.OrderBy(range => range.TopRow).ToArray();
        if (SharesColumnSpan(vertical) && HasDisjointRowSpans(vertical))
        {
            var totalRows = 0;
            foreach (var segment in vertical)
                totalRows += segment.RowCount;
            return new CopyPlan(vertical, CopyOrientation.Vertical, totalRows, vertical[0].ColumnCount);
        }

        var horizontal = ranges.OrderBy(range => range.LeftColumn).ToArray();
        if (SharesRowSpan(horizontal) && HasDisjointColumnSpans(horizontal))
        {
            var totalColumns = 0;
            foreach (var segment in horizontal)
                totalColumns += segment.ColumnCount;
            return new CopyPlan(horizontal, CopyOrientation.Horizontal, horizontal[0].RowCount, totalColumns);
        }

        return null;
    }

    private static bool SharesColumnSpan(SelectionRange[] segments)
    {
        var span = segments[0].ColumnSpan;
        foreach (var segment in segments)
        {
            if (segment.ColumnSpan != span)
                return false;
        }
        return true;
    }

    private static bool SharesRowSpan(SelectionRange[] segments)
    {
        var span = segments[0].RowSpan;
        foreach (var segment in segments)
        {
            if (segment.RowSpan != span)
                return false;
        }
        return true;
    }

    /// <summary>Sorted by top row; an overlap (or an exact duplicate) fails — stacking
    /// would emit the same cells twice, a quietly wrong total (ADR-0005).</summary>
    private static bool HasDisjointRowSpans(SelectionRange[] sortedByTopRow)
    {
        for (var i = 1; i < sortedByTopRow.Length; i++)
        {
            if (sortedByTopRow[i - 1].BottomRow >= sortedByTopRow[i].TopRow)
                return false;
        }
        return true;
    }

    private static bool HasDisjointColumnSpans(SelectionRange[] sortedByLeftColumn)
    {
        for (var i = 1; i < sortedByLeftColumn.Length; i++)
        {
            if (sortedByLeftColumn[i - 1].RightColumn >= sortedByLeftColumn[i].LeftColumn)
                return false;
        }
        return true;
    }
}
