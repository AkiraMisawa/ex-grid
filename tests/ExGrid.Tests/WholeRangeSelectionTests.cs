using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Whole columns stay whole (ADR-0012, decided 2026-09-25): extending a range that spans
/// every row sideways keeps it spanning every row, as Excel does, and Shift+click on a
/// header selects whole columns. Resizing several whole columns at once (ADR-0016)
/// depends on both.
/// </summary>
public class WholeRangeSelectionTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0012 / SR-2b: Ctrl+Space then Shift+→ gives two whole columns, not one row
    public void Shift_right_on_a_whole_column_keeps_every_row()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid)
            .SelectWholeColumns(Grid)
            .Extend(GridDirection.Right, Grid);

        Assert.Equal([new SelectionRange(0, 2, 100, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 2), selection.Anchor);
        Assert.Equal(new CellPosition(5, 3), selection.Focus);
    }

    [Fact] // ADR-0012 / SR-2b: shrinking back through the Anchor flips, still whole
    public void Shift_left_past_the_anchor_flips_and_stays_whole()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid)
            .SelectWholeColumns(Grid)
            .Extend(GridDirection.Right, Grid)
            .Extend(GridDirection.Left, Grid)
            .Extend(GridDirection.Left, Grid);

        Assert.Equal([new SelectionRange(0, 1, 100, 2)], selection.Ranges);
    }

    [Fact] // ADR-0012 / SR-2b: Ctrl+Shift+→ on a whole column runs whole columns to the edge
    public void Ctrl_shift_right_on_a_whole_column_runs_to_the_edge_whole()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 20), Grid)
            .SelectWholeColumns(Grid)
            .ExtendToEdge(GridDirection.Right, Grid);

        Assert.Equal([new SelectionRange(0, 20, 100, 6)], selection.Ranges);
    }

    [Fact] // ADR-0012 / SR-2b: Shift+Space then Shift+↓ gives two whole rows
    public void Shift_down_on_a_whole_row_keeps_every_column()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid)
            .SelectWholeRows(Grid)
            .Extend(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(5, 0, 2, 26)], selection.Ranges);
    }

    [Fact] // ADR-0012 / SR-2b: Shift+PageDown on a whole row keeps every column
    public void Shift_page_down_on_a_whole_row_keeps_every_column()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid)
            .SelectWholeRows(Grid)
            .ExtendByViewport(10, Grid);

        Assert.Equal([new SelectionRange(5, 0, 11, 26)], selection.Ranges);
    }

    [Fact] // ADR-0012: only the axis already spanned in full is kept — an ordinary range is redrawn as before
    public void An_ordinary_range_is_redrawn_between_its_corners()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid)
            .ExtendTo(new(7, 2), Grid)
            .Extend(GridDirection.Right, Grid);

        Assert.Equal([new SelectionRange(5, 2, 3, 2)], selection.Ranges);
    }

    [Fact] // ADR-0012 / SR-2a: Shift+click on a header selects whole columns from the Anchor's column
    public void Shift_click_on_a_header_selects_whole_columns_from_the_anchor()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 2), Grid)
            .ExtendToColumn(6, Grid);

        Assert.Equal([new SelectionRange(0, 2, 100, 5)], selection.Ranges);
        Assert.Equal(new CellPosition(5, 2), selection.Anchor);
        Assert.Equal(new CellPosition(5, 6), selection.Focus);
    }

    [Fact] // ADR-0012 / SR-2a: leftward works the same way
    public void Shift_click_on_a_header_to_the_left_selects_whole_columns()
    {
        var selection = GridSelection.Empty
            .Click(new(5, 4), Grid)
            .ExtendToColumn(1, Grid);

        Assert.Equal([new SelectionRange(0, 1, 100, 4)], selection.Ranges);
    }

    [Fact] // ADR-0012 / SR-2a: from Empty, the clicked column alone, anchored at its top
    public void Shift_click_on_a_header_from_empty_selects_that_column()
    {
        var selection = GridSelection.Empty.ExtendToColumn(3, Grid);

        Assert.Equal([new SelectionRange(0, 3, 100, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 3), selection.Anchor);
        Assert.Equal(new CellPosition(0, 3), selection.Focus);
    }

    [Fact] // ADR-0012: the other ranges stay, as Shift+click on a cell leaves them
    public void Shift_click_on_a_header_replaces_only_the_anchors_range()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ToggleRange(new(9, 9), Grid)
            .ExtendToColumn(10, Grid);

        Assert.Equal(2, selection.Ranges.Count);
        Assert.Equal(new SelectionRange(1, 1, 1, 1), selection.Ranges[0]);
        Assert.Equal(new SelectionRange(0, 9, 100, 2), selection.Ranges[1]);
    }

    [Fact] // ADR-0016 / FN-12c: the columns whole-column ranges cover, in order, each once
    public void Whole_columns_lists_the_columns_whole_column_ranges_cover()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 8), Grid)
            .SelectWholeColumns(Grid)          // 8, whole
            .ToggleRange(new(3, 1), Grid)      // an ordinary cell: not a column
            .ToggleRange(new(0, 5), Grid)
            .ExtendToColumn(8, Grid);          // 5 to 8, whole — 8 covered twice, listed once

        Assert.Equal([5, 6, 7, 8], selection.WholeColumns(Grid));
    }

    [Fact] // ADR-0016 / FN-12c: nothing whole, nothing listed
    public void Whole_columns_is_empty_without_a_whole_column_range()
    {
        Assert.Empty(GridSelection.Empty.WholeColumns(Grid));
        Assert.Empty(GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(98, 3), Grid).WholeColumns(Grid));
    }
}
