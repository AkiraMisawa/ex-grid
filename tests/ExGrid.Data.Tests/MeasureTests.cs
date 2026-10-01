using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ExGrid.Data;
using Xunit;

namespace ExGrid.Data.Tests;

/// <summary>
/// DA-17 on CoreCLR: a million records built from objects and read from a CSV (ADR-0063), measured
/// and never gated — a measurement swings with the machine (AGENTS.md). Read from Arrow is
/// <c>ExGrid.Data.Arrow.Tests</c>' <c>MeasureTests</c>. These run only when asked for, in a Release
/// build, and write what they measured to the test output:
/// <code>
/// dotnet build tests/ExGrid.Data.Tests -c Release
/// dotnet tests/ExGrid.Data.Tests/bin/Release/net10.0/ExGrid.Data.Tests.dll -explicit only -showLiveOutput
/// </code>
/// The records are the demo's trades, and the CSV is the trade export /pivot-csv reads
/// (<c>DemoCsv</c>): the same columns, the same writing and the same Schema, so the numbers stand
/// beside the browser's.
/// </summary>
public class MeasureTests
{
    /// <summary>A trade as the demo's trade export writes one: a unique id, an account with leading
    /// zeros, five more text columns, a date, two money columns, an integer and a flag.</summary>
    private sealed record Trade(
        string Id, string Account, string Region, string Desk, string Book, string Product, string Currency,
        DateOnly TradeDate, decimal Notional, decimal Pnl, int Quantity, bool Confirmed);

    private const int Million = 1_000_000;

    private static readonly Lazy<Trade[]> Trades = new(() => Make(Million));

    [Fact(Explicit = true)] // DA-17 / ADR-0063: a million records built from objects through typed accessors, at once and in slices
    public async Task DA17_a_million_records_built_from_objects()
    {
        var output = TestContext.Current.TestOutputHelper!;
        var token = TestContext.Current.CancellationToken;
        var trades = Trades.Value;
        output.WriteLine(Machine());

        foreach (var keyed in new[] { true, false })
        {
            var builder = Declared(keyed);
            var shape = keyed ? "12 columns, keyed by Id" : "12 columns, no Record Key";
            output.WriteLine($"{Show("Build, 1,000,000 records, " + shape, Time(() => builder.Build(trades)))}, allocating {Mib(Allocated(() => builder.Build(trades)))}");
            output.WriteLine(Show("BuildAsync (30 ms slices), 1,000,000 records, " + shape,
                Time(() => builder.BuildAsync(trades, cancellationToken: token).AsTask().GetAwaiter().GetResult())));
        }
        Assert.Equal(Million, (await Declared(keyed: true).BuildAsync(trades, cancellationToken: token)).RowCount);
    }

