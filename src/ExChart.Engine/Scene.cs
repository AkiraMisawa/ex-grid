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
/// <param name="YFrom">The lowest value the left axis shows, or null for the data's (ADR-0290).</param>
/// <param name="YTo">The highest value the left axis shows, or null for the data's.</param>
/// <param name="YScale">The vertical axes' scale: <see cref="AxisScale.Logarithmic"/>, or null or <see cref="AxisScale.Proportional"/> for proportional (ADR-0290).</param>
public sealed record ChartView(IReadOnlySet<string> Hidden, double? From = null, double? To = null, AxisScale? Scale = null,
    double? YFrom = null, double? YTo = null, AxisScale? YScale = null)
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
public sealed record VerticalAxis(AxisSide Side, double Min, double Max, EquatableList<(double Value, double Y, string Label)> Ticks, bool Broken)
{
    /// <summary>Whether it is logarithmic, its Min and Max then both above zero (ADR-0290).</summary>
    public bool Logarithmic { get; init; }

    /// <summary>The height in pixels at which <paramref name="value"/> stands on <paramref name="plot"/>.</summary>
    public double YOf(double value, Box plot) =>
        plot.Y + plot.Height - (Max == Min ? 0.5 : Logarithmic ? Math.Log(value / Min) / Math.Log(Max / Min) : (value - Min) / (Max - Min)) * plot.Height;

    /// <summary>The value that stands at height <paramref name="y"/> on <paramref name="plot"/>.</summary>
    public double ValueAt(double y, Box plot)
    {
        var share = plot.Height == 0 ? 0.5 : (plot.Y + plot.Height - y) / plot.Height;
        return Logarithmic ? Min * Math.Pow(Max / Min, share) : Min + share * (Max - Min);
    }
}

/// <summary>A marker of an Observation series as the pointer finds it (ADR-0290): which series and point, where it stands, and how many points it stands for.</summary>
/// <param name="SeriesId">The series' identity.</param>
/// <param name="Panel">The panel it stands in.</param>
/// <param name="X">Its centre across, in pixels.</param>
/// <param name="Y">Its centre down, in pixels.</param>
/// <param name="Point">The index of the series' point it stands for, the first where it stands for more than one.</param>
/// <param name="Count">How many of the series' points it stands for.</param>
public readonly record struct ChartMark(string SeriesId, int Panel, double X, double Y, int Point, int Count);

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
    EquatableList<(double From, double To)> Extrapolated, EquatableList<PixelSummary> Thinned)
{
    /// <summary>A Price Range's candles in pixels (ADR-0300).</summary>
    public EquatableList<PlacedCandle> Candles { get; init; } = EquatableList<PlacedCandle>.Empty;

    /// <summary>The period its candles stand for: a row each, or the market period they were merged into (ADR-0302).</summary>
    public CandlePeriod Period { get; init; } = CandlePeriod.Row;

    /// <summary>A volume panel's bars in pixels (ADR-0300).</summary>
    public EquatableList<PlacedVolume> Volumes { get; init; } = EquatableList<PlacedVolume>.Empty;

    /// <summary>Its Interval's whiskers in pixels: the position, and the heights of the lower and the upper bound (ADR-0303).</summary>
    public EquatableList<DrawnWhisker> Whiskers { get; init; } = EquatableList<DrawnWhisker>.Empty;

    /// <summary>Its Interval's bands in pixels, each a run of neighbours.</summary>
    public EquatableList<EquatableList<DrawnWhisker>> Bands { get; init; } = EquatableList<EquatableList<DrawnWhisker>>.Empty;

    /// <summary>What its Interval means, or null when it has none.</summary>
    public string? IntervalMeaning { get; init; }

    /// <summary>Whether its markers' colour bands diverge from a centre (ADR-0320, ADR-0310).</summary>
    public bool ColourDiverging { get; init; }

    /// <summary>Its edges in pixels, cut to the plot, drawn under its nodes (ADR-0321).</summary>
    public EquatableList<DrawnEdge> Edges { get; init; } = EquatableList<DrawnEdge>.Empty;
}

