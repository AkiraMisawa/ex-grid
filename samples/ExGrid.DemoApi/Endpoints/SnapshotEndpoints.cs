using ExGrid.Data.Arrow;

namespace ExGrid.DemoApi;

/// <summary>
/// <c>GET /api/trades.arrows</c>: the trades as an Arrow IPC stream (ADR-0064), for
/// <c>/pivot-db</c>'s "database → Snapshot". A page reads it whole and into a Snapshot:
/// <code>
/// using var request = new HttpRequestMessage(HttpMethod.Get, "api/trades.arrows");
/// request.SetBrowserResponseStreamingEnabled(false);   // in a browser: 0.3 s rather than 2 s (ADR-0064)
/// using var response = await http.SendAsync(request, token);
/// var version = response.Headers.GetValues("ExGrid-Source-Version").Single();
/// var snapshot = await SnapshotArrow.ReadAsync(await response.Content.ReadAsByteArrayAsync(token), cancellationToken: token);
/// </code>
/// The stream is written uncompressed, and HTTP compresses it (<see cref="DemoCompression"/>): a
/// browser undoes gzip and Brotli natively.
/// </summary>
internal static class SnapshotEndpoints
{
    /// <summary>The response header that names the Source Version the stream was read at, which
    /// a page compares with the hub's <c>VersionChanged</c>. Exposed to a page on another origin
    /// (<see cref="DemoCors"/>).</summary>
    public const string VersionHeader = "ExGrid-Source-Version";

    /// <summary>Maps <c>GET /api/trades.arrows</c>.</summary>
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/trades.arrows", async (TradeArrow arrow, TradeStore store, HttpResponse response,
            CancellationToken cancellationToken) =>
        {
            if (store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            var stream = await arrow.GetAsync(cancellationToken);
            response.Headers[VersionHeader] = stream.Version;
            return Results.Bytes(stream.Bytes, SnapshotArrow.StreamMediaType);
        });
        return app;
    }
}
