using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>The live updates, turned on and off over the API and heard on the hub as a page hears them.</summary>
public sealed class LiveApiTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    [Fact] // ADR-0066/0067/0068: the server changes trades, moves the Source Version on, and the hub says both
    public async Task ADR0066_live_updates_move_the_version_on_and_the_hub_says_every_change()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;

        var versions = Channel.CreateUnbounded<string>();
        var changes = Channel.CreateUnbounded<(string Version, string[] TradeIds)>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.Factory.Server.BaseAddress, TradesHub.Path.TrimStart('/')), options =>
            {
                // The test server has no sockets; long polling runs the same hub over plain requests.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.Factory.Server.CreateHandler();
            })
            .Build();
        // The message names a page subscribes to, spelled as a page spells them.
        connection.On<string>("VersionChanged", version => versions.Writer.TryWrite(version));
        connection.On<string, string[]>("TradesChanged", (version, tradeIds) => changes.Writer.TryWrite((version, tradeIds)));
        await connection.StartAsync(token);

        using var client = server.Factory.CreateClient();
        var before = server.Store.Version!;
        using (var on = await client.PostAsJsonAsync("/api/live", new { on = true, intervalMs = 20, tradesPerTick = 3 }, token))
        {
            Assert.Equal(HttpStatusCode.OK, on.StatusCode);
            var settings = await on.Content.ReadFromJsonAsync<JsonElement>(token);
            Assert.True(settings.GetProperty("on").GetBoolean());
            Assert.Equal(20, settings.GetProperty("intervalMs").GetInt32());
            Assert.Equal(3, settings.GetProperty("tradesPerTick").GetInt32());
        }

        var heard = new List<(string Version, string[] TradeIds)>();
        var heardVersions = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            heard.Add(await changes.Reader.ReadAsync(token));
            heardVersions.Add(await versions.Reader.ReadAsync(token));
        }
        using (var off = await client.PostAsJsonAsync("/api/live", new { on = false }, token))
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        // Both messages for every change, in the order committed, from the version before on.
        Assert.Equal(heardVersions, heard.Select(h => h.Version));
        for (var i = 0; i < heard.Count; i++)
        {
            Assert.Equal(TestData.Run(before), TestData.Run(heard[i].Version));
            Assert.Equal(TestData.Counter(before) + 1 + i, TestData.Counter(heard[i].Version));
            Assert.InRange(heard[i].TradeIds.Length, 3, 5);
            Assert.Equal(heard[i].TradeIds.Order(StringComparer.Ordinal), heard[i].TradeIds);
        }

        // What the hub named is what the change touched: the database recorded the same.
        var recorded = await server.Store.ReadAsync(async (read, readToken) =>
        {
            await using var command = read.Command("SELECT TradeIds FROM changes WHERE Version = $version");
            command.Parameters.AddWithValue("$version", TestData.Counter(heard[0].Version));
            return JsonSerializer.Deserialize<string[]>((string)(await command.ExecuteScalarAsync(readToken))!);
        }, token);
        Assert.Equal(heard[0].TradeIds, recorded);

        // Off means the data holds still again.
        var status = await client.GetFromJsonAsync<JsonElement>("/api/status", token);
        Assert.False(status.GetProperty("live").GetProperty("on").GetBoolean());
        var stopped = status.GetProperty("version").GetString()!;
        Assert.True(TestData.Counter(stopped) >= TestData.Counter(heard[^1].Version));
        await Task.Delay(300, token);
        Assert.Equal(stopped, server.Store.Version);
        Assert.Equal(DemoApiServer.Trades, server.Store.TradeCount);
    }
}
