using System.Globalization;
using AngleSharp.Dom;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// A painted row's position, read back from its <c>aria-rowindex</c>, which counts the header's
/// rows before the data rows (ADR-0033, decided with the user 2026-10-12): the leaf header row,
/// a row per tier of Header Groups above it, or none while the header is hidden.
/// </summary>
internal static class AriaRows
{
    /// <summary>How many rows the header of the grid holding <paramref name="element"/> counts:
    /// its own header, never one of a grid nested in its cells.</summary>
    public static int HeaderRowsOf(IElement element)
    {
        var root = element.ClassList.Contains("ex-grid") ? element : element.Closest(".ex-grid")!;
        var header = Child(Child(Child(root, "ex-scroller"), "ex-spacer"), "ex-header");
        return header is null ? 0
            : header.GetAttribute("role") == "row" ? 1
            : header.QuerySelectorAll("[role=row]").Length;
    }

    private static IElement? Child(IElement? parent, string className)
        => parent?.Children.FirstOrDefault(child => child.ClassList.Contains(className));

    /// <summary>A data row's (or a Placeholder's) 0-based position in the whole result.</summary>
    public static int PositionOf(IElement row) => PositionOf(row, row);

    /// <summary>A data row's position, its header counted in <paramref name="grid"/>: for a row
    /// read from its own component's markup, which holds no root.</summary>
    public static int PositionOf(IElement row, IElement grid)
        => int.Parse(row.GetAttribute("aria-rowindex")!, CultureInfo.InvariantCulture) - 1 - HeaderRowsOf(grid);
}
