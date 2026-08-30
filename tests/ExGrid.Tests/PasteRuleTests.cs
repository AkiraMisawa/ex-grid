using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

public class PasteRuleTests
{
    private static readonly GridExtent Grid = new(1_000_000, 50);

    [Fact] // ADR-0014: 1×1 → range: the whole range fills with that value
    public void A_single_cell_fills_the_whole_target_range()
    {
        var target = GridSelection.Empty.Click(new(2, 2), Grid).ExtendTo(new(4, 5), Grid);

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(1, 1));

        Assert.False(decision.IsRefused);
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(2, 2)));
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(4, 5)));
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(2, 5)));
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(4, 2)));
    }

    [Fact] // ADR-0011: bulk entry into disjoint ranges is the main use of disjoint selection
    public void A_single_cell_fills_every_disjoint_range()
    {
        var target = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid);

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(1, 1));

        Assert.False(decision.IsRefused);
        Assert.Equal(2, decision.Plan.Targets.Count);
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(5, 5)));
    }

    [Fact] // ADR-0014: copy 2 rows, paste into 6 — repeats three times
    public void Two_rows_into_six_tile_three_times()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(5, 2), Grid); // 6×3

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(2, 3));

        Assert.False(decision.IsRefused);
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(0, 0)));
        Assert.Equal(new SourceCell(1, 0), decision.Plan.SourceCellFor(new(1, 0)));
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(2, 0)));
        Assert.Equal(new SourceCell(0, 1), decision.Plan.SourceCellFor(new(4, 1)));
    }

    [Fact] // ADR-0014: an exact shape match is the 1× tiling
    public void An_exact_shape_match_is_approved()
    {
        var target = GridSelection.Empty.Click(new(3, 3), Grid).ExtendTo(new(8, 5), Grid); // 6×3

        Assert.False(ClipboardRules.PlanPaste(target, new PasteShape(6, 3)).IsRefused);
    }

    [Fact] // ADR-0014: tiling works on both axes at once
    public void Tiling_repeats_on_both_axes()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(5, 3), Grid); // 6×4

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(2, 2));

        Assert.False(decision.IsRefused);
        Assert.Equal(new SourceCell(1, 1), decision.Plan.SourceCellFor(new(5, 3)));
        Assert.Equal(new SourceCell(0, 1), decision.Plan.SourceCellFor(new(2, 3)));
    }

    [Fact] // ADR-0014: range → 1 cell is refused — paste never spills outside the selection
    public void A_block_onto_a_single_cell_is_refused_with_its_own_reason()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid);

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(3, 2));

        Assert.True(decision.IsRefused);
        // Distinct from ShapeMismatch: Chrome attaches "reselect a target of the same shape".
        Assert.Equal(PasteRefusalReason.SingleCellTarget, decision.Reason);
    }

    [Fact] // ADR-0014: ragged — copy 3 rows, paste into 5 — refused, on either axis
    public void A_ragged_shape_is_refused()
    {
        var fiveRows = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(4, 0), Grid);
        Assert.Equal(PasteRefusalReason.ShapeMismatch,
            ClipboardRules.PlanPaste(fiveRows, new PasteShape(3, 1)).Reason);

        var fiveColumns = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(0, 4), Grid);
        Assert.Equal(PasteRefusalReason.ShapeMismatch,
            ClipboardRules.PlanPaste(fiveColumns, new PasteShape(1, 3)).Reason);
    }

    [Fact] // ADR-0014: a block into a disjoint target is refused even when every range is a multiple
    public void A_block_into_disjoint_ranges_is_refused_even_when_each_range_would_tile()
    {
        var target = GridSelection.Empty
            .Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid)       // 2×2
            .ToggleRange(new(5, 0), Grid).ExtendTo(new(6, 1), Grid); // 2×2

        Assert.Equal(PasteRefusalReason.DisjointTarget,
            ClipboardRules.PlanPaste(target, new PasteShape(2, 2)).Reason);
    }

    [Fact] // ADR-0014: a disjoint target that happens to contain a 1×1 range still gets the multi-selection refusal
    public void A_disjoint_target_containing_a_single_cell_is_refused_as_disjoint()
    {
        var target = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ToggleRange(new(5, 5), Grid); // two 1×1 ranges

        Assert.Equal(PasteRefusalReason.DisjointTarget,
            ClipboardRules.PlanPaste(target, new PasteShape(2, 2)).Reason);
    }

    [Fact] // ADR-0014: rows invisible or not yet fetched are included — pasting into a whole column is intended
    public void One_value_into_a_whole_million_row_column_is_approved_without_materializing_cells()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid).SelectWholeColumns(Grid); // 1,000,000 × 1

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(1, 1));

        Assert.False(decision.IsRefused);
        Assert.Single(decision.Plan.Targets);
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(999_999, 0)));
    }

    [Fact] // ADR-0014: a 2-row block tiles a million-row column — the multiple rule, at scale
    public void Two_rows_tile_a_whole_million_row_column()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid).SelectWholeColumns(Grid);

        var decision = ClipboardRules.PlanPaste(target, new PasteShape(2, 1));

        Assert.False(decision.IsRefused);
        Assert.Equal(new SourceCell(1, 0), decision.Plan.SourceCellFor(new(999_999, 0)));
    }

    [Fact] // ADR-0014: nowhere to paste — refused with its own reason, nothing happens
    public void An_empty_target_is_refused_as_empty()
    {
        Assert.Equal(PasteRefusalReason.EmptySelection,
            ClipboardRules.PlanPaste(GridSelection.Empty, new PasteShape(1, 1)).Reason);
    }

    [Fact] // ADR-0014: a cell outside every target range is a caller bug — refuse to answer
    public void SourceCellFor_a_cell_outside_the_targets_throws()
    {
        var target = GridSelection.Empty.Click(new(2, 2), Grid).ExtendTo(new(4, 5), Grid);
        var plan = ClipboardRules.PlanPaste(target, new PasteShape(1, 1)).Plan;

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.SourceCellFor(new(0, 0)));
    }

    [Fact] // ADR-0014: 1×1 into a single selected cell is an ordinary paste, not "range → 1 cell"
    public void A_single_cell_into_a_single_cell_is_approved()
    {
        var target = GridSelection.Empty.Click(new(3, 3), Grid);

        Assert.False(ClipboardRules.PlanPaste(target, new PasteShape(1, 1)).IsRefused);
    }

    [Fact] // ADR-0014: a shape with no rows or columns is a parser bug, not a refusal
    public void A_degenerate_paste_shape_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasteShape(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PasteShape(1, 0));
    }

    [Fact] // ADR-0014: default(PasteShape) sidesteps the ctor — PlanPaste refuses it by name
    public void A_default_paste_shape_is_refused_by_name_not_by_crash()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid);

        Assert.Throws<ArgumentOutOfRangeException>(() => ClipboardRules.PlanPaste(target, default));
    }
}
