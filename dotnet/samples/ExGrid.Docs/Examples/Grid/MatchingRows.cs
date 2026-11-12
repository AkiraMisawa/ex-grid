namespace ExGrid.Docs.Examples.Grid;

using ExGrid.Selection;

/// <summary>The rows that meet a condition, as the ranges a Consumer places with <c>PlaceSelectionAsync</c>.</summary>
public static class MatchingRows
{
    /// <summary>
    /// The rows of <paramref name="rows"/> that meet <paramref name="condition"/>, each run of neighbours as one range
    /// across <paramref name="columnCount"/> columns, top to bottom. <paramref name="rows"/> are the rows in the
    /// order the grid shows now, so a position here is a position there. The grid joins nothing: rows that stand
    /// together are one range because they are handed as one, and every range handed costs a little on each paint.
    /// </summary>
    public static List<SelectionRange> Ranges<TRow>(IReadOnlyList<TRow> rows, Func<TRow, bool> condition, int columnCount)
    {
        var ranges = new List<SelectionRange>();
        for (var row = 0; row < rows.Count; row++)
        {
            if (!condition(rows[row]))
                continue;
            if (ranges.Count > 0 && ranges[^1] is var last && last.TopRow + last.RowCount == row)
                ranges[^1] = new SelectionRange(last.TopRow, 0, last.RowCount + 1, columnCount);
            else
                ranges.Add(new SelectionRange(row, 0, 1, columnCount));
        }
        return ranges;
    }
}
