namespace ExGrid.DemoApi;

/// <summary>
/// What <c>GET /api/status</c> answers.
/// </summary>
/// <param name="State"><c>starting</c>, <c>generating</c>, <c>ready</c> or <c>failed</c>.</param>
/// <param name="Trades">How many trades there are now; null until ready.</param>
/// <param name="Version">The current Source Version; null until ready.</param>
/// <param name="Generating">How far generation has got, while it runs; null otherwise.</param>
/// <param name="Live">What the live updates are set to.</param>
internal sealed record StatusResponse(
    TradeStoreState State, long? Trades, string? Version, GenerationProgress? Generating, LiveSettings Live);

/// <summary><c>GET /api/status</c>.</summary>
internal static class StatusEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/status</c>: <c>200</c> once the trades are ready, and <c>503</c> with the
    /// same body until then — so a start-up wait that polls it, such as layer 3's
    /// <c>webServer</c>, waits for the data and not only for the port.
    /// </summary>
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/status", (TradeStore store, LiveUpdater live, HttpResponse response) =>
        {
            var status = new StatusResponse(store.State, store.TradeCount, store.Version, store.Generating, live.Settings);
            if (store.State == TradeStoreState.Ready)
                return Results.Ok(status);
            response.Headers.RetryAfter = "1";
            return Results.Json(status, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
        return app;
    }
}
