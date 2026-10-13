using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Which Headings the Heading Mark marks (ADR-0008, 2026-10-13): every column and every row that
/// holds part of the Selection, for every Selection, as runs — the union of the ranges' spans on each
/// axis, spans that overlap or touch joined. The readings of 2026-10-12 (R4) are the cases where they
/// apply: A1:B2 and D4:E6 marked A, B, D and E, and rows 1, 2, 4, 5 and 6, with C and 3 unmarked.
/// </summary>
public class HeadingMarksTests
{
    private static readonly GridExtent Grid = new(100, 26);

    [Fact] // ADR-0008 (2026-10-13): nothing selected marks nothing
    public void ADR0008_an_empty_selection_marks_no_heading()
    {
        Assert.Empty(HeadingMarks.Columns(GridSelection.Empty));
        Assert.Empty(HeadingMarks.Rows(GridSelection.Empty));
    }

    [Fact] // ADR-0008 (2026-10-13): one cell marks its own column and row — every Selection is marked, not only several ranges
    public void ADR0008_one_cell_marks_its_column_and_its_row()
    {
        var selection = GridSelection.Empty.Click(new(4, 2), Grid);

        Assert.Equal([new ColumnRange(2, 1)], HeadingMarks.Columns(selection));
        Assert.Equal([new RowRange(4, 1)], HeadingMarks.Rows(selection));
    }

    [Fact] // ADR-0008 (2026-10-13): a range marks every column and row it spans, as one run on each axis
    public void ADR0008_a_range_is_one_run_on_each_axis()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid).ExtendTo(new(6, 3), Grid);

        Assert.Equal([new ColumnRange(1, 3)], HeadingMarks.Columns(selection));
        Assert.Equal([new RowRange(1, 6)], HeadingMarks.Rows(selection));
    }

    [Fact] // ADR-0008 (2026-10-13): R4's two areas, A1:B2 and D4:E6, mark A:B and D:E, and rows 1:2 and 4:6 — C and row 3 between them stay unmarked
    public void ADR0008_several_ranges_mark_each_and_leave_the_headings_between_them()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid)
            .ToggleRange(new(3, 3), Grid).ExtendTo(new(5, 4), Grid);
        Assert.Equal(2, selection.Ranges.Count);

        Assert.Equal([new ColumnRange(0, 2), new ColumnRange(3, 2)], HeadingMarks.Columns(selection));
        Assert.Equal([new RowRange(0, 2), new RowRange(3, 3)], HeadingMarks.Rows(selection));
    }

    [Fact] // ADR-0008 (2026-10-13): runs that overlap or touch are one, in order whatever order the ranges were made in — the line runs on unbroken across neighbouring Headings, as Excel's did under A:B
    public void ADR0008_runs_that_overlap_or_touch_are_joined_and_ordered()
    {
        // Made right to left and bottom to top: E8, then C3:D5, then B5, then F1:F2.
        var selection = GridSelection.Empty.Click(new(7, 4), Grid)
            .ToggleRange(new(2, 2), Grid).ExtendTo(new(4, 3), Grid)
            .ToggleRange(new(4, 1), Grid)
            .ToggleRange(new(0, 5), Grid).ExtendTo(new(1, 5), Grid);
        Assert.Equal(4, selection.Ranges.Count);

        // Columns B, C:D, E and F touch: one run, B to F.
        Assert.Equal([new ColumnRange(1, 5)], HeadingMarks.Columns(selection));
        // Rows 1:2 and 3:5 touch, B5's row 5 lies inside, and row 8 stands alone.
        Assert.Equal([new RowRange(0, 5), new RowRange(7, 1)], HeadingMarks.Rows(selection));
    }

    [Fact] // ADR-0008 (2026-10-13): the Focus marks nothing of its own — Enter walks it through the ranges and the marks stay the ranges'
    public void ADR0008_the_focus_moving_inside_the_selection_changes_no_mark()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid)
            .ToggleRange(new(3, 3), Grid).ExtendTo(new(5, 4), Grid);
        var columns = HeadingMarks.Columns(selection);
        var rows = HeadingMarks.Rows(selection);

        for (var step = 0; step < 6; step++)
        {
            selection = selection.CycleFocus(CycleOrder.RowMajor, backward: false, Grid);
            Assert.Equal(columns, HeadingMarks.Columns(selection));
            Assert.Equal(rows, HeadingMarks.Rows(selection));
        }
    }

    [Fact] // ADR-0008 (2026-10-13) / ADR-0050: whole columns mark every row, whole rows every column, and select-all both axes whole
    public void ADR0008_whole_columns_and_rows_mark_the_whole_other_axis()
    {
        var columns = GridSelection.Empty.SelectColumn(2, Grid, focusRow: 0).ExtendToColumn(4, Grid);
        Assert.Equal([new ColumnRange(2, 3)], HeadingMarks.Columns(columns));
        Assert.Equal([new RowRange(0, 100)], HeadingMarks.Rows(columns));

        var rows = GridSelection.Empty.SelectRow(7, Grid, focusColumn: 0);
        Assert.Equal([new ColumnRange(0, 26)], HeadingMarks.Columns(rows));
        Assert.Equal([new RowRange(7, 1)], HeadingMarks.Rows(rows));

        var all = GridSelection.Empty.Click(new(3, 3), Grid).SelectAll(Grid);
        Assert.Equal([new ColumnRange(0, 26)], HeadingMarks.Columns(all));
        Assert.Equal([new RowRange(0, 100)], HeadingMarks.Rows(all));
    }
}
