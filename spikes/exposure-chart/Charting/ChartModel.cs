using ExGrid;
using ExGrid.Columns;

namespace ExposureChart.Charting;

/// <summary>Whether one panel draws everything on one value axis or splits its series.</summary>
public enum ChartLayout
{
    /// <summary>One panel per Header Group; a Change series reads against a right-hand axis.</summary>
    PanelPerGroup,

    /// <summary>Every series on one panel and one axis, as Excel's default chart would draw them.</summary>
    OneAxis,
}

/// <summary>
/// A point; a missing value is a gap, never a zero. It knows the data's dates on either side of
/// it, charted in this series or not: a line joins it to the previous point only when that
/// point is the previous date in the data, and a step's interval runs to the data's neighbour.
/// </summary>
public readonly record struct ChartPoint(DateOnly X, decimal? Y, DateOnly? Previous = null, DateOnly? Next = null);

/// <param name="Slot">The series' colour, by its leaf header: every "After" is one colour in every panel.</param>
/// <param name="Group">The Header Group the column stands under, or empty.</param>
/// <param name="Text">The column's own format, so a value reads as its cell does.</param>
public sealed record ChartSeries(
    string Column, string Name, string Group, string Leaf, int Slot, int GroupIndex,
    bool Secondary, IReadOnlyList<ChartPoint> Points, Func<decimal, string> Text, SeriesStyle Style);

public static class ChartSeriesExtensions
{
    /// <summary>The series' point at a date, or null where the series is not charted at it —
    /// which is not the same as a point whose value is missing.</summary>
    public static ChartPoint? At(this ChartSeries series, DateOnly date)
    {
        var points = series.Points;
        int lo = 0, hi = points.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var c = points[mid].X.CompareTo(date);
            if (c == 0) return points[mid];
            if (c < 0) lo = mid + 1; else hi = mid - 1;
        }
        return null;
    }
}

public sealed record ChartPanel(string Title, IReadOnlyList<ChartSeries> Series);

/// <summary>
/// Excel's Chart Filters: which series are shown and which stretch of the axis. It narrows what
/// the chart draws and what its CSV holds, and leaves the grid and the definition alone, so
/// showing a series again needs no new selection.
/// </summary>
public sealed record ChartView(IReadOnlySet<string> HiddenSeries, DateOnly? From, DateOnly? To)
{
    public static ChartView All { get; } = new(new HashSet<string>(), null, null);
}

/// <summary>What is drawn, resolved against the data as it is now.</summary>
/// <param name="AllSeries">Every series of the definition, shown or not, for the filter's list.</param>
/// <param name="Dates">Every date some shown series is charted at, in order: the axis' extent,
/// and one hover band each.</param>
/// <param name="AllDates">Every date of the definition the data holds, for the filter's range.</param>
/// <param name="Missing">Series dates of the definition the data no longer holds.</param>
public sealed record ChartModel(
    string XHeader,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<ChartPanel> Panels,
    IReadOnlyList<string> Leaves,
    int Missing,
    IReadOnlyList<ChartSeries> AllSeries,
    IReadOnlyList<DateOnly> AllDates)
{
    /// <summary>How the horizontal axis is drawn: the page's choice, applied after the build.</summary>
    public XAxisStyle XAxis { get; init; } = XAxisStyle.Default;
}

public static class ChartModelBuilder
{
    /// <param name="rows">The Consumer's data: every row it holds, in any order.</param>
    /// <param name="isChange">Which columns hold a change rather than a level — the Consumer's
    /// declaration, not a guess from the header's wording.</param>
    public static ChartModel Build<TRow>(
        ChartDefinition definition,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<HeaderGroup> groups,
        IReadOnlyList<TRow> rows,
        Func<string, bool> isChange,
        ChartLayout layout,
        ChartView view,
        Func<string, SeriesStyle>? styleOf = null)
    {
        var byName = columns.ToDictionary(c => c.Name);
        var x = byName[definition.XColumn];

        // A date axis plots by value, so the grid's row order plays no part. The axis value is
        // the row's identity here: one row per date.
        var byDate = new Dictionary<DateOnly, TRow>();
        foreach (var r in rows)
            if (x.Value(r) is DateOnly d) byDate[d] = r;
        var missing = definition.Series.Sum(s => s.RowKeys.Count(k => !byDate.ContainsKey(k)));
        var dataDates = byDate.Keys.Order().ToList();
        var position = dataDates.Select((d, i) => (d, i)).ToDictionary(p => p.d, p => p.i);
        DateOnly? Neighbour(DateOnly d, int step) =>
            position[d] + step is var j && j >= 0 && j < dataDates.Count ? dataDates[j] : null;

        var tierOne = groups.Where(g => g.Tier == 1).ToList();
        string GroupOf(string column) => tierOne.FirstOrDefault(g => g.Columns.Contains(column))?.Label ?? "";

        var leaves = new List<string>();
        var groupNames = new List<string>();
        var series = new List<ChartSeries>();
        foreach (var seriesDefinition in definition.Series)
        {
            var name = seriesDefinition.Column;
            var column = byName[name];
            var group = GroupOf(name);
            if (!leaves.Contains(column.Header)) leaves.Add(column.Header);
            if (!groupNames.Contains(group)) groupNames.Add(group);

            // Every tier's label above the leaf, top-down, then the leaf: "EPE(0) · Before".
            var path = groups.Where(g => g.Columns.Contains(name)).OrderByDescending(g => g.Tier)
                .Select(g => g.Label).Append(column.Header);
            var points = seriesDefinition.RowKeys.Where(byDate.ContainsKey).Order()
                .Select(d => new ChartPoint(d, ToDecimal(column.Value(byDate[d])), Neighbour(d, -1), Neighbour(d, 1))).ToList();
            var format = column.Format;
            series.Add(new ChartSeries(
                name, string.Join(" · ", path), group, column.Header,
                leaves.IndexOf(column.Header), groupNames.IndexOf(group),
                layout == ChartLayout.PanelPerGroup && isChange(name),
                points,
                format is null ? v => v.ToString("N2") : v => format(v),
                styleOf?.Invoke(name) ?? SeriesStyle.Default));
        }

        // The filter applies after the colours and dashes are given out, so that hiding "Before"
        // leaves "After" the colour it had.
        var from = view.From ?? DateOnly.MinValue;
        var to = view.To ?? DateOnly.MaxValue;
        var shown = series
            .Where(s => !view.HiddenSeries.Contains(s.Column))
            .Select(s => s with { Points = s.Points.Where(p => p.X >= from && p.X <= to).ToList() })
            .ToList();
        var dates = shown.SelectMany(s => s.Points).Select(p => p.X).Distinct().Order().ToList();
        var allDates = series.SelectMany(s => s.Points).Select(p => p.X).Distinct().Order().ToList();

        var panels = layout == ChartLayout.OneAxis
            ? shown.Count == 0 ? [] : [new ChartPanel("", shown)]
            : shown.GroupBy(s => s.Group).Select(g => new ChartPanel(g.Key, g.ToList())).ToList();
        return new ChartModel(x.Header, dates, panels, leaves, missing, series, allDates);
    }

    private static decimal? ToDecimal(object? value) => value switch
    {
        null => null,
        decimal d => d,
        double d when double.IsFinite(d) => (decimal)d,
        float f when float.IsFinite(f) => (decimal)f,
        int i => i,
        long l => l,
        _ => null,
    };
}
