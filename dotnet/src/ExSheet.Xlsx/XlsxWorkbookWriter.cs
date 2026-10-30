using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// Writes a Workbook as it stands to an <c>.xlsx</c> (ADR-0330 §6): every Sheet, in order, as a worksheet of its name, the
/// Sheet shown shown; every constant; every Formula with the Value it shows, Excel's stored prefixes put back; the cells an
/// array spills into with their Values; the Cell Formats at cell, row and column level and the column widths in Excel's
/// unit. Each Linked Table is written after the Sheets as an Excel Table of its name, on a worksheet of its name, holding
/// its rows as they stand, so that a Formula that reads it (<c>Positions[PV]</c>) reads it in Excel too. The workbook is
/// marked to be calculated in full when Excel opens it, and a program that does not calculate reads the Values the user saw,
/// <c>RAND()</c>'s included. What cannot be written faithfully is refused by name, and nothing reaches the stream.
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
/// <para>
/// A Linked Table's worksheet is written for Excel, and is not read back as a Linked Table (ADR-0330 §6, X1): opened again by
/// <see cref="XlsxWorkbookReader"/>, it is a worksheet of the table's cells, and a Formula that reads the Excel Table is
/// refused until ExSheet reads Excel Tables. Its header, at A1, holds the columns' names, and its rows follow.
/// </para>
/// </remarks>
public static class XlsxWorkbookWriter
{
    /// <summary>The longest Formula Excel holds, in characters, without its <c>=</c>.</summary>
    public const int MaxFormulaLength = 8_192;

    /// <summary>The longest text a cell of Excel holds, in characters.</summary>
    public const int MaxTextLength = 32_767;

    /// <summary>The most rows a Linked Table written as an Excel Table holds: a worksheet's 1,048,576, less its header's.</summary>
    public const int MaxLinkedTableRows = XlsxCellReference.MaxRows - 1;

    /// <summary>
    /// Writes <paramref name="workbook"/> as it stands into <paramref name="stream"/> as an <c>.xlsx</c>, or refuses it by name
    /// (<see cref="XlsxSaveException"/>) with nothing written to the stream.
    /// </summary>
    /// <param name="workbook">
    /// The Workbook. Its snapshot is taken before this method first yields — every Sheet's document, every Value it shows,
    /// its spills and each Linked Table's rows — and the file is built from that snapshot while yielding (ADR-0330 §9): the
    /// file is the Workbook as it stood when the call was made, and the Workbook may change once the returned task is under
    /// way. The package is held in memory, compressed, until it is built, and then handed to the stream asynchronously.
    /// </param>
    /// <param name="stream">Where the file goes; it need not seek, and is left open.</param>
    /// <param name="options">What the write may produce, and how it paces itself; null for the defaults.</param>
    /// <param name="cancellationToken">
    /// Cancels the write, between slices while the file is built, and while it is handed to the stream. Cancelled while it is
    /// built, nothing reaches the stream.
    /// </param>
    /// <exception cref="XlsxSaveException">The Workbook is refused, and nothing was written.</exception>
    /// <exception cref="OperationCanceledException">The write was cancelled.</exception>
    public static async ValueTask WriteAsync(Workbook workbook, Stream stream, XlsxSaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new XlsxSaveOptions();
        if (options.MaxUncompressedBytes is < 1 or > XlsxReadOptions.DefaultMaxUncompressedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxUncompressedBytes,
                string.Create(CultureInfo.InvariantCulture, $"MaxUncompressedBytes is 1 to {XlsxReadOptions.DefaultMaxUncompressedBytes:N0}."));
        }

        // Everything the file says is taken here, synchronously, before the write first yields; the rest reads only this.
        var snapshot = Snapshot.Of(workbook);
        var pacer = new SavePacer(options, cancellationToken);
        XlsxWriter writer;
        try
        {
            writer = await BuildAsync(snapshot, stream, options, pacer).ConfigureAwait(false);
        }
        catch (XlsxException e) when (e.Reason == XlsxRefusal.TooLarge)
        {
            throw TooLarge(e, options);
        }
        await using (writer.ConfigureAwait(false))
        {
            // The parts written last are written here, before anything is handed to the stream.
            try
            {
                await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (XlsxException e) when (e.Reason == XlsxRefusal.TooLarge)
            {
                throw TooLarge(e, options);
            }
        }
    }

