using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using ExGrid.Data.Xlsx;
using ExPivot.Engine;
using ExSheet.Engine;
using ExSheet.Xlsx;

// The Worker starts the runtime and calls the exports below; Main has nothing of its own to do.
return;

namespace EngineHost
{
    /// <summary>
    /// What the Worker asks of the engines (ADR-0370 section 2), one call per message. Every answer is JSON
    /// that names the version of the document or the report it was taken on, so the page can refuse one
    /// that arrives for an older version; and the ms the engine spent on it, so the page can tell the
    /// engine's time from the crossing's and its own.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("browser")]
    public static partial class Exports
    {
        // --- ExSheet -------------------------------------------------------------------------------

        private static Workbook? _workbook;
        private static int _sheetVersion;

        /// <summary>Opens a Sheet Document (its JSON form) in place of any Sheet held.</summary>
        [JSExport]
        public static string SheetOpen(string documentJson)
        {
            var clock = Stopwatch.StartNew();
            _workbook = Workbook.Open(ExSheet.Engine.SheetDocument.FromJson(documentJson));
            _sheetVersion = 0;
            return Answer(w =>
            {
                w.WriteNumber("version", _sheetVersion);
                w.WriteNumber("rowCount", Sheet.RowCount);
                w.WriteNumber("columnCount", Sheet.ColumnCount);
                w.WriteNumber("engineMs", clock.Elapsed.TotalMilliseconds);
            });
        }

        /// <summary>
        /// Enters <paramref name="typed"/> into <paramref name="address"/> as a user typed it, and answers the
        /// new version with the Window the page shows (<paramref name="start"/>, <paramref name="count"/> rows of
        /// <paramref name="columns"/> columns) and the rows the change named outside it, which the page holds
        /// as stale until it asks for them again.
        /// </summary>
        [JSExport]
        public static string SheetEnter(string address, string typed, int start, int count, int columns)
        {
            var workbook = _workbook ?? throw new InvalidOperationException("No Sheet is open.");
            var clock = Stopwatch.StartNew();
            var step = workbook.Do(workbook.Sheets[0].Name, SheetEdit.Enter(CellAddress.Parse(address), typed));
            var engineMs = clock.Elapsed.TotalMilliseconds;
            _sheetVersion++;
            var rows = step.Change.For(workbook.Sheets[0]).Rows;
            return Answer(w =>
            {
                w.WriteNumber("version", _sheetVersion);
                w.WriteNumber("engineMs", engineMs);
                w.WriteStartArray("changedRows");
                foreach (var (first, last) in Runs(rows))
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(first);
                    w.WriteNumberValue(last);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                WriteWindow(w, workbook.Sheets[0], start, count, columns);
            });
        }

        /// <summary>The Window of display texts at the current version.</summary>
        [JSExport]
        public static string SheetWindow(int start, int count, int columns)
        {
            var workbook = _workbook ?? throw new InvalidOperationException("No Sheet is open.");
            return Answer(w =>
            {
                w.WriteNumber("version", _sheetVersion);
                WriteWindow(w, workbook.Sheets[0], start, count, columns);
            });
        }

