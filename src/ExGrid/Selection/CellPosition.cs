namespace ExGrid.Selection;

/// <summary>
/// A cell named by its position in the current order — row index (0-based in the
/// filtered-and-sorted sequence) and visible-column index — never by identity: the grid
/// does not know identities outside the Window (ADR-0011).
/// </summary>
/// <param name="Row">The row's position in the filtered-and-sorted sequence, from 0.</param>
/// <param name="Column">The visible column's index, from 0.</param>
public readonly record struct CellPosition(int Row, int Column);
