namespace ExChart.Engine;

/// <summary>A point of a drawn line, in the axes' own units: the horizontal position and the height.</summary>
/// <param name="X">The horizontal position.</param>
/// <param name="Y">The height, in the vertical axis' units.</param>
public readonly record struct DrawnPoint(double X, double Y);

/// <summary>A marker: where it stands, and whether it is filled — the function takes the value there — or an outline — it does not (ADR-0253).</summary>
/// <param name="X">The horizontal position.</param>
/// <param name="Y">The height.</param>
/// <param name="Filled">Filled where the function takes the value; an outline where it does not.</param>
public readonly record struct DrawnMarker(double X, double Y, bool Filled)
{
    /// <summary>The index of the series' point the marker stands for, or -1 when it stands for none.</summary>
    public int Point { get; init; } = -1;

    /// <summary>How many of the series' points the marker stands for: more than one where markers on one device pixel were merged (ADR-0290).</summary>
    public int Count { get; init; } = 1;

    /// <summary>An Observation's vertical interval at the marker, or null: where marks merged, the union of theirs (ADR-0304).</summary>
    public (double Lower, double Upper)? YBounds { get; init; }

    /// <summary>An Observation's horizontal interval at the marker, or null: where marks merged, the union of theirs (ADR-0304).</summary>
    public (double Lower, double Upper)? XBounds { get; init; }

    /// <summary>Its colour band from the series' <see cref="ChartSeries.ColourBy"/>, or null for the series' own colour (ADR-0320).</summary>
    public int? Band { get; init; }

    /// <summary>
    /// Its radius from the series' <see cref="ChartSeries.SizeBy"/>, or null for the series' own: a share of
    /// the largest in a drawing, CSS pixels in a scene (ADR-0320).
    /// </summary>
    public double? Radius { get; init; }
}

/// <summary>
/// What one series draws, in the axes' units, before any pixel: the lines — each joining only
/// neighbours in the data — and the markers. A step has no riser: the function takes no value between
/// its levels (ADR-0253).
/// </summary>
/// <param name="Lines">Each an unbroken line; a gap, a step's level or a Segment's boundary starts another.</param>
/// <param name="Markers">The markers, filled or an outline.</param>
public sealed record SeriesDrawing(IReadOnlyList<IReadOnlyList<DrawnPoint>> Lines, IReadOnlyList<DrawnMarker> Markers)
{
    /// <summary>A Price Range's candles, one a row or merged (ADR-0300, ADR-0302).</summary>
    public IReadOnlyList<DrawnCandle> Candles { get; init; } = [];

    /// <summary>A Level's Interval drawn as whiskers (ADR-0303).</summary>
    public IReadOnlyList<DrawnWhisker> Whiskers { get; init; } = [];

    /// <summary>A Level's Interval drawn as a band: each run of neighbours that all have one (ADR-0303).</summary>
    public IReadOnlyList<IReadOnlyList<DrawnWhisker>> Bands { get; init; } = [];

    /// <summary>
    /// What a series draws. A sampled series is asked at about <paramref name="pixels"/> positions over
    /// the part of its range inside <paramref name="from"/>–<paramref name="to"/>, and always at each
    /// Segment's boundary and at each of <paramref name="quotes"/> (ADR-0257).
    /// </summary>
    public static SeriesDrawing Of(ChartSeries series, ChartAxis axis, double from = double.NegativeInfinity, double to = double.PositiveInfinity, int pixels = 0, IReadOnlyCollection<double>? quotes = null) =>
        series.Kind switch
        {
            SeriesKind.Level => series.Sampler is { } f
                ? Level(Sample(f, series.Segments, from, to, pixels, quotes ?? [], axis.EffectiveScale == AxisScale.Logarithmic), series.Segments, withMarkers: false)
                : WithInterval(Level(series.Points, series.Segments, withMarkers: true), series),
            SeriesKind.PriceRange => new SeriesDrawing([], []) { Candles = PriceRanges.Candles(series, from, to, CandlePeriod.Row) },
            SeriesKind.RateOverInterval => Steps(series, axis, perYear: false),
            SeriesKind.AmountPerPeriod => Steps(series, axis, perYear: true),
            SeriesKind.Observation => Observations(series),
            _ => throw new ChartRefusedException($"'{series.Name}' declares no Series Kind."),
        };

