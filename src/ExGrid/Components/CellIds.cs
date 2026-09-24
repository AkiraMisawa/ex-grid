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

    private static readonly InternedStrings ColumnIndexes =
        new(static column => (column + 1).ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// The 1-based <c>aria-colindex</c> of a 0-based column, interned (ADR-0027 P5). An int
    /// written straight into the attribute is boxed and turned into a string on every
    /// render of every cell and header cell.
    /// </summary>
    internal static string ColumnIndex(int column) => ColumnIndexes[column];

    /// <summary>The <c>aria-colspan</c> of a header rectangle over <paramref name="columns"/>
    /// leaves — the same interned numbers, read one down.</summary>
    internal static string Span(int columns) => ColumnIndexes[columns - 1];
}
