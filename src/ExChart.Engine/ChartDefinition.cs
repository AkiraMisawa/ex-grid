namespace ExChart.Engine;

/// <summary>A chart that cannot be drawn as it was asked for, said by name (spine rule 1).</summary>
public sealed class ChartRefusedException(string message) : Exception(message);

/// <summary>
/// What a chart is of, in the data's own terms (ADR-0251): its horizontal axis, its series and the
/// Consumer's Notes (ADR-0257). A definition that cannot be drawn honestly is refused when it is made,
/// with the sentence that says why.
/// </summary>
public sealed record ChartDefinition
{
    /// <summary>A chart of the given series on the given axis.</summary>
    /// <exception cref="ChartRefusedException">The definition cannot be drawn as asked.</exception>
    public ChartDefinition(ChartAxis axis, IReadOnlyList<ChartSeries> series, IReadOnlyList<string>? notes = null)
    {
        Axis = axis;
        Series = series;
        Notes = notes ?? [];
        Validate();
        ValidatePacked();
    }

    /// <summary>The horizontal axis.</summary>
    public ChartAxis Axis { get; }

    /// <summary>The series, in the order the legend lists them.</summary>
    public IReadOnlyList<ChartSeries> Series { get; }

    /// <summary>The Consumer's Notes: plain lines shown under the chart and written into its drawing's files (ADR-0257).</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>
    /// Every function of the Consumer's own script the definition names (ADR-0420 section 3), the axis' first and then
    /// each series' format and sampler; empty for a definition a chart drawn in .NET can draw.
    /// </summary>
    public IEnumerable<ScriptFunction> ScriptFunctions =>
        new[] { Axis.FormatScript }.Concat(Series.SelectMany(s => new[] { s.FormatScript, s.Sampler?.Script })).OfType<ScriptFunction>();

    private void ValidatePacked()
    {
        if (!Axis.Packed) return;
        if (Axis.Values != AxisValues.Dates)
            throw new ChartRefusedException("Only a Dates axis packs its sessions.");
        var candles = ChartScene.PackedSessions(this).ToHashSet();
        if (candles.Count == 0)
            throw new ChartRefusedException("A packed axis takes its sessions from a Price Range, and the chart has none.");
        foreach (var s in Series.Where(s => s.Kind != SeriesKind.PriceRange))
        {
            if (s.Sampler is not null)
                throw new ChartRefusedException($"'{s.Name}' is a function, which has no values between the packed sessions.");
            if (s.Points.Where(p => !candles.Contains(p.X)).Select(p => (double?)p.X).FirstOrDefault() is { } x)
                throw new ChartRefusedException($"'{s.Name}' has a point at {Axis.Write(x)}, where no candle stands: a packed axis has nowhere true to put it.");
        }
    }

    private static ChartPoint? EdgeWithoutInterval(ChartSeries s)
    {
        if (s.Points.Count == 0) return null;
        var edge = s.Step == StepSide.Preceding ? s.Points[0] : s.Points[^1];
        return edge.Y is null ? null : edge;
    }

