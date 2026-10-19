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
/// <param name="ColourSlots">How many colour slots the stylesheet defines: ten, unless a theme declares more (ADR-0256).</param>
public sealed record ChartMetrics(double Width, double Height, double DevicePixelRatio = 1, double LineHeight = 14, double Padding = 8, double PanelGap = 16, Func<string, double>? TextWidth = null, int ColourSlots = Appearance.DefaultColourSlots)
{
    /// <summary>The width estimate, defaults applied.</summary>
    public Func<string, double> EffectiveTextWidth => TextWidth ?? (s => s.Length * LineHeight * 0.6);
}

/// <summary>What the viewer narrowed a chart to — its Chart Filters — which narrow the drawing and the CSV, never the definition.</summary>
/// <param name="Hidden">The identities of the series hidden.</param>
/// <param name="From">The first position shown, or null for the data's first.</param>
/// <param name="To">The last position shown, or null for the data's last.</param>
/// <param name="Scale">The scale the viewer switched the axis to, or null for the declared one (ADR-0251). A Labels axis stays categorical, and an axis with a sampled series stays proportional.</param>
public sealed record ChartView(IReadOnlySet<string> Hidden, double? From = null, double? To = null, AxisScale? Scale = null)
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
/// <param name="Look">The look they chose.</param>
/// <param name="Kind">Its Series Kind.</param>
/// <param name="Interpolated">Whether it is the Consumer's interpolation (ADR-0257).</param>
/// <param name="Lines">Its lines in pixels, each unbroken.</param>
/// <param name="Markers">Its markers in pixels.</param>
/// <param name="Extrapolated">Its extrapolated ranges in pixels, drawn distinctly.</param>
/// <param name="Thinned">What each thinned pixel holds (ADR-0254).</param>
public sealed record SeriesPiece(string Id, string Name, AppearanceKeys Keys, SeriesLook Look, SeriesKind Kind, bool Interpolated,
    EquatableList<EquatableList<DrawnPoint>> Lines, EquatableList<DrawnMarker> Markers,
    EquatableList<(double From, double To)> Extrapolated, EquatableList<PixelSummary> Thinned);

/// <summary>A panel: its plot's box, its vertical axes and its series.</summary>
/// <param name="Name">The panel's name.</param>
/// <param name="Plot">The plot's box.</param>
/// <param name="Left">The left axis, or null when no series reads on it.</param>
/// <param name="Right">The right axis, or null.</param>
/// <param name="ZeroY">The height of zero in pixels, where the plot holds it.</param>
/// <param name="Series">The visible series, in the definition's order.</param>
/// <param name="Legend">Its legend: every series of the panel, hidden ones included (ADR-0271).</param>
public sealed record PanelScene(string Name, Box Plot, VerticalAxis? Left, VerticalAxis? Right, double? ZeroY, EquatableList<SeriesPiece> Series, EquatableList<LegendEntry> Legend);

