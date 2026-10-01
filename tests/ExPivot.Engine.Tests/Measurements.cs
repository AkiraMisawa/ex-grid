using System.Diagnostics;
using System.Globalization;
using ExGrid.Data;
using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;
using static ExPivot.Engine.Tests.Sources;

namespace ExPivot.Engine.Tests;

/// <summary>
/// What the engine costs over a million records (PV-21, ADR-0063/0065/0066), measured and never
/// gated: a measurement swings with the machine (AGENTS.md). These run only when asked for, in a
/// Release build:
/// <code>
/// dotnet build tests/ExPivot.Engine.Tests -c Release
/// dotnet tests/ExPivot.Engine.Tests/bin/Release/net10.0/ExPivot.Engine.Tests.dll -explicit only -showLiveOutput
/// </code>
/// The records are the demo's trades, shaped as the grilling's prototype measured them (ADR-0063's
/// table): three regions, four desks, 62 books and four products make 2,976 leaves under the main
/// layout, three row fields, one column field and two money Sums.
/// </summary>
public class Measurements
{
    internal sealed record Trade(
        long Id, string Region, string Desk, string Book, string Product, string Currency, DateOnly TradeDate,
        decimal Notional, decimal Pnl, int Quantity, bool Confirmed, double Price);

    private const int Million = 1_000_000;

    private static readonly Lazy<Trade[]> Trades = new(() => Make(Million));

    internal static Trade[] Make(int count, int seed = 20261001)
    {
        string[] regions = ["Americas", "EMEA", "APAC"];
        string[] desks = ["Rates", "Credit", "FX", "Equities"];
        string[] products = ["Swap", "Bond", "Option", "Future"];
        string[] currencies = ["USD", "EUR", "JPY", "GBP", "CHF"];
        var books = Enumerable.Range(0, 62).Select(b => "BK-" + b.ToString("00", CultureInfo.InvariantCulture)).ToArray();
        var random = new Random(seed);
        var start = new DateOnly(2026, 1, 2);
        var trades = new Trade[count];
        for (var i = 0; i < count; i++)
        {
            var notional = random.Next(1, 500) * 10_000m;
            trades[i] = new Trade(
                i,
                regions[random.Next(regions.Length)],
                desks[random.Next(desks.Length)],
                books[random.Next(books.Length)],
                products[random.Next(products.Length)],
                currencies[random.Next(currencies.Length)],
                start.AddDays(random.Next(270)),
                notional,
                Math.Round((decimal)(random.NextDouble() - 0.45) * notional / 100m, 2),
                random.Next(1, 50),
                random.Next(10) > 0,
                Math.Round(90 + (random.NextDouble() * 20), 4));
        }
        return trades;
    }

    internal static PivotFields<Trade> Fields(bool keyed = true)
    {
        var fields = PivotFields.Of<Trade>();
        if (keyed)
            fields.Key("Id", t => t.Id);
        return fields
            .Text("Region", t => t.Region)
            .Text("Desk", t => t.Desk)
            .Text("Book", t => t.Book)
            .Text("Product", t => t.Product)
            .Text("Currency", t => t.Currency)
            .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
            .Month("Month", of: "TradeDate")
            .Number("Notional", t => t.Notional, format: "#,##0.00")
            .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")
            .Number("Quantity", t => t.Quantity)
            .Number("Price", t => t.Price)
            .Boolean("Confirmed", t => t.Confirmed);
    }

    // The slices never yield, so a question is timed whole, on this thread.
    private static readonly PivotSlicing Whole = new() { Budget = TimeSpan.FromDays(1) };

    private static PivotQuery MainLayout(params string[] columns) => new(
        rows: [F("Region"), F("Desk"), F("Book")],
        columns: [.. columns.Select(c => F(c))],
        values: [V("Notional", PivotParts.Sum), V("Pnl", PivotParts.Sum)],
        maxLeaves: int.MaxValue);

    private static void Report(string what, IReadOnlyList<double> milliseconds, string? note = null)
    {
        var sorted = milliseconds.Order().ToArray();
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{what,-62} median {sorted[sorted.Length / 2],8:F1} ms   min {sorted[0],8:F1} ms   (n={sorted.Length}){(note is null ? "" : "   " + note)}   GCs:{Collections}");
        Collections = "";
        TestContext.Current.TestOutputHelper?.WriteLine(line);
    }