    [Fact(Explicit = true)] // DA-17 / ADR-0063: a million rows of the trade export read from a CSV under its declared Schema
    public async Task DA17_a_million_rows_read_from_a_CSV()
    {
        var output = TestContext.Current.TestOutputHelper!;
        var token = TestContext.Current.CancellationToken;
        output.WriteLine(Machine());
        var bytes = Export(Trades.Value);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"The trade export: {Million:N0} rows, {Mib(bytes.Length)} of UTF-8"));

        output.WriteLine($"{Show("Read from memory, 1,000,000 rows, 12 columns, keyed by Id", Time(() => Read(new MemoryStream(bytes, writable: false), token)))}, allocating {Mib(Allocated(() => Read(new MemoryStream(bytes, writable: false), token)))}");
        var path = Path.Combine(Path.GetTempPath(), $"exgrid-da17-{Guid.NewGuid():N}.csv");
        await File.WriteAllBytesAsync(path, bytes, token);
        try
        {
            output.WriteLine(Show("Read from a file (in the page cache), 1,000,000 rows", Time(() => TradeExport.ReadAsync(path, cancellationToken: token).AsTask().GetAwaiter().GetResult())));
        }
        finally
        {
            File.Delete(path);
        }
        var snapshot = await TradeExport.ReadAsync(new MemoryStream(bytes, writable: false), cancellationToken: token);
        Assert.Equal(Million, snapshot.RowCount);
        Assert.Equal(12, snapshot.Columns.Count);
    }

    // The trade export as DemoCsv declares it: nothing guessed, the header naming the columns.
    private static readonly CsvSchema TradeExport = new(
    [
        new CsvColumn("Id", SnapshotKind.Text),
        new CsvColumn("Account", SnapshotKind.Text),
        new CsvColumn("Region", SnapshotKind.Text),
        new CsvColumn("Desk", SnapshotKind.Text),
        new CsvColumn("Book", SnapshotKind.Text),
        new CsvColumn("Product", SnapshotKind.Text),
        new CsvColumn("Currency", SnapshotKind.Text),
        new CsvColumn("TradeDate", SnapshotKind.Date) { Header = "Trade date", DateFormats = ["yyyy-MM-dd"] },
        new CsvColumn("Notional", SnapshotKind.Decimal) { ThousandsSeparator = "," },
        new CsvColumn("Pnl", SnapshotKind.Decimal) { Header = "P&L", ThousandsSeparator = "," },
        new CsvColumn("Quantity", SnapshotKind.Integer),
        new CsvColumn("Confirmed", SnapshotKind.Boolean),
    ])
    {
        BlankText = ["NULL"],
        RecordKey = "Id",
    };

    private static Snapshot Read(Stream stream, CancellationToken token)
        => TradeExport.ReadAsync(stream, cancellationToken: token).AsTask().GetAwaiter().GetResult();

    private static SnapshotBuilder<Trade> Declared(bool keyed)
    {
        var builder = new SnapshotBuilder<Trade>()
            .Text("Id", t => t.Id)
            .Text("Account", t => t.Account)
            .Text("Region", t => t.Region)
            .Text("Desk", t => t.Desk)
            .Text("Book", t => t.Book)
            .Text("Product", t => t.Product)
            .Text("Currency", t => t.Currency)
            .Date("TradeDate", t => t.TradeDate)
            .Decimal("Notional", t => t.Notional)
            .Decimal("Pnl", t => t.Pnl)
            .Integer("Quantity", t => t.Quantity)
            .Boolean("Confirmed", t => t.Confirmed);
        return keyed ? builder.Key("Id") : builder;
    }

    // The demo's trades (DemoPivotData): three regions and their ten books, five products, five
    // currencies, 270 trade dates, money to the cent.
    private static Trade[] Make(int count)
    {
        (string Region, string[] Books)[] regions =
        [
            ("Americas", ["NY-RATES-01", "NY-CREDIT-02", "TOR-FX-01"]),
            ("EMEA", ["LDN-RATES-01", "LDN-FX-02", "FRA-CREDIT-01", "ZRH-EQ-01"]),
            ("APAC", ["TKY-RATES-01", "HKG-EQ-01", "SGP-FX-01"]),
        ];
        string[] products = ["Swap", "Bond", "Option", "Future", "Spot"];
        string[] currencies = ["USD", "EUR", "JPY", "GBP", "CHF"];
        var random = new Random(20260930);
        var start = new DateOnly(2026, 1, 2);
        var trades = new Trade[count];
        for (var i = 0; i < count; i++)
        {
            var (region, books) = regions[random.Next(regions.Length)];
            var book = books[random.Next(books.Length)];
            var desk = book.Contains("RATES", StringComparison.Ordinal) ? "Rates"
                : book.Contains("CREDIT", StringComparison.Ordinal) ? "Credit"
                : book.Contains("FX", StringComparison.Ordinal) ? "FX"
                : "Equities";
            var notional = random.Next(1, 500) * 10_000m;
            trades[i] = new Trade(
                "T" + (100_000 + i).ToString(CultureInfo.InvariantCulture),
                AccountOf(book),
                region,
                desk,
                book,
                products[random.Next(products.Length)],
                currencies[random.Next(currencies.Length)],
                start.AddDays(random.Next(0, 270)),
                notional,
                Math.Round((decimal)(random.NextDouble() - 0.45) * notional / 100m, 2),
                random.Next(1, 50),
                random.Next(10) > 0);
        }
        return trades;
    }

    // The trade export, written as DemoCsv writes it: UTF-8 without a byte-order mark, CR LF, a
    // comma between fields, ISO dates, money with a thousands separator and in quotes where it has one.
    private static byte[] Export(Trade[] trades)
    {
        using var bytes = new MemoryStream();
        using (var writer = new StreamWriter(bytes, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1 << 16))
        {
            writer.Write("Id,Account,Region,Desk,Book,Product,Currency,Trade date,Notional,P&L,Quantity,Confirmed\r\n");
            foreach (var t in trades)
            {
                writer.Write(t.Id);
                writer.Write(',');
                writer.Write(t.Account);
                writer.Write(',');
                writer.Write(t.Region);
                writer.Write(',');
                writer.Write(t.Desk);
                writer.Write(',');
                writer.Write(t.Book);
                writer.Write(',');
                writer.Write(t.Product);
                writer.Write(',');
                writer.Write(t.Currency);
                writer.Write(',');
                writer.Write(t.TradeDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(Quoted(t.Notional.ToString("#,##0.00", CultureInfo.InvariantCulture)));
                writer.Write(',');
                writer.Write(Quoted(t.Pnl.ToString("#,##0.00", CultureInfo.InvariantCulture)));
                writer.Write(',');
                writer.Write(t.Quantity.ToString(CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(t.Confirmed ? "TRUE" : "FALSE");
                writer.Write("\r\n");
            }
        }
        return bytes.ToArray();
    }

    private static string Quoted(string field) => field.Contains(',', StringComparison.Ordinal) ? $"\"{field}\"" : field;

    private static string AccountOf(string book)
    {
        var sum = 0;
        foreach (var c in book)
            sum = ((sum * 31) + c) % 9000;
        return (1000 + sum).ToString("000000", CultureInfo.InvariantCulture);
    }

    private static string Machine() => string.Create(CultureInfo.InvariantCulture,
        $"{Environment.ProcessorCount} logical CPUs, {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, server GC {System.Runtime.GCSettings.IsServerGC}");

    /// <summary>The milliseconds of each of seven runs, after two to warm up, each from a collected heap.</summary>
    private static double[] Time(Action run)
    {
        run();
        run();
        var times = new double[7];
        for (var i = 0; i < times.Length; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var clock = Stopwatch.StartNew();
            run();
            times[i] = clock.Elapsed.TotalMilliseconds;
        }
        return times;
    }

    private static long Allocated(Action run)
    {
        var before = GC.GetTotalAllocatedBytes(precise: true);
        run();
        return GC.GetTotalAllocatedBytes(precise: true) - before;
    }

    private static string Show(string what, double[] times)
    {
        var sorted = times.Order().ToArray();
        return string.Create(CultureInfo.InvariantCulture,
            $"{what}: median {sorted[sorted.Length / 2]:F0} ms, best {sorted[0]:F0} ms, worst {sorted[^1]:F0} ms (n={sorted.Length})");
    }

    private static string Mib(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:F1} MiB");
}
