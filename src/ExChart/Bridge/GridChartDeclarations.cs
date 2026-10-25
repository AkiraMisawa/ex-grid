using global::ExChart.Engine;

namespace ExChart;

/// <summary>
/// What the Consumer declares for a grid's selections to be charted (ADR-0251, ADR-0258). Nothing
/// here is read off a header or guessed from a selection's shape: "6m" is a header's text, 0.5 years
/// is what the Consumer says it stands for.
/// </summary>
/// <typeparam name="TRow">The grid's row type.</typeparam>
public sealed record GridChartDeclarations<TRow>
{
    /// <summary>A row's identity (ADR-0140), which a chart follows through a sort, a filter and a recalculation.</summary>
    public required Func<TRow, object> RowKey { get; init; }

    /// <summary>
    /// The row a Row Key names in the Consumer's data now, or null once it no longer exists. A chart
    /// reads its values here, never from the grid's Window, so a row only filtered out of the grid is
    /// not reported as gone (ADR-0258, M3-Q7).
    /// </summary>
    public required Func<object, TRow?> RowOf { get; init; }

    /// <summary>Each column's Series Kind by its name (ADR-0253). A column with none declared is refused when it is charted.</summary>
    public Func<string, SeriesKind>? KindOf { get; init; }

    /// <summary>
    /// Whether each column's values are read from zero, by its name (ADR-0253, ADR-0258): false for a
    /// level read away from zero, such as rates between 3.0% and 3.6%; null, or no answer, leaves the
    /// Series Kind's default. A series in rows takes its columns' declaration, which they share.
    /// </summary>
    public Func<string, bool?>? FromZeroOf { get; init; }

    /// <summary>
    /// The number columns that are an axis, by name. A date column is always one; a number column is
    /// one only when it is named here.
    /// </summary>
    public IReadOnlySet<string> AxisColumns { get; init; } = new HashSet<string>();

    /// <summary>A column's declared position along a curve's axis (a tenor's years), or null for a column that has none.</summary>
    public Func<string, double?>? PositionOf { get; init; }

    /// <summary>The title of the declared positions' axis, such as "Tenor (years)".</summary>
    public string PositionTitle { get; init; } = "";

    /// <summary>Whether the declared positions are drawn proportional to their values or as categories (ADR-0251).</summary>
    public AxisScale PositionScale { get; init; } = AxisScale.Proportional;

    /// <summary>A row's name in the legend and the tooltip, for series in rows; the Row Key's text when null.</summary>
    public Func<TRow, string>? RowName { get; init; }

    /// <summary>
    /// The orientation every selection is charted in, when the Consumer fixes it; null lets the grid's
    /// declarations decide (ADR-0258).
    /// </summary>
    public ChartOrientation? Orientation { get; init; }
}
