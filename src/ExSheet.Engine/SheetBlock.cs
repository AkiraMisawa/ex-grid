namespace ExSheet.Engine;

/// <summary>
/// A rectangle of cells copied inside ExSheet: each cell's Entry and the Cell Format it showed, as
/// they were at the copy, and where they came from (ADR-0048, ADR-0063). Pasting it writes the
/// Entries with their relative References shifted by the distance pasted, as Excel does, and the
/// Cell Formats with them. Immutable.
/// </summary>
public sealed class SheetBlock
{
    private readonly CellState[] _cells;

    internal SheetBlock(CellRange source, CellState[] cells)
    {
        Source = source;
        _cells = cells;
    }

    /// <summary>Where the block was copied from; its References are relative to this place.</summary>
    public CellRange Source { get; }

    /// <summary>How many rows it spans.</summary>
    public int RowCount => Source.RowCount;

    /// <summary>How many columns it spans.</summary>
    public int ColumnCount => Source.ColumnCount;

    /// <summary>The Entry at a position inside the block, as copied; <see langword="null"/> for a blank cell.</summary>
    public Entry? EntryAt(int row, int column) => At(row, column).Entry;

    /// <summary>The Cell Format at a position inside the block: what the cell showed at the copy, from whichever level recorded it.</summary>
    public CellFormat CellFormatAt(int row, int column) => At(row, column).CellFormat;

    internal CellState At(int row, int column)
    {
        if ((uint)row >= RowCount) throw new ArgumentOutOfRangeException(nameof(row), row, $"The block has {RowCount} row(s).");
        if ((uint)column >= ColumnCount) throw new ArgumentOutOfRangeException(nameof(column), column, $"The block has {ColumnCount} column(s).");
        return _cells[row * ColumnCount + column];
    }
}

/// <summary>
/// A copy of a rectangle of cells, or its refusal (ADR-0048, ADR-0049): the Entries for a paste
/// inside ExSheet, and the Values for anywhere else in the two flavours ExGrid puts on the
/// clipboard (ADR-0005).
/// </summary>
public sealed class SheetCopy
{
    internal SheetCopy(SheetRefusal refusal) => Refusal = refusal;

    internal SheetCopy(SheetBlock block, string text, string html)
    {
        Block = block;
        Text = text;
        Html = html;
    }

    /// <summary>
    /// Why the copy is refused — a cell in it is <c>#GETTING_DATA</c> (ADR-0049) — or
    /// <see langword="null"/>. A refused copy carries nothing, and the clipboard is left alone.
    /// </summary>
    public SheetRefusal? Refusal { get; }

    /// <summary>Whether the copy is refused.</summary>
    public bool IsRefused => Refusal is not null;

    /// <summary>The Entries, for a paste inside ExSheet.</summary>
    public SheetBlock? Block { get; }

    /// <summary>
    /// The <c>text/plain</c> flavour: each Value as the cell shows it, tab-separated, one line per
    /// row ending in CR LF, a field holding a tab, line break or quote quoted as Excel quotes it.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// The <c>text/html</c> flavour: a table of the unformatted Values, invariant — a number in
    /// full precision, which is what Excel reads when both flavours are there (ADR-0005) — in
    /// Excel's markup: a number's Value in <c>x:num</c> and a format that is not General in
    /// <c>mso-number-format</c>, so a copy to Excel carries formats (ADR-0048).
    /// </summary>
    public string? Html { get; }
}
