using System.Globalization;
using System.Text.Json;
using ExPivot.Engine;

namespace ExGrid.DemoApi;

/// <summary>What <c>POST /api/lab/live-tick</c> takes: the tick's seed and how many trades it moves.</summary>
internal sealed record LabTickRequest(ulong Seed, int Trades);

/// <summary>What <c>POST /api/lab/live-tick</c> answers: the Source Version the tick moved the data on to,
/// and the trades it moved, booked or cancelled.</summary>
internal sealed record LabTickResponse(string Version, string[] TradeIds, string[] BookedIds);

/// <summary>What <c>POST /api/lab/kept/{id}/release</c> answers: the managed heap after a full collection
/// with the report's kept Windows and without them, and what they were.</summary>
internal sealed record LabReleaseResponse(long[] HeapWithBytes, long[] HeapWithoutBytes, long HeldBytes, LabWindowMeasure Released);

/// <summary>
/// LAB ONLY, never merged (<c>lab/remote-read-ahead</c>, ticket 14 of the live-data spec): what the
/// read-ahead measurement asks of the server beside the pages' own questions — a live tick that changes
/// the same trades for the same seed, the Windows a report keeps, and what they hold on the heap — and
/// the measure of each Window answered, in its response's headers.
/// </summary>
internal static class LabEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Maps <c>POST /api/lab/live-tick</c>, <c>GET /api/lab/kept/{id}</c> and
    /// <c>POST /api/lab/kept/{id}/release</c>.</summary>
    public static IEndpointRouteBuilder MapLabEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/lab/live-tick", async (LabTickRequest tick, TradeStore store, HttpResponse response, CancellationToken ct) =>
        {
            if (store.State != TradeStoreState.Ready)
                return ApiResults.NotReady(store, response);
            if (tick.Trades is < 1 or > LiveSettings.MaxTradesPerTick)
                return ApiResults.BadRequest($"trades is from 1 to {LiveSettings.MaxTradesPerTick}.");
            var change = await store.ApplyLiveChangesAsync(tick.Trades, tick.Seed, ct);
            return Results.Ok(new LabTickResponse(change.Version, change.TradeIds, change.BookedIds));
        });
        app.MapGet("/api/lab/kept/{id}", async (string id, PivotReportStore reports, CancellationToken ct) =>
            await reports.LabAsync(id, source => source.LabKept(), ct) is { } kept ? Results.Ok(kept) : Results.NotFound());
        app.MapPost("/api/lab/kept/{id}/release", async (string id, PivotReportStore reports, CancellationToken ct) =>
            await reports.LabAsync(id, Release, ct) is { } released ? Results.Ok(released) : Results.NotFound());
        return app;
    }

    // The heap after a full collection, three times, with the kept Windows; then let go of, and three
    // times again. Run under the store's gate: no question is answered meanwhile.
    private static LabReleaseResponse Release(LocalPivotReportSource source)
    {
        var with = new long[3];
        for (var i = 0; i < with.Length; i++)
            with[i] = Settled();
        var released = source.LabReleaseKept();
        var without = new long[3];
        for (var i = 0; i < without.Length; i++)
            without[i] = Settled();
        return new(with, without, Median(with) - Median(without), released);
    }

    private static long Settled()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static long Median(long[] values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }

    /// <summary>The measure of a Window answered, in its response's headers: <c>Server-Timing</c> for
    /// the times, and <c>X-Lab-Window</c> for the whole measure as JSON.</summary>
    /// <param name="response">The window question's response, before its body is written.</param>
    /// <param name="measure">The report source's own measure of the answer.</param>
    /// <param name="beforeSourceMs">From the request's arrival to the report source asked: the body
    /// read, the store's gate waited for, and its refresh of a moved Source Version.</param>
    /// <param name="serializeMs">The answer written as JSON.</param>
    /// <param name="endpointMs">From the request's arrival to the answer written.</param>
    public static void Describe(HttpResponse response, LabWindowMeasure measure, double beforeSourceMs, double serializeMs, double endpointMs)
    {
        static string Ms(double ms) => ms.ToString("0.###", CultureInfo.InvariantCulture);
        response.Headers["Server-Timing"] =
            $"before;dur={Ms(beforeSourceMs)}, compute;dur={Ms(measure.ComputeMs)}, project;dur={Ms(measure.ProjectMs)}, "
            + $"digest;dur={Ms(measure.DigestMs)}, source;dur={Ms(measure.TotalMs)}, serialize;dur={Ms(serializeMs)}, endpoint;dur={Ms(endpointMs)}";
        response.Headers["X-Lab-Window"] = JsonSerializer.Serialize(measure, Json);
    }
}
