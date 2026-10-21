using ExGrid.Data.Xlsx;

namespace ExSheet.Xlsx;

/// <summary>
/// Why a workbook was refused, and nothing opened (ADR-0330 §4): what it held would change a Value, or the number a cell
/// shows, or it is not a file that can be read faithfully.
/// </summary>
public enum XlsxOpenRefusal
{
    /// <summary>The file layer refused the file: not a plain <c>.xlsx</c>, damaged, or past a limit. <see cref="XlsxOpenException.FileRefusal"/> says why.</summary>
    File,

    /// <summary>A Formula the engine cannot read: a syntax it does not implement (an array constant, the union operator, <c>@</c>), or a What-If data table.</summary>
    UnreadableFormula,

    /// <summary>A Formula reads a defined name, which ExSheet does not define, so it would show <c>#NAME?</c> where Excel shows a Value.</summary>
    DefinedName,

    /// <summary>A Formula reads another workbook, which ExSheet cannot reach.</summary>
    OtherWorkbook,

    /// <summary>A Formula reads an Excel Table, which ExSheet does not read yet (ADR-0330 §6).</summary>
    ExcelTable,

    /// <summary>A legacy array formula (Ctrl+Shift+Enter), whose cells Excel computes as one, and ExSheet would spill otherwise.</summary>
    LegacyArrayFormula,

    /// <summary>A constant a cell cannot hold in ExSheet, such as an Error Value the engine does not read.</summary>
    UnreadableConstant,

    /// <summary>A worksheet's name is not one a Sheet can have, or the workbook holds no worksheet at all.</summary>
    Sheets,

    /// <summary>
    /// A cell, a row or a column takes a Number Format the engine does not read, or a built-in one whose code follows the
    /// computer's locale and that the file does not spell out: shown in General instead, a date would show as a serial, or a
    /// currency lose its symbol (ADR-0330 §4). <see cref="XlsxOpenException.Held"/> is the format's code.
    /// </summary>
    NumberFormat,
}

/// <summary>
/// A workbook refused by name, and nothing opened (ADR-0330 §4): the reason, the Sheet and the cell, and what the cell
/// held, as far as the refusal is about one — <c>Sheet 'Trades', cell B7 (=Rate*A7): …</c>, as the CSV reader names a
/// row and a column.
/// </summary>
public sealed class XlsxOpenException : Exception
{
    /// <summary>A refusal naming what it met.</summary>
    /// <param name="reason">Why the workbook is refused.</param>
    /// <param name="message">What was wrong, as a sentence that follows the Sheet and the cell.</param>
    /// <param name="sheet">The name of the Sheet it met, when it is about one.</param>
    /// <param name="cell">The cell it met, as <c>B7</c>, when it is about one.</param>
    /// <param name="held">What the cell held, as a Formula's text with its <c>=</c> or a constant's, when it is about one.</param>
    /// <param name="innerException">What caused it, when it was another exception.</param>
    public XlsxOpenException(XlsxOpenRefusal reason, string message, string? sheet = null, string? cell = null, string? held = null, Exception? innerException = null)
        : this(reason, Format(sheet, cell, held, message), sheet, cell, held, innerException, formatted: true)
    {
    }

    private XlsxOpenException(XlsxOpenRefusal reason, string message, string? sheet, string? cell, string? held, Exception? innerException, bool formatted)
        : base(message, innerException)
    {
        _ = formatted;
        Reason = reason;
        Sheet = sheet;
        Cell = cell;
        Held = held;
    }

    /// <summary>Why the workbook was refused.</summary>
    public XlsxOpenRefusal Reason { get; }

    /// <summary>The name of the Sheet the refusal met, or <see langword="null"/> when it is not about one Sheet.</summary>
    public string? Sheet { get; }

    /// <summary>The cell the refusal met, as <c>B7</c>, or <see langword="null"/> when it is not about one cell.</summary>
    public string? Cell { get; }

    /// <summary>What the cell held — a Formula's text with its <c>=</c>, or a constant — or <see langword="null"/> when the refusal is not about one cell.</summary>
    public string? Held { get; }

    /// <summary>Why the file layer refused the file, when <see cref="Reason"/> is <see cref="XlsxOpenRefusal.File"/>; otherwise <see langword="null"/>.</summary>
    public XlsxRefusal? FileRefusal => InnerException is XlsxException file && Reason == XlsxOpenRefusal.File ? file.Reason : null;

    /// <summary>The file layer's refusal, with its Sheet and cell.</summary>
    internal static XlsxOpenException FromFile(XlsxException e) =>
        new(XlsxOpenRefusal.File, e.Message, e.Sheet, e.Cell, held: null, e, formatted: true);

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
