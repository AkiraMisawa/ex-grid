using System.Text;
using System.Text.Json;

namespace ExPivot.Engine;

/// <summary>
/// The Pivot Layout's serialised form (ADR-0058): versioned JSON a Consumer stores as View State
/// and reads back as the same layout. Written and read by hand, member by member, so that
/// nothing depends on reflection a trimmed WebAssembly build may have removed, and a document of
/// a version this reader does not know is refused rather than half-read.
/// </summary>
public static class PivotLayoutJson
{
    /// <summary>The version this writer writes and the only one this reader reads.</summary>
    public const int Version = 1;

    /// <summary>The layout as JSON.</summary>
    public static string Write(PivotLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("version", Version);
            json.WriteString("form", Name(layout.Form));
            json.WriteString("valuesAxis", layout.ValuesAxis == PivotAxis.Rows ? "rows" : "columns");
            json.WriteBoolean("subtotalsAtTop", layout.SubtotalsAtTop);
            json.WriteBoolean("grandTotalRow", layout.GrandTotalRow);
            json.WriteBoolean("grandTotalColumn", layout.GrandTotalColumn);
            json.WriteBoolean("repeatItemLabels", layout.RepeatItemLabels);
            WritePlacements(json, "filters", layout.Filters);
            WritePlacements(json, "rows", layout.Rows);
            WritePlacements(json, "columns", layout.Columns);
            json.WriteStartArray("values");
            foreach (var value in layout.Values)
            {
                json.WriteStartObject();
                json.WriteString("field", value.Field);
                json.WriteString("aggregation", Name(value.Aggregation));
                if (value.Caption is not null)
                    json.WriteString("caption", value.Caption);
                if (value.ShowValuesAs != PivotShowValuesAs.NoCalculation)
                    json.WriteString("showValuesAs", Name(value.ShowValuesAs));
                if (value.NumberFormat is not null)
                    json.WriteString("numberFormat", value.NumberFormat);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// A layout read back from <see cref="Write"/>'s JSON. Refuses by name a document of another
    /// version, an unknown name for a form, an axis, an Aggregation, a Show Values As, a sort
    /// direction or an Item kind, and a member of the wrong type. A member left out takes its
    /// default.
    /// </summary>
    public static PivotLayout Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new FormatException("A Pivot Layout is a JSON object.");
        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
            throw new FormatException("A Pivot Layout names its version.");
        if (!version.TryGetInt32(out var number) || number != Version)
            throw new NotSupportedException($"A Pivot Layout of version {version.GetRawText()} cannot be read; this reader reads version {Version}.");

        var layout = new PivotLayout
        {
            Form = Parse(root, "form", ParseForm, PivotReportForm.Compact),
            ValuesAxis = Parse(root, "valuesAxis", text => text switch
            {
                "columns" => PivotAxis.Columns,
                "rows" => PivotAxis.Rows,
                _ => throw Unknown("valuesAxis", text),
            }, PivotAxis.Columns),
            SubtotalsAtTop = Boolean(root, "subtotalsAtTop", true),
            GrandTotalRow = Boolean(root, "grandTotalRow", true),
            GrandTotalColumn = Boolean(root, "grandTotalColumn", true),
            RepeatItemLabels = Boolean(root, "repeatItemLabels", false),
            Filters = ReadPlacements(root, "filters"),
            Rows = ReadPlacements(root, "rows"),
            Columns = ReadPlacements(root, "columns"),
            Values = Array(root, "values").Select(ReadValue).ToArray(),
        };
        return layout;
    }

    private static void WritePlacements(Utf8JsonWriter json, string name, IReadOnlyList<PivotFieldPlacement> placements)
    {
        json.WriteStartArray(name);
        foreach (var placement in placements)
        {
            json.WriteStartObject();
            json.WriteString("field", placement.Field);
            if (placement.Sort != PivotSort.Ascending)
            {
                json.WriteStartObject("sort");
                json.WriteString("direction", placement.Sort.Direction == PivotSortDirection.Descending ? "descending" : "ascending");
                if (placement.Sort.ByValue is { } byValue)
                    json.WriteNumber("byValue", byValue);
                json.WriteEndObject();
            }
            if (!placement.Subtotals)
                json.WriteBoolean("subtotals", false);
            if (placement.Collapsed)
                json.WriteBoolean("collapsed", true);
            WriteKeys(json, "hiddenItems", placement.HiddenItems);
            WriteKeys(json, "toggledItems", placement.ToggledItems);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteKeys(Utf8JsonWriter json, string name, IReadOnlyList<PivotItemKey> keys)
    {
        if (keys.Count == 0)
            return;
        json.WriteStartArray(name);
        foreach (var key in keys)
        {
            json.WriteStartObject();
            json.WriteString("kind", Name(key.Kind));
            if (key.Value is not null)
                json.WriteString("value", key.Value);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static PivotFieldPlacement[] ReadPlacements(JsonElement root, string name)
        => Array(root, name).Select(element =>
        {
            var placement = new PivotFieldPlacement(String(element, "field"))
            {
                Subtotals = Boolean(element, "subtotals", true),
                Collapsed = Boolean(element, "collapsed", false),
                HiddenItems = ReadKeys(element, "hiddenItems"),
                ToggledItems = ReadKeys(element, "toggledItems"),
            };
            if (element.TryGetProperty("sort", out var sort))
            {
                if (sort.ValueKind != JsonValueKind.Object)
                    throw new FormatException("'sort' is an object.");
                var direction = Parse(sort, "direction", text => text switch
                {
                    "ascending" => PivotSortDirection.Ascending,
                    "descending" => PivotSortDirection.Descending,
                    _ => throw Unknown("direction", text),
                }, PivotSortDirection.Ascending);
                int? byValue = sort.TryGetProperty("byValue", out var index)
                    ? index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out var at) ? at : throw new FormatException("'byValue' is an integer.")
                    : null;
                placement = placement with { Sort = new PivotSort(direction, byValue) };
            }
            return placement;
        }).ToArray();

    private static PivotItemKey[] ReadKeys(JsonElement element, string name)
        => Array(element, name).Select(key =>
        {
            var kind = Parse(key, "kind", ParseKind, PivotItemKind.Blank);
            string? value = key.TryGetProperty("value", out var text)
                ? text.ValueKind == JsonValueKind.String ? text.GetString() : throw new FormatException("An Item's 'value' is a string.")
                : null;
            return new PivotItemKey(kind, value);
        }).ToArray();

    private static PivotValueField ReadValue(JsonElement element)
        => new(String(element, "field"), Parse(element, "aggregation", ParseAggregation, PivotAggregation.Sum))
        {
            Caption = OptionalString(element, "caption"),
            ShowValuesAs = Parse(element, "showValuesAs", ParseShowValuesAs, PivotShowValuesAs.NoCalculation),
            NumberFormat = OptionalString(element, "numberFormat"),
        };

    private static IEnumerable<JsonElement> Array(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array))
            return [];
        if (array.ValueKind != JsonValueKind.Array)
            throw new FormatException($"'{name}' is an array.");
        return array.EnumerateArray().ToArray();
    }

    private static string String(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new FormatException($"'{name}' is a string, and is required.");

    private static string? OptionalString(JsonElement element, string name)
        => !element.TryGetProperty(name, out var value) ? null
            : value.ValueKind == JsonValueKind.String ? value.GetString()
            : throw new FormatException($"'{name}' is a string.");

    private static bool Boolean(JsonElement element, string name, bool fallback)
        => !element.TryGetProperty(name, out var value) ? fallback
            : value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new FormatException($"'{name}' is true or false."),
            };

    private static T Parse<T>(JsonElement element, string name, Func<string, T> parse, T fallback)
        => !element.TryGetProperty(name, out var value) ? fallback
            : value.ValueKind == JsonValueKind.String ? parse(value.GetString()!)
            : throw new FormatException($"'{name}' is a string.");

    internal static FormatException Unknown(string name, string text) => new($"'{text}' is not a {name} this reader knows.");

    private static string Name(PivotReportForm form) => form switch
    {
        PivotReportForm.Compact => "compact",
        PivotReportForm.Outline => "outline",
        PivotReportForm.Tabular => "tabular",
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "Unknown report form."),
    };

    private static PivotReportForm ParseForm(string text) => text switch
    {
        "compact" => PivotReportForm.Compact,
        "outline" => PivotReportForm.Outline,
        "tabular" => PivotReportForm.Tabular,
        _ => throw Unknown("form", text),
    };

    private static readonly (PivotAggregation Value, string Name)[] Aggregations =
    [
        (PivotAggregation.Sum, "sum"), (PivotAggregation.Count, "count"), (PivotAggregation.Average, "average"),
        (PivotAggregation.Max, "max"), (PivotAggregation.Min, "min"), (PivotAggregation.Product, "product"),
        (PivotAggregation.CountNumbers, "countNumbers"), (PivotAggregation.StdDev, "stdDev"),
        (PivotAggregation.StdDevp, "stdDevp"), (PivotAggregation.Var, "var"), (PivotAggregation.Varp, "varp"),
    ];

    private static string Name(PivotAggregation aggregation)
        => Aggregations.FirstOrDefault(a => a.Value == aggregation).Name
            ?? throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown Aggregation.");

    private static PivotAggregation ParseAggregation(string text)
        => Aggregations.Any(a => a.Name == text) ? Aggregations.First(a => a.Name == text).Value : throw Unknown("aggregation", text);

    private static string Name(PivotShowValuesAs showAs) => showAs switch
    {
        PivotShowValuesAs.NoCalculation => "noCalculation",
        PivotShowValuesAs.PercentOfGrandTotal => "percentOfGrandTotal",
        PivotShowValuesAs.PercentOfColumnTotal => "percentOfColumnTotal",
        PivotShowValuesAs.PercentOfRowTotal => "percentOfRowTotal",
        _ => throw new ArgumentOutOfRangeException(nameof(showAs), showAs, "Unknown Show Values As."),
    };

    private static PivotShowValuesAs ParseShowValuesAs(string text) => text switch
    {
        "noCalculation" => PivotShowValuesAs.NoCalculation,
        "percentOfGrandTotal" => PivotShowValuesAs.PercentOfGrandTotal,
        "percentOfColumnTotal" => PivotShowValuesAs.PercentOfColumnTotal,
        "percentOfRowTotal" => PivotShowValuesAs.PercentOfRowTotal,
        _ => throw Unknown("showValuesAs", text),
    };

    internal static string Name(PivotItemKind kind) => kind switch
    {
        PivotItemKind.Number => "number",
        PivotItemKind.Date => "date",
        PivotItemKind.Text => "text",
        PivotItemKind.Boolean => "boolean",
        PivotItemKind.Error => "error",
        PivotItemKind.Blank => "blank",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Item kind."),
    };

    internal static PivotItemKind ParseKind(string text) => text switch
    {
        "number" => PivotItemKind.Number,
        "date" => PivotItemKind.Date,
        "text" => PivotItemKind.Text,
        "boolean" => PivotItemKind.Boolean,
        "error" => PivotItemKind.Error,
        "blank" => PivotItemKind.Blank,
        _ => throw Unknown("kind", text),
    };
}
