namespace ExGrid.Selection;

/// <summary>
/// A contiguous span of visible columns — the column-axis shape of a
/// <see cref="SelectionRange"/> (ADR-0011). The row-axis sibling reuses
/// <see cref="RowRange"/>.
/// </summary>
/// <param name="Start">The first visible column's index.</param>
/// <param name="Count">How many columns the span covers.</param>
public readonly record struct ColumnRange(int Start, int Count);
