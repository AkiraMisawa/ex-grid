using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary><c>/api/trades/by-id</c>: the trades the hub names, read again (ADR-0068, ADR-0069).</summary>
public sealed class TradesByIdTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0068/0069: the named trades as they are now, in TradeId order, with the keys no trade has, at one version
    public async Task ADR0069_by_id_answers_the_named_trades_and_the_missing_keys_at_one_version()
    {
        using var client = server.Factory.CreateClient();
        var answer = await client.GetFromJsonAsync<JsonElement>("/api/trades/by-id?ids=T10000007,T10000002,T99999999,T10000002", Token);

        Assert.Equal(server.Store.Version, answer.GetProperty("version").GetString());
        var trades = answer.GetProperty("trades").EnumerateArray().ToArray();
        Assert.Equal(["T10000002", "T10000007"], trades.Select(t => t.GetProperty("tradeId").GetString()));
        Assert.Equal(JsonValueKind.Null, trades[1].GetProperty("currency").ValueKind);
        var generated = TradeGenerator.Generate(2);
        Assert.Equal(Cents.ToDecimal(generated.PnlCents), trades[0].GetProperty("pnl").GetDecimal());
        Assert.Equal(TradeDatabase.FormatDate(generated.TradeDate), trades[0].GetProperty("tradeDate").GetString());
        Assert.Equal(["T99999999"], answer.GetProperty("missing").EnumerateArray().Select(id => id.GetString()));

        // The keys repeated rather than comma-separated, and in a body for a long list.
        var repeated = await client.GetFromJsonAsync<JsonElement>("/api/trades/by-id?ids=T10000003&ids=T10000001", Token);
        Assert.Equal(["T10000001", "T10000003"], repeated.GetProperty("trades").EnumerateArray().Select(t => t.GetProperty("tradeId").GetString()));
        // Every third trade up to 3,003: the last two are past the 3,000 there are.
        var ids = Enumerable.Range(0, 1_002).Select(n => TradeGenerator.TradeId(n * 3)).ToArray();
        using var posted = await client.PostAsJsonAsync("/api/trades/by-id", new { ids }, Token);
        var body = await posted.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Equal(1_000, body.GetProperty("trades").GetArrayLength());
        Assert.Equal([TradeGenerator.TradeId(3_000), TradeGenerator.TradeId(3_003)],
            body.GetProperty("missing").EnumerateArray().Select(id => id.GetString()));
    }

    [Fact] // ADR-0068: a trade a tick cancelled is missing, and the one it booked is found
    public async Task ADR0068_a_cancelled_trade_is_missing_and_a_booked_one_found()
    {
        using var client = server.Factory.CreateClient();
        try
        {
            TradeChange? booking = null;
            for (var tick = 0; tick < 200 && booking is null; tick++)
            {
                var change = await server.Store.ApplyLiveChangesAsync(1, Token);
                if (change.TradeIds.Length == 3)
                    booking = change;
            }
            Assert.NotNull(booking);

            var answer = await client.GetFromJsonAsync<JsonElement>("/api/trades/by-id?ids=" + string.Join(',', booking.TradeIds), Token);
            Assert.Equal(server.Store.Version, answer.GetProperty("version").GetString());
            Assert.Equal(2, answer.GetProperty("trades").GetArrayLength());
            var missing = Assert.Single(answer.GetProperty("missing").EnumerateArray()).GetString();
            Assert.Contains(missing, booking.TradeIds);
        }
        finally
        {
            await server.Store.ResetAsync(Token);
        }
    }

    [Fact] // ADR-0141 / LV-8: a trade cancelled by name over HTTP leaves, moves the order token, and a reset puts it back
    public async Task ADR0141_a_trade_cancelled_by_name_moves_the_order_token()
    {
        using var client = server.Factory.CreateClient();
        try
        {
            var before = await client.GetFromJsonAsync<JsonElement>("/api/trades?start=0&count=10", Token);
            var id = TradeGenerator.TradeId(2_500);

            using var cancelled = await client.PostAsJsonAsync("/api/trades/cancel", new { tradeId = id }, Token);
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            var after = await client.GetFromJsonAsync<JsonElement>("/api/trades?start=0&count=10", Token);

            // The page's rows are the same rows; only the order token says a trade beyond it went.
            Assert.Equal(
                before.GetProperty("trades").EnumerateArray().Select(t => t.GetProperty("tradeId").GetString()),
                after.GetProperty("trades").EnumerateArray().Select(t => t.GetProperty("tradeId").GetString()));
            Assert.NotEqual(before.GetProperty("orderToken").GetString(), after.GetProperty("orderToken").GetString());
            Assert.Equal(DemoApiServer.Trades - 1, after.GetProperty("total").GetInt64());

            using var again = await client.PostAsJsonAsync("/api/trades/cancel", new { tradeId = id }, Token);
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
            using var nothing = await client.PostAsJsonAsync("/api/trades/cancel", new { }, Token);
            Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
        }
        finally
        {
            await server.Store.ResetAsync(Token);
        }
    }

    [Theory] // ADR-0069 and principle 1: a question naming no trade, or more than one answer holds, is refused by name, not cut short
    [InlineData("/api/trades/by-id", "ids names the trades to read")]
    [InlineData("/api/trades/by-id?ids=,", "ids names the trades to read")]
    public async Task ADR0069_by_id_without_ids_is_refused_by_name(string url, string detail)
    {
        using var client = server.Factory.CreateClient();
        using var response = await client.GetAsync(url, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith(detail, (await response.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("detail").GetString());
    }

    [Fact] // ADR-0069 and principle 1: more keys than one answer holds are refused, never answered in part
    public async Task ADR0069_more_ids_than_an_answer_holds_are_refused()
    {
        using var client = server.Factory.CreateClient();
        var ids = Enumerable.Range(0, TradeEndpoints.MaxIds + 1).Select(n => TradeGenerator.TradeId(n)).ToArray();
        using var response = await client.PostAsJsonAsync("/api/trades/by-id", new { ids }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal($"ids names at most {TradeEndpoints.MaxIds} trades; ask for the rest in another request.",
            (await response.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("detail").GetString());
    }
}

/// <summary><c>POST /api/reset</c>: the data as a start serves it, under a new version (ADR-0069).</summary>
public sealed class ResetTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0069: a reset puts back exactly what the live updates moved, booked and cancelled, in one change that moves the version on
    public async Task ADR0069_a_reset_puts_back_the_generated_trades_and_names_what_it_changed()
    {
        using var directory = new TempDirectory();
        await using var store = await TestData.ReadyStore(directory.Path, 1_000);
        var generated = directory.File(TradeDatabase.FileName(1_000));
        var bytes = TestData.FileHash(generated);
        var (_, before) = await TestData.AllTrades(store);
        var touched = new SortedSet<string>(StringComparer.Ordinal);
        var booked = false;
        for (var tick = 0; tick < 40 || !booked; tick++)
        {
            var change = await store.ApplyLiveChangesAsync(5, Token);
            touched.UnionWith(change.TradeIds);
            booked |= change.TradeIds.Length > 5;
            Assert.True(tick < 400, "No tick booked a trade.");
        }
        var (_, moved) = await TestData.AllTrades(store);
        // The trades a tick touched that differ now: moved, cancelled, or booked and still there.
        // One moved back to where it started, or booked and cancelled again, is as generated.
        var differing = touched.Where(id => before.TryGetValue(id, out var was)
            ? !moved.TryGetValue(id, out var now) || was != now
            : moved.ContainsKey(id)).ToArray();
        var told = new List<TradeChange>();
        while (store.Changes.TryRead(out var heard))
            told.Add(heard);

        var reset = await store.ResetAsync(Token);

        Assert.Equal(TestData.Counter(told[^1].Version) + 1, TestData.Counter(reset.Version));
        Assert.Equal(TestData.Run(told[^1].Version), TestData.Run(reset.Version));
        Assert.Equal(differing, reset.TradeIds);
        Assert.Equal(reset.Version, store.Version);
        Assert.Equal(1_000, store.TradeCount);
        Assert.True(store.Changes.TryRead(out var said));
        Assert.Equal(reset.Version, said.Version);
        Assert.Equal(reset.TradeIds, said.TradeIds);
        using (var copy = TradeDatabase.Open(store.WorkingPath!, SqliteOpenMode.ReadOnly))
            Assert.Equal(TestData.Fingerprint(1_000), TestData.Fingerprint(copy));
        Assert.Equal(bytes, TestData.FileHash(generated));

        // A reset with nothing to put back still moves the version on.
        var again = await store.ResetAsync(Token);
        Assert.Equal(TestData.Counter(reset.Version) + 1, TestData.Counter(again.Version));
        Assert.Empty(again.TradeIds);

        // And live updates go on from it, booking the next generated trade again.
        var next = await store.ApplyLiveChangesAsync(3, Token);
        Assert.Equal(TestData.Counter(again.Version) + 1, TestData.Counter(next.Version));
    }

    [Fact] // ADR-0069: POST /api/reset turns live updates off, puts the trades back, and the hub says the change as a page hears it
    public async Task ADR0069_POST_reset_turns_live_updates_off_and_the_hub_says_the_change()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var changes = Channel.CreateUnbounded<(string Version, string[] TradeIds)>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.Factory.Server.BaseAddress, TradesHub.Path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.Factory.Server.CreateHandler();
            })
            .Build();
        connection.On<string, string[]>("TradesChanged", (version, tradeIds) => changes.Writer.TryWrite((version, tradeIds)));
        await connection.StartAsync(token);

        using var client = server.Factory.CreateClient();
        using (var on = await client.PostAsJsonAsync("/api/live", new { on = true, intervalMs = 10, tradesPerTick = 25 }, token))
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        for (var heard = 0; heard < 3; heard++)
            await changes.Reader.ReadAsync(token);

        using var response = await client.PostAsync("/api/reset", null, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(token);
        var version = answer.GetProperty("version").GetString()!;
        Assert.False(answer.GetProperty("live").GetProperty("on").GetBoolean());
        Assert.Equal(DemoApiServer.Trades, answer.GetProperty("trades").GetInt64());
        Assert.True(answer.GetProperty("restored").GetInt32() > 0);
        Assert.Equal(version, server.Store.Version);
        (string Version, string[] TradeIds) said;
        do
            said = await changes.Reader.ReadAsync(token);
        while (said.Version != version);
        Assert.Equal(answer.GetProperty("restored").GetInt32(), said.TradeIds.Length);
        using (var copy = TradeDatabase.Open(server.Store.WorkingPath!, SqliteOpenMode.ReadOnly))
            Assert.Equal(TestData.Fingerprint(DemoApiServer.Trades), TestData.Fingerprint(copy));
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/live", token)).GetProperty("on").GetBoolean());
    }
}