    private static XlsxSaveException TooLarge(XlsxException e, XlsxSaveOptions options) =>
        new(XlsxSaveRefusal.TooLarge, string.Create(CultureInfo.InvariantCulture,
            $"The workbook would hold more than the {options.MaxUncompressedBytes:N0} uncompressed bytes a file is written with, and read with by default; the part '{e.Part}' went past it."),
            e.Sheet);

    /// <summary>
    /// The Workbook as it stood when the write was called: all the write reads, taken synchronously, in time that grows with
    /// the cells holding a Value and not with what the file writes for them.
    /// </summary>
    private sealed record Snapshot(
        CultureInfo Culture, int Shown, IReadOnlyList<SheetSnapshot> Sheets, Dictionary<string, string> SheetNames,
        IReadOnlyList<(LinkedTable Table, IReadOnlyList<IReadOnlyList<Value?>>? Columns)> Tables)
    {
        public static Snapshot Of(Workbook workbook)
        {
            var document = workbook.ToDocument();
            var sheets = new List<SheetSnapshot>(workbook.Sheets.Count);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < workbook.Sheets.Count; i++)
            {
                var sheet = workbook.Sheets[i];
                var values = new Dictionary<CellAddress, Value>();
                var spilledFrom = new Dictionary<CellAddress, CellAddress>();
                foreach (var at in sheet.ValueAddresses)
                {
                    if (sheet.GetValue(at) is { } value) values[at] = value;
                    if (sheet.SpilledFrom(at) is { } anchor) spilledFrom[at] = anchor;
                }
                sheets.Add(new SheetSnapshot(sheet.Name, document.Sheets[i], values, spilledFrom));
                names.TryAdd(sheet.Name, sheet.Name);
            }
            // A table's rows are read-only, and replaced whole by a later snapshot: holding the reference holds them as they are.
            var tables = workbook.LinkedTables.Select(t => (t, t.IsWaiting ? null : workbook.LinkedTableColumns(t.Name))).ToList();
            return new Snapshot(workbook.Culture, IndexOf(workbook, workbook.Shown), sheets, names, tables);
        }
    }

    /// <summary>A Sheet as it stood: its document, the Value of every cell holding one, and the Anchor each spilled cell spills from.</summary>
    private sealed record SheetSnapshot(string Name, SheetDocument Document, Dictionary<CellAddress, Value> Values, Dictionary<CellAddress, CellAddress> SpilledFrom)
    {
        public Value? ValueAt(CellAddress at) => Values.TryGetValue(at, out var value) ? value : null;
    }

    /// <summary>One cell element a Sheet writes of its own: an Entry, a spilled Value, or a format.</summary>
    private readonly record struct Recorded(CellAddress At, Entry? Entry, int Style);

    private sealed record Planned(SheetSnapshot Sheet, SheetFormatPlan Plan, List<(CellAddress At, Entry? Entry, FormatParts Own)> Cells);

    /// <summary>A Linked Table to write as an Excel Table: its declaration, and its snapshot column by column.</summary>
    private sealed record PlannedTable(LinkedTable Table, IReadOnlyList<IReadOnlyList<Value?>> Columns);

    /// <summary>
    /// The snapshot written into a package held in memory, every part but those written on completion: first every element
    /// counted against the limit, then written, yielding between slices.
    /// </summary>
    private static async ValueTask<XlsxWriter> BuildAsync(Snapshot snapshot, Stream stream, XlsxSaveOptions options, SavePacer pacer)
    {
        pacer.Sheets = snapshot.Sheets.Count + snapshot.Tables.Count;
        var tables = await TablesAsync(snapshot, pacer).ConfigureAwait(false);
        var planned = new List<Planned>(snapshot.Sheets.Count);
        long cells = 0;
        foreach (var sheet in snapshot.Sheets)
        {
            if (!Carries(sheet.Name, inName: true))
            {
                throw new XlsxSaveException(XlsxSaveRefusal.Text,
                    "The Sheet's name holds a character a file cannot carry in a worksheet's name: a control character below U+0020, U+FFFE or U+FFFF, or half of a surrogate pair.", sheet.Name);
            }
            var own = Own(sheet);
            var plan = SheetFormatPlan.For(sheet.Document);
            var crossings = plan.Crossings(own.Select(c => c.At));
            var rows = plan.FormattedRows;
            cells += own.Count + crossings + rows;
            if (cells > options.MaxCells)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.TooManyCells, string.Create(CultureInfo.InvariantCulture,
                    $"The workbook would write more than the {options.MaxCells:N0} elements it may (XlsxSaveOptions.MaxCells, which counts the cells written and each row a row format writes; by default the cells a file is read with): this Sheet writes {own.Count:N0} cells of its own, {crossings:N0} where its formatted rows cross its formatted columns, and {rows:N0} rows its row formats write."),
                    sheet.Name);
            }
            planned.Add(new Planned(sheet, plan, own));
            await pacer.CheckpointAsync().ConfigureAwait(false);
        }
        foreach (var (table, columns) in tables)
        {
            cells += table.Columns.Count + columns.Sum(c => (long)c.Count(v => v is not null));
            if (cells > options.MaxCells)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.TooManyCells, string.Create(CultureInfo.InvariantCulture,
                    $"The workbook would write more than the {options.MaxCells:N0} elements it may (XlsxSaveOptions.MaxCells, which counts the cells written and each row a row format writes; by default the cells a file is read with): the Linked Table '{table.Name}' writes {table.RowCount:N0} rows of {table.Columns.Count:N0} columns under its header."),
                    table.Name);
            }
        }

        var styles = new StyleTable(snapshot.Culture);
        var sheets = new List<(SheetSnapshot Sheet, SheetFormatPlan Plan, List<Recorded> Cells, Dictionary<CellAddress, CellAddress> Spills)>(planned.Count);
        foreach (var (sheet, plan, own) in planned)
        {
            plan.Build(styles);
            var recorded = new List<Recorded>(own.Count);
            foreach (var (at, entry, parts) in own)
            {
                recorded.Add(new Recorded(at, entry, plan.Style(styles, parts, at)));
                await pacer.StepAsync(0).ConfigureAwait(false);
            }
            sheets.Add((sheet, plan, recorded, Spills(sheet)));
        }

        pacer.Total = cells;
        pacer.Sheet = 0;
        var writer = XlsxWriter.Create(stream, new XlsxWriteOptions
        {
            Styles = styles.Build(),
            ActiveSheet = snapshot.Shown,
            FullCalculationOnLoad = true,
            MaxUncompressedBytes = options.MaxUncompressedBytes,
        });
        var declared = snapshot.Tables.ToDictionary(t => t.Table.Name, t => t.Table.Columns, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string>? ColumnsOf(string name) => declared.GetValueOrDefault(name);
        try
        {
            foreach (var (sheet, plan, recorded, spills) in sheets)
            {
                await WriteSheetAsync(writer.AddSheet(sheet.Name, plan.Columns), sheet, snapshot, plan, recorded, spills, ColumnsOf, pacer).ConfigureAwait(false);
                pacer.Sheet++;
            }
            foreach (var table in tables)
            {
                await WriteTableAsync(writer, table, pacer).ConfigureAwait(false);
                pacer.Sheet++;
            }
            pacer.Sheet = Math.Max(0, pacer.Sheets - 1);
            pacer.Report();
            return writer;
        }
        catch
        {
            await writer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Each Linked Table with its snapshot, in the order they were declared, or the refusal of the first that cannot be written
    /// as an Excel Table on a worksheet of its name (ADR-0330 §6).
    /// </summary>
    private static async ValueTask<List<PlannedTable>> TablesAsync(Snapshot snapshot, SavePacer pacer)
    {
        var tables = new List<PlannedTable>(snapshot.Tables.Count);
        foreach (var (table, held) in snapshot.Tables)
        {
            var name = table.Name;
            if (table.IsWaiting || held is not { } columns)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                    $"The Linked Table '{name}' has not answered: it holds no rows yet, or its last snapshot was refused, and every Formula that reads it shows #GETTING_DATA. Its rows are written as they stand, and it has none to write.", name);
            }
            if (snapshot.SheetNames.TryGetValue(name, out var sheet))
            {
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                    $"The Linked Table '{name}' is written on a worksheet of its name, and the Sheet '{sheet}' already has it.", name);
            }
            if (!XlsxTable.IsValidName(name))
            {
                // The engine takes some names Excel gives no Table, such as A01, which a file reads as the cell A1.
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                    $"The Linked Table '{name}' is written as an Excel Table of its name, and Excel gives a Table no such name: it reads as a cell's address, or holds a character a Table's name does not.", name);
            }
            if (!Sheet.IsValidName(name, out var why))
            {
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                    $"The Linked Table '{name}' is written on a worksheet of its name, and a worksheet cannot have it: {why}", name);
            }
            if (table.Columns.Count > XlsxCellReference.MaxColumns)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable, string.Create(CultureInfo.InvariantCulture,
                    $"The Linked Table '{name}' has {table.Columns.Count:N0} columns, and a worksheet of Excel holds {XlsxCellReference.MaxColumns:N0}."), name);
            }
            if (table.RowCount == 0)
            {
                // ROWS(Positions[PV]) is 0 in ExSheet, and 1 in Excel, which holds a table of no rows as one empty row.
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable,
                    $"The Linked Table '{name}' holds no rows, and an Excel Table holds at least one: written with one empty row, its Formulas would answer otherwise in Excel (ROWS gives 1, COUNTBLANK 1, the column itself 0 where ExSheet shows #VALUE!).", name);
            }
            if (table.RowCount > MaxLinkedTableRows)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.LinkedTable, string.Create(CultureInfo.InvariantCulture,
                    $"The Linked Table '{name}' holds {table.RowCount:N0} rows, and a worksheet of Excel holds {XlsxCellReference.MaxRows:N0}, its header's included."), name);
            }
            for (var c = 0; c < columns.Count; c++)
            {
                CheckTableText(name, table.Columns[c], 0, c, "as a column's name");
                for (var r = 0; r < columns[c].Count; r++)
                {
                    if (columns[c][r] is { Kind: ValueKind.Text } value)
                        CheckTableText(name, value.Text, r + 1, c, string.Create(CultureInfo.InvariantCulture, $"in its column '{table.Columns[c]}', row {r + 1:N0},"));
                    await pacer.StepAsync(0).ConfigureAwait(false);
                }
            }
            tables.Add(new PlannedTable(table, columns));
        }
        return tables;
    }

    /// <summary>Refuses text of a Linked Table that a cell of Excel cannot hold: longer than 32,767 characters, or holding half of a surrogate pair.</summary>
    /// <param name="table">The table's name, which its worksheet has.</param>
    /// <param name="text">The text.</param>
    /// <param name="row">The worksheet's row it is written in, from zero: the header's is 0.</param>
    /// <param name="column">The worksheet's column it is written in, from zero.</param>
    /// <param name="at">Where the table holds it, in words.</param>
    private static void CheckTableText(string table, string text, int row, int column, string at)
    {
        var cell = XlsxCellReference.Format(row, column);
        if (text.Length > MaxTextLength)
        {
            throw new XlsxSaveException(XlsxSaveRefusal.Text, string.Create(CultureInfo.InvariantCulture,
                $"The Linked Table '{table}' holds, {at} text {text.Length:N0} characters long, and a cell of Excel holds at most {MaxTextLength:N0}."), table, cell);
        }
        if (!Carries(text, inName: false))
            throw new XlsxSaveException(XlsxSaveRefusal.Text, $"The Linked Table '{table}' holds, {at} text with half of a surrogate pair, which a file cannot carry.", table, cell);
    }

    /// <summary>A Linked Table's worksheet: its header at A1, each column's name, and its rows below as they stand, as an Excel Table of its name.</summary>
    private static async ValueTask WriteTableAsync(XlsxWriter writer, PlannedTable planned, SavePacer pacer)
    {
        var (table, columns) = planned;
        var sheet = writer.AddSheet(table.Name);
        sheet.AddTable(new XlsxTable(table.Name, 0, 0, table.Columns, table.RowCount));
        for (var c = 0; c < table.Columns.Count; c++)
        {
            sheet.Write(new XlsxCell(0, c, XlsxCellType.SharedString, null) { Text = table.Columns[c] });
            await pacer.StepAsync().ConfigureAwait(false);
        }
        for (var r = 0; r < table.RowCount; r++)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                if (columns[c][r] is not { } value) continue;
                var cell = new XlsxCell(r + 1, c, XlsxCellType.Number, null);
                sheet.Write(value.Kind switch
                {
                    ValueKind.Number => cell with { Value = value.Number.ToString("R", CultureInfo.InvariantCulture) },
                    ValueKind.Text => cell with { Type = XlsxCellType.SharedString, Text = value.Text },
                    ValueKind.Boolean => cell with { Type = XlsxCellType.Boolean, Value = value.Boolean ? "1" : "0" },
                    _ => cell with { Type = XlsxCellType.Error, Value = value.ToString() },
                });
                await pacer.StepAsync().ConfigureAwait(false);
            }
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
    private static List<(CellAddress At, Entry? Entry, FormatParts Own)> Own(SheetSnapshot sheet)
    {
        var cells = new Dictionary<CellAddress, (Entry? Entry, FormatParts Own)>(sheet.Document.Cells.Count);
        foreach (var cell in sheet.Document.Cells)
            cells[cell.Address] = (cell.Entry, new FormatParts(cell.NumberFormat, cell.Alignment, cell.Font, cell.Fill, cell.Borders));
        foreach (var at in sheet.SpilledFrom.Keys) cells.TryAdd(at, (null, default));
        var ordered = new List<(CellAddress, Entry?, FormatParts)>(cells.Count);
        foreach (var (at, (entry, own)) in cells.OrderBy(c => c.Key)) ordered.Add((at, entry, own));
        return ordered;
    }

    /// <summary>Each spilling Anchor, with the last cell of its Spill Range.</summary>
    private static Dictionary<CellAddress, CellAddress> Spills(SheetSnapshot sheet)
    {
        var spills = new Dictionary<CellAddress, CellAddress>();
        foreach (var (at, anchor) in sheet.SpilledFrom)
        {
            spills[anchor] = spills.TryGetValue(anchor, out var last)
                ? new CellAddress(Math.Max(last.Row, at.Row), Math.Max(last.Column, at.Column))
                : at;
        }
        return spills;
    }

    /// <summary>A Sheet's rows, in order: each formatted row with its format, and every cell element in it, its own and its crossings'.</summary>
    private static async ValueTask WriteSheetAsync(
        XlsxSheetWriter writer, SheetSnapshot sheet, Snapshot snapshot, SheetFormatPlan plan, List<Recorded> cells,
        Dictionary<CellAddress, CellAddress> spills, Func<string, IReadOnlyList<string>?> columnsOf, SavePacer pacer)
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
                await pacer.StepAsync().ConfigureAwait(false);
                var at = next;
                foreach (var crossing in formatted.Crossings)
                {
                    for (var column = crossing.First; column <= crossing.Last; column++)
                    {
                        while (at < end && cells[at].At.Column < column)
                        {
                            WriteCell(writer, sheet, snapshot, cells[at++], spills, columnsOf);
                            await pacer.StepAsync().ConfigureAwait(false);
                        }
                        if (at < end && cells[at].At.Column == column) continue;
                        writer.Write(new XlsxCell(row, column, XlsxCellType.Number, null, StyleIndex: crossing.Style));
                        await pacer.StepAsync().ConfigureAwait(false);
                    }
                }
                while (at < end)
                {
                    WriteCell(writer, sheet, snapshot, cells[at++], spills, columnsOf);
                    await pacer.StepAsync().ConfigureAwait(false);
                }
                runRow++;
                if (runRow > formatted.Last)
                {
                    run++;
                    runRow = run < runs.Count ? runs[run].First : int.MaxValue;
                }
            }
            else
            {
                for (var at = next; at < end; at++)
                {
                    WriteCell(writer, sheet, snapshot, cells[at], spills, columnsOf);
                    await pacer.StepAsync().ConfigureAwait(false);
                }
            }
            next = end;
        }
    }

    /// <summary>One cell of the Sheet's own: its constant, or its Formula with the Value it shows, or a spilled Value, or its format alone.</summary>
    private static void WriteCell(
        XlsxSheetWriter writer, SheetSnapshot sheet, Snapshot snapshot, Recorded cell, Dictionary<CellAddress, CellAddress> spills,
        Func<string, IReadOnlyList<string>?> columnsOf)
    {
        var at = cell.At;
        var empty = new XlsxCell(at.Row, at.Column, XlsxCellType.Number, null, StyleIndex: cell.Style);
        if (cell.Entry is { Constant: { } constant })
        {
            writer.Write(constant.Kind == ValueKind.Text ? Shared(sheet, at, constant.Text, empty) : Valued(sheet, at, constant, empty, null));
            return;
        }
        var value = sheet.ValueAt(at);
        if (cell.Entry is { Formula: { } formula } entry)
        {
            if (formula.Length - 1 > MaxFormulaLength)
            {
                throw new XlsxSaveException(XlsxSaveRefusal.Formula, string.Create(CultureInfo.InvariantCulture,
                    $"The Formula is {formula.Length - 1:N0} characters long, and Excel holds at most {MaxFormulaLength:N0}."), sheet.Name, at.ToString());
            }
            if (!Carries(formula, inName: false))
            {
                throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                    "The Formula holds half of a surrogate pair, which a file cannot carry.", sheet.Name, at.ToString(), formula);
            }
            var stored = WrittenFormula.Write(entry, snapshot.SheetNames.ContainsKey, out var problem, columnsOf);
            switch (problem)
            {
                case WrittenFormulaProblem.BoundCall:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula calls a name a LET binds or a LAMBDA takes, which ExSheet reads as an unknown function and Excel would call.", sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.LongText:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula, string.Create(CultureInfo.InvariantCulture,
                        $"The Formula holds a text constant longer than the {WrittenFormula.MaxText} characters Excel takes in one."), sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.TooDeep:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula, string.Create(CultureInfo.InvariantCulture,
                        $"The Formula nests its calls deeper than the {WrittenFormula.MaxDepth} levels Excel takes."), sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.TooManyArguments:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula, string.Create(CultureInfo.InvariantCulture,
                        $"The Formula makes a call with more than the {WrittenFormula.MaxArguments} arguments Excel takes."), sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.MissingSheet:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula reads a Sheet the Workbook does not have, which Excel would read as another workbook's.", sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.StructuredReference:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula reads, by a structured reference, a Linked Table or a column the Workbook does not declare; the file would hold no Excel Table of it for Excel to read.", sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.ColumnName:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula reads a Linked Table's column whose name begins or ends with a space, which a structured reference in Excel does not keep, or holds @ or one of , : . \" { } $ ^ & * + = - < > / or a tab or line break, whose form in a structured reference Excel has not been seen to write.", sheet.Name, at.ToString(), formula);
                case WrittenFormulaProblem.TableName:
                    throw new XlsxSaveException(XlsxSaveRefusal.Formula,
                        "The Formula reads a Linked Table's name as a name of its own, which ExSheet shows as #NAME? and Excel would read as the Excel Table written.", sheet.Name, at.ToString(), formula);
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
    private static XlsxCell Valued(SheetSnapshot sheet, CellAddress at, Value value, XlsxCell cell, string? formula)
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

    private static XlsxCell Shared(SheetSnapshot sheet, CellAddress at, string text, XlsxCell cell)
    {
        CheckText(sheet, at, text, null);
        return cell with { Type = XlsxCellType.SharedString, Text = text };
    }

    /// <summary>
    /// Whether a file carries <paramref name="text"/>, as the file layer writes it: no half of a surrogate pair anywhere, and in
    /// a worksheet's name, which is written as it is, no control character below U+0020 and no U+FFFE or U+FFFF. Elsewhere the
    /// file layer escapes those (<c>_x0001_</c>).
    /// </summary>
    private static bool Carries(string text, bool inName)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
                continue;
            }
            if (char.IsSurrogate(c)) return false;
            if (inName && (c < 0x20 || c is '\uFFFE' or '\uFFFF')) return false;
        }
        return true;
    }

    private static void CheckText(SheetSnapshot sheet, CellAddress at, string text, string? formula)
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
