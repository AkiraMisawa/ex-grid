using System.Text;
using ExPivot.Engine;

namespace ExGrid.DemoApi;

/// <summary>A field as <c>GET /api/pivot/fields</c> answers it: <see cref="PivotField"/>'s name,
/// type, caption and format.</summary>
internal sealed record PivotFieldResponse(string Name, PivotFieldType Type, string Caption, string? Format);

/// <summary>The source's features as <c>GET /api/pivot/fields</c> answers them.</summary>
internal sealed record PivotFeaturesResponse(IReadOnlyList<PivotAggregation> Aggregations, bool CanRefresh);

/// <summary>What <c>GET /api/pivot/fields</c> answers: everything <c>PivotSource.Fetch</c> takes
/// besides the transport.</summary>
internal sealed record PivotFieldsResponse(IReadOnlyList<PivotFieldResponse> Fields, PivotFeaturesResponse Features);

/// <summary>
/// The server as a Pivot Source (ADR-0065, ADR-0068): <see cref="TradePivotSource"/>'s three
/// questions over HTTP, each a <c>PivotJson</c> document both ways, so a page's
/// <c>PivotSource.Fetch</c> delegates are a <c>POST</c> each:
/// <code>
/// var source = PivotSource.Fetch(fields, features,
///     async (query, ct) => PivotJson.ReadAnswer(await Post("api/pivot/aggregate", PivotJson.Write(query), ct)),
///     async (query, ct) => PivotJson.ReadItemPage(await Post("api/pivot/items", PivotJson.Write(query), ct)),
///     async (query, ct) => PivotJson.ReadDetailPage(await Post("api/pivot/details", PivotJson.Write(query), ct)));
/// </code>
/// A refusal is an answer, not a failure: it comes back <c>200</c>, as the document it is. A
/// document that cannot be read — not JSON, of a <c>PivotJson</c> version this server does not
/// read, or asking something that cannot be asked — is the caller's mistake: <c>400</c>, naming it.
/// </summary>
internal static class PivotEndpoints
{
    /// <summary>The media type every <c>PivotJson</c> document is sent as.</summary>
    public const string JsonMediaType = "application/json";

    /// <summary>Maps <c>GET /api/pivot/fields</c>, and <c>POST /api/pivot/aggregate</c>,
    /// <c>/api/pivot/items</c> and <c>/api/pivot/details</c>.</summary>
    public static IEndpointRouteBuilder MapPivotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/pivot/fields", () => new PivotFieldsResponse(
            TradePivotFields.Fields.Select(field => new PivotFieldResponse(field.Name, field.Type, field.Caption, field.Format)).ToArray(),
            new PivotFeaturesResponse(TradePivotFields.Features.Aggregations, TradePivotFields.Features.CanRefresh)));

        app.MapPost("/api/pivot/aggregate", (HttpRequest request, TradePivotSource source, TradeStore store, CancellationToken cancellationToken) =>
            Answer(request, store, PivotJson.ReadQuery,
                async (query, token) => PivotJson.Write(await source.AggregateAsync(query, token)), cancellationToken));
        app.MapPost("/api/pivot/items", (HttpRequest request, TradePivotSource source, TradeStore store, CancellationToken cancellationToken) =>
            Answer(request, store, PivotJson.ReadItemsQuery,
                async (query, token) => PivotJson.Write(await source.ItemsAsync(query, token)), cancellationToken));
        app.MapPost("/api/pivot/details", (HttpRequest request, TradePivotSource source, TradeStore store, CancellationToken cancellationToken) =>
            Answer(request, store, PivotJson.ReadDetailsQuery,
                async (query, token) => PivotJson.Write(await source.DetailsAsync(query, token)), cancellationToken));
        return app;
    }

    // Reads the question's document, asks the source, and answers with the answer's document.
    private static async Task<IResult> Answer<TQuestion>(
        HttpRequest request,
        TradeStore store,
        Func<string, TQuestion> read,
        Func<TQuestion, CancellationToken, ValueTask<string>> answer,
        CancellationToken cancellationToken)
    {
        if (store.State != TradeStoreState.Ready)
            return ApiResults.NotReady(store, request.HttpContext.Response);
        string document;
        // The body is the server's to close, not the reader's.
        using (var body = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            document = await body.ReadToEndAsync(cancellationToken);
        TQuestion question;
        try
        {
            question = read(document);
        }
        catch (Exception e) when (e is FormatException or NotSupportedException)
        {
            return ApiResults.BadRequest(e.Message);
        }
        return Results.Text(await answer(question, cancellationToken), JsonMediaType, Encoding.UTF8);
    }
}
