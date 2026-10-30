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
    /// Workbook does not declare, or a column whose name begins or ends with a space or holds <c>@</c>, a tab, a line break
    /// or one of <c>, : . " { } $ ^ &amp; * + = - &lt; &gt; /</c>, whose form Excel has not been seen to write; it reads a
    /// Linked Table's name as a name of its own (<c>=SUM(Positions)</c>), which ExSheet shows as <c>#NAME?</c> and Excel
    /// would read as the table; or it does not read back as the Formula it is.
    /// </summary>
    Formula,

    /// <summary>
    /// A cell's text is longer than the 32,767 characters a cell of Excel holds, or holds half of a surrogate pair, which a
    /// file cannot carry; or a Sheet's name holds a character a worksheet's name cannot carry in a file: a control character
    /// below U+0020, U+FFFE or U+FFFF, or half of a surrogate pair. The refusal then names the Sheet and no cell.
    /// </summary>
    Text,

    /// <summary>
    /// The workbook would write more elements than <see cref="XlsxSaveOptions.MaxCells"/>, which counts each cell written and
    /// each row a row format writes. A Sheet formatted on many rows and many columns writes a cell wherever a formatted row
    /// crosses a formatted column that records a part the row does not, and a row format writes a row element for every row
    /// it covers, holding a cell or not. The default limit is the cells a file is read with
    /// (<see cref="XlsxReadOptions.DefaultMaxCells"/>); the reader counts cells only, so a file within it opens again, and
    /// rows are counted here so that a row format over every row is bounded too.
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

/// <summary>
/// What a write may produce, and how it paces itself (ADR-0330 §6, §9). Beyond either limit the Workbook is refused by name,
/// never written in part. The write takes its snapshot of the Workbook before it first yields, then builds the file from it
/// in slices sized by time, yielding between them so that a browser keeps painting, and reports its progress after each, as
/// a read does (<see cref="XlsxReadOptions"/>).
/// </summary>
public sealed class XlsxSaveOptions
{
    /// <summary>
    /// The cells the whole workbook may write, every worksheet counted, each element a cell: the Sheets' own cells, the cells
    /// an array spills into, the cells of no value written where a formatted row crosses a formatted column, a Linked Table's
    /// cells and its header's, and each row a row format writes, which is an element of its own whether or not it holds a
    /// cell (a row format over every row writes 1,048,576). The default is <see cref="XlsxReadOptions.DefaultMaxCells"/>, the
    /// most a file is read with by default, so that what is written opens again; an application that raises its read limit
    /// may raise this one with it. Beyond it the write is refused by name (<see cref="XlsxSaveRefusal.TooManyCells"/>) before
    /// anything is written.
    /// </summary>
    public long MaxCells { get; init; } = XlsxReadOptions.DefaultMaxCells;

    /// <summary>
    /// The uncompressed bytes the file's parts may hold together. The default is
    /// <see cref="XlsxReadOptions.DefaultMaxUncompressedBytes"/>, the most a file is read with by default, and also the most
    /// it may be: the file is held in memory, compressed, until it is handed to the stream. Beyond it the write is refused by
    /// name (<see cref="XlsxSaveRefusal.TooLarge"/>) before anything is written.
    /// </summary>
    public long MaxUncompressedBytes { get; init; } = XlsxReadOptions.DefaultMaxUncompressedBytes;

    /// <summary>
    /// Told how far the write has come after every slice, and once more when every element is written. It is told on the
    /// thread the write is running on, which after its first yield need not be a component's renderer: a component that
    /// shows it passes a <see cref="Progress{T}"/> made on its renderer, which posts there, or marshals with
    /// <c>InvokeAsync</c>.
    /// </summary>
    public IProgress<XlsxSaveProgress>? Progress { get; init; }

    /// <summary>
    /// How long one slice works before the write yields. A slice always does some work, so a zero budget yields after every
    /// batch. The default is 30 ms, a read's.
    /// </summary>
    public TimeSpan SliceBudget { get; init; } = TimeSpan.FromMilliseconds(30);

    /// <summary>
    /// What the write awaits between slices. By default it is <see cref="Task.Yield"/>, in a browser too, where it gives the
    /// page a turn of its event loop to paint in.
    /// </summary>
    public Func<ValueTask>? Yield { get; init; }
}

/// <summary>How far a write has come (<see cref="XlsxSaveOptions.Progress"/>): the Workbook's snapshot being planned, then
/// the worksheets being written.</summary>
/// <param name="Sheet">The index of the worksheet being written, in the file's order, from zero, a Linked Table's after the
/// Sheets'; -1 while the snapshot is planned: its cells counted against the limit and checked.</param>
/// <param name="Sheets">The worksheets the file holds: one for each Sheet and one for each Linked Table.</param>
/// <param name="Elements">The elements written so far, counted as <see cref="XlsxSaveOptions.MaxCells"/> counts them; 0 while
/// the snapshot is planned.</param>
/// <param name="TotalElements">The elements the file writes, counted as <see cref="XlsxSaveOptions.MaxCells"/> counts them;
/// 0 while the snapshot is planned.</param>
public readonly record struct XlsxSaveProgress(int Sheet, int Sheets, long Elements, long TotalElements);