    private void Validate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var logarithmic = Axis.EffectiveScale == AxisScale.Logarithmic;
        if (logarithmic && Axis.Values != AxisValues.Numbers)
            throw new ChartRefusedException("Only a number axis can be logarithmic.");
        ChartCodes.CheckAxis(Axis);
        foreach (var s in Series)
        {
            if (!ids.Add(s.Id))
                throw new ChartRefusedException($"Two series are named '{s.Id}'; each series needs an identity of its own.");
            ChartCodes.CheckSeries(s);
            if (s.Kind == SeriesKind.Undeclared)
                throw new ChartRefusedException($"'{s.Name}' declares no Series Kind: say whether it is a Level, a Rate over an interval or an Amount per period.");
            if (s.Kind == SeriesKind.AmountPerPeriod && s.FromZero == false)
                throw new ChartRefusedException($"'{s.Name}' is an Amount per period, which is always drawn from zero.");
            if (s.Kind == SeriesKind.AmountPerPeriod && Axis.Values == AxisValues.Labels)
                throw new ChartRefusedException($"'{s.Name}' is an Amount per period, but categories have no length to draw it per year over.");
            if (s.Sampler is not null && s.Points.Count > 0)
                throw new ChartRefusedException($"'{s.Name}' is handed in both as points and as a function; give one.");
            if (s.Sampler is not null && Axis.EffectiveScale == AxisScale.Categories)
                throw new ChartRefusedException($"'{s.Name}' is a function, which has no values between categories to sample.");
            if (s.Sampler is not null && s.Kind != SeriesKind.Level)
                throw new ChartRefusedException($"'{s.Name}' is a function, which is drawn as a Level; a step is handed in as its points.");
            if (s.Kind == SeriesKind.AmountPerPeriod && EdgeWithoutInterval(s) is { } edge)
                throw new ChartRefusedException($"'{s.Name}' has an amount at {edge.X} with no interval to draw it per year over: an Amount per period's {(s.Step == StepSide.Preceding ? "first" : "last")} point only bounds the interval next to it, and holds no amount.");
            if (s.Sampler is { } f && !(f.From < f.To))
                throw new ChartRefusedException($"'{s.Name}' is sampled over an empty range.");
            if (s.Kind == SeriesKind.Observation && Axis.EffectiveScale == AxisScale.Categories)
                throw new ChartRefusedException($"'{s.Name}' is an Observation, which needs a value axis across: numbers or dates, not categories.");
            if (s.Kind == SeriesKind.Observation && s.Sampler is not null)
                throw new ChartRefusedException($"'{s.Name}' is an Observation, which is handed in as its points, not as a function.");
            if (logarithmic && s.Points.Any(p => !(p.X > 0)))
                throw new ChartRefusedException($"'{s.Name}' has a point at {s.Points.First(p => !(p.X > 0)).X}, which a logarithmic axis cannot place: every position must be above zero.");
            if (logarithmic && s.Sampler is { } sampled && !(sampled.From > 0))
                throw new ChartRefusedException($"'{s.Name}' is sampled from {sampled.From}, which a logarithmic axis cannot place: every position must be above zero.");
            // An Observation's points are neither ordered nor unique in x (ADR-0290).
            for (var i = 1; i < s.Points.Count && s.Kind != SeriesKind.Observation; i++)
                if (!(s.Points[i - 1].X < s.Points[i].X))
                    throw new ChartRefusedException($"'{s.Name}' has its points out of order at position {s.Points[i].X}; a line joins only neighbours, so the order must be the data's.");
            if (s.Kind == SeriesKind.PriceRange)
            {
                if (Axis.Values != AxisValues.Dates)
                    throw new ChartRefusedException($"'{s.Name}' is a Price Range, which stands on a Dates axis: its candles merge into weeks and months.");
                if (s.Sampler is not null || s.Points.Count != s.Bars.Count)
                    throw new ChartRefusedException($"'{s.Name}' is a Price Range, handed in as its rows (open, high, low, close), not as points.");
                foreach (var b in s.Bars)
                    if (PriceRanges.Disagreement(s, Axis, b) is { } refusal)
                        throw new ChartRefusedException(refusal);
            }
            else if (s.Bars.Count > 0)
                throw new ChartRefusedException($"'{s.Name}' has rows of open, high, low and close, but is not a Price Range.");
            if (PointScales.Refusal(s) is { } scaleRefusal)
                throw new ChartRefusedException(scaleRefusal);
            foreach (var detail in s.Details)
            {
                if (s.Kind != SeriesKind.Observation)
                    throw new ChartRefusedException($"'{s.Name}' says {detail.Title} in its tooltip, which belongs to marks drawn one a row: an observation.");
                if (detail.Values.Count != s.Points.Count)
                    throw new ChartRefusedException($"'{s.Name}' has {s.Points.Count} points and {detail.Values.Count} values of {detail.Title}: its tooltip says one a point.");
            }
            if (ExChart.Engine.Edges.Refusal(s) is { } edgeRefusal)
                throw new ChartRefusedException(edgeRefusal);
            if (s.MergeTo is not null && s.Kind != SeriesKind.PriceRange)
                throw new ChartRefusedException($"'{s.Name}' fixes a candle period, but is not a Price Range.");
            if (s.Interval is { } iv)
            {
                if (s.Kind is not (SeriesKind.Level or SeriesKind.Observation) || s.Sampler is not null)
                    throw new ChartRefusedException($"'{s.Name}' has an Interval, which belongs to a Level's or an Observation's points.");
                ValidateInterval(s, iv, "");
            }
            if (s.XInterval is { } xv)
            {
                if (s.Kind != SeriesKind.Observation)
                    throw new ChartRefusedException($"'{s.Name}' has a horizontal Interval, which belongs to an Observation's points.");
                ValidateInterval(s, xv, "horizontal ");
            }
            foreach (var g in s.Segments)
                if (!(g.From < g.To))
                    throw new ChartRefusedException($"'{s.Name}' has an empty segment, '{g.Method}'.");
        }
    }

    // A Level's bounds are found by position; an Observation's are one a row, in the points' order (ADR-0303, ADR-0304).
    private void ValidateInterval(ChartSeries s, SeriesInterval iv, string which)
    {
        if (string.IsNullOrWhiteSpace(iv.Meaning))
            throw new ChartRefusedException($"'{s.Name}' has a {which}Interval that does not say what it means: name it (\"95% CI\", \"±1σ\", \"Bid–Ask\").");
        string Write(ChartValue v) => which.Length > 0 ? Axis.Write(v.ToDouble()) : s.Write(v);
        if (s.Kind == SeriesKind.Observation)
        {
            if (iv.Style != IntervalStyle.Whiskers)
                throw new ChartRefusedException($"'{s.Name}' draws its {which}Interval as a band, which joins neighbours: an Observation's points have none, so it takes whiskers.");
            if (iv.Bounds.Count != s.Points.Count)
                throw new ChartRefusedException($"'{s.Name}' has {s.Points.Count} points and {iv.Bounds.Count} {which}bounds: an Observation's bounds are one a row, in its points' order.");
            for (var i = 0; i < s.Points.Count; i++)
            {
                var b = iv.Bounds[i];
                var at = s.Points[i].Name ?? $"row {i + 1}";
                if (b.X != s.Points[i].X && !(double.IsNaN(b.X) && double.IsNaN(s.Points[i].X)))
                    throw new ChartRefusedException($"'{s.Name}' at {at}: the {which}bound stands at {Axis.Write(b.X)}, but its point at {Axis.Write(s.Points[i].X)}.");
                if (b.Lower is { } lo && b.Upper is { } hi && lo.ToDouble() > hi.ToDouble())
                    throw new ChartRefusedException($"'{s.Name}' at {at}: the lower bound {Write(lo)} of its {iv.Meaning} is above the upper {Write(hi)}.");
            }
            return;
        }
        var xs = s.Points.Select(p => p.X).ToHashSet();
        var seen = new HashSet<double>();
        foreach (var b in iv.Bounds)
        {
            if (!xs.Contains(b.X))
                throw new ChartRefusedException($"'{s.Name}' has an interval at {Axis.Write(b.X)}, where it has no point.");
            if (!seen.Add(b.X))
                throw new ChartRefusedException($"'{s.Name}' has two intervals at {Axis.Write(b.X)}.");
            if (b.Lower is { } lo && b.Upper is { } hi && lo.ToDouble() > hi.ToDouble())
                throw new ChartRefusedException($"'{s.Name}' at {Axis.Write(b.X)}: the lower bound {s.Write(lo)} of its {iv.Meaning} is above the upper {s.Write(hi)}.");
        }
    }
}
