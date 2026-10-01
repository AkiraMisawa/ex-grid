using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// The demo API server's Pivot Source (ADR-0065, ADR-0068), as <c>/pivot-db</c> and
/// <c>/pivot-live</c> reach it: the fields it offers, from <c>GET /api/pivot/fields</c>, and
/// <c>PivotSource.Fetch</c> over <c>POST /api/pivot/aggregate</c>, <c>/items</c> and
/// <c>/details</c>, each question and each answer a <c>PivotJson</c> document. The server answers
/// with SQL written by hand, and is held to the bundled source's answers (PV-22).
/// </summary>
public static class DemoServerPivot
{
    #region The code: fields
    // GET /api/pivot/fields: what the server's Pivot Source offers — its fields, with their
    // captions and formats, and the Aggregations SQLite answers exactly (ADR-0065).
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
    // ExPivot asks the source, and PivotSource.Fetch hands each question to these delegates. Every
    // question and every answer is a PivotJson document, so each delegate is one POST, and the
    // server answers it with SQL written by hand (ADR-0065). ExPivot never opens a connection: the
    // transport, its authentication and its retries are the application's.
    public static FetchingPivotSource Fetch(
        HttpClient http, IReadOnlyList<PivotField> fields, PivotSourceFeatures features, Action<string>? asked = null)
    {
        async Task<string> PostAsync(string path, string document, CancellationToken token)
        {
            asked?.Invoke(path);
            using var content = new StringContent(document, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(path, content, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token);
        }

        // The server answers at most 10,000 records a page (ADR-0068), so a longer question is
        // asked a page at a time, every page under the report's Source Version: if the data moves
        // on between two pages, the server refuses, and ExPivot says the data has changed rather
        // than show records that do not add up. ExPivot's own Details tab asks for no more than
        // its grid paints and reads ahead.
        async ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken token)
        {
            if (query.Count <= MaxDetailsPage)
                return PivotJson.ReadDetailPage(await PostAsync("api/pivot/details", PivotJson.Write(query), token));
            var records = new List<PivotDetailRecord>();
            PivotDetailPage page;
            do
            {
                var next = new PivotDetailsQuery(query.SourceVersion, query.RowItems, query.ColumnItems, query.HiddenItems,
                    query.Start + records.Count, Math.Min(MaxDetailsPage, query.Count - records.Count));
                page = PivotJson.ReadDetailPage(await PostAsync("api/pivot/details", PivotJson.Write(next), token));
                if (page.IsRefused)
                    return page;
                records.AddRange(page.Records);
            }
            while (page.Records.Count > 0 && records.Count < query.Count && query.Start + records.Count < page.Total);
            return new PivotDetailPage(page.SourceVersion, page.Fields, query.Start, page.Total, records);
        }

        return PivotSource.Fetch(fields, features,
            async (query, token) => PivotJson.ReadAnswer(await PostAsync("api/pivot/aggregate", PivotJson.Write(query), token)),
            async (query, token) => PivotJson.ReadItemPage(await PostAsync("api/pivot/items", PivotJson.Write(query), token)),
            DetailsAsync);
    }

    /// <summary>The most records the server answers in one Details page.</summary>
    public const int MaxDetailsPage = 10_000;
    #endregion
}
