using ExGrid;
using ExGrid.Columns;
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
public sealed record ChartDefinition(string XColumn, IReadOnlyList<SeriesDefinition> Series) : ChartSpec
{
    public override int SeriesCount => Series.Count;
    public override string Describe() => $"x: {XColumn} · {Series.Count} series · {AllKeys.Count} dates";

    public IReadOnlyList<string> SeriesColumns => Series.Select(s => s.Column).ToList();

    /// <summary>Every date some series is charted at.</summary>
    public IReadOnlySet<DateOnly> AllKeys => Series.SelectMany(s => s.RowKeys).ToHashSet();
}

/// <summary>What a chart is of: series in columns along a date column, or series in rows.</summary>
public abstract record ChartSpec
{
    public abstract int SeriesCount { get; }
    public abstract string Describe();
}

/// <summary>
/// A chart whose model the page builds itself — a smile across deltas, a term structure across
/// expiries, compared at several dates — when the series are not columns or rows of the grid.
/// </summary>
public sealed record PrebuiltChart(string Description, int Count, Func<ChartLook, ChartModel> Build) : ChartSpec
{
    public override int SeriesCount => Count;
    public override string Describe() => Description;
}

/// <summary>One series in rows: a row, by its key, across some of one Header Group's columns.</summary>
public sealed record RowSeriesDefinition(object RowKey, string Group, IReadOnlyList<string> Columns);

/// <summary>A chart of series in rows — Excel's "series in rows" — each a row and a group.</summary>
public sealed record RowChartDefinition(IReadOnlyList<RowSeriesDefinition> Series) : ChartSpec
{
    public override int SeriesCount => Series.Count;
    public override string Describe() =>
        $"{Series.Select(s => s.RowKey).Distinct().Count()} rows · {Series.Count} series";
}

/// <summary>
/// The Consumer's declarations for series in rows. The positions are declared, never read off a
/// header: "6m" is a header's text, 0.5 years is what the Consumer says it stands for.
/// </summary>
/// <param name="Key">A row's identity, so a series follows its row through a sort.</param>
/// <param name="Name">A row's name in the legend and the tooltip: ticker, seniority, currency.</param>
/// <param name="XOf">A column's position along the axis, or null for a column that has none.</param>
public sealed record RowChartSpec<TRow>(
    Func<TRow, object> Key,
    Func<TRow, string> Name,
    Func<string, double?> XOf,
    XScale Scale,
    string XHeader);

/// <summary>A chart that could be made, or the sentence that says why not.</summary>
public sealed record ChartOutcome(ChartSpec? Definition, string? Refusal)
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

    /// <summary>
    /// A selection read as series in rows: each selected row, across the positioned columns the
    /// selection covered, one series per Header Group they fall in. A selection that covers no
    /// positioned column, or one only, stands for its rows' whole curves — a curve needs two
    /// points. A number column with no position (a recovery rate) is refused by name, not
    /// dropped: it was chosen, and it cannot be drawn on this axis.
    /// </summary>
    public static ChartOutcome FromRowSelection<TRow>(
        IReadOnlyList<SelectionRange> ranges,
        int selectionVersion,
        int currentVersion,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<HeaderGroup> groups,
        IReadOnlyList<TRow> window,
        RowChartSpec<TRow> spec,
        IReadOnlyCollection<RowSeriesDefinition>? existing = null)
    {
        if (ranges.Count == 0)
            return ChartOutcome.Refused("Nothing is selected.");
        if (selectionVersion != currentVersion)
            return ChartOutcome.Refused("The rows were reordered after the selection was made. Select again.");

        var covered = ranges.SelectMany(r => Enumerable.Range(r.LeftColumn, r.ColumnCount)).Distinct().Order().Select(i => columns[i]).ToList();
        var unpositioned = covered.Where(c => c.Type == ColumnType.Number && spec.XOf(c.Name) is null).ToList();
        if (unpositioned.Count > 0)
            return ChartOutcome.Refused(
                $"{string.Join(", ", unpositioned.Select(c => c.Header))} has no position on the {spec.XHeader} axis, so it cannot be drawn here.");

        var positioned = covered.Where(c => spec.XOf(c.Name) is not null).Select(c => c.Name).ToList();
        var groupsOfCurve = groups.Where(g => g.Tier == 1 && g.Columns.Any(c => spec.XOf(c) is not null)).ToList();
        var series = new List<RowSeriesDefinition>();
        var rowsCovered = ranges.SelectMany(r => Enumerable.Range(r.TopRow, r.RowCount)).Distinct().Order();
        foreach (var rowIndex in rowsCovered)
        {
            var key = spec.Key(window[rowIndex]);
            foreach (var group in groupsOfCurve)
            {
                var inGroup = positioned.Where(group.Columns.Contains).ToList();
                if (positioned.Count > 1 && inGroup.Count == 0) continue;
                var cols = inGroup.Count > 1 ? inGroup : group.Columns.Where(c => spec.XOf(c) is not null).ToList();
                if (existing?.Any(e => Equals(e.RowKey, key) && e.Group == group.Label) == true) continue;
                series.Add(new RowSeriesDefinition(key, group.Label, cols));
            }
        }
        if (series.Count == 0)
            return ChartOutcome.Refused(existing is null ? "The selection makes no curve." : "Every curve in the selection is already in the chart.");
        return new(new RowChartDefinition(series), null);
    }
}