/// <summary>
/// A chart laid out in CSS pixels as immutable pieces (ADR-0252): panels stacked down the chart, the
/// horizontal axis under the last, and the Notes. A change to one series changes only its piece.
/// </summary>
/// <param name="Panels">The panels, top to bottom.</param>
/// <param name="XTicks">The horizontal axis' ticks, each with its pixel position.</param>
/// <param name="XFrom">The first position shown.</param>
/// <param name="XTo">The last.</param>
/// <param name="Notes">The lines said under the chart: the Consumer's Notes, each Segment's method, and what the scene itself must say.</param>
/// <param name="Positions">The positions the pointer can be over, each with its pixel position, left to right (ADR-0255).</param>
public sealed record ChartScene(EquatableList<PanelScene> Panels, EquatableList<(double Position, double X, string? Label)> XTicks, double XFrom, double XTo, EquatableList<string> Notes,
    EquatableList<(double Position, double X)> Positions)
{
    /// <summary>
    /// Lays <paramref name="definition"/> out at <paramref name="metrics"/>, narrowed by
    /// <paramref name="view"/>. <paramref name="independentScales"/> lists the panels whose left and
    /// right axes the Consumer declared independent (ADR-0251); <paramref name="legend"/> says where
    /// each panel's legend sits (ADR-0271).
    /// </summary>
    public static ChartScene Build(ChartDefinition definition, ChartMetrics metrics, ChartView? view = null, AxisStyle? style = null,
        IReadOnlySet<string>? independentScales = null, LegendPlacement? legend = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(metrics);
        view ??= ChartView.All;
        style ??= AxisStyle.Default;
        legend ??= LegendPlacement.Default;
        var (looks, coloursRepeat) = Appearance.Of(definition, metrics.ColourSlots);
        var (axis, visible, from, to, plotLeft, plotWidth, devicePixels) = Frame(definition, metrics, view, legend);
        var categories = axis.EffectiveScale == AxisScale.Categories;
        var lh = metrics.LineHeight;
        var pad = metrics.Padding;
        var notes = new List<string>(definition.Notes);

        // What no longer matches is said, not ignored (ADR-0272).
        foreach (var key in view.Hidden.Where(k => definition.Series.All(s => s.Id != k)).Order(StringComparer.Ordinal))
            notes.Add($"\"{key}\" is hidden, but no series has that key.");

        // The horizontal map: proportional, or categories evenly spaced, centred in their slots.
        var slots = CategorySlots(axis, from, to);
        Func<double, double> map = categories
            ? x => slots.TryGetValue(x, out var i) ? plotLeft + (i + 0.5) * plotWidth / Math.Max(1, slots.Count) : double.NaN
            : x => plotLeft + (to == from ? 0.5 : (x - from) / (to - from)) * plotWidth;

        var positions = PositionsOf(visible, from, to, plotWidth, map, categories ? slots.Keys : null);
        if (visible.Count > 0 && positions.Count == 0)
            notes.Add($"No series has a point between {axis.Write(from)} and {axis.Write(to)}.");

        var points = visible.SelectMany(s => s.Points.Select(p => p.X)).ToHashSet();
        var xTicks = TickLayout.Horizontal(axis, style, from, to, points, map, metrics.EffectiveTextWidth, lh);
        var ticksBand = xTicks.Depth + lh / 2;

        // Every series' panel, hidden ones included, so a panel whose series are all hidden keeps
        // its legend and can show them again.
        var panelNames = definition.Series.Select(s => s.Panel).Distinct().ToList();
        var swatch = lh * 1.5;
        double EntryWidth(ChartSeries s) => swatch + pad / 2 + metrics.EffectiveTextWidth(s.Name) + pad;
        var rows = panelNames.Select(name => legend.Position is LegendPosition.Top or LegendPosition.Bottom
            ? Rows(definition.Series.Where(s => s.Panel == name).ToList(), EntryWidth, plotWidth)
            : []).ToList();
        double Band(int i) => rows[i].Count == 0 ? 0 : rows[i].Count * lh + pad / 2;

        var bands = Enumerable.Range(0, panelNames.Count).Sum(Band);
        var panelHeight = panelNames.Count == 0 ? 0
            : Math.Max(1, (metrics.Height - 2 * pad - ticksBand - bands - metrics.PanelGap * (panelNames.Count - 1)) / panelNames.Count);

        var panels = new List<PanelScene>();
        var y = pad;
        for (var pi = 0; pi < panelNames.Count; pi++)
        {
            var name = panelNames[pi];
            var last = pi == panelNames.Count - 1;
            double plotY, legendY;
            if (legend.Position == LegendPosition.Top)
            {
                legendY = y;
                plotY = y + Band(pi);
                y = plotY + panelHeight;
            }
            else
            {
                plotY = y;
                legendY = plotY + panelHeight + (last ? ticksBand : pad / 2);
                y = plotY + panelHeight + Band(pi);
            }
            y += metrics.PanelGap;

            var plot = new Box(plotLeft, plotY, plotWidth, panelHeight);
            var members = visible.Where(s => s.Panel == name).ToList();
            var drawings = members.ToDictionary(s => s.Id, s =>
                SeriesDrawing.Of(s, axis, from, to, devicePixels, Quotes(s, visible)));
            var (left, right, independent) = VerticalAxes(members, drawings, from, to, plot, independentScales?.Contains(name) == true);
            if (independent)
                notes.Add(panelNames.Count > 1 && name.Length > 0
                    ? $"{name}: the left and right axes put zero at different heights."
                    : "The left and right axes put zero at different heights.");
            var pieces = members.Select(s => Piece(s, looks[s.Id], Marked(s, visible), drawings[s.Id], s.Axis == AxisSide.Right ? right! : left!, map, plot, from, to, metrics.DevicePixelRatio, devicePixels)).ToEquatableList();
            var zero = left is { } l && l.Min <= 0 && l.Max >= 0 ? YOf(l, 0, plot) : right is { } r && r.Min <= 0 && r.Max >= 0 ? YOf(r, 0, plot) : (double?)null;
            var entries = Legend(definition.Series.Where(s => s.Panel == name).ToList(), rows[pi], legend, metrics, plot, legendY, looks, view.Hidden, EntryWidth);
            panels.Add(new PanelScene(name, plot, left, right, zero, pieces, entries));
        }

        foreach (var s in visible.Where(s => s.Segments.Count > 0))
            foreach (var g in s.Segments)
                notes.Add($"{s.Name}: {XLabel(axis, g.From)}–{XLabel(axis, g.To)} {g.Method}{(g.Extrapolated ? " (extrapolated)" : "")}");
        if (coloursRepeat)
            notes.Add("Colours repeat: there are more series than colours. The legend and the tooltip name each series.");

        return new ChartScene(panels.ToEquatableList(),
            xTicks.Ticks.Select(t => (t.Position, map(t.Position), t.Label)).ToEquatableList(), from, to, notes.ToEquatableList(),
            positions.ToEquatableList());
    }

    /// <summary>
    /// What both the scene and the CSV read: the axis as the viewer switched it, the visible series,
    /// the range shown, and the plot's span in pixels and in device pixels.
    /// </summary>
    internal static (ChartAxis Axis, List<ChartSeries> Visible, double From, double To, double PlotLeft, double PlotWidth, int DevicePixels) Frame(
        ChartDefinition definition, ChartMetrics metrics, ChartView view, LegendPlacement? legend = null)
    {
        var axis = AxisOf(definition, view);
        var visible = definition.Series.Where(s => !view.Hidden.Contains(s.Id)).ToList();
        var (from, to) = Domain(axis, visible, view);
        var pad = metrics.Padding;
        var column = legend?.Position is LegendPosition.Left or LegendPosition.Right && definition.Series.Count > 0
            ? definition.Series.Max(s => metrics.LineHeight * 1.5 + pad / 2 + metrics.EffectiveTextWidth(s.Name) + pad)
            : 0;
        var plotLeft = pad + (legend?.Position == LegendPosition.Left ? column : 0) + YAxisWidth(metrics);
        var plotRight = metrics.Width - pad - (legend?.Position == LegendPosition.Right ? column : 0)
            - (visible.Any(s => s.Axis == AxisSide.Right) ? YAxisWidth(metrics) : 0);
        var plotWidth = Math.Max(1, plotRight - plotLeft);
        return (axis, visible, from, to, plotLeft, plotWidth, (int)Math.Ceiling(plotWidth * metrics.DevicePixelRatio));
    }

    // The axis as the viewer switched it (ADR-0251). A Number or Date axis drawn as categories with
    // no named positions takes each position a series has, written as the axis writes it.
    private static ChartAxis AxisOf(ChartDefinition d, ChartView view)
    {
        var axis = d.Axis;
        if (view.Scale is not { } scale || scale == axis.EffectiveScale || axis.Values == AxisValues.Labels)
            return axis;
        if (scale == AxisScale.Categories && d.Series.Any(s => s.Sampler is not null))
            return axis;
        if (scale == AxisScale.Categories && axis.Named.Count == 0)
            axis = axis with
            {
                Named = d.Series.SelectMany(s => s.Points.Select(p => p.X)).Distinct().Order()
                    .Select(x => (x, XLabel(axis, x))).ToArray(),
            };
        return axis with { Scale = scale };
    }

    // The positions the pointer can be over: each point a visible series has in range, every slot of
    // a category axis, and for a sampled series one position per CSS pixel, which is as finely as
    // the pointer can tell them apart.
    private static List<(double Position, double X)> PositionsOf(List<ChartSeries> visible, double from, double to, double plotWidth, Func<double, double> map, IEnumerable<double>? categories)
    {
        var xs = categories is not null ? categories.ToHashSet()
            : visible.SelectMany(s => s.Points.Select(p => p.X)).Where(x => x >= from && x <= to).ToHashSet();
        if (categories is null)
            foreach (var f in visible.Select(s => s.Sampler).OfType<SeriesSampler>())
            {
                var n = Math.Max(1, (int)Math.Ceiling(plotWidth));
                for (var c = 0; c <= n; c++)
                {
                    var x = to == from ? from : from + (to - from) * c / n;
                    if (x >= f.From && x <= f.To) xs.Add(x);
                }
            }
        return xs.Select(x => (x, map(x))).OrderBy(p => p.Item2).ThenBy(p => p.x).ToList();
    }

    // Top and bottom legends wrap their entries into rows the plot's width.
    private static List<List<ChartSeries>> Rows(List<ChartSeries> series, Func<ChartSeries, double> width, double plotWidth)
    {
        var rows = new List<List<ChartSeries>>();
        var used = 0d;
        foreach (var s in series)
        {
            var w = width(s);
            if (rows.Count == 0 || used + w > plotWidth && rows[^1].Count > 0)
            {
                rows.Add([]);
                used = 0;
            }
            rows[^1].Add(s);
            used += w;
        }
        return rows;
    }

    private static EquatableList<LegendEntry> Legend(List<ChartSeries> series, List<List<ChartSeries>> rows, LegendPlacement legend, ChartMetrics metrics,
        Box plot, double legendY, IReadOnlyDictionary<string, SeriesLook> looks, IReadOnlySet<string> hidden, Func<ChartSeries, double> width)
    {
        var lh = metrics.LineHeight;
        var pad = metrics.Padding;
        var entries = new List<LegendEntry>();
        LegendEntry Entry(ChartSeries s, double x, double y) => new(s.Id, s.Name, looks[s.Id], hidden.Contains(s.Id), new Box(x, y, width(s), lh));
        double Offset(double space, double used) => legend.Align switch
        {
            LegendAlign.Centre => (space - used) / 2,
            LegendAlign.End => space - used,
            _ => 0,
        };
        switch (legend.Position)
        {
            case LegendPosition.Top or LegendPosition.Bottom:
                for (var r = 0; r < rows.Count; r++)
                {
                    var x = plot.X + Math.Max(0, Offset(plot.Width, rows[r].Sum(width)));
                    foreach (var s in rows[r])
                    {
                        entries.Add(Entry(s, x, legendY + r * lh));
                        x += width(s);
                    }
                }
                break;
            case LegendPosition.Left or LegendPosition.Right:
                var column = series.Count == 0 ? 0 : series.Max(width);
                var x0 = legend.Position == LegendPosition.Left ? pad : metrics.Width - pad - column;
                var y = plot.Y + Math.Max(0, Offset(plot.Height, series.Count * lh));
                foreach (var s in series)
                {
                    entries.Add(Entry(s, x0, y));
                    y += lh;
                }
                break;
        }
        return entries.ToEquatableList();
    }

    /// <summary>The quotes an Interpolated series is paired with.</summary>
    internal static IReadOnlyCollection<double> QuotesOf(ChartSeries s, List<ChartSeries> visible) => Quotes(s, visible);

    private static double YAxisWidth(ChartMetrics m) => m.EffectiveTextWidth("-0,000.00") + m.Padding;

    // The categories in range, each with its slot: evenly spaced, in order.
    private static Dictionary<double, int> CategorySlots(ChartAxis axis, double from, double to) =>
        (axis.Values == AxisValues.Labels ? Enumerable.Range(0, axis.Labels.Count).Select(i => (double)i) : axis.Named.Select(n => n.Position))
            .Where(x => x >= from && x <= to).Order().Select((x, i) => (x, i)).ToDictionary(t => t.x, t => t.i);

    private static (double From, double To) Domain(ChartAxis axis, List<ChartSeries> visible, ChartView view)
    {
        if (axis.EffectiveScale == AxisScale.Categories)
        {
            // Positions stay in their own units; only the map spaces them evenly.
            var (first, last) = axis.Values == AxisValues.Labels ? (0d, Math.Max(0, axis.Labels.Count - 1))
                : axis.Named.Count == 0 ? (0d, 0d) : (axis.Named.Min(n => n.Position), axis.Named.Max(n => n.Position));
            return (view.From ?? first, view.To ?? last);
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
            if (value == 0) value = 0; // never "-0"
            ticks.Add((value, YOf(v, value, plot), value.ToString("0.####", CultureInfo.InvariantCulture)));
        }
        return v with { Ticks = ticks.ToEquatableList() };
    }

    private static double YOf(VerticalAxis axis, double value, Box plot) =>
        plot.Y + plot.Height - (axis.Max == axis.Min ? 0.5 : (value - axis.Min) / (axis.Max - axis.Min)) * plot.Height;

    // A level's points are marked when its marker key asks for it, or when they are the quotes an
    // Interpolated series is drawn through, which the markers tell apart from the curve (ADR-0257).
    // A step's ends are always marked: filled and open say which end takes the value (ADR-0253).
    private static bool Marked(ChartSeries s, List<ChartSeries> visible) =>
        s.Kind != SeriesKind.Level || s.Keys?.Marker is not null || s.Interpolated
        || visible.Any(o => o.Id != s.Id && o.Interpolated && o.EffectiveKeys.Colour == s.EffectiveKeys.Colour);

    private static SeriesPiece Piece(ChartSeries s, SeriesLook look, bool marked, SeriesDrawing drawing, VerticalAxis axis, Func<double, double> map, Box plot, double from, double to, double dpr, int devicePixels)
    {
        var inRange = new SeriesDrawing(
            drawing.Lines.Select(l => (IReadOnlyList<DrawnPoint>)l.Where(p => p.X >= from && p.X <= to).ToList()).Where(l => l.Count > 1).ToList(),
            drawing.Markers.Where(m => m.X >= from && m.X <= to).ToList());
        var thinned = Thinning.Of(inRange, x => (map(x) - plot.X) * dpr, devicePixels);
        DrawnPoint Px(DrawnPoint p) => new(map(p.X), YOf(axis, p.Y, plot));
        return new SeriesPiece(s.Id, s.Name, s.EffectiveKeys, look, s.Kind, s.Interpolated,
            thinned.Drawing.Lines.Select(l => l.Select(Px).ToEquatableList()).ToEquatableList(),
            (marked ? thinned.Drawing.Markers : []).Select(m => m with { X = map(m.X), Y = YOf(axis, m.Y, plot) }).ToEquatableList(),
            s.Segments.Where(g => g.Extrapolated).Select(g => (map(Math.Max(g.From, from)), map(Math.Min(g.To, to)))).ToEquatableList(),
            thinned.Thinned.ToEquatableList());
    }

    private static string XLabel(ChartAxis axis, double x) =>
        axis.Format is null && axis.Values == AxisValues.Numbers && axis.Named.All(n => n.Position != x) ? TickLayout.Tenor(x) : axis.Write(x);
}
