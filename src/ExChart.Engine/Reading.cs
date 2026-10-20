namespace ExChart.Engine;

/// <summary>One series' line in the tooltip (ADR-0270).</summary>
/// <param name="Id">The series' identity.</param>
/// <param name="Name">Its name.</param>
/// <param name="Look">Its look, for the swatch.</param>
/// <param name="Value">Its value at the position, written through the series' format; null when it has none there.</param>
/// <param name="Note">What the value is: a step's interval, a thinned pixel's count, interpolated or extrapolated, or why there is no value.</param>
/// <param name="Y">The value's height in pixels, which the nearest lines are chosen by; null when it has none there.</param>
public sealed record ReadingLine(string Id, string Name, SeriesLook Look, string? Value, string? Note, double? Y);

/// <summary>What the tooltip says at one position in one panel (ADR-0270).</summary>
/// <param name="Position">The position, written as the axis writes it.</param>
/// <param name="Lines">The lines shown, top to bottom: the nearest to the pointer's height, as many as fit the plot.</param>
/// <param name="More">How many series are not listed, which the tooltip says as "and M more series"; never a silent cut.</param>
public sealed record ChartReading(string Position, EquatableList<ReadingLine> Lines, int More);

/// <summary>
/// Reads the chart at the position under the pointer (ADR-0270): every visible series of the
/// pointer's panel, each by its own Series Kind. A value is only ever one the Consumer handed in or
/// its sampler answered; nothing is read between two points.
/// </summary>
public static class ChartReadings
{
    /// <summary>
    /// What the tooltip says at <paramref name="position"/> in panel <paramref name="panel"/> of
    /// <paramref name="scene"/>, built from <paramref name="definition"/> at <paramref name="metrics"/>
    /// and <paramref name="view"/>. Past what fits the plot's height, the series nearest
    /// <paramref name="pointerY"/> are listed and the rest counted.
    /// </summary>
    public static ChartReading At(ChartDefinition definition, ChartScene scene, ChartMetrics metrics, ChartView view, double position, int panel, double pointerY)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(view);
        var p = scene.Panels[panel];
        var axis = definition.Axis;
        var byId = definition.Series.ToDictionary(s => s.Id);
        var lines = p.Series.Select(piece => byId[piece.Id].Kind == SeriesKind.PriceRange
            ? Candle(byId[piece.Id], piece, p, scene, axis, position)
            : Line(byId[piece.Id], piece, p, axis, position)).ToList();