    // A point's interval where both its bounds are given; a band joins only neighbours that each have
    // one, and breaks at a point that has none (ADR-0303).
    private static SeriesDrawing WithInterval(SeriesDrawing drawing, ChartSeries series)
    {
        if (series.Interval is not { } iv) return drawing;
        var bounds = iv.Bounds.Where(b => b.Lower is not null && b.Upper is not null)
            .ToDictionary(b => b.X, b => new DrawnWhisker(b.X, b.Lower!.Value.ToDouble(), b.Upper!.Value.ToDouble()));
        if (iv.Style == IntervalStyle.Whiskers)
            return drawing with { Whiskers = series.Points.Where(p => bounds.ContainsKey(p.X)).Select(p => bounds[p.X]).ToList() };
        var bands = new List<IReadOnlyList<DrawnWhisker>>();
        var run = new List<DrawnWhisker>();
        foreach (var p in series.Points)
        {
            if (bounds.TryGetValue(p.X, out var w)) { run.Add(w); continue; }
            if (run.Count > 0) bands.Add(run);
            run = [];
        }
        if (run.Count > 0) bands.Add(run);
        return drawing with { Bands = bands };
    }

    /// <summary>The height a step at point <paramref name="i"/> is drawn at: its value, or for an amount its value per year.</summary>
    internal static double Height(ChartSeries series, ChartAxis axis, int i)
    {
        var v = series.Points[i].Y!.Value.ToDouble();
        if (series.Kind != SeriesKind.AmountPerPeriod) return v;
        var (a, b) = Interval(series, i);
        return v / axis.Years(a, b);
    }

    /// <summary>The interval a step's point <paramref name="i"/> stands for.</summary>
    internal static (double From, double To) Interval(ChartSeries series, int i) =>
        series.Step == StepSide.Preceding
            ? (series.Points[i - 1].X, series.Points[i].X)
            : (series.Points[i].X, series.Points[i + 1].X);

