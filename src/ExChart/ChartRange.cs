namespace ExChart;

/// <summary>
/// The range of the horizontal axis the user narrowed the chart to (ADR-0273), in the axis' own
/// values — a date's day number, a number, a category's index — never in pixels (ADR-0272). A null
/// end is the data's own.
/// </summary>
/// <param name="From">The first position shown, or null for the data's first.</param>
/// <param name="To">The last position shown, or null for the data's last.</param>
/// <param name="YFrom">The lowest value the left axis shows, which a rectangle dragged across a scatter narrows (ADR-0290); null for the data's.</param>
/// <param name="YTo">The highest, or null for the data's.</param>
public sealed record ChartRange(double? From, double? To, double? YFrom = null, double? YTo = null)
{
    /// <summary>A range of dates.</summary>
    public static ChartRange Dates(DateOnly? from, DateOnly? to) => new(from?.DayNumber, to?.DayNumber);
}

/// <summary>A series the user hid or showed from the legend (ADR-0271, ADR-0272).</summary>
/// <param name="Key">The legend entry's key: the series' identity, as the Chart Definition gives it, or the colour key quotes and their Interpolated curve share, which hides both (ADR-0257).</param>
/// <param name="Hidden">Whether it is now hidden.</param>
public sealed record SeriesVisibility(string Key, bool Hidden);


/// <summary>A click on a point of a chart (ADR-0258, M3-Q6): the series nearest the pointer, at the position clicked.</summary>
/// <param name="SeriesId">The series' identity, as the Chart Definition gives it.</param>
/// <param name="Position">The position clicked, in the axis' own values.</param>
/// <param name="Row">The Row Key of an Observation's point clicked, as its <see cref="ExChart.Engine.ChartPoint.Row"/> gives it (ADR-0290); null otherwise.</param>
public sealed record ChartPointClick(string SeriesId, double Position, object? Row = null);
