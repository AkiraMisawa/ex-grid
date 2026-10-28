using System.Globalization;
using System.Numerics;
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
    /// <paramref name="kind"/> (<c>aggregate</c>, <c>items</c>, <c>details</c> or <c>itemCounts</c>), as PivotJson. An
    /// answer that names Items of a field with an Order Key comes after a line of their keys (ADR-0412):
    /// <c>{"orderKeys":[…]}</c>, a line break, and the answer. Neither holds a line break of its own, since both are
    /// written without indentation and a string's line break is escaped.</summary>
    public static async Task<string> AnswerAsync(PivotSource source, string kind, string? query, CancellationToken token)
    {
        switch (kind)
        {
            case "aggregate":
                var answer = await source.AggregateAsync(PivotJson.ReadQuery(Must(query)), token);
                return Keyed(source, PivotJson.Write(answer),
                    answer.IsRefused ? [] : [.. answer.Rows.Concat(answer.Columns).Select(axis => (axis.Field, axis.Items))]);
            case "items":
                var asked = PivotJson.ReadItemsQuery(Must(query));
                var page = await source.ItemsAsync(asked, token);
                return Keyed(source, PivotJson.Write(page), page.IsRefused ? [] : [(asked.Field, page.Items)]);
            case "details":
                return PivotJson.Write(await source.DetailsAsync(PivotJson.ReadDetailsQuery(Must(query)), token));
            case "itemCounts":
                return PivotJson.Write(await source.ItemCountsAsync(token));
            default:
                throw new InvalidOperationException($"Internal: the browser asked a question of the kind '{kind}'.");
        }
    }

    /// <summary>A question the source failed to answer, as the browser reads it in place of an answer:
    /// <c>{"failed":"…"}</c>, the failure's message, which the browser throws as the question's error.</summary>
    public static string Failed(string message)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("failed", message);
            json.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    // The answer as it is where it names no Item of a field with an Order Key; otherwise after the line of those Items'
    // keys, which the browser orders them by in place of the .NET function it cannot call (ADR-0412).
    private static string Keyed(PivotSource source, string answer, IEnumerable<(string Field, IReadOnlyList<PivotItemKey> Items)> named)
    {
        var keyed = named
            .Select(each => (each.Items, Field: source.Fields.FirstOrDefault(field => field.Name == each.Field && field.OrderKey is not null)))
            .Where(each => each.Field is not null)
            .ToList();
        if (keyed.Count == 0)
            return answer;
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartArray("orderKeys");
            foreach (var (items, field) in keyed)
            {
                json.WriteStartObject();
                json.WriteString("field", field!.Name);
                json.WriteStartArray("items");
                Type? first = null;
                foreach (var item in items)
                {
                    if (ValueOf(item) is not { } value)
                        continue;
                    json.WriteStartArray();
                    json.WriteStartObject();
                    json.WriteString("kind", Camel(item.Kind));
                    json.WriteString("value", item.Value);
                    json.WriteEndObject();
                    WriteOrderKey(json, field, item, value, field.OrderKey!, ref first);
                    json.WriteEndArray();
                }
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return string.Concat(System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan), "\n", answer);
    }

    // The value .NET's engine hands an Order Key for an Item it is answered, as its ItemRef holds it: a text Item's
    // text, a number's double, a date's DateTime, a Boolean; none for a Blank or #NUM!, which no key orders.
    private static object? ValueOf(PivotItemKey item) => item.Kind switch
    {
        PivotItemKind.Text => item.Value,
        PivotItemKind.Number => double.Parse(item.Value!, NumberStyles.Float, CultureInfo.InvariantCulture),
        PivotItemKind.Date => DateTime.ParseExact(item.Value!, ItemDateFormat, CultureInfo.InvariantCulture),
        PivotItemKind.Boolean => item.Value == "TRUE",
        _ => null,
    };

    // PivotItemKey's invariant date text, which its own reader keeps internal.
    private const string ItemDateFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";

    // An Item's key as the browser compares it, exactly as .NET's CompareTo orders keys of its type: text as text, a
    // floating number as its round-trip text, and every other ordered type as a whole number in its order. A key the
    // function failed to give, or one of a type the browser cannot order as .NET does, is written as a failure, which
    // the browser raises only if the report orders by it, as .NET's engine would.
    private static void WriteOrderKey(Utf8JsonWriter json, PivotField field, PivotItemKey item, object value, Func<object, IComparable?> orderKey,
        ref Type? first)
    {
        IComparable? key;
        try
        {
            key = orderKey(value);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            json.WriteStartObject();
            json.WriteString("threw", e.Message);
            json.WriteEndObject();
            return;
        }
        if (key is null)
        {
            json.WriteNullValue();
            return;
        }
        var type = key.GetType();
        var whole = key is string or bool or double or float or Half ? null : WholeOf(key);
        if (key is not (string or bool or double or float or Half) && whole is null)
        {
            Refused(json, $"The Order Key of {field.Caption} gave '{item.Value}' a key of type {type.Name}, which the pivot in the browser "
                + "cannot order as .NET does (ADR-0412): give a text, a number, a Boolean, a date or a time, a Guid or an enum.");
            return;
        }
        if (first is null)
        {
            first = type;
        }
        else if (type != first)
        {
            Refused(json, $"The Order Key of {field.Caption} gave '{item.Value}' a key of type {type.Name} after keys of type {first.Name}; "
                + "a field's keys are of one type.");
            return;
        }
        json.WriteStartObject();
        json.WriteString("type", type.FullName);
        switch (key)
        {
            case string text: json.WriteString("text", text); break;
            case bool flag: json.WriteBoolean("boolean", flag); break;
            case double number: json.WriteString("number", RoundTrip(number)); break;
            case float number: json.WriteString("number", RoundTrip(number)); break;
            case Half number: json.WriteString("number", RoundTrip((double)number)); break;
            default: json.WriteString("bigint", whole!.Value.ToString(CultureInfo.InvariantCulture)); break;
        }
        json.WriteEndObject();
    }

    private static string RoundTrip(double number) => number.ToString("R", CultureInfo.InvariantCulture);

    private static void Refused(Utf8JsonWriter json, string message)
    {
        json.WriteStartObject();
        json.WriteString("refused", message);
        json.WriteEndObject();
    }

    // A key of an ordered type as a whole number that orders as its CompareTo does: an integer as itself, a decimal
    // at the largest scale a decimal has, a moment, a day or a span by its ticks, a Guid by the bytes it compares in
    // order, an enum by its underlying value. Null for any other type.
    private static BigInteger? WholeOf(object key) => key switch
    {
        sbyte or byte or short or ushort or int or uint or long or ulong or char
            => new BigInteger(Convert.ToDecimal(key is char c ? (int)c : key, CultureInfo.InvariantCulture)),
        Int128 whole => (BigInteger)whole,
        UInt128 whole => (BigInteger)whole,
        BigInteger whole => whole,
        decimal number => Scaled(number),
        DateTime moment => moment.Ticks,
        DateTimeOffset moment => moment.UtcTicks,
        DateOnly day => day.DayNumber,
        TimeOnly time => time.Ticks,
        TimeSpan span => span.Ticks,
        Guid guid => new BigInteger(guid.ToByteArray(bigEndian: true), isUnsigned: true, isBigEndian: true),
        Enum value => new BigInteger(Convert.ToDecimal(value, CultureInfo.InvariantCulture)),
        _ => null,
    };

    // A decimal as the whole number of its 10^-28ths, the finest step a decimal has: every decimal is one, exactly.
    private static BigInteger Scaled(decimal number)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(number, bits);
        var mantissa = new BigInteger((uint)bits[0]) | (new BigInteger((uint)bits[1]) << 32) | (new BigInteger((uint)bits[2]) << 64);
        var scale = (bits[3] >> 16) & 0xFF;
        var scaled = mantissa * BigInteger.Pow(10, 28 - scale);
        return bits[3] < 0 ? -scaled : scaled;
    }

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
        json.WriteStartObject();
        json.WriteString("name", field.Name);
        json.WriteString("type", Camel(field.Type));
        json.WriteString("caption", field.Caption);
        // An Order Key is a .NET function: what crosses is that the field has one, and its keys with each answer.
        json.WriteBoolean("orderKey", field.OrderKey is not null);
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
