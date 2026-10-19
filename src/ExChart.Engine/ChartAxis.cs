namespace ExChart.Engine;

/// <summary>
/// The horizontal axis: what its positions are and how it places them (ADR-0251). The scale is
/// declared in the Chart Definition, with a default by type: dates and numbers proportional, labels
/// as categories.
/// </summary>
public sealed record ChartAxis
{
    /// <summary>What the positions are.</summary>
    public required AxisValues Values { get; init; }

    /// <summary>The axis' title.</summary>
    public string Title { get; init; } = "";

    /// <summary>How positions are placed; null takes the default for <see cref="Values"/>.</summary>
    public AxisScale? Scale { get; init; }

    /// <summary>The categories' labels in their order, for <see cref="AxisValues.Labels"/>; position i is label i.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>
    /// Labels for declared positions, such as "6M" at 0.5, shown in place of the number; a Number
    /// axis drawn as categories places these positions evenly, in order.
    /// </summary>
    public IReadOnlyList<(double Position, string Label)> Named { get; init; } = [];

    /// <summary>How many days a year has when an Amount per period on a Date axis is drawn per year.</summary>
    public double DaysPerYear { get; init; } = 365.25;

    /// <summary>The scale, defaults applied.</summary>
    public AxisScale EffectiveScale => Scale ?? (Values == AxisValues.Labels ? AxisScale.Categories : AxisScale.Proportional);

    /// <summary>An axis of dates.</summary>
    public static ChartAxis Dates(string title = "") => new() { Values = AxisValues.Dates, Title = title };

    /// <summary>An axis of numbers.</summary>
    public static ChartAxis Numbers(string title = "") => new() { Values = AxisValues.Numbers, Title = title };

    /// <summary>An axis of labels in a declared order.</summary>
    public static ChartAxis Categories(IReadOnlyList<string> labels, string title = "") =>
        new() { Values = AxisValues.Labels, Labels = labels, Title = title };

    /// <summary>The length in years of the interval between two positions, which an Amount per period is divided by.</summary>
    internal double Years(double from, double to) => Values switch
    {
        AxisValues.Dates => (to - from) / DaysPerYear,
        AxisValues.Numbers => to - from,
        _ => throw new InvalidOperationException("A category axis has no length."),
    };
}
