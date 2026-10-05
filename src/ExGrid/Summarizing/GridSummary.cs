using ExGrid.Data;
using ExGrid.Selection;

namespace ExGrid.Summarizing;

/// <summary>
/// The reference implementation of a Selection Summary (ADR-0130), which
/// <see cref="InMemoryGridSource{TRow}"/> answers with and a remote Source is held to — as
/// <see cref="Finding.GridFind"/> is for Find.
///
/// <para>Each figure means the Aggregation of the same name (<see cref="AggregateAccumulator"/>,
/// ExPivot's one definition): a number is in every figure; text, a Boolean and a date are counted
/// and are never a number; a blank is in none. A cell covered by two ranges counts once. A column
/// with no value of its own (an Action Column) is blank. Where no cell holds a number, the figures
/// that read numbers are empty, and only Count is shown, as in Excel's status bar.</para>
/// </summary>
public static class GridSummary
{
    /// <summary>
    /// The figures <paramref name="request"/> asks for over <paramref name="rows"/> — the result in
    /// its current order.
    /// </summary>
    /// <param name="rows">Every row of the result, in the order the request was read in.</param>
    /// <param name="request">The question.</param>
    /// <param name="valueOf">A column's value accessor by name, or null for a column with no value
    /// (an Action Column) or one this Source does not know: its cells are blank.</param>
    public static GridSummaryResult Of<TRow>(
        IReadOnlyList<TRow> rows, GridSummaryRequest request, Func<string, Func<TRow, object?>?> valueOf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(valueOf);

        var columnCount = request.Columns.Count;
        var values = new Func<TRow, object?>?[columnCount];
        for (var i = 0; i < columnCount; i++)
            values[i] = valueOf(request.Columns[i]);

        var accumulator = new AggregateAccumulator();
        foreach (var (top, bottom, columns) in Bands(request.Ranges, rows.Count, columnCount))
        {
            for (var row = top; row < bottom; row++)
            {
                var item = rows[row];
                foreach (var column in columns)
                {
                    if (values[column] is { } value)
                        accumulator.Add(value(item));
                }
            }
        }
        return Answer(accumulator, request.Figures);
    }

    /// <summary>The figures asked for, read from an accumulator over the selected cells — for an
    /// answerer that folds the cells in itself (ExSheet, ExPivot).</summary>
    public static GridSummaryResult Answer(AggregateAccumulator accumulator, SummaryFigures figures)
    {
        ArgumentNullException.ThrowIfNull(accumulator);
        var answers = new Dictionary<SummaryFigures, AggregateResult>();
        foreach (var figure in SummaryFigureOrder.Each)
        {
            if ((figures & figure) == 0)
                continue;
            answers[figure] = !SummaryFigureOrder.IsCount(figure) && accumulator.Numbers == 0
                ? AggregateResult.Empty
                : accumulator.Read(SummaryFigureOrder.AggregationOf(figure));
        }
        return GridSummaryResult.Answered(answers);
    }

    /// <summary>
    /// The selected cells as bands of rows, each with the columns some range covers across the
    /// whole band, so a cell under two ranges is visited once and a whole column of a million rows
    /// is one band. Rows past <paramref name="rowCount"/> and columns past
    /// <paramref name="columnCount"/> are not cells.
    /// </summary>
    public static IEnumerable<(int Top, int Bottom, int[] Columns)> Bands(
        IReadOnlyList<SelectionRange> ranges, int rowCount, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var edges = new SortedSet<int>();
        foreach (var range in ranges)
        {
            var top = Math.Clamp(range.TopRow, 0, rowCount);
            var bottom = Math.Clamp(range.TopRow + range.RowCount, 0, rowCount);
            if (top < bottom)
            {
                edges.Add(top);
                edges.Add(bottom);
            }
        }
        int? previous = null;
        foreach (var edge in edges)
        {
            if (previous is { } top)
            {
                var columns = new SortedSet<int>();
                foreach (var range in ranges)
                {
                    if (range.TopRow <= top && range.TopRow + range.RowCount > top)
                    {
                        var right = Math.Min(range.LeftColumn + range.ColumnCount, columnCount);
                        for (var column = Math.Max(range.LeftColumn, 0); column < right; column++)
                            columns.Add(column);
                    }
                }
                if (columns.Count > 0)
                    yield return (top, edge, [.. columns]);
            }
            previous = edge;
        }
    }
}
