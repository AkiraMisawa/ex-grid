using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// The geometry a chart is laid out with, in CSS pixels, resolved in C# and never paired with a
/// stylesheet literal (ADR-0027/0028).
/// </summary>
/// <param name="Width">The chart's width.</param>
/// <param name="Height">The chart's height.</param>
/// <param name="DevicePixelRatio">Device pixels per CSS pixel, which Thinning and sampling count in (ADR-0090).</param>
/// <param name="LineHeight">A label's height.</param>
/// <param name="Padding">The space around the plots.</param>
/// <param name="PanelGap">The space between panels.</param>
/// <param name="TextWidth">A label's estimated width; text is never measured (ADR-0021). Null takes 0.6 of the line height a character.</param>
public sealed record ChartMetrics(double Width, double Height, double DevicePixelRatio = 1, double LineHeight = 14, double Padding = 8, double PanelGap = 16, Func<string, double>? TextWidth = null)
{
    /// <summary>The width estimate, defaults applied.</summary>
    public Func<string, double> EffectiveTextWidth => TextWidth ?? (s => s.Length * LineHeight * 0.6);
}

/// <summary>What the viewer narrowed a chart to — its Chart Filters — which narrow the drawing and the CSV, never the definition.</summary>
/// <param name="Hidden">The identities of the series hidden.</param>
/// <param name="From">The first position shown, or null for the data's first.</param>
/// <param name="To">The last position shown, or null for the data's last.</param>
public sealed record ChartView(IReadOnlySet<string> Hidden, double? From = null, double? To = null)
{
    /// <summary>Everything shown.</summary>
    public static ChartView All { get; } = new(new HashSet<string>());
}

/// <summary>A rectangle in CSS pixels.</summary>
/// <param name="X">Its left.</param>
/// <param name="Y">Its top.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
public readonly record struct Box(double X, double Y, double Width, double Height);

/// <summary>A vertical axis, laid out.</summary>
/// <param name="Side">Which side it stands on.</param>
/// <param name="Min">The value at the plot's foot.</param>
/// <param name="Max">The value at its top.</param>
/// <param name="Ticks">Each tick's value, height in pixels and label.</param>
/// <param name="Broken">Whether it does not reach zero, which a break mark at its foot says (ADR-0253).</param>
public sealed record VerticalAxis(AxisSide Side, double Min, double Max, EquatableList<(double Value, double Y, string Label)> Ticks, bool Broken);

/// <summary>One series as the scene draws it: everything its component needs, equal when nothing in it changed (ADR-0252).</summary>
/// <param name="Id">The series' identity.</param>
/// <param name="Name">Its name.</param>
/// <param name="Keys">The keys its look is chosen by (ADR-0256).</param>
/// <param name="Kind">Its Series Kind.</param>
/// <param name="Interpolated">Whether it is the Consumer's interpolation (ADR-0257).</param>
/// <param name="Lines">Its lines in pixels, each unbroken.</param>
/// <param name="Markers">Its markers in pixels.</param>
/// <param name="Extrapolated">Its extrapolated ranges in pixels, drawn distinctly.</param>
/// <param name="Thinned">What each thinned pixel holds (ADR-0254).</param>
public sealed record SeriesPiece(string Id, string Name, AppearanceKeys Keys, SeriesKind Kind, bool Interpolated,
    EquatableList<EquatableList<DrawnPoint>> Lines, EquatableList<DrawnMarker> Markers,
    EquatableList<(double From, double To)> Extrapolated, EquatableList<PixelSummary> Thinned);

/// <summary>A panel: its plot's box, its vertical axes and its series.</summary>
/// <param name="Name">The panel's name.</param>
/// <param name="Plot">The plot's box.</param>
/// <param name="Left">The left axis, or null when no series reads on it.</param>
/// <param name="Right">The right axis, or null.</param>
/// <param name="ZeroY">The height of zero in pixels, where the plot holds it.</param>
/// <param name="Series">The visible series, in the definition's order.</param>
public sealed record PanelScene(string Name, Box Plot, VerticalAxis? Left, VerticalAxis? Right, double? ZeroY, EquatableList<SeriesPiece> Series);

