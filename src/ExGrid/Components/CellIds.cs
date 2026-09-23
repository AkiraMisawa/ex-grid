using System.Globalization;

namespace ExGrid.Components;

/// <summary>
/// The ids <c>aria-activedescendant</c> resolves to (ADR-0033/0037), written by the row and
/// named by the root. Composed in one place so the two cannot drift apart — an id the root
/// names that the row spelled differently is an accessibility tree pointing at nothing.
/// Every id carries the instance prefix, or two grids on a page would share them (ADR-0018).
/// </summary>
internal static class CellIds
{
    /// <summary>A cell: instance prefix, absolute row, column.</summary>
    internal static string Cell(string prefix, int row, int column)
        => string.Create(CultureInfo.InvariantCulture, $"{prefix}r{row}c{column}");

    /// <summary>One action's button inside a cell — written only on the chosen one, the only
    /// button the root ever names (ADR-0037).</summary>
    internal static string Action(string prefix, int row, int column, int action)
        => string.Create(CultureInfo.InvariantCulture, $"{prefix}r{row}c{column}a{action}");
}
