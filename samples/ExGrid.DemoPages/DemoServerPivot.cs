using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// The demo API's server Pivot Source: its fields, and the Leaf Aggregates, Items and Details it answers in SQL, from
/// which the pivot builds the report in the browser (ADR-0066, ADR-0370 section 3).
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
        var offered = declared.Features;
        return (fields, new PivotSourceFeatures(offered.Aggregations, offered.CanRefresh, offered.CountsItems, offered.AnswersLevels));
    }

    private sealed record FieldsAnswer(FieldAnswer[] Fields, FeaturesAnswer Features);
    private sealed record FieldAnswer(string Name, PivotFieldType Type, string Caption, string? Format);
    private sealed record FeaturesAnswer(PivotAggregation[] Aggregations, bool CanRefresh, bool CountsItems, bool AnswersLevels);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    #endregion

    #region The code: source
    // The server as a Pivot Source (ADR-0066, ADR-0370 section 3): it answers the Leaf Aggregates of
    // each question in SQL, and the pivot builds the report and its totals from them in the browser.
    // Each question is one POST of a PivotJson document; Details come a page at a time, each asked
    // under the question's Source Version. The transport, and what it says when the data moves on,
    // are the page's.
    public static FetchingPivotSource Source(
        HttpClient http, IReadOnlyList<PivotField> fields, PivotSourceFeatures features, Action<string>? asked = null)
    {
        async ValueTask<string> PostAsync(string path, string question, CancellationToken token)
        {
            asked?.Invoke(path);
            using var content = new StringContent(question, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path, content, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token);
        }
        async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken token)
        {
            var records = new List<PivotDetailRecord>();
            while (true)
            {
                var next = new PivotDetailsQuery(query.SourceVersion, query.RowItems, query.ColumnItems, query.HiddenItems,
                    query.Start + records.Count, Math.Min(MaxDetailsPage, query.Count - records.Count));
                var page = PivotJson.ReadDetailPage(await PostAsync("api/pivot/details", PivotJson.Write(next), token));
                if (page.IsRefused) return page;
                // Pages of two versions are not one page: the records moved between them.
                if (page.SourceVersion != query.SourceVersion) return PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion));
                records.AddRange(page.Records);
                if (page.Records.Count == 0 || records.Count >= query.Count || query.Start + records.Count >= page.Total)
                    return new PivotDetailPage(page.SourceVersion, page.Fields, query.Start, page.Total, records);
            }
        }
        async ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken token)
            => PivotJson.ReadAnswer(await PostAsync("api/pivot/aggregate", PivotJson.Write(query), token));
        async ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken token)
            => PivotJson.ReadItemPage(await PostAsync("api/pivot/items", PivotJson.Write(query), token));
        if (!features.CountsItems)
            return PivotSource.Fetch(fields, features, AggregateAsync, ItemsAsync, DetailsAsync);
        return PivotSource.Fetch(fields, features, AggregateAsync, ItemsAsync, DetailsAsync, async token =>
        {
            using var response = await http.GetAsync("api/pivot/item-counts", token);
            response.EnsureSuccessStatusCode();
            return PivotJson.ReadItemCounts(await response.Content.ReadAsStringAsync(token));
        });
    }

    /// <summary>The most records the server answers in one Details page.</summary>
    public const int MaxDetailsPage = 10_000;
    #endregion
}
