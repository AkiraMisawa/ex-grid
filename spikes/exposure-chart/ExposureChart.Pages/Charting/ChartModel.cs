using System.Globalization;
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

    /// <summary>Every series on one panel, each against the axis the Consumer declared for it.</summary>
    OnePanel,
}

/// <summary>
/// What the horizontal axis measures. A position is a number either way: a date's day number, or
/// a tenor's years. The scale says how a position reads.
/// </summary>
/// <param name="Format">A position as the tooltip and the filter show it: "2031-12-20", "5Y".</param>
/// <param name="Raw">A position as a file holds it: "2031-12-20", "5".</param>
/// <param name="Origin">Where the axis starts when the data does not reach it: a tenor axis
/// starts at 0, where the first interval of a term structure begins.</param>
public sealed record XScale(bool IsDate, Func<double, string> Format, Func<double, string> Raw, double? Origin = null)
{
    /// <summary>Positions that are only an order — 1W, 1M, 1Y, or 25P, ATM, 25C — spaced evenly, as
    /// a market screen draws a term structure or a smile. The position is the label's index.</summary>
    public bool IsCategory { get; init; }

    public static XScale Categories(IReadOnlyList<string> labels) =>
        new(false, i => Label(labels, i), i => Label(labels, i)) { IsCategory = true };

    private static string Label(IReadOnlyList<string> labels, double i) =>
        (int)Math.Round(i) is var k && k >= 0 && k < labels.Count ? labels[k] : "";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static XScale Dates { get; } = new(true, Iso, Iso);

    /// <summary>Years from today, labelled as the market writes a tenor.</summary>
    public static XScale TenorYears { get; } = new(false, Tenor, v => v.ToString("0.####", Invariant), Origin: 0);

    public static double Of(DateOnly date) => date.DayNumber;
    public static DateOnly DateOf(double position) => DateOnly.FromDayNumber((int)position);

    private static string Iso(double position) => DateOf(position).ToString("yyyy-MM-dd", Invariant);

    public static string Tenor(double years)
    {
        if (years == 0) return "0";
        var months = years * 12;
        if (Math.Abs(months - Math.Round(months)) < 1e-9)
        {
            var m = (int)Math.Round(months);
            return m % 12 == 0 ? $"{m / 12}Y" : $"{m}M";
        }
        return years.ToString("0.##", Invariant) + "Y";
    }
}

/// <summary>
/// A point; a missing value is a gap, never a zero. It knows the data's positions on either side
/// of it, charted in this series or not: a line joins it to the previous point only when that
/// point is the data's previous position, and a step's interval runs to the data's neighbour.
/// </summary>
public readonly record struct ChartPoint(double X, decimal? Y, double? Previous = null, double? Next = null);

/// <param name="Id">The series' identity: a column's name, or a row's key and a group.</param>
/// <param name="Slot">The series' colour: the Consumer's where it says one, otherwise its leaf's,
/// so every "After" is one colour in every panel.</param>
/// <param name="Group">The Header Group the series stands under, or empty.</param>
/// <param name="Text">The column's own format, so a value reads as its cell does.</param>
/// <param name="FromZero">Whether its axis must reach zero: an exposure must, a forward rate need not.</param>
public sealed record ChartSeries(
    string Id, string Name, string Group, string Leaf, int Slot, int GroupIndex,
    bool Secondary, IReadOnlyList<ChartPoint> Points, Func<decimal, string> Text, SeriesStyle Style,
    string? Unit = null,
    int Shade = 0,
    bool FromZero = true);

