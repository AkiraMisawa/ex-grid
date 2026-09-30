namespace ExGrid.DemoApi;

/// <summary><c>GET /api/trades</c>: the trades as they are stored, a page at a time.</summary>
internal static class TradeEndpoints
{
    /// <summary>The largest page answered. A larger one is refused rather than cut short, so a
    /// page that asked for more never takes a short answer for the end of the data.</summary>
    public const int MaxPageSize = 1_000;

    /// <summary>
    /// Maps <c>GET /api/trades?start=0&amp;count=100</c>: <c>count</c> trades from the
    /// <c>start</c>th in <c>TradeId</c> order — fewer at the end — with how many there are and the
    /// Source Version they were read at, all from one state of the data (<see cref="TradePage"/>).
    /// A smoke test of the server, and the shape a page reads trades in.
    /// </summary>
    public static IEndpointRouteBuilder MapTradeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/trades", async (TradeStore store, HttpResponse response, CancellationToken cancellationToken,
            long start = 0, int count = 100) =>
        {
            if (store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            if (start < 0)
                return ApiResults.BadRequest("start is 0 or more.");
            if (count is < 1 or > MaxPageSize)
                return ApiResults.BadRequest($"count is from 1 to {MaxPageSize}.");
            return Results.Ok(await store.ReadPageAsync(start, count, cancellationToken));
        });
        return app;
    }
}
