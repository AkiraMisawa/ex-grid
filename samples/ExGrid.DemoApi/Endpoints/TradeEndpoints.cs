namespace ExGrid.DemoApi;

/// <summary>What <c>POST /api/trades/by-id</c> takes: <c>{ "ids": ["T10000001", "T10000002"] }</c>.</summary>
internal sealed record TradeIdsRequest(string[]? Ids);

/// <summary>What <c>POST /api/trades/cancel</c> takes: <c>{ "tradeId": "T10000250" }</c>.</summary>
internal sealed record TradeCancelRequest(string? TradeId);

/// <summary><c>GET /api/trades</c>, a page at a time, and <c>/api/trades/by-id</c>, the trades named.</summary>
internal static class TradeEndpoints
{
    /// <summary>The largest page answered. A larger one is refused rather than cut short, so a
    /// page that asked for more never takes a short answer for the end of the data.</summary>
    public const int MaxPageSize = 1_000;

    /// <summary>The most trades one by-id question names: a live tick's worth, its largest
    /// (<see cref="LiveSettings.MaxTradesPerTick"/>) with a trade cancelled and one booked, and
    /// room to spare. More is refused, not cut short.</summary>
    public const int MaxIds = 2_000;

    /// <summary>
    /// Maps <c>GET /api/trades?start=0&amp;count=100</c>: <c>count</c> trades from the
    /// <c>start</c>th in <c>TradeId</c> order — fewer at the end — with how many there are, the
    /// Source Version they were read at and its order token, all from one state of the data
    /// (<see cref="TradePage"/>). The order token moves when a trade anywhere is cancelled or booked,
    /// which is what a grid's <c>GridSource.Fetch</c> drops a Selection on (ADR-0141). A smoke test
    /// of the server, and the shape a page reads trades in.
    /// <para>
    /// And <c>GET /api/trades/by-id?ids=T10000001,T10000002</c>: the named trades as they are now,
    /// for <c>/grid-live</c> to read again the trades the hub's <c>TradesChanged</c> names
    /// (ADR-0068) — <see cref="TradesById"/>, whose <c>missing</c> keys are trades removed. The
    /// keys come comma-separated, or as <c>ids</c> repeated. A long list goes in the body of
    /// <c>POST /api/trades/by-id</c> instead (<see cref="TradeIdsRequest"/>), since a URL of a
    /// thousand keys passes what a server reads of a request line.
    /// </para>
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
        app.MapGet("/api/trades/by-id", (TradeStore store, HttpResponse response, CancellationToken cancellationToken,
            string[]? ids) => ById(store, response, ids, cancellationToken));
        app.MapPost("/api/trades/by-id", (TradeIdsRequest request, TradeStore store, HttpResponse response,
            CancellationToken cancellationToken) => ById(store, response, request.Ids, cancellationToken));
        // One trade cancelled where a test says (ADR-0141's LV-8), told on the hub as a tick's change
        // is; POST /api/reset puts it back.
        app.MapPost("/api/trades/cancel", async (TradeCancelRequest request, TradeStore store, HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            if (store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            if (string.IsNullOrEmpty(request.TradeId))
                return ApiResults.BadRequest("tradeId names the trade to cancel.");
            return await store.CancelAsync(request.TradeId, cancellationToken) is { } change
                ? Results.Ok(change)
                : Results.Problem($"No trade is {request.TradeId}.", statusCode: StatusCodes.Status404NotFound);
        });
        return app;
    }

    private static async Task<IResult> ById(TradeStore store, HttpResponse response, string[]? ids, CancellationToken cancellationToken)
    {
        if (store.State != TradeStoreState.Ready)
            return ApiResults.NotReady(store, response);
        var named = (ids ?? [])
            .SelectMany(id => id.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        if (named.Length == 0)
            return ApiResults.BadRequest("ids names the trades to read: ids=T10000000,T10000001.");
        if (named.Length > MaxIds)
            return ApiResults.BadRequest($"ids names at most {MaxIds} trades; ask for the rest in another request.");
        return Results.Ok(await store.ReadByIdAsync(named, cancellationToken));
    }
}
