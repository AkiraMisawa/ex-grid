using System.Net;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// CORS as a browser asks it (ADR-0068): the demo hosts' pages run on other ports of this
/// machine, so every call they make is cross-origin, and the browser asks first.
/// </summary>
public sealed class CorsTests(DemoApiServer server) : IClassFixture<DemoApiServer>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory] // ADR-0068: a page on another port of this machine may call the API, without credentials
    [InlineData("http://localhost:5299")]
    [InlineData("http://127.0.0.1:5298")]
    [InlineData("http://[::1]:6298")]
    [InlineData("https://localhost:7001")]
    public async Task ADR0068_the_API_answers_a_page_on_another_port_of_this_machine(string origin)
    {
        using var client = server.Factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/live");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        using var allowed = await client.SendAsync(preflight, Token);

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(origin, Header(allowed, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", Header(allowed, "Access-Control-Allow-Methods"));
        Assert.Contains("content-type", Header(allowed, "Access-Control-Allow-Headers"));
        Assert.Null(Header(allowed, "Access-Control-Allow-Credentials"));

        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        get.Headers.Add("Origin", origin);
        using var answered = await client.SendAsync(get, Token);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Equal(origin, Header(answered, "Access-Control-Allow-Origin"));
        Assert.Null(Header(answered, "Access-Control-Allow-Credentials"));
    }

    [Fact] // ADR-0068: SignalR's negotiate, sent with credentials by the browser client, is allowed from another port
    public async Task ADR0068_the_hub_negotiates_with_a_page_on_another_port_with_credentials()
    {
        const string origin = "http://localhost:5299";
        using var client = server.Factory.CreateClient();
        // What the JavaScript client's negotiate makes the browser ask first: its two headers.
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/hubs/trades/negotiate?negotiateVersion=1");
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-requested-with,x-signalr-user-agent");
        using var allowed = await client.SendAsync(preflight, Token);

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(origin, Header(allowed, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(allowed, "Access-Control-Allow-Credentials"));
        Assert.Contains("x-signalr-user-agent", Header(allowed, "Access-Control-Allow-Headers"));

        using var negotiate = new HttpRequestMessage(HttpMethod.Post, "/hubs/trades/negotiate?negotiateVersion=1");
        negotiate.Headers.Add("Origin", origin);
        negotiate.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var negotiated = await client.SendAsync(negotiate, Token);
        Assert.Equal(HttpStatusCode.OK, negotiated.StatusCode);
        Assert.Equal(origin, Header(negotiated, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(negotiated, "Access-Control-Allow-Credentials"));
        Assert.Contains("connectionToken", await negotiated.Content.ReadAsStringAsync(Token));
    }

    [Theory] // ADR-0068: only this machine's pages: any other origin gets no leave to read the answer
    [InlineData("http://example.com")]
    [InlineData("http://localhost.example.com:5299")]
    [InlineData("http://192.168.1.20:5299")]
    [InlineData("null")]
    public async Task ADR0068_a_page_elsewhere_is_not_allowed(string origin)
    {
        using var client = server.Factory.CreateClient();
        foreach (var (path, method) in new[] { ("/api/status", "GET"), ("/hubs/trades/negotiate?negotiateVersion=1", "POST") })
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, path);
            preflight.Headers.Add("Origin", origin);
            preflight.Headers.Add("Access-Control-Request-Method", method);
            using var refused = await client.SendAsync(preflight, Token);
            Assert.Null(Header(refused, "Access-Control-Allow-Origin"));
            Assert.Null(Header(refused, "Access-Control-Allow-Credentials"));
        }
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
