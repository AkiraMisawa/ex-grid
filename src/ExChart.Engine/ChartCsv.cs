using System.Globalization;
using System.Text;

namespace ExChart.Engine;

/// <summary>
/// What a chart shows, as CSV (ticket 07): the series and range left after the Chart Filters, every
/// point before Thinning (ADR-0254), each value with the digits it was handed in with. Invariant,
/// unformatted, a byte-order mark first so Excel reads it as UTF-8; a missing value is an empty field,
/// never a zero.
/// </summary>
public static class ChartCsv
{
    private const string Bom = "﻿";

    /// <summary>
    /// A row per point: Series, the axis' title, Value, Kind (quoted, interpolated, extrapolated; a
    /// Price Range's columns are "price range", an Interval's bounds "interval") and
    /// Method (the Segment's). Its empty Value is only ever a missing one.
    /// </summary>
    public static string Long(ChartDefinition definition, ChartMetrics metrics, ChartView? view = null)
    {
        var sb = new StringBuilder(Bom);
        var named = definition.Series.Any(s => s.Kind == SeriesKind.Observation);
        Row(sb, named ? ["Series", "Point", Title(definition.Axis), "Value", "Kind", "Method"] : ["Series", Title(definition.Axis), "Value", "Kind", "Method"]);
        foreach (var (s, points) in Shown(definition, metrics, view ?? ChartView.All))
        {
            // An Observation's bounds follow each of its points, one a row (ADR-0304).
            if (s.Kind == SeriesKind.Observation)
            {
                foreach (var (p, i) in Observed(s, points))
                {
                    Row(sb, [s.Name, p.Name ?? "", X(definition.Axis, p.X), p.Y?.ToInvariantString() ?? "", "observed", ""]);
                    foreach (var (iv, which) in new[] { (s.Interval, s.XInterval is null ? "" : "y "), (s.XInterval, "x ") })
                        if (iv is not null)
                        {
                            Row(sb, [$"{s.Name} {which}lower ({iv.Meaning})", p.Name ?? "", X(definition.Axis, p.X), iv.Bounds[i].Lower?.ToInvariantString() ?? "", "interval", ""]);
                            Row(sb, [$"{s.Name} {which}upper ({iv.Meaning})", p.Name ?? "", X(definition.Axis, p.X), iv.Bounds[i].Upper?.ToInvariantString() ?? "", "interval", ""]);
                        }
                }
                continue;
            }
            if (Extra(s, points) is { Count: > 0 } extra)
            {
                // A Price Range's every row, and an Interval's bounds named by their meaning (ADR-0300, ADR-0303).
                var kind = s.Kind == SeriesKind.PriceRange ? "price range" : "interval";
                if (s.Kind != SeriesKind.PriceRange)
                    foreach (var p in points)
                    {
                        var (pk, pm) = KindOf(s, p.X);
                        Row(sb, [s.Name, X(definition.Axis, p.X), p.Y?.ToInvariantString() ?? "", pk, pm]);
                    }
                foreach (var (header, values) in extra)
                    foreach (var (x, v) in values.OrderBy(e => e.Key))
                        Row(sb, [header, X(definition.Axis, x), v, kind, ""]);
                continue;
            }
            foreach (var p in points)
            {
                var (kind, method) = KindOf(s, p.X);
                string[] tail = [X(definition.Axis, p.X), p.Y?.ToInvariantString() ?? "", kind, method];
                Row(sb, named ? [s.Name, p.Name ?? "", .. tail] : [s.Name, .. tail]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// A row per position, a column per series — an Interpolated series' column says so, and a
    /// series with extrapolated points has a column of its own for them. An empty cell cannot tell a
    /// missing value from a position the series has no point at; the long form can.
    /// </summary>
    public static string Wide(ChartDefinition definition, ChartMetrics metrics, ChartView? view = null)
    {
        var table = WideTable(definition, metrics, view);
        var sb = new StringBuilder(Bom);
        Row(sb, table.Header);
        foreach (var row in table.Rows)
            Row(sb, row);
        return sb.ToString();
    }

    /// <summary>
    /// The wide form's header and rows, unquoted: what "View as table" shows (ADR-0275), the same
    /// content as <see cref="Wide"/>.
    /// </summary>
    public static ChartTable WideTable(ChartDefinition definition, ChartMetrics metrics, ChartView? view = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(metrics);
        var shown = Shown(definition, metrics, view ?? ChartView.All);
        // A scatter's points share positions across, so its table is a row per point, never a column
        // per series keyed by position, which would keep one point of each position (ADR-0290).
        if (definition.Series.Any(s => s.Kind == SeriesKind.Observation))
            return Observations(definition, shown);
        var columns = new List<(string Header, Dictionary<double, string> Values)>();
        foreach (var (s, points) in shown)
        {
            if (s.Kind == SeriesKind.PriceRange)
            {
                columns.AddRange(Extra(s, points));
                continue;
            }
            var main = new Dictionary<double, string>();
            var extra = new Dictionary<double, string>();
            foreach (var p in points)
                (KindOf(s, p.X).Kind == "extrapolated" ? extra : main)[p.X] = p.Y?.ToInvariantString() ?? "";
            var name = s.Interpolated ? $"{s.Name} (interpolated)" : s.Name;
            columns.Add((name, main));
            if (extra.Count > 0) columns.Add(($"{s.Name} (extrapolated)", extra));
            columns.AddRange(Extra(s, points));
        }
        var rows = columns.SelectMany(c => c.Values.Keys).Distinct().Order()
            .Select(x => (IReadOnlyList<string>)[X(definition.Axis, x), .. columns.Select(c => c.Values.GetValueOrDefault(x, ""))]).ToList();
        return new ChartTable([Title(definition.Axis), .. columns.Select(c => c.Header)], rows);
    }

    private static List<(ChartSeries Series, IReadOnlyList<ChartPoint> Points)> Shown(ChartDefinition d, ChartMetrics metrics, ChartView view)
    {
        var (axis, visible, from, to, _, _, devicePixels) = ChartScene.Frame(d, metrics, view);
        return visible.Select(s => (s, (IReadOnlyList<ChartPoint>)(s.Sampler is { } f
            ? SeriesDrawing.Sample(f, s.Segments, from, to, devicePixels, ChartScene.QuotesOf(s, visible), axis.EffectiveScale == AxisScale.Logarithmic)
            : s.Points.Where(p => p.X >= from && p.X <= to).ToList()))).ToList();
    }

    // A scatter's table, a row per point, with a lower and an upper column for each quantity that has
    // an interval: named by its meaning when every series means the same, else with the meaning in a
    // column of its own (ADR-0304).
    private static ChartTable Observations(ChartDefinition d, List<(ChartSeries Series, IReadOnlyList<ChartPoint> Points)> shown)
    {
        var header = new List<string> { "Series", "Point", Title(d.Axis), "Value" };
        var quantities = new List<(Func<ChartSeries, SeriesInterval?> Of, bool Plain)>();
        foreach (var (of, name) in new (Func<ChartSeries, SeriesInterval?>, string)[] { (s => s.Interval, "Value"), (s => s.XInterval, Title(d.Axis)) })
        {
            var meanings = shown.Where(t => t.Series.Kind == SeriesKind.Observation).Select(t => of(t.Series)?.Meaning).Where(m => m is not null).Distinct().ToList();
            if (meanings.Count == 0) continue;
            var plain = meanings.Count > 1 || shown.Any(t => t.Series.Kind == SeriesKind.Observation && of(t.Series) is null);
            header.AddRange(plain ? [$"{name} lower", $"{name} upper", $"{name} interval"] : [$"{name} lower ({meanings[0]})", $"{name} upper ({meanings[0]})"]);
            quantities.Add((of, plain));
        }
        // A colour or a size read from a column is a column of the file: files carry every value (ADR-0320).
        var scales = shown.Where(t => t.Series.Kind == SeriesKind.Observation)
            .SelectMany(t => new[] { t.Series.ColourBy?.Title, t.Series.SizeBy?.Title }).OfType<string>().Distinct().ToList();
        header.AddRange(scales);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var (s, points) in shown)
            foreach (var (p, i) in Observed(s, points))
            {
                var row = new List<string> { s.Name, p.Name ?? "", X(d.Axis, p.X), p.Y?.ToInvariantString() ?? "" };
                foreach (var (of, plain) in quantities)
                {
                    var iv = s.Kind == SeriesKind.Observation ? of(s) : null;
                    row.Add(iv?.Bounds[i].Lower?.ToInvariantString() ?? "");
                    row.Add(iv?.Bounds[i].Upper?.ToInvariantString() ?? "");
                    if (plain) row.Add(iv?.Meaning ?? "");
                }
                foreach (var title in scales)
                {
                    var values = s.ColourBy?.Title == title ? s.ColourBy.Values : s.SizeBy?.Title == title ? s.SizeBy.Values : null;
                    row.Add(values?[i]?.ToInvariantString() ?? "");
                }
                rows.Add(row);
            }
        // A node series lists its edges after its nodes (ADR-0321).
        foreach (var (s, _) in shown.Where(t => t.Series.Edges.Count > 0))
            foreach (var e in s.Edges)
            {
                var row = new List<string> { s.Name, $"edge {e.From} → {e.To}" };
                while (row.Count < header.Count) row.Add("");
                rows.Add(row);
            }
        return new ChartTable(header, rows);
    }

    // The points shown with their rows: an Observation's every point in range, a sampled series' samples.
    private static IEnumerable<(ChartPoint Point, int Row)> Observed(ChartSeries s, IReadOnlyList<ChartPoint> shown)
    {
        if (s.Kind != SeriesKind.Observation || s.Sampler is not null)
        {
            for (var i = 0; i < shown.Count; i++) yield return (shown[i], -1);
            yield break;
        }
        var kept = shown.ToHashSet();
        for (var i = 0; i < s.Points.Count; i++)
            if (kept.Contains(s.Points[i])) yield return (s.Points[i], i);
    }

    // The columns a series carries beyond its points: a Price Range's open, high, low, close and
    // volume, and an Interval's lower and upper bounds, each named by its meaning.
    private static List<(string Header, Dictionary<double, string> Values)> Extra(ChartSeries s, IReadOnlyList<ChartPoint> points)
    {
        var xs = points.Select(p => p.X).ToHashSet();
        string V(ChartValue? v) => v?.ToInvariantString() ?? "";
        List<(string, Dictionary<double, string>)> Of(params (string Name, Func<double, string> Value)[] cols) =>
            cols.Select(c => ($"{s.Name} {c.Name}", xs.ToDictionary(x => x, c.Value))).ToList();
        if (s.Kind == SeriesKind.PriceRange)
        {
            var bars = s.Bars.Where(b => xs.Contains(b.X)).ToDictionary(b => b.X);
            var cols = new List<(string, Func<double, string>)>();
            if (PriceRanges.HasOpen(s)) cols.Add(("open", x => V(bars[x].Open)));
            cols.AddRange([("high", x => V(bars[x].High)), ("low", x => V(bars[x].Low)), ("close", x => V(bars[x].Close))]);
            if (s.Bars.Any(b => b.Volume is not null)) cols.Add(("volume", x => V(bars[x].Volume)));
            return Of([.. cols]);
        }
        if (s.Interval is { } iv)
        {
            var bounds = iv.Bounds.Where(b => xs.Contains(b.X)).ToDictionary(b => b.X);
            return Of(($"lower ({iv.Meaning})", x => bounds.TryGetValue(x, out var b) ? V(b.Lower) : ""),
                ($"upper ({iv.Meaning})", x => bounds.TryGetValue(x, out var b) ? V(b.Upper) : ""));
        }
        return [];
    }

    private static (string Kind, string Method) KindOf(ChartSeries s, double x)
    {
        var g = s.Segments.FirstOrDefault(g => g.From <= x && x < g.To) ?? s.Segments.LastOrDefault(g => g.To == x);
        if (s.Kind == SeriesKind.Observation) return ("observed", "");
        if (g?.Extrapolated == true) return ("extrapolated", g.Method);
        return (s.Interpolated ? "interpolated" : "quoted", g?.Method ?? "");
    }

    internal static string Title(ChartAxis axis) => axis.Title.Length > 0 ? axis.Title : axis.Values == AxisValues.Dates ? "Date" : "X";

    private static string X(ChartAxis axis, double x) => axis.Values switch
    {
        AxisValues.Dates => DateOnly.FromDayNumber((int)x).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        AxisValues.Labels => axis.Labels[(int)x],
        _ => x.ToString("R", CultureInfo.InvariantCulture),
    };

    private static void Row(StringBuilder sb, IEnumerable<string> fields)
    {
        sb.AppendJoin(',', fields.Select(Field));
        sb.Append("\r\n");
    }

    private static string Field(string f) =>
        f.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + f.Replace("\"", "\"\"") + "\"" : f;
}

/// <summary>A table of what the chart shows: a header and rows of invariant text.</summary>
/// <param name="Header">The column headers: the axis' title, then a column per series.</param>
/// <param name="Rows">A row per position.</param>
public sealed record ChartTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows);
