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
    }

    /// <summary>The horizontal axis.</summary>
    public ChartAxis Axis { get; }

    /// <summary>The series, in the order the legend lists them.</summary>
    public IReadOnlyList<ChartSeries> Series { get; }

    /// <summary>The Consumer's Notes: plain lines shown under the chart and written into its drawing's files (ADR-0257).</summary>
    public IReadOnlyList<string> Notes { get; }

    private static ChartPoint? EdgeWithoutInterval(ChartSeries s)
    {
        if (s.Points.Count == 0) return null;
        var edge = s.Step == StepSide.Preceding ? s.Points[0] : s.Points[^1];
        return edge.Y is null ? null : edge;
    }

    private void Validate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
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
            for (var i = 1; i < s.Points.Count; i++)
                if (!(s.Points[i - 1].X < s.Points[i].X))
                    throw new ChartRefusedException($"'{s.Name}' has its points out of order at position {s.Points[i].X}; a line joins only neighbours, so the order must be the data's.");
            foreach (var g in s.Segments)
                if (!(g.From < g.To))
                    throw new ChartRefusedException($"'{s.Name}' has an empty segment, '{g.Method}'.");
        }
    }
}