/// <summary>A panel: its plot's box, its vertical axes and its series.</summary>
/// <param name="Name">The panel's name.</param>
/// <param name="Plot">The plot's box.</param>
/// <param name="Left">The left axis, or null when no series reads on it.</param>
/// <param name="Right">The right axis, or null.</param>
/// <param name="ZeroY">The height of zero in pixels, where the plot holds it.</param>
/// <param name="Series">The visible series, in the definition's order.</param>
/// <param name="Legend">Its legend: every series of the panel, hidden ones included (ADR-0271).</param>
public sealed record PanelScene(string Name, Box Plot, VerticalAxis? Left, VerticalAxis? Right, double? ZeroY, EquatableList<SeriesPiece> Series, EquatableList<LegendEntry> Legend)
{
    /// <summary>The Price Range whose volume this panel draws, or null for a panel of the Consumer's (ADR-0300).</summary>
    public string? VolumeOf { get; init; }
}

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
    /// <summary>Whether a visible series is an Observation: the pointer then finds the nearest mark, and a drag narrows both axes (ADR-0290).</summary>
    public bool Scatter { get; init; }

    /// <summary>The Observation series' marks, in drawing order, which the pointer finds the nearest of (ADR-0290).</summary>
    public EquatableList<ChartMark> Marks { get; init; } = EquatableList<ChartMark>.Empty;

    /// <summary>Whether the axis across is logarithmic as drawn (ADR-0290).</summary>
    public bool LogarithmicX { get; init; }

    /// <summary>The colour bars of the visible series that take their colour from a column, beside the plot (ADR-0320).</summary>
    public EquatableList<ColourKey> ColourKeys { get; init; } = EquatableList<ColourKey>.Empty;

    /// <summary>The size keys of the visible series that take their size from a column, beside the plot (ADR-0320).</summary>
    public EquatableList<SizeKey> SizeKeys { get; init; } = EquatableList<SizeKey>.Empty;

    /// <summary>The position across that stands at <paramref name="x"/> pixels, on a proportional or logarithmic axis.</summary>
    public double PositionAt(double x)
    {
        if (Panels.Count == 0) return XFrom;
        var plot = Panels[0].Plot;
        var share = plot.Width == 0 ? 0.5 : (x - plot.X) / plot.Width;
        return LogarithmicX ? XFrom * Math.Pow(XTo / XFrom, share) : XFrom + share * (XTo - XFrom);
    }

    /// <summary>
    /// The horizontal axis' title as drawn: the axis' own, and what the scene must add to it — that the
    /// sessions are packed (ADR-0301), or the period candles were merged into (ADR-0302).
    /// </summary>
    public string XTitle { get; init; } = "";

    // A volume panel's height against a price panel's.
    private const double VolumeShare = 0.35;

    // A candle's body against the width it stands for.
    private const double BodyShare = 0.7;

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
        var logX = axis.EffectiveScale == AxisScale.Logarithmic;
        var scatter = visible.Any(s => s.Kind == SeriesKind.Observation);
        var lh = metrics.LineHeight;
        var pad = metrics.Padding;
        var notes = new List<string>(definition.Notes);

        // What no longer matches is said, not ignored (ADR-0272).
        foreach (var key in view.Hidden.Where(k => definition.Series.All(s => s.Id != k)).Order(StringComparer.Ordinal))
            notes.Add($"\"{key}\" is hidden, but no series has that key.");
        // A scale asked for and not drawn is said by name (ADR-0290).
        if (view.Scale == AxisScale.Logarithmic && !logX && axis.Values == AxisValues.Numbers
            && visible.FirstOrDefault(s => LowBound(s.XInterval) is not null) is { } bounded && !visible.Any(s => s.Points.Any(p => !(p.X > 0)) || s.Sampler is { From: <= 0 }))
            notes.Add($"The axis across stays proportional: '{bounded.Name}' has a bound of {LowBound(bounded.XInterval)!.Value.Bound.ToString("0.####", CultureInfo.InvariantCulture)} in its {bounded.XInterval!.Meaning} at {axis.Write(LowBound(bounded.XInterval)!.Value.X)}, which a logarithmic axis cannot place.");
        else if (view.Scale == AxisScale.Logarithmic && !logX)
            notes.Add(NotLogarithmic(visible.FirstOrDefault(s => s.Points.Any(p => !(p.X > 0)) || s.Sampler is { From: <= 0 } || LowBound(s.XInterval) is not null), "across", axis.Values != AxisValues.Numbers));
        var logY = view.YScale == AxisScale.Logarithmic;
        if (logY && LogarithmicRefusal(visible, axis, from, to, devicePixels) is { } refusal)
        {
            notes.Add(refusal);
            logY = false;
        }

        // The horizontal map: proportional, or categories evenly spaced, centred in their slots.
        var slots = CategorySlots(axis, from, to);
        Func<double, double> map = categories
            ? x => slots.TryGetValue(x, out var i) ? plotLeft + (i + 0.5) * plotWidth / Math.Max(1, slots.Count) : double.NaN
            : logX ? x => plotLeft + (to == from ? 0.5 : Math.Log(x / from) / Math.Log(to / from)) * plotWidth
            : x => plotLeft + (to == from ? 0.5 : (x - from) / (to - from)) * plotWidth;

        var positions = PositionsOf(visible, from, to, plotWidth, map, categories ? slots.Keys : null);
        if (visible.Count > 0 && positions.Count == 0)
            notes.Add($"No series has a point between {axis.Write(from)} and {axis.Write(to)}.");

        var points = visible.SelectMany(s => s.Points.Select(p => p.X)).ToHashSet();
        // On packed sessions a date tick (a month's start) stands at the first session on or after it (ADR-0301).
        var sessions = categories && axis.Values == AxisValues.Dates ? slots.Keys.Order().ToArray() : null;
        Func<double, double> tickMap = sessions is null ? map : x =>
        {
            var i = Array.BinarySearch(sessions, x);
            if (i < 0) i = ~i;
            return i < sessions.Length ? map(sessions[i]) : double.NaN;
        };
        var xTicks = TickLayout.Horizontal(axis, style, from, to, points, tickMap, metrics.EffectiveTextWidth, lh);
        var ticksBand = xTicks.Depth + lh / 2;

        // Every series' panel, hidden ones included, so a panel whose series are all hidden keeps
        // its legend and can show them again.
        var panelNames = definition.Series.Select(s => s.Panel).Distinct().ToList();
        // A visible Price Range with a volume column gets a panel of its own below its panel (ADR-0300).
        var specs = new List<(string Name, ChartSeries? VolumeOf)>();
        foreach (var name in panelNames)
        {
            specs.Add((name, null));
            foreach (var s in visible.Where(s => s.Panel == name && s.Kind == SeriesKind.PriceRange && s.Bars.Any(b => b.Volume is not null)))
                specs.Add(($"{s.Name} volume", s));
        }
        var swatch = lh * 1.5;
        double EntryWidth(ChartSeries s) => swatch + pad / 2 + metrics.EffectiveTextWidth(LegendName(s)) + pad;
        var rows = specs.Select(spec => spec.VolumeOf is null && legend.Position is LegendPosition.Top or LegendPosition.Bottom
            ? Rows(definition.Series.Where(s => s.Panel == spec.Name).ToList(), EntryWidth, plotWidth)
            : []).ToList();
        double Band(int i) => rows[i].Count == 0 ? 0 : rows[i].Count * lh + pad / 2;
        static double Weight((string Name, ChartSeries? VolumeOf) spec) => spec.VolumeOf is null ? 1 : VolumeShare;

        var bands = Enumerable.Range(0, specs.Count).Sum(Band);
        var unit = specs.Count == 0 ? 0
            : Math.Max(1, (metrics.Height - 2 * pad - ticksBand - bands - metrics.PanelGap * (specs.Count - 1)) / specs.Sum(Weight));

        // Candles merged into the market's own periods where the plot is too narrow for each (ADR-0302).
        var merged = new Dictionary<string, (List<DrawnCandle> Candles, CandlePeriod Period)>();
        foreach (var s in visible.Where(s => s.Kind == SeriesKind.PriceRange))
        {
            var (candles, period, unfit) = Merge(s, from, to, devicePixels);
            merged[s.Id] = (candles, period);
            if (unfit is not null) notes.Add(unfit);
            else if (period != CandlePeriod.Row)
                notes.Add($"{s.Name}: {PriceRanges.Name(period).ToLowerInvariant()} candles, merged from rows{(s.MergeTo is null ? " to fit" : "")}.");
        }
        // An interval's meaning, in the legend and here (ADR-0303).
        foreach (var s in visible.Where(s => s.Interval is not null))
            notes.Add($"{s.Name}: the {(s.XInterval is null ? "" : "vertical ")}interval is {s.Interval!.Meaning}.");
        foreach (var s in visible.Where(s => s.XInterval is not null))
            notes.Add($"{s.Name}: the horizontal interval is {s.XInterval!.Meaning}.");
        var rowWidth = RowWidth(visible, axis, map, from, to, plotWidth);
        if (axis.Packed)
            notes.Add("Dates are packed: the days nobody traded are left out, so the spacing is sessions, not calendar time.");

        var panels = new List<PanelScene>();
        var marks = new List<ChartMark>();
        var y = pad;
        for (var pi = 0; pi < specs.Count; pi++)
        {
            var (name, volumeOf) = specs[pi];
            var panelHeight = Weight(specs[pi]) * unit;
            var last = pi == specs.Count - 1;
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
            if (volumeOf is not null)
            {
                panels.Add(VolumePanel(name, volumeOf, looks[volumeOf.Id], merged[volumeOf.Id].Candles, map, rowWidth, plot));
                continue;
            }
            var members = visible.Where(s => s.Panel == name).ToList();
            var drawings = members.ToDictionary(s => s.Id, s => s.Kind == SeriesKind.PriceRange
                ? new SeriesDrawing([], []) { Candles = merged[s.Id].Candles }
                : SeriesDrawing.Of(s, axis, from, to, devicePixels, Quotes(s, visible)));
            var (left, right, independent) = VerticalAxes(members, drawings, from, to, plot, independentScales?.Contains(name) == true, logY, view.YFrom, view.YTo);
            if (independent)
                notes.Add(panelNames.Count > 1 && name.Length > 0
                    ? $"{name}: the left and right axes put zero at different heights."
                    : "The left and right axes put zero at different heights.");
            var pieces = members.Select(s => Piece(s, looks[s.Id], Marked(s, visible), drawings[s.Id], s.Axis == AxisSide.Right ? right! : left!, map, plot, from, to, metrics.DevicePixelRatio, devicePixels, rowWidth, PointScales.MaxRadius(lh))
                with { Period = merged.TryGetValue(s.Id, out var m) ? m.Period : CandlePeriod.Row }).ToEquatableList();
            if (scatter)
                foreach (var piece in pieces.Where(p => p.Kind == SeriesKind.Observation))
                    foreach (var m in piece.Markers)
                        marks.Add(new ChartMark(piece.Id, pi, m.X, m.Y, m.Point, m.Count));
            var zero = left is { } l && l.Min <= 0 && l.Max >= 0 ? YOf(l, 0, plot) : right is { } r && r.Min <= 0 && r.Max >= 0 ? YOf(r, 0, plot) : (double?)null;
            var entries = Legend(definition.Series.Where(s => s.Panel == name).ToList(), rows[pi], legend, metrics, plot, legendY, looks, view.Hidden, EntryWidth);
            panels.Add(new PanelScene(name, plot, left, right, zero, pieces, entries));
        }

        foreach (var s in visible.Where(s => s.Segments.Count > 0))
            foreach (var g in s.Segments)
                notes.Add($"{s.Name}: {XLabel(axis, g.From)}–{XLabel(axis, g.To)} {g.Method}{(g.Extrapolated ? " (extrapolated)" : "")}");
        foreach (var s in visible)
            notes.AddRange(PointScales.Notes(s));
        var plots = panels.Where(p => p.VolumeOf is null).ToList();
        var (colourKeys, sizeKeys) = plots.Count == 0 ? ([], []) : PointScales.Keys(visible,
            plotLeft + plotWidth + (visible.Any(s => s.Axis == AxisSide.Right) ? YAxisWidth(metrics) : 0) + PointScales.Gap(metrics),
            plots[0].Plot.Y, plots[^1].Plot.Y + plots[^1].Plot.Height - plots[0].Plot.Y, metrics);
        if (coloursRepeat)
            notes.Add("Colours repeat: there are more series than colours. The legend and the tooltip name each series.");

        return new ChartScene(panels.ToEquatableList(),
            xTicks.Ticks.Select(t => (t.Position, tickMap(t.Position), t.Label)).Where(t => double.IsFinite(t.Item2)).ToEquatableList(), from, to, notes.ToEquatableList(),
            positions.ToEquatableList())
        {
            XTitle = TitleOf(axis, merged.Select(m => (m.Value.Period, visible.First(s => s.Id == m.Key).MergeTo is not null))),
            Scatter = scatter,
            Marks = marks.ToEquatableList(),
            LogarithmicX = logX,
            ColourKeys = colourKeys.ToEquatableList(),
            SizeKeys = sizeKeys.ToEquatableList(),
        };
    }

    private static string NotLogarithmic(ChartSeries? s, string which, bool notNumbers) =>
        notNumbers ? $"The axis {which} stays proportional: only numbers can be placed on a logarithmic axis."
        : $"The axis {which} stays proportional: '{s?.Name}' has a value at or below zero, which a logarithmic axis cannot place.";

    // A vertical log axis is refused, by name, by a series with a value at or below zero, or one that
    // always reaches zero (ADR-0290). A value is never dropped to make the axis fit.
    private static string? LogarithmicRefusal(List<ChartSeries> visible, ChartAxis axis, double from, double to, int devicePixels)
    {
        foreach (var s in visible)
        {
            if (s.Kind == SeriesKind.AmountPerPeriod)
                return $"The vertical axis stays proportional: '{s.Name}' is an Amount per period, which is drawn from zero.";
            // A function is judged by the samples it would be drawn from, such as a fitted line that
            // crosses zero inside its range.
            var low = s.Sampler is null ? [] : SeriesDrawing.Of(s, axis, from, to, devicePixels, Quotes(s, visible)).Lines.SelectMany(l => l).Where(p => !(p.Y > 0)).Take(1).ToList();
            if (low is [var sample])
                return $"The vertical axis stays proportional: '{s.Name}' reaches {sample.Y.ToString("0.####", CultureInfo.InvariantCulture)} at {axis.Write(sample.X)}, which a logarithmic axis cannot place.";
            var point = s.Sampler is null ? s.Points.FirstOrDefault(p => p.Y is { } v && !(v.ToDouble() > 0)) : default;
            if (s.Sampler is null && point.Y is { } value)
                return $"The vertical axis stays proportional: '{s.Name}' has {s.Write(value)} at {axis.Write(point.X)}, which a logarithmic axis cannot place.";
            if (LowBound(s.Interval) is { } low0)
                return $"The vertical axis stays proportional: '{s.Name}' has a bound of {low0.Bound.ToString("0.####", CultureInfo.InvariantCulture)} in its {s.Interval!.Meaning} at {axis.Write(low0.X)}, which a logarithmic axis cannot place.";
        }
        return null;
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
            ? definition.Series.Max(s => metrics.LineHeight * 1.5 + pad / 2 + metrics.EffectiveTextWidth(LegendName(s)) + pad)
            : 0;
        var plotLeft = pad + (legend?.Position == LegendPosition.Left ? column : 0) + YAxisWidth(metrics);
        var plotRight = metrics.Width - pad - (legend?.Position == LegendPosition.Right ? column : 0)
            - (visible.Any(s => s.Axis == AxisSide.Right) ? YAxisWidth(metrics) : 0)
            - PointScales.ColumnWidth(visible, metrics);
        var plotWidth = Math.Max(1, plotRight - plotLeft);
        return (axis, visible, from, to, plotLeft, plotWidth, (int)Math.Ceiling(plotWidth * metrics.DevicePixelRatio));
    }

    // The axis as the viewer switched it (ADR-0251). A Number or Date axis drawn as categories with
    // no named positions takes each position a series has, written as the axis writes it.
    private static ChartAxis AxisOf(ChartDefinition d, ChartView view)
    {
        var axis = d.Axis;
        // A packed axis is the candles' positions as categories (ADR-0301).
        if (axis.Packed && view.Scale != AxisScale.Proportional)
            return axis with
            {
                Scale = AxisScale.Categories,
                Named = PackedSessions(d).Order()
                    .Select(x => (x, axis.Write(x))).ToArray(),
            };
        if (axis.Packed) axis = axis with { Packed = false };
        if (view.Scale is not { } scale || scale == axis.EffectiveScale || axis.Values == AxisValues.Labels)
            return axis;
        if (scale == AxisScale.Categories && d.Series.Any(s => s.Sampler is not null || s.Kind == SeriesKind.Observation))
            return axis;
        if (scale == AxisScale.Logarithmic && (axis.Values != AxisValues.Numbers
                || d.Series.Any(s => s.Points.Any(p => !(p.X > 0)) || s.Sampler is { From: <= 0 } || LowBound(s.XInterval) is not null)))
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
        LegendEntry Entry(ChartSeries s, double x, double y) => new(s.Id, LegendName(s), looks[s.Id], hidden.Contains(s.Id), new Box(x, y, width(s), lh));
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
        var xs = visible.SelectMany(s => s.Points.Select(p => p.X).Concat(s.Sampler is { } f ? [f.From, f.To] : []).Concat(BoundValues(s.XInterval))).ToList();
        var lo = xs.Count > 0 ? xs.Min() : axis.EffectiveScale == AxisScale.Logarithmic ? 1 : 0;
        var hi = xs.Count > 0 ? xs.Max() : axis.EffectiveScale == AxisScale.Logarithmic ? 10 : 1;
        // A scatter's marks are not drawn on the plot's edge: its bounds are rounded out, to whole
        // decades on a logarithmic axis (ADR-0290).
        if (visible.Any(s => s.Kind == SeriesKind.Observation) && axis.Values == AxisValues.Numbers)
        {
            var nice = TickLayout.Nice(lo, hi, 6);
            (lo, hi) = axis.EffectiveScale == AxisScale.Logarithmic ? TickLayout.Decades(lo, hi) : (nice.Min, nice.Max);
        }
        return (view.From ?? lo, view.To ?? hi);
    }

    // The quotes an Interpolated series is paired with: the positions of the series that share its colour key.
    private static IReadOnlyCollection<double> Quotes(ChartSeries s, List<ChartSeries> visible) =>
        s.Sampler is null ? [] : visible.Where(o => o.Id != s.Id && !o.Interpolated && o.EffectiveKeys.Colour == s.EffectiveKeys.Colour)
            .SelectMany(o => o.Points.Select(p => p.X)).ToList();

    private static (VerticalAxis? Left, VerticalAxis? Right, bool Independent) VerticalAxes(List<ChartSeries> members, Dictionary<string, SeriesDrawing> drawings, double from, double to, Box plot, bool declaredIndependent,
        bool logarithmic = false, double? yFrom = null, double? yTo = null)
    {
        (double Lo, double Hi, double Step, bool Zero)? Range(AxisSide side)
        {
            var ss = members.Where(s => s.Axis == side).ToList();
            if (ss.Count == 0) return null;
            var ys = ss.SelectMany(s => drawings[s.Id].Lines.SelectMany(l => l).Where(p => p.X >= from && p.X <= to).Select(p => p.Y)
                .Concat(drawings[s.Id].Markers.Where(m => m.X >= from && m.X <= to).SelectMany(m => m.YBounds is { } yb ? [m.Y, yb.Lower, yb.Upper] : new[] { m.Y }))
                .Concat(drawings[s.Id].Candles.SelectMany(c => new[] { c.Low, c.High }))
                .Concat(drawings[s.Id].Whiskers.Concat(drawings[s.Id].Bands.SelectMany(b => b)).Where(w => w.X >= from && w.X <= to).SelectMany(w => new[] { w.Lower, w.Upper }))).ToList();
            var lo = ys.Count > 0 ? ys.Min() : logarithmic ? 1 : 0;
            var hi = ys.Count > 0 ? ys.Max() : logarithmic ? 10 : 1;
            // The left axis narrowed by a rectangle shows what was asked, unrounded (ADR-0290).
            var narrowed = side == AxisSide.Left && (yFrom is not null || yTo is not null);
            if (narrowed) { lo = yFrom ?? lo; hi = yTo ?? hi; }
            if (logarithmic)
            {
                var (dlo, dhi) = narrowed && lo > 0 && hi > lo ? (lo, hi) : TickLayout.Decades(lo, hi);
                return (dlo, dhi, 0, false);
            }
            if (narrowed && hi > lo) return (lo, hi, TickLayout.Nice(lo, hi).Step, lo <= 0 && hi >= 0);
            if (ss.Any(s => s.ReachesZero)) { lo = Math.Min(lo, 0); hi = Math.Max(hi, 0); }
            var (nlo, nhi, step) = TickLayout.Nice(lo, hi);
            return (nlo, nhi, step, nlo <= 0 && nhi >= 0);
        }
        var l = Range(AxisSide.Left);
        var r = Range(AxisSide.Right);
        var independent = false;
        // A logarithmic axis has no zero to align (ADR-0290).
        if (logarithmic)
            return (l is { } la ? LogAxis(AxisSide.Left, la.Lo, la.Hi, plot) : null, r is { } ra ? LogAxis(AxisSide.Right, ra.Lo, ra.Hi, plot) : null, false);
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

    private static VerticalAxis LogAxis(AxisSide side, double lo, double hi, Box plot)
    {
        var v = new VerticalAxis(side, lo, hi, EquatableList<(double, double, string)>.Empty, Broken: false) { Logarithmic = true };
        var ticks = TickLayout.LogSteps(lo, hi).Select(t => (t, v.YOf(t, plot), t.ToString("0.####", CultureInfo.InvariantCulture))).ToEquatableList();
        return v with { Ticks = ticks };
    }

    private static double YOf(VerticalAxis axis, double value, Box plot) => axis.YOf(value, plot);

    // A level's points are marked when its marker key asks for it, or when they are the quotes an
    // Interpolated series is drawn through, which the markers tell apart from the curve (ADR-0257).
    // A step's ends are always marked: filled and open say which end takes the value (ADR-0253).
    private static bool Marked(ChartSeries s, List<ChartSeries> visible) =>
        s.Kind != SeriesKind.Level || s.Keys?.Marker is not null || s.Interpolated
        || visible.Any(o => o.Id != s.Id && o.Interpolated && o.EffectiveKeys.Colour == s.EffectiveKeys.Colour);

    private static SeriesPiece Piece(ChartSeries s, SeriesLook look, bool marked, SeriesDrawing drawing, VerticalAxis axis, Func<double, double> map, Box plot, double from, double to, double dpr, int devicePixels, double rowWidth, double maxRadius)
    {
        if (s.Kind == SeriesKind.PriceRange)
            return new SeriesPiece(s.Id, s.Name, s.EffectiveKeys, look, s.Kind, s.Interpolated, EquatableList<EquatableList<DrawnPoint>>.Empty,
                EquatableList<DrawnMarker>.Empty, EquatableList<(double, double)>.Empty, EquatableList<PixelSummary>.Empty)
            {
                Candles = drawing.Candles.Select(c =>
                {
                    var (x, half) = Extent(c, map, rowWidth);
                    return new PlacedCandle(x, half, c.Open is { } o ? YOf(axis, o, plot) : null, YOf(axis, c.High, plot), YOf(axis, c.Low, plot), YOf(axis, c.Close, plot),
                        c.Rising, c.Unchanged, c.Rows, c.From, c.To);
                }).ToEquatableList(),
            };

        var inRange = new SeriesDrawing(
            drawing.Lines.Select(l => (IReadOnlyList<DrawnPoint>)l.Where(p => p.X >= from && p.X <= to).ToList()).Where(l => l.Count > 1).ToList(),
            drawing.Markers.Where(m => m.X >= from && m.X <= to).ToList());
        // What the axis does not show is not drawn: a mark outside it is left out, and a line is cut at
        // its edge (a rectangle narrows the left axis, ADR-0290).
        bool Shown(double v) => v >= axis.Min - Math.Abs(axis.Max - axis.Min) * 1e-9 && v <= axis.Max + Math.Abs(axis.Max - axis.Min) * 1e-9;
        inRange = new SeriesDrawing(inRange.Lines.SelectMany(l => ClipLine(l, axis.Min, axis.Max, axis.Logarithmic)).ToList(), inRange.Markers.Where(m => Shown(m.Y)).ToList());
        if (s.Kind == SeriesKind.Observation)
            return new SeriesPiece(s.Id, s.Name, s.EffectiveKeys, look, s.Kind, s.Interpolated, EquatableList<EquatableList<DrawnPoint>>.Empty,
                Merged(inRange.Markers.Select(m => m with
                {
                    X = map(m.X),
                    Y = YOf(axis, m.Y, plot),
                    // A whisker stops at the plot's edge when the range is narrowed past it (ADR-0304).
                    YBounds = m.YBounds is { } yb ? Ordered(YOf(axis, Math.Clamp(yb.Lower, axis.Min, axis.Max), plot), YOf(axis, Math.Clamp(yb.Upper, axis.Min, axis.Max), plot)) : null,
                    XBounds = m.XBounds is { } xb ? Ordered(map(Math.Clamp(xb.Lower, from, to)), map(Math.Clamp(xb.Upper, from, to))) : null,
                    Radius = m.Radius is { } share ? Math.Max(0.75, share * maxRadius) : null,
                }), dpr).ToEquatableList(),
                EquatableList<(double, double)>.Empty, EquatableList<PixelSummary>.Empty)
            {
                ColourDiverging = s.ColourBy?.Centre is not null,
                Edges = ExChart.Engine.Edges.Pixels(s, map, v => YOf(axis, v, plot), plot).ToEquatableList(),
            };
        var thinned = Thinning.Of(inRange, x => (map(x) - plot.X) * dpr, devicePixels);
        var (whiskers, bands) = Intervals(drawing with
        {
            Whiskers = drawing.Whiskers.Where(w => w.X >= from && w.X <= to).ToList(),
            Bands = drawing.Bands.Select(b => (IReadOnlyList<DrawnWhisker>)b.Where(w => w.X >= from && w.X <= to).ToList()).Where(b => b.Count > 0).ToList(),
        }, x => (map(x) - plot.X) * dpr, devicePixels);
        DrawnPoint Px(DrawnPoint p) => new(map(p.X), YOf(axis, p.Y, plot));
        DrawnWhisker Wpx(DrawnWhisker w) => new(map(w.X), YOf(axis, w.Lower, plot), YOf(axis, w.Upper, plot));
        return new SeriesPiece(s.Id, s.Name, s.EffectiveKeys, look, s.Kind, s.Interpolated,
            thinned.Drawing.Lines.Select(l => l.Select(Px).ToEquatableList()).ToEquatableList(),
            (marked ? thinned.Drawing.Markers : []).Select(m => m with { X = map(m.X), Y = YOf(axis, m.Y, plot) }).ToEquatableList(),
            s.Segments.Where(g => g.Extrapolated).Select(g => (map(Math.Max(g.From, from)), map(Math.Min(g.To, to)))).ToEquatableList(),
            Thinning.Through(s.Thinned.Where(t => t.To >= from && t.From <= to).ToList(), thinned.Thinned,
                inRange.Lines.SelectMany(l => l.Select(p => p.X)).ToList()).ToEquatableList())
        {
            Whiskers = whiskers.Select(Wpx).ToEquatableList(),
            Bands = bands.Select(b => b.Select(Wpx).ToEquatableList()).ToEquatableList(),
            IntervalMeaning = s.Interval?.Meaning,
        };
    }

    // Past one point a device pixel, whiskers become the band, thinned to the lowest lower and the
    // highest upper per pixel column, so no interval is dropped where markers are (ADR-0303).
    private static (List<DrawnWhisker> Whiskers, List<IReadOnlyList<DrawnWhisker>> Bands) Intervals(SeriesDrawing inRange, Func<double, double> toPixel, int devicePixels)
    {
        var whiskers = inRange.Whiskers.ToList();
        var bands = inRange.Bands.ToList();
        if (whiskers.Count > devicePixels) { bands = [whiskers]; whiskers = []; }
        if (bands.Sum(b => b.Count) <= devicePixels) return (whiskers, bands);
        return (whiskers, bands.Select(b => (IReadOnlyList<DrawnWhisker>)b.GroupBy(w => Math.Floor(toPixel(w.X)))
            .Select(g => new DrawnWhisker((g.First().X + g.Last().X) / 2, g.Min(w => w.Lower), g.Max(w => w.Upper))).ToList()).ToList());
    }

    // A candle centred on what it stands for, 70% as wide: one row's width, or a merged candle's rows
    // from first to last (ADR-0302).
    private static (double X, double Half) Extent(DrawnCandle c, Func<double, double> map, double rowWidth)
    {
        var a = map(c.From) - rowWidth / 2;
        var b = map(c.To) + rowWidth / 2;
        return ((a + b) / 2, Math.Max(0.5, BodyShare / 2 * (b - a)));
    }

    // The width of one row in pixels: the narrowest gap between neighbouring candles.
    private static double RowWidth(List<ChartSeries> visible, ChartAxis axis, Func<double, double> map, double from, double to, double plotWidth)
    {
        var xs = visible.Where(s => s.Kind == SeriesKind.PriceRange).SelectMany(s => s.Bars.Select(b => b.X)).Where(x => x >= from && x <= to).Distinct().Order().ToList();
        if (xs.Count < 2) return plotWidth / 10;
        var gap = double.PositiveInfinity;
        for (var i = 1; i < xs.Count; i++) gap = Math.Min(gap, map(xs[i]) - map(xs[i - 1]));
        return double.IsFinite(gap) && gap > 0 ? gap : plotWidth / xs.Count;
    }

    // The first of the market's periods whose candles fit three device pixels each, or the Consumer's
    // fixed one, which says so when it does not fit (ADR-0302).
    private static (List<DrawnCandle> Candles, CandlePeriod Period, string? Unfit) Merge(ChartSeries s, double from, double to, int devicePixels)
    {
        static bool Fits(int count, int pixels) => count <= 1 || count * 3 <= pixels;
        if (s.MergeTo is { } fixedPeriod)
        {
            var candles = PriceRanges.Candles(s, from, to, fixedPeriod);
            return Fits(candles.Count, devicePixels) ? (candles, fixedPeriod, null)
                : ([], fixedPeriod, $"{s.Name}: {candles.Count.ToString("N0", CultureInfo.InvariantCulture)} {(fixedPeriod == CandlePeriod.Row ? "rows" : PriceRanges.Name(fixedPeriod).ToLowerInvariant() + " candles")} do not fit; narrow the range.");
        }
        List<DrawnCandle> c = [];
        foreach (var period in Enum.GetValues<CandlePeriod>())
        {
            c = PriceRanges.Candles(s, from, to, period);
            if (Fits(c.Count, devicePixels)) return (c, period, null);
        }
        // Not even years fit: nothing is drawn in slivers, and the chart says so (ADR-0302).
        return ([], CandlePeriod.Year, $"{s.Name}: {c.Count.ToString("N0", CultureInfo.InvariantCulture)} yearly candles do not fit; narrow the range or widen the chart.");
    }

    private static PanelScene VolumePanel(string name, ChartSeries s, SeriesLook look, List<DrawnCandle> candles, Func<double, double> map, double rowWidth, Box plot)
    {
        var max = candles.Count == 0 ? 1 : Math.Max(candles.Max(c => c.Volume ?? 0), 0);
        var (lo, hi, step) = TickLayout.Nice(0, max == 0 ? 1 : max);
        var axis = Axis(AxisSide.Left, (lo, hi, step, true), plot);
        var bars = candles.Where(c => c.Volume is not null).Select(c =>
        {
            var (x, half) = Extent(c, map, rowWidth);
            return new PlacedVolume(x, half, YOf(axis, c.Volume!.Value, plot), YOf(axis, 0, plot), c.Rising, c.Unchanged, c.Rows, c.From, c.To);
        }).ToEquatableList();
        var piece = new SeriesPiece(s.Id, name, s.EffectiveKeys, look, s.Kind, false, EquatableList<EquatableList<DrawnPoint>>.Empty,
            EquatableList<DrawnMarker>.Empty, EquatableList<(double, double)>.Empty, EquatableList<PixelSummary>.Empty) { Volumes = bars };
        return new PanelScene(name, plot, axis, null, YOf(axis, 0, plot), new[] { piece }.ToEquatableList(), EquatableList<LegendEntry>.Empty) { VolumeOf = s.Id };
    }

    private static string TitleOf(ChartAxis axis, IEnumerable<(CandlePeriod Period, bool Fixed)> periods)
    {
        var title = axis.Title.Length > 0 ? axis.Title : axis.Packed ? "Trading days" : periods.Any(p => p.Period != CandlePeriod.Row) ? "Date" : "";
        if (axis.Packed) title += " (closed days packed)";
        if (periods.Where(p => p.Period != CandlePeriod.Row).Distinct().ToList() is [var (period, isFixed)])
            title += $" · {PriceRanges.Name(period)} (merged from rows{(isFixed ? "" : " to fit")})";
        return title;
    }

    // A series' name in its legend, with its interval's meaning (ADR-0303).
    internal static string LegendName(ChartSeries s) => (s.Interval?.Meaning, s.XInterval?.Meaning) switch
    {
        (null, null) => s.Name,
        ({ } y, null) => $"{s.Name} ({y})",
        (null, { } x) => $"{s.Name} (x: {x})",
        ({ } y, { } x) when y == x => $"{s.Name} ({y})",
        ({ } y, { } x) => $"{s.Name} (y: {y}; x: {x})",
    };

    // The bounds a series hands in, with values (ADR-0303, ADR-0304).
    private static IEnumerable<double> BoundValues(SeriesInterval? interval) =>
        interval?.Bounds.SelectMany(b => new[] { b.Lower, b.Upper }).OfType<ChartValue>().Select(v => v.ToDouble()) ?? [];

    // A bound a logarithmic axis cannot place: one at or below zero (ADR-0304).
    private static (double X, double Bound)? LowBound(SeriesInterval? interval) =>
        interval?.Bounds.SelectMany(b => new[] { b.Lower, b.Upper }.OfType<ChartValue>().Select(v => (b.X, v.ToDouble()))).Where(e => !(e.Item2 > 0))
            .Select(e => ((double, double)?)e).FirstOrDefault();

    // The packed axis's slots: each row of a Price Range with a value (ADR-0301).
    internal static IEnumerable<double> PackedSessions(ChartDefinition d) =>
        d.Series.Where(s => s.Kind == SeriesKind.PriceRange)
            .SelectMany(s => { var hasOpen = PriceRanges.HasOpen(s); return s.Bars.Where(b => PriceRanges.Whole(b, hasOpen)).Select(b => b.X); })
            .Distinct();

    private static (double Lower, double Upper) Ordered(double a, double b) => (Math.Min(a, b), Math.Max(a, b));

    // Merged marks carry the union of their intervals, so none is dropped (ADR-0304).
    private static (double Lower, double Upper)? Union((double Lower, double Upper)? a, (double Lower, double Upper)? b) =>
        a is { } p ? b is { } q ? (Math.Min(p.Lower, q.Lower), Math.Max(p.Upper, q.Upper)) : p : b;

    // Past the pixels, an Observation's marks centred on one device pixel are one mark, which says how
    // many it stands for (ADR-0290). The first point drawn there names it.
    private static List<DrawnMarker> Merged(IEnumerable<DrawnMarker> marks, double dpr)
    {
        var at = new Dictionary<(long, long, int?, double?, bool), int>();
        var kept = new List<DrawnMarker>();
        foreach (var m in marks)
        {
            // Marks merge only where they look alike, so no colour or size is lost to a merge (ADR-0320).
            var cell = ((long)Math.Floor(m.X * dpr), (long)Math.Floor(m.Y * dpr), m.Band, m.Radius, m.Filled);
            if (at.TryGetValue(cell, out var i))
                kept[i] = kept[i] with { Count = kept[i].Count + m.Count, YBounds = Union(kept[i].YBounds, m.YBounds), XBounds = Union(kept[i].XBounds, m.XBounds) };
            else
            {
                at[cell] = kept.Count;
                kept.Add(m);
            }
        }
        return kept;
    }

    // A line cut where it leaves [lo, hi], each piece ending on the edge it crosses.
    // On a logarithmic axis the segment is straight in the logarithm, so the crossing is found there.
    private static IEnumerable<IReadOnlyList<DrawnPoint>> ClipLine(IReadOnlyList<DrawnPoint> line, double lo, double hi, bool logarithmic)
    {
        if (line.All(p => p.Y >= lo && p.Y <= hi)) { yield return line; yield break; }
        var piece = new List<DrawnPoint>();
        double T(double v) => logarithmic ? Math.Log(v) : v;
        DrawnPoint Cross(DrawnPoint a, DrawnPoint b, double edge) => new(a.X + (b.X - a.X) * (T(edge) - T(a.Y)) / (T(b.Y) - T(a.Y)), edge);
        for (var i = 0; i < line.Count; i++)
        {
            var p = line[i];
            var inside = p.Y >= lo && p.Y <= hi;
            if (i > 0)
            {
                var q = line[i - 1];
                var qInside = q.Y >= lo && q.Y <= hi;
                if (qInside && !inside) piece.Add(Cross(q, p, p.Y > hi ? hi : lo));
                else if (!qInside && inside) piece.Add(Cross(q, p, q.Y > hi ? hi : lo));
                else if ((q.Y < lo && p.Y > hi) || (q.Y > hi && p.Y < lo))
                {
                    piece.Add(Cross(q, p, q.Y > hi ? hi : lo));
                    piece.Add(Cross(q, p, q.Y > hi ? lo : hi));
                }
            }
            if (inside) piece.Add(p);
            else if (piece.Count > 0)
            {
                if (piece.Count > 1) yield return piece;
                piece = [];
            }
        }
        if (piece.Count > 1) yield return piece;
    }

    private static string XLabel(ChartAxis axis, double x) =>
        axis.Format is null && axis.Values == AxisValues.Numbers && axis.Named.All(n => n.Position != x) ? TickLayout.Tenor(x) : axis.Write(x);
}
