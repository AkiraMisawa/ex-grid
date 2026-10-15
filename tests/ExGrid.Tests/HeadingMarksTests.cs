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

    [Fact]
    public void ADR0008_empty_selection_or_empty_extent_has_no_whole_heading()
    {
        Assert.Empty(HeadingMarks.WholeColumns(GridSelection.Empty, Grid));
        Assert.Empty(HeadingMarks.WholeRows(GridSelection.Empty, Grid));
        var selected = GridSelection.Empty.Click(new(0, 0), Grid);
        foreach (var extent in new[] { new GridExtent(0, 4), new GridExtent(4, 0), new GridExtent(0, 0) })
        {
            Assert.Empty(HeadingMarks.WholeColumns(selected, extent));
            Assert.Empty(HeadingMarks.WholeRows(selected, extent));
        }
    }

    [Fact]
    public void ADR0008_adjacent_tiles_cover_the_whole_grid_without_one_whole_range()
    {
        var extent = new GridExtent(4, 4);
        var selection = GridSelection.Empty.Click(new(0, 0), extent).ExtendTo(new(1, 1), extent)
            .ToggleRange(new(0, 2), extent).ExtendTo(new(1, 3), extent)
            .ToggleRange(new(2, 3), extent).ExtendTo(new(3, 0), extent);
        Assert.Equal(3, selection.Ranges.Count);
        Assert.All(selection.Ranges, range => Assert.False(range.SpansEveryRow(extent)));

        Assert.Equal([new ColumnRange(0, 4)], HeadingMarks.WholeColumns(selection, extent));
        Assert.Equal([new RowRange(0, 4)], HeadingMarks.WholeRows(selection, extent));
    }

    [Fact]
    public void ADR0008_overlaps_do_not_make_up_for_a_hole_and_another_range_can_fill_it()
    {
        var extent = new GridExtent(5, 4);
        var selection = GridSelection.Empty.Click(new(0, 0), extent).ExtendTo(new(2, 2), extent)
            .ToggleRange(new(3, 2), extent).ExtendTo(new(1, 0), extent);
        Assert.Equal(2, selection.Ranges.Count);
        // Each marked column is counted six times, but row 4 is absent from both ranges.
        Assert.Equal(18, selection.CellCount);
        Assert.Empty(HeadingMarks.WholeColumns(selection, extent));
        Assert.Empty(HeadingMarks.WholeRows(selection, extent));

        selection = selection.ToggleRange(new(4, 1), extent).ExtendTo(new(2, 2), extent);
        Assert.Equal([new ColumnRange(1, 2)], HeadingMarks.WholeColumns(selection, extent));
        Assert.Empty(HeadingMarks.WholeRows(selection, extent));
    }

    [Fact]
    public void ADR0008_whole_heading_runs_keep_holes_and_join_touching_coverage()
    {
        var extent = new GridExtent(4, 6);
        var columns = GridSelection.Empty.SelectColumn(5, extent, 0)
            .ToggleColumn(1, extent, 0).ToggleColumn(0, extent, 0).ToggleColumn(3, extent, 0);
        Assert.Equal([new ColumnRange(0, 2), new ColumnRange(3, 1), new ColumnRange(5, 1)],
            HeadingMarks.WholeColumns(columns, extent));
        Assert.Empty(HeadingMarks.WholeRows(columns, extent));

        var rows = GridSelection.Empty.SelectRow(3, extent, 0)
            .ToggleRow(0, extent, 0).ToggleRow(1, extent, 0);
        Assert.Equal([new RowRange(0, 2), new RowRange(3, 1)], HeadingMarks.WholeRows(rows, extent));
        Assert.Empty(HeadingMarks.WholeColumns(rows, extent));
    }

    [Fact]
    public void ADR0008_a_removed_cell_interrupts_both_whole_axes()
    {
        var extent = new GridExtent(5, 6);
        var selection = GridSelection.Empty.Click(new(0, 0), extent).SelectAll(extent)
            .ToggleRange(new(2, 3), extent);
        Assert.Equal([new ColumnRange(0, 3), new ColumnRange(4, 2)], HeadingMarks.WholeColumns(selection, extent));
        Assert.Equal([new RowRange(0, 2), new RowRange(3, 2)], HeadingMarks.WholeRows(selection, extent));
    }

    [Fact]
    public void ADR0008_the_current_extent_decides_whether_coverage_is_whole()
    {
        var extent = new GridExtent(4, 4);
        var selection = GridSelection.Empty.Click(new(0, 0), extent).SelectAll(extent);
        Assert.Empty(HeadingMarks.WholeColumns(selection, new GridExtent(5, 4)));
        Assert.Equal([new RowRange(0, 4)], HeadingMarks.WholeRows(selection, new GridExtent(5, 4)));
        Assert.Empty(HeadingMarks.WholeRows(selection, new GridExtent(4, 5)));
        Assert.Equal([new ColumnRange(0, 4)], HeadingMarks.WholeColumns(selection, new GridExtent(4, 5)));
    }

    [Fact]
    public void ADR0008_maximum_extents_are_covered_by_boundaries_without_enumerating_cells()
    {
        var extent = new GridExtent(int.MaxValue, int.MaxValue);
        var all = GridSelection.Empty.Click(new(0, 0), extent).SelectAll(extent);
        Assert.Equal([new ColumnRange(0, int.MaxValue)], HeadingMarks.WholeColumns(all, extent));
        Assert.Equal([new RowRange(0, int.MaxValue)], HeadingMarks.WholeRows(all, extent));

        var split = GridSelection.Empty.Click(new(0, 1), extent).ExtendTo(new(int.MaxValue - 2, 2), extent)
            .ToggleRange(new(int.MaxValue - 1, 1), extent).ExtendTo(new(int.MaxValue - 1, 2), extent);
        Assert.Equal([new ColumnRange(1, 2)], HeadingMarks.WholeColumns(split, extent));
        Assert.Empty(HeadingMarks.WholeRows(split, extent));

        var last = GridSelection.Empty.SelectColumn(int.MaxValue - 1, extent, 0);
        Assert.Equal([new ColumnRange(int.MaxValue - 1, 1)], HeadingMarks.WholeColumns(last, extent));
    }

    [Fact]
    public void ADR0008_whole_axes_match_cell_union_after_deterministic_mixed_gestures()
    {
        var random = new Random(8008);
        for (var sample = 0; sample < 200; sample++)
        {
            var extent = new GridExtent(random.Next(1, 9), random.Next(1, 9));
            var selection = GridSelection.Empty;
            for (var step = 0; step < 25; step++)
            {
                var row = random.Next(extent.RowCount);
                var column = random.Next(extent.ColumnCount);
                selection = (step % 5) switch
                {
                    0 => selection.ToggleColumn(column, extent, row),
                    1 => selection.ToggleRow(row, extent, column),
                    _ => selection.ToggleRange(new(row, column), extent)
                        .ExtendTo(new(random.Next(extent.RowCount), random.Next(extent.ColumnCount)), extent),
                };
                var expectedColumns = BruteRuns(extent.ColumnCount, columnIndex =>
                    Enumerable.Range(0, extent.RowCount).All(rowIndex => selection.Contains(new(rowIndex, columnIndex))));
                var expectedRows = BruteRuns(extent.RowCount, rowIndex =>
                    Enumerable.Range(0, extent.ColumnCount).All(columnIndex => selection.Contains(new(rowIndex, columnIndex))));
                Assert.Equal(expectedColumns.Select(run => new ColumnRange(run.Start, run.Count)),
                    HeadingMarks.WholeColumns(selection, extent));
                Assert.Equal(expectedRows.Select(run => new RowRange(run.Start, run.Count)),
                    HeadingMarks.WholeRows(selection, extent));
            }
        }
    }

    private static List<(int Start, int Count)> BruteRuns(int count, Func<int, bool> covered)
    {
        var runs = new List<(int Start, int Count)>();
        for (var position = 0; position < count; position++)
        {
            if (!covered(position))
                continue;
            if (runs.Count > 0 && runs[^1].Start + runs[^1].Count == position)
                runs[^1] = (runs[^1].Start, runs[^1].Count + 1);
            else
                runs.Add((position, 1));
        }
        return runs;
    }

}