public static class ChartSeriesExtensions
{
    /// <summary>The series' point at a position, or null where the series is not charted at it —
    /// which is not the same as a point whose value is missing.</summary>
    public static ChartPoint? At(this ChartSeries series, double x)
    {
        var points = series.Points;
        int lo = 0, hi = points.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var c = points[mid].X.CompareTo(x);
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
public sealed record ChartView(IReadOnlySet<string> HiddenSeries, double? From, double? To)
{
    public static ChartView All { get; } = new(new HashSet<string>(), null, null);
}

/// <summary>What is drawn, resolved against the data as it is now.</summary>
/// <param name="Xs">Every position some shown series is charted at, in order: one hover band each.</param>
/// <param name="AllSeries">Every series of the definition, shown or not, for the filter's list.</param>
/// <param name="AllXs">Every position of the definition the data holds, for the filter's range.</param>
/// <param name="Missing">Points of the definition the data no longer holds.</param>
public sealed record ChartModel(
    string XHeader,
    IReadOnlyList<double> Xs,
    IReadOnlyList<ChartPanel> Panels,
    IReadOnlyList<string> Leaves,
    int Missing,
    IReadOnlyList<ChartSeries> AllSeries,
    IReadOnlyList<double> AllXs)
{
    /// <summary>How the horizontal axis is drawn: the page's choice, applied after the build.</summary>
    public XAxisStyle XAxis { get; init; } = XAxisStyle.Default;

    public XScale Scale { get; init; } = XScale.Dates;

    /// <summary>The axis' extent: the shown positions, from the scale's origin where it has one.</summary>
    public (double From, double To) Domain => Xs.Count == 0 ? (0, 1)
        // A category takes a slot of its own: half a step of room at either end.
        : Scale.IsCategory ? (Xs[0] - 0.5, Xs[^1] + 0.5)
        : (Math.Min(Scale.Origin ?? Xs[0], Xs[0]), Xs[^1]);
}

/// <summary>How a chart is drawn, apart from what it is of: the page's and the viewer's choices.</summary>
public sealed record ChartLook(
    ChartLayout Layout,
    ChartView View,
    Func<string, bool> OnRight,
    Func<string, SeriesStyle>? StyleOf = null,
    Func<string, string?>? UnitOf = null,
    Func<string, int?>? SlotOf = null);

public static class ChartModelBuilder
{
    /// <summary>A chart whose series are columns, along a date column.</summary>
    /// <param name="rows">The Consumer's data: every row it holds, in any order.</param>
    public static ChartModel Build<TRow>(
        ChartDefinition definition,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<HeaderGroup> groups,
        IReadOnlyList<TRow> rows,
        ChartLook look)
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
        double? Neighbour(DateOnly d, int step) =>
            position[d] + step is var j && j >= 0 && j < dataDates.Count ? XScale.Of(dataDates[j]) : null;

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
                .Select(d => new ChartPoint(XScale.Of(d), ToDecimal(column.Value(byDate[d])), Neighbour(d, -1), Neighbour(d, 1))).ToList();
            series.Add(new ChartSeries(
                name, string.Join(" · ", path), group, column.Header,
                look.SlotOf?.Invoke(name) ?? leaves.IndexOf(column.Header),
                // The dash tells groups apart only where they share one axis.
                look.Layout == ChartLayout.OneAxis ? groupNames.IndexOf(group) : 0,
                look.Layout != ChartLayout.OneAxis && look.OnRight(name),
                points, TextOf(column), look.StyleOf?.Invoke(name) ?? SeriesStyle.Default, look.UnitOf?.Invoke(name)));
        }
        return Assemble(x.Header, series, leaves, missing, look) with { Scale = XScale.Dates };
    }

