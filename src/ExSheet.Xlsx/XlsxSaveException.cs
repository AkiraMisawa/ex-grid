using ExGrid.Data.Xlsx;

namespace ExSheet.Xlsx;

/// <summary>
/// Why a Workbook was not written, and nothing reached the stream (ADR-0330 §6): what it holds cannot be written as a file
/// Excel reads with the Values the user saw, or would make a file too large to open again.
/// </summary>
public enum XlsxSaveRefusal
{
    /// <summary>
    /// The Workbook declares a Linked Table. Each is written as an Excel Table on a worksheet of its own (ADR-0330 §6), which
    /// this version does not write yet; written without it, a Formula that reads the table would read nothing in Excel.
    /// </summary>
    LinkedTable,

    /// <summary>
    /// A cell shows <c>#CIRC!</c>: it is part of a circular reference, or depends on one. A file holds only Excel's Error
    /// Values, and Excel shows 0 there, which ExSheet refuses to show (ADR-0047); no Value the file can hold is the one the
    /// user saw.
    /// </summary>
    CircularReference,

    /// <summary>A Formula that cannot be written as Excel stores it: its text is longer than Excel's 8,192 characters, it reads a Sheet the Workbook does not have (Excel would read another workbook's), or it does not read back as the Formula it is.</summary>
    Formula,

    /// <summary>A cell's text is longer than the 32,767 characters a cell of Excel holds, or holds half of a surrogate pair, which a file cannot carry.</summary>
    Text,

    /// <summary>
    /// The workbook would write more cells than <see cref="XlsxSaveOptions.MaxCells"/>: by default, more than a file is read
    /// with (<see cref="XlsxReadOptions.DefaultMaxCells"/>), so it would not open again. A Sheet formatted on many rows and many
    /// columns writes a cell wherever a formatted row crosses a formatted column that records a part the row does not.
    /// </summary>
    TooManyCells,
}

/// <summary>
/// A Workbook not written, refused by name (ADR-0330 §6): the reason, the Sheet and the cell, and what the cell held, as far
/// as the refusal is about one — <c>Sheet 'Trades', cell B7 (=B6*2): …</c>, as a refused opening names them. Nothing reached
/// the stream.
/// </summary>
public sealed class XlsxSaveException : Exception
{
    /// <summary>A refusal naming what it met.</summary>
    /// <param name="reason">Why the Workbook is not written.</param>
    /// <param name="message">What was wrong, as a sentence that follows the Sheet and the cell.</param>
    /// <param name="sheet">The name of the Sheet it met, when it is about one.</param>
    /// <param name="cell">The cell it met, as <c>B7</c>, when it is about one.</param>
    /// <param name="held">What the cell held, as a Formula's text with its <c>=</c>, when it is about one.</param>
    public XlsxSaveException(XlsxSaveRefusal reason, string message, string? sheet = null, string? cell = null, string? held = null)
        : base(Format(sheet, cell, held, message))
    {
        Reason = reason;
        Sheet = sheet;
        Cell = cell;
        Held = held;
    }

    /// <summary>Why the Workbook was not written.</summary>
    public XlsxSaveRefusal Reason { get; }

    /// <summary>The name of the Sheet the refusal met, or <see langword="null"/> when it is not about one Sheet.</summary>
    public string? Sheet { get; }

    /// <summary>The cell the refusal met, as <c>B7</c>, or <see langword="null"/> when it is not about one cell.</summary>
    public string? Cell { get; }

    /// <summary>What the cell held — a Formula's text with its <c>=</c> — or <see langword="null"/>.</summary>
    public string? Held { get; }

    private static string Format(string? sheet, string? cell, string? held, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var at = held is null ? cell : $"{cell} ({held})";
        return (sheet, at) switch
        {
            ({ } s, { } c) => $"Sheet '{s}', cell {c}: {message}",
            ({ } s, null) => $"Sheet '{s}': {message}",
            _ => message,
        };
    }
}

/// <summary>What a write may produce (ADR-0330 §6, §9).</summary>
public sealed class XlsxSaveOptions
{
    /// <summary>
    /// The cells the whole workbook may write, every worksheet counted, each cell element a cell: the Sheets' own, the cells an
    /// array spills into, and the cells of no value written where a formatted row crosses a formatted column. The default is
    /// <see cref="XlsxReadOptions.DefaultMaxCells"/>, the most a file is read with by default, so that what is written opens
    /// again; an application that raises its read limit may raise this one with it. Beyond it the write is refused by name
    /// (<see cref="XlsxSaveRefusal.TooManyCells"/>) before anything is written.
    /// </summary>
    public long MaxCells { get; init; } = XlsxReadOptions.DefaultMaxCells;
}
