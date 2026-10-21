using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// Reads an <c>.xlsx</c> workbook into a Workbook Document and its Open Report (ADR-0330 §3, §4, §5, §7). Every worksheet
/// becomes a Sheet, in the file's order, and the worksheet shown is the Sheet shown. Constants — numbers, text, booleans,
/// Error Values — and Formulas are carried, never a Value the file stored: opening the document computes every Value
/// again. Anything that would change a Value is refused by name, naming the Sheet, the cell and what it held, and nothing
/// opens. What changes only how a Sheet looks, or what else the file held, is listed in the Open Report.
/// </summary>
/// <remarks>
/// Cell Formats cross at the levels the file records them — cell, row and column — with the column widths, in Excel's own
/// unit: the Number Format, horizontal alignment, the Font's colour and emphasis, a solid Fill and the four sides' borders
/// (ADR-0071). A colour that names the theme is the RGB the file's theme gives it (ADR-0330 §8). A Number Format the engine
/// does not read refuses the file by name, as a Formula does.
/// </remarks>
public static class XlsxWorkbookReader
{
    /// <summary>
    /// Reads the workbook in <paramref name="stream"/> into a Workbook Document under <paramref name="culture"/>, with its
    /// Open Report, or refuses it by name (<see cref="XlsxOpenException"/>). The read goes a batch of cells at a time,
    /// reporting progress and yielding between slices as <paramref name="options"/> says, so a browser keeps painting while
    /// a large file is read.
    /// </summary>
    /// <param name="stream">The file, from its current position. The read owns it from here, and disposes it.</param>
    /// <param name="culture">
    /// The Workbook's culture, declared by the application as a CSV's encoding is (ADR-0330 §7): how typing is read and
    /// Values are shown from here on. No Value read from the file depends on it, and nothing in the file is taken for it.
    /// </param>
    /// <param name="options">The file layer's limits, progress and pacing (ADR-0330 §9); null for its defaults.</param>
    /// <param name="cancellationToken">Cancels the read, looked at between slices; a cancelled read opens nothing.</param>
    /// <exception cref="XlsxOpenException">The workbook is refused, and nothing opens.</exception>
    /// <exception cref="ArgumentException">The culture is not one the engine knows by name, so the document could not be opened again.</exception>
    public static async ValueTask<XlsxOpenResult> ReadAsync(Stream stream, CultureInfo culture, XlsxReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(culture);
        try
        {
            // Refused before the file is read: a document under a culture the engine cannot name could not be opened.
            _ = new WorkbookDocument(culture, "Sheet1", [new SheetDocument(culture, "Sheet1", [])]);
        }
        catch (ArgumentException e)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw new ArgumentException(e.Message, nameof(culture), e);
        }

