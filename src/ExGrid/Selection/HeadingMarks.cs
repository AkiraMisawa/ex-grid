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

    /// <summary>The columns covered in full by the Selection's union, as ordered runs (ADR-0008, D1).</summary>
    public static ColumnRange[] WholeColumns(GridSelection selection, GridExtent extent)
        => WholeAxis(selection, extent.ColumnCount, extent.RowCount, columns: true)
            .Select(run => new ColumnRange(run.Start, run.End - run.Start)).ToArray();

    /// <summary>The rows covered in full by the Selection's union, as ordered runs (ADR-0008, D1).</summary>
    public static RowRange[] WholeRows(GridSelection selection, GridExtent extent)
        => WholeAxis(selection, extent.RowCount, extent.ColumnCount, columns: false)
            .Select(run => new RowRange(run.Start, run.End - run.Start)).ToArray();

    // Sweep one axis at rectangle boundaries. Between two boundaries, every cross-section has
    // the same selected intervals on the other axis. Its minimum coverage is positive exactly
    // when the union covers that whole axis: overlaps cannot make up for a hole. Compressed
    // coordinates keep both time and storage dependent on ranges, never on the grid's extent.
    private static List<(int Start, int End)> WholeAxis(
        GridSelection selection, int axisCount, int crossCount, bool columns)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var runs = new List<(int Start, int End)>();
        if (selection.IsEmpty || axisCount <= 0 || crossCount <= 0)
            return runs;

        var events = new List<CoverageEvent>();
        var boundaries = new List<int> { 0, crossCount };
        foreach (var range in selection.Ranges)
        {
            var start = columns ? range.LeftColumn : range.TopRow;
            var crossStart = columns ? range.TopRow : range.LeftColumn;
            // Half-open ends fit even when the extent is int.MaxValue. Add in long before
            // clipping, so the inclusive-bound accessor's intermediate arithmetic is not used.
            var end = (int)Math.Min(axisCount, (long)start + (columns ? range.ColumnCount : range.RowCount));
            var crossEnd = (int)Math.Min(crossCount,
                (long)crossStart + (columns ? range.RowCount : range.ColumnCount));
            if (start >= end || crossStart >= crossEnd)
                continue;
            events.Add(new(start, crossStart, crossEnd, 1));
            events.Add(new(end, crossStart, crossEnd, -1));
            boundaries.Add(crossStart);
            boundaries.Add(crossEnd);
        }
        if (events.Count == 0)
            return runs;

        events.Sort(static (a, b) => a.Position.CompareTo(b.Position));
        boundaries.Sort();
        var coordinates = new Dictionary<int, int>();
        foreach (var boundary in boundaries)
            if (!coordinates.ContainsKey(boundary))
                coordinates.Add(boundary, coordinates.Count);
        var coverage = new CoverageMinimum(coordinates.Count - 1);
        var previous = events[0].Position;
        var index = 0;
        while (index < events.Count)
        {
            var position = events[index].Position;
            if (previous < position && coverage.AllCovered)
            {
                if (runs.Count > 0 && runs[^1].End == previous)
                    runs[^1] = (runs[^1].Start, position);
                else
                    runs.Add((previous, position));
            }
            // Apply every start and end at this coordinate before examining the next span;
            // an adjacent pair of rectangles leaves no gap, whatever the event sort's tie order.
            do
            {
                var change = events[index++];
                coverage.Add(coordinates[change.CrossStart], coordinates[change.CrossEnd], change.Delta);
            } while (index < events.Count && events[index].Position == position);
            previous = position;
        }
        return runs;
    }

    private readonly record struct CoverageEvent(int Position, int CrossStart, int CrossEnd, int Delta);

    // Range addition and the global minimum on compressed intervals. Each node's minimum
    // includes its own pending addition but not its ancestors', so a partial update needs no
    // propagation; recomputing the parent adds its pending value to its children's minimum.
    private sealed class CoverageMinimum(int count)
    {
        private readonly int[] _minimum = new int[checked(count * 4)];
        private readonly int[] _added = new int[checked(count * 4)];

        public bool AllCovered => _minimum[1] > 0;

        public void Add(int start, int end, int delta) => Add(1, 0, count, start, end, delta);

        private void Add(int node, int left, int right, int start, int end, int delta)
        {
            if (start <= left && right <= end)
            {
                _minimum[node] += delta;
                _added[node] += delta;
                return;
            }
            var middle = left + (right - left) / 2;
            if (start < middle)
                Add(node * 2, left, middle, start, end, delta);
            if (end > middle)
                Add(node * 2 + 1, middle, right, start, end, delta);
            _minimum[node] = _added[node] + Math.Min(_minimum[node * 2], _minimum[node * 2 + 1]);
        }
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