/// <summary>
/// A chart laid out in CSS pixels as immutable pieces (ADR-0252): panels stacked down the chart, the
/// horizontal axis under the last, and the Notes. A change to one series changes only its piece.
/// </summary>
/// <param name="Panels">The panels, top to bottom.</param>
/// <param name="XTicks">The horizontal axis' ticks, each with its pixel position.</param>
/// <param name="XFrom">The first position shown.</param>
/// <param name="XTo">The last.</param>
/// <param name="Notes">The lines said under the chart: the Consumer's Notes, each Segment's method, and what the scene itself must say.</param>
public sealed record ChartScene(EquatableList<PanelScene> Panels, EquatableList<(double Position, double X, string? Label)> XTicks, double XFrom, double XTo, EquatableList<string> Notes)
{
    /// <summary>
    /// Lays <paramref name="definition"/> out at <paramref name="metrics"/>, narrowed by
    /// <paramref name="view"/>. <paramref name="independentScales"/> lists the panels whose left and
    /// right axes the Consumer declared independent (ADR-0251).
    /// </summary>
    public static ChartScene Build(ChartDefinition definition, ChartMetrics metrics, ChartView? view = null, AxisStyle? style = null, IReadOnlySet<string>? independentScales = null)
    {
        view ??= ChartView.All;
        style ??= AxisStyle.Default;
        var axis = definition.Axis;
        var (visible, from, to, plotLeft, plotWidth, devicePixels) = Frame(definition, metrics, view);
        var categories = axis.EffectiveScale == AxisScale.Categories;
        var notes = new List<string>(definition.Notes);

        // The horizontal map: proportional, or categories evenly spaced, centred in their slots.
        var slots = CategorySlots(axis);
        Func<double, double> map = categories
            ? x => slots.TryGetValue(x, out var i) ? plotLeft + (i + 0.5) * plotWidth / Math.Max(1, slots.Count) : double.NaN
            : x => plotLeft + (to == from ? 0.5 : (x - from) / (to - from)) * plotWidth;

        var points = visible.SelectMany(s => s.Points.Select(p => p.X)).ToHashSet();
        var xTicks = TickLayout.Horizontal(axis, style, from, to, points, map, metrics.EffectiveTextWidth, metrics.LineHeight);

        var panelNames = visible.Select(s => s.Panel).Distinct().ToList();
        var top = metrics.Padding;
        var bottom = metrics.Height - metrics.Padding - xTicks.Depth - metrics.LineHeight / 2;
        var panelHeight = panelNames.Count == 0 ? 0 : Math.Max(1, (bottom - top - metrics.PanelGap * (panelNames.Count - 1)) / panelNames.Count);

        var panels = new List<PanelScene>();
        for (var pi = 0; pi < panelNames.Count; pi++)
        {
            var name = panelNames[pi];
            var plot = new Box(plotLeft, top + pi * (panelHeight + metrics.PanelGap), plotWidth, panelHeight);
            var members = visible.Where(s => s.Panel == name).ToList();
            var drawings = members.ToDictionary(s => s.Id, s =>
                SeriesDrawing.Of(s, axis, from, to, devicePixels, Quotes(s, visible)));
            var (left, right, independent) = VerticalAxes(members, drawings, from, to, plot, independentScales?.Contains(name) == true);
            if (independent)
                notes.Add(panelNames.Count > 1 && name.Length > 0
                    ? $"{name}: the left and right axes put zero at different heights."
                    : "The left and right axes put zero at different heights.");
            var pieces = members.Select(s => Piece(s, drawings[s.Id], s.Axis == AxisSide.Right ? right! : left!, map, plot, from, to, metrics.DevicePixelRatio, devicePixels)).ToEquatableList();
            var zero = left is { } l && l.Min <= 0 && l.Max >= 0 ? YOf(l, 0, plot) : right is { } r && r.Min <= 0 && r.Max >= 0 ? YOf(r, 0, plot) : (double?)null;
            panels.Add(new PanelScene(name, plot, left, right, zero, pieces));
        }

        foreach (var s in visible.Where(s => s.Segments.Count > 0))
            foreach (var g in s.Segments)
                notes.Add($"{s.Name}: {XLabel(axis, g.From)}–{XLabel(axis, g.To)} {g.Method}{(g.Extrapolated ? " (extrapolated)" : "")}");

        return new ChartScene(panels.ToEquatableList(),
            xTicks.Ticks.Select(t => (t.Position, map(t.Position), t.Label)).ToEquatableList(), from, to, notes.ToEquatableList());
    }

