namespace ExGrid.Selection;

/// <summary>
/// A cell named by its position in the current order — row index (0-based in the
/// filtered-and-sorted sequence) and visible-column index — never by identity: the grid
/// does not know identities outside the Window (ADR-0011).
/// </summary>
public readonly record struct CellPosition(int Row, int Column);
