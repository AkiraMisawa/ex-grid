namespace ExSheet;

/// <summary>
/// How many rows and columns every Sheet of an ExSheet holds (ADR-0450): ten by ten draws <c>A1:J10</c> and nothing
/// past it. Without one, a Sheet holds Excel's 1,048,576 rows by 16,384 columns. It bounds where a cell may hold an
/// Entry and what the user reaches, not the address space: a Reference past it still reads the empty cell it names.
/// </summary>
public sealed record SheetSize
{
    /// <summary>A size of <paramref name="rows"/> by <paramref name="columns"/>.</summary>
    /// <param name="rows">How many rows, from 1 to 1,048,576.</param>
    /// <param name="columns">How many columns, from 1 to 16,384 (<c>A</c> to <c>XFD</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException">A count is outside its range.</exception>
    public SheetSize(int rows, int columns)
    {
        if (rows is < 1 or > MaxRows)
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "A Sheet holds from 1 to 1,048,576 rows.");
        if (columns is < 1 or > MaxColumns)
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "A Sheet holds from 1 to 16,384 columns.");
        Rows = rows;
        Columns = columns;
    }

    /// <summary>Excel's row count, 1,048,576: the most a Sheet holds.</summary>
    public const int MaxRows = ExSheet.Engine.Sheet.RowCount;

    /// <summary>Excel's column count, 16,384 (<c>A</c> to <c>XFD</c>): the most a Sheet holds.</summary>
    public const int MaxColumns = ExSheet.Engine.Sheet.ColumnCount;

    /// <summary>How many rows.</summary>
    public int Rows { get; }

    /// <summary>How many columns.</summary>
    public int Columns { get; }
}
