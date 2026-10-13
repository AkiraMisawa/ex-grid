namespace ExGrid.Selection;

/// <summary>
/// Which Headings the Heading Mark marks (ADR-0008, 2026-10-13): every column and every row that
/// holds part of the Selection, for every Selection, as Excel marks its Headings. Answered as runs,
/// each a stretch of consecutive marked columns or rows, so the overlay draws one rectangle per run
/// and never one per Heading: what the mark costs depends on how many runs there are, never on how
/// many columns or rows they cover (ADR-0008's economy). A run is the union of the ranges' spans
/// on that axis, with spans that overlap or touch joined, so a line runs on unbroken across
/// neighbouring marked Headings, as Excel's ran on under A and B for A1:B2 (observed: the readings
/// of 2026-10-12, R4 (2)).
///
/// <para>The Focus marks nothing of its own: it is always inside a range (ADR-0052), so its row and
/// its column are marked as any other selected cell's are, and Excel marked A1's Headings as it
/// marked B2's (observed, R4 (2)). Pure and stateless — the grid holds the Selection, and this
/// answers what it marks.</para>
/// </summary>
internal static class HeadingMarks
{
    /// <summary>The marked columns, as runs in column order, or none for an empty Selection.</summary>
    public static ColumnRange[] Columns(GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var spans = new (int First, int Last)[selection.Ranges.Count];
        for (var i = 0; i < spans.Length; i++)
            spans[i] = (selection.Ranges[i].LeftColumn, selection.Ranges[i].RightColumn);
        var runs = Union(spans);
        var columns = new ColumnRange[runs.Count];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new ColumnRange(runs[i].First, runs[i].Last - runs[i].First + 1);
        return columns;
    }

    /// <summary>The marked rows, as runs in row order, or none for an empty Selection.</summary>
    public static RowRange[] Rows(GridSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var spans = new (int First, int Last)[selection.Ranges.Count];
        for (var i = 0; i < spans.Length; i++)
            spans[i] = (selection.Ranges[i].TopRow, selection.Ranges[i].BottomRow);
        var runs = Union(spans);
        var rows = new RowRange[runs.Count];
        for (var i = 0; i < rows.Length; i++)
            rows[i] = new RowRange(runs[i].First, runs[i].Last - runs[i].First + 1);
        return rows;
    }

    /// <summary>The union of inclusive spans, as the fewest disjoint spans in order: two spans that
    /// overlap, or that touch with no index between them, are one.</summary>
    private static List<(int First, int Last)> Union((int First, int Last)[] spans)
    {
        Array.Sort(spans);
        var runs = new List<(int First, int Last)>(spans.Length);
        foreach (var span in spans)
        {
            // Compared as long: a span ending at the last index there is would overflow a step past it.
            if (runs.Count > 0 && span.First <= (long)runs[^1].Last + 1)
                runs[^1] = (runs[^1].First, Math.Max(runs[^1].Last, span.Last));
            else
                runs.Add(span);
        }
        return runs;
    }
}
