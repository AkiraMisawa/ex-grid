using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// Writes a Workbook as it stands to an <c>.xlsx</c> (ADR-0330 §6): every Sheet, in order, as a worksheet of its name, the
/// Sheet shown shown; every constant; every Formula with the Value it shows, Excel's stored prefixes put back; the cells an
/// array spills into with their Values; the Cell Formats at cell, row and column level and the column widths in Excel's
/// unit. The workbook is marked to be calculated in full when Excel opens it, and a program that does not calculate reads
/// the Values the user saw, <c>RAND()</c>'s included. What cannot be written faithfully is refused by name, and nothing
/// reaches the stream.
/// </summary>
/// <remarks>
/// <para>
/// Every Formula is written as a dynamic array (<c>t="array"</c>, marked by the workbook's cell metadata as Excel 365 marks
/// one), over the cells it spills into or its own cell alone. The engine computes every Formula as Excel 365 does
/// (ADR-0125), and a Formula written plainly is read by Excel as one of an earlier version, where a range an operator or a
/// function's argument takes is implicitly intersected (<c>=SUM(A1:A3*2)</c> would read as <c>=SUM(@A1:A3*2)</c>) and its
/// Value would change.
/// </para>
/// <para>
/// Text is written into the shared strings, as Excel writes it; a Formula's text result is written in its cell
/// (<c>t="str"</c>). Fonts are the workbook's own, Calibri 11, and colours their RGB values (ADR-0330 §8).
/// </para>
/// </remarks>
public static class XlsxWorkbookWriter
{
    /// <summary>The longest Formula Excel holds, in characters, without its <c>=</c>.</summary>
    public const int MaxFormulaLength = 8_192;

    /// <summary>The longest text a cell of Excel holds, in characters.</summary>
    public const int MaxTextLength = 32_767;

    /// <summary>
    /// Writes <paramref name="workbook"/> as it stands into <paramref name="stream"/> as an <c>.xlsx</c>, or refuses it by name
    /// (<see cref="XlsxSaveException"/>) with nothing written to the stream.
    /// </summary>
    /// <param name="workbook">
    /// The Workbook. It is read whole before this method first yields: the file is the Workbook as it stood when the call was
    /// made, and the Workbook may change once the returned task is under way. The package is held in memory, compressed, until
    /// then, and handed to the stream asynchronously.
    /// </param>
    /// <param name="stream">Where the file goes; it need not seek, and is left open.</param>
    /// <param name="options">What the write may produce; null for the defaults.</param>
    /// <param name="cancellationToken">Cancels the hand-off to the stream.</param>
    /// <exception cref="XlsxSaveException">The Workbook is refused, and nothing was written.</exception>
    public static async ValueTask WriteAsync(Workbook workbook, Stream stream, XlsxSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new XlsxSaveOptions();

        // Everything the file says is taken here, synchronously, before the hand-off first yields.
        var writer = Write(workbook, stream, options);
        await using (writer.ConfigureAwait(false))
        {
            await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>One cell element a Sheet writes of its own: an Entry, a spilled Value, or a format.</summary>
    private readonly record struct Recorded(CellAddress At, Entry? Entry, int Style);

    private sealed record Planned(Sheet Sheet, SheetFormatPlan Plan, List<(CellAddress At, Entry? Entry, FormatParts Own)> Cells);

    /// <summary>The Workbook written into a package held in memory, every part but those written on completion.</summary>
    private static XlsxWriter Write(Workbook workbook, Stream stream, XlsxSaveOptions options)
    {
        if (workbook.LinkedTables.Count > 0)
        {
            var table = workbook.LinkedTables[0].Name;
            throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                $"The Workbook declares the Linked Table '{table}', which is written as an Excel Table on a worksheet of its own; this version does not write one yet, and without it a Formula that reads the table would read nothing in Excel.");
        }

        var document = workbook.ToDocument();
        var planned = new List<Planned>(workbook.Sheets.Count);
        long cells = 0;
        for (var i = 0; i < workbook.Sheets.Count; i++)
        {
            var sheet = workbook.Sheets[i];
            if (sheet.Name.Any(char.IsControl))
                throw new XlsxSaveException(XlsxSaveRefusal.Text, "The Sheet's name holds a control character, which a file cannot carry in a worksheet's name.", sheet.Name);
            var own = Own(sheet, document.Sheets[i]);
            var plan = SheetFormatPlan.For(document.Sheets[i]);
            var crossings = plan.Crossings(own.Select(c => c.At));
            cells += own.Count + crossings;
            if (cells > options.MaxCells)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.TooManyCells, string.Create(CultureInfo.InvariantCulture,
                    $"The workbook would write more than {options.MaxCells:N0} cells, more than a file is read with; this Sheet writes {own.Count:N0} of its own and {crossings:N0} where its formatted rows cross its formatted columns."),
                    sheet.Name);
            }
            planned.Add(new Planned(sheet, plan, own));
        }

