using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The paste that spills (ADR-0050, item 3): a Consumer's declaration that "range → one
/// cell" writes the block from that cell. The shape half, pure; the Selection that follows
/// is the component's and is pinned in layer 2.
/// </summary>
public class SpillPasteTests
{
    private static readonly GridExtent Grid = new(100, 10);

    private static readonly Func<int, bool> Editable = _ => true;

    private static Func<int, bool> EditableExcept(params int[] locked) =>
        column => !locked.Contains(column);

    private static GridSelection Cell(int row, int column) =>
        GridSelection.Empty.Click(new(row, column), Grid);

    [Fact] // ADR-0050 item 3 / DC-8: an m×n block onto one cell plans the block from that cell
    public void A_block_onto_one_cell_spills_from_that_cell_as_its_top_left()
    {
        var decision = ClipboardRules.PlanPaste(Cell(4, 2), new PasteShape(3, 2), Editable, Grid);

        Assert.False(decision.IsRefused);
        Assert.Equal([new SelectionRange(4, 2, 3, 2)], decision.Plan.Targets);
        Assert.Equal(new SourceCell(0, 0), decision.Plan.SourceCellFor(new(4, 2)));
        Assert.Equal(new SourceCell(2, 1), decision.Plan.SourceCellFor(new(6, 3)));
    }

    [Fact] // ADR-0050 item 3 / DC-8: one plan, one range — the intent is one notification carrying the block
    public void A_spill_is_one_target_range_covering_exactly_the_block()
    {
        var plan = ClipboardRules.PlanPaste(Cell(0, 0), new PasteShape(3, 3), Editable, Grid).Plan;

        var range = Assert.Single(plan.Targets);
        Assert.Equal(9, range.CellCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.SourceCellFor(new(3, 0)));
    }

    [Fact] // ADR-0014 / DC-9: without the declaration, range → one cell keeps its refusal
    public void Without_the_declaration_the_single_cell_refusal_stands()
    {
        var decision = ClipboardRules.PlanPaste(Cell(4, 2), new PasteShape(3, 2), Editable);

        Assert.Equal(PasteRefusalReason.SingleCellTarget, decision.Reason);
    }

    [Fact] // ADR-0050 item 3 / DC-10: a block reaching past the last row is refused by name
    public void A_spill_past_the_last_row_is_refused_by_name()
    {
        var decision = ClipboardRules.PlanPaste(Cell(98, 0), new PasteShape(3, 1), Editable, Grid);

        Assert.Equal(PasteRefusalReason.SpillPastExtent, decision.Reason);
    }

    [Fact] // ADR-0050 item 3 / DC-10: a block reaching past the last column is refused by name
    public void A_spill_past_the_last_column_is_refused_by_name()
    {
        var decision = ClipboardRules.PlanPaste(Cell(0, 9), new PasteShape(1, 2), Editable, Grid);

        Assert.Equal(PasteRefusalReason.SpillPastExtent, decision.Reason);
    }

    [Fact] // ADR-0050 item 3: a block exactly reaching the last row and column fits
    public void A_spill_ending_on_the_last_cell_fits()
    {
        var decision = ClipboardRules.PlanPaste(Cell(97, 8), new PasteShape(3, 2), Editable, Grid);

        Assert.Equal([new SelectionRange(97, 8, 3, 2)], decision.Plan.Targets);
    }

    [Fact] // ADR-0050 item 3 / ADR-0035 / DC-10: Editable is judged on the spilled block, not only the pasted-on cell
    public void A_spill_covering_a_non_editable_column_is_refused_whole()
    {
        var decision = ClipboardRules.PlanPaste(Cell(0, 2), new PasteShape(2, 3), EditableExcept(4), Grid);

        Assert.Equal(PasteRefusalReason.TargetNotEditable, decision.Reason);
    }

    [Fact] // ADR-0035 order: "may not" before "cannot" — a locked column in the block outranks the edge
    public void The_declaration_is_reported_before_the_edge()
    {
        var decision = ClipboardRules.PlanPaste(Cell(99, 8), new PasteShape(3, 3), EditableExcept(9), Grid);

        Assert.Equal(PasteRefusalReason.TargetNotEditable, decision.Reason);
    }

    [Fact] // ADR-0050 item 3: every other shape rule of ADR-0014 stands under the declaration
    public void The_other_shape_rules_stand_under_the_declaration()
    {
        var ragged = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(4, 0), Grid);
        Assert.Equal(PasteRefusalReason.ShapeMismatch,
            ClipboardRules.PlanPaste(ragged, new PasteShape(3, 1), Editable, Grid).Reason);

        var disjoint = Cell(0, 0).ToggleRange(new(5, 5), Grid);
        Assert.Equal(PasteRefusalReason.DisjointTarget,
            ClipboardRules.PlanPaste(disjoint, new PasteShape(2, 2), Editable, Grid).Reason);

        Assert.Equal(PasteRefusalReason.EmptySelection,
            ClipboardRules.PlanPaste(GridSelection.Empty, new PasteShape(2, 2), Editable, Grid).Reason);

        var tiled = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(5, 1), Grid);
        Assert.Equal([new SelectionRange(0, 0, 6, 2)],
            ClipboardRules.PlanPaste(tiled, new PasteShape(2, 1), Editable, Grid).Plan.Targets);
    }

    [Fact] // ADR-0014 / ADR-0050: a single value onto one cell is not a spill — it writes that cell
    public void A_single_value_onto_one_cell_is_unchanged_by_the_declaration()
    {
        var decision = ClipboardRules.PlanPaste(Cell(3, 3), new PasteShape(1, 1), Editable, Grid);

        Assert.Equal([new SelectionRange(3, 3, 1, 1)], decision.Plan.Targets);
    }
}
