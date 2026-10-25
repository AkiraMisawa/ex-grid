using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Heading Mark's whole runs (ADR-0008, D1) against a cell-by-cell union, over random mixes of
/// gestures: the check a single case cannot state. Its cases are shared
/// (<c>tests/cases/selection/heading-marks.json</c>, ADR-0350); the Script Core's half is
/// <c>src/npm/core/test/heading-marks-property.test.ts</c>, over the same mix of gestures drawn by a
/// generator and seed of its own, so a failing sample is replayed in the language that found it.
/// </summary>
public class HeadingMarksPropertyTests
{
    [Fact] // ADR-0008, D1: the sweep answers what a cell-by-cell union does
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
