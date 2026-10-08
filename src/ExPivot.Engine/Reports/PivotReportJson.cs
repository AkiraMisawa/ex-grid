using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ExPivot.Engine;

/// <summary>The report protocol's typed JSON representation; it never serializes an engine graph.</summary>
public static class PivotReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
        Converters = { new LayoutConverter(), new DetailPageConverter(), new ItemPageConverter() },
    };
    private static readonly PivotReportJsonContext Context = new(Options);
    private static JsonTypeInfo<T> TypeInfo<T>() => Context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new NotSupportedException($"{typeof(T).Name} is not a report protocol type.");
    /// <summary>Writes a request, response or versioned operation as trim-safe JSON.</summary>
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, TypeInfo<T>());
    /// <summary>Reads a typed request, response or versioned operation; null is not a protocol value.</summary>
    public static T Read<T>(string json) => JsonSerializer.Deserialize(json, TypeInfo<T>())
        ?? throw new JsonException("A report protocol value cannot be null.");
    internal static bool SameSettings(PivotReportSettings a, PivotReportSettings b)
        => a.CultureName == b.CultureName && a.ChangeHighlightDuration == b.ChangeHighlightDuration && Same(a.Words, b.Words) && Same(a.OrderKeyPolicies, b.OrderKeyPolicies)
        && (a.LabelMetrics is null ? b.LabelMetrics is null : b.LabelMetrics is not null && a.LabelMetrics.SameAs(b.LabelMetrics));
    private static bool Same(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var value) && value == p.Value);
    internal sealed class LayoutConverter : JsonConverter<PivotLayout>
    {
        public override PivotLayout Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return PivotLayoutJson.Read(document.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, PivotLayout value, JsonSerializerOptions options)
            => writer.WriteRawValue(PivotLayoutJson.Write(value));
    }
    internal sealed class ItemPageConverter : JsonConverter<PivotItemPage>
    {
        public override PivotItemPage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return PivotJson.ReadItemPage(document.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, PivotItemPage value, JsonSerializerOptions options)
            => writer.WriteRawValue(PivotJson.Write(value));
    }
    internal sealed class DetailPageConverter : JsonConverter<PivotDetailPage>
    {
        public override PivotDetailPage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return PivotJson.ReadDetailPage(document.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, PivotDetailPage value, JsonSerializerOptions options)
            => writer.WriteRawValue(PivotJson.Write(value));
    }
}
