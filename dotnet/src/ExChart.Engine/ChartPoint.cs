namespace ExChart.Engine;

/// <summary>A point of a series: its position on the horizontal axis and its value, or null where it is missing — a gap, never a zero.</summary>
/// <param name="X">The position: a day number, a number, or a category's index.</param>
/// <param name="Y">The value, or null where it is missing.</param>
public readonly record struct ChartPoint(double X, ChartValue? Y)
{
    /// <summary>What the point is, such as a name, which an Observation's tooltip says (ADR-0290); null when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>The Row Key of the row the point was read from, raised when it is clicked (ADR-0290); null when it has none.</summary>
    public object? Row { get; init; }
}

/// <summary>
/// A column an Observation's tooltip says beside its values, such as a node's probability (ADR-0321):
/// one value a point, in the points' order, written as its table holds it. The tooltip says only the
/// columns a Consumer names, never every column of the table.
/// </summary>
/// <param name="Title">What the values are, as the tooltip says it.</param>
/// <param name="Values">One value a point, written; null where the point has none.</param>
public sealed record PointDetail(string Title, IReadOnlyList<string?> Values);

/// <summary>A range of an Interpolated series and the method the Consumer used over it (ADR-0257).</summary>
/// <param name="From">Where the range starts.</param>
/// <param name="To">Where it ends.</param>
/// <param name="Method">The method's name, as the tooltip and the Notes say it.</param>
/// <param name="Extrapolated">Whether the range lies beyond the quotes.</param>
public sealed record Segment(double From, double To, string Method, bool Extrapolated = false);

/// <summary>The keys a series' look is chosen by; the same key always takes the same look (ADR-0256).</summary>
/// <param name="Colour">The colour key.</param>
/// <param name="LineStyle">The line-style key, or null for a solid line.</param>
/// <param name="Marker">The marker key, or null for the first marker.</param>
public sealed record AppearanceKeys(string Colour, string? LineStyle = null, string? Marker = null);