        private static void WriteWindow(Utf8JsonWriter w, Sheet sheet, int start, int count, int columns)
        {
            var clock = Stopwatch.StartNew();
            var end = Math.Min(Sheet.RowCount, start + count);
            w.WriteNumber("start", start);
            w.WriteStartArray("rows");
            for (var row = start; row < end; row++)
            {
                w.WriteStartArray();
                for (var column = 0; column < columns; column++)
                {
                    var address = new CellAddress(row, column);
                    if (sheet.GetEntry(address) is null) w.WriteNullValue();
                    else w.WriteStringValue(sheet.GetDisplay(address).Text);
                }
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteNumber("windowMs", clock.Elapsed.TotalMilliseconds);
        }

        private static IEnumerable<(int First, int Last)> Runs(IReadOnlyList<int> rows)
        {
            var sorted = rows.Order().ToArray();
            for (var i = 0; i < sorted.Length;)
            {
                var first = sorted[i];
                var last = first;
                while (++i < sorted.Length && sorted[i] <= last + 1) last = sorted[i];
                yield return (first, last);
            }
        }

        // --- ExPivot -------------------------------------------------------------------------------

        private static Probe.Trade[]? _trades;
        private static SnapshotPivotSource? _pivotSource;
        private static LocalPivotReportSource? _reports;
        private static readonly PivotReportSettings PivotSettings = new() { CultureName = "en-US" };
        private static int _requests;

        /// <summary>
        /// Takes the trades as columns (ids, books, products, currencies, trade dates as yyyy-MM-dd, P&amp;Ls)
        /// and puts a Pivot Source and a report source over them, as CorePivot does over its own.
        /// </summary>
        [JSExport]
        public static string PivotOpen(string tradesJson)
        {
            var clock = Stopwatch.StartNew();
            using var parsed = JsonDocument.Parse(tradesJson);
            var root = parsed.RootElement;
            var ids = root.GetProperty("id");
            var books = root.GetProperty("book");
            var products = root.GetProperty("product");
            var currencies = root.GetProperty("currency");
            var dates = root.GetProperty("tradeDate");
            var pnls = root.GetProperty("pnl");
            var trades = new Probe.Trade[ids.GetArrayLength()];
            for (var i = 0; i < trades.Length; i++)
            {
                trades[i] = new Probe.Trade(
                    ids[i].GetString()!, books[i].GetString()!, products[i].GetString()!, currencies[i].GetString()!,
                    DateTime.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    pnls[i].GetDecimal());
            }
            _trades = trades;
            _pivotSource = PivotSource.From(trades, Probe.Fields);
            _reports = PivotReportSource.From(_pivotSource);
            return Answer(w =>
            {
                w.WriteNumber("trades", trades.Length);
                w.WriteNumber("engineMs", clock.Elapsed.TotalMilliseconds);
            });
        }

        /// <summary>The report Window, rows <paramref name="start"/> on, laid out across <paramref name="across"/>, as <see cref="PivotReportJson"/> writes it.</summary>
        [JSExport]
        public static async Task<string> PivotWindow(string across, int start, int count, bool markChanges)
        {
            var reports = _reports ?? throw new InvalidOperationException("No trades are open.");
            var clock = Stopwatch.StartNew();
            var update = await reports.WindowAsync(new PivotReportRequest(
                "r" + (++_requests).ToString(CultureInfo.InvariantCulture), Probe.Layout(across), PivotSettings, new PivotReportWindow(start, count))
            { MarkChanges = markChanges });
            return Wrapped(update, clock.Elapsed.TotalMilliseconds);
        }

        /// <summary>Adds <paramref name="delta"/> to trade <paramref name="index"/>'s P&amp;L in a Change Batch, and answers the Window after it.</summary>
        [JSExport]
        public static async Task<string> PivotAmend(int index, double delta, string across, int start, int count)
        {
            var trades = _trades ?? throw new InvalidOperationException("No trades are open.");
            var clock = Stopwatch.StartNew();
            var amended = trades[index] = trades[index] with { Pnl = trades[index].Pnl + (decimal)delta };
            _pivotSource!.Apply(Probe.Fields.Batch(changed: [amended]));
            var update = await _reports!.WindowAsync(new PivotReportRequest(
                "r" + (++_requests).ToString(CultureInfo.InvariantCulture), Probe.Layout(across), PivotSettings, new PivotReportWindow(start, count)));
            return Wrapped(update, clock.Elapsed.TotalMilliseconds);
        }

        // The update as PivotReportJson writes it, inside an object that carries the engine's ms beside it.
        private static string Wrapped(PivotReportUpdate update, double engineMs)
        {
            var clock = Stopwatch.StartNew();
            var json = PivotReportJson.Write(update);
            var jsonMs = clock.Elapsed.TotalMilliseconds;
            return string.Create(CultureInfo.InvariantCulture, $"{{\"engineMs\":{engineMs},\"jsonMs\":{jsonMs},\"update\":{json}}}");
        }

        // --- The readers the Worker carries ----------------------------------------------------------

        /// <summary>Opens an .xlsx as a Workbook (ExSheet.Xlsx) and answers how many Sheets it holds: the reader is in the build, and its bytes in the download.</summary>
        [JSExport]
        public static async Task<string> XlsxSheets(byte[] file)
        {
            using var stream = new MemoryStream(file);
            var opened = await XlsxWorkbookReader.ReadAsync(stream, CultureInfo.GetCultureInfo("en-US"));
            return Answer(w => w.WriteNumber("sheets", opened.Document.Sheets.Count));
        }

        /// <summary>Reads an .xlsx's first sheet with ExGrid.Data.Xlsx and answers its rows.</summary>
        [JSExport]
        public static async Task<string> XlsxRows(byte[] file)
        {
            using var stream = new MemoryStream(file);
            using var reader = await XlsxReader.OpenAsync(stream);
            var sheet = await reader.ReadSheetAsync(0);
            return Answer(w => w.WriteNumber("cells", sheet.Cells.Count));
        }

        /// <summary>Saves the open Sheet as an .xlsx (ExSheet.Xlsx's writer) and answers its size.</summary>
        [JSExport]
        public static async Task<string> SheetSaveXlsx()
        {
            var workbook = _workbook ?? throw new InvalidOperationException("No Sheet is open.");
            using var stream = new MemoryStream();
            await XlsxWorkbookWriter.WriteAsync(workbook, stream);
            return Answer(w => w.WriteNumber("bytes", stream.Length));
        }

        // --- Where the time goes ----------------------------------------------------------------------

        /// <summary>The engine steps of /core/sheet and /core/pivot timed alone on this runtime (Probe.cs).</summary>
        [JSExport]
        public static Task<string> ProbeRun(string runtime, int repeats) => Probe.Run(runtime, repeats);

        private static string Answer(Action<Utf8JsonWriter> write)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                write(writer);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
