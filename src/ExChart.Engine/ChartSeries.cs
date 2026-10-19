namespace ExChart.Engine;

/// <summary>
/// A Consumer's curve as a function, asked by the engine at about one position per device pixel as it
/// draws (ADR-0257). The function is the Consumer's interpolation; the engine only chooses where to ask.
/// </summary>
/// <param name="From">The first position the function is asked at.</param>
/// <param name="To">The last.</param>
/// <param name="ValueAt">The Consumer's curve: the value at a position, or null where it has none.</param>
public sealed record SeriesSampler(double From, double To, Func<double, ChartValue?> ValueAt);

/// <summary>
/// One series of a chart, in the data's own terms (ADR-0251): its points, or a function to sample, and
/// what they are — its <see cref="SeriesKind"/> — with where it is drawn and the keys its look is
/// chosen by.
/// </summary>
public sealed record ChartSeries
{
    /// <summary>The series' identity in the chart: unique, and stable through a sort or a recalculation.</summary>
    public required string Id { get; init; }

    /// <summary>Its name in the legend and the tooltip.</summary>
    public required string Name { get; init; }

    /// <summary>What its numbers are; a series that declares none is refused (ADR-0253).</summary>
    public required SeriesKind Kind { get; init; }

    /// <summary>Which interval a step's point stands for; read only for a step.</summary>
    public StepSide Step { get; init; } = StepSide.Preceding;

    /// <summary>The vertical axis it reads on.</summary>
    public AxisSide Axis { get; init; } = AxisSide.Left;

    /// <summary>The panel it is drawn in; series naming the same panel share it.</summary>
    public string Panel { get; init; } = "";

    /// <summary>The keys its look is chosen by (ADR-0256); by default its colour key is its <see cref="Id"/>.</summary>
    public AppearanceKeys? Keys { get; init; }

    /// <summary>
    /// Whether its axis reaches zero. Null takes the default, which is yes (ADR-0253); a series read
    /// away from zero — a rate, a volatility — says false. An Amount per period always reaches zero.
    /// </summary>
    public bool? FromZero { get; init; }

    /// <summary>Whether it is values the Consumer computed between quotes rather than quotes (ADR-0257).</summary>
    public bool Interpolated { get; init; }

    /// <summary>The ranges of an Interpolated series and each one's method (ADR-0257).</summary>
    public IReadOnlyList<Segment> Segments { get; init; } = [];

    /// <summary>Its points, in increasing position; empty when it is sampled.</summary>
    public IReadOnlyList<ChartPoint> Points { get; init; } = [];

    /// <summary>The function it is sampled from, or null when it is handed in as points.</summary>
    public SeriesSampler? Sampler { get; init; }

    /// <summary>The keys its look is chosen by, its <see cref="Id"/> standing for a colour key it does not set.</summary>
    public AppearanceKeys EffectiveKeys => Keys ?? new AppearanceKeys(Id);

    /// <summary>Whether its axis reaches zero, defaults applied.</summary>
    public bool ReachesZero => Kind == SeriesKind.AmountPerPeriod || FromZero != false;

    /// <summary>A series of points.</summary>
    public static ChartSeries Of(string id, string name, SeriesKind kind, IEnumerable<ChartPoint> points) =>
        new() { Id = id, Name = name, Kind = kind, Points = points.ToArray() };

    /// <summary>A series sampled from the Consumer's function over a range.</summary>
    public static ChartSeries Sampled(string id, string name, SeriesKind kind, double from, double to, Func<double, ChartValue?> valueAt) =>
        new() { Id = id, Name = name, Kind = kind, Sampler = new SeriesSampler(from, to, valueAt) };
}
