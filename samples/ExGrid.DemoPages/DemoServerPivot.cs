using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// The demo API's versioned report Windows and operations. SQL aggregation and report
/// computation stay on the server; the browser receives its requested display rows.
/// </summary>
public static class DemoServerPivot
{
    #region The code: fields
    // GET /api/pivot/fields: what the server's Pivot Source offers — its fields, with their
    // captions and formats, and the Aggregations SQLite answers exactly (ADR-0066).
    public static async Task<(PivotField[] Fields, PivotSourceFeatures Features)> DeclaredAsync(
        HttpClient http, CancellationToken token)
    {
        var declared = await http.GetFromJsonAsync<FieldsAnswer>("api/pivot/fields", Json, token)
            ?? throw new InvalidOperationException("The server declared no fields.");
        var fields = declared.Fields.Select(f => new PivotField(f.Name, f.Type, f.Caption, f.Format)).ToArray();
        return (fields, new PivotSourceFeatures(declared.Features.Aggregations, declared.Features.CanRefresh));
    }

    private sealed record FieldsAnswer(FieldAnswer[] Fields, FeaturesAnswer Features);
    private sealed record FieldAnswer(string Name, PivotFieldType Type, string Caption, string? Format);
    private sealed record FeaturesAnswer(PivotAggregation[] Aggregations, bool CanRefresh);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    #endregion

    #region The code: fetch
    // A report identity belongs to one view. Its server calculation is bounded by the
    // Consumer's store; an expired baseline is recovered by the Window protocol.
    public static FetchingPivotReportSource Fetch(
        HttpClient http, IReadOnlyList<PivotField> fields, PivotSourceFeatures features, Action<string>? asked = null)
    {
        var path = "api/pivot/reports/" + Guid.NewGuid().ToString("N");
        async ValueTask<TAnswer> PostAsync<TQuery, TAnswer>(string operation, TQuery query, CancellationToken token)
        {
            asked?.Invoke("api/pivot/" + operation);
            using var content = new StringContent(PivotReportJson.Write(query), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path + "/" + operation, content, token);
            response.EnsureSuccessStatusCode();
            return PivotReportJson.Read<TAnswer>(await response.Content.ReadAsStringAsync(token));
        }
        async ValueTask<PivotReportDetailsResult> DetailsAsync(PivotReportDetailsQuery query, CancellationToken token)
        {
            var records = new List<PivotDetailRecord>();
            PivotReportDetailsResult result;
            do
            {
                var next = query with { Start = query.Start + records.Count, Count = Math.Min(MaxDetailsPage, query.Count - records.Count) };
                result = await PostAsync<PivotReportDetailsQuery, PivotReportDetailsResult>("details", next, token);
                if (result.Refusal is not null || result.Page is not { } page || page.IsRefused) return result;
                records.AddRange(page.Records);
                if (page.Records.Count == 0 || records.Count >= query.Count || query.Start + records.Count >= page.Total) break;
            } while (true);
            var last = result.Page!;
            return result with { Page = new PivotDetailPage(last.SourceVersion, last.Fields, query.Start, last.Total, records) };
        }
        return PivotReportSource.Fetch(fields, features, PivotReportUpdateMode.FullRefresh,
            (query, token) => PostAsync<PivotReportRequest, PivotReportUpdate>("window", query, token),
            copy: (query, token) => PostAsync<PivotReportCopyQuery, PivotReportCopyResult>("copy", query, token),
            summary: (query, token) => PostAsync<PivotReportSummaryQuery, PivotReportSummaryResult>("summary", query, token),
            details: DetailsAsync,
            reportItems: (query, token) => PostAsync<PivotReportItemsQuery, PivotReportItemsResult>("items", query, token),
            dispose: async () =>
            {
                try { using var response = await http.DeleteAsync(path); }
                catch (HttpRequestException) { /* The store's idle expiry releases an unreachable report. */ }
                catch (OperationCanceledException) { /* Likewise when the transport has stopped. */ }
            });
    }

    /// <summary>The most records the server answers in one Details page.</summary>
    public const int MaxDetailsPage = 10_000;
    #endregion
}
