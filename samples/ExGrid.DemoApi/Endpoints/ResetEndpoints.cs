namespace ExGrid.DemoApi;

/// <summary>What <c>POST /api/reset</c> answers.</summary>
/// <param name="Version">The Source Version the reset moved the data on to.</param>
/// <param name="Trades">How many trades there are now: the generated count.</param>
/// <param name="Restored">How many trades it put back: moved, booked or cancelled since.</param>
/// <param name="Live">What the live updates are set to now: off.</param>
internal sealed record ResetResponse(string Version, long Trades, int Restored, LiveSettings Live);

/// <summary><c>POST /api/reset</c>: the data as a start serves it.</summary>
internal static class ResetEndpoints
{
    /// <summary>
    /// Maps <c>POST /api/reset</c>, for a test that turned live updates on and must leave the data
    /// as it found it: the live updates are turned off — waiting for a tick under way — and the
    /// generated trades put back in one transaction that moves the Source Version on
    /// (<see cref="TradeStore.ResetAsync"/>). The hub says the change as it says a tick's, so a page
    /// still open asks again. Every reset moves the version on, even one that puts nothing back.
    /// </summary>
    public static IEndpointRouteBuilder MapResetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reset", async (TradeStore store, LiveUpdater live, HttpResponse response, CancellationToken cancellationToken) =>
        {
            if (store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            var settings = await live.SetAsync(live.Settings with { On = false }, cancellationToken);
            var change = await store.ResetAsync(cancellationToken);
            return Results.Ok(new ResetResponse(change.Version, store.TradeCount ?? 0, change.TradeIds.Length, settings));
        });
        return app;
    }
}
