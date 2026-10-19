namespace ExChart;

/// <summary>
/// The range of the horizontal axis the user narrowed the chart to (ADR-0273), in the axis' own
/// values — a date's day number, a number, a category's index — never in pixels (ADR-0272). A null
/// end is the data's own.
/// </summary>
/// <param name="From">The first position shown, or null for the data's first.</param>
/// <param name="To">The last position shown, or null for the data's last.</param>
public sealed record ChartRange(double? From, double? To)
{
    /// <summary>A range of dates.</summary>
    public static ChartRange Dates(DateOnly? from, DateOnly? to) => new(from?.DayNumber, to?.DayNumber);
}

/// <summary>A series the user hid or showed from the legend (ADR-0271, ADR-0272).</summary>
/// <param name="Key">The series' identity, as the Chart Definition gives it.</param>
/// <param name="Hidden">Whether it is now hidden.</param>
public sealed record SeriesVisibility(string Key, bool Hidden);

