using System.Text.Json;
using ExPivot.Engine;

namespace ExGrid.Script;

/// <summary>
/// A C# <see cref="PivotSource"/> asked by a Script Core pivot through .NET (ADR-0370 sections 1 and 3), .NET's half:
/// what the browser is told of the source, its fields and what it answers, and each question it asks answered as the
/// <see cref="PivotJson"/> both engines read and write. The engine in the browser builds the report and its totals from
/// the Leaf Aggregates the source answers, as it does from a server's. Compiled into each adapter that hands the
/// browser a C# Pivot Source, ExPivot's and ExBoard's, so both describe and answer one alike; the browser's half is
/// ExPivot's <c>DotNetPivotSource</c> (<c>src/npm/pivot-blazor/src/dotnet-pivot-source.ts</c>).
/// </summary>
internal static class PivotSourceCrossing
{
    /// <summary>Writes the members the browser declares <paramref name="source"/> by, after the number it asks it under:
    /// its fields and what it answers. A field this adapter cannot carry is refused by name.</summary>
    public static void WriteMembers(Utf8JsonWriter json, PivotSource source)
    {
        json.WriteStartArray("fields");
        foreach (var field in source.Fields)
            WriteField(json, field);
        json.WriteEndArray();
        var features = source.Features;
        json.WriteStartArray("aggregations");
        foreach (var aggregation in features.Aggregations)
            json.WriteStringValue(Camel(aggregation));
        json.WriteEndArray();
        json.WriteBoolean("canRefresh", features.CanRefresh);
        json.WriteBoolean("countsItems", features.CountsItems);
        json.WriteBoolean("answersLevels", features.AnswersLevels);
    }

    /// <summary>The answer of <paramref name="source"/> to a question of the browser's, of the kind
    /// <paramref name="kind"/> (<c>aggregate</c>, <c>items</c>, <c>details</c> or <c>itemCounts</c>), as PivotJson.</summary>
    public static async Task<string> AnswerAsync(PivotSource source, string kind, string? query, CancellationToken token) => kind switch
    {
        "aggregate" => PivotJson.Write(await source.AggregateAsync(PivotJson.ReadQuery(Must(query)), token)),
        "items" => PivotJson.Write(await source.ItemsAsync(PivotJson.ReadItemsQuery(Must(query)), token)),
        "details" => PivotJson.Write(await source.DetailsAsync(PivotJson.ReadDetailsQuery(Must(query)), token)),
        "itemCounts" => PivotJson.Write(await source.ItemCountsAsync(token)),
        _ => throw new InvalidOperationException($"Internal: the browser asked a question of the kind '{kind}'."),
    };

    /// <summary>An enum's member as the Script Core spells it: its name, its first letter lower-case.</summary>
    public static string Camel<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{value} is not a {typeof(TEnum).Name}.");
        var name = value.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    private static string Must(string? query) => query ?? throw new InvalidOperationException("Internal: the browser asked without a question.");

    private static void WriteField(Utf8JsonWriter json, PivotField field)
    {
        if (field.OrderKey is not null)
        {
            throw new NotSupportedException(
                $"Pivot Field '{field.Name}' has an Order Key, a .NET function the pivot in the browser cannot call (ADR-0370 section 1). "
                + "Declare its Items' order with ItemOrder instead.");
        }
        json.WriteStartObject();
        json.WriteString("name", field.Name);
        json.WriteString("type", Camel(field.Type));
        json.WriteString("caption", field.Caption);
        if (field.Format is { } format)
            json.WriteString("format", format);
        else
            json.WriteNull("format");
        json.WriteStartArray("itemOrder");
        foreach (var item in field.ItemOrder)
        {
            switch (item)
            {
                case string text: json.WriteStringValue(text); break;
                case bool flag: json.WriteBooleanValue(flag); break;
                case int whole: json.WriteNumberValue(whole); break;
                case long whole: json.WriteNumberValue(whole); break;
                case double number when double.IsFinite(number): json.WriteNumberValue(number); break;
                case double number:
                    throw new NotSupportedException(
                        $"Pivot Field '{field.Name}' declares the Item {number}, which no Item of a field can be: an Item is a finite number.");
                case decimal number: json.WriteNumberValue(number); break;
                default:
                    throw new NotSupportedException(
                        $"Pivot Field '{field.Name}' declares an Item of the type {item.GetType().Name}, which the pivot in the browser does not "
                        + "read yet: declared Items are text, numbers or Booleans (ADR-0370 section 1).");
            }
        }
        json.WriteEndArray();
        if (field.Column is { } column)
            json.WriteString("column", column);
        else
            json.WriteNull("column");
        if (field.DatePart is { } part)
            json.WriteString("datePart", Camel(part));
        else
            json.WriteNull("datePart");
        json.WriteEndObject();
    }
}
