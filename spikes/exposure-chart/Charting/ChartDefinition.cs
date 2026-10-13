using ExGrid;
using ExGrid.Selection;

namespace ExposureChart.Charting;

/// <summary>One series: a column, and the rows it is charted at, named by their axis value.</summary>
public sealed record SeriesDefinition(string Column, IReadOnlySet<DateOnly> RowKeys);

/// <summary>
/// What a chart is of, in the data's own terms: the column that is the horizontal axis, and its
/// series — each a column with dates of its own, never "where it was painted". A sort, a column
/// drag or a recalculation leaves it meaning the same thing, which a definition by selection
/// indices would not (ADR-0011 drops a selection on reorder for exactly that reason). Each
/// series' rows are its own, as each Excel series has its own ranges: a series added later, or a
/// range selected over other rows, is charted at the dates it was given.
/// </summary>
public sealed record ChartDefinition(string XColumn, IReadOnlyList<SeriesDefinition> Series)
{
    public IReadOnlyList<string> SeriesColumns => Series.Select(s => s.Column).ToList();

    /// <summary>Every date some series is charted at.</summary>
    public IReadOnlySet<DateOnly> AllKeys => Series.SelectMany(s => s.RowKeys).ToHashSet();
}

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

        // One cell is Excel's "chart the table around it": every column that can be plotted. A
        // text column is left out here, where nobody chose it, and refused when somebody did.
        var wholeTable = ranges is [{ RowCount: 1, ColumnCount: 1 }];
        if (wholeTable)
            ranges = [new SelectionRange(0, 0, window.Count, columns.Count)];

        var columnIndices = ranges.SelectMany(r => Enumerable.Range(r.LeftColumn, r.ColumnCount)).Distinct().Order().ToList();
        var dates = columnIndices.Where(i => columns[i].Type == ColumnType.Date).ToList();
        if (dates.Count == 0)
            return ChartOutcome.Refused("The selection has no date column to put on the horizontal axis.");
        if (dates.Count > 1)
            return ChartOutcome.Refused(
                $"The selection has {dates.Count} date columns ({string.Join(", ", dates.Select(i => columns[i].Header))}); choose one for the horizontal axis.");

        var x = columns[dates[0]];
        var series = SeriesOf(ranges, columns, window, x, exclude: [], skipUnplottable: wholeTable);
        if (series.Refusal is not null) return ChartOutcome.Refused(series.Refusal);
        if (series.Series.Count == 0)
            return ChartOutcome.Refused("The selection has only the date column; select the figures to plot beside it.");
        return new(new ChartDefinition(x.Name, series.Series), null);
    }

    /// <summary>
    /// The selection's number columns as series, each at the rows of the ranges that cover it —
    /// so a column selected over two separate stretches of rows is charted at both.
    /// </summary>
    public static (IReadOnlyList<SeriesDefinition> Series, string? Refusal) SeriesOf<TRow>(
        IReadOnlyList<SelectionRange> ranges,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<TRow> window,
        GridColumn<TRow> x,
        IReadOnlyCollection<string> exclude,
        bool skipUnplottable = false)
    {
        var columnIndices = ranges.SelectMany(r => Enumerable.Range(r.LeftColumn, r.ColumnCount)).Distinct().Order().ToList();
        var notNumbers = columnIndices.Where(i => columns[i].Type is not (ColumnType.Number or ColumnType.Date)).ToList();
        if (notNumbers.Count > 0 && !skipUnplottable)
            return ([], $"{string.Join(", ", notNumbers.Select(i => columns[i].Header))} cannot be plotted: not a number column.");

        var series = new List<SeriesDefinition>();
        foreach (var i in columnIndices.Where(i => columns[i].Type == ColumnType.Number && !exclude.Contains(columns[i].Name)))
        {
            var keys = ranges
                .Where(r => i >= r.LeftColumn && i <= r.RightColumn)
                .SelectMany(r => Enumerable.Range(r.TopRow, r.RowCount))
                .Select(row => x.Value(window[row]))
                .OfType<DateOnly>()
                .ToHashSet();
            series.Add(new SeriesDefinition(columns[i].Name, keys));
        }
        return (series, null);
    }
}