    // A run starts from a collected heap, so that the garbage of the run before is not charged to
    // it; a collection the run itself causes is charged, and counted.
    private static double Time(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen2 = GC.CollectionCount(2);
        var gen0 = GC.CollectionCount(0);
        var watch = Stopwatch.StartNew();
        action();
        var elapsed = watch.Elapsed.TotalMilliseconds;
        Collections += $" g0+{GC.CollectionCount(0) - gen0}/g2+{GC.CollectionCount(2) - gen2}";
        return elapsed;
    }

    private static string Collections = "";

    private static PivotAnswer Ask(PivotSource source, PivotQuery query)
    {
        var answer = source.AggregateAsync(query, Ct);
        Assert.True(answer.IsCompletedSuccessfully);
        Assert.False(answer.Result.IsRefused);
        return answer.Result;
    }

    [Fact(Explicit = true)] // PV-21 / ADR-0063: building the Snapshot of a million records from typed declarations
    public void Building_the_snapshot_from_typed_declarations()
    {
        var trades = Trades.Value;
        foreach (var keyed in new[] { false, true })
        {
            var fields = Fields(keyed);
            fields.Build(trades); // warm
            var times = Enumerable.Range(0, 5).Select(_ => Time(() => fields.Build(trades))).ToArray();
            Report($"Build, 1,000,000 records, 13 columns{(keyed ? ", Record Key" : "")}", times);
        }
    }

    [Fact(Explicit = true)] // PV-21 / ADR-0063/0065: a question over a million records, by layout
    public void Asking_a_million_records()
    {
        var trades = Trades.Value;
        foreach (var keyed in new[] { false, true })
        {
            var fields = Fields(keyed);
            var snapshot = fields.Build(trades);
            foreach (var (name, query) in new[]
                     {
                         ("main: 3 row fields, Product, two Sums", MainLayout("Product")),
                         ("main with Month (a date part) in Columns", MainLayout("Product", "Month")),
                         ("270 dates: TradeDate in Columns", MainLayout("TradeDate")),
                     })
            {
                // Warm: the runtime's tiers have compiled the pass's loops fully before it is timed.
                var leaves = 0;
                for (var warm = 0; warm < 5; warm++)
                    leaves = Ask(PivotSource.From(snapshot, fields.Fields, Whole), query).LeafCount;
                var times = Enumerable.Range(0, 7).Select(_ => Time(() => Ask(PivotSource.From(snapshot, fields.Fields, Whole), query))).ToArray();
                Report($"{name}{(keyed ? " (keyed: rows kept)" : "")}", times, $"{leaves:N0} leaves");
            }
        }

        // Every slice of the money columns held as decimals: a value that no 64-bit integer holds
        // at its slice's scale, once a slice.
        var decimals = Trades.Value.Select((t, i) => i % 65_536 == 0 ? t with { Notional = 0.00000000000000000001m, Pnl = 0.00000000000000000001m } : t).ToArray();
        var held = Fields(keyed: false);
        var heldAsDecimals = held.Build(decimals);
        for (var warm = 0; warm < 5; warm++)
            Ask(PivotSource.From(heldAsDecimals, held.Fields, Whole), MainLayout("Product"));
        Report("main, money held as decimal (no 64-bit scale)", [.. Enumerable.Range(0, 7).Select(_ => Time(() => Ask(PivotSource.From(heldAsDecimals, held.Fields, Whole), MainLayout("Product"))))]);

        // The records' own accessors: read into a Snapshot on the first question, boxing each value.
        var untyped = new PivotField<Trade>[]
        {
            new("Region", PivotFieldType.Text, t => t.Region), new("Desk", PivotFieldType.Text, t => t.Desk),
            new("Book", PivotFieldType.Text, t => t.Book), new("Product", PivotFieldType.Text, t => t.Product),
            new("Notional", PivotFieldType.Number, t => t.Notional), new("Pnl", PivotFieldType.Number, t => t.Pnl),
        };
        var first = Enumerable.Range(0, 3).Select(_ => Time(() => Ask(PivotSource.From(trades, untyped, Whole), MainLayout("Product")))).ToArray();
        Report("main, first question over untyped accessors (builds too)", first);
    }

