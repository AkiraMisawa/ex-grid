namespace ExGrid.Selection;

/// <summary>
/// One selected rectangle in position space, held with normalized bounds (ADR-0011).
/// Orientation lives on the selection's single Anchor/Focus pair, not here — a rectangle
/// with a direction would duplicate that state and admit invalid values.
/// A range covers at least one cell; "nothing selected" is the empty range <em>list</em>.
/// </summary>
public readonly record struct SelectionRange
{
    public SelectionRange(int topRow, int leftColumn, int rowCount, int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(topRow);
        ArgumentOutOfRangeException.ThrowIfNegative(leftColumn);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 1);
        TopRow = topRow;
        LeftColumn = leftColumn;
        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    public int TopRow { get; }

    public int LeftColumn { get; }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public int BottomRow => TopRow + RowCount - 1;

    public int RightColumn => LeftColumn + ColumnCount - 1;

    /// <summary>
    /// The area. Needs no data, which is what lets the status display show the selected
    /// count for any selection size (ADR-0014). <see cref="long"/>: a whole-grid
    /// selection over a million rows exceeds <see cref="int"/>.
    /// </summary>
    public long CellCount => (long)RowCount * ColumnCount;

    /// <summary>The row-axis shape — what the copy alignment check compares (ADR-0005).</summary>
    public RowRange RowSpan => new(TopRow, RowCount);

    /// <summary>The column-axis shape — what the copy alignment check compares (ADR-0005).</summary>
    public ColumnRange ColumnSpan => new(LeftColumn, ColumnCount);

    /// <summary>Normalizes any pair of opposite corners — shrinking a range back through
    /// its Anchor flips it around the Anchor for free (ADR-0012).</summary>
    public static SelectionRange FromCorners(CellPosition a, CellPosition b) => new(
        Math.Min(a.Row, b.Row),
        Math.Min(a.Column, b.Column),
        Math.Abs(a.Row - b.Row) + 1,
        Math.Abs(a.Column - b.Column) + 1);

    public bool Contains(CellPosition cell) =>
        cell.Row >= TopRow && cell.Row <= BottomRow &&
        cell.Column >= LeftColumn && cell.Column <= RightColumn;

    /// <summary>
    /// Removes one cell, yielding the up-to-four rectangles that remain — the Ctrl+click
    /// toggle (ADR-0012). A cell outside the range leaves it unchanged; subtracting the
    /// only cell of a 1×1 range yields nothing.
    /// </summary>
    public IReadOnlyList<SelectionRange> Subtract(CellPosition cell)
    {
        if (!Contains(cell))
            return [this];

        var pieces = new List<SelectionRange>(4);
        // Full-width bands above and below the cell's row, then what remains of that row
        // to the cell's left and right.
        if (cell.Row > TopRow)
            pieces.Add(new(TopRow, LeftColumn, cell.Row - TopRow, ColumnCount));
        if (cell.Row < BottomRow)
            pieces.Add(new(cell.Row + 1, LeftColumn, BottomRow - cell.Row, ColumnCount));
        if (cell.Column > LeftColumn)
            pieces.Add(new(cell.Row, LeftColumn, 1, cell.Column - LeftColumn));
        if (cell.Column < RightColumn)
            pieces.Add(new(cell.Row, cell.Column + 1, 1, RightColumn - cell.Column));
        return pieces;
    }
}
