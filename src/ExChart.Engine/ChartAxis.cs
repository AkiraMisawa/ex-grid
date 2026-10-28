using System.Globalization;
using Code = ExGrid.Data.FormatCode;

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

    /// <summary>
    /// The sessions packed (ADR-0301): each position a Price Range has is one evenly spaced slot, the
    /// days nobody traded skipped, and the title says so. By default a Dates axis is calendar time.
    /// </summary>
    public bool Packed { get; init; }

    /// <summary>
    /// Writes a position, as the column the positions came from writes it (ADR-0250). Null writes a
    /// date as <c>yyyy-MM-dd</c>, a label as itself, a named position by its name and a number
    /// invariantly.
    /// </summary>
    public Func<double, string>? Format { get; init; }

    /// <summary>
    /// An Excel number format code in the invariant spelling that writes every position (ADR-0420): each
    /// tick, the position under the pointer and a range's ends, a date code on a Dates axis and a number code
    /// on a Numbers axis, written invariantly. It stands where a <see cref="Format"/> would, and is what a
    /// chart drawn in the browser takes, where no .NET function can be called. A code the reader does not
    /// read, one beside a <see cref="Format"/>, and one on a Labels axis are refused as the definition is checked.
    /// </summary>
    public string? FormatCode
    {
        get => _formatCode;
        init => (_formatCode, _code, CodeRefusal) = ChartCodes.Read(value);
    }

    private readonly string? _formatCode;
    private readonly Code? _code;

    // Why FormatCode cannot be read, a phrase completing "the format code …"; null when it can or there is none.
    internal string? CodeRefusal { get; private init; }

    // The code, refused by name where it cannot be read, so that no writing falls back to the axis' own (spine rule 1).
    private Code? ReadCode => (CodeRefusal, _formatCode) is ({ } refusal, { } code) ? throw new ChartRefusedException(ChartCodes.AxisRefusal(code, refusal)) : _code;

    /// <summary>Whether <see cref="Format"/> or <see cref="FormatCode"/> writes the positions, in place of the axis' own writing.</summary>
    internal bool Writes => Format is not null || ReadCode is not null;

    // Whether FormatCode writes the ticks, which a Format, written for the values a column holds, leaves to the
    // axis' own labels (ADR-0420).
    internal bool CodeWritesTicks => ReadCode is not null;

    // Whether FormatCode writes dates; null when there is none.
    internal bool? CodeWritesDates => ReadCode?.IsDate;

    /// <summary>Writes <paramref name="position"/> through <see cref="Format"/> or <see cref="FormatCode"/>.</summary>
    public string Write(double position) => Format is { } f ? f(position) : ReadCode is { } code ? ChartCodes.WritePosition(code, Values, position) : Values switch
    {
        AxisValues.Dates => DateOnly.FromDayNumber((int)position).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        AxisValues.Labels => position >= 0 && position < Labels.Count && position == Math.Floor(position) ? Labels[(int)position] : position.ToString("R", CultureInfo.InvariantCulture),
        _ => Named.FirstOrDefault(n => n.Position == position).Label ?? position.ToString("R", CultureInfo.InvariantCulture),
    };

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
