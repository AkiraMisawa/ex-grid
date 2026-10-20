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
        foreach (var s in Series)
        {
            if (!ids.Add(s.Id))
                throw new ChartRefusedException($"Two series are named '{s.Id}'; each series needs an identity of its own.");
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
            if (s.MergeTo is not null && s.Kind != SeriesKind.PriceRange)
                throw new ChartRefusedException($"'{s.Name}' fixes a candle period, but is not a Price Range.");
            if (s.Interval is { } iv)
            {
                if (s.Kind != SeriesKind.Level || s.Sampler is not null)
                    throw new ChartRefusedException($"'{s.Name}' has an Interval, which belongs to a Level's points.");
                if (string.IsNullOrWhiteSpace(iv.Meaning))
                    throw new ChartRefusedException($"'{s.Name}' has an Interval that does not say what it means: name it (\"95% CI\", \"±1σ\", \"Bid–Ask\").");
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
            foreach (var g in s.Segments)
                if (!(g.From < g.To))
                    throw new ChartRefusedException($"'{s.Name}' has an empty segment, '{g.Method}'.");
        }
    }
}
