using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Ctrl+click and Ctrl+drag on Headings, the pure half (ADR-0050, item 1, and ADR-0012, both
/// 2026-09-29; SR-2e, DC-42). Ctrl+click on a column not wholly selected adds it as a new range,
/// the range made last, with the Focus on the row the holder names — its first visible row. On a
/// column wholly selected it takes the column out, and the Focus follows ADR-0052's take-out rule:
/// the first remaining cell, by rows, of the range made last. Ctrl+drag is the add followed by
/// <see cref="GridSelection.ExtendToColumn"/>. The same by rows. 100 rows × 26 columns.
/// </summary>
public class HeadingToggleTests
{
    private static readonly GridExtent Grid = new(100, 26);

    private static SelectionRange Column(int column) => new(0, column, Grid.RowCount, 1);

    private static SelectionRange Row(int row) => new(row, 0, 1, Grid.ColumnCount);

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: Ctrl+click on a column not wholly selected adds it, the Focus on its first visible row
    public void Ctrl_click_on_a_column_adds_it_as_a_new_range()
    {
        var selection = GridSelection.Empty.Click(new(3, 3), Grid).ToggleColumn(5, Grid, focusRow: 12);

        Assert.Equal([new SelectionRange(3, 3, 1, 1), Column(5)], selection.Ranges);
        Assert.Equal(new CellPosition(12, 5), selection.Focus);
        Assert.Equal(Column(5), selection.FocusRange);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: a column only partly selected is not wholly selected, and is added whole
    public void Ctrl_click_on_a_partly_selected_column_adds_the_whole_column()
    {
        var partly = GridSelection.Empty.Click(new(0, 2), Grid).ExtendTo(new(4, 2), Grid);

        var selection = partly.ToggleColumn(2, Grid, focusRow: 0);

        Assert.Equal([new SelectionRange(0, 2, 5, 1), Column(2)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 2), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: Ctrl+click on a wholly selected column takes it out; the Focus goes to the first remaining cell by rows
    public void Ctrl_click_on_a_wholly_selected_column_takes_it_out()
    {
        var threeColumns = GridSelection.Empty.SelectColumn(0, Grid, focusRow: 7).ExtendToColumn(2, Grid);

        var selection = threeColumns.ToggleColumn(1, Grid, focusRow: 7);

        // The pieces in the order a take-out lists them (SelectionRange.SubtractArea): to the right,
        // then to the left.
        Assert.Equal([Column(2), Column(0)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
        Assert.False(selection.Contains(new(50, 1)));
    }

    [Fact] // ADR-0052 (third run) / SR-2e: taking out the range made last leaves the Focus on the latest range still standing
    public void Taking_out_the_column_made_last_moves_the_focus_to_the_range_before_it()
    {
        var withColumn = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid).ToggleColumn(3, Grid, focusRow: 0);

        var selection = withColumn.ToggleColumn(3, Grid, focusRow: 0);

        Assert.Equal([new SelectionRange(0, 0, 2, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
    }

    [Fact] // ADR-0052 (third run) / SR-2e: the Focus goes to the range made last, whichever range the column was cut from
    public void Taking_a_column_out_of_an_earlier_range_leaves_the_focus_in_the_range_made_last()
    {
        var two = GridSelection.Empty.SelectColumn(1, Grid, focusRow: 0).ExtendToColumn(3, Grid)
            .ToggleRange(new(10, 8), Grid).ExtendTo(new(12, 9), Grid);

        var selection = two.ToggleColumn(2, Grid, focusRow: 0);

        Assert.Equal([Column(3), Column(1), new SelectionRange(10, 8, 3, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(10, 8), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: a column is wholly selected when every cell of it is, whichever ranges hold them
    public void A_column_covered_by_several_ranges_is_wholly_selected()
    {
        var pieces = GridSelection.Empty.Click(new(0, 0), Grid)
            .ToggleRange(new(0, 2), Grid).ExtendTo(new(49, 2), Grid)
            .ToggleRange(new(50, 2), Grid).ExtendTo(new(99, 2), Grid);
        Assert.True(pieces.CoversColumn(2, Grid));
        Assert.False(pieces.CoversColumn(0, Grid));

        var selection = pieces.ToggleColumn(2, Grid, focusRow: 0);

        Assert.Equal([new SelectionRange(0, 0, 1, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
    }

    [Fact] // ADR-0052 / SR-2e: the only thing selected cannot be taken out, as the only selected cell cannot
    public void The_only_selected_column_cannot_be_taken_out()
    {
        var one = GridSelection.Empty.SelectColumn(4, Grid, focusRow: 9);

        Assert.Same(one, one.ToggleColumn(4, Grid, focusRow: 9));
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: from nothing selected, Ctrl+click selects the column alone
    public void Ctrl_click_from_empty_selects_the_column()
    {
        var selection = GridSelection.Empty.ToggleColumn(6, Grid, focusRow: 20);

        Assert.Equal([Column(6)], selection.Ranges);
        Assert.Equal(new CellPosition(20, 6), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: Ctrl+drag adds whole columns from the pressed one to the pointer's as one range
    public void Ctrl_drag_adds_one_range_of_whole_columns()
    {
        var before = GridSelection.Empty.Click(new(3, 3), Grid);

        var selection = before.ToggleColumn(8, Grid, focusRow: 0).ExtendToColumn(5, Grid);

        Assert.Equal([new SelectionRange(3, 3, 1, 1), new SelectionRange(0, 5, 100, 4)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 8), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: the same on Row Headings — Ctrl+click adds a whole row, the Focus on the column named
    public void Ctrl_click_on_a_row_adds_it_as_a_new_range()
    {
        var selection = GridSelection.Empty.Click(new(3, 3), Grid).ToggleRow(40, Grid, focusColumn: 2);

        Assert.Equal([new SelectionRange(3, 3, 1, 1), Row(40)], selection.Ranges);
        Assert.Equal(new CellPosition(40, 2), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: Ctrl+click on a wholly selected row takes it out
    public void Ctrl_click_on_a_wholly_selected_row_takes_it_out()
    {
        var rows = GridSelection.Empty.SelectRow(10, Grid, focusColumn: 0).ExtendToRow(12, Grid);

        var selection = rows.ToggleRow(11, Grid, focusColumn: 0);

        Assert.Equal([Row(12), Row(10)], selection.Ranges);
        Assert.Equal(new CellPosition(10, 0), selection.Focus);
        Assert.True(selection.CoversRow(12, Grid));
        Assert.False(selection.CoversRow(11, Grid));
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: Ctrl+drag on Row Headings adds whole rows as one range
    public void Ctrl_drag_on_rows_adds_one_range_of_whole_rows()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ToggleRow(20, Grid, focusColumn: 0).ExtendToRow(23, Grid);

        Assert.Equal([new SelectionRange(0, 0, 1, 1), new SelectionRange(20, 0, 4, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(20, 0), selection.Focus);
    }

    [Fact] // ADR-0050: a column or row outside the grid is refused by name
    public void A_column_or_row_outside_the_grid_is_refused()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid);

        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ToggleColumn(26, Grid, focusRow: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.ToggleRow(-1, Grid, focusColumn: 0));
    }

    [Fact] // ADR-0012/0052: subtracting a rectangle leaves the pieces bottom to top, as subtracting a cell does
    public void Subtracting_a_rectangle_leaves_its_pieces_bottom_to_top()
    {
        var range = new SelectionRange(0, 0, 5, 5);

        Assert.Equal(
            [new SelectionRange(4, 0, 1, 5), new SelectionRange(1, 4, 3, 1), new SelectionRange(1, 0, 3, 1), new SelectionRange(0, 0, 1, 5)],
            range.SubtractArea(new SelectionRange(1, 1, 3, 3)));
        Assert.Equal([range], range.SubtractArea(new SelectionRange(10, 10, 2, 2)));
        Assert.Empty(range.SubtractArea(new SelectionRange(0, 0, 10, 10)));
        Assert.Equal(range.Subtract(new CellPosition(2, 2)), range.SubtractArea(new SelectionRange(2, 2, 1, 1)));
    }
}
