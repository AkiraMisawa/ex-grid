using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ExPivot.Engine;
using ExSheet.Engine;

namespace EngineHost;

/// <summary>
/// Where an edit's time goes, engine step by engine step, on whatever runtime runs this: the .NET JIT
/// (EngineProbe, a console), or the WebAssembly interpreter or AOT (EngineHost, in the Worker). The data
/// is render-bench's /core/sheet and /core/pivot (Bench.Live/CoreSheet.razor, CorePivot.razor): the same
/// Sheet, and trades drawn from the same distribution. Each step is the engine call Blazor's ExSheet or
/// ExPivot makes for that gesture, timed alone, so the steps can be read against the 3.3 s the page takes.
/// </summary>
public static class Probe
{
    /// <summary>The rows that read A1, as CoreSheet.</summary>
    public const int SheetRows = 2_000;

    /// <summary>The trades, as CorePivot.</summary>
    public const int Trades = 20_000;

    /// <summary>The Sheet's culture, as CoreSheet's.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>CoreSheet's document: A1 a number, A2:A2001 read it, B to J each add one to the cell on their left, L1 read by nothing.</summary>
    public static SheetDocument SheetDocument()
    {
        var cells = new List<SheetDocumentCell>(SheetRows * 10 + 2);
        void Add(string address, string typed)
            => cells.Add(new SheetDocumentCell(CellAddress.Parse(address), Entry.Parse(typed, Culture), null, null, null, null, null));
        Add("A1", "1");
        Add("L1", "0");
        for (var row = 2; row <= SheetRows + 1; row++)
        {
            var r = row.ToString(CultureInfo.InvariantCulture);
            Add("A" + r, "=$A$1+ROW()");
            for (var c = 'B'; c <= 'J'; c++)
                Add(c + r, $"={(char)(c - 1)}{r}+1");
        }
        return new SheetDocument(Culture, "Sheet1", cells);
    }

    /// <summary>A trade, as CorePivot's.</summary>
    public sealed record Trade(string Id, string Book, string Product, string Currency, DateTime TradeDate, decimal Pnl);

    /// <summary>CorePivot's fields.</summary>
    public static readonly PivotFields<Trade> Fields = PivotFields.Of<Trade>()
        .Key("Id", t => t.Id)
        .Text("Book", t => t.Book)
        .Text("Product", t => t.Product)
        .Text("Currency", t => t.Currency)
        .Date("TradeDate", t => t.TradeDate, caption: "Trade date", format: "yyyy-MM-dd")
        .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0");

    /// <summary>CorePivot's layout: P&amp;L by book and trade date, across <paramref name="across"/>.</summary>
    public static PivotLayout Layout(string across) => new()
    {
        Rows = [new PivotFieldPlacement("Book"), new PivotFieldPlacement("TradeDate")],
        Columns = [new PivotFieldPlacement(across)],
        Values = [new PivotValueField("Pnl", PivotAggregation.Sum) { NumberFormat = "#,##0" }],
    };

    private static readonly string[] Books = ["BK-A", "BK-B", "BK-C", "BK-D", "BK-E", "BK-F", "BK-G", "BK-H", "BK-I", "BK-J"];
    private static readonly string[] Currencies = ["USD", "EUR", "JPY", "GBP", "CHF"];
    private static readonly string[] Products = ["Swap", "Bond", "Option", "Future", "Spot"];

    /// <summary>CorePivot's trades, with its seed.</summary>
    public static Trade[] MakeTrades()
    {
        var random = new Random(20261025);
        var start = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
        var trades = new Trade[Trades];
        for (var i = 0; i < Trades; i++)
        {
            trades[i] = new Trade(
                "T" + i.ToString(CultureInfo.InvariantCulture),
                Books[random.Next(Books.Length)],
                Products[random.Next(Products.Length)],
                Currencies[random.Next(Currencies.Length)],
                start.AddDays(random.Next(0, 270)),
                random.Next(-50_000, 60_000));
        }
        return trades;
    }

    /// <summary>The cells the Blazor page paints of the Sheet: 22 rows of 17 columns at the top.</summary>
    private const int PaintedRows = 22;
    private const int PaintedColumns = 17;

