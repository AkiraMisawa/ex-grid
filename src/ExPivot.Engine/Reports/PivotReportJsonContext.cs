using System.Text.Json.Serialization;

namespace ExPivot.Engine;

// Only wire DTOs are rooted. Never preserve an entire engine assembly for a browser Consumer.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, IncludeFields = true,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = [typeof(PivotReportJson.LayoutConverter), typeof(PivotReportJson.DetailPageConverter), typeof(PivotReportJson.ItemPageConverter),
        typeof(PivotReportJson.DetailsQueryConverter)])]
[JsonSerializable(typeof(PivotReportRequest))]
[JsonSerializable(typeof(PivotReportUpdate))]
[JsonSerializable(typeof(PivotReportItemsQuery))]
[JsonSerializable(typeof(PivotReportItemsResult))]
[JsonSerializable(typeof(PivotReportCopyQuery))]
[JsonSerializable(typeof(PivotReportCopyResult))]
[JsonSerializable(typeof(PivotReportSummaryQuery))]
[JsonSerializable(typeof(PivotReportSummaryResult))]
[JsonSerializable(typeof(PivotDetailsQuery))]
[JsonSerializable(typeof(PivotDetailPage))]
[JsonSerializable(typeof(PivotItemPage))]
[JsonSerializable(typeof(PivotItemsQuery))]
internal partial class PivotReportJsonContext : JsonSerializerContext;