    private static SeriesDrawing Level(IReadOnlyList<ChartPoint> points, IReadOnlyList<Segment> segments, bool withMarkers)
    {
        var lines = new List<IReadOnlyList<DrawnPoint>>();
        var markers = new List<DrawnMarker>();
        var line = new List<DrawnPoint>();
        void End()
        {
            if (line.Count > 1) lines.Add(line);
            line = [];
        }
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (p.Y is not { } y) { End(); continue; }
            var d = new DrawnPoint(p.X, y.ToDouble());
            if (line.Count > 0 && segments.Any(g => Crosses(g, line[^1].X, p.X))) End();
            var atBoundary = segments.Any(g => g.From == p.X || g.To == p.X);
            // Sampled, the curve was asked just short of the boundary from its left: the left line
            // ends there and the right one starts at the boundary itself, never joined.
            if (atBoundary && line.Count > 0 && line[^1].X == Math.BitDecrement(p.X)) End();
            line.Add(d);
            if (withMarkers) markers.Add(new DrawnMarker(d.X, d.Y, Filled: true));
            // Handed in as points, a point on the boundary ends the left line and starts the right.
            if (atBoundary && line.Count > 1) { End(); line.Add(d); }
        }
        End();
        return new SeriesDrawing(lines, markers);
    }

    // A boundary strictly between two neighbours: the line is not joined across it, where the
    // Consumer's method changes and a forward may jump (ADR-0257).
    private static bool Crosses(Segment g, double a, double b) => (a < g.From && g.From < b) || (a < g.To && g.To < b);

    private static SeriesDrawing Steps(ChartSeries series, ChartAxis axis, bool perYear)
    {
        var p = series.Points;
        var lines = new List<IReadOnlyList<DrawnPoint>>();
        var markers = new List<DrawnMarker>();
        var preceding = series.Step == StepSide.Preceding;
        for (var i = 0; i < p.Count; i++)
        {
            if (p[i].Y is null) continue;
            var hasInterval = preceding ? i > 0 : i < p.Count - 1;
            if (!hasInterval)
            {
                // A rate at the edge holds at its own instant only; an amount there is refused when
                // the definition is made.
                markers.Add(new DrawnMarker(p[i].X, p[i].Y!.Value.ToDouble(), Filled: true));
                continue;
            }
            var h = Height(series, axis, i);
            var (a, b) = Interval(series, i);
            lines.Add([new DrawnPoint(a, h), new DrawnPoint(b, h)]);
            var (own, other) = preceding ? (b, a) : (a, b);
            markers.Add(new DrawnMarker(own, h, Filled: true));
            var neighbour = preceding ? i - 1 : i + 1;
            var continuous = p[neighbour].Y is not null && Height(series, axis, neighbour, preceding) == h;
            if (!continuous) markers.Add(new DrawnMarker(other, h, Filled: false));
        }
        return new SeriesDrawing(lines, markers);
    }

    // The height the neighbour's own level takes at the shared end; at an edge with no interval the
    // value itself, which an amount never has there.
    private static double Height(ChartSeries series, ChartAxis axis, int i, bool preceding)
    {
        var hasInterval = preceding ? i > 0 : i < series.Points.Count - 1;
        return hasInterval ? Height(series, axis, i) : series.Points[i].Y!.Value.ToDouble();
    }

    // An Observation's points, each a marker of its own and none joined (ADR-0290); a point missing
    // its value is no marker.
    // An interval's bounds, one a row (ADR-0304).
    private static SeriesDrawing Observations(ChartSeries series)
    {
        var points = series.Points;
        var markers = new List<DrawnMarker>(points.Count);
        for (var i = 0; i < points.Count; i++)
            if (points[i].Y is { } y && double.IsFinite(points[i].X))
            {
                // A point a scale has no value for is an outline, and the Notes say so (ADR-0320).
                var colour = series.ColourBy?.Values[i];
                var size = series.SizeBy?.Values[i];
                var unscaled = (series.ColourBy is not null && colour is null) || (series.SizeBy is not null && size is null);
                markers.Add(new DrawnMarker(points[i].X, y.ToDouble(), Filled: !unscaled)
                {
                    Point = i,
                    YBounds = BoundsAt(series.Interval, i),
                    XBounds = BoundsAt(series.XInterval, i),
                    Band = colour is { } c ? PointScales.Band(series.ColourBy!, c.ToDouble()) : null,
                    Radius = size is { } z ? PointScales.Share(series.SizeBy!, z.ToDouble()) : null,
                });
            }
        return new SeriesDrawing([], markers);
    }

    internal static (double Lower, double Upper)? BoundsAt(SeriesInterval? interval, int i) =>
        interval?.Bounds[i] is { Lower: { } lo, Upper: { } hi } ? (lo.ToDouble(), hi.ToDouble()) : null;

    internal static List<ChartPoint> Sample(SeriesSampler f, IReadOnlyList<Segment> segments, double from, double to, int pixels, IReadOnlyCollection<double> quotes, bool logarithmic = false)
    {
        var lo = Math.Max(f.From, from);
        var hi = Math.Min(f.To, to);
        if (!(lo <= hi)) return [];
        var xs = new SortedSet<double> { lo, hi };
        var n = Math.Max(pixels, 2);
        // On a logarithmic axis the samples are even in the axis' own scale (ADR-0290).
        for (var k = 1; k < n; k++) xs.Add(logarithmic && lo > 0 ? lo * Math.Pow(hi / lo, (double)k / n) : lo + (hi - lo) * k / n);
        foreach (var g in segments)
            foreach (var b in new[] { g.From, g.To })
                if (lo <= b && b <= hi) xs.Add(b);
        foreach (var q in quotes)
            if (lo <= q && q <= hi) xs.Add(q);
        var points = new List<ChartPoint>(xs.Count);
        foreach (var x in xs)
        {
            // Just short of a boundary the curve is asked from its left, so a jump there is drawn as
            // the Consumer's method has it on each side.
            if (segments.Any(g => (g.From == x || g.To == x) && x > lo))
                points.Add(new ChartPoint(Math.BitDecrement(x), f.ValueAt(Math.BitDecrement(x))));
            points.Add(new ChartPoint(x, f.ValueAt(x)));
        }
        return points;
    }
}