    /// <summary>Runs every step <paramref name="repeats"/> times and answers each step's times in ms, as JSON.</summary>
    public static async Task<string> Run(string runtime, int repeats)
    {
        var steps = new Dictionary<string, List<double>>();
        void Record(string step, double ms)
        {
            if (!steps.TryGetValue(step, out var list)) steps[step] = list = [];
            list.Add(ms);
        }
        T Time<T>(string step, Func<T> act)
        {
            var clock = Stopwatch.StartNew();
            var result = act();
            Record(step, clock.Elapsed.TotalMilliseconds);
            return result;
        }
        async Task<T> TimeAsync<T>(string step, Func<ValueTask<T>> act)
        {
            var clock = Stopwatch.StartNew();
            var result = await act();
            Record(step, clock.Elapsed.TotalMilliseconds);
            return result;
        }

        var paintedChars = 0;
        for (var round = 0; round < repeats; round++)
        {
            // --- the Sheet ---------------------------------------------------------------------------
            var document = Time("sheet: build the document", SheetDocument);
            var json = Time("sheet: document to JSON", document.ToJson);
            var read = Time("sheet: document from JSON", () => ExSheet.Engine.SheetDocument.FromJson(json));
            var workbook = Time("sheet: open (first recalculation)", () => Workbook.Open(read));
            var sheet = workbook.Sheets[0];
            for (var edit = 0; edit < 3; edit++)
            {
                var typed = (1000 + 100 * round + edit).ToString(CultureInfo.InvariantCulture);
                var step = Time("sheet: edit A1 (20,000 recalculated)", () => workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("A1"), typed)));
                Record("sheet: edit A1, rows named", step.Change.For(sheet).Rows.Count);
                paintedChars += Time("sheet: read the painted cells", () => Painted(sheet));
                Time("sheet: document handed back (ToDocument)", () => workbook.ToDocument("Sheet1"));
                var other = (5000 + 100 * round + edit).ToString(CultureInfo.InvariantCulture);
                Time("sheet: edit L1 (nothing reads it)", () => workbook.Do("Sheet1", SheetEdit.Enter(CellAddress.Parse("L1"), other)));
                Time("sheet: document handed back after L1 (ToDocument)", () => workbook.ToDocument("Sheet1"));
            }

            // --- the pivot ---------------------------------------------------------------------------
            var trades = Time("pivot: make the trades", MakeTrades);
            var source = Time("pivot: source over the trades", () => PivotSource.From(trades, Fields));
            await using var reports = PivotReportSource.From(source);
            var settings = new PivotReportSettings { CultureName = "en-US" };
            var window = new PivotReportWindow(0, 80);
            var first = await TimeAsync("pivot: first report Window", () => reports.WindowAsync(new PivotReportRequest("first", Layout("Currency"), settings, window)));
            if (first.Refusal is { } refusal) throw new InvalidOperationException(refusal.Message);
            Record("pivot: report rows", first.Metadata!.RowCount);
            Time("pivot: report Window to JSON", () => PivotReportJson.Write(first));
            var at = Array.FindIndex(trades, t => t.Book == "BK-A");
            for (var amend = 0; amend < 3; amend++)
            {
                trades[at] = trades[at] with { Pnl = trades[at].Pnl + 1_000_003m };
                Time("pivot: Change Batch applied", () => source.Apply(Fields.Batch(changed: [trades[at]])));
                await TimeAsync("pivot: Window after one trade", () => reports.WindowAsync(new PivotReportRequest($"amend{amend}", Layout("Currency"), settings, window)));
            }
            for (var swap = 0; swap < 2; swap++)
            {
                var across = swap % 2 == 0 ? "Product" : "Currency";
                await TimeAsync("pivot: Window after the columns swapped", () => reports.WindowAsync(new PivotReportRequest($"swap{swap}", Layout(across), settings, window) { MarkChanges = false }));
            }
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("runtime", runtime);
            writer.WriteNumber("repeats", repeats);
            writer.WriteNumber("paintedChars", paintedChars);
            writer.WriteStartObject("steps");
            foreach (var (step, times) in steps)
            {
                writer.WriteStartArray(step);
                foreach (var ms in times) writer.WriteNumberValue(Math.Round(ms, 2));
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static int Painted(Sheet sheet)
    {
        var chars = 0;
        for (var row = 0; row < PaintedRows; row++)
            for (var column = 0; column < PaintedColumns; column++)
                chars += sheet.GetDisplay(new CellAddress(row, column)).Text.Length;
        return chars;
    }
}
