using ExGrid;
using ExGrid.Selection;

namespace ExposureChart.Charting;

/// <summary>
/// What a chart is of, in the data's own terms: the column that is the horizontal axis, the
/// columns that are series, and the rows — named by their value in the axis column, never by
/// where they are painted. A sort, a column drag or a recalculation leaves it meaning the same
/// thing, which a definition by selection indices would not (ADR-0011 drops a selection on
/// reorder for exactly that reason).
/// </summary>
/// <param name="RowKeys">The axis values of the rows charted: the rows the selection covered,
/// as they were shown (a filter in force narrows them, as it narrows what was selectable).</param>
public sealed record ChartDefinition(
    string XColumn,
    IReadOnlyList<string> SeriesColumns,
    IReadOnlySet<DateOnly> RowKeys);

/// <summary>A chart that could be made, or the sentence that says why not.</summary>
public sealed record ChartOutcome(ChartDefinition? Definition, string? Refusal)
{
    public static ChartOutcome Refused(string why) => new(null, why);
}

/// <summary>
/// Turns a selection into a <see cref="ChartDefinition"/>. Excel reads a selected range by
/// guessing which row is the header and which column is the axis; the grid need not guess,
/// because it knows each column's type and that headers are not data. What it cannot make
/// sense of it refuses by name rather than drawing something plausible.
/// </summary>
public static class ChartRequest
{
    public static ChartOutcome FromSelection<TRow>(
        IReadOnlyList<SelectionRange> ranges,
        int selectionVersion,
        int currentVersion,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<TRow> window)
    {
        if (ranges.Count == 0)
            return ChartOutcome.Refused("Nothing is selected.");
        // The gesture carries the order it was taken against; a different order now means the
        // indices name other rows (the spine's sixth rule).
        if (selectionVersion != currentVersion)
            return ChartOutcome.Refused("The rows were reordered after the selection was made. Select again.");

        // One cell is Excel's "chart the table around it".
        var wholeTable = ranges is [{ RowCount: 1, ColumnCount: 1 }];
        var columnIndices = wholeTable
            ? Enumerable.Range(0, columns.Count).ToList()
            : ranges.SelectMany(r => Enumerable.Range(r.LeftColumn, r.ColumnCount)).Distinct().Order().ToList();

        if (!wholeTable)
        {
            // Several ranges make one chart only when they cover the same rows, as in Excel.
            var first = ranges[0];
            if (ranges.Any(r => r.TopRow != first.TopRow || r.RowCount != first.RowCount))
                return ChartOutcome.Refused("The selected ranges cover different rows, so their points cannot be paired.");
        }

        var dates = columnIndices.Where(i => columns[i].Type == ColumnType.Date).ToList();
        if (dates.Count == 0)
            return ChartOutcome.Refused("The selection has no date column to put on the horizontal axis.");
        if (dates.Count > 1)
            return ChartOutcome.Refused(
                $"The selection has {dates.Count} date columns ({string.Join(", ", dates.Select(i => columns[i].Header))}); choose one for the horizontal axis.");

        var others = columnIndices.Where(i => columns[i].Type != ColumnType.Date).ToList();
        var notNumbers = others.Where(i => columns[i].Type != ColumnType.Number).ToList();
        if (notNumbers.Count > 0)
            return ChartOutcome.Refused(
                $"{string.Join(", ", notNumbers.Select(i => columns[i].Header))} cannot be plotted: not a number column.");
        if (others.Count == 0)
            return ChartOutcome.Refused("The selection has only the date column; select the figures to plot beside it.");

        var x = columns[dates[0]];
        var (top, count) = wholeTable ? (0, window.Count) : (ranges[0].TopRow, ranges[0].RowCount);
        var keys = Enumerable.Range(top, count)
            .Select(i => x.Value(window[i]))
            .OfType<DateOnly>()
            .ToHashSet();

        return new(new ChartDefinition(x.Name, others.Select(i => columns[i].Name).ToArray(), keys), null);
    }
}
