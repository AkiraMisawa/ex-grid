namespace ExSheet.Engine;

/// <summary>
/// Excel's thirteen line styles for a Border (ADR-0071), and <see cref="None"/>. No diagonals.
/// </summary>
public enum BorderLineStyle
{
    /// <summary>No line on that side.</summary>
    None,

    /// <summary>Hair: Excel's finest line.</summary>
    Hair,

    /// <summary>Thin.</summary>
    Thin,

    /// <summary>Medium.</summary>
    Medium,

    /// <summary>Thick.</summary>
    Thick,

    /// <summary>Double.</summary>
    Double,

    /// <summary>Dotted.</summary>
    Dotted,

    /// <summary>Dashed.</summary>
    Dashed,

    /// <summary>Dash-dot.</summary>
    DashDot,

    /// <summary>Dash-dot-dot.</summary>
    DashDotDot,

    /// <summary>Medium dashed.</summary>
    MediumDashed,

    /// <summary>Medium dash-dot.</summary>
    MediumDashDot,

    /// <summary>Medium dash-dot-dot.</summary>
    MediumDashDotDot,

    /// <summary>Slanted dash-dot.</summary>
    SlantedDashDot,
}

/// <summary>
/// A Border (<c>CONTEXT.md</c>; ADR-0071): the line on one side of a cell, in one of Excel's
/// thirteen line styles and a colour, or <see cref="None"/>. The default value is <see cref="None"/>.
/// </summary>
public readonly record struct BorderLine
{
    /// <summary>A line in <paramref name="style"/> and <paramref name="colour"/>. <see cref="BorderLineStyle.None"/> is no line, and has no colour.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The line style is not one.</exception>
    public BorderLine(BorderLineStyle style, CellColour colour = default)
    {
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style), style, "Not a line style.");
        Style = style;
        Colour = style == BorderLineStyle.None ? CellColour.Automatic : colour;
    }

    /// <summary>No line.</summary>
    public static BorderLine None => default;

    /// <summary>The line style; <see cref="BorderLineStyle.None"/> for no line.</summary>
    public BorderLineStyle Style { get; }

    /// <summary>The line's colour; Automatic for no line.</summary>
    public CellColour Colour { get; }

    /// <summary>Whether this is no line.</summary>
    public bool IsNone => Style == BorderLineStyle.None;
}

/// <summary>
/// A cell's Borders (ADR-0071): the line on each of its four sides. Each cell records its own four
/// sides, as Excel's files do, and the edge two cells share shows one line from either side: the
/// upper cell's, or the left cell's for a vertical edge, where both record one, and otherwise
/// whichever does (the twelfth Windows run). A border command records the edge on the cells it sets
/// and clears the neighbour's record of it, so the later setting wins from either side (the eleventh
/// run, case 7). The default value is <see cref="None"/>.
/// </summary>
/// <param name="Top">The line on the top side.</param>
/// <param name="Bottom">The line on the bottom side.</param>
/// <param name="Left">The line on the left side.</param>
/// <param name="Right">The line on the right side.</param>
public readonly record struct CellBorders(BorderLine Top = default, BorderLine Bottom = default, BorderLine Left = default, BorderLine Right = default)
{
    /// <summary>No line on any side.</summary>
    public static CellBorders None => default;
}

/// <summary>
/// The lines drawn along a range's four outer edges, each line once per edge
/// (<see cref="Sheet.GetEdgeLines"/>): what Format Cells shows on a range's outline (ADR-0071; the
/// fourteenth Windows run, case 13).
/// </summary>
/// <param name="Top">The lines along the top edge.</param>
/// <param name="Bottom">The lines along the bottom edge.</param>
/// <param name="Left">The lines along the left edge.</param>
/// <param name="Right">The lines along the right edge.</param>
public sealed record RangeEdgeLines(IReadOnlySet<BorderLine> Top, IReadOnlySet<BorderLine> Bottom, IReadOnlySet<BorderLine> Left, IReadOnlySet<BorderLine> Right);
