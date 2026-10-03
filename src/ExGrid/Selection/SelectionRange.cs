namespace ExGrid.Selection;

/// <summary>
/// One selected rectangle in position space, held with normalized bounds (ADR-0011).
/// Orientation lives on the selection's Focus and Extent, not here — a rectangle
/// with a direction would duplicate that state and admit invalid values.
/// A range covers at least one cell; "nothing selected" is the empty range <em>list</em>.
/// </summary>
public readonly record struct SelectionRange
{
    /// <summary>A rectangle from its top-left cell and its size: positions are
    /// non-negative, and each count is at least one.</summary>
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

    /// <summary>The first row, as a position in the current order.</summary>
    public int TopRow { get; }

    /// <summary>The first visible column's index.</summary>
    public int LeftColumn { get; }

    /// <summary>How many rows the rectangle covers; at least one.</summary>
    public int RowCount { get; }

    /// <summary>How many columns the rectangle covers; at least one.</summary>
    public int ColumnCount { get; }

    /// <summary>The last row, inclusive.</summary>
    public int BottomRow => TopRow + RowCount - 1;

    /// <summary>The last column, inclusive.</summary>
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

    /// <summary>Whether this range is whole columns: it spans every row of
    /// <paramref name="extent"/> (ADR-0012, ADR-0016).</summary>
    public bool SpansEveryRow(GridExtent extent) => TopRow == 0 && RowCount == extent.RowCount;

    /// <summary>Whether this range is whole rows: it spans every column of
    /// <paramref name="extent"/> (ADR-0012).</summary>
    public bool SpansEveryColumn(GridExtent extent) => LeftColumn == 0 && ColumnCount == extent.ColumnCount;

    /// <summary>Normalizes any pair of opposite corners — shrinking a range back through
    /// the Focus flips it around the Focus for free (ADR-0052).</summary>
    public static SelectionRange FromCorners(CellPosition a, CellPosition b) => new(
        Math.Min(a.Row, b.Row),
        Math.Min(a.Column, b.Column),
        Math.Abs(a.Row - b.Row) + 1,
        Math.Abs(a.Column - b.Column) + 1);

    /// <summary>Whether the cell lies inside this rectangle.</summary>
    public bool Contains(CellPosition cell) =>
        cell.Row >= TopRow && cell.Row <= BottomRow &&
        cell.Column >= LeftColumn && cell.Column <= RightColumn;

    /// <summary>
    /// Removes one cell, yielding the up-to-four rectangles that remain — the Ctrl+click
    /// toggle (ADR-0012). A cell outside the range leaves it unchanged; subtracting the
    /// only cell of a 1×1 range yields nothing. The pieces come bottom to top, in the order
    /// Excel's <c>Selection.Address</c> lists them: the full-width band below the cell's row,
    /// what remains of that row to its right, then to its left, then the band above
    /// (ADR-0052, "What the third run settled").
    /// </summary>
    public IReadOnlyList<SelectionRange> Subtract(CellPosition cell)
        => Contains(cell) ? SubtractArea(new SelectionRange(cell.Row, cell.Column, 1, 1)) : [this];

    /// <summary>
    /// Removes the part of this range that <paramref name="area"/> covers, yielding the up-to-four
    /// rectangles that remain, in the order a cell's do (<see cref="Subtract(CellPosition)"/>): the
    /// full-width band below the covered rows, what remains of those rows to their right, then to
    /// their left, then the band above. A Ctrl+click on a wholly selected Heading takes out a whole
    /// column or row this way (ADR-0050, item 1). An area that misses the range leaves it
    /// unchanged; one that covers it yields nothing. Not an overload of <c>Subtract</c>: a call
    /// written <c>Subtract(new(1, 1))</c> would stop compiling.
    /// </summary>
    public IReadOnlyList<SelectionRange> SubtractArea(SelectionRange area)
    {
        var top = Math.Max(TopRow, area.TopRow);
        var bottom = Math.Min(BottomRow, area.BottomRow);
        var left = Math.Max(LeftColumn, area.LeftColumn);
        var right = Math.Min(RightColumn, area.RightColumn);
        if (top > bottom || left > right)
            return [this];

        var pieces = new List<SelectionRange>(4);
        if (bottom < BottomRow)
            pieces.Add(new(bottom + 1, LeftColumn, BottomRow - bottom, ColumnCount));
        if (right < RightColumn)
            pieces.Add(new(top, right + 1, bottom - top + 1, RightColumn - right));
        if (left > LeftColumn)
            pieces.Add(new(top, LeftColumn, bottom - top + 1, left - LeftColumn));
        if (top > TopRow)
            pieces.Add(new(TopRow, LeftColumn, top - TopRow, ColumnCount));
        return pieces;
    }
}