        // A header line, the lines, and the "more" line, all inside the plot's height.
        var room = Math.Max(1, (int)Math.Floor(p.Plot.Height / metrics.LineHeight) - 2);
        var shown = lines.Count <= room ? lines
            : lines.OrderBy(l => l.Y is { } y ? Math.Abs(y - pointerY) : double.PositiveInfinity).Take(room).ToList();
        var ordered = shown.OrderBy(l => l.Y ?? double.PositiveInfinity).ThenBy(l => lines.IndexOf(l)).ToEquatableList();
        return new ChartReading(axis.Write(position), ordered, lines.Count - shown.Count);
    }

    /// <summary>
    /// What the tooltip says over an Observation's mark (ADR-0290): the point's name, or its position
    /// across, then the series with the point's x and y, and how many points the mark stands for when
    /// markers on one device pixel were merged — never one of them as if it were the only one.
    /// </summary>
    public static ChartReading OfMark(ChartDefinition definition, ChartScene scene, int mark)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(scene);
        var m = scene.Marks[mark];
        var s = definition.Series.First(x => x.Id == m.SeriesId);
        var look = scene.Panels[m.Panel].Series.First(p => p.Id == s.Id).Look;
        var p = s.Points[m.Point];
        var axis = definition.Axis;
        var value = $"{axis.Write(p.X)}, {(p.Y is { } y ? s.Write(y) : "")}";
        var note = Join(Join(ObservedBounds(s.Interval, m.Point, s.Write, s.XInterval is null ? "" : "y "), ObservedBounds(s.XInterval, m.Point, v => axis.Write(v.ToDouble()), "x ")),
            m.Count > 1 ? $"{m.Count} points here: {p.Name ?? "this one"} and {m.Count - 1} more" : null);
        // A colour or a size read from a column is said with its value (ADR-0320).
        if (s.ColourBy is { } c)
            note = Join(note, $"{c.Title} {(c.Values[m.Point] is { } cv ? cv.ToInvariantString() : "has no value")}");
        if (s.SizeBy is { } z)
            note = Join(note, $"{z.Title} {(z.Values[m.Point] is { } zv ? zv.ToInvariantString() : "has no value")}");
        return new ChartReading(p.Name ?? axis.Write(p.X), new[] { new ReadingLine(s.Id, s.Name, look, value, note, m.Y) }.ToEquatableList(), 0);
    }

    private static ReadingLine Line(ChartSeries s, SeriesPiece piece, PanelScene panel, ChartAxis axis, double x)
    {
        var vertical = s.Axis == AxisSide.Right ? panel.Right : panel.Left;
        double? Y(double height) => vertical is { } v ? v.YOf(height, panel.Plot) : null;
        ReadingLine Make(string? value, string? note, double? y) => new(s.Id, s.Name, piece.Look, value, note, y);

        // A thinned pixel names its range, never one point as if it were the only one (ADR-0254).
        var thinned = piece.Thinned.FirstOrDefault(t => t.From <= x && x <= t.To);
        if (thinned is not null && s.Sampler is null)
        {
            var held = s.Points.Where(q => q.X >= thinned.From && q.X <= thinned.To && q.Y is not null).Select(q => q.Y!.Value).ToList();
            if (held.Count > 1)
            {
                var lo = held.MinBy(v => v.ToDouble());
                var hi = held.MaxBy(v => v.ToDouble());
                var height = s.Kind == SeriesKind.AmountPerPeriod ? (double?)null : Mid(lo, hi);
                return Make($"{s.Write(lo)} – {s.Write(hi)}", $"{held.Count} points, {axis.Write(thinned.From)} – {axis.Write(thinned.To)}", height is { } h ? Y(h) : null);
            }
        }

        if (s.Sampler is { } f)
        {
            if (x < f.From || x > f.To)
                return Make(null, "outside its range", null);
            var v = f.ValueAt(x);
            return v is { } value ? Make(s.Write(value), Method(s, x), Y(value.ToDouble())) : Make(null, "missing", null);
        }

        var points = s.Points;
        if (s.Kind == SeriesKind.Level)
        {
            var i = points.ToList().FindIndex(q => q.X == x);
            if (i < 0) return Make(null, "no point here", null);
            return points[i].Y is { } value ? Make(s.Write(value), Join(Method(s, x), Bounds(s, x)), Y(value.ToDouble())) : Make(null, "missing", null);
        }

        // A step names its interval (ADR-0253): the point whose value holds at x, and the interval it holds over.
        var (index, from, to) = Interval(points, x, s.Step);
        if (index < 0) return Make(null, "no interval here", null);
        var held1 = points[index].Y;
        if (held1 is not { } v1) return Make(null, "missing", null);
        var interval = from == to ? $"at {axis.Write(from)}" : $"{axis.Write(from)} – {axis.Write(to)}";
        var note = Method(s, x) is { } m ? $"{interval}, {m}" : interval;
        var drawn = s.Kind == SeriesKind.AmountPerPeriod && from != to ? v1.ToDouble() / axis.Years(from, to) : v1.ToDouble();
        return Make(s.Write(v1), note, Y(drawn));
    }

    // A candle's four values, its change from the previous candle's close and its volume; a merged
    // candle names its period and how many rows it holds (ADR-0300, ADR-0302). In a volume panel, the
    // volume alone.
    private static ReadingLine Candle(ChartSeries s, SeriesPiece piece, PanelScene panel, ChartScene scene, ChartAxis axis, double x)
    {
        var vertical = panel.Left;
        double? Y(double height) => vertical is { } v ? panel.Plot.Y + panel.Plot.Height - (v.Max == v.Min ? 0.5 : (height - v.Min) / (v.Max - v.Min)) * panel.Plot.Height : null;
        ReadingLine Make(string? value, string? note, double? y) => new(s.Id, piece.Name, piece.Look, value, note, y);
        var period = piece.Period;
        // The candles as drawn, over the range shown, so the tooltip reads what the plot draws.
        var candles = PriceRanges.Candles(s, scene.XFrom, scene.XTo, period);
        var i = candles.FindIndex(c => period == CandlePeriod.Row ? c.From == x : PriceRanges.PeriodStart(c.From, period) == PriceRanges.PeriodStart(x, period));
        if (i < 0)
            return s.Bars.Any(b => b.X == x) ? Make(null, "(no value)", null) : Make(null, "no candle here", null);
        var c = candles[i];
        string W(double v) => s.Write(ChartValue.Of(v));
        string? Volume() => c.Volume is { } vol ? $"Vol {s.Write(ChartValue.Of(vol))}" : null;
        var span = c.Rows > 1 || period != CandlePeriod.Row ? $"{axis.Write(c.From)} – {axis.Write(c.To)}, {c.Rows} rows" : null;
        if (panel.VolumeOf is not null)
            return Make(Volume() ?? "(no value)", span, c.Volume is { } v0 ? Y(v0) : null);
        var values = c.Open is { } o ? $"O {W(o)} H {W(c.High)} L {W(c.Low)} C {W(c.Close)}" : $"H {W(c.High)} L {W(c.Low)} C {W(c.Close)}";
        // The change from the last close before this candle, shown or not.
        var hasOpen = PriceRanges.HasOpen(s);
        var previous = s.Bars.Where(b => b.X < c.From && PriceRanges.Whole(b, hasOpen)).Select(b => (double?)b.Close!.Value.ToDouble()).LastOrDefault();
        var change = previous is not { } p0 ? "Chg —" : $"Chg {(c.Close - p0 >= 0 ? "+" : "")}{W(c.Close - p0)}{PriceRanges.Percent(c.Close - p0, p0)}";
        return Make(values, Join(Join(span, c.Unchanged ? "unchanged" : null), Join(change, Volume())), Y(c.Close));
    }

    private static string? Bounds(ChartSeries s, double x)
    {
        if (s.Interval is not { } iv) return null;
        var found = iv.Bounds.Where(b => b.X == x).ToList();
        return found is [var b] && b.Lower is { } lo && b.Upper is { } hi ? $"[{s.Write(lo)}, {s.Write(hi)}] {iv.Meaning}" : "(no interval)";
    }

    // An Observation's bounds at its row, with their meaning (ADR-0304).
    private static string? ObservedBounds(SeriesInterval? iv, int point, Func<ChartValue, string> write, string which) =>
        iv is null ? null
        : iv.Bounds[point] is { Lower: { } lo, Upper: { } hi } ? $"{which}[{write(lo)}, {write(hi)}] {iv.Meaning}" : $"{which}(no interval)";

    private static string? Join(string? a, string? b) => a is null ? b : b is null ? a : $"{a}, {b}";

    private static double Mid(ChartValue lo, ChartValue hi) => (lo.ToDouble() + hi.ToDouble()) / 2;

    // Preceding: a point's value holds over (previous, point]; Following: over [point, next). At an
    // edge with no interval, the point's own value at its own position.
    private static (int Index, double From, double To) Interval(IReadOnlyList<ChartPoint> points, double x, StepSide side)
    {
        for (var i = 0; i < points.Count; i++)
        {
            if (side == StepSide.Preceding && i > 0 && points[i - 1].X < x && x <= points[i].X) return (i, points[i - 1].X, points[i].X);
            if (side == StepSide.Following && i < points.Count - 1 && points[i].X <= x && x < points[i + 1].X) return (i, points[i].X, points[i + 1].X);
        }
        if (points.Count > 0 && side == StepSide.Preceding && points[0].X == x) return (0, x, x);
        if (points.Count > 0 && side == StepSide.Following && points[^1].X == x) return (points.Count - 1, x, x);
        return (-1, 0, 0);
    }

    // An interpolated value says so (ADR-0257), with its Segment's method where one covers it.
    private static string? Method(ChartSeries s, double x)
    {
        var g = s.Segments.FirstOrDefault(g => g.From <= x && x < g.To) ?? s.Segments.LastOrDefault(g => g.To == x);
        if (g is not null) return g.Extrapolated ? $"{g.Method}, extrapolated" : s.Interpolated ? $"interpolated, {g.Method}" : g.Method;
        return s.Interpolated ? "interpolated" : null;
    }
}
