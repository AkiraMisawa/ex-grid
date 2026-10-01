namespace ExGrid.Cells;

/// <summary>
/// Where a grid pointed at from outside draws its dashes (ADR-0058, "What is drawn"): over one cell,
/// or down the body of one column, in the Focus outline's colour. It is how the Consumer shows which
/// cell or column a press handed over was written for. The dashes are one element in the selection
/// overlay, cut to the painted rows as a Reference Outline is (ADR-0057); a row that is not painted
/// draws nothing, and the grid never scrolls to show one. Asked through
/// <see cref="GridPointedAt{TRow}.Dashes"/>.
/// </summary>
public sealed record GridPointDashes<TRow>
    where TRow : class
{
    private GridPointDashes(Func<TRow, bool>? row, string column)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        Row = row;
        Column = column;
    }

    /// <summary>Dashes over the cell of <paramref name="row"/> in <paramref name="column"/>: the row by
    /// its identity, the instance a press handed over carried. After a reorder they are over the same
    /// row, wherever the order put it; a Window that no longer holds that instance dashes nothing.</summary>
    /// <param name="row">The row instance, compared by reference as Row Identity is.</param>
    /// <param name="column">The column's name, as <see cref="GridColumn{TRow}.Name"/> names it.</param>
    /// <exception cref="ArgumentNullException">The row or the name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public static GridPointDashes<TRow> OverCell(TRow row, string column)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new GridPointDashes<TRow>(candidate => ReferenceEquals(candidate, row), column);
    }

    /// <summary>Dashes over the cell in <paramref name="column"/> of the row <paramref name="isRow"/>
    /// answers true for: the row named by something the Consumer reads from it, such as its key, so
    /// that the dashes follow it through a sort and through a Window of new instances alike. The grid
    /// asks it of the painted rows only, in order, and dashes the first it answers true for.</summary>
    /// <param name="isRow">Whether a row is the one to dash. Asked on every render while the dashes
    /// stand, so it must be cheap and must not throw.</param>
    /// <param name="column">The column's name, as <see cref="GridColumn{TRow}.Name"/> names it.</param>
    /// <exception cref="ArgumentNullException">The function or the name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public static GridPointDashes<TRow> OverCell(Func<TRow, bool> isRow, string column)
    {
        ArgumentNullException.ThrowIfNull(isRow);
        return new GridPointDashes<TRow>(isRow, column);
    }

    /// <summary>Dashes down the body of <paramref name="column"/>, across all its rows, as a pressed
    /// column header asks.</summary>
    /// <param name="column">The column's name, as <see cref="GridColumn{TRow}.Name"/> names it.</param>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public static GridPointDashes<TRow> OverColumn(string column) => new(null, column);

    /// <summary>Which row the dashed cell is in, or null when the dashes run down the whole
    /// column.</summary>
    public Func<TRow, bool>? Row { get; }

    /// <summary>The dashed column's name. A name the grid does not show is dashed nowhere.</summary>
    public string Column { get; }
}
