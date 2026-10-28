using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExSheet.Engine;

/// <summary>
/// What crosses between .NET and the Script Core's Sheet, for an adapter over it (ADR-0370 section 1,
/// ADR-0201): a Sheet Edit, a Cell Format change and a Linked Table's rows written as JSON the Script Core
/// reads, and a Cell Format and a refused snapshot read back from the JSON it writes. Each is written in the
/// vocabulary the shared cases of both engines write edits, changes and Values in (<c>tests/cases/sheet</c>),
/// so that what an edit means is held to one text on both sides. Nothing here decides anything: the Script
/// Core does the edit, and refuses it as the C# Sheet would.
/// </summary>
public static class SheetWire
{
    /// <summary>
    /// <paramref name="edit"/> as JSON: an object of one key, the factory that made it, and its arguments as the
    /// factory took them, so that the Script Core makes the same edit and checks it as the C# Sheet would.
    /// </summary>
    public static string Write(SheetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return Text(json =>
        {
            json.WriteStartObject();
            edit.WriteTo(json);
            json.WriteEndObject();
        });
    }

    /// <summary><paramref name="change"/> as JSON: each part it sets, by the Script Core's name, and none it leaves as each cell has it.</summary>
    public static string Write(CellFormatChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return Text(json => WriteChange(json, change));
    }

    /// <summary>
    /// A Linked Table's snapshot as JSON: rows of Values, a blank as <c>null</c>, as the Script Core's
    /// <c>pushLinkedTable</c> takes them. A row's length, and whether each Value is data, the Script Core checks.
    /// </summary>
    public static string WriteRows(IEnumerable<IReadOnlyList<Value?>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return Text(json =>
        {
            json.WriteStartArray();
            foreach (var row in rows)
            {
                ArgumentNullException.ThrowIfNull(row, nameof(rows));
                json.WriteStartArray();
                foreach (var value in row) WriteValue(json, value);
                json.WriteEndArray();
            }
            json.WriteEndArray();
        });
    }

    /// <summary>
    /// A Linked Table's snapshot read from the JSON <see cref="WriteRows"/> writes and the Script Core's
    /// <c>writeLinkedRows</c> writes: rows of Values, a blank as <c>null</c>, and a refusal a Consumer read from a
    /// refused cell as one, so that pushed again it is refused where it was.
    /// </summary>
    /// <exception cref="FormatException">The JSON is not rows of Values so written.</exception>
    public static IReadOnlyList<IReadOnlyList<Value?>> ReadRows(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = Parse(json);
        return [.. Array(document.RootElement).Select(row => (IReadOnlyList<Value?>)[.. Array(row).Select(ValueOf)])];
    }

    /// <summary>
    /// A Cell Format read from the JSON the Script Core writes for one: its <c>numberFormat</c> code, its
    /// <c>alignment</c>, its whole <c>font</c>, its <c>fill</c> and its four <c>borders</c>, each written.
    /// </summary>
    /// <exception cref="FormatException">The JSON is not a Cell Format so written.</exception>
    public static CellFormat ReadCellFormat(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = Parse(json);
        return CellFormatOf(document.RootElement);
    }

