using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExPivot.Engine;

/// <summary>
/// Every Pivot Source question and answer as JSON (ADR-0065), so it crosses to a server unchanged:
/// <see cref="PivotQuery"/> and <see cref="PivotAnswer"/>, <see cref="PivotItemsQuery"/> and
/// <see cref="PivotItemPage"/>, <see cref="PivotDetailsQuery"/> and <see cref="PivotDetailPage"/>,
/// and <see cref="PivotSourceChanged"/>. Each document names its format's <c>version</c> and its
/// <c>type</c>, and a reader refuses a version it does not know rather than half-read it.
///
/// <para>Written and read by hand, member by member, as <see cref="PivotLayoutJson"/> is, so that
/// nothing depends on reflection a trimmed WebAssembly build may have removed. What must survive
/// the trip does: a <c>decimal</c> is written as its exact digits; a <c>double</c> in its shortest
/// round-trip form, and a non-finite one as the named literal <c>"NaN"</c>, <c>"Infinity"</c> or
/// <c>"-Infinity"</c>, as System.Text.Json's <c>AllowNamedFloatingPointLiterals</c> writes it. A
/// part that is exact or not — a sum, an extreme — is a JSON number when it is an exact
/// <c>decimal</c> and a JSON string when it is a <c>double</c>. An answer's leaves are written
/// column by column: one array per level and per part, never an object per leaf.</para>
/// </summary>
public static class PivotJson
{
    /// <summary>The format version this writer writes and the only one this reader reads.</summary>
    public const int Version = 1;

    private const string QueryType = "query";
    private const string AnswerType = "answer";
    private const string ItemsQueryType = "itemsQuery";
    private const string ItemPageType = "itemPage";
    private const string DetailsQueryType = "detailsQuery";
    private const string DetailPageType = "detailPage";
    private const string SourceChangedType = "sourceChanged";

    // ---- Writing -------------------------------------------------------------------------------