        var styles = new StyleTable(workbook.Culture);
        var sheets = new List<(Sheet Sheet, SheetFormatPlan Plan, List<Recorded> Cells, Dictionary<CellAddress, CellAddress> Spills)>(planned.Count);
        foreach (var (sheet, plan, own) in planned)
        {
            plan.Build(styles);
            var recorded = new List<Recorded>(own.Count);
            foreach (var (at, entry, parts) in own) recorded.Add(new Recorded(at, entry, plan.Style(styles, parts, at)));
            sheets.Add((sheet, plan, recorded, Spills(sheet)));
        }

        var writer = XlsxWriter.Create(stream, new XlsxWriteOptions
        {
            Styles = styles.Build(),
            ActiveSheet = IndexOf(workbook, workbook.Shown),
            FullCalculationOnLoad = true,
        });
        try
        {
            foreach (var (sheet, plan, recorded, spills) in sheets)
                WriteSheet(writer.AddSheet(sheet.Name, plan.Columns), sheet, plan, recorded, spills);
            return writer;
        }
        catch
        {
            writer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    private static int IndexOf(Workbook workbook, Sheet sheet)
    {
        for (var i = 0; i < workbook.Sheets.Count; i++)
        {
            if (ReferenceEquals(workbook.Sheets[i], sheet)) return i;
        }
        return 0;
    }

    /// <summary>
    /// The cells a Sheet writes of its own, in the sheet's order: each the document records (an Entry, or a format of its own),
    /// and each an array spills into.
    /// </summary>
    private static List<(CellAddress At, Entry? Entry, FormatParts Own)> Own(Sheet sheet, SheetDocument document)
    {
        var cells = new Dictionary<CellAddress, (Entry? Entry, FormatParts Own)>(document.Cells.Count);
        foreach (var cell in document.Cells)
            cells[cell.Address] = (cell.Entry, new FormatParts(cell.NumberFormat, cell.Alignment, cell.Font, cell.Fill, cell.Borders));
        foreach (var at in sheet.ValueAddresses)
        {
            if (sheet.SpilledFrom(at) is not null) cells.TryAdd(at, (null, default));
        }
        var ordered = new List<(CellAddress, Entry?, FormatParts)>(cells.Count);
        foreach (var (at, (entry, own)) in cells.OrderBy(c => c.Key)) ordered.Add((at, entry, own));
        return ordered;
    }

    /// <summary>Each spilling Anchor, with the last cell of its Spill Range.</summary>
    private static Dictionary<CellAddress, CellAddress> Spills(Sheet sheet)
    {
        var spills = new Dictionary<CellAddress, CellAddress>();
        foreach (var at in sheet.ValueAddresses)
        {
            if (sheet.SpilledFrom(at) is not { } anchor) continue;
            spills[anchor] = spills.TryGetValue(anchor, out var last)
                ? new CellAddress(Math.Max(last.Row, at.Row), Math.Max(last.Column, at.Column))
                : at;
        }
        return spills;
    }

    /// <summary>A Sheet's rows, in order: each formatted row with its format, and every cell element in it, its own and its crossings'.</summary>
    private static void WriteSheet(XlsxSheetWriter writer, Sheet sheet, SheetFormatPlan plan, List<Recorded> cells, Dictionary<CellAddress, CellAddress> spills)
    {
        var runs = plan.Rows;
        var run = 0;
        var runRow = runs.Count > 0 ? runs[0].First : int.MaxValue;
        var next = 0;
        while (next < cells.Count || run < runs.Count)
        {
            var cellRow = next < cells.Count ? cells[next].At.Row : int.MaxValue;
            var row = Math.Min(cellRow, runRow);
            var end = next;
            while (end < cells.Count && cells[end].At.Row == row) end++;
            if (row == runRow)
            {
                var formatted = runs[run];
                writer.WriteRow(new XlsxRow(row, formatted.Style));
                var at = next;
                foreach (var crossing in formatted.Crossings)
                {
                    for (var column = crossing.First; column <= crossing.Last; column++)
                    {
                        while (at < end && cells[at].At.Column < column) WriteCell(writer, sheet, cells[at++], spills);
                        if (at < end && cells[at].At.Column == column) continue;
                        writer.Write(new XlsxCell(row, column, XlsxCellType.Number, null, StyleIndex: crossing.Style));
                    }
                }
                while (at < end) WriteCell(writer, sheet, cells[at++], spills);
                runRow++;
                if (runRow > formatted.Last)
                {
                    run++;
                    runRow = run < runs.Count ? runs[run].First : int.MaxValue;
                }
            }
            else
            {
                for (var at = next; at < end; at++) WriteCell(writer, sheet, cells[at], spills);
            }
            next = end;
        }
    }

    /// <summary>One cell of the Sheet's own: its constant, or its Formula with the Value it shows, or a spilled Value, or its format alone.</summary>
    private static void WriteCell(XlsxSheetWriter writer, Sheet sheet, Recorded cell, Dictionary<CellAddress, CellAddress> spills)
    {
        var at = cell.At;
        var empty = new XlsxCell(at.Row, at.Column, XlsxCellType.Number, null, StyleIndex: cell.Style);
        if (cell.Entry is { Constant: { } constant })
        {
            writer.Write(constant.Kind == ValueKind.Text ? Shared(sheet, at, constant.Text, empty) : Valued(sheet, at, constant, empty, null));
            return;
        }
        var value = sheet.GetValue(at);
        if (cell.Entry is { Formula: { } formula } entry)
        {
            if (formula.Length - 1 > MaxFormulaLength)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.Formula, string.Create(CultureInfo.InvariantCulture,
                    $"The Formula is {formula.Length - 1:N0} characters long, and Excel holds at most {MaxFormulaLength:N0}."), sheet.Name, at.ToString());
            }
            var stored = WrittenFormula.Write(entry, name => sheet.Workbook.Find(name) is not null, out var problem);
            switch (problem)
            {
                case WrittenFormulaProblem.MissingSheet:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula reads a Sheet the Workbook does not have, which Excel would read as another workbook's.", sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.StructuredReference:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula reads a table by a structured reference, which the workbook written holds no table for.", sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.Unreadable:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula cannot be written as Excel stores it: its names do not bind as the engine binds them.", sheet.Name, at.ToString(), formula);
            }
            var range = spills.TryGetValue(at, out var last) ? $"{at}:{last}" : at.ToString();
            var written = new XlsxFormula(stored, XlsxFormulaKind.Array, range, IsDynamicArray: true);
            writer.Write(value is { } v ? Valued(sheet, at, v, empty, formula) with { Formula = written } : empty with { Formula = written });
            return;
        }
        writer.Write(value is { } spilled ? Valued(sheet, at, spilled, empty, null) : empty);
    }

    /// <summary>A cell holding <paramref name="value"/>: a number, a boolean or an Error Value in the cell, text in the cell as a Formula's result.</summary>
    private static XlsxCell Valued(Sheet sheet, CellAddress at, Value value, XlsxCell cell, string? formula)
    {
        switch (value.Kind)
        {
            case ValueKind.Number:
                return cell with { Value = value.Number.ToString("R", CultureInfo.InvariantCulture) };
            case ValueKind.Boolean:
                return cell with { Type = XlsxCellType.Boolean, Value = value.Boolean ? "1" : "0" };
            case ValueKind.Text:
                CheckText(sheet, at, value.Text, formula);
                return cell with { Type = XlsxCellType.FormulaString, Value = value.Text };
        }
        switch (value.Error)
        {
            case ErrorValue.Circ:
                throw new XlsxSaveException(XlsxSaveRefusal.CircularReference,
                    "The cell shows #CIRC!: it is part of a circular reference, or depends on one. A file holds no such Error Value, and Excel shows 0 there; no Value it can hold is the one shown.",
                    sheet.Name, at.ToString(), formula);
            case ErrorValue.GettingData:
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                    "The cell shows #GETTING_DATA: it waits on a Linked Table that has not answered.", sheet.Name, at.ToString(), formula);
        }
        return cell with { Type = XlsxCellType.Error, Value = value.ToString() };
    }

    private static XlsxCell Shared(Sheet sheet, CellAddress at, string text, XlsxCell cell)
    {
        CheckText(sheet, at, text, null);
        return cell with { Type = XlsxCellType.SharedString, Text = text };
    }

    private static void CheckText(Sheet sheet, CellAddress at, string text, string? formula)
    {
        if (text.Length > MaxTextLength)
        {
            throw new XlsxSaveException(XlsxSaveRefusal.Text, string.Create(CultureInfo.InvariantCulture,
                $"The cell's text is {text.Length:N0} characters long, and a cell of Excel holds at most {MaxTextLength:N0}."), sheet.Name, at.ToString(), formula);
        }
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }
            if (char.IsSurrogate(text[i]))
                throw new XlsxSaveException(XlsxSaveRefusal.Text, "The cell's text holds half of a surrogate pair, which a file cannot carry.", sheet.Name, at.ToString(), formula);
        }
    }
}
