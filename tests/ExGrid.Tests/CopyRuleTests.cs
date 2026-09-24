using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class CopyRuleTests
{
    private static readonly GridExtent Grid = new(1_000_000, 50);

    [Fact] // ADR-0005: a single range under the cap copies — one segment, canonical Vertical
    public void A_single_range_under_the_cap_is_approved()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid).ExtendTo(new(3, 4), Grid);

        var decision = ClipboardRules.PlanCopy(selection);

        Assert.False(decision.IsRefused);
        Assert.Equal([new SelectionRange(1, 1, 3, 4)], decision.Plan.Segments);
        Assert.Equal(CopyOrientation.Vertical, decision.Plan.Orientation);
        Assert.Equal(3, decision.Plan.TotalRows);
        Assert.Equal(4, decision.Plan.TotalColumns);
    }

    [Fact] // ADR-0005: past the cap it does not copy at all — it refuses and points at export
    public void Select_all_over_a_million_rows_is_refused_as_too_large()
    {
        var selection = GridSelection.Empty.SelectAll(Grid); // 50,000,000 cells

        var decision = ClipboardRules.PlanCopy(selection);

        Assert.True(decision.IsRefused);
        Assert.Equal(CopyRefusalReason.TooLarge, decision.Reason);
    }

    [Fact] // ADR-0005: refusal is strictly past the cap — exactly at the cap still copies
    public void Exactly_at_the_cap_is_approved_and_one_cell_more_is_refused()
    {
        var wholeColumn = GridSelection.Empty.Click(new(0, 0), Grid).SelectWholeColumns(Grid); // 1,000,000 cells
        Assert.False(ClipboardRules.PlanCopy(wholeColumn).IsRefused);

        var twoColumns = GridSelection.Empty
            .Click(new(0, 0), Grid).ExtendTo(new(0, 1), Grid).SelectWholeColumns(Grid); // 2,000,000 cells
        Assert.Equal(CopyRefusalReason.TooLarge, ClipboardRules.PlanCopy(twoColumns).Reason);
    }

    [Fact] // ADR-0005: the cap is the Consumer's to raise or lower; below one cell it is a misconfiguration
    public void A_custom_cap_is_respected_and_a_capless_cap_throws()
    {
        var twelveCells = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 3), Grid);
        Assert.Equal(CopyRefusalReason.TooLarge, ClipboardRules.PlanCopy(twelveCells, cellCap: 10).Reason);

        var tenCells = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(1, 4), Grid);
        Assert.False(ClipboardRules.PlanCopy(tenCells, cellCap: 10).IsRefused);

        Assert.Throws<ArgumentOutOfRangeException>(() => ClipboardRules.PlanCopy(tenCells, cellCap: 0));
    }

    [Fact] // ADR-0011: ranges sharing a column span with disjoint row spans stack vertically
    public void Disjoint_ranges_sharing_a_column_span_stack_vertically()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 2), Grid).ExtendTo(new(2, 3), Grid)
            .ToggleRange(new(6, 2), Grid).ExtendTo(new(8, 3), Grid); // a gap between the spans is fine

        var plan = ClipboardRules.PlanCopy(selection).Plan;

        Assert.Equal([new SelectionRange(1, 2, 2, 2), new SelectionRange(6, 2, 3, 2)], plan.Segments);
        Assert.Equal(CopyOrientation.Vertical, plan.Orientation);
        Assert.Equal(5, plan.TotalRows);      // segment rows sum; the gap closes up
        Assert.Equal(2, plan.TotalColumns);   // the shared span
        Assert.Equal(selection.CellCount, (long)plan.TotalRows * plan.TotalColumns);
    }

    [Fact] // ADR-0011: the transpose — a shared row span with disjoint column spans concatenates horizontally
    public void Disjoint_ranges_sharing_a_row_span_concatenate_horizontally()
    {
        var selection = GridSelection.Empty
            .Click(new(2, 1), Grid).ExtendTo(new(3, 2), Grid)
            .ToggleRange(new(2, 5), Grid).ExtendTo(new(3, 6), Grid);

        var plan = ClipboardRules.PlanCopy(selection).Plan;

        Assert.Equal([new SelectionRange(2, 1, 2, 2), new SelectionRange(2, 5, 2, 2)], plan.Segments);
        Assert.Equal(CopyOrientation.Horizontal, plan.Orientation);
        Assert.Equal(2, plan.TotalRows);
        Assert.Equal(4, plan.TotalColumns);
    }

    [Fact] // ADR-0011: ranges lining up on neither axis are refused — and the reason says so
    public void Ranges_aligned_on_neither_axis_are_refused_as_misaligned()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid).ExtendTo(new(7, 7), Grid);

        var decision = ClipboardRules.PlanCopy(selection);

        Assert.True(decision.IsRefused);
        Assert.Equal(CopyRefusalReason.MisalignedShape, decision.Reason);
    }

    [Fact] // ADR-0011: overlapping row spans would emit the same cells twice — refused as misaligned
    public void Overlapping_row_spans_are_refused_as_misaligned()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid).ExtendTo(new(4, 1), Grid)   // rows 0-4
            .ToggleRange(new(6, 0), Grid).ExtendTo(new(3, 1), Grid); // rows 3-6, same column span

        Assert.Equal(CopyRefusalReason.MisalignedShape, ClipboardRules.PlanCopy(selection).Reason);
    }

    [Fact] // ADR-0011: segments are emitted in position order, not the order the user created them
    public void Segments_come_back_in_position_order_not_creation_order()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid).ExtendTo(new(6, 3), Grid)   // bottom range created first
            .ToggleRange(new(1, 2), Grid).ExtendTo(new(2, 3), Grid);

        var plan = ClipboardRules.PlanCopy(selection).Plan;

        Assert.Equal([new SelectionRange(1, 2, 2, 2), new SelectionRange(5, 2, 2, 2)], plan.Segments);
    }

    [Fact] // ADR-0005: nothing selected, nothing copied — with its own reason
    public void An_empty_selection_is_refused_as_empty()
    {
        Assert.Equal(CopyRefusalReason.EmptySelection, ClipboardRules.PlanCopy(GridSelection.Empty).Reason);
    }

    [Fact] // ADR-0005: misaligned wins over too large — the cap has no well-formed block to judge
    public void A_misaligned_selection_over_the_cap_reports_the_misalignment()
    {
        var selection = GridSelection.Empty
            .SelectAll(Grid)                 // 50M cells, far past the cap
            .ToggleRange(new(0, 0), Grid);   // toggle-off splits it into misaligned fragments

        Assert.Equal(CopyRefusalReason.MisalignedShape, ClipboardRules.PlanCopy(selection).Reason);
    }

    [Fact] // ADR-0005: the plan's segments cannot be cast back to a mutable array
    public void Segments_is_not_a_castable_mutable_array()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 2), Grid).ExtendTo(new(2, 3), Grid)
            .ToggleRange(new(6, 2), Grid).ExtendTo(new(8, 3), Grid);

        Assert.IsNotType<SelectionRange[]>(ClipboardRules.PlanCopy(selection).Plan.Segments);
    }

    [Fact] // ADR-0005: a decision is approved or refused, never both — the absent half refuses to answer
    public void Reading_the_absent_half_of_a_decision_throws()
    {
        var refused = ClipboardRules.PlanCopy(GridSelection.Empty);
        Assert.Throws<InvalidOperationException>(() => refused.Plan);

        var approved = ClipboardRules.PlanCopy(GridSelection.Empty.Click(new(0, 0), Grid));
        Assert.Throws<InvalidOperationException>(() => approved.Reason);
    }

    [Fact] // ADR-0005 / CP-18: the header row counts, so the cap keeps meaning what it says
    public void A_selection_exactly_on_the_cap_copies_plainly_and_refuses_with_headers()
    {
        // 4 rows x 2 columns = 8 cells; with a header row it is 10.
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(3, 1), Grid);

        Assert.False(ClipboardRules.PlanCopy(selection, cellCap: 8).IsRefused);
        var refused = ClipboardRules.PlanCopy(selection, cellCap: 8, withHeaders: true);

        Assert.True(refused.IsRefused);
        Assert.Equal(CopyRefusalReason.TooLarge, refused.Reason);
    }

    [Fact] // ADR-0005 / CP-18: room for the extra row and it approves, as the plain copy does
    public void With_headers_it_approves_when_the_cap_has_room_for_the_extra_row()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(3, 1), Grid);

        Assert.False(ClipboardRules.PlanCopy(selection, cellCap: 10, withHeaders: true).IsRefused);
    }
}