    /// <summary>
    /// A refused snapshot read from the JSON the Script Core writes for one (ADR-0049): the table, its key's
    /// columns, the key that repeats as its first and its second row hold it, the two rows, and what the
    /// table's going back to waiting changed on the Workbook's first Sheet.
    /// </summary>
    /// <exception cref="FormatException">The JSON is not a refused snapshot so written.</exception>
    public static RepeatedKeyException ReadRepeatedKey(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = Parse(json);
        var root = Object(document.RootElement, "a refused snapshot");
        var change = Object(Member(root, "change"), "a change");
        return new RepeatedKeyException(
            String(Member(root, "table")),
            [.. Array(Member(root, "keyColumns")).Select(String)],
            [.. Array(Member(root, "keys")).Select(DataOf)],
            Int(Member(root, "firstRow")),
            [.. Array(Member(root, "repeats")).Select(DataOf)],
            Int(Member(root, "secondRow")),
            new SheetChange(
                [.. Array(Member(change, "valueChanges")).Select(a => CellAddress.Parse(String(a)))],
                [.. Array(Member(change, "recalculated")).Select(a => CellAddress.Parse(String(a)))],
                [.. Array(Member(change, "rows")).Select(Int)],
                [.. Array(Member(change, "columns")).Select(Int)],
                Member(change, "reformatsUnnamedRows").ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new FormatException("'reformatsUnnamedRows' is a Boolean."),
                }));
    }

    internal static void WriteAddresses(Utf8JsonWriter json, string name, IEnumerable<CellAddress> addresses)
    {
        json.WriteStartArray(name);
        foreach (var address in addresses) json.WriteStringValue(address.ToString());
        json.WriteEndArray();
    }

    internal static void WriteRanges(Utf8JsonWriter json, string name, IEnumerable<CellRange> ranges)
    {
        json.WriteStartArray(name);
        foreach (var range in ranges) json.WriteStringValue(range.ToString());
        json.WriteEndArray();
    }

    // A range written with its colon even when it is one cell, so that it is never read back as an address.
    internal static string RangeText(CellRange range) => $"{range.First}:{range.Last}";

    internal static void WriteTyped(Utf8JsonWriter json, string name, IEnumerable<KeyValuePair<CellAddress, string>> typed)
    {
        json.WriteStartObject(name);
        foreach (var (address, text) in typed) json.WriteString(address.ToString(), text);
        json.WriteEndObject();
    }

    internal static void WriteEntries(Utf8JsonWriter json, string name, IEnumerable<KeyValuePair<CellAddress, Entry?>> entries)
    {
        json.WriteStartObject(name);
        foreach (var (address, entry) in entries)
        {
            json.WritePropertyName(address.ToString());
            WriteEntry(json, entry);
        }
        json.WriteEndObject();
    }

    internal static void WritePlaceAndCount(Utf8JsonWriter json, string name, int place, int count)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(place);
        json.WriteNumberValue(count);
        json.WriteEndArray();
    }

    internal static void WriteChange(Utf8JsonWriter json, CellFormatChange change)
    {
        json.WriteStartObject();
        if (change.NumberFormat is { } format) json.WriteString("numberFormat", format.Code);
        if (change.Alignment is { } alignment) json.WriteString("alignment", Camel(alignment));
        if (change.FontColour is { } colour) json.WriteString("fontColour", colour.ToString());
        if (change.Bold is { } bold) json.WriteBoolean("bold", bold);
        if (change.Italic is { } italic) json.WriteBoolean("italic", italic);
        if (change.Underline is { } underline) json.WriteBoolean("underline", underline);
        if (change.Strikethrough is { } strikethrough) json.WriteBoolean("strikethrough", strikethrough);
        if (change.Fill is { } fill) json.WriteString("fill", FillText(fill));
        if (change.Borders is { } borders)
        {
            json.WriteStartObject("borders");
            WriteEdge(json, "top", borders.Top);
            WriteEdge(json, "bottom", borders.Bottom);
            WriteEdge(json, "left", borders.Left);
            WriteEdge(json, "right", borders.Right);
            WriteEdge(json, "insideHorizontal", borders.InsideHorizontal);
            WriteEdge(json, "insideVertical", borders.InsideVertical);
            json.WriteEndObject();
        }
        json.WriteEndObject();
    }

    /// <summary>A block copied inside ExSheet: the range it came from, and each cell as copied, rows of columns.</summary>
    internal static void WriteBlock(Utf8JsonWriter json, string name, SheetBlock block)
    {
        json.WriteStartObject(name);
        json.WriteString("source", RangeText(block.Source));
        json.WriteStartArray("cells");
        for (var row = 0; row < block.RowCount; row++)
        {
            json.WriteStartArray();
            for (var column = 0; column < block.ColumnCount; column++)
            {
                var state = block.At(row, column);
                json.WriteStartObject();
                if (state.Entry is { } entry)
                {
                    json.WritePropertyName("entry");
                    WriteEntry(json, entry);
                }
                if (state.NumberFormat is { } format) json.WriteString("numberFormat", format.Code);
                if (state.Alignment is { } alignment) json.WriteString("alignment", Camel(alignment));
                if (state.Font is { } font)
                {
                    json.WriteStartObject("font");
                    json.WriteString("colour", font.Colour.ToString());
                    json.WriteBoolean("bold", font.Bold);
                    json.WriteBoolean("italic", font.Italic);
                    json.WriteBoolean("underline", font.Underline);
                    json.WriteBoolean("strikethrough", font.Strikethrough);
                    json.WriteEndObject();
                }
                if (state.Fill is { } fill) json.WriteString("fill", FillText(fill));
                if (state.Borders is { } borders)
                {
                    json.WriteStartObject("borders");
                    WriteEdge(json, "top", borders.Top);
                    WriteEdge(json, "bottom", borders.Bottom);
                    WriteEdge(json, "left", borders.Left);
                    WriteEdge(json, "right", borders.Right);
                    json.WriteEndObject();
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    internal static string Camel<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static void WriteEntry(Utf8JsonWriter json, Entry? entry)
    {
        if (entry is null)
        {
            json.WriteNullValue();
            return;
        }
        json.WriteStartObject();
        if (entry.Formula is { } formula)
        {
            json.WriteString("formula", formula);
        }
        else
        {
            json.WritePropertyName("constant");
            WriteValue(json, entry.Constant);
        }
        json.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter json, Value? value)
    {
        if (value is not { } v)
        {
            json.WriteNullValue();
            return;
        }
        switch (v.Kind)
        {
            case ValueKind.Number:
                json.WriteNumberValue(v.Number);
                break;
            case ValueKind.Text:
                json.WriteStringValue(v.Text);
                break;
            case ValueKind.Boolean:
                json.WriteBooleanValue(v.Boolean);
                break;
            case ValueKind.Error:
                json.WriteStartObject();
                json.WriteString("error", v.Error.ToText());
                // A refusal crosses as one, so that the Script Core refuses it where the C# Sheet would.
                if (v.IsRefused) json.WriteBoolean("refused", true);
                json.WriteEndObject();
                break;
            default:
                throw new InvalidOperationException($"{v.Kind} is no kind of Value.");
        }
    }

    private static void WriteEdge(Utf8JsonWriter json, string name, BorderLine? line)
    {
        if (line is not { } l) return;
        if (l.IsNone)
        {
            json.WriteString(name, "none");
            return;
        }
        json.WriteStartObject(name);
        json.WriteString("style", Camel(l.Style));
        json.WriteString("colour", l.Colour.ToString());
        json.WriteEndObject();
    }

    private static string FillText(CellFill fill) => fill.Colour is { } colour ? colour.ToString() : "none";

    private static string Text(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer)) write(json);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Not JSON: {e.Message}", e);
        }
    }

    private static CellFormat CellFormatOf(JsonElement element)
    {
        var format = Object(element, "a Cell Format");
        var font = Object(Member(format, "font"), "a Font");
        var borders = Object(Member(format, "borders"), "the Borders");
        return new CellFormat(
            NumberFormat.Parse(String(Member(format, "numberFormat"))),
            EnumOf<HorizontalAlignment>(Member(format, "alignment")),
            new CellFont(
                ColourOf(Member(font, "colour")),
                Bool(Member(font, "bold")),
                Bool(Member(font, "italic")),
                Bool(Member(font, "underline")),
                Bool(Member(font, "strikethrough"))),
            String(Member(format, "fill")) is var fill && fill == "none" ? CellFill.None : CellFill.Solid(ColourOf(Member(format, "fill"))),
            new CellBorders(
                LineOf(Member(borders, "top")),
                LineOf(Member(borders, "bottom")),
                LineOf(Member(borders, "left")),
                LineOf(Member(borders, "right"))));
    }

    private static BorderLine LineOf(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && element.GetString() == "none") return BorderLine.None;
        var line = Object(element, "a line");
        return new BorderLine(EnumOf<BorderLineStyle>(Member(line, "style")), ColourOf(Member(line, "colour")));
    }

    private static CellColour ColourOf(JsonElement element)
    {
        var text = String(element);
        return text == "Automatic" ? CellColour.Automatic
            : CellColour.FromHex(text) ?? throw new FormatException($"'{text}' is no colour: Automatic, or #RRGGBB.");
    }

    // A Value as WriteValue writes one: a blank as null, and a refusal as one.
    private static Value? ValueOf(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Count() == 2
            && element.TryGetProperty("refused", out var refused) && refused.ValueKind == JsonValueKind.True
            && element.TryGetProperty("error", out var error))
        {
            return String(error) switch
            {
                "#VALUE!" => Value.Refusal,
                "#NAME?" => Value.RefusedName,
                _ => throw new FormatException($"{element.GetRawText()} is no refusal: #VALUE! or #NAME?."),
            };
        }
        // The states of a computation, which a snapshot never holds as data: read, so that the engine refuses them.
        if (element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Count() == 1
            && element.TryGetProperty("error", out var state) && state.ValueKind == JsonValueKind.String
            && state.GetString() is "#GETTING_DATA" or "#CIRC!")
        {
            return Value.FromError(String(state) == "#CIRC!" ? ErrorValue.Circ : ErrorValue.GettingData);
        }
        return DataOf(element);
    }

    // A Value of a key, which is data: a refusal is never one.
    private static Value DataOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => Value.FromNumber(element.GetDouble()),
        JsonValueKind.String => Value.FromText(element.GetString()!),
        JsonValueKind.True => Value.FromBoolean(true),
        JsonValueKind.False => Value.FromBoolean(false),
        JsonValueKind.Object when element.EnumerateObject().Count() == 1 && element.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String && ErrorValues.TryParseWritten(error.GetString(), out var code) => Value.FromError(code),
        _ => throw new FormatException($"{element.GetRawText()} is no Value of data."),
    };

    private static T EnumOf<T>(JsonElement element) where T : struct, Enum
    {
        var text = String(element);
        return text.Length > 0 && char.IsLower(text[0]) && Enum.TryParse<T>(text, ignoreCase: true, out var value) && Camel(value) == text
            ? value
            : throw new FormatException($"'{text}' is no {typeof(T).Name}.");
    }

    private static JsonElement Object(JsonElement element, string what) =>
        element.ValueKind == JsonValueKind.Object ? element : throw new FormatException($"{element.GetRawText()} is no {what}.");

    private static JsonElement Member(JsonElement element, string name) =>
        element.TryGetProperty(name, out var member) ? member : throw new FormatException($"'{name}' is missing from {element.GetRawText()}.");

    private static JsonElement.ArrayEnumerator Array(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : throw new FormatException($"{element.GetRawText()} is no list.");

    private static string String(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? element.GetString()! : throw new FormatException($"{element.GetRawText()} is no text.");

    private static bool Bool(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new FormatException($"{element.GetRawText()} is no Boolean."),
    };

    private static int Int(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number) ? number : throw new FormatException($"{element.GetRawText()} is no whole number.");
}
