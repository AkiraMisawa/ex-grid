namespace ExGrid.DemoApi;

/// <summary>
/// What <c>POST /api/live</c> takes: <c>{ "on": true, "intervalMs": 250, "tradesPerTick": 20 }</c>.
/// <c>on</c> is required; a setting left out keeps its current value.
/// </summary>
internal sealed record LiveRequest(bool? On, int? IntervalMs, int? TradesPerTick);

/// <summary><c>GET</c> and <c>POST /api/live</c>: the live updates, off until a page turns them on.</summary>
internal static class LiveEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/live</c>, which answers the settings (<see cref="LiveSettings"/>), and
    /// <c>POST /api/live</c>, which sets them and answers what they now are. A value out of range
    /// is refused by name, not clamped. Once turning them off has answered, the data holds still.
    /// </summary>
    public static IEndpointRouteBuilder MapLiveEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/live", (LiveUpdater live) => live.Settings);
        app.MapPost("/api/live", async (LiveRequest request, LiveUpdater live, TradeStore store, HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            if (request.On is not { } on)
                return ApiResults.BadRequest("on is true or false.");
            var current = live.Settings;
            var intervalMs = request.IntervalMs ?? current.IntervalMs;
            if (intervalMs is < LiveSettings.MinIntervalMs or > LiveSettings.MaxIntervalMs)
            {
                return ApiResults.BadRequest(
                    $"intervalMs is from {LiveSettings.MinIntervalMs} to {LiveSettings.MaxIntervalMs}.");
            }
            var tradesPerTick = request.TradesPerTick ?? current.TradesPerTick;
            if (tradesPerTick is < 1 or > LiveSettings.MaxTradesPerTick)
                return ApiResults.BadRequest($"tradesPerTick is from 1 to {LiveSettings.MaxTradesPerTick}.");
            if (on && store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            return Results.Ok(await live.SetAsync(new LiveSettings(on, intervalMs, tradesPerTick), cancellationToken));
        });
        return app;
    }
}
