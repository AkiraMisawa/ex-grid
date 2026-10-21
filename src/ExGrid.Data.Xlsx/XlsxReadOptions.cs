namespace ExGrid.Data.Xlsx;

/// <summary>
/// What a read may take, and how it paces itself (ADR-0330 §9). A workbook is untrusted input: beyond
/// either limit the file is refused by name, never read in part. The read works in slices sized by
/// time, yields between them so that a browser keeps painting, and reports its progress after each, as
/// ExGrid.Data's loads do.
/// </summary>
public sealed class XlsxReadOptions
{
    /// <summary>
    /// The default for <see cref="MaxUncompressedBytes"/>: 1 GiB. Measured, a worksheet of <see cref="DefaultMaxCells"/> cells
    /// of numbers, text, Formulas and dates is 409 to 420 MiB of XML, about 43 bytes a cell, and its shared strings up to
    /// 106 MiB when every text is its own; this leaves room for heavier markup, and refuses a zip bomb before it is
    /// inflated. The shared strings are held whole, at about twice their XML's size (ticket 01 of
    /// <c>docs/specs/exsheet-xlsx</c>).
    /// </summary>
    public const long DefaultMaxUncompressedBytes = 1L << 30;

    /// <summary>
    /// The default for <see cref="MaxCells"/>: ten million, a full-height worksheet of ten columns. Measured on CoreCLR, the
    /// file layer reads them in 3.3 s (4.9 s with every text its own), holding no more than the batch at hand and the shared
    /// strings; what the application builds from them is its own to bound, below this if it must (ticket 01 of
    /// <c>docs/specs/exsheet-xlsx</c>).
    /// </summary>
    public const long DefaultMaxCells = 10_000_000;

    /// <summary>
    /// The uncompressed bytes the whole package may hold, every part counted, as its zip declares them. A
    /// part is never read past what it declares: a hostile zip may declare less than a part holds, and the
    /// read refuses it there. The default is <see cref="DefaultMaxUncompressedBytes"/>.
    /// </summary>
    public long MaxUncompressedBytes { get; init; } = DefaultMaxUncompressedBytes;

    /// <summary>
    /// The cells the whole workbook may hold, every worksheet counted, as they are read. The default is
    /// <see cref="DefaultMaxCells"/>.
    /// </summary>
    public long MaxCells { get; init; } = DefaultMaxCells;

    /// <summary>Told how far the read has come after every slice, and once more at the end of the shared strings and of
    /// each worksheet.</summary>
    public IProgress<XlsxProgress>? Progress { get; init; }

    /// <summary>
    /// How long one slice works before the read yields. A slice always does some work, so a zero budget
    /// yields after every batch. The default is 30 ms.
    /// </summary>
    public TimeSpan SliceBudget { get; init; } = TimeSpan.FromMilliseconds(30);

    /// <summary>
    /// What the read awaits between slices. By default it is <see cref="Task.Yield"/>, in a browser too,
    /// where it gives the page a turn of its event loop to paint in.
    /// </summary>
    public Func<ValueTask>? Yield { get; init; }
}

/// <summary>How far a read has come: the shared strings' part while <see cref="XlsxReader.OpenAsync"/> reads it, then the
/// worksheets'.</summary>
/// <param name="Sheet">The index of the worksheet being read, in the workbook's order, from zero; -1 while the shared
/// strings are read.</param>
/// <param name="Sheets">The worksheets the workbook holds.</param>
/// <param name="Cells">The cells read so far, every worksheet counted.</param>
/// <param name="Bytes">The worksheets' uncompressed bytes read so far; while <paramref name="Sheet"/> is -1, the shared
/// strings'.</param>
/// <param name="TotalBytes">The uncompressed bytes of every worksheet, as the zip declares them; while
/// <paramref name="Sheet"/> is -1, the shared strings'.</param>
public readonly record struct XlsxProgress(int Sheet, int Sheets, long Cells, long Bytes, long TotalBytes);