    /// <summary>A question for the Leaf Aggregates, as JSON.</summary>
    public static string Write(PivotQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Document(QueryType, json =>
        {
            WriteFields(json, "rows", query.Rows);
            WriteFields(json, "columns", query.Columns);
            WriteFields(json, "filters", query.Filters);
            json.WriteStartArray("values");
            foreach (var value in query.Values)
            {
                json.WriteStartObject();
                json.WriteString("field", value.Field);
                WriteParts(json, value.Parts);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteNumber("maxLeaves", query.MaxLeaves);
        });
    }

    /// <summary>The Leaf Aggregates, or a refusal, as JSON: the leaves column by column.</summary>
    public static string Write(PivotAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return Document(AnswerType, json =>
        {
            if (answer.Refusal is { } refusal)
            {
                WriteRefusal(json, refusal);
                return;
            }
            var leaves = answer.LeafCount;
            json.WriteString("sourceVersion", answer.SourceVersion);
            WriteAxes(json, "rows", answer.RowAxes, leaves);
            WriteAxes(json, "columns", answer.ColumnAxes, leaves);
            json.WriteStartArray("records");
            for (var leaf = 0; leaf < leaves; leaf++)
                json.WriteNumberValue(answer.Records[leaf]);
            json.WriteEndArray();
            json.WriteStartArray("values");
            foreach (var values in answer.ValueColumns)
                WriteValues(json, values, leaves);
            json.WriteEndArray();
        });
    }

    /// <summary>A question for a field's Items, as JSON.</summary>
    public static string Write(PivotItemsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Document(ItemsQueryType, json =>
        {
            json.WriteString("field", query.Field);
            json.WriteString("sourceVersion", query.SourceVersion);
            if (query.Search is not null)
                json.WriteString("search", query.Search);
            json.WriteNumber("max", query.Max);
        });
    }

    /// <summary>A page of Items, or a refusal, as JSON.</summary>
    public static string Write(PivotItemPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Document(ItemPageType, json =>
        {
            if (page.Refusal is { } refusal)
            {
                WriteRefusal(json, refusal);
                return;
            }
            json.WriteString("sourceVersion", page.SourceVersion);
            WriteKeys(json, "items", page.Items);
            json.WriteNumber("total", page.Total);
        });
    }

    /// <summary>A question for the records behind a cell, as JSON.</summary>
    public static string Write(PivotDetailsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Document(DetailsQueryType, json =>
        {
            json.WriteString("sourceVersion", query.SourceVersion);
            WritePath(json, "rowItems", query.RowItems);
            WritePath(json, "columnItems", query.ColumnItems);
            WriteFields(json, "hiddenItems", query.HiddenItems);
            json.WriteNumber("start", query.Start);
            json.WriteNumber("count", query.Count);
        });
    }

    /// <summary>A page of the records behind a cell, or a refusal, as JSON. Each value is written by
    /// its field's declared type; the Consumer's own record objects are not written.</summary>
    public static string Write(PivotDetailPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Document(DetailPageType, json =>
        {
            if (page.Refusal is { } refusal)
            {
                WriteRefusal(json, refusal);
                return;
            }
            json.WriteString("sourceVersion", page.SourceVersion);
            json.WriteStartArray("fields");
            foreach (var field in page.Fields)
            {
                json.WriteStartObject();
                json.WriteString("name", field.Name);
                json.WriteString("type", Name(field.Type));
                if (field.Caption != field.Name)
                    json.WriteString("caption", field.Caption);
                if (field.Format is not null)
                    json.WriteString("format", field.Format);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteNumber("start", page.Start);
            json.WriteNumber("total", page.Total);
            json.WriteStartArray("records");
            foreach (var record in page.Records)
            {
                json.WriteStartArray();
                for (var f = 0; f < page.Fields.Count; f++)
                    WriteDetailValue(json, page.Fields[f].Type, record.Values[f]);
                json.WriteEndArray();
            }
            json.WriteEndArray();
        });
    }

    /// <summary>A source's notice that its data moved on, as JSON.</summary>
    public static string Write(PivotSourceChanged change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return Document(SourceChangedType, json =>
        {
            if (change.SourceVersion is not null)
                json.WriteString("sourceVersion", change.SourceVersion);
        });
    }

    private static string Document(string type, Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("version", Version);
            json.WriteString("type", type);
            write(json);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static void WriteRefusal(Utf8JsonWriter json, PivotSourceRefusal refusal)
    {
        json.WriteStartObject("refusal");
        json.WriteString("kind", Name(refusal.Kind));
        json.WriteString("message", refusal.Message);
        if (refusal.Field is not null)
            json.WriteString("field", refusal.Field);
        if (refusal.Limit is { } limit)
            json.WriteNumber("limit", limit);
        json.WriteEndObject();
    }

    private static void WriteFields(Utf8JsonWriter json, string name, IReadOnlyList<PivotQueryField> fields)
    {
        json.WriteStartArray(name);
        foreach (var field in fields)
        {
            json.WriteStartObject();
            json.WriteString("field", field.Field);
            if (field.HiddenItems.Count > 0)
                WriteKeys(json, "hiddenItems", field.HiddenItems);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WritePath(Utf8JsonWriter json, string name, IReadOnlyList<PivotFieldItem> path)
    {
        json.WriteStartArray(name);
        foreach (var step in path)
        {
            json.WriteStartObject();
            json.WriteString("field", step.Field);
            json.WritePropertyName("item");
            WriteKey(json, step.Item);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteKeys(Utf8JsonWriter json, string name, IReadOnlyList<PivotItemKey> keys)
    {
        json.WriteStartArray(name);
        foreach (var key in keys)
            WriteKey(json, key);
        json.WriteEndArray();
    }

    // An Item as a Pivot Layout writes it (ADR-0059): its kind and its invariant text.
    private static void WriteKey(Utf8JsonWriter json, PivotItemKey key)
    {
        json.WriteStartObject();
        json.WriteString("kind", PivotLayoutJson.Name(key.Kind));
        if (key.Value is not null)
            json.WriteString("value", key.Value);
        json.WriteEndObject();
    }

    private static void WriteParts(Utf8JsonWriter json, PivotParts parts)
    {
        json.WriteStartArray("parts");
        foreach (var (part, name) in PartNames)
        {
            if ((parts & part) != 0)
                json.WriteStringValue(name);
        }
        json.WriteEndArray();
    }

    private static void WriteAxes(Utf8JsonWriter json, string name, PivotAnswerAxis[] axes, int leaves)
    {
        json.WriteStartArray(name);
        foreach (var axis in axes)
        {
            json.WriteStartObject();
            json.WriteString("field", axis.Field);
            WriteKeys(json, "items", axis.ItemArray);
            json.WriteStartArray("leaves");
            for (var leaf = 0; leaf < leaves; leaf++)
                json.WriteNumberValue(axis.ItemOfLeaf[leaf]);
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteValues(Utf8JsonWriter json, PivotAnswerValues values, int leaves)
    {
        var columns = values.Columns;
        json.WriteStartObject();
        json.WriteString("field", values.Field);
        WriteParts(json, columns.Parts);
        json.WriteStartArray("values");
        for (var leaf = 0; leaf < leaves; leaf++)
            json.WriteNumberValue(columns.Counts[leaf].Values);
        json.WriteEndArray();
        json.WriteStartArray("numbers");
        for (var leaf = 0; leaf < leaves; leaf++)
            json.WriteNumberValue(columns.Counts[leaf].Numbers);
        json.WriteEndArray();
        // Rare, so written as the leaves that have one.
        json.WriteStartArray("nonFinite");
        for (var leaf = 0; leaf < leaves; leaf++)
        {
            if (columns.Counts[leaf].NonFinite)
                json.WriteNumberValue(leaf);
        }
        json.WriteEndArray();
        if (columns.Sums is { } sums)
        {
            json.WriteStartArray("sum");
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                var sum = sums[leaf];
                if (sum.Inexact)
                    json.WriteStringValue(DoubleText(sum.Double + sum.Compensation));
                else
                    json.WriteNumberValue(sum.Exact);
            }
            json.WriteEndArray();
        }
        if (columns.Extremes is { } extremes)
        {
            json.WriteStartArray("min");
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                if (extremes[leaf].Inexact)
                    json.WriteStringValue(DoubleText(extremes[leaf].Min));
                else
                    json.WriteNumberValue(extremes[leaf].ExactMin);
            }
            json.WriteEndArray();
            json.WriteStartArray("max");
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                if (extremes[leaf].Inexact)
                    json.WriteStringValue(DoubleText(extremes[leaf].Max));
                else
                    json.WriteNumberValue(extremes[leaf].ExactMax);
            }
            json.WriteEndArray();
        }
        if (columns.Products is { } products)
            WriteDoubles(json, "product", products, leaves, static (products, leaf) => products[leaf]);
        if (columns.Variances is { } variances)
        {
            WriteDoubles(json, "mean", variances, leaves, static (variances, leaf) => variances[leaf].Mean);
            WriteDoubles(json, "m2", variances, leaves, static (variances, leaf) => variances[leaf].M2);
        }
        json.WriteEndObject();
    }

    private static void WriteDoubles<T>(Utf8JsonWriter json, string name, T[] parts, int leaves, Func<T[], int, double> read)
    {
        json.WriteStartArray(name);
        for (var leaf = 0; leaf < leaves; leaf++)
            WriteDouble(json, read(parts, leaf));
        json.WriteEndArray();
    }

    // A double that is always one: a JSON number, or a named literal when it is not finite.
    private static void WriteDouble(Utf8JsonWriter json, double value)
    {
        if (double.IsFinite(value))
            json.WriteNumberValue(value);
        else
            json.WriteStringValue(DoubleText(value));
    }

    // The shortest text that reads back as the same double; NaN, Infinity and -Infinity by name.
    private static string DoubleText(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    // A detail value by its field's declared type; a value of another type is tagged with its own.
    private static void WriteDetailValue(Utf8JsonWriter json, PivotFieldType type, object? value)
    {
        switch (type, value)
        {
            case (_, null):
                json.WriteNullValue();
                return;
            case (PivotFieldType.Text, string text):
                json.WriteStringValue(text);
                return;
            case (PivotFieldType.Number, decimal number):
                json.WriteNumberValue(number);
                return;
            case (PivotFieldType.Number, double number):
                json.WriteStringValue(DoubleText(number));
                return;
            case (PivotFieldType.Date, DateTime date):
                json.WriteStringValue(DateText(date));
                return;
            case (PivotFieldType.Boolean, bool boolean):
                json.WriteBooleanValue(boolean);
                return;
        }
        json.WriteStartObject();
        switch (value)
        {
            case string text:
                json.WriteString("text", text);
                break;
            case decimal number:
                json.WriteNumber("number", number);
                break;
            case double number:
                json.WriteString("double", DoubleText(number));
                break;
            case DateTime date:
                json.WriteString("date", DateText(date));
                break;
            case bool boolean:
                json.WriteBoolean("boolean", boolean);
                break;
            default:
                throw new InvalidOperationException($"A detail value of type {value.GetType()} cannot be written; a PivotDetailRecord normalises its values.");
        }
        json.WriteEndObject();
    }

    private static string DateText(DateTime date) => date.ToString(PivotItemKey.DateFormat, CultureInfo.InvariantCulture);

    // ---- Reading -------------------------------------------------------------------------------

    /// <summary>A question read back from <see cref="Write(PivotQuery)"/>'s JSON.</summary>
    public static PivotQuery ReadQuery(string json) => Read(json, QueryType, root => new PivotQuery(
        ReadFields(root, "rows"),
        ReadFields(root, "columns"),
        ReadFields(root, "filters"),
        Array(root, "values").Select(value => new PivotQueryValue(String(value, "field"), ReadParts(value))).ToArray(),
        Int(root, "maxLeaves")));

    /// <summary>
    /// An answer read back from <see cref="Write(PivotAnswer)"/>'s JSON. Refuses by name a leaf
    /// array of the wrong length, an Item index out of range, two Items of one field that are one
    /// Item, counts that cannot be — more numbers than values, more values than records — and a
    /// part in a form it cannot be.
    /// </summary>
    public static PivotAnswer ReadAnswer(string json) => Read(json, AnswerType, root =>
    {
        if (ReadRefusal(root) is { } refusal)
            return PivotAnswer.Refused(refusal);
        var sourceVersion = String(root, "sourceVersion");
        var records = Array(root, "records").Select(element => LongValue(element, "records")).ToArray();
        var leaves = records.Length;
        for (var leaf = 0; leaf < leaves; leaf++)
        {
            if (records[leaf] < 1)
                throw new FormatException($"Leaf {leaf} holds {records[leaf]} records; a leaf has records.");
        }
        var rows = ReadAxes(root, "rows", leaves);
        var columns = ReadAxes(root, "columns", leaves);
        var values = Array(root, "values").Select(element => ReadValues(element, records)).ToArray();
        return new PivotAnswer(sourceVersion, rows, columns, leaves, records, values);
    });

    /// <summary>A question for a field's Items read back from <see cref="Write(PivotItemsQuery)"/>'s JSON.</summary>
    public static PivotItemsQuery ReadItemsQuery(string json) => Read(json, ItemsQueryType, root => new PivotItemsQuery(
        String(root, "field"),
        String(root, "sourceVersion"),
        OptionalString(root, "search"),
        Int(root, "max")));

    /// <summary>A page of Items read back from <see cref="Write(PivotItemPage)"/>'s JSON.</summary>
    public static PivotItemPage ReadItemPage(string json) => Read(json, ItemPageType, root =>
        ReadRefusal(root) is { } refusal
            ? PivotItemPage.Refused(refusal)
            : new PivotItemPage(String(root, "sourceVersion"), ReadKeys(root, "items"), Int(root, "total")));

    /// <summary>A question for the records behind a cell read back from
    /// <see cref="Write(PivotDetailsQuery)"/>'s JSON.</summary>
    public static PivotDetailsQuery ReadDetailsQuery(string json) => Read(json, DetailsQueryType, root => new PivotDetailsQuery(
        String(root, "sourceVersion"),
        ReadPath(root, "rowItems"),
        ReadPath(root, "columnItems"),
        ReadFields(root, "hiddenItems"),
        Int(root, "start"),
        Int(root, "count")));

    /// <summary>A page of the records behind a cell read back from
    /// <see cref="Write(PivotDetailPage)"/>'s JSON, each value read by its field's declared type.
    /// The records carry no object of the Consumer's: those never left the process.</summary>
    public static PivotDetailPage ReadDetailPage(string json) => Read(json, DetailPageType, root =>
    {
        if (ReadRefusal(root) is { } refusal)
            return PivotDetailPage.Refused(refusal);
        var fields = Array(root, "fields").Select(field => new PivotField(
            String(field, "name"),
            Parse(field, "type", ParseType),
            OptionalString(field, "caption"),
            OptionalString(field, "format"))).ToArray();
        var records = Array(root, "records").Select(record =>
        {
            if (record.ValueKind != JsonValueKind.Array)
                throw new FormatException("A detail record is an array of values.");
            var values = record.EnumerateArray().ToArray();
            if (values.Length != fields.Length)
                throw new FormatException($"A detail record carries {values.Length} values, and the page has {fields.Length} fields.");
            return new PivotDetailRecord(values.Select((value, f) => ReadDetailValue(value, fields[f])).ToArray());
        }).ToArray();
        return new PivotDetailPage(String(root, "sourceVersion"), fields, Int(root, "start"), Long(root, "total"), records);
    });

    /// <summary>A source's notice read back from <see cref="Write(PivotSourceChanged)"/>'s JSON.</summary>
    public static PivotSourceChanged ReadSourceChanged(string json)
        => Read(json, SourceChangedType, root => new PivotSourceChanged(OptionalString(root, "sourceVersion")));

    /// <summary>
    /// Opens a document: a JSON object naming its version — refused with
    /// <see cref="NotSupportedException"/> when it is not one this reader reads — and its type,
    /// refused with <see cref="FormatException"/> when it is not the one expected.
    /// </summary>
    private static T Read<T>(string json, string type, Func<JsonElement, T> read)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            throw new FormatException("A Pivot Source document is JSON: " + error.Message, error);
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("A Pivot Source document is a JSON object.");
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
                throw new FormatException("A Pivot Source document names its version.");
            if (!version.TryGetInt32(out var number) || number != Version)
                throw new NotSupportedException($"A Pivot Source document of version {version.GetRawText()} cannot be read; this reader reads version {Version}.");
            var actual = OptionalString(root, "type");
            if (actual != type)
                throw new FormatException($"The document is a Pivot Source '{actual ?? "(untyped)"}' document, not a '{type}'.");
            try
            {
                return read(root);
            }
            catch (ArgumentException error)
            {
                // A value the model refuses — a key naming no Item, a count out of range — is a
                // document this reader cannot take.
                throw new FormatException("The document holds a value that cannot be: " + error.Message, error);
            }
        }
    }

    private static PivotSourceRefusal? ReadRefusal(JsonElement root)
    {
        if (!root.TryGetProperty("refusal", out var refusal))
            return null;
        if (refusal.ValueKind != JsonValueKind.Object)
            throw new FormatException("'refusal' is an object.");
        return new PivotSourceRefusal(Parse(refusal, "kind", ParseRefusalKind), String(refusal, "message"))
        {
            Field = OptionalString(refusal, "field"),
            Limit = refusal.TryGetProperty("limit", out var limit) ? LongValue(limit, "limit") : null,
        };
    }

    private static PivotQueryField[] ReadFields(JsonElement element, string name)
        => Array(element, name).Select(field => new PivotQueryField(String(field, "field"), ReadKeys(field, "hiddenItems"))).ToArray();

    private static PivotFieldItem[] ReadPath(JsonElement element, string name)
        => Array(element, name).Select(step => new PivotFieldItem(
            String(step, "field"),
            step.TryGetProperty("item", out var item) ? ReadKey(item) : throw new FormatException("A step of a cell's path names its 'item'."))).ToArray();

    private static PivotItemKey[] ReadKeys(JsonElement element, string name) => Array(element, name).Select(ReadKey).ToArray();

    private static PivotItemKey ReadKey(JsonElement key)
    {
        if (key.ValueKind != JsonValueKind.Object)
            throw new FormatException("An Item is an object with its 'kind' and its 'value'.");
        return new PivotItemKey(Parse(key, "kind", PivotLayoutJson.ParseKind), OptionalString(key, "value"));
    }

    private static PivotParts ReadParts(JsonElement element)
    {
        var parts = PivotParts.Counts;
        foreach (var part in Array(element, "parts"))
        {
            if (part.ValueKind != JsonValueKind.String)
                throw new FormatException("A part is named by a string.");
            var text = part.GetString()!;
            var known = PartNames.FirstOrDefault(p => p.Name == text);
            if (known.Name is null)
                throw PivotLayoutJson.Unknown("part", text);
            parts |= known.Part;
        }
        return parts;
    }

    private static PivotAnswerAxis[] ReadAxes(JsonElement root, string name, int leaves)
        => Array(root, name).Select(axis =>
        {
            var field = String(axis, "field");
            var items = ReadKeys(axis, "items");
            var distinct = new HashSet<PivotItemKey>();
            foreach (var item in items)
            {
                if (!distinct.Add(item))
                    throw new FormatException($"'{field}' lists the Item {item} twice; text Items are told apart ignoring case (ADR-0059).");
            }
            var itemOfLeaf = Column(axis, "leaves", leaves, element => IntValue(element, "leaves"));
            return new PivotAnswerAxis(field, items, itemOfLeaf, leaves);
        }).ToArray();

    private static PivotAnswerValues ReadValues(JsonElement element, long[] records)
    {
        var field = String(element, "field");
        var leaves = records.Length;
        var columns = new PartColumns(ReadParts(element), Math.Max(1, leaves));
        var values = Column(element, "values", leaves, value => LongValue(value, "values"));
        var numbers = Column(element, "numbers", leaves, value => LongValue(value, "numbers"));
        for (var leaf = 0; leaf < leaves; leaf++)
        {
            if (numbers[leaf] < 0 || numbers[leaf] > values[leaf] || values[leaf] > records[leaf])
                throw new FormatException($"Leaf {leaf} of '{field}' holds {records[leaf]} records, {values[leaf]} values and {numbers[leaf]} numbers.");
            columns.Counts[leaf] = new CountsPart { Values = values[leaf], Numbers = numbers[leaf] };
        }
        foreach (var leafElement in Array(element, "nonFinite"))
        {
            var leaf = IntValue(leafElement, "nonFinite");
            if ((uint)leaf >= (uint)leaves)
                throw new FormatException($"'nonFinite' names leaf {leaf}, and the answer has {leaves}.");
            columns.Counts[leaf].NonFinite = true;
        }
        if (columns.Sums is { } sums)
        {
            var read = Column(element, "sum", leaves, ReadExactOrDouble);
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                sums[leaf] = read[leaf].IsExact
                    ? new SumPart { Exact = read[leaf].ExactValue }
                    : new SumPart { Double = read[leaf].Value, Inexact = true };
            }
        }
        if (columns.Extremes is { } extremes)
        {
            var min = Column(element, "min", leaves, ReadExactOrDouble);
            var max = Column(element, "max", leaves, ReadExactOrDouble);
            for (var leaf = 0; leaf < leaves; leaf++)
            {
                if (min[leaf].IsExact != max[leaf].IsExact)
                    throw new FormatException($"Leaf {leaf} of '{field}' has one extreme exact and the other a double.");
                extremes[leaf] = min[leaf].IsExact
                    ? new ExtremesPart { ExactMin = min[leaf].ExactValue, ExactMax = max[leaf].ExactValue }
                    : new ExtremesPart { Min = min[leaf].Value, Max = max[leaf].Value, Inexact = true };
            }
        }
        if (columns.Products is { } products)
            Column(element, "product", leaves, ReadDouble).CopyTo(products, 0);
        if (columns.Variances is { } variances)
        {
            var mean = Column(element, "mean", leaves, ReadDouble);
            var m2 = Column(element, "m2", leaves, ReadDouble);
            for (var leaf = 0; leaf < leaves; leaf++)
                variances[leaf] = new VariancePart { Mean = mean[leaf], M2 = m2[leaf] };
        }
        return new PivotAnswerValues(field, columns, leaves);
    }

    // One array of an answer's columns: exactly one entry per leaf.
    private static T[] Column<T>(JsonElement element, string name, int leaves, Func<JsonElement, T> read)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
            throw new FormatException($"'{name}' is an array, and is required.");
        var length = array.GetArrayLength();
        if (length != leaves)
            throw new FormatException($"'{name}' has {length} entries, and the answer has {leaves} leaves.");
        var column = new T[leaves];
        var leaf = 0;
        foreach (var entry in array.EnumerateArray())
            column[leaf++] = read(entry);
        return column;
    }

    // A part that is exact or not: a JSON number is an exact decimal, a JSON string a double.
    private static PivotNumber ReadExactOrDouble(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetDecimal(out var exact)
            ? PivotNumber.Exact(exact)
            : throw new FormatException($"{element.GetRawText()} is not an exact decimal; a double is written as a string."),
        JsonValueKind.String => PivotNumber.Double(ParseDouble(element.GetString()!)),
        _ => throw new FormatException("An exact part is a number, and a double one a string."),
    };

    // A double: a JSON number, or a named literal.
    private static double ReadDouble(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String => element.GetString() switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            var text => throw new FormatException($"'{text}' is not NaN, Infinity or -Infinity; a finite double is a number."),
        },
        _ => throw new FormatException("A double is a number, or NaN, Infinity or -Infinity."),
    };

    private static double ParseDouble(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException($"'{text}' is not the invariant text of a double.");

    private static object? ReadDetailValue(JsonElement element, PivotField field)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Object:
                return ReadTaggedValue(element);
            case JsonValueKind.String when field.Type == PivotFieldType.Text:
                return element.GetString();
            case JsonValueKind.String when field.Type == PivotFieldType.Number:
                return ParseDouble(element.GetString()!);
            case JsonValueKind.String when field.Type == PivotFieldType.Date:
                return ParseDate(element.GetString()!);
            case JsonValueKind.Number when field.Type == PivotFieldType.Number:
                return element.TryGetDecimal(out var number)
                    ? number
                    : throw new FormatException($"{element.GetRawText()} is not an exact decimal; a double is written as a string.");
            case JsonValueKind.True or JsonValueKind.False when field.Type == PivotFieldType.Boolean:
                return element.GetBoolean();
            default:
                throw new FormatException($"A {element.ValueKind} is not a value of '{field.Name}', declared {field.Type}; a value of another type is tagged with its own.");
        }
    }

    private static object ReadTaggedValue(JsonElement element)
    {
        var properties = element.EnumerateObject().ToArray();
        if (properties.Length != 1)
            throw new FormatException("A tagged detail value has one member: text, number, double, date or boolean.");
        var value = properties[0].Value;
        return properties[0].Name switch
        {
            "text" when value.ValueKind == JsonValueKind.String => value.GetString()!,
            "number" when value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) => number,
            "double" when value.ValueKind == JsonValueKind.String => ParseDouble(value.GetString()!),
            "date" when value.ValueKind == JsonValueKind.String => ParseDate(value.GetString()!),
            "boolean" when value.ValueKind is JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            var tag => throw new FormatException($"'{tag}' with a {value.ValueKind} is not a tagged detail value this reader knows."),
        };
    }

