namespace ExGrid.Clipboard;

/// <summary>
/// A coordinate inside the copied block — (0, 0) is its top-left. Deliberately not
/// <see cref="Selection.CellPosition"/>: that names a position in the grid's current
/// order, which is a different space (ADR-0011).
/// </summary>
/// <param name="Row">The row within the block, from 0.</param>
/// <param name="Column">The column within the block, from 0.</param>
public readonly record struct SourceCell(int Row, int Column);
