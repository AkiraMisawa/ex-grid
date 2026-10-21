using ExGrid.Data.Xlsx;

namespace ExSheet.Xlsx;

/// <summary>
/// Why a Workbook was not written, and nothing reached the stream (ADR-0330 §6): what it holds cannot be written as a file
/// Excel reads with the Values the user saw, or would make a file too large to open again.
/// </summary>
public enum XlsxSaveRefusal
{
    /// <summary>
    /// A Linked Table cannot be written as an Excel Table on a worksheet of its name, holding its rows as they stand
    /// (ADR-0330 §6): it has not answered (every Formula that reads it shows <c>#GETTING_DATA</c>), it holds no rows (an Excel
    /// Table holds at least one, and its Formulas would answer otherwise over one empty row), it holds more rows than a
    /// worksheet's 1,048,576 less its header's or more columns than its 16,384, or its name is already a Sheet's or is one a
    /// worksheet cannot have (longer than 31 characters, holding <c>\</c>, or <c>History</c>). Or a cell shows
    /// <c>#GETTING_DATA</c>. The refusal names the table, and a cell's refusal the Sheet and the cell.
    /// </summary>
    LinkedTable,

    /// <summary>
    /// A cell shows <c>#CIRC!</c>: it is part of a circular reference, or depends on one. A file holds only Excel's Error
    /// Values, and Excel shows 0 there, which ExSheet refuses to show (ADR-0047); no Value the file can hold is the one the
    /// user saw.
    /// </summary>
    CircularReference,

    /// <summary>
    /// A Formula that cannot be written as Excel stores it, or as Excel takes it: its text is longer than Excel's 8,192
    /// characters, holds a text constant longer than 255, nests its calls deeper than 64 levels or makes one with more than
    /// 255 arguments; it holds half of a surrogate pair, which a file cannot carry; it reads a Sheet the Workbook does not have
    /// (Excel would read another workbook's); it calls a name a <c>LET</c> binds or a <c>LAMBDA</c> takes, which ExSheet
    /// reads as an unknown function and Excel would call; it reads, by a structured reference, a Linked Table or a column the
    /// Workbook does not declare, or a column whose name begins or ends with a space or holds <c>@</c>, which Excel would
    /// read otherwise; or it does not read back as the Formula it is.
    /// </summary>
    Formula,

    /// <summary>
    /// A cell's text is longer than the 32,767 characters a cell of Excel holds, or holds half of a surrogate pair, which a
    /// file cannot carry; or a Sheet's name holds a character a worksheet's name cannot carry in a file: a control character
    /// below U+0020, U+FFFE or U+FFFF, or half of a surrogate pair. The refusal then names the Sheet and no cell.
    /// </summary>
    Text,

    /// <summary>
    /// The workbook would write more cells than <see cref="XlsxSaveOptions.MaxCells"/>: by default, more than a file is read
    /// with (<see cref="XlsxReadOptions.DefaultMaxCells"/>), so it would not open again. A Sheet formatted on many rows and many
    /// columns writes a cell wherever a formatted row crosses a formatted column that records a part the row does not.
    /// </summary>
    TooManyCells,

    /// <summary>
    /// The workbook would hold more uncompressed bytes than <see cref="XlsxSaveOptions.MaxUncompressedBytes"/>: by default,
    /// more than a file is read with (<see cref="XlsxReadOptions.DefaultMaxUncompressedBytes"/>), so it would not open again.
    /// The refusal names the Sheet whose worksheet went past it, when one did.
    /// </summary>
    TooLarge,
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

    /// <summary>
    /// The uncompressed bytes the file's parts may hold together. The default is
    /// <see cref="XlsxReadOptions.DefaultMaxUncompressedBytes"/>, the most a file is read with by default, and also the most
    /// it may be: the file is held in memory, compressed, until it is handed to the stream. Beyond it the write is refused by
    /// name (<see cref="XlsxSaveRefusal.TooLarge"/>) before anything is written.
    /// </summary>
    public long MaxUncompressedBytes { get; init; } = XlsxReadOptions.DefaultMaxUncompressedBytes;
}
