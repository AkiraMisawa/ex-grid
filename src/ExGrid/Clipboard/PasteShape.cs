namespace ExGrid.Clipboard;

/// <summary>
/// The dimensions of the block on the clipboard, as the paste parser produced them
/// (ADR-0014). At least one cell each way: a shape with no rows or columns is a parser
/// bug, not a user-facing refusal — when the clipboard holds nothing tabular, the paste
/// rules are never consulted.
/// </summary>
public readonly record struct PasteShape
{
    /// <summary>A block of <paramref name="rows"/> × <paramref name="columns"/>, each at
    /// least one.</summary>
    public PasteShape(int rows, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        Rows = rows;
        Columns = columns;
    }

    /// <summary>How many rows the block has.</summary>
    public int Rows { get; }

    /// <summary>How many columns the block has.</summary>
    public int Columns { get; }

    /// <summary>A 1×1 source fills every target range — the bulk-entry shape (ADR-0011).</summary>
    public bool IsSingleCell => Rows == 1 && Columns == 1;
}