        XlsxReader reader;
        try
        {
            reader = await XlsxReader.OpenAsync(stream, options, cancellationToken).ConfigureAwait(false);
        }
        catch (XlsxException e)
        {
            throw XlsxOpenException.FromFile(e);
        }
        using (reader)
        {
            try
            {
                return await new Mapping(reader, culture, cancellationToken).ReadAsync().ConfigureAwait(false);
            }
            catch (XlsxException e)
            {
                throw XlsxOpenException.FromFile(e);
            }
        }
    }

    /// <summary>One workbook's mapping, Sheet by Sheet.</summary>
    private sealed class Mapping(XlsxReader reader, CultureInfo culture, CancellationToken cancellationToken)
    {
        /// <summary>How many days the 1904 date system's serials lie below 1900's for the same day.</summary>
        private const int Days1904 = 1462;

        private readonly List<NotCarried> notCarried = [];
        private readonly List<UnsupportedFunctionCell> unsupported = [];
        private readonly HashSet<string> definedNames = new(reader.DefinedNames.Select(n => n.Name), StringComparer.OrdinalIgnoreCase);
        private readonly bool hasExcelTables = reader.Parts.Any(p => p.StartsWith("xl/tables/", StringComparison.OrdinalIgnoreCase));
        private readonly Dictionary<int, bool> dateStyles = [];
        private readonly StyleReading styles = new(reader.Styles, reader.Theme);
        private bool namesTheme;

        public async ValueTask<XlsxOpenResult> ReadAsync()
        {
            var worksheets = new List<int>();
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.Sheets.Count; i++)
            {
                var info = reader.Sheets[i];
                if (info.Kind != XlsxSheetKind.Worksheet || info.Part is null)
                {
                    notCarried.Add(new(NotCarriedKind.ChartSheet, info.Name, null, $"The sheet '{info.Name}' is not a worksheet (a chart sheet, or another that holds no cells), and is not opened."));
                    continue;
                }
                if (!Sheet.IsValidName(info.Name, out var why))
                    throw new XlsxOpenException(XlsxOpenRefusal.Sheets, $"The worksheet's name is not one a Sheet can have: {why}", info.Name);
                if (!names.TryAdd(info.Name, info.Name))
                {
                    var same = names[info.Name];
                    throw new XlsxOpenException(XlsxOpenRefusal.Sheets,
                        $"The worksheets '{same}' and '{info.Name}' have names that differ only in case, which two Sheets of one Workbook cannot have.", info.Name);
                }
                worksheets.Add(i);
            }
            if (worksheets.Count == 0)
                throw new XlsxOpenException(XlsxOpenRefusal.Sheets, "The workbook holds no worksheet, so it holds no cells to open.");

            WorkbookParts();
            foreach (var name in reader.DefinedNames)
            {
                // A name local to a worksheet is said with that Sheet's entries; one of the workbook's, or of a chart sheet, here.
                if (name.LocalSheet is { } local && worksheets.Contains(local)) continue;
                if (BuiltInName(name) is not { } shown) continue;
                var sheet = name.LocalSheet is { } other && other >= 0 && other < reader.Sheets.Count ? reader.Sheets[other].Name : null;
                notCarried.Add(new(NotCarriedKind.DefinedName, sheet, shown, DescribeName(name, shown, sheet)));
            }
            var workbookEntries = notCarried.Count;
            var sheets = new List<SheetDocument>(worksheets.Count);
            foreach (var index in worksheets)
            {
                var info = reader.Sheets[index];
                if (info.Visibility != XlsxSheetVisibility.Visible)
                    notCarried.Add(new(NotCarriedKind.HiddenSheet, info.Name, null, $"The Sheet '{info.Name}' is hidden in the file, and opens shown."));
                var printNames = false;
                foreach (var name in reader.DefinedNames.Where(n => n.LocalSheet == index))
                {
                    if (name.Name.Equals(PrintArea, StringComparison.OrdinalIgnoreCase) || name.Name.Equals(PrintTitles, StringComparison.OrdinalIgnoreCase))
                        printNames = true;
                    else if (BuiltInName(name) is { } shown)
                        notCarried.Add(new(NotCarriedKind.DefinedName, info.Name, shown, DescribeName(name, shown, info.Name)));
                }
                sheets.Add(await ReadSheetAsync(index, printNames).ConfigureAwait(false));
            }
            if (namesTheme)
            {
                // Said of the whole workbook, with what else it holds, before each Sheet's entries.
                notCarried.Insert(workbookEntries, new(NotCarriedKind.ThemeColours, null, null,
                    "Colours of the workbook name its theme: each is carried as the RGB value the file's theme gives it, and the link to the theme is not, so another theme will not recolour them."));
            }
            var shownIndex = worksheets.Contains(reader.ActiveSheet) ? reader.ActiveSheet : worksheets[0];
            var document = new WorkbookDocument(culture, reader.Sheets[shownIndex].Name, sheets);
            return new XlsxOpenResult(document, new OpenReport(notCarried, unsupported));
        }

        private const string FilterDatabase = "_xlnm._FilterDatabase";
        private const string PrintArea = "_xlnm.Print_Area";
        private const string PrintTitles = "_xlnm.Print_Titles";

        /// <summary>
        /// The name a defined name is listed under, or null when it is not listed: <c>_xlnm._FilterDatabase</c> is the range an
        /// AutoFilter keeps, which the AutoFilter's own entry says. A print area or titles is a print setting of its Sheet's.
        /// </summary>
        private static string? BuiltInName(XlsxDefinedName name) => name.Name.Equals(FilterDatabase, StringComparison.OrdinalIgnoreCase) ? null : name.Name;

        private static string DescribeName(XlsxDefinedName name, string shown, string? sheet) => sheet is null
            ? $"The defined name '{shown}' ({name.Text}) is not carried; no Formula carried reads it."
            : $"The defined name '{shown}' of the sheet '{sheet}' ({name.Text}) is not carried; no Formula carried reads it.";

        /// <summary>What the workbook as a whole holds, beside its sheets, as its parts say.</summary>
        private void WorkbookParts()
        {
            var parts = reader.Parts.ToList();
            bool Has(string folder) => parts.Any(p => p.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
            if (Has("xl/pivotTables/"))
                notCarried.Add(new(NotCarriedKind.PivotTables, null, null, "The workbook holds PivotTables: the cells they show are carried as constants, the PivotTables are not."));
            if (Has("xl/externalLinks/"))
                notCarried.Add(new(NotCarriedKind.ExternalLinks, null, null, "The workbook holds links to other workbooks, which no Formula carried reads; they are not carried."));
            if (Has("xl/connections.xml") || Has("xl/queryTables/"))
                notCarried.Add(new(NotCarriedKind.DataConnections, null, null, "The workbook holds connections to outside data, which are not carried; the cells they filled are carried as constants."));
            if (Has("xl/slicers/") || Has("xl/slicerCaches/") || Has("xl/timelines/") || Has("xl/timelineCaches/"))
                notCarried.Add(new(NotCarriedKind.Slicers, null, null, "The workbook holds slicers or timelines, which are not carried."));
        }

        private async ValueTask<SheetDocument> ReadSheetAsync(int index, bool printNames)
        {
            using var sheet = reader.OpenSheet(index);
            var name = sheet.Sheet.Name;
            var cells = new List<SheetDocumentCell>();
            var shared = new Dictionary<int, SharedMaster>();
            var spills = new List<CellRange>();
            var spillRow = -1;
            var richCells = 0;
            string? firstRich = null;
            var formats = new List<(NotCarriedKind Kind, string Detail)>();
            ColumnLevels? columns = null;
            var rowRuns = new List<SheetDocumentAxisFormat>();
            var rowLevels = new Dictionary<int, FormatParts>();
            (int Row, FormatParts Level)? lastRow = null;
            while (await sheet.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The columns come before the cells, complete once the first batch is read.
                columns ??= ReadColumns(name, sheet.Columns, formats);
                // A row is begun in the batch of its first cell or an earlier one; only the last row begun can hold cells of a later batch.
                rowLevels.Clear();
                if (lastRow is { } carried) rowLevels[carried.Row] = carried.Level;
                foreach (var row in sheet.Rows)
                {
                    var level = ReadRow(name, row, columns, formats);
                    rowLevels[row.Row] = level;
                    lastRow = (row.Row, level);
                    AddRun(rowRuns, row.Row, level);
                }
                foreach (var cell in sheet.Cells)
                {
                    if (cell.IsRichText)
                    {
                        richCells++;
                        firstRich ??= cell.Reference;
                    }
                    if (cell.Row != spillRow)
                    {
                        // Cells come row by row: a Spill Range whose last row lies above this one is behind the reader for good.
                        spillRow = cell.Row;
                        if (spills.Count > 0) spills.RemoveAll(s => s.Last.Row < spillRow);
                    }
                    var entry = cell.Formula is { } formula
                        ? FormulaEntry(name, cell, formula, shared, spills)
                        : InsideSpill(spills, new CellAddress(cell.Row, cell.Column)) ? null : Constant(name, cell);
                    var own = ReadCell(name, cell, columns, rowLevels.TryGetValue(cell.Row, out var rowLevel) ? rowLevel : default, formats);
                    if (entry is not null || !own.IsEmpty)
                        cells.Add(new SheetDocumentCell(new CellAddress(cell.Row, cell.Column), entry, own.NumberFormat, own.Alignment, own.Font, own.Fill, own.Borders));
                }
            }
            columns ??= ReadColumns(name, sheet.Columns, formats);
            SheetElements(name, sheet, richCells, firstRich, printNames, formats);
            return new SheetDocument(culture, name, cells) { Columns = columns.Runs, Rows = rowRuns, ColumnWidths = Widths(sheet.Columns) };
        }

        /// <summary>
        /// The formats the columns record, by column, with the parts any of them records: a formatted row records those parts
        /// even at their defaults, because in the file a row's format stands in place of its columns', where in a Sheet each part
        /// of a row's level that records nothing takes the column's.
        /// </summary>
        private sealed record ColumnLevels(Dictionary<int, FormatParts> ByColumn, List<SheetDocumentAxisFormat> Runs, FormatParts Recorded);

        /// <summary>The cell format at <paramref name="style"/>, refusing the file where a level records a Number Format that cannot cross.</summary>
        private ReadFormat Format(string sheet, int style, string place, List<(NotCarriedKind Kind, string Detail)> formats)
        {
            var format = styles[style];
            if (format.Refusal is { } refusal)
                throw new XlsxOpenException(XlsxOpenRefusal.NumberFormat, refusal, sheet, place, format.Held);
            namesTheme |= format.NamesTheme;
            foreach (var kind in format.NotCarried)
            {
                if (!formats.Any(f => f.Kind == kind)) formats.Add((kind, place));
            }
            return format;
        }

        private ColumnLevels ReadColumns(string sheet, IReadOnlyList<XlsxColumn> columns, List<(NotCarriedKind Kind, string Detail)> formats)
        {
            var byColumn = new Dictionary<int, FormatParts>();
            var runs = new List<SheetDocumentAxisFormat>();
            foreach (var range in columns.OrderBy(c => c.First))
            {
                if (range.StyleIndex == 0) continue;
                var place = CellAddress.ColumnName(range.First) + ":" + CellAddress.ColumnName(range.Last);
                var level = FormatParts.Differing(Format(sheet, range.StyleIndex, place, formats).Format, CellFormat.Default);
                if (level.IsEmpty) continue;
                for (var c = range.First; c <= range.Last; c++) byColumn[c] = level;
                if (runs.Count > 0 && runs[^1] is var last && last.Last == range.First - 1 && Parts(last) == level)
                    runs[^1] = last with { Last = range.Last };
                else
                    runs.Add(new SheetDocumentAxisFormat(range.First, range.Last, level.NumberFormat, level.Alignment, level.Font, level.Fill, level.Borders));
            }
            var recorded = new FormatParts(
                runs.Any(r => r.NumberFormat is not null) ? NumberFormat.General : null,
                runs.Any(r => r.Alignment is not null) ? HorizontalAlignment.General : null,
                runs.Any(r => r.Font is not null) ? CellFont.Default : null,
                runs.Any(r => r.Fill is not null) ? CellFill.None : null,
                runs.Any(r => r.Borders is not null) ? CellBorders.None : null);
            return new ColumnLevels(byColumn, runs, recorded);
        }

        /// <summary>
        /// A formatted row's level: each part that differs from the default, and each part a column records, so that the row
        /// stands in place of its columns as in the file.
        /// </summary>
        private FormatParts ReadRow(string sheet, XlsxRow row, ColumnLevels columns, List<(NotCarriedKind Kind, string Detail)> formats)
        {
            var number = (row.Row + 1).ToString(CultureInfo.InvariantCulture);
            var format = Format(sheet, row.StyleIndex, number + ":" + number, formats).Format;
            var differing = FormatParts.Differing(format, CellFormat.Default);
            var recorded = columns.Recorded;
            return new FormatParts(
                differing.NumberFormat ?? (recorded.NumberFormat is null ? null : format.NumberFormat),
                differing.Alignment ?? (recorded.Alignment is null ? null : format.Alignment),
                differing.Font ?? (recorded.Font is null ? null : format.Font),
                differing.Fill ?? (recorded.Fill is null ? null : format.Fill),
                differing.Borders ?? (recorded.Borders is null ? null : format.Borders));
        }

        /// <summary>A cell's own level: the parts of its format that differ from what its row and column give it. In the file a cell's format stands alone.</summary>
        private FormatParts ReadCell(string sheet, XlsxCell cell, ColumnLevels columns, FormatParts row, List<(NotCarriedKind Kind, string Detail)> formats)
        {
            var format = Format(sheet, cell.StyleIndex, cell.Reference, formats).Format;
            var column = columns.ByColumn.TryGetValue(cell.Column, out var level) ? level : default;
            return FormatParts.Differing(format, row.Over(column.Over(CellFormat.Default)));
        }

        private static FormatParts Parts(SheetDocumentAxisFormat run) => new(run.NumberFormat, run.Alignment, run.Font, run.Fill, run.Borders);

        /// <summary>Adds a formatted row to the runs, joining the run before it when it is the next row, formatted alike.</summary>
        private static void AddRun(List<SheetDocumentAxisFormat> runs, int row, FormatParts level)
        {
            if (level.IsEmpty) return;
            if (runs.Count > 0 && runs[^1] is var last && last.Last == row - 1 && Parts(last) == level)
                runs[^1] = last with { Last = row };
            else
                runs.Add(new SheetDocumentAxisFormat(row, row, level.NumberFormat, level.Alignment, level.Font, level.Fill, level.Borders));
        }

        /// <summary>The columns' widths in the engine's unit (<see cref="ColumnWidthUnit"/>), a run wherever adjacent columns share one; a width fitted to an entry (<c>bestFit</c>) is one an entry widened.</summary>
        private static List<SheetDocumentColumnWidth> Widths(IReadOnlyList<XlsxColumn> columns)
        {
            var runs = new List<SheetDocumentColumnWidth>();
            foreach (var range in columns.OrderBy(c => c.First))
            {
                if (range.Width is not { } width || ColumnWidthUnit.Read(width) is not { } characters) continue;
                var kind = range.BestFit ? SheetColumnWidthKind.WidenedByEntry : SheetColumnWidthKind.SetByUser;
                if (runs.Count > 0 && runs[^1] is var last && last.Last == range.First - 1 && last.Width == characters && last.Kind == kind)
                    runs[^1] = last with { Last = range.Last };
                else
                    runs.Add(new SheetDocumentColumnWidth(range.First, range.Last, characters, kind));
            }
            return runs;
        }

        /// <summary>A shared formula's master: its Entry where it stands, and what it names.</summary>
        private sealed record SharedMaster(Entry Entry, int Row, int Column, IReadOnlyList<string> Unsupported);

        private Entry FormulaEntry(string sheet, XlsxCell cell, XlsxFormula formula, Dictionary<int, SharedMaster> shared, List<CellRange> spills)
        {
            var at = cell.Reference;
            switch (formula.Kind)
            {
                case XlsxFormulaKind.DataTable:
                    throw new XlsxOpenException(XlsxOpenRefusal.UnreadableFormula,
                        "The cell is part of a What-If data table, which the engine does not compute.", sheet, at, Held(formula.Text));
                case XlsxFormulaKind.Array when !formula.IsDynamicArray:
                    throw new XlsxOpenException(XlsxOpenRefusal.LegacyArrayFormula,
                        "The cell holds a legacy array formula (entered with Ctrl+Shift+Enter), which Excel computes as one over its cells and ExSheet would spill instead.",
                        sheet, at, "{" + Held(formula.Text) + "}");
                case XlsxFormulaKind.Shared when formula.Text.Length == 0:
                    if (formula.SharedIndex is not { } si || !shared.TryGetValue(si, out var master))
                    {
                        throw new XlsxOpenException(XlsxOpenRefusal.UnreadableFormula,
                            string.Create(CultureInfo.InvariantCulture, $"The cell takes the shared formula {formula.SharedIndex}, whose first cell does not come before it."), sheet, at);
                    }
                    var moved = master.Entry.MovedBy(cell.Row - master.Row, cell.Column - master.Column);
                    Report(sheet, at, master.Unsupported);
                    return moved;
            }
            var (entry, names) = Read(sheet, at, formula.Text);
            Report(sheet, at, names);
            if (formula.Kind == XlsxFormulaKind.Shared && formula.SharedIndex is { } index)
                shared[index] = new SharedMaster(entry, cell.Row, cell.Column, names);
            if (formula.Kind == XlsxFormulaKind.Array && formula.Reference is { } range && Range(range) is { } spill)
                spills.Add(spill);
            return entry;
        }

        /// <summary>Reads a Formula's stored text into its Entry, refusing what would read wrongly, with the functions it names that ExSheet does not support.</summary>
        private (Entry Entry, IReadOnlyList<string> Unsupported) Read(string sheet, string at, string stored)
        {
            var text = StoredFormula.Read(stored, out var problem);
            switch (problem)
            {
                case StoredFormulaProblem.OtherWorkbook:
                    throw new XlsxOpenException(XlsxOpenRefusal.OtherWorkbook, "The Formula reads another workbook, which ExSheet cannot reach.", sheet, at, text);
                case StoredFormulaProblem.ExcelTable:
                    throw new XlsxOpenException(XlsxOpenRefusal.ExcelTable, "The Formula reads an Excel Table, which ExSheet does not read.", sheet, at, text);
                case StoredFormulaProblem.ImplicitIntersection:
                    throw new XlsxOpenException(XlsxOpenRefusal.UnreadableFormula, "The Formula uses the implicit intersection operator (@), which the engine does not read.", sheet, at, text);
                case StoredFormulaProblem.SpillRange:
                    throw new XlsxOpenException(XlsxOpenRefusal.UnreadableFormula, "The Formula reads a Spill Range in a form the engine does not read.", sheet, at, text);
            }
            Entry entry;
            try
            {
                entry = Entry.Parse(text, CultureInfo.InvariantCulture)!;
            }
            catch (FormulaSyntaxException e)
            {
                throw new XlsxOpenException(XlsxOpenRefusal.UnreadableFormula, $"The engine cannot read the Formula: {e.Message}", sheet, at, text, e);
            }
            foreach (var name in entry.UnboundNames)
            {
                if (definedNames.Contains(name))
                    throw new XlsxOpenException(XlsxOpenRefusal.DefinedName, $"The Formula reads the defined name '{name}', which ExSheet does not define.", sheet, at, text);
                if (hasExcelTables)
                {
                    throw new XlsxOpenException(XlsxOpenRefusal.ExcelTable,
                        $"The Formula reads the name '{name}', which the workbook does not define; it holds Excel Tables, which a name can read, and ExSheet does not read them.",
                        sheet, at, text);
                }
            }
            var functions = entry.UndeclaredFunctions;
            if (functions.FirstOrDefault(definedNames.Contains) is { } lambda)
                throw new XlsxOpenException(XlsxOpenRefusal.DefinedName, $"The Formula calls the defined name '{lambda}', which ExSheet does not define.", sheet, at, text);
            return (entry, functions);
        }

        private void Report(string sheet, string at, IReadOnlyList<string> functions)
        {
            if (functions.Count > 0) unsupported.Add(new UnsupportedFunctionCell(sheet, at, functions));
        }

        private static string Held(string stored) => "=" + StoredFormula.Strip(stored);

        /// <summary>A constant's Entry, or null for a cell that holds none (a cell with a style only).</summary>
        private Entry? Constant(string sheet, XlsxCell cell)
        {
            var at = cell.Reference;
            switch (cell.Type)
            {
                case XlsxCellType.SharedString or XlsxCellType.InlineString:
                    return Entry.FromValue(Value.FromText(cell.Text ?? ""));
                case XlsxCellType.FormulaString:
                    return cell.Value is null ? null : Entry.FromValue(Value.FromText(cell.Value));
                case XlsxCellType.Number:
                    if (cell.Value is null) return null;
                    if (!double.TryParse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                        throw new XlsxOpenException(XlsxOpenRefusal.UnreadableConstant, "The cell's number cannot be read.", sheet, at, cell.Value);
                    if (reader.Date1904 && ShowsDate(cell.StyleIndex)) number += Days1904;
                    return Entry.FromValue(Value.FromNumber(number));
                case XlsxCellType.Boolean:
                    return cell.Value switch
                    {
                        "1" or "true" => Entry.FromValue(Value.FromBoolean(true)),
                        "0" or "false" => Entry.FromValue(Value.FromBoolean(false)),
                        null => null,
                        _ => throw new XlsxOpenException(XlsxOpenRefusal.UnreadableConstant, "The cell's boolean cannot be read.", sheet, at, cell.Value),
                    };
                case XlsxCellType.Error:
                    if (cell.Value is null) return null;
                    if (Entry.FromErrorText(cell.Value) is { } error) return error;
                    throw new XlsxOpenException(XlsxOpenRefusal.UnreadableConstant, "The cell holds an Error Value a cell of ExSheet cannot hold.", sheet, at, cell.Value);
                case XlsxCellType.Date:
                    if (cell.Value is null) return null;
                    if (IsoDate.Serial(cell.Value) is not { } serial)
                    {
                        throw new XlsxOpenException(XlsxOpenRefusal.UnreadableConstant,
                            "The cell's date is not an ISO 8601 date or time, or lies outside the dates a serial counts.", sheet, at, cell.Value);
                    }
                    return Entry.FromValue(Value.FromNumber(serial));
                default:
                    return null;
            }
        }

        /// <summary>Whether the cell format at <paramref name="style"/> shows a date (its serial moves from 1904's to 1900's).</summary>
        private bool ShowsDate(int style)
        {
            if (dateStyles.TryGetValue(style, out var known)) return known;
            var id = reader.Styles.CellFormats[style].NumberFormatId;
            var shows = DateFormats.ShowsDate(id, reader.Styles.NumberFormats.GetValueOrDefault(id));
            dateStyles[style] = shows;
            return shows;
        }

        /// <summary>
        /// Whether a cell lies in a dynamic array's Spill Range, whose cells hold the Values Excel stored, not constants. The
        /// list holds only the ranges not yet behind the reader, pruned as each row begins.
        /// </summary>
        private static bool InsideSpill(List<CellRange> spills, CellAddress cell)
        {
            foreach (var spill in spills)
            {
                if (spill.Contains(cell)) return true;
            }
            return false;
        }

        /// <summary>A range written <c>A1:B3</c> or <c>A1</c>, or null when it is not one.</summary>
        private static CellRange? Range(string text)
        {
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            var first = colon < 0 ? text : text[..colon];
            var last = colon < 0 ? text : text[(colon + 1)..];
            if (!XlsxCellReference.TryParse(first, out var r1, out var c1) || !XlsxCellReference.TryParse(last, out var r2, out var c2)) return null;
            return new CellRange(new CellAddress(r1, c1), new CellAddress(r2, c2));
        }

        /// <summary>What else a worksheet held, as the file layer reports it, said once per Sheet and kind.</summary>
        private void SheetElements(string sheet, XlsxSheetReader read, int richCells, string? firstRich, bool printNames, List<(NotCarriedKind Kind, string Detail)> formats)
        {
            var kinds = new List<(NotCarriedKind Kind, string? Detail)>();
            void Add(NotCarriedKind kind, string? detail = null)
            {
                if (!kinds.Any(k => k.Kind == kind && k.Detail == detail)) kinds.Add((kind, detail));
            }
            if (read.CustomRowHeights) Add(NotCarriedKind.RowHeights);
            if (read.HiddenRows) Add(NotCarriedKind.HiddenRows);
            if (read.Columns.Any(c => c.Hidden || c.Width is <= 0)) Add(NotCarriedKind.HiddenColumns);
            if (read.FrozenPanes) Add(NotCarriedKind.FrozenPanes);
            foreach (var element in read.OtherElements)
            {
                foreach (var (kind, detail) in ExSheet.Xlsx.SheetElements.NotCarried(element)) Add(kind, detail);
            }
            if (printNames) Add(NotCarriedKind.PrintSettings);
            if (richCells > 0) Add(NotCarriedKind.RichText, firstRich);
            foreach (var (kind, detail) in formats) Add(kind, detail);
            foreach (var (kind, detail) in kinds)
            {
                notCarried.Add(new NotCarried(kind, sheet, detail, Describe(kind, sheet, detail, richCells)));
            }
        }

        private static string Describe(NotCarriedKind kind, string sheet, string? detail, int richCells) => kind switch
        {
            NotCarriedKind.RowHeights => $"Rows of '{sheet}' have heights of their own, which are not carried.",
            NotCarriedKind.HiddenRows => $"Rows of '{sheet}' are hidden in the file, and open shown.",
            NotCarriedKind.HiddenColumns => $"Columns of '{sheet}' are hidden in the file, and open shown.",
            NotCarriedKind.FrozenPanes => $"'{sheet}' has frozen panes, which are not carried.",
            NotCarriedKind.MergedCells => $"'{sheet}' has merged cells; each cell is carried on its own.",
            NotCarriedKind.ConditionalFormatting => $"'{sheet}' has conditional formatting, which is not carried.",
            NotCarriedKind.DataValidation => $"'{sheet}' has data validation, which is not carried.",
            NotCarriedKind.NotesAndComments => $"'{sheet}' has notes or comments, which are not carried.",
            NotCarriedKind.Hyperlinks => $"'{sheet}' has hyperlinks; their cells' text is carried, the links are not.",
            NotCarriedKind.PicturesAndCharts => $"'{sheet}' has pictures, charts, shapes or controls, which are not carried.",
            NotCarriedKind.Protection => $"'{sheet}' is protected in the file; the protection is not carried.",
            NotCarriedKind.PrintSettings => $"'{sheet}' has print settings beyond Excel's defaults (margins, a header or footer, page breaks, a print area, a scale or the like), which are not carried.",
            NotCarriedKind.View => $"'{sheet}' has view settings (hidden gridlines, headings or zeros, Formulas shown, right to left, a zoom or a page layout), which are not carried.",
            NotCarriedKind.AutoFilter => $"'{sheet}' has an AutoFilter, which is not carried; every row opens shown.",
            NotCarriedKind.ExcelTable => $"'{sheet}' has Excel Tables; their cells are carried, the Tables are not.",
            NotCarriedKind.Slicers => $"'{sheet}' has slicers or timelines, which are not carried.",
            NotCarriedKind.SheetProperties => $"The tab colour of '{sheet}' is not carried.",
            NotCarriedKind.RichText => string.Create(CultureInfo.InvariantCulture,
                $"{richCells} cell(s) of '{sheet}', the first {detail}, hold rich text: the text is carried, the runs' own formatting is not."),
            NotCarriedKind.DefaultColumnWidth => $"'{sheet}' has a default column width of its own, which is not carried; its columns without a width of their own open at the default width.",
            NotCarriedKind.FontNamesAndSizes => $"Cells of '{sheet}', the first at {detail}, use a typeface or a size other than the workbook's own, which is not carried; they open in the Sheet's font.",
            NotCarriedKind.VerticalAlignment => $"Cells of '{sheet}', the first at {detail}, are aligned vertically, which is not carried.",
            NotCarriedKind.TextLayout => $"Cells of '{sheet}', the first at {detail}, wrap, indent, rotate or shrink their text, which is not carried.",
            NotCarriedKind.HorizontalAlignment => $"Cells of '{sheet}', the first at {detail}, are aligned to fill, justify, distribute or centre across the selection, which is not carried; they open aligned as General.",
            NotCarriedKind.Underline => $"Cells of '{sheet}', the first at {detail}, are underlined twice or for accounting; they open with a single underline.",
            NotCarriedKind.PatternFills => $"Cells of '{sheet}', the first at {detail}, have a pattern or gradient fill, or a solid fill of no colour, which is not carried; they open with no Fill.",
            NotCarriedKind.DiagonalBorders => $"Cells of '{sheet}', the first at {detail}, have diagonal borders, which are not carried; their four sides are.",
            _ => $"'{sheet}' holds <{detail}>, which is not carried.",
        };
    }
}
