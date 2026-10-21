namespace ExGrid.Data.Xlsx;

/// <summary>Why a file was refused (ADR-0330 §4, §9). A refused file is never read in part.</summary>
public enum XlsxRefusal
{
    /// <summary>The bytes are not a zip package, or the zip is damaged.</summary>
    NotAZip,

    /// <summary>A zip package, but not a spreadsheet: it has no workbook part (a <c>.docx</c>, a <c>.pptx</c>, any other zip).</summary>
    NotAWorkbook,

    /// <summary>A workbook with macros (<c>.xlsm</c>, <c>.xltm</c>, <c>.xlam</c>): its content types name a macro-enabled workbook or a VBA project.</summary>
    MacroEnabled,

    /// <summary>A binary workbook (<c>.xlsb</c>).</summary>
    Binary,

    /// <summary>A legacy Excel 97–2003 workbook (<c>.xls</c>): an OLE compound file that holds no encrypted package.</summary>
    Legacy,

    /// <summary>An encrypted (password-protected) workbook: an OLE compound file holding an <c>EncryptedPackage</c>.</summary>
    Encrypted,

    /// <summary>A workbook written as Strict Open XML, whose parts use other namespaces than the transitional ones Excel saves by default.</summary>
    StrictOpenXml,

    /// <summary>A part declares a document type (<c>&lt;!DOCTYPE</c>), which a workbook never needs and an attack uses.</summary>
    Dtd,

    /// <summary>A part is not well-formed XML, or does not hold what its place in the package says it does.</summary>
    Malformed,

    /// <summary>The file holds more uncompressed bytes than <see cref="XlsxReadOptions.MaxUncompressedBytes"/> allows.</summary>
    TooLarge,

    /// <summary>The workbook holds more cells than <see cref="XlsxReadOptions.MaxCells"/> allows.</summary>
    TooManyCells,
}

/// <summary>
/// A file refused by name (ADR-0330 §4, §9): the reason, and the part, the Sheet and the cell it met, as
/// far as the refusal is about one. Nothing is read from a refused file:
/// <c>Sheet 'Trades', cell B7: …</c>.
/// </summary>
public sealed class XlsxException : Exception
{
    /// <summary>A refusal naming what it met.</summary>
    /// <param name="reason">Why the file is refused.</param>
    /// <param name="message">What was wrong, as a sentence that follows the Sheet and the cell.</param>
    /// <param name="part">The package part it met, when it is about one, as <c>xl/worksheets/sheet1.xml</c>.</param>
    /// <param name="sheet">The name of the Sheet it met, when it is about one.</param>
    /// <param name="cell">The cell it met, as <c>B7</c>, when it is about one.</param>
    /// <param name="innerException">What caused it, when it was another exception.</param>
    public XlsxException(XlsxRefusal reason, string message, string? part = null, string? sheet = null, string? cell = null, Exception? innerException = null)
        : base(Format(sheet, cell, part, message), innerException)
    {
        Reason = reason;
        Part = part;
        Sheet = sheet;
        Cell = cell;
    }

    /// <summary>Why the file was refused.</summary>
    public XlsxRefusal Reason { get; }

    /// <summary>The package part the refusal met, or <see langword="null"/> when it is about the file as a whole.</summary>
    public string? Part { get; }

    /// <summary>The name of the Sheet the refusal met, or <see langword="null"/> when it is not about one Sheet.</summary>
    public string? Sheet { get; }

    /// <summary>The cell the refusal met, as <c>B7</c>, or <see langword="null"/> when it is not about one cell.</summary>
    public string? Cell { get; }

    private static string Format(string? sheet, string? cell, string? part, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return (sheet, cell, part) switch
        {
            ({ } s, { } c, _) => $"Sheet '{s}', cell {c}: {message}",
            ({ } s, null, _) => $"Sheet '{s}': {message}",
            (null, _, { } p) => $"Part '{p}': {message}",
            _ => message,
        };
    }
}
