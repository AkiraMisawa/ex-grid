using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using ExGrid.Data;
using Xunit;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// Measures writing and reading a million trades (ADR-0065). Never gated (AGENTS.md): it runs only
/// when asked for, in Release, and prints what it measured —
/// <c>dotnet test tests/ExGrid.Data.Arrow.Tests -c Release -- --explicit only</c>.
/// </summary>
public class MeasureTests
{
    /// <summary>A trade as the demo's API server holds one (ADR-0069): a unique id, five more text
    /// columns, a date, two money columns, an integer and a flag.</summary>
    private sealed record Trade(string TradeId, string Region, string Desk, string Book, string Product, string? Currency,
        DateOnly TradeDate, decimal Notional, decimal Pnl, int Quantity, bool Confirmed);

    [Fact(Explicit = true)] // ADR-0065: a million trades written and read on CoreCLR, and the payload's size — measured, never gated
    public async Task Measure_a_million_trades()
    {
        var output = TestContext.Current.TestOutputHelper!;
        var token = TestContext.Current.CancellationToken;
        var trades = Trades(1_000_000);
        output.WriteLine($"{Environment.ProcessorCount} logical CPUs, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

        // The demo's shape: every column of the API's trades, keyed by the unique TradeId.
        output.WriteLine(await Measure("the demo's trades, keyed by TradeId", await Build(trades, withId: true, token), token));
        // ADR-0065's measured shape: six text columns of few values, two money columns, a date and an integer.
        output.WriteLine(await Measure("ADR-0065's shape, without the unique id", await Build(trades, withId: false, token), token));
    }

    private static async Task<string> Measure(string shape, Snapshot snapshot, CancellationToken token)
    {
        byte[] payload = [];
        var writes = Time(() =>
        {
            using var stream = new MemoryStream();
            SnapshotArrow.WriteAsync(snapshot, stream, token).GetAwaiter().GetResult();
            payload = stream.ToArray();
        });
        var inMemory = Time(() => SnapshotArrow.ReadAsync(payload, cancellationToken: token).AsTask().GetAwaiter().GetResult());
        var streamed = Time(() => SnapshotArrow.ReadAsync(new MemoryStream(payload, writable: false), cancellationToken: token).AsTask().GetAwaiter().GetResult());
        Fixtures.AssertSame(snapshot, await SnapshotArrow.ReadAsync(payload, cancellationToken: token));
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        await SnapshotArrow.ReadAsync(payload, cancellationToken: token);
        allocated = GC.GetTotalAllocatedBytes(precise: true) - allocated;

        return string.Create(CultureInfo.InvariantCulture,
            $"""
            {shape}: {snapshot.RowCount:N0} rows, {snapshot.Columns.Count} columns
              payload {Mib(payload.Length)} raw, {Mib(Gzipped(payload))} with gzip, {Mib(Brotli(payload))} with Brotli
              write {Show(writes)}
              read from memory {Show(inMemory)}, allocating {Mib(allocated)}
              read from a stream {Show(streamed)}
            """);
    }

    private static Trade[] Trades(int count)
    {
        string[] regions = ["Americas", "EMEA", "APAC"];
        string[][] books = [["NY-RATES-01", "NY-CREDIT-02", "TOR-FX-01"], ["LDN-RATES-01", "LDN-FX-02", "FRA-CREDIT-01", "ZRH-EQ-01"], ["TKY-RATES-01", "HKG-EQ-01", "SGP-FX-01"]];
        string[] desks = ["Rates", "Credit", "FX", "Equities"];
        string[] products = ["Swap", "Bond", "Option", "Future", "Spot"];
        string?[] currencies = ["USD", "EUR", "JPY", "GBP", "CHF", null];
        var random = new Random(20260930);
        var start = new DateOnly(2026, 1, 2);
        var trades = new Trade[count];
        for (var i = 0; i < count; i++)
        {
            var region = random.Next(regions.Length);
            var notional = random.Next(1, 500) * 10_000m;
            trades[i] = new Trade(
                "T" + (100_000 + i).ToString(CultureInfo.InvariantCulture),
                regions[region],
                desks[random.Next(desks.Length)],
                books[region][random.Next(books[region].Length)],
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

    private static async Task<Snapshot> Build(Trade[] trades, bool withId, CancellationToken token)
    {
        var builder = new SnapshotBuilder<Trade>();
        if (withId)
            builder.Text("TradeId", t => t.TradeId);
        builder
            .Text("Region", t => t.Region)
            .Text("Desk", t => t.Desk)
            .Text("Book", t => t.Book)
            .Text("Product", t => t.Product)
            .Text("Currency", t => t.Currency)
            .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
            .Decimal("Notional", t => t.Notional)
            .Decimal("Pnl", t => t.Pnl, caption: "P&L")
            .Integer("Quantity", t => t.Quantity);
        if (withId)
            builder.Boolean("Confirmed", t => t.Confirmed).Key("TradeId");
        return await builder.BuildAsync(trades, cancellationToken: token);
    }

    /// <summary>The milliseconds of each of five runs, after one to warm up.</summary>
    private static double[] Time(Action run)
    {
        run();
        var times = new double[5];
        for (var i = 0; i < times.Length; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var clock = Stopwatch.StartNew();
            run();
            times[i] = clock.Elapsed.TotalMilliseconds;
        }
        return times;
    }

    private static string Show(double[] times)
    {
        var sorted = times.Order().ToArray();
        return string.Create(CultureInfo.InvariantCulture, $"best {sorted[0]:F0} ms, median {sorted[sorted.Length / 2]:F0} ms");
    }

    private static string Mib(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:F1} MiB");

    private static long Gzipped(byte[] payload)
    {
        using var sink = new MemoryStream();
        using (var gzip = new GZipStream(sink, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(payload);
        return sink.Length;
    }

    private static long Brotli(byte[] payload)
    {
        using var sink = new MemoryStream();
        using (var brotli = new BrotliStream(sink, CompressionLevel.Optimal, leaveOpen: true))
            brotli.Write(payload);
        return sink.Length;
    }
}