    private static DateTime ParseDate(string text)
        => DateTime.TryParseExact(text, PivotItemKey.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new FormatException($"'{text}' is not an ISO date ({PivotItemKey.DateFormat}).");

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

    // An integer member of an object.
    private static int Int(JsonElement element, string name)
        => element.TryGetProperty(name, out var member)
            ? IntValue(member, name)
            : throw new FormatException($"'{name}' is an integer, and is required.");

    // An integer value: a member's, or an entry of an array named by name.
    private static int IntValue(JsonElement value, string name)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new FormatException($"'{name}' holds integers; {value.GetRawText()} is not one.");

    private static long Long(JsonElement element, string name)
        => element.TryGetProperty(name, out var member)
            ? LongValue(member, name)
            : throw new FormatException($"'{name}' is an integer, and is required.");

    private static long LongValue(JsonElement value, string name)
        => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : throw new FormatException($"'{name}' holds integers; {value.GetRawText()} is not one.");

    private static T Parse<T>(JsonElement element, string name, Func<string, T> parse)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? parse(value.GetString()!)
            : throw new FormatException($"'{name}' is a string, and is required.");

    private static readonly (PivotParts Part, string Name)[] PartNames =
    [
        (PivotParts.Sum, "sum"), (PivotParts.Extremes, "extremes"), (PivotParts.Product, "product"), (PivotParts.Variance, "variance"),
    ];

    private static string Name(PivotFieldType type) => type switch
    {
        PivotFieldType.Text => "text",
        PivotFieldType.Number => "number",
        PivotFieldType.Date => "date",
        PivotFieldType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown PivotFieldType."),
    };

    private static PivotFieldType ParseType(string text) => text switch
    {
        "text" => PivotFieldType.Text,
        "number" => PivotFieldType.Number,
        "date" => PivotFieldType.Date,
        "boolean" => PivotFieldType.Boolean,
        _ => throw PivotLayoutJson.Unknown("type", text),
    };

    private static string Name(PivotSourceRefusalKind kind) => kind switch
    {
        PivotSourceRefusalKind.TooManyLeaves => "tooManyLeaves",
        PivotSourceRefusalKind.UnknownField => "unknownField",
        PivotSourceRefusalKind.AggregationNotOffered => "aggregationNotOffered",
        PivotSourceRefusalKind.SourceVersionNotHeld => "sourceVersionNotHeld",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown PivotSourceRefusalKind."),
    };

    private static PivotSourceRefusalKind ParseRefusalKind(string text) => text switch
    {
        "tooManyLeaves" => PivotSourceRefusalKind.TooManyLeaves,
        "unknownField" => PivotSourceRefusalKind.UnknownField,
        "aggregationNotOffered" => PivotSourceRefusalKind.AggregationNotOffered,
        "sourceVersionNotHeld" => PivotSourceRefusalKind.SourceVersionNotHeld,
        _ => throw PivotLayoutJson.Unknown("refusal kind", text),
    };
}
