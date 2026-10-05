namespace ExGrid.DemoApi;

/// <summary>What <c>POST /api/trades/by-id</c> takes: <c>{ "ids": ["T10000001", "T10000002"] }</c>.</summary>
internal sealed record TradeIdsRequest(string[]? Ids);

/// <summary>What <c>POST /api/trades/summary</c> takes (ADR-0130): the selected ranges over the
/// trades in <c>TradeId</c> order, the columns they index, and the figures asked —
/// <c>{ "ranges": [{ "top": 0, "left": 7, "rows": 10, "columns": 1 }], "columns": ["TradeId", ...], "figures": "sum, count" }</c>.</summary>
internal sealed record TradeSummaryRequest(TradeSummaryRange[]? Ranges, string[]? Columns, ExGrid.Summarizing.SummaryFigures Figures);

/// <summary>One selected range: its top row and left column, and how many of each.</summary>
internal sealed record TradeSummaryRange(int Top, int Left, int Rows, int Columns);

/// <summary>What <c>POST /api/trades/summary</c> answers (ADR-0130): the decline's reason, or each
/// figure asked.</summary>
internal sealed record TradeSummaryResponse(string? Declined, IReadOnlyDictionary<string, TradeSummaryFigure> Figures);

/// <summary>One figure: its exact value as text where it has one, its <c>double</c>, or its error.</summary>
internal sealed record TradeSummaryFigure(bool Empty, string? Exact, double? Number, string? Error)
{
    public static TradeSummaryFigure Of(ExGrid.Data.AggregateResult result) => new(
        result.IsEmpty,
        result.Exact?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        result.IsNumber ? result.Number : null,
        result.Error == ExGrid.Data.AggregateError.None ? null : result.Error.ToString());
}

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
    /// <c>start</c>th in <c>TradeId</c> order — fewer at the end — with how many there are and the
    /// Source Version they were read at, all from one state of the data (<see cref="TradePage"/>).
    /// A smoke test of the server, and the shape a page reads trades in.
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
        // The Selection Summary in SQL (ADR-0130): the request as the grid asks it, the figures as
        // the one definition reads them — each its exact value, its double, or the error it is.
        app.MapPost("/api/trades/summary", async (TradeSummaryRequest request, TradeStore store,
            HttpResponse response, CancellationToken cancellationToken) =>
        {
            if (store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            if (request.Ranges is not { } ranges || request.Columns is not { } columns)
                return ApiResults.BadRequest("ranges and columns name the cells to summarise.");
            var result = await store.SummarizeAsync(new ExGrid.Summarizing.GridSummaryRequest
            {
                Ranges = [.. ranges.Select(r => new ExGrid.Selection.SelectionRange(r.Top, r.Left, r.Rows, r.Columns))],
                Columns = columns,
                RowSequenceVersion = 0,
                Figures = request.Figures,
            }, cancellationToken);
            return Results.Ok(new TradeSummaryResponse(result.DeclineReason, ExGrid.Summarizing.SummaryFigureOrder.Each
                .Where(figure => result[figure] is not null)
                .ToDictionary(figure => figure.ToString(), figure => TradeSummaryFigure.Of(result[figure]!.Value))));
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
