using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>Ctrl+D and Ctrl+R (ADR-0035, CP-24/CP-25): where the source is, what the target
/// is, and every reason a fill is refused.</summary>
public class FillRuleTests
{
    private static readonly GridExtent Grid = new(1_000, 20);
    private static readonly Func<int, bool> Editable = _ => true;

    private static GridSelection Range(int top, int left, int bottom, int right)
        => GridSelection.Empty.Click(new(top, left), Grid).ExtendTo(new(bottom, right), Grid);

    [Fact] // ADR-0035 / CP-24: Ctrl+D copies the top row down the rest of the range
    public void Fill_down_reads_the_top_row_and_writes_the_rows_below_it()
    {
        var plan = ClipboardRules.PlanFill(Range(3, 2, 7, 4), GridDirection.Down, Editable).Plan;

        Assert.Equal(new SelectionRange(3, 2, 1, 3), plan.Source);
        Assert.Equal([new SelectionRange(4, 2, 4, 3)], plan.Paste.Targets);
        Assert.Equal(new PasteShape(1, 3), plan.Paste.Source);
        // Tiled by the paste arithmetic: every target row reads source row 0, same column.
        Assert.Equal(new SourceCell(0, 2), plan.Paste.SourceCellFor(new(7, 4)));
    }

    [Fact] // ADR-0035 / CP-24: Ctrl+R copies the left column across the rest of the range
    public void Fill_right_reads_the_left_column_and_writes_the_columns_right_of_it()
    {
        var plan = ClipboardRules.PlanFill(Range(3, 2, 7, 4), GridDirection.Right, Editable).Plan;

        Assert.Equal(new SelectionRange(3, 2, 5, 1), plan.Source);
        Assert.Equal([new SelectionRange(3, 3, 5, 2)], plan.Paste.Targets);
        Assert.Equal(new SourceCell(4, 0), plan.Paste.SourceCellFor(new(7, 4)));
    }

    [Fact] // ADR-0035: a range one row tall fills from the row above — Excel's Ctrl+D on one cell
    public void A_range_one_row_tall_fills_from_the_row_above()
    {
        var plan = ClipboardRules.PlanFill(Range(5, 1, 5, 3), GridDirection.Down, Editable).Plan;

        Assert.Equal(new SelectionRange(4, 1, 1, 3), plan.Source);
        Assert.Equal([new SelectionRange(5, 1, 1, 3)], plan.Paste.Targets);
    }

    [Fact] // ADR-0035: a range one column wide fills from the column to its left
    public void A_range_one_column_wide_fills_from_the_column_to_its_left()
    {
        var plan = ClipboardRules.PlanFill(Range(2, 6, 4, 6), GridDirection.Right, Editable).Plan;

        Assert.Equal(new SelectionRange(2, 5, 3, 1), plan.Source);
        Assert.Equal([new SelectionRange(2, 6, 3, 1)], plan.Paste.Targets);
    }

    [Theory] // ADR-0035 / CP-25: at the first row or column there is nothing to fill from
    [InlineData(GridDirection.Down, 0, 3)]
    [InlineData(GridDirection.Right, 3, 0)]
    public void Nothing_to_fill_from_is_refused_by_name(GridDirection direction, int row, int column)
    {
        var decision = ClipboardRules.PlanFill(Range(row, column, row, column), direction, Editable);

        Assert.True(decision.IsRefused);
        Assert.Equal(PasteRefusalReason.NothingToFillFrom, decision.Reason);
    }

    [Fact] // ADR-0035 / CP-25: several ranges would need several sources
    public void More_than_one_range_is_refused()
    {
        var selection = Range(1, 1, 3, 1).ToggleRange(new(8, 8), Grid);

        var decision = ClipboardRules.PlanFill(selection, GridDirection.Down, Editable);

        Assert.Equal(PasteRefusalReason.MultipleRanges, decision.Reason);
    }

    [Fact] // ADR-0035 / CP-25: an empty selection names nothing
    public void An_empty_selection_is_refused()
        => Assert.Equal(
            PasteRefusalReason.EmptySelection,
            ClipboardRules.PlanFill(GridSelection.Empty, GridDirection.Down, Editable).Reason);

    [Fact] // ADR-0035 / CP-25: the columns written must be editable
    public void A_non_editable_target_column_is_refused_whole()
    {
        var decision = ClipboardRules.PlanFill(Range(1, 1, 5, 3), GridDirection.Down, column => column != 3);

        Assert.Equal(PasteRefusalReason.TargetNotEditable, decision.Reason);
    }

    [Theory] // ADR-0035 / CP-25: the declaration is reported first — before there being nothing to fill from
    [InlineData(GridDirection.Down)]
    [InlineData(GridDirection.Right)]
    public void The_declaration_outranks_nothing_to_fill_from(GridDirection direction)
    {
        var decision = ClipboardRules.PlanFill(Range(0, 0, 0, 0), direction, _ => false);

        Assert.Equal(PasteRefusalReason.TargetNotEditable, decision.Reason);
    }

    [Fact] // ADR-0035 / CP-25: and before several ranges, each judged on the target it alone would have
    public void The_declaration_outranks_multiple_ranges()
    {
        var selection = Range(1, 1, 3, 1).ToggleRange(new(8, 4), Grid);

        var decision = ClipboardRules.PlanFill(selection, GridDirection.Down, column => column != 4);

        Assert.Equal(PasteRefusalReason.TargetNotEditable, decision.Reason);
    }

    [Fact] // ADR-0035 / CP-25: the Ctrl+R source column is read, not written, so it may be non-editable
    public void A_non_editable_source_column_is_not_refused()
    {
        var multi = ClipboardRules.PlanFill(Range(1, 1, 5, 3), GridDirection.Right, column => column != 1);
        var single = ClipboardRules.PlanFill(Range(1, 2, 5, 2), GridDirection.Right, column => column != 1);

        Assert.False(multi.IsRefused);
        Assert.False(single.IsRefused);
    }

    [Fact] // ADR-0035 / ADR-0005: a source past the copy cap is refused, as its read would be
    public void A_source_past_the_cap_is_refused_as_too_large()
    {
        var decision = ClipboardRules.PlanFill(Range(0, 1, 999, 2), GridDirection.Right, Editable, cellCap: 999);

        Assert.Equal(PasteRefusalReason.TooLarge, decision.Reason);
        Assert.False(ClipboardRules.PlanFill(Range(0, 1, 998, 2), GridDirection.Right, Editable, cellCap: 999).IsRefused);
    }

    [Fact] // ADR-0035: a fill runs down or right; up and left are not keys the grid has
    public void Only_down_and_right_are_fill_directions()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => ClipboardRules.PlanFill(Range(1, 1, 2, 2), GridDirection.Up, Editable));
}
