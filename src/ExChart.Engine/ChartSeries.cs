using System.Globalization;

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
    /// away from zero — a rate, a volatility — says false. An Amount per period always reaches zero; an
    /// Observation reaches it only when it says true (ADR-0290).
    /// </summary>
    public bool? FromZero { get; init; }

    /// <summary>Whether it is values the Consumer computed between quotes rather than quotes (ADR-0257).</summary>
    public bool Interpolated { get; init; }

    /// <summary>The ranges of an Interpolated series and each one's method (ADR-0257).</summary>
    public IReadOnlyList<Segment> Segments { get; init; } = [];

    /// <summary>Its points, in increasing position; empty when it is sampled.</summary>
    public IReadOnlyList<ChartPoint> Points { get; init; } = [];

    /// <summary>An Observation's colour read from a column, one value a point, with its key always drawn (ADR-0320).</summary>
    public PointColour? ColourBy { get; init; }

    /// <summary>An Observation's size read from a column, one value a point, with its key always drawn (ADR-0320).</summary>
    public PointSize? SizeBy { get; init; }

    /// <summary>An Observation's columns its tooltip says beside its values, in order (ADR-0321).</summary>
    public IReadOnlyList<PointDetail> Details { get; init; } = [];

    /// <summary>
    /// An Observation's edges, each joining two of its points by their <see cref="ChartPoint.Row"/>, its
    /// nodes (ADR-0321). ExChart lays out nothing: a node stands where its point does.
    /// </summary>
    public IReadOnlyList<ChartEdge> Edges { get; init; } = [];

    /// <summary>
    /// A Price Range's rows (ADR-0300). Setting them also sets <see cref="Points"/> to each row's close,
    /// or a gap where the row is not whole, so the series' domain, positions, CSV and grid bridge read
    /// it as they read a Level's.
    /// </summary>
    public IReadOnlyList<PriceBar> Bars
    {
        get => _bars;
        init
        {
            _bars = value;
            var hasOpen = PriceRanges.HasOpen(value);
            Points = value.Select(b => new ChartPoint(b.X, PriceRanges.Whole(b, hasOpen) ? b.Close : null)).ToArray();
        }
    }

    private readonly IReadOnlyList<PriceBar> _bars = [];

    /// <summary>
    /// A Price Range's period fixed by the Consumer (ADR-0302); null merges into the first of the
    /// market's periods that fits the plot.
    /// </summary>
    public CandlePeriod? MergeTo { get; init; }

    /// <summary>
    /// The series' vertical Interval, its bounds and their meaning (ADR-0303): a Level's, by position,
    /// or an Observation's, one bound a row in the points' order (ADR-0304).
    /// </summary>
    public SeriesInterval? Interval { get; init; }

    /// <summary>An Observation's horizontal Interval, one bound a row in the points' order, its bounds in the axis' units across (ADR-0304).</summary>
    public SeriesInterval? XInterval { get; init; }

    /// <summary>
    /// What each pixel its Chart Source thinned held (ADR-0344): its points are the ones kept, and the
    /// tooltip says the range and the count of every row under a pixel. Empty for a series handed in whole.
    /// </summary>
    public IReadOnlyList<PixelSummary> Thinned { get; init; } = [];

    /// <summary>The function it is sampled from, or null when it is handed in as points.</summary>
    public SeriesSampler? Sampler { get; init; }

    /// <summary>
    /// Writes one of its values as it was handed in (a boxed <see cref="decimal"/> or
    /// <see cref="double"/>): the column's own format, so the tooltip shows the cell's digits
    /// (ADR-0250, ADR-0251). Null writes the value invariantly.
    /// </summary>
    public Func<object, string>? Format { get; init; }

    /// <summary>
    /// An Excel number format code in the invariant spelling that writes every value (ADR-0420): the ones
    /// handed in and the ones the chart computes, a candle's change and a merged candle's volume, written
    /// invariantly. It stands where a <see cref="Format"/> would, and is what a chart drawn in the browser
    /// takes, where no .NET function can be called. A code the reader does not read, a date code, and one
    /// beside a <see cref="Format"/> are refused as the definition is checked.
    /// </summary>
    public string? FormatCode
    {
        get => _formatCode;
        init => (_formatCode, _code, CodeRefusal) = ChartCodes.Read(value);
    }

    private readonly string? _formatCode;
    private readonly ExGrid.Data.FormatCode? _code;

    // Why FormatCode cannot be read, a phrase completing "the format code …"; null when it can or there is none.
    internal string? CodeRefusal { get; private init; }

    // The code, refused by name where it cannot be read, so that no value is written another way (spine rule 1).
    private ExGrid.Data.FormatCode? ReadCode =>
        (CodeRefusal, _formatCode) is ({ } refusal, { } code) ? throw new ChartRefusedException(ChartCodes.SeriesRefusal(Name, code, refusal)) : _code;

    // Whether FormatCode writes dates, which a series' values are not.
    internal bool CodeWritesDates => ReadCode is { IsDate: true };

    /// <summary>Writes <paramref name="value"/> through <see cref="Format"/> or <see cref="FormatCode"/>.</summary>
    public string Write(ChartValue value) => Format is { } f ? f(value.Value)
        : ReadCode is { } code ? code.Format(value.Value, CultureInfo.InvariantCulture) : value.ToInvariantString();

    /// <summary>The keys its look is chosen by, its <see cref="Id"/> standing for a colour key it does not set.</summary>
    public AppearanceKeys EffectiveKeys => Keys ?? new AppearanceKeys(Id);

    /// <summary>Whether its axis reaches zero, defaults applied.</summary>
    public bool ReachesZero => Kind == SeriesKind.AmountPerPeriod || (Kind is SeriesKind.Observation or SeriesKind.PriceRange ? FromZero == true : FromZero != false);

    /// <summary>A series of points.</summary>
    public static ChartSeries Of(string id, string name, SeriesKind kind, IEnumerable<ChartPoint> points) =>
        new() { Id = id, Name = name, Kind = kind, Points = points.ToArray() };

    /// <summary>A Price Range of these rows (ADR-0300).</summary>
    public static ChartSeries Candles(string id, string name, IEnumerable<PriceBar> bars) =>
        new() { Id = id, Name = name, Kind = SeriesKind.PriceRange, Bars = bars.ToArray() };

    /// <summary>A series sampled from the Consumer's function over a range.</summary>
    public static ChartSeries Sampled(string id, string name, SeriesKind kind, double from, double to, Func<double, ChartValue?> valueAt) =>
        new() { Id = id, Name = name, Kind = kind, Sampler = new SeriesSampler(from, to, valueAt) };
}