    [Fact(Explicit = true)] // PV-21 / ADR-0059/0065: a collapse, a sort and a change of form laid out from the answer held, asking nothing
    public void Laying_out_the_answer_held()
    {
        var fields = Fields();
        var snapshot = fields.Build(Trades.Value);
        foreach (var (name, layout) in new[]
                 {
                     ("main: 3 row fields, Product, two Sums", MainReport("Product")),
                     ("270 dates: TradeDate in Columns", MainReport("TradeDate")),
                 })
        {
            var query = PivotQuery.For(layout, int.MaxValue);
            var answer = Ask(PivotSource.From(snapshot, fields.Fields, Whole), query);
            var cube = PivotEngine.Cube(query, answer, fields.Fields);
            var (region, inner) = (layout.Rows[0], layout.Rows.Skip(1).ToArray());
            foreach (var (gesture, next) in new[]
                     {
                         ("as asked", layout),
                         ("Americas collapsed", layout with { Rows = [region with { ToggledItems = [PivotItemKey.Text("Americas")] }, .. inner] }),
                         ("Region sorted Z to A", layout with { Rows = [region with { Sort = PivotSort.Descending }, .. inner] }),
                         ("Book sorted by Sum of Notional, largest first", layout with { Rows = [region, inner[0], inner[1] with { Sort = new(PivotSortDirection.Descending, ByValue: 0) }] }),
                         ("Tabular form", layout with { Form = PivotReportForm.Tabular }),
                     })
            {
                // Warm, as a question is: the runtime's tiers have compiled the layout's loops.
                var report = PivotEngine.Report(cube, next);
                for (var warm = 0; warm < 4; warm++)
                    report = PivotEngine.Report(cube, next);
                var times = Enumerable.Range(0, 9).Select(_ => Time(() => report = PivotEngine.Report(cube, next))).ToArray();
                Report($"{name}: {gesture}", times,
                    $"{answer.LeafCount:N0} leaves, {report.Rows.Count:N0} rows x {report.ValueColumns.Count:N0} value columns");
            }
        }
    }

    private static PivotLayout MainReport(string column) => new()
    {
        Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk"), new PivotFieldPlacement("Book")],
        Columns = [new PivotFieldPlacement(column)],
        Values = [new PivotValueField("Notional"), new PivotValueField("Pnl")],
    };

    [Fact(Explicit = true)] // PV-21 / ADR-0066: 1,000 changes to a million records, folded into the held answer
    public void Folding_a_thousand_changes_into_a_million_records()
    {
        var trades = Trades.Value;
        var fields = Fields();
        var snapshot = fields.Build(trades);
        foreach (var (name, query) in new[]
                 {
                     ("exact Sums (subtracted)", MainLayout("Product")),
                     ("a double's Sum (leaves recomputed)", new PivotQuery(rows: [F("Region"), F("Desk"), F("Book")], columns: [F("Product")], values: [V("Price", PivotParts.Sum)], maxLeaves: int.MaxValue)),
                     ("Max of Notional (leaves recomputed)", new PivotQuery(rows: [F("Region"), F("Desk"), F("Book")], columns: [F("Product")], values: [V("Notional", PivotParts.Extremes)], maxLeaves: int.MaxValue)),
                     ("270 dates, exact Sums", MainLayout("TradeDate")),
                 })
        {
            var source = PivotSource.From(snapshot, fields.Fields, Whole);
            Ask(source, query);
            var random = new Random(7);
            var removed = new bool[Million];
            var nextId = (long)Million;
            var batches = new List<double>();
            var applies = new List<double>();
            var answers = new List<double>();
            var compactions = 0;
            for (var b = 0; b < 30; b++)
            {
                // 1,000 changes: 800 records changed, 100 added and 100 removed.
                var ids = new HashSet<int>();
                while (ids.Count < 900)
                {
                    var id = random.Next(Million);
                    if (!removed[id])
                        ids.Add(id);
                }
                var chosen = ids.ToArray();
                var changed = chosen[..800].Select(id => trades[id] with { Notional = random.Next(1, 500) * 10_000m, Pnl = random.Next(-100_000, 100_000) / 100m, Price = 90 + (random.NextDouble() * 20) }).ToArray();
                var added = Make(100, seed: b).Select(t => t with { Id = nextId++ }).ToArray();
                foreach (var id in chosen[800..])
                    removed[id] = true;
                ChangeBatch? batch = null;
                batches.Add(Time(() => batch = fields.Batch(added: added, changed: changed, removedKeys: chosen[800..].Select(id => (object)(long)id))));
                SnapshotChange? change = null;
                applies.Add(Time(() => change = source.Apply(batch!)));
                answers.Add(Time(() => Ask(source, query)));
                if (change!.Compacted)
                    compactions++;
            }
            Report($"1,000 changes, {name}: build the batch", batches);
            Report($"1,000 changes, {name}: apply and fold", applies, $"{compactions} compacted");
            Report($"1,000 changes, {name}: the next answer", answers);
        }
    }
}
