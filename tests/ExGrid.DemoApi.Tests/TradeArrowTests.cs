using System.IO.Compression;
using System.Net;
using ExGrid.Data;
using ExGrid.Data.Arrow;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>"Database → Snapshot" (ADR-0064, ADR-0065, ADR-0069): the trades as an Arrow stream.</summary>
public sealed class TradeArrowTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0065/0069: the stream reads back as the Snapshot the DbDataReader way builds from the same trades, with the Source Version beside it
    public async Task ADR0065_the_Arrow_stream_reads_back_as_the_trades_Snapshot_and_names_its_version()
    {
        using var client = server.Factory.CreateClient();
        using var response = await client.GetAsync("/api/trades.arrows", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SnapshotArrow.StreamMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(server.Store.Version, Version(response));
        var read = await SnapshotArrow.ReadAsync(await response.Content.ReadAsByteArrayAsync(Token), cancellationToken: Token);
        var built = await Built(server.Store);
        Assert.Equal(server.Store.Version, built.Version);
        SameSnapshot(built.Snapshot, read);
    }

    [Fact] // ADR-0064/0065: each column has its kind — cents an exact Decimal, the ISO text a Date, 0 and 1 a Boolean — and TradeId is the Record Key
    public async Task ADR0064_each_column_is_read_as_its_kind_exactly()
    {
        using var client = server.Factory.CreateClient();
        var snapshot = await SnapshotArrow.ReadAsync(await client.GetByteArrayAsync("/api/trades.arrows", Token), cancellationToken: Token);

        Assert.Equal(
            [
                ("TradeId", "Trade ID", SnapshotKind.Text), ("Region", "Region", SnapshotKind.Text), ("Desk", "Desk", SnapshotKind.Text),
                ("Book", "Book", SnapshotKind.Text), ("Product", "Product", SnapshotKind.Text), ("Currency", "Currency", SnapshotKind.Text),
                ("Month", "Month", SnapshotKind.Text), ("TradeDate", "Trade date", SnapshotKind.Date), ("Notional", "Notional", SnapshotKind.Decimal),
                ("Pnl", "P&L", SnapshotKind.Decimal), ("Quantity", "Quantity", SnapshotKind.Integer), ("Confirmed", "Confirmed", SnapshotKind.Boolean),
            ],
            snapshot.Columns.Select(column => (column.Name, column.Caption, column.Kind)));
        Assert.Equal("TradeId", snapshot.RecordKey?.Name);
        Assert.Equal(DemoApiServer.Trades, snapshot.RowCount);

        // Row n is generated trade n, converted exactly.
        for (var n = 0; n < snapshot.RowCount; n++)
        {
            var row = snapshot.Rows[n];
            var trade = TradeGenerator.Generate(n);
            object?[] expected =
            [
                trade.TradeId, trade.Region, trade.Desk, trade.Book, trade.Product, trade.Currency,
                trade.TradeDate.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                trade.TradeDate.ToDateTime(TimeOnly.MinValue), Cents.ToDecimal(trade.NotionalCents), Cents.ToDecimal(trade.PnlCents),
                (long)trade.Quantity, trade.Confirmed,
            ];
            Assert.Equal(expected, snapshot.Columns.Select(column => snapshot.ValueAt(row, column)));
        }
        Assert.True(snapshot.IsBlank(snapshot.Rows[7], snapshot["Currency"]));
        Assert.Equal(0.01m, Cents.ToDecimal(1));
    }

    [Fact] // ADR-0065/0069: HTTP compresses the stream, gzip or Brotli as the browser accepts, and it decompresses to the same bytes
    public async Task ADR0065_the_stream_is_compressed_by_HTTP_with_gzip_or_Brotli()
    {
        using var client = server.Factory.CreateClient();
        var identity = await client.GetByteArrayAsync("/api/trades.arrows", Token);

        foreach (var (encoding, decompress) in new (string, Func<Stream, Stream>)[]
                 {
                     ("gzip", body => new GZipStream(body, CompressionMode.Decompress)),
                     ("br", body => new BrotliStream(body, CompressionMode.Decompress)),
                 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/trades.arrows");
            request.Headers.AcceptEncoding.ParseAdd(encoding);
            using var response = await client.SendAsync(request, Token);
            Assert.Equal(encoding, Assert.Single(response.Content.Headers.ContentEncoding));
            Assert.Contains("Accept-Encoding", response.Headers.Vary);
            var compressed = await response.Content.ReadAsByteArrayAsync(Token);
            Assert.True(compressed.Length < identity.Length / 2, $"{encoding}: {compressed.Length} of {identity.Length} bytes");
            using var decompressed = new MemoryStream();
            await using (var stream = decompress(new MemoryStream(compressed)))
                await stream.CopyToAsync(decompressed, Token);
            Assert.Equal(identity, decompressed.ToArray());
        }

        // A browser accepts both, and is answered with Brotli: faster than gzip at the fastest
        // levels, and smaller.
        using var browser = new HttpRequestMessage(HttpMethod.Get, "/api/trades.arrows");
        browser.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br, zstd");
        using var answered = await client.SendAsync(browser, Token);
        Assert.Equal("br", Assert.Single(answered.Content.Headers.ContentEncoding));
    }

    [Fact] // ADR-0065/0069: the bytes are kept per Source Version, and a live tick makes a new version, new bytes and a new Snapshot
    public async Task ADR0065_the_stream_is_built_once_per_version_and_a_live_tick_builds_the_next()
    {
        using var client = server.Factory.CreateClient();
        var arrow = server.Factory.Services.GetRequiredService<TradeArrow>();
        using var first = await client.GetAsync("/api/trades.arrows", Token);
        var builds = arrow.Builds;
        using var again = await client.GetAsync("/api/trades.arrows", Token);
        Assert.Equal(builds, arrow.Builds);
        var before = await first.Content.ReadAsByteArrayAsync(Token);
        Assert.Equal(before, await again.Content.ReadAsByteArrayAsync(Token));
        Assert.Equal(Version(first), Version(again));

        try
        {
            var change = await server.Store.ApplyLiveChangesAsync(10, Token);
            using var moved = await client.GetAsync("/api/trades.arrows", Token);

            Assert.Equal(change.Version, Version(moved));
            Assert.NotEqual(Version(first), Version(moved));
            Assert.Equal(builds + 1, arrow.Builds);
            var after = await moved.Content.ReadAsByteArrayAsync(Token);
            Assert.NotEqual(before, after);
            var snapshot = await SnapshotArrow.ReadAsync(after, cancellationToken: Token);
            var (_, trades) = await TestData.AllTrades(server.Store);
            var pnl = snapshot["Pnl"];
            var ids = snapshot["TradeId"];
            var changed = 0;
            foreach (var row in snapshot.Rows)
            {
                var id = (string)snapshot.ValueAt(row, ids)!;
                Assert.Equal(trades[id].Pnl, (decimal)snapshot.ValueAt(row, pnl)!);
                changed += change.TradeIds.Contains(id) ? 1 : 0;
            }
            // Ten moved, and one booked when the tick also cancelled one.
            Assert.InRange(changed, 10, 11);
            Assert.Equal(trades.Count, snapshot.RowCount);
        }
        finally
        {
            // The other tests here read the generated trades.
            await server.Store.ResetAsync(Token);
        }
    }

    [Fact] // ADR-0069: a page on another port reads the version header: CORS exposes it by name
    public async Task ADR0069_a_page_on_another_port_may_read_the_version_header()
    {
        using var client = server.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/trades.arrows");
        request.Headers.Add("Origin", "http://localhost:5299");
        using var response = await client.SendAsync(request, Token);

        Assert.Equal("http://localhost:5299", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains(SnapshotEndpoints.VersionHeader, string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")));
    }

    private static string Version(HttpResponseMessage response) =>
        string.Join(",", response.Headers.GetValues(SnapshotEndpoints.VersionHeader));

    // The trades read the DbDataReader way, under one read, with the server's declaration.
    private static Task<(string Version, Snapshot Snapshot)> Built(TradeStore store) =>
        store.ReadAsync(async (read, token) =>
        {
            await using var command = read.Command(TradeArrow.Sql);
            await using var reader = await command.ExecuteReaderAsync(token);
            return (read.Version, await TradeArrow.Declaration().BuildAsync(reader, cancellationToken: token));
        }, Token);

    /// <summary>Equal column by column and Blank by Blank, with the captions, kinds, Record Key and version.</summary>
    internal static void SameSnapshot(Snapshot expected, Snapshot actual)
    {
        Assert.Equal(expected.Columns.Select(c => (c.Name, c.Caption, c.Kind)), actual.Columns.Select(c => (c.Name, c.Caption, c.Kind)));
        Assert.Equal(expected.RecordKey?.Name, actual.RecordKey?.Name);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RowCount, actual.RowCount);
        for (var r = 0; r < expected.RowCount; r++)
        {
            for (var c = 0; c < expected.Columns.Count; c++)
            {
                Assert.Equal(expected.ValueAt(expected.Rows[r], expected.Columns[c]), actual.ValueAt(actual.Rows[r], actual.Columns[c]));
                Assert.Equal(expected.IsBlank(expected.Rows[r], expected.Columns[c]), actual.IsBlank(actual.Rows[r], actual.Columns[c]));
            }
        }
    }
}