    /// <summary>What both the scene and the CSV read: the visible series, the range shown, and the plot's span in pixels and in device pixels.</summary>
    internal static (List<ChartSeries> Visible, double From, double To, double PlotLeft, double PlotWidth, int DevicePixels) Frame(ChartDefinition definition, ChartMetrics metrics, ChartView view)
    {
        var visible = definition.Series.Where(s => !view.Hidden.Contains(s.Id)).ToList();
        var (from, to) = Domain(definition, visible, view);
        var plotLeft = metrics.Padding + YAxisWidth(metrics);
        var plotRight = metrics.Width - metrics.Padding - (visible.Any(s => s.Axis == AxisSide.Right) ? YAxisWidth(metrics) : 0);
        var plotWidth = Math.Max(1, plotRight - plotLeft);
        return (visible, from, to, plotLeft, plotWidth, (int)Math.Ceiling(plotWidth * metrics.DevicePixelRatio));
    }

    /// <summary>The quotes an Interpolated series is paired with.</summary>
    internal static IReadOnlyCollection<double> QuotesOf(ChartSeries s, List<ChartSeries> visible) => Quotes(s, visible);

    private static double YAxisWidth(ChartMetrics m) => m.EffectiveTextWidth("-0,000.00") + m.Padding;

    private static Dictionary<double, int> CategorySlots(ChartAxis axis) => axis.Values == AxisValues.Labels
        ? Enumerable.Range(0, axis.Labels.Count).ToDictionary(i => (double)i, i => i)
        : axis.Named.Select((n, i) => (n.Position, i)).ToDictionary(t => t.Position, t => t.i);

    private static (double From, double To) Domain(ChartDefinition d, List<ChartSeries> visible, ChartView view)
    {
        if (d.Axis.EffectiveScale == AxisScale.Categories)
        {
            // Positions stay in their own units; only the map spaces them evenly.
            if (d.Axis.Values == AxisValues.Labels) return (0, Math.Max(0, d.Axis.Labels.Count - 1));
            return d.Axis.Named.Count == 0 ? (0, 0) : (d.Axis.Named.Min(n => n.Position), d.Axis.Named.Max(n => n.Position));
        }
        var xs = visible.SelectMany(s => s.Points.Select(p => p.X).Concat(s.Sampler is { } f ? [f.From, f.To] : [])).ToList();
        var lo = view.From ?? (xs.Count > 0 ? xs.Min() : 0);
        var hi = view.To ?? (xs.Count > 0 ? xs.Max() : 1);
        return (lo, hi);
    }

    // The quotes an Interpolated series is paired with: the positions of the series that share its colour key.
    private static IReadOnlyCollection<double> Quotes(ChartSeries s, List<ChartSeries> visible) =>
        s.Sampler is null ? [] : visible.Where(o => o.Id != s.Id && !o.Interpolated && o.EffectiveKeys.Colour == s.EffectiveKeys.Colour)
            .SelectMany(o => o.Points.Select(p => p.X)).ToList();

    private static (VerticalAxis? Left, VerticalAxis? Right, bool Independent) VerticalAxes(List<ChartSeries> members, Dictionary<string, SeriesDrawing> drawings, double from, double to, Box plot, bool declaredIndependent)
    {
        (double Lo, double Hi, double Step, bool Zero)? Range(AxisSide side)
        {
            var ss = members.Where(s => s.Axis == side).ToList();
            if (ss.Count == 0) return null;
            var ys = ss.SelectMany(s => drawings[s.Id].Lines.SelectMany(l => l).Where(p => p.X >= from && p.X <= to).Select(p => p.Y)
                .Concat(drawings[s.Id].Markers.Where(m => m.X >= from && m.X <= to).Select(m => m.Y))).ToList();
            var zero = ss.Any(s => s.ReachesZero);
            var lo = ys.Count > 0 ? ys.Min() : 0;
            var hi = ys.Count > 0 ? ys.Max() : 1;
            if (zero) { lo = Math.Min(lo, 0); hi = Math.Max(hi, 0); }
            var (nlo, nhi, step) = TickLayout.Nice(lo, hi);
            return (nlo, nhi, step, nlo <= 0 && nhi >= 0);
        }
        var l = Range(AxisSide.Left);
        var r = Range(AxisSide.Right);
        var independent = false;
        if (l is { } a && r is { } b)
        {
            if (declaredIndependent || !a.Zero || !b.Zero) independent = true;
            else
            {
                // Zero at the same height on both sides (ADR-0251): each axis is stretched until its
                // share below zero is the larger of the two.
                var share = Math.Max(Below(a.Lo, a.Hi), Below(b.Lo, b.Hi));
                l = Stretch(a, share);
                r = Stretch(b, share);
            }
        }
        return (l is { } x ? Axis(AxisSide.Left, x, plot) : null, r is { } y ? Axis(AxisSide.Right, y, plot) : null, independent);
    }

