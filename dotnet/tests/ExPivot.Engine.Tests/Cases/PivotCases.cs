using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// Reads the engine's shared cases (<c>tests/cases/pivot/</c>; ADR-0370 sections 2 and 5), which Vitest
/// runs against <c>@ex-grid/pivot-engine</c> as well: a data set of <c>tests/cases/pivot/data/</c>, a
/// layout as <see cref="PivotLayoutJson"/> writes one, the report's locale and words, and what the engine
/// answers. <c>tests/cases/README.md</c> says what a file holds.
///
/// <para>With <c>EXGRID_WRITE_PIVOT_CASES=1</c>, a runner writes what the C# engine answers into each case
/// of the source files instead of holding the engine to it: a case's input is transcribed from the fact it
/// holds, and its answer is the C# engine's, which the Script Core is then held to.</para>
/// </summary>
internal static class PivotCases
{
    internal static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "pivot");

    /// <summary>Whether the runners write the engine's answers into the source files.</summary>
    public static bool Writing => Environment.GetEnvironmentVariable("EXGRID_WRITE_PIVOT_CASES") == "1";

    /// <summary>The case of the file named so, and the file it is in.</summary>
    public static (JsonObject File, JsonObject Case) Find(string file, string name)
    {
        var document = Read(Path.Combine(Root, file));
        foreach (var entry in document["cases"]!.AsArray())
        {
            if ((string?)entry!["name"] == name)
                return (document, entry.AsObject());
        }
        throw new InvalidOperationException($"pivot/{file} holds no case named \"{name}\".");
    }

    internal static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static readonly Lock WriteGate = new();

    /// <summary>Writes the engine's answer into the case of the source file, under <paramref name="key"/>.</summary>
    public static void Write(string file, string name, string key, JsonNode? answer)
    {
        lock (WriteGate)
        {
            var path = Path.Combine(SourceRoot(), file);
            var document = Read(path);
            var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
            foreach (var stale in new[] { "expect", "throws" })
                entry.Remove(stale);
            entry[key] = answer;
            File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                IndentSize = 2,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
        }
    }

    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "dotnet", "ExGrid.slnx")))
                return Path.Combine(dir.FullName, "tests", "cases", "pivot");
        }
        throw new InvalidOperationException("The repository holding dotnet/ExGrid.slnx is not above the test's directory.");
    }

    /// <summary>A key a case must name.</summary>
    public static JsonNode Must(JsonObject node, string key)
        => node[key] ?? throw new InvalidOperationException($"The case names no '{key}'.");

    /// <summary>The case's own value of a key, or else the file's; one of them names it.</summary>
    public static JsonNode Inherited(JsonObject file, JsonObject entry, string key)
        => entry[key] ?? file[key] ?? throw new InvalidOperationException($"Neither the case nor its file names '{key}'.");

    // ---- The data -------------------------------------------------------------------------------

    /// <summary>A data set: its fields, each reading a record's column, its records, and the values its
    /// Order Keys were called with, in the order of the calls.</summary>
    public sealed record DataSet(PivotField<object?[]>[] Fields, object?[][] Records, List<string> KeyCalls);

    /// <summary>The data set a case pivots: <c>data</c> names one of <c>tests/cases/pivot/data/</c>, or is
    /// the set itself, and the case's own <c>fields</c> and <c>records</c>, when it has them, stand in for the
    /// set's.</summary>
    public static DataSet Data(JsonObject file, JsonObject entry)
    {
        var set = SetOf(file, entry);
        var columns = set["columns"]!.AsArray().Select(c => (string)c!).ToArray();
        var calls = new List<string>();
        var fields = (entry["fields"] ?? set["fields"]!).AsArray().Select(f => Field(f!.AsObject(), columns, calls)).ToArray();
        var records = (entry["records"] ?? set["records"]!).AsArray()
            .Select(r =>
            {
                var values = r!.AsArray();
                if (values.Count != columns.Length)
                    throw new InvalidOperationException($"A record holds {values.Count} values, and the data set has {columns.Length} columns.");
                return values.Select(Value).ToArray();
            })
            .ToArray();
        return new DataSet(fields, records, calls);
    }

    // The data set a case names: one of tests/cases/pivot/data/ by name, or the set itself, as a
    // differential run sends one.
    private static JsonObject SetOf(JsonObject file, JsonObject entry)
        => Inherited(file, entry, "data") is JsonObject set ? set : Read(Path.Combine(Root, "data", (string)Inherited(file, entry, "data")! + ".json"));

    /// <summary>The Snapshot a data set's columns make, when the set is a Snapshot's: <c>snapshot</c> names
    /// each column's name, kind and caption, and the column that is its Record Key, where it has one; and
    /// <c>rows</c> holds a row's values in column order, written as <see cref="Value"/> reads them; the case's
    /// own <c>rows</c>, when it has them, stand in for the set's. Null for a set of records.</summary>
    public static Snapshot? SnapshotOf(JsonObject file, JsonObject entry)
    {
        var set = SetOf(file, entry);
        if (set["snapshot"] is not { } shape)
            return null;
        return Rows(shape.AsObject(), (entry["rows"] ?? set["rows"]!).AsArray(), (string?)shape["recordKey"]);
    }

    /// <summary>The shape of a data set's Snapshot, when the set is a Snapshot's: what a Change Batch's records
    /// are written in.</summary>
    public static JsonObject? SnapshotShapeOf(JsonObject file, JsonObject entry) => SetOf(file, entry)["snapshot"]?.AsObject();

    /// <summary>Rows written in a Snapshot's shape, as a Snapshot of their own: keyed by <paramref name="key"/>
    /// where it names a column, and with no key otherwise, as a Change Batch's records are.</summary>
    public static Snapshot Rows(JsonObject shape, JsonArray written, string? key)
    {
        var columns = shape["columns"]!.AsArray().Select(c => c!.AsObject()).ToArray();
        var rows = written.Select(r => r!.AsArray()).ToArray();
        var builder = new SnapshotColumnsBuilder();
        for (var c = 0; c < columns.Length; c++)
        {
            var name = (string)columns[c]["name"]!;
            var caption = (string?)columns[c]["caption"];
            var values = rows.Select(r => r.Count == columns.Length
                ? Value(r[c])
                : throw new InvalidOperationException($"A row holds {r.Count} values, and the Snapshot has {columns.Length} columns.")).ToArray();
            switch ((string)columns[c]["kind"]!)
            {
                case "text":
                    var text = builder.Text(name, caption);
                    foreach (var value in values)
                        text.Append((string?)value);
                    break;
                case "decimal":
                    var exact = builder.Decimal(name, caption);
                    foreach (var value in values)
                        exact.Append((decimal?)value);
                    break;
                case "double":
                    var real = builder.Double(name, caption);
                    foreach (var value in values)
                        real.Append((double?)value);
                    break;
                case "integer":
                    var whole = builder.Integer(name, caption);
                    foreach (var value in values)
                        whole.Append(value is double d ? checked((long)d) : (long?)value);
                    break;
                case "date":
                    var date = builder.Date(name, caption);
                    foreach (var value in values)
                    {
                        if (value is DateTime when)
                            date.Append(when);
                        else
                            date.AppendBlank();
                    }
                    break;
                case "boolean":
                    var flag = builder.Boolean(name, caption);
                    foreach (var value in values)
                        flag.Append((bool?)value);
                    break;
                case var kind:
                    throw new InvalidOperationException($"'{kind}' is not a kind of column the cases name.");
            }
        }
        if (key is not null)
            builder.Key(key);
        return builder.Build();
    }

    private static PivotField<object?[]> Field(JsonObject declared, string[] columns, List<string> calls)
    {
        var name = (string)declared["name"]!;
        var reads = declared["of"] is { } of ? (string)of! : name;
        var at = Array.IndexOf(columns, reads);
        if (at < 0)
            throw new InvalidOperationException($"The field '{name}' reads '{reads}', which is no column of the data set.");
        return new PivotField<object?[]>(
            name,
            Member<PivotFieldType>((string)declared["type"]!),
            record => record[at],
            caption: (string?)declared["caption"],
            format: (string?)declared["format"],
            itemOrder: declared["itemOrder"]?.AsArray().Select(v => Value(v) ?? throw new InvalidOperationException("A declared Item is a value.")).ToArray())
        {
            DatePart = declared["datePart"] is { } part ? Member<PivotDatePart>((string)part!) : null,
            OrderKey = declared["orderKey"] is { } key ? OrderKey((string)key!, calls) : null,
        };
    }

    /// <summary>
    /// A value as a case writes it: a JSON string is text, a number a double, <c>true</c> and <c>false</c>
    /// Booleans, <c>null</c> a Blank; <c>{"decimal": "100.50"}</c> an exact decimal, <c>{"integer": "7"}</c>
    /// a 64-bit integer, <c>{"double": "NaN"}</c> a double JSON cannot hold, and <c>{"date":
    /// "2026-01-10T13:45:00"}</c> a date, its time optional.
    /// </summary>
    public static object? Value(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                return value.GetValue<string>();
            case JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False:
                return value.GetValue<bool>();
            case JsonValue value when value.GetValueKind() == JsonValueKind.Number:
                return value.GetValue<double>();
            case JsonObject typed when typed.Count == 1:
            {
                var (kind, text) = (typed.First().Key, (string)typed.First().Value!);
                return kind switch
                {
                    "decimal" => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                    "integer" => long.Parse(text, CultureInfo.InvariantCulture),
                    "double" => double.Parse(text, CultureInfo.InvariantCulture),
                    "date" => DateTime.ParseExact(text, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None),
                    _ => throw new InvalidOperationException($"'{kind}' is not a kind of value a case writes."),
                };
            }
            default:
                throw new InvalidOperationException($"{node.ToJsonString()} is not a value a case writes.");
        }
    }

    // The Order Keys a case names, each the Consumer's function the C# tests declared, noting each value it
    // is called with.
    private static Func<object, IComparable?> OrderKey(string name, List<string> calls)
    {
        Func<object, IComparable?> key = name switch
        {
            "tenor" => value => Tenor((string)value),
            "tenorButOnIsText" => value => (string)value == "ON" ? "zero" : Tenor((string)value),
            "tenorThrowsOn7Y" => value => (string)value == "7Y" ? throw new FormatException("no 7Y bucket") : Tenor((string)value),
            "absolute" => value => Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            _ => throw new InvalidOperationException($"'{name}' is not an Order Key the cases declare."),
        };
        return value =>
        {
            calls.Add(Convert.ToString(value, CultureInfo.InvariantCulture)!);
            return key(value);
        };
    }

    /// <summary>A tenor's length in days, as a desk would key it: ON and TN first, then weeks, months and
    /// years, a compound tenor summed. Anything else has no key.</summary>
    public static IComparable? Tenor(string tenor)
    {
        switch (tenor)
        {
            case "ON":
                return 0;
            case "TN":
                return 1;
        }
        var days = 0;
        var number = 0;
        var any = false;
        foreach (var c in tenor)
        {
            if (char.IsAsciiDigit(c))
            {
                number = (number * 10) + (c - '0');
                continue;
            }
            var unit = c switch { 'D' => 1, 'W' => 7, 'M' => 30, 'Y' => 360, _ => -1 };
            if (unit < 0 || number == 0)
                return null;
            days += number * unit;
            number = 0;
            any = true;
        }
        return any && number == 0 ? days : null;
    }

    // ---- The layout and the options --------------------------------------------------------------

    /// <summary>A layout as <see cref="PivotLayoutJson"/> writes one, its version left out.</summary>
    public static PivotLayout Layout(JsonNode written)
    {
        var json = written.DeepClone().AsObject();
        json.Insert(0, "version", PivotLayoutJson.Version);
        return PivotLayoutJson.Read(json.ToJsonString());
    }

    /// <summary>The layout as a case writes it: <see cref="PivotLayoutJson"/>'s document without its
    /// version.</summary>
    public static JsonNode LayoutJson(PivotLayout layout)
    {
        var json = JsonNode.Parse(PivotLayoutJson.Write(layout))!.AsObject();
        json.Remove("version");
        return json;
    }

    /// <summary>The report's options: <c>locale</c>, a .NET culture name or <c>invariant</c>, and
    /// <c>words</c> (<see cref="Words"/>).</summary>
    public static PivotOptions Options(JsonObject file, JsonObject entry)
    {
        var locale = (string)Inherited(file, entry, "locale")!;
        return new PivotOptions
        {
            Culture = locale == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale),
            Label = Words(Inherited(file, entry, "words")),
        };
    }

    /// <summary>The words a case names: <c>english</c>, <c>japanese</c> or <c>unitedKingdom</c>, or a
    /// Consumer's own, an object of a word by its id, every other id left to the English.</summary>
    public static Func<string, string?>? Words(JsonNode words)
    {
        if (words is JsonObject own)
        {
            var replaced = own.ToDictionary(w => w.Key, w => (string)w.Value!, StringComparer.Ordinal);
            return id => replaced.GetValueOrDefault(id);
        }
        return (string)words! switch
        {
            "english" => null,
            "japanese" => PivotWords.Japanese,
            "unitedKingdom" => PivotWords.EnglishUnitedKingdom,
            var name => throw new InvalidOperationException($"'{name}' is not a set of words the cases name."),
        };
    }

    /// <summary>An Item's key as a layout's JSON writes one: its kind, and its value where it has one.</summary>
    public static JsonNode ItemKey(PivotItemKey key)
    {
        var json = new JsonObject { ["kind"] = Name(key.Kind) };
        if (key.Value is not null)
            json["value"] = key.Value;
        return json;
    }

    /// <summary>An Item's key as a layout's JSON writes one.</summary>
    public static PivotItemKey ItemKeyOf(JsonNode written)
    {
        var json = new JsonObject { ["version"] = PivotLayoutJson.Version, ["rows"] = new JsonArray(new JsonObject { ["field"] = "f", ["hiddenItems"] = new JsonArray(written.DeepClone()) }) };
        return PivotLayoutJson.Read(json.ToJsonString()).Rows[0].HiddenItems[0];
    }

    /// <summary>An enum member by its camelCase name, as the layout's JSON writes one.</summary>
    public static T Member<T>(string name) where T : struct, Enum
        => Enum.TryParse<T>(name, ignoreCase: true, out var member) && char.IsLower(name[0]) && Enum.IsDefined(member)
            ? member
            : throw new InvalidOperationException($"'{name}' is not a {typeof(T).Name} as a case names one.");

    /// <summary>An enum member's camelCase name.</summary>
    public static string Name<T>(T member) where T : struct, Enum
    {
        var text = member.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    // ---- Refusals -----------------------------------------------------------------------------------

    /// <summary>
    /// What a refusal is named in a case: <c>argument</c>, <c>outOfRange</c>, <c>invalidOperation</c>,
    /// <c>format</c> (a document that does not read), <c>version</c> (one of a version not read) or
    /// <c>snapshot</c> (a value its column cannot hold), and the message, without .NET's parameter suffix.
    /// </summary>
    public static JsonObject Refusal(Exception thrown)
    {
        var kind = thrown switch
        {
            ArgumentOutOfRangeException => "outOfRange",
            ArgumentException => "argument",
            NotSupportedException => "version",
            FormatException => "format",
            InvalidOperationException => "invalidOperation",
            ExGrid.Data.SnapshotException => "snapshot",
            _ => throw new InvalidOperationException($"The engine threw {thrown.GetType().Name}, which no case names.", thrown),
        };
        var message = thrown.Message;
        if (thrown is ArgumentOutOfRangeException { ActualValue: { } actual })
        {
            var actualSuffix = $"{Environment.NewLine}Actual value was {actual}.";
            if (message.EndsWith(actualSuffix, StringComparison.Ordinal))
                message = message[..^actualSuffix.Length];
        }
        if (thrown is ArgumentException { ParamName: { } param })
        {
            var suffix = $" (Parameter '{param}')";
            if (message.EndsWith(suffix, StringComparison.Ordinal))
                message = message[..^suffix.Length];
        }
        return new JsonObject { ["refusal"] = kind, ["saying"] = message };
    }
}
