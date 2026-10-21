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
/// Cell formats and column widths are not carried yet; every cell opens in General (ticket 03 of
/// <c>docs/specs/exsheet-xlsx</c>).
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

        public async ValueTask<XlsxOpenResult> ReadAsync()
        {
            var worksheets = new List<int>();
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
                worksheets.Add(i);
            }
            if (worksheets.Count == 0)
                throw new XlsxOpenException(XlsxOpenRefusal.Sheets, "The workbook holds no worksheet, so it holds no cells to open.");

            WorkbookParts();
            var sheets = new List<SheetDocument>(worksheets.Count);
            foreach (var index in worksheets)
            {
                var info = reader.Sheets[index];
                if (info.Visibility != XlsxSheetVisibility.Visible)
                    notCarried.Add(new(NotCarriedKind.HiddenSheet, info.Name, null, $"The Sheet '{info.Name}' is hidden in the file, and opens shown."));
                sheets.Add(await ReadSheetAsync(index).ConfigureAwait(false));
            }
            foreach (var name in reader.DefinedNames)
            {
                notCarried.Add(new(NotCarriedKind.DefinedName, null, name.Name,
                    $"The defined name '{name.Name}' ({name.Text}) is not carried; no Formula carried reads it."));
            }
            var shownIndex = worksheets.Contains(reader.ActiveSheet) ? reader.ActiveSheet : worksheets[0];
            var document = new WorkbookDocument(culture, reader.Sheets[shownIndex].Name, sheets);
            return new XlsxOpenResult(document, new OpenReport(notCarried, unsupported));
        }

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

        private async ValueTask<SheetDocument> ReadSheetAsync(int index)
        {
            using var sheet = reader.OpenSheet(index);
            var name = sheet.Sheet.Name;
            var cells = new List<SheetDocumentCell>();
            var shared = new Dictionary<int, SharedMaster>();
            var spills = new List<(int Top, int Left, int Bottom, int Right)>();
            var richCells = 0;
            string? firstRich = null;
            while (await sheet.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var cell in sheet.Cells)
                {
                    if (cell.IsRichText)
                    {
                        richCells++;
                        firstRich ??= cell.Reference;
                    }
                    var entry = cell.Formula is { } formula
                        ? FormulaEntry(name, cell, formula, shared, spills)
                        : InsideSpill(spills, cell.Row, cell.Column) ? null : Constant(name, cell);
                    if (entry is not null) cells.Add(new SheetDocumentCell(new CellAddress(cell.Row, cell.Column), entry, null, null, null, null, null));
                }
            }
            SheetElements(name, sheet, richCells, firstRich);
            return new SheetDocument(culture, name, cells);
        }

        /// <summary>A shared formula's master: its Entry where it stands, and what it names.</summary>
        private sealed record SharedMaster(Entry Entry, int Row, int Column, IReadOnlyList<string> Unsupported);

        private Entry FormulaEntry(string sheet, XlsxCell cell, XlsxFormula formula, Dictionary<int, SharedMaster> shared, List<(int, int, int, int)> spills)
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
                    if (Entry.Parse(cell.Value, CultureInfo.InvariantCulture) is { Constant: { IsError: true } } error) return error;
                    throw new XlsxOpenException(XlsxOpenRefusal.UnreadableConstant, "The cell holds an Error Value a cell of ExSheet cannot hold.", sheet, at, cell.Value);
                case XlsxCellType.Date:
                    if (cell.Value is null) return null;
                    if (!DateTime.TryParse(cell.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                        || Serial(date) is not { } day)
                    {
                        throw new XlsxOpenException(XlsxOpenRefusal.UnreadableConstant, "The cell's date cannot be read, or lies outside the dates a serial counts.", sheet, at, cell.Value);
                    }
                    return Entry.FromValue(Value.FromNumber(day + date.TimeOfDay.TotalDays));
                default:
                    return null;
            }
        }

        /// <summary>
        /// The 1900 date system's serial of a day, as Excel counts it: 1 is 1 January 1900, and 60 the 29 February 1900 Excel
        /// keeps for Lotus's sake, so every day from 1 March 1900 counts from 30 December 1899. Null before 1900.
        /// </summary>
        private static int? Serial(DateTime date)
        {
            if (date.Year < 1900) return null;
            var day = date.Date;
            return day < new DateTime(1900, 3, 1) ? (day - new DateTime(1899, 12, 31)).Days : (day - new DateTime(1899, 12, 30)).Days;
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

        private static bool InsideSpill(List<(int Top, int Left, int Bottom, int Right)> spills, int row, int column)
        {
            foreach (var (top, left, bottom, right) in spills)
            {
                if (row >= top && row <= bottom && column >= left && column <= right) return true;
            }
            return false;
        }

        /// <summary>A range written <c>A1:B3</c> or <c>A1</c>, or null when it is not one.</summary>
        private static (int Top, int Left, int Bottom, int Right)? Range(string text)
        {
            var colon = text.IndexOf(':', StringComparison.Ordinal);
            var first = colon < 0 ? text : text[..colon];
            var last = colon < 0 ? text : text[(colon + 1)..];
            if (!XlsxCellReference.TryParse(first, out var r1, out var c1) || !XlsxCellReference.TryParse(last, out var r2, out var c2)) return null;
            return (Math.Min(r1, r2), Math.Min(c1, c2), Math.Max(r1, r2), Math.Max(c1, c2));
        }

        /// <summary>What else a worksheet held, as the file layer reports it, said once per Sheet and kind.</summary>
        private void SheetElements(string sheet, XlsxSheetReader read, int richCells, string? firstRich)
        {
            var kinds = new List<(NotCarriedKind Kind, string? Detail)>();
            void Add(NotCarriedKind kind, string? detail = null)
            {
                if (!kinds.Any(k => k.Kind == kind && k.Detail == detail)) kinds.Add((kind, detail));
            }
            if (read.CustomRowHeights) Add(NotCarriedKind.RowHeights);
            if (read.HiddenRows) Add(NotCarriedKind.HiddenRows);
            if (read.Columns.Any(c => c.Hidden)) Add(NotCarriedKind.HiddenColumns);
            if (read.FrozenPanes) Add(NotCarriedKind.FrozenPanes);
            foreach (var element in read.OtherElements)
            {
                switch (element)
                {
                    case "mergeCells": Add(NotCarriedKind.MergedCells); break;
                    case "conditionalFormatting": Add(NotCarriedKind.ConditionalFormatting); break;
                    case "dataValidations": Add(NotCarriedKind.DataValidation); break;
                    case "legacyDrawing": Add(NotCarriedKind.NotesAndComments); break;
                    case "hyperlinks": Add(NotCarriedKind.Hyperlinks); break;
                    case "drawing" or "picture" or "oleObjects" or "controls" or "legacyDrawingHF": Add(NotCarriedKind.PicturesAndCharts); break;
                    case "sheetProtection" or "protectedRanges": Add(NotCarriedKind.Protection); break;
                    case "printOptions" or "pageMargins" or "pageSetup" or "headerFooter" or "rowBreaks" or "colBreaks": Add(NotCarriedKind.PrintSettings); break;
                    case "autoFilter" or "sortState": Add(NotCarriedKind.AutoFilter); break;
                    case "tableParts": Add(NotCarriedKind.ExcelTable); break;
                    case "sheetPr": Add(NotCarriedKind.SheetProperties); break;
                    default: Add(NotCarriedKind.Other, element); break;
                }
            }
            if (richCells > 0) Add(NotCarriedKind.RichText, firstRich);
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
            NotCarriedKind.PrintSettings => $"'{sheet}' has print settings, which are not carried.",
            NotCarriedKind.AutoFilter => $"'{sheet}' has an AutoFilter, which is not carried; every row opens shown.",
            NotCarriedKind.ExcelTable => $"'{sheet}' has Excel Tables; their cells are carried, the Tables are not.",
            NotCarriedKind.SheetProperties => $"The properties of '{sheet}' (its tab colour, outline settings and the like) are not carried.",
            NotCarriedKind.RichText => string.Create(CultureInfo.InvariantCulture,
                $"{richCells} cell(s) of '{sheet}', the first {detail}, hold rich text: the text is carried, the runs' own formatting is not."),
            _ => $"'{sheet}' holds <{detail}>, which is not carried.",
        };
    }
}