    private static double Below(double lo, double hi) => hi == lo ? 0 : -lo / (hi - lo);

    private static (double Lo, double Hi, double Step, bool Zero) Stretch((double Lo, double Hi, double Step, bool Zero) a, double share)
    {
        if (share >= 1) return (Math.Min(a.Lo, -a.Step), 0, a.Step, true);
        if (Below(a.Lo, a.Hi) < share) return (-share * a.Hi / (1 - share), a.Hi, a.Step, true);
        if (Below(a.Lo, a.Hi) > share) return (a.Lo, -a.Lo * (1 - share) / share, a.Step, true);
        return a;
    }

    private static VerticalAxis Axis(AxisSide side, (double Lo, double Hi, double Step, bool Zero) a, Box plot)
    {
        var v = new VerticalAxis(side, a.Lo, a.Hi, EquatableList<(double, double, string)>.Empty, Broken: !a.Zero);
        var first = Math.Ceiling(a.Lo / a.Step - 1e-9) * a.Step;
        var ticks = new List<(double, double, string)>();
        for (var t = first; t <= a.Hi + a.Step * 1e-9; t += a.Step)
        {
            var value = Math.Round(t, 12);
            ticks.Add((value, YOf(v, value, plot), value.ToString("0.####", CultureInfo.InvariantCulture)));
        }
        return v with { Ticks = ticks.ToEquatableList() };
    }

    private static double YOf(VerticalAxis axis, double value, Box plot) =>
        plot.Y + plot.Height - (axis.Max == axis.Min ? 0.5 : (value - axis.Min) / (axis.Max - axis.Min)) * plot.Height;

    private static SeriesPiece Piece(ChartSeries s, SeriesDrawing drawing, VerticalAxis axis, Func<double, double> map, Box plot, double from, double to, double dpr, int devicePixels)
    {
        var inRange = new SeriesDrawing(
            drawing.Lines.Select(l => (IReadOnlyList<DrawnPoint>)l.Where(p => p.X >= from && p.X <= to).ToList()).Where(l => l.Count > 1).ToList(),
            drawing.Markers.Where(m => m.X >= from && m.X <= to).ToList());
        var thinned = Thinning.Of(inRange, x => (map(x) - plot.X) * dpr, devicePixels);
        DrawnPoint Px(DrawnPoint p) => new(map(p.X), YOf(axis, p.Y, plot));
        return new SeriesPiece(s.Id, s.Name, s.EffectiveKeys, s.Kind, s.Interpolated,
            thinned.Drawing.Lines.Select(l => l.Select(Px).ToEquatableList()).ToEquatableList(),
            thinned.Drawing.Markers.Select(m => m with { X = map(m.X), Y = YOf(axis, m.Y, plot) }).ToEquatableList(),
            s.Segments.Where(g => g.Extrapolated).Select(g => (map(Math.Max(g.From, from)), map(Math.Min(g.To, to)))).ToEquatableList(),
            thinned.Thinned.ToEquatableList());
    }

    private static string XLabel(ChartAxis axis, double x) => axis.Values switch
    {
        AxisValues.Dates => DateOnly.FromDayNumber((int)x).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => axis.Named.FirstOrDefault(n => n.Position == x).Label ?? TickLayout.Tenor(x),
    };
}
