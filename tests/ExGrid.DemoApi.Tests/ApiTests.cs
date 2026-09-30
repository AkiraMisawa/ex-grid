using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>The API as a page calls it, with the live updates left off.</summary>
public sealed class ApiTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0068: what the server holds, which version, and whether it is changing
    public async Task ADR0068_status_answers_the_trades_the_version_and_the_live_settings()
    {
        using var client = server.Factory.CreateClient();
        using var response = await client.GetAsync("/api/status", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Equal("ready", status.GetProperty("state").GetString());
        Assert.Equal(DemoApiServer.Trades, status.GetProperty("trades").GetInt64());
        Assert.Equal(server.Store.Version, status.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("generating").ValueKind);
        var live = status.GetProperty("live");
        Assert.False(live.GetProperty("on").GetBoolean());
        Assert.Equal(250, live.GetProperty("intervalMs").GetInt32());
        Assert.Equal(20, live.GetProperty("tradesPerTick").GetInt32());
    }

    [Fact] // ADR-0068: live updates are off until a page turns them on, so the data holds still
    public async Task ADR0068_live_updates_are_off_until_a_page_turns_them_on()
    {
        using var client = server.Factory.CreateClient();
        var live = await client.GetFromJsonAsync<JsonElement>("/api/live", Token);
        Assert.False(live.GetProperty("on").GetBoolean());

        var before = server.Store.Version;
        await Task.Delay(400, Token);
        Assert.Equal(before, server.Store.Version);
        Assert.Equal(0, TestData.Counter(before!));
    }

    [Fact] // ADR-0068: a page of the trades as stored, money exact to the cent, a Blank as null
    public async Task ADR0068_trades_come_a_page_at_a_time_in_TradeId_order_with_the_total_and_the_version()
    {
        using var client = server.Factory.CreateClient();
        var page = await client.GetFromJsonAsync<JsonElement>("/api/trades?start=0&count=10", Token);

        Assert.Equal(server.Store.Version, page.GetProperty("version").GetString());
        Assert.Equal(DemoApiServer.Trades, page.GetProperty("total").GetInt64());
        Assert.Equal(0, page.GetProperty("start").GetInt64());
        var trades = page.GetProperty("trades").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(0, 10).Select(n => TradeGenerator.TradeId(n)), trades.Select(t => t.GetProperty("tradeId").GetString()));

        for (var n = 0; n < trades.Length; n++)
        {
            var expected = TradeGenerator.Generate(n);
            var trade = trades[n];
            Assert.Equal(expected.Book, trade.GetProperty("book").GetString());
            Assert.Equal(expected.Currency, trade.GetProperty("currency").GetString());
            Assert.Equal(TradeDatabase.FormatDate(expected.TradeDate), trade.GetProperty("tradeDate").GetString());
            // As decimals, exactly: the raw JSON number has its two decimal places.
            Assert.Equal(Cents.ToDecimal(expected.PnlCents), trade.GetProperty("pnl").GetDecimal());
            Assert.Equal(Cents.ToDecimal(expected.NotionalCents).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                trade.GetProperty("notional").GetRawText());
            Assert.Equal(expected.Quantity, trade.GetProperty("quantity").GetInt32());
            Assert.Equal(expected.Confirmed, trade.GetProperty("confirmed").GetBoolean());
        }
        Assert.Equal(JsonValueKind.Null, trades[7].GetProperty("currency").ValueKind);
        Assert.Equal(TradeGenerator.QuoteBook, trades[5].GetProperty("book").GetString());

        var last = await client.GetFromJsonAsync<JsonElement>($"/api/trades?start={DemoApiServer.Trades - 3}", Token);
        Assert.Equal(3, last.GetProperty("trades").GetArrayLength());
    }

    [Theory] // ADR-0068 and principle 1: a question out of range is refused by name, not cut short
    [InlineData("/api/trades?count=1001", "count is from 1 to 1000.")]
    [InlineData("/api/trades?count=0", "count is from 1 to 1000.")]
    [InlineData("/api/trades?start=-1", "start is 0 or more.")]
    public async Task ADR0068_a_page_out_of_range_is_refused_by_name(string url, string detail)
    {
        using var client = server.Factory.CreateClient();
        using var response = await client.GetAsync(url, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Equal(detail, problem.GetProperty("detail").GetString());
    }

    [Theory] // ADR-0068: a malformed question is the caller's mistake: a 400, never the server's failure
    [InlineData("GET", "/api/trades?count=abc", null)]
    [InlineData("POST", "/api/live", "{\"on\":\"yes\"}")]
    [InlineData("POST", "/api/live", "{\"intervalMs\":100}")]
    [InlineData("POST", "/api/live", "{\"on\":true,\"intervalMs\":5}")]
    [InlineData("POST", "/api/live", "{\"on\":true,\"tradesPerTick\":1001}")]
    public async Task ADR0068_a_malformed_question_is_answered_400(string method, string url, string? body)
    {
        using var client = server.Factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/live", Token)).GetProperty("on").GetBoolean());
    }
}

/// <summary>What the server answers while its trades are not ready.</summary>
public sealed class NotReadyTests(DemoApiServerNotReady server) : IClassFixture<DemoApiServerNotReady>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact] // ADR-0068: until the data is ready, status says so with a 503 — what layer 3's start-up wait polls
    public async Task ADR0068_status_is_503_until_the_trades_are_ready()
    {
        using var client = server.Factory.CreateClient();
        using var response = await client.GetAsync("/api/status", Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.RetryAfter is not null);
        var status = await response.Content.ReadFromJsonAsync<JsonElement>(Token);
        Assert.Equal("starting", status.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("version").ValueKind);
        Assert.Equal(JsonValueKind.Null, status.GetProperty("trades").ValueKind);
    }

    [Fact] // ADR-0068 and principle 1: a page asking before the data is ready is told to wait, never handed no trades
    public async Task ADR0068_trades_and_live_updates_wait_for_the_data()
    {
        using var client = server.Factory.CreateClient();
        using var trades = await client.GetAsync("/api/trades", Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, trades.StatusCode);
        Assert.Equal("The trades are being made ready.",
            (await trades.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("detail").GetString());

        using var on = await client.PostAsJsonAsync("/api/live", new { on = true }, Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, on.StatusCode);
        using var off = await client.PostAsJsonAsync("/api/live", new { on = false }, Token);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
    }
}