    /// <summary>
    /// A chart whose series are rows: each row and Header Group — a reference entity's spreads,
    /// its hazard rates — is one series, along the positions the Consumer declared for the
    /// group's columns. Excel's "series in rows".
    /// </summary>
    public static ChartModel BuildRows<TRow>(
        RowChartDefinition definition,
        RowChartSpec<TRow> spec,
        IReadOnlyList<GridColumn<TRow>> columns,
        IReadOnlyList<HeaderGroup> groups,
        IReadOnlyList<TRow> rows,
        ChartLook look)
    {
        var byName = columns.ToDictionary(c => c.Name);
        var byKey = new Dictionary<object, TRow>();
        foreach (var r in rows) byKey[spec.Key(r)] = r;

        var keys = definition.Series.Select(s => s.RowKey).Distinct().ToList();
        var groupNames = new List<string>();
        var series = new List<ChartSeries>();
        var missing = 0;
        foreach (var seriesDefinition in definition.Series)
        {
            if (!byKey.TryGetValue(seriesDefinition.RowKey, out var row)) { missing += seriesDefinition.Columns.Count; continue; }
            var group = groups.First(g => g.Tier == 1 && g.Label == seriesDefinition.Group);
            if (!groupNames.Contains(group.Label)) groupNames.Add(group.Label);

            // The group's columns along the axis, the curve's origin before the first: the data's
            // neighbours, whether this series takes them or not.
            var positions = group.Columns.Select(c => spec.XOf(c)).OfType<double>().Order().ToList();
            double? Neighbour(double at, int step)
            {
                var i = positions.IndexOf(at) + step;
                return i >= 0 && i < positions.Count ? positions[i] : step < 0 ? spec.Scale.Origin : null;
            }

            var first = byName[seriesDefinition.Columns[0]];
            var name = spec.Name(row);
            var points = seriesDefinition.Columns
                .Select(c => (Position: spec.XOf(c)!.Value, Column: byName[c]))
                .OrderBy(p => p.Position)
                .Select(p => new ChartPoint(p.Position, ToDecimal(p.Column.Value(row)), Neighbour(p.Position, -1), Neighbour(p.Position, 1)))
                .ToList();
            series.Add(new ChartSeries(
                $"{seriesDefinition.RowKey}|{group.Label}", $"{name} · {group.Label}", group.Label, name,
                // A colour per row, the same for its spreads and its hazard rates.
                keys.IndexOf(seriesDefinition.RowKey) % 7 + 1,
                look.Layout == ChartLayout.OneAxis ? groupNames.IndexOf(group.Label) : 0,
                look.Layout != ChartLayout.OneAxis && look.OnRight(first.Name),
                points, TextOf(first), look.StyleOf?.Invoke(first.Name) ?? SeriesStyle.Default, look.UnitOf?.Invoke(first.Name)));
        }
        var leaves = series.Select(s => s.Leaf).Distinct().ToList();
        return Assemble(spec.XHeader, series, leaves, missing, look) with { Scale = spec.Scale };
    }

    /// <summary>A model the page assembled itself, from series it built: the filter still applies.</summary>
    public static ChartModel Assemble(string xHeader, IReadOnlyList<ChartSeries> series, ChartLook look, XScale scale) =>
        Assemble(xHeader, [.. series], [.. series.Select(s => s.Leaf).Distinct()], 0, look) with { Scale = scale };

    // The filter applies after the colours and dashes are given out, so that hiding "Before"
    // leaves "After" the colour it had.
    private static ChartModel Assemble(string xHeader, List<ChartSeries> series, List<string> leaves, int missing, ChartLook look)
    {
        var view = look.View;
        var from = view.From ?? double.NegativeInfinity;
        var to = view.To ?? double.PositiveInfinity;
        var shown = series
            .Where(s => !view.HiddenSeries.Contains(s.Id))
            .Select(s => s with { Points = s.Points.Where(p => p.X >= from && p.X <= to).ToList() })
            .ToList();
        var xs = shown.SelectMany(s => s.Points).Select(p => p.X).Distinct().Order().ToList();
        var allXs = series.SelectMany(s => s.Points).Select(p => p.X).Distinct().Order().ToList();

        var panels = look.Layout != ChartLayout.PanelPerGroup
            ? shown.Count == 0 ? [] : [new ChartPanel("", shown)]
            : shown.GroupBy(s => s.Group).Select(g => new ChartPanel(g.Key, g.ToList())).ToList();
        return new ChartModel(xHeader, xs, panels, leaves, missing, series, allXs);
    }

    private static Func<decimal, string> TextOf<TRow>(GridColumn<TRow> column) =>
        column.Format is { } format ? v => format(v) : v => v.ToString("N2", CultureInfo.InvariantCulture);

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
