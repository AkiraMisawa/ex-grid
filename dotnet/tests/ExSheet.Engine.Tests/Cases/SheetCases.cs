using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExSheet.Engine;

namespace ExSheet.Engine.Tests.Cases;

/// <summary>
/// Reads the engine's shared cases (<c>tests/cases/sheet/</c>; ADR-0370 sections 2 and 5), which Vitest runs
/// against <c>@ex-grid/sheet-engine</c> as well: a Workbook made under a culture, steps taken on it in turn, and
/// what each step reads back. <c>tests/cases/README.md</c> says what a file holds.
/// </summary>
internal static class SheetCases
{
    internal static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "sheet");

    internal static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    /// <summary>Every case of every file of the kind, as (file, name) pairs.</summary>
    public static IEnumerable<(string File, string Name)> Of(string kind)
    {
        foreach (var file in Directory.GetFiles(Root, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal))
        {
            var document = Read(Path.Combine(Root, file!));
            if ((string?)document["kind"] != kind)
                continue;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in document["cases"]!.AsArray())
            {
                var name = (string)entry!["name"]!;
                if (!names.Add(name))
                    throw new InvalidDataException($"sheet/{file} holds two cases named \"{name}\".");
                yield return (file!, name);
            }
        }
    }

    /// <summary>The names of the cases of one file.</summary>
    public static IEnumerable<string> NamesIn(string file) =>
        Read(Path.Combine(Root, file))["cases"]!.AsArray().Select(entry => (string)entry!["name"]!);

    /// <summary>The case of the file named so, and the file it is in.</summary>
    public static (JsonObject File, JsonObject Case) Find(string file, string name)
    {
        var document = Read(Path.Combine(Root, file));
        foreach (var entry in document["cases"]!.AsArray())
        {
            if ((string?)entry!["name"] == name)
                return (document, entry.AsObject());
        }
        throw new InvalidOperationException($"sheet/{file} holds no case named \"{name}\".");
    }

    /// <summary>Runs the case, and answers every way the engine differs from what it expects; empty when it agrees.</summary>
    public static IReadOnlyList<string> Run(JsonObject file, JsonObject entry)
    {
        var run = new Run(Culture((string?)(entry["culture"] ?? file["culture"])
            ?? throw new InvalidDataException("Neither the case nor its file names a culture.")));
        if (entry["sheets"] is JsonArray sheets)
            run.Workbook = new Workbook(run.Culture, [.. sheets.Select(s => (string)s!)]);
        if (entry["setup"] is { } setup)
        {
            var named = file["setups"]?[(string)setup!] as JsonArray
                ?? throw new InvalidDataException($"The file names no setup '{(string)setup!}'.");
            for (var i = 0; i < named.Count; i++)
                run.Take(named[i]!.AsObject(), $"setup step {i + 1}");
        }
        if (entry["steps"] is not JsonArray steps || steps.Count == 0)
            throw new InvalidDataException("The case takes no steps.");
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i]!.AsObject();
            run.Take(step, $"step {i + 1}");
        }
        return run.Differences;
    }

    /// <summary>
    /// Runs the case with the thread in the case's culture (ADR-0047): what the engine writes or reads stays invariant
    /// whatever culture a server thread is in, and a format or parse that leaked the thread's culture would show here.
    /// </summary>
    public static IReadOnlyList<string> RunInCulture(JsonObject file, JsonObject entry)
    {
        var culture = Culture((string?)(entry["culture"] ?? file["culture"])
            ?? throw new InvalidDataException("Neither the case nor its file names a culture."));
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            return Run(file, entry);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    internal static CultureInfo Culture(string name) =>
        name == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);

    /// <summary>A member of a closed vocabulary as a case writes it: its name in camelCase.</summary>
    internal static string Camel<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    internal static T Enum<T>(JsonNode node) where T : struct, Enum
    {
        var text = (string)node!;
        if (text.Length == 0 || char.IsUpper(text[0]) || !System.Enum.TryParse<T>(text, ignoreCase: true, out var value) || Camel(value) != text)
            throw new InvalidDataException($"'{text}' is no {typeof(T).Name}.");
        return value;
    }

    /// <summary>A Value as a case writes one: a number, text, a Boolean, <c>null</c> for a blank, and an Error Value as <c>{"error": "#N/A"}</c> (with <c>"refused": true</c> for a refusal).</summary>
    internal static JsonNode? Encode(Value? value)
    {
        if (value is not { } v) return null;
        return v.Kind switch
        {
            ValueKind.Number => JsonValue.Create(v.Number),
            ValueKind.Text => JsonValue.Create(v.Text),
            ValueKind.Boolean => JsonValue.Create(v.Boolean),
            ValueKind.Error => v.IsRefused
                ? new JsonObject { ["error"] = v.ToString(), ["refused"] = true }
                : new JsonObject { ["error"] = v.ToString() },
            _ => throw new InvalidOperationException($"{v.Kind} is no kind of Value."),
        };
    }

    /// <summary>A Value a case writes, read back; the inverse of <see cref="Encode"/>.</summary>
    internal static Value? Decode(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject { Count: 2 } refused when refused["refused"] is JsonValue flag && flag.GetValueKind() == JsonValueKind.True:
                // A refusal, as a Consumer holds one read from a refused cell.
                return (string?)refused["error"] switch
                {
                    "#VALUE!" => Value.Refusal,
                    "#NAME?" => Value.RefusedName,
                    _ => throw new InvalidDataException($"{refused.ToJsonString()} is no refusal: a refusal is #VALUE! or #NAME?."),
                };
            case JsonObject error:
                // The two states of a computation are no Error Value typed, but a case may give one to be refused.
                if (error.Count != 1)
                    throw new InvalidDataException($"{error.ToJsonString()} is no Error Value a case gives.");
                return Value.FromError((string?)error["error"] switch
                {
                    "#CIRC!" => ErrorValue.Circ,
                    "#GETTING_DATA" => ErrorValue.GettingData,
                    var written when ErrorValues.TryParseWritten(written, out var parsed) => parsed,
                    _ => throw new InvalidDataException($"{error.ToJsonString()} is no Error Value a case gives."),
                });
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                return Value.FromText((string)value!);
            case JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False:
                return Value.FromBoolean((bool)value);
            case JsonValue value:
                return Value.FromNumber(Number(value));
            default:
                throw new InvalidDataException($"{node.ToJsonString()} is no Value.");
        }
    }

    /// <summary>A number a case writes: a JSON number, or <c>"Infinity"</c>, <c>"-Infinity"</c>, <c>"NaN"</c>.</summary>
    internal static double Number(JsonNode node) => node.GetValueKind() switch
    {
        JsonValueKind.Number => double.Parse(node.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture),
        JsonValueKind.String => (string)node! switch
        {
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            "NaN" => double.NaN,
            var other => throw new InvalidDataException($"'{other}' is no number."),
        },
        _ => throw new InvalidDataException($"{node.ToJsonString()} is no number."),
    };

    internal static JsonNode Colour(CellColour colour) => colour.ToString();

    internal static CellColour ColourOf(JsonNode node) =>
        (string)node! == "Automatic" ? CellColour.Automatic
            : CellColour.FromHex((string)node!) ?? throw new InvalidDataException($"'{(string)node!}' is no colour.");

    internal static JsonObject Font(CellFont font) => new()
    {
        ["colour"] = Colour(font.Colour),
        ["bold"] = font.Bold,
        ["italic"] = font.Italic,
        ["underline"] = font.Underline,
        ["strikethrough"] = font.Strikethrough,
    };

    internal static JsonNode Fill(CellFill fill) => fill.Colour is { } colour ? Colour(colour) : "none";

    internal static CellFill FillOf(JsonNode node) => (string)node! == "none" ? CellFill.None : CellFill.Solid(ColourOf(node));

    internal static JsonNode Line(BorderLine line) =>
        line.IsNone ? "none" : new JsonObject { ["style"] = Camel(line.Style), ["colour"] = Colour(line.Colour) };

    internal static BorderLine LineOf(JsonNode node) => node is JsonObject line
        ? new BorderLine(Enum<BorderLineStyle>(line["style"]!), line["colour"] is { } colour ? ColourOf(colour) : CellColour.Automatic)
        : (string)node! == "none" ? BorderLine.None : throw new InvalidDataException($"{node.ToJsonString()} is no line.");

    internal static JsonObject CellFormatJson(CellFormat format) => new()
    {
        ["numberFormat"] = format.NumberFormat.Code,
        ["alignment"] = Camel(format.Alignment),
        ["font"] = Font(format.Font),
        ["fill"] = Fill(format.Fill),
        ["borders"] = Borders(format.Borders),
    };

    /// <summary>Nodes in the ordinal order of their JSON's text, so that a set reads the same from both runners.</summary>
    internal static JsonArray Sorted(IEnumerable<JsonNode> nodes) =>
        new([.. nodes.OrderBy(n => n.ToJsonString(Unescaped), StringComparer.Ordinal)]);

    /// <summary>JSON text as JavaScript's <c>JSON.stringify</c> writes it, so that both runners sort a set alike: <c>+</c>, quotes, HTML's characters and non-ASCII written as they are.</summary>
    private static readonly JsonSerializerOptions Unescaped = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static JsonObject Borders(CellBorders borders) => new()
    {
        ["top"] = Line(borders.Top),
        ["bottom"] = Line(borders.Bottom),
        ["left"] = Line(borders.Left),
        ["right"] = Line(borders.Right),
    };

    /// <summary>Text a case types: a string, or <c>{"parts": [...]}</c>, each part a string or <c>[string, times]</c>, for a Formula too long to write out.</summary>
    internal static string Typed(JsonNode? node)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            return (string)value!;
        if (node is not JsonObject { Count: 1 } built || built["parts"] is not JsonArray parts)
            throw new InvalidDataException($"'{node?.ToJsonString()}' is no text to type: a string, or {{\"parts\": [...]}}.");
        var text = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            switch (part)
            {
                case JsonValue piece when piece.GetValueKind() == JsonValueKind.String:
                    text.Append((string)piece!);
                    break;
                case JsonArray { Count: 2 } repeated when repeated[0] is JsonValue p && p.GetValueKind() == JsonValueKind.String
                    && repeated[1] is JsonValue n && n.TryGetValue<int>(out var times) && times >= 0:
                    text.Insert(text.Length, (string)p!, times);
                    break;
                default:
                    throw new InvalidDataException($"'{part?.ToJsonString()}' is no part of a text: a string, or [string, times] with times a whole number, 0 or more.");
            }
        }
        return text.ToString();
    }

    /// <summary>A per-cell reading of texts with each built text (<see cref="Typed"/>) written out; a <c>null</c> stays one.</summary>
    internal static JsonNode? Texts(JsonNode? expected) =>
        new JsonObject([.. expected!.AsObject().Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value is JsonObject ? Typed(p.Value) : p.Value?.DeepClone()))]);

    internal static JsonNode? EntryOf(Entry? entry) => entry switch
    {
        null => null,
        { IsFormula: true } => new JsonObject { ["formula"] = entry.Formula },
        _ => new JsonObject { ["constant"] = Encode(entry.Constant) },
    };

    /// <summary>A Cell Format change as a case writes one: each part it sets, by the Script Core's name.</summary>
    internal static CellFormatChange ChangeOf(JsonNode node)
    {
        var parts = node.AsObject();
        foreach (var (key, _) in parts)
        {
            if (key is not ("numberFormat" or "alignment" or "fontColour" or "bold" or "italic" or "underline" or "strikethrough" or "fill" or "borders"))
                throw new InvalidDataException($"'{key}' is no part of a Cell Format change.");
        }
        return new CellFormatChange
        {
            NumberFormat = parts["numberFormat"] is { } code ? NumberFormat.Parse((string)code!) : null,
            // A number is a value cast in from outside the vocabulary, given to be refused.
            Alignment = parts["alignment"] switch
            {
                null => null,
                var cast when cast.GetValueKind() == JsonValueKind.Number => (HorizontalAlignment)cast.GetValue<int>(),
                var alignment => Enum<HorizontalAlignment>(alignment),
            },
            FontColour = parts["fontColour"] is { } colour ? ColourOf(colour) : null,
            Bold = (bool?)parts["bold"],
            Italic = (bool?)parts["italic"],
            Underline = (bool?)parts["underline"],
            Strikethrough = (bool?)parts["strikethrough"],
            Fill = parts["fill"] is { } fill ? FillOf(fill) : null,
            Borders = parts["borders"] is { } borders ? BorderChangeOf(borders) : null,
        };
    }

    private static BorderChange BorderChangeOf(JsonNode node)
    {
        if (node is JsonValue && (string)node! == "none") return BorderChange.None;
        var edges = node.AsObject();
        if (edges["outline"] is { } outline && edges.Count == 1) return BorderChange.Outline(LineOf(outline));
        if (edges["inside"] is { } inside && edges.Count == 1) return BorderChange.Inside(LineOf(inside));
        foreach (var (key, _) in edges)
        {
            if (key is not ("top" or "bottom" or "left" or "right" or "insideHorizontal" or "insideVertical"))
                throw new InvalidDataException($"'{key}' is no edge of a Border change.");
        }
        BorderLine? Edge(string key) => edges[key] is { } line ? LineOf(line) : null;
        return new BorderChange
        {
            Top = Edge("top"),
            Bottom = Edge("bottom"),
            Left = Edge("left"),
            Right = Edge("right"),
            InsideHorizontal = Edge("insideHorizontal"),
            InsideVertical = Edge("insideVertical"),
        };
    }

    /// <summary>A Sheet Document as a case reads one: its cells, its column and row formats and its column widths.</summary>
    internal static JsonObject Document(SheetDocument document)
    {
        static JsonObject Run(SheetDocumentAxisFormat run) => new()
        {
            ["first"] = run.First,
            ["last"] = run.Last,
            ["numberFormat"] = run.NumberFormat?.Code,
            ["alignment"] = run.Alignment is { } alignment ? Camel(alignment) : null,
            ["font"] = run.Font is { } font ? Font(font) : null,
            ["fill"] = run.Fill is { } fill ? Fill(fill) : null,
            ["borders"] = run.Borders is { } borders ? Borders(borders) : null,
        };
        return new JsonObject
        {
            ["cells"] = new JsonArray([.. document.Cells.Select(c => (JsonNode)new JsonObject
            {
                ["address"] = c.Address.ToString(),
                ["entry"] = EntryOf(c.Entry),
                ["numberFormat"] = c.NumberFormat?.Code,
                ["alignment"] = c.Alignment is { } alignment ? Camel(alignment) : null,
                ["font"] = c.Font is { } font ? Font(font) : null,
                ["fill"] = c.Fill is { } fill ? Fill(fill) : null,
                ["borders"] = c.Borders is { } borders ? Borders(borders) : null,
            })]),
            ["cellAddresses"] = new JsonArray([.. document.Cells.Select(c => (JsonNode)c.Address.ToString())]),
            ["columns"] = new JsonArray([.. document.Columns.Select(r => (JsonNode)Run(r))]),
            ["rows"] = new JsonArray([.. document.Rows.Select(r => (JsonNode)Run(r))]),
            ["columnWidths"] = new JsonArray([.. document.ColumnWidths.Select(w => (JsonNode)new JsonObject
            {
                ["first"] = w.First,
                ["last"] = w.Last,
                ["width"] = w.Width,
                ["kind"] = Camel(w.Kind),
                ["isCustom"] = w.IsCustom,
            })]),
        };
    }

    internal static JsonArray Addresses(IEnumerable<CellAddress> addresses) => [.. addresses.Select(a => (JsonNode)a.ToString())];

    internal static JsonArray Numbers(IEnumerable<int> numbers) => [.. numbers.Select(n => (JsonNode)n)];

    /// <summary>
    /// Whether <paramref name="actual"/> is what <paramref name="expected"/> says, and where not, how: an object is
    /// compared by the keys the case writes, a key the engine leaves out reading as <c>null</c>; a list, element by
    /// element; a number, exactly, or within <c>{"near": n, "within": e}</c>.
    /// </summary>
    internal static void Compare(string path, JsonNode? expected, JsonNode? actual, List<string> differences)
    {
        if (expected is JsonObject near && near["near"] is { } centre && near["within"] is { } within && near.Count == 2)
        {
            if (actual is not JsonValue a || a.GetValueKind() != JsonValueKind.Number || Math.Abs(Number(a) - Number(centre)) > Number(within))
                differences.Add($"{path}: expected {Number(centre).ToString("R", CultureInfo.InvariantCulture)} within {Number(within).ToString("R", CultureInfo.InvariantCulture)}, got {Show(actual)}");
            return;
        }
        // An Error Value compares in full: one written without "refused" is Excel's own, and a refusal is not it.
        if (expected is JsonObject error && actual is JsonObject other && error.ContainsKey("error") && other.ContainsKey("error") && error.Count != other.Count)
        {
            differences.Add($"{path}: expected {Show(expected)}, got {Show(actual)}");
            return;
        }
        switch (expected)
        {
            case JsonObject e when actual is JsonObject a:
                foreach (var (key, value) in e)
                    Compare($"{path}.{key}", value, a[key], differences);
                return;
            case JsonArray e when actual is JsonArray a:
                if (e.Count != a.Count)
                {
                    differences.Add($"{path}: expected {e.ToJsonString()}, got {a.ToJsonString()}");
                    return;
                }
                for (var i = 0; i < e.Count; i++)
                    Compare($"{path}[{i}]", e[i], a[i], differences);
                return;
            case JsonValue e when actual is JsonValue a && e.GetValueKind() == JsonValueKind.Number && a.GetValueKind() == JsonValueKind.Number:
                var x = Number(e);
                var y = Number(a);
                if (!(x == y && (x != 0 || double.IsNegative(x) == double.IsNegative(y))))
                    differences.Add($"{path}: expected {Show(e)}, got {Show(a)}");
                return;
            case null when actual is null:
                return;
            default:
                if (!JsonNode.DeepEquals(expected, actual))
                    differences.Add($"{path}: expected {Show(expected)}, got {Show(actual)}");
                return;
        }
    }

    internal static string Show(JsonNode? node) => node switch
    {
        null => "null",
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => Number(v).ToString("R", CultureInfo.InvariantCulture),
        _ => node.ToJsonString(),
    };
}

/// <summary>One case being run: its Workbook, the steps it has done, and what it remembered.</summary>
internal sealed class Run(CultureInfo culture)
{
    public CultureInfo Culture { get; } = culture;

    public Workbook Workbook { get; set; } = new(culture);

    public List<string> Differences { get; } = [];

    private readonly List<WorkbookStep> _done = [];
    private readonly Dictionary<string, string> _pictures = new(StringComparer.Ordinal);
    private int _clockReads;
    private readonly Dictionary<string, SheetCopy> _copies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<IReadOnlyList<Value?>>?> _tables = new(StringComparer.Ordinal);

    private static readonly string[] Verbs =
    [
        "enter", "enterAll", "enterRun", "do", "doWire", "check", "undo", "redo", "reopen", "remember", "read", "copy",
        "setNumberFormat", "setAlignment", "declareTable", "pushTable", "setToday", "setSeed", "setClock", "recalculateVolatile", "getDisplay", "setCellFormat", "show",
        "doAll", "readTable", "setColumnWidth", "setAutomaticColumnWidth", "rename", "retype",
    ];

    private static readonly string[] Modifiers = ["on", "expect", "note"];

    public void Take(JsonObject step, string where)
    {
        foreach (var (key, _) in step)
        {
            if (!Verbs.Contains(key) && !Modifiers.Contains(key))
                throw new InvalidDataException($"{where}: '{key}' is no step or modifier.");
        }
        var verbs = step.Where(p => Verbs.Contains(p.Key)).ToList();
        if (verbs.Count != 1)
            throw new InvalidDataException($"{where}: a step takes one verb, not {verbs.Count}.");
        var (verb, argument) = (verbs[0].Key, verbs[0].Value);
        var sheet = step["on"] is { } on
            ? Workbook.Find((string)on!) ?? throw new InvalidDataException($"{where}: the Workbook has no Sheet '{(string)on!}'.")
            : Workbook.Shown;
        var expect = step["expect"] as JsonObject;
        var outcome = new Outcome(sheet) { ClockReadsBefore = _clockReads };
        try
        {
            Act(verb, argument!, sheet, outcome);
        }
        catch (Exception e) when (IsRefusal(e))
        {
            if (expect?["throws"] is not { } throws)
            {
                Differences.Add($"{where} ({verb}) threw {e.GetType().Name}: {e.Message}");
                return;
            }
            SheetCases.Compare($"{where}.throws", throws, Thrown(e), Differences);
            foreach (var (key, _) in expect)
            {
                if (key != "throws")
                    Read(where, key, expect[key], outcome);
            }
            return;
        }
        if (expect is null)
            return;
        if (expect["throws"] is { } missed)
        {
            Differences.Add($"{where} ({verb}): expected it to throw {missed.ToJsonString()}, but it did not");
            return;
        }
        foreach (var (key, value) in expect)
            Read(where, key, value, outcome);
    }

    /// <summary>A Linked Table's columns as <c>readTable</c> read them: <c>null</c> while it waits.</summary>
    private sealed record TableRead(IReadOnlyList<IReadOnlyList<Value?>>? Columns);

    private static JsonNode? ColumnsJson(IReadOnlyList<IReadOnlyList<Value?>>? columns) => columns is null
        ? null
        : new JsonArray([.. columns.Select(c => (JsonNode)new JsonArray([.. c.Select(SheetCases.Encode)]))]);

    /// <summary>What a step did, for its readings: the change it reported, the refusal it was checked for, its step.</summary>
    private sealed class Outcome(Sheet sheet)
    {
        public Sheet Sheet { get; set; } = sheet;
        public SheetChange? Change { get; set; }
        public bool Checked { get; set; }
        public SheetRefusal? Refusal { get; set; }
        public WorkbookStep? Step { get; set; }
        /// <summary>How many times the case's clock had been read before the step.</summary>
        public int ClockReadsBefore { get; init; }
        /// <summary>What a step, an undo or a redo changed across the Workbook.</summary>
        public WorkbookChange? WorkbookChange { get; set; }
        /// <summary>A Linked Table's columns as <c>readTable</c> read them, or <c>null</c> while it waits.</summary>
        public TableRead? Table { get; set; }
    }

    /// <summary>A Sheet's change as a case reads one.</summary>
    private static JsonObject ChangeJson(SheetChange change) => new()
    {
        ["valueChanges"] = SheetCases.Addresses(change.ValueChanges),
        ["recalculated"] = SheetCases.Addresses(change.Recalculated),
        ["recalculatedCount"] = change.Recalculated.Count,
        ["rows"] = SheetCases.Numbers(change.Rows),
        ["columns"] = SheetCases.Numbers(change.Columns),
        ["reformatsUnnamedRows"] = change.ReformatsUnnamedRows,
    };

    /// <summary>
    /// Whether the exception is the engine refusing, by a type a case reads as one. The runner's own reading of a
    /// malformed case throws the same types, and the engine's broken invariants say "Internal", as the Script Core's
    /// do: each is a defect, never a refusal.
    /// </summary>
    internal static bool IsRefusal(Exception e) =>
        e is FormulaSyntaxException or SheetRefusedException or InvalidOperationException or ArgumentException or FormatException
            or RepeatedKeyException or SheetDocumentException
        && !e.Message.StartsWith("Internal", StringComparison.Ordinal)
        && !ThrownByTheRunner(e);

    /// <summary>Whether the innermost frame outside .NET's own libraries is this runner's rather than the engine's.</summary>
    private static bool ThrownByTheRunner(Exception e)
    {
        foreach (var frame in new System.Diagnostics.StackTrace(e).GetFrames())
        {
            var assembly = frame.GetMethod()?.DeclaringType?.Assembly;
            if (assembly == typeof(SheetCases).Assembly) return true;
            if (assembly == typeof(Sheet).Assembly) return false;
        }
        return false;
    }

    internal static JsonObject Thrown(Exception e) => e switch
    {
        FormulaSyntaxException f => new JsonObject { ["refusal"] = "formulaSyntax", ["reason"] = f.Reason, ["beyondLimit"] = f.IsBeyondLimit, ["saying"] = e.Message },
        SheetRefusedException r => new JsonObject { ["refusal"] = "refused", ["reason"] = SheetCases.Camel(r.Refusal.Reason), ["saying"] = r.Refusal.Message },
        RepeatedKeyException k => new JsonObject
        {
            ["refusal"] = "repeatedKey", ["saying"] = WithoutParameter(k), ["table"] = k.Table,
            ["keyColumn"] = k.KeyColumn, ["key"] = SheetCases.Encode(k.Key),
            ["keyColumns"] = new JsonArray([.. k.KeyColumns.Select(c => (JsonNode)c)]), ["keys"] = new JsonArray([.. k.Keys.Select(v => SheetCases.Encode(v))]),
            ["firstRow"] = k.FirstRow, ["secondRow"] = k.SecondRow, ["valueChanges"] = SheetCases.Addresses(k.Change.ValueChanges),
        },
        SheetDocumentException document => new JsonObject { ["refusal"] = "document", ["saying"] = e.Message, ["documentVersion"] = document.DocumentVersion },
        ArgumentException a => new JsonObject { ["refusal"] = "argument", ["saying"] = Saying(a) },
        FormatException => new JsonObject { ["refusal"] = "format", ["saying"] = e.Message },
        _ => new JsonObject { ["refusal"] = "invalidOperation", ["saying"] = e.Message },
    };

    /// <summary>An argument exception's message without the parameter suffix .NET adds.</summary>
    private static string WithoutParameter(ArgumentException e)
    {
        var message = e.Message;
        var suffix = e.ParamName is null ? -1 : message.LastIndexOf($" (Parameter '{e.ParamName}')", StringComparison.Ordinal);
        return suffix >= 0 ? message[..suffix] : message;
    }

    /// <summary>An argument exception's message as the Script Core's RangeError says it: without the parameter suffix,
    /// and without the value an ArgumentOutOfRangeException adds on a line of its own.</summary>
    private static string Saying(ArgumentException e)
    {
        var message = WithoutParameter(e);
        if (e is not ArgumentOutOfRangeException) return message;
        var line = message.IndexOf('\n');
        return line >= 0 ? message[..line].TrimEnd('\r') : message;
    }

    private void Act(string verb, JsonNode argument, Sheet sheet, Outcome outcome)
    {
        switch (verb)
        {
            case "enter":
                foreach (var (address, typed) in argument.AsObject())
                    outcome.Change = sheet.Enter(CellAddress.Parse(address), SheetCases.Typed(typed));
                break;
            case "enterAll":
                outcome.Change = sheet.Enter([.. argument.AsObject().Select(p => new KeyValuePair<CellAddress, string>(CellAddress.Parse(p.Key), SheetCases.Typed(p.Value)))]);
                break;
            case "enterRun":
            {
                // One batch over a range, each cell typed from a template naming its row: {row} or {row-1}.
                var run = argument.AsObject();
                var typed = (string)run["typed"]!;
                outcome.Change = sheet.Enter([.. CellRange.Parse((string)run["range"]!).Cells().Select(a => new KeyValuePair<CellAddress, string>(a,
                    typed.Replace("{row-1}", a.Row.ToString(CultureInfo.InvariantCulture)).Replace("{row}", (a.Row + 1).ToString(CultureInfo.InvariantCulture))))]);
                break;
            }
            case "do":
            case "doWire":
                var step = Workbook.Do(sheet.Name, verb == "do" ? Edit(argument) : WiredEdit((string)argument!));
                _done.Add(step);
                outcome.Step = step;
                outcome.Sheet = step.Sheet;
                outcome.WorkbookChange = step.Change;
                outcome.Change = step.Change.ChangesSheets && !Workbook.Sheets.Contains(step.Sheet) ? null : step.Change.For(step.Sheet);
                break;
            case "doAll":
            {
                // Several edits, each on the Sheet it names, as one operation.
                var all = Workbook.Do([.. argument.AsArray().Select(pair => ((string)pair![0]!, Edit(pair[1]!)))]);
                _done.Add(all);
                outcome.Step = all;
                outcome.Sheet = all.Sheet;
                outcome.WorkbookChange = all.Change;
                outcome.Change = all.Change.For(all.Sheet);
                break;
            }
            case "readTable":
            {
                // A table's name, or {"name", "as"}, which also holds the snapshot read under that label.
                var (table, label) = argument is JsonObject named
                    ? ((string)named["name"]!, (string?)named["as"] ?? throw new InvalidDataException("'readTable' names the snapshot it holds 'as'."))
                    : ((string)argument!, null);
                outcome.Table = new TableRead(Workbook.LinkedTableColumns(table));
                if (label is not null)
                    _tables[label] = outcome.Table.Columns;
                break;
            }
            case "check":
                outcome.Checked = true;
                outcome.Refusal = Workbook.Check(sheet.Name, Edit(argument));
                break;
            case "undo":
            {
                var done = Done(argument);
                outcome.Step = done;
                var change = done.Undo();
                outcome.WorkbookChange = change;
                outcome.Change = Workbook.Sheets.Contains(done.Sheet) ? change.For(done.Sheet) : null;
                break;
            }
            case "redo":
            {
                var done = Done(argument);
                outcome.Step = done;
                var change = done.Redo();
                outcome.WorkbookChange = change;
                outcome.Change = Workbook.Sheets.Contains(done.Sheet) ? change.For(done.Sheet) : null;
                break;
            }
            case "reopen":
                Workbook = argument switch
                {
                    // One Sheet saved as a Sheet Document's JSON, read back, and opened as a Workbook of that one Sheet.
                    JsonObject { Count: 1 } one when one["sheet"] is { } name => Workbook.Open(SheetDocument.FromJson(Workbook.ToDocument((string)name!).ToJson())),
                    // A document's JSON as the case writes it, read and opened.
                    JsonObject { Count: 1 } one when one["sheetJson"] is { } json => Workbook.Open(SheetDocument.FromJson((string)json!)),
                    JsonObject { Count: 1 } one when one["workbookJson"] is { } json => Workbook.Open(WorkbookDocument.FromJson((string)json!)),
                    JsonObject other => throw new InvalidDataException($"{other.ToJsonString()} is no way to reopen a Workbook."),
                    _ => (string)argument! switch
                    {
                        "document" => Workbook.Open(Workbook.ToDocument()),
                        "json" => Workbook.Open(WorkbookDocument.FromJson(Workbook.ToDocument().ToJson())),
                        var other => throw new InvalidDataException($"'{other}' is no way to reopen a Workbook."),
                    },
                };
                outcome.Sheet = Workbook.Shown;
                break;
            case "remember":
                _pictures[(string)argument!] = Picture();
                break;
            case "read":
                if (argument.GetValueKind() != JsonValueKind.True)
                    throw new InvalidDataException("'read' takes true.");
                break;
            case "copy":
            {
                var copy = argument.AsObject();
                _copies[(string)copy["as"]!] = sheet.Copy(CellRange.Parse((string)copy["range"]!));
                break;
            }
            case "setNumberFormat":
                foreach (var (at, code) in argument.AsObject())
                    outcome.Change = sheet.SetNumberFormat(CellRange.Parse(at), code is null ? null : NumberFormat.Parse((string)code!));
                break;
            case "setAlignment":
                foreach (var (at, alignment) in argument.AsObject())
                    outcome.Change = sheet.SetAlignment(CellRange.Parse(at), SheetCases.Enum<HorizontalAlignment>(alignment!));
                break;
            case "setColumnWidth":
                // The Sheet's own forms, outside any step.
                if (!argument.AsObject().TryGetPropertyValue("width", out var setWidth))
                    throw new InvalidDataException("setColumnWidth names no width: write null for the default.");
                outcome.Change = sheet.SetColumnWidth(CellRange.Parse((string)argument["columns"]!), setWidth is null ? null : SheetCases.Number(setWidth));
                break;
            case "setAutomaticColumnWidth":
                outcome.Change = sheet.SetAutomaticColumnWidth(CellRange.Parse((string)argument["columns"]!), SheetCases.Number(argument["width"]!));
                break;
            case "rename":
                // The Sheet's own rename, outside any step.
                outcome.Change = sheet.Rename((string)argument!);
                break;
            case "retype":
                // Each cell's Entry text, as the Cell Editor opens it, typed back into the cell.
                foreach (var cell in argument!.AsArray())
                {
                    var address = CellAddress.Parse((string)cell!);
                    outcome.Change = sheet.Enter(address, sheet.GetEntryText(address));
                }
                break;
            case "setCellFormat":
                // The Sheet's own form, outside any step.
                outcome.Change = sheet.SetCellFormat([.. argument["ranges"]!.AsArray().Select(r => CellRange.Parse((string)r!))], SheetCases.ChangeOf(argument["change"]!));
                break;
            case "declareTable":
            {
                var table = argument.AsObject();
                foreach (var (part, _) in table)
                {
                    if (part is not ("name" or "columns" or "key"))
                        throw new InvalidDataException($"'{part}' is no part of a Linked Table declared: name, columns and key.");
                }
                var columns = table["columns"]!.AsArray().Select(c => (string)c!).ToList();
                var change = table["key"] switch
                {
                    null => Workbook.DeclareLinkedTable((string)table["name"]!, columns),
                    JsonArray key => Workbook.DeclareLinkedTable((string)table["name"]!, columns, [.. key.Select(k => (string)k!)]),
                    var key => Workbook.DeclareLinkedTable((string)table["name"]!, columns, (string?)key),
                };
                outcome.Change = change.For(sheet);
                break;
            }
            case "pushTable":
            {
                var table = argument.AsObject();
                var rows = table["rows"]!.AsArray().Select(r => (IReadOnlyList<Value?>)[.. r!.AsArray().Select(SheetCases.Decode)]).ToList();
                // The rows as an adapter's wire carries them (ADR-0370 section 1): the case's own text read by
                // SheetWire, which must read what the case means and write what it reads.
                var wired = SheetWire.ReadRows(table["rows"]!.ToJsonString());
                if (SheetWire.WriteRows(wired) != SheetWire.WriteRows(rows))
                    throw new InvalidDataException($"SheetWire reads the rows {table["rows"]!.ToJsonString()} as {SheetWire.WriteRows(wired)}.");
                outcome.WorkbookChange = Workbook.PushLinkedTable((string)table["name"]!, wired);
                outcome.Change = outcome.WorkbookChange.For(sheet);
                break;
            }
            case "setToday":
                outcome.Change = Workbook.SetToday(argument is null ? null : DateOnly.ParseExact((string)argument!, "yyyy-MM-dd", CultureInfo.InvariantCulture)).For(sheet);
                break;
            case "setSeed":
            {
                // A seed, or {"seed", "changes"}: the seed and the count of changes together.
                static ulong? Seed(JsonNode? node) => node is null ? null : ulong.Parse(node.ToJsonString().Trim('"'), CultureInfo.InvariantCulture);
                outcome.Change = (argument is JsonObject both
                    ? Workbook.SetSeed(both.TryGetPropertyValue("seed", out var seed) ? Seed(seed) : throw new InvalidDataException("setSeed names no seed: write null for none."), both["changes"]!.GetValue<long>())
                    : Workbook.SetSeed(Seed(argument))).For(sheet);
                break;
            }
            case "getDisplay":
                // A display asked for, so that a width refused is a refusal the step can expect.
                _ = sheet.GetDisplay(CellAddress.Parse((string)argument["at"]!), SheetCases.Number(argument["width"]!));
                break;
            case "setClock":
            {
                // A clock that moves on `step` seconds each time it is read, and counts its reads.
                var clock = argument.AsObject();
                var at = DateTime.ParseExact((string)clock["from"]!, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                // A finite number of seconds, as the TS runner reads it: no spelled non-finite step.
                var seconds = clock["step"] is JsonValue stepNode && stepNode.GetValueKind() == JsonValueKind.Number
                    ? stepNode.GetValue<double>()
                    : throw new InvalidDataException("setClock.step is a number of seconds.");
                Workbook.NowSource = () =>
                {
                    _clockReads++;
                    at = at.AddSeconds(seconds);
                    return at;
                };
                break;
            }
            case "recalculateVolatile":
                outcome.Change = Workbook.RecalculateVolatile().For(sheet);
                break;
            case "show":
                // The Sheet shown, by its name.
                Workbook.Shown = Workbook.Find((string)argument!) ?? throw new InvalidDataException($"The Workbook has no Sheet '{(string)argument!}' to show.");
                outcome.Sheet = Workbook.Shown;
                break;
            default:
                throw new InvalidDataException($"'{verb}' is no step.");
        }
    }

    private WorkbookStep Done(JsonNode argument)
    {
        if (argument.GetValueKind() == JsonValueKind.String && (string)argument! == "last")
            return _done.Count > 0 ? _done[^1] : throw new InvalidDataException("No step has been done.");
        var index = argument.GetValue<int>();
        return index >= 0 && index < _done.Count ? _done[index] : throw new InvalidDataException($"No step {index} has been done.");
    }

    /// <summary>
    /// A Sheet Edit as a case writes one: an object of one key, the factory, and its arguments, done as the edit
    /// <see cref="SheetWire"/> writes it reads back. The Script Core reads that text, so every case holds the writer
    /// to the vocabulary: an edit whose text left out what it was made with would do something else here and fail
    /// its expectations (ADR-0370 section 1).
    /// </summary>
    private SheetEdit Edit(JsonNode node)
    {
        var wire = SheetWire.Write(EditAsWritten(node));
        var again = EditAsWritten(JsonNode.Parse(wire)!);
        var rewritten = SheetWire.Write(again);
        if (rewritten != wire) throw new InvalidDataException($"The edit's text {wire} reads back as {rewritten}.");
        return again;
    }

    /// <summary>An edit's text exactly as <see cref="SheetWire"/> writes it: the case says the text, and the Script Core reads that text.</summary>
    private SheetEdit WiredEdit(string wire)
    {
        var edit = EditAsWritten(JsonNode.Parse(wire)!);
        var written = SheetWire.Write(edit);
        if (written != wire) throw new InvalidDataException($"SheetWire writes the edit as {written}, not as the case's {wire}.");
        return edit;
    }

    private SheetEdit EditAsWritten(JsonNode node)
    {
        var edit = node.AsObject();
        if (edit.Count != 1)
            throw new InvalidDataException($"An edit names one operation: {edit.ToJsonString()}.");
        var (name, argument) = (edit.First().Key, edit.First().Value!);
        switch (name)
        {
            case "enter":
                return SheetEdit.Enter([.. argument.AsObject().Select(p => new KeyValuePair<CellAddress, string>(CellAddress.Parse(p.Key), SheetCases.Typed(p.Value)))]);
            case "setEntries":
                return SheetEdit.SetEntries([.. argument.AsObject().Select(p => new KeyValuePair<CellAddress, Entry?>(CellAddress.Parse(p.Key), Entry(p.Value)))]);
            case "setNumberFormat":
            {
                var format = argument["format"] is { } code ? NumberFormat.Parse((string)code!) : null;
                return argument["at"] is JsonArray cells
                    ? SheetEdit.SetNumberFormat([.. cells.Select(c => CellAddress.Parse((string)c!))], format)
                    : SheetEdit.SetNumberFormat(CellRange.Parse((string)argument["at"]!), format);
            }
            case "setAlignment":
            {
                var alignment = SheetCases.Enum<HorizontalAlignment>(argument["alignment"]!);
                return argument["at"] is JsonArray cells
                    ? SheetEdit.SetAlignment([.. cells.Select(c => CellAddress.Parse((string)c!))], alignment)
                    : SheetEdit.SetAlignment(CellRange.Parse((string)argument["at"]!), alignment);
            }
            case "setCellFormat":
                return SheetEdit.SetCellFormat([.. argument["ranges"]!.AsArray().Select(r => CellRange.Parse((string)r!))], SheetCases.ChangeOf(argument["change"]!));
            case "insertRows":
                return SheetEdit.InsertRows(Int(argument, 0), Int(argument, 1));
            case "deleteRows":
                return SheetEdit.DeleteRows(Int(argument, 0), Int(argument, 1));
            case "insertColumns":
                return SheetEdit.InsertColumns(Int(argument, 0), Int(argument, 1));
            case "deleteColumns":
                return SheetEdit.DeleteColumns(Int(argument, 0), Int(argument, 1));
            case "setColumnWidth":
                return SheetEdit.SetColumnWidth(CellRange.Parse((string)argument["columns"]!), argument["width"] is { } width ? SheetCases.Number(width) : null);
            case "setAutomaticColumnWidth":
                return SheetEdit.SetAutomaticColumnWidth(CellRange.Parse((string)argument["columns"]!), SheetCases.Number(argument["width"]!));
            case "fill":
            case "fillCopy":
            {
                var source = CellRange.Parse((string)argument["source"]!);
                var target = CellRange.Parse((string)argument["target"]!);
                var direction = SheetCases.Enum<FillDirection>(argument["direction"]!);
                return name == "fill" ? SheetEdit.Fill(source, target, direction) : SheetEdit.FillCopy(source, target, direction);
            }
            case "paste":
            {
                var block = argument["block"] is { } written ? Block(written)
                    : _copies[(string)argument["copy"]!].Block ?? throw new InvalidDataException("The copy pasted was refused.");
                return argument["at"] switch
                {
                    JsonArray targets => SheetEdit.Paste(block, [.. targets.Select(t => CellRange.Parse((string)t!))]),
                    var at when ((string)at!).Contains(':', StringComparison.Ordinal) => SheetEdit.Paste(block, CellRange.Parse((string)at!)),
                    var at => SheetEdit.Paste(block, CellAddress.Parse((string)at!)),
                };
            }
            case "pasteText":
                return SheetEdit.PasteText([.. argument["fields"]!.AsArray().Select(r => (IReadOnlyList<string>)[.. r!.AsArray().Select(f => (string)f!)])],
                    CellAddress.Parse((string)argument["at"]!));
            case "pasteFields":
            {
                // The Script Core's name for the two forms of PasteText that take fields cell by cell.
                List<KeyValuePair<CellAddress, string>> typed = [.. argument["typed"]!.AsObject().Select(p => new KeyValuePair<CellAddress, string>(CellAddress.Parse(p.Key),
                    p.Value is JsonValue field && field.GetValueKind() == JsonValueKind.String ? (string)field! : throw new InvalidDataException($"'{p.Value?.ToJsonString()}' is no typed field.")))];
                return argument.AsObject().TryGetPropertyValue("entries", out var entries)
                    ? SheetEdit.PasteText(typed, [.. (entries as JsonObject ?? throw new InvalidDataException("'entries' is an object of cells.")).Select(p => new KeyValuePair<CellAddress, Entry?>(CellAddress.Parse(p.Key), Entry(p.Value)))])
                    : SheetEdit.PasteText(typed);
            }
            case "enterInto":
                return SheetEdit.EnterInto([.. argument["targets"]!.AsArray().Select(t => CellRange.Parse((string)t!))],
                    CellAddress.Parse((string)argument["at"]!), (string)argument["typed"]!);
            case "rename":
                return SheetEdit.Rename((string)argument!);
            case "insertSheet":
                return SheetEdit.InsertSheet((string?)argument["name"], argument["before"] is { } before && (bool)before);
            case "deleteSheet":
                return SheetEdit.DeleteSheet();
            case "moveSheet":
                return SheetEdit.MoveSheet(argument.GetValue<int>());
            case "copySheet":
                return SheetEdit.CopySheet(argument.GetValueKind() == JsonValueKind.Number ? argument.GetValue<int>() : null);
            default:
                throw new InvalidDataException($"'{name}' is no Sheet Edit.");
        }
    }

    /// <summary>A block copied inside ExSheet as its text writes one: the range it came from, and each cell as copied, rows of columns.</summary>
    private SheetBlock Block(JsonNode node)
    {
        var source = CellRange.Parse((string)node["source"]!);
        var rows = node["cells"]!.AsArray();
        if (rows.Count != source.RowCount || rows.Any(r => r!.AsArray().Count != source.ColumnCount))
            throw new InvalidDataException($"{node.ToJsonString()} is no block of {source}'s shape.");
        return new SheetBlock(source, [.. rows.SelectMany(r => r!.AsArray()).Select(c => c!.AsObject()).Select(cell => new CellState(
            cell.ContainsKey("entry") ? Entry(cell["entry"]) : null,
            null,
            cell["numberFormat"] is { } code ? NumberFormat.Parse((string)code!) : null,
            cell["alignment"] is { } alignment ? SheetCases.Enum<HorizontalAlignment>(alignment) : null,
            cell["font"] is JsonObject font
                ? new CellFont(SheetCases.ColourOf(font["colour"]!), (bool)font["bold"]!, (bool)font["italic"]!, (bool)font["underline"]!, (bool)font["strikethrough"]!)
                : null,
            cell["fill"] is { } fill ? SheetCases.FillOf(fill) : null,
            cell["borders"] is JsonObject sides
                ? new CellBorders(SheetCases.LineOf(sides["top"]!), SheetCases.LineOf(sides["bottom"]!), SheetCases.LineOf(sides["left"]!), SheetCases.LineOf(sides["right"]!))
                : null))]);
    }

    /// <summary>A row or column and a count, as <c>[2]</c> or <c>[2, 3]</c>; the count left out is one.</summary>
    private static int Int(JsonNode argument, int index)
    {
        var list = argument.AsArray();
        if (list.Count is < 1 or > 2) throw new InvalidDataException($"{argument.ToJsonString()} is no place and count.");
        return index < list.Count ? list[index]!.GetValue<int>() : 1;
    }

    private Entry? Entry(JsonNode? node) => node switch
    {
        null => null,
        JsonObject o when o["formula"] is { } formula => ExSheet.Engine.Entry.FromFormula((string)formula!),
        JsonObject o when o.ContainsKey("constant") => ExSheet.Engine.Entry.FromValue(SheetCases.Decode(o["constant"]) ?? throw new InvalidDataException("A constant is not blank.")),
        _ => throw new InvalidDataException($"{node.ToJsonString()} is no Entry."),
    };

    /// <summary>Everything the Workbook records and shows: its document, and every held cell's Value on every Sheet.</summary>
    private string Picture() =>
        Workbook.ToDocument().ToJson() + "\n" + string.Join("\n", Workbook.Sheets.SelectMany(s =>
            s.EntryAddresses.Select(a => $"{s.Name}!{a}={s.GetValue(a)}")));

    private void Read(string where, string key, JsonNode? expected, Outcome outcome)
    {
        if (key is "formulas" or "entryTexts")
            expected = SheetCases.Texts(expected);
        var sheet = outcome.Sheet;
        JsonNode? actual;
        switch (key)
        {
            case "values":
                actual = PerCell(expected, a => SheetCases.Encode(sheet.GetValue(a)));
                break;
            case "displays":
            {
                var displays = new JsonObject();
                foreach (var (address, wanted) in expected!.AsObject())
                {
                    var at = CellAddress.Parse(address);
                    var display = wanted?["width"] is { } width ? sheet.GetDisplay(at, SheetCases.Number(width)) : sheet.GetDisplay(at);
                    displays[address] = new JsonObject
                    {
                        ["width"] = wanted?["width"]?.DeepClone(),
                        ["text"] = display.Text,
                        ["cannotShow"] = display.CannotShow,
                        ["alignment"] = SheetCases.Camel(display.Alignment),
                        ["colour"] = display.Colour is { } colour ? SheetCases.Camel(colour) : null,
                        ["isNumber"] = display.IsNumber,
                    };
                }
                actual = displays;
                break;
            }
            case "texts":
                actual = PerCell(expected, a => sheet.GetDisplay(a).Text);
                break;
            case "fonts":
                actual = PerCell(expected, a => SheetCases.Font(sheet.GetFont(a)));
                break;
            case "fills":
                actual = PerCell(expected, a => SheetCases.Fill(sheet.GetFill(a)));
                break;
            case "borders":
                actual = PerCell(expected, a => SheetCases.Borders(sheet.GetBorders(a)));
                break;
            case "cellFormats":
                // The Cell Formats a range shows, each once, in the order of their JSON's text, as the TS runner orders them.
                // Each is read back through SheetWire first, which reads a Cell Format the Script Core writes in this shape
                // (ADR-0370 section 1), so every case holds that reader to it.
                actual = PerRange(expected, r => SheetCases.Sorted(sheet.GetCellFormats(r)
                    .Select(f => SheetCases.CellFormatJson(SheetWire.ReadCellFormat(SheetCases.CellFormatJson(f).ToJsonString())))));
                break;
            case "edgeLines":
                // The lines along each of a range's outer edges, each once, in the order of their JSON's text.
                actual = PerRange(expected, r => sheet.GetEdgeLines(r) is var lines ? new JsonObject
                {
                    ["top"] = SheetCases.Sorted(lines.Top.Select(SheetCases.Line)),
                    ["bottom"] = SheetCases.Sorted(lines.Bottom.Select(SheetCases.Line)),
                    ["left"] = SheetCases.Sorted(lines.Left.Select(SheetCases.Line)),
                    ["right"] = SheetCases.Sorted(lines.Right.Select(SheetCases.Line)),
                } : null);
                break;
            case "workbookJson":
                // The Workbook Document's JSON as written, which the two engines write alike.
                actual = Workbook.ToDocument().ToJson();
                break;
            case "documentJson":
                // The Sheet Document's JSON as written, which the two engines write alike.
                actual = Workbook.ToDocument(sheet.Name).ToJson();
                break;
            case "document":
                actual = SheetCases.Document(Workbook.ToDocument(sheet.Name));
                break;
            case "formulas":
                actual = PerCell(expected, a => sheet.GetEntry(a)?.Formula);
                break;
            case "entryTexts":
                actual = PerCell(expected, a => sheet.GetEntryText(a));
                break;
            case "entries":
                actual = PerCell(expected, a => SheetCases.EntryOf(sheet.GetEntry(a)));
                break;
            case "numberFormats":
                actual = PerCell(expected, a => sheet.GetNumberFormat(a).Code);
                break;
            case "alignments":
                actual = PerCell(expected, a => SheetCases.Camel(sheet.GetAlignment(a)));
                break;
            case "widthOnEntry":
                actual = PerCell(expected, a => sheet.GetWidthOnEntry(a));
                break;
            case "entryAddressesIn":
                // The cells of each range that hold an Entry, in row order and then column order.
                actual = PerRange(expected, r => SheetCases.Addresses(sheet.EntryAddressesIn(r).Order()));
                break;
            case "spilledFrom":
                actual = PerCell(expected, a => sheet.SpilledFrom(a)?.ToString());
                break;
            case "completions":
                // Per text being typed, its caret marked |, the Sheet's completion there: what it replaces and each candidate.
                actual = new JsonObject([.. expected!.AsObject().Select(p =>
                {
                    var caret = p.Key.IndexOf('|', StringComparison.Ordinal);
                    if (caret < 0 || p.Key.IndexOf('|', caret + 1) >= 0)
                        throw new InvalidDataException($"'{p.Key}' marks no caret: one | is the caret.");
                    var completion = sheet.Complete(p.Key.Remove(caret, 1), caret);
                    return KeyValuePair.Create(p.Key, (JsonNode?)(completion is null ? null : new JsonObject
                    {
                        ["start"] = completion.Start,
                        ["length"] = completion.Length,
                        ["candidates"] = new JsonArray([.. completion.Candidates.Select(c => (JsonNode)new JsonObject
                        {
                            ["name"] = c.Name,
                            ["kind"] = SheetCases.Camel(c.Kind),
                            ["insertText"] = c.InsertText,
                            ["description"] = c.Description,
                        })]),
                    }));
                })]);
                break;
            case "references":
                // Per text being edited, the References the Sheet answers in it: each span, and its cells or its Linked Table's column.
                actual = new JsonObject([.. expected!.AsObject().Select(p => KeyValuePair.Create(p.Key, (JsonNode?)new JsonArray([.. sheet.References(p.Key).Select(r => (JsonNode)new JsonObject
                {
                    ["start"] = r.Start,
                    ["length"] = r.Length,
                    ["cells"] = r.Cells?.ToString(),
                    ["linkedColumn"] = r.LinkedColumn is { } column ? new JsonObject { ["table"] = column.Table, ["column"] = column.Column } : null,
                })])))]);
                break;
            case "columnWidths":
            {
                var widths = new JsonObject();
                foreach (var (column, _) in expected!.AsObject())
                {
                    if (!CellAddress.TryParseColumn(column, out var index)) throw new InvalidDataException($"'{column}' is no column.");
                    widths[column] = sheet.GetColumnWidth(index) is { } width
                        ? new JsonObject { ["width"] = width.Width, ["kind"] = SheetCases.Camel(width.Kind), ["isCustom"] = width.IsCustom, ["isSetByUser"] = width.IsSetByUser }
                        : null;
                }
                actual = widths;
                break;
            }
            case "change":
                actual = outcome.Change is { } change ? ChangeJson(change) : null;
                break;
            case "changes":
            {
                // What a step, an undo or a redo changed on each Sheet it reached, by the Sheet's name.
                var changes = outcome.WorkbookChange ?? throw new InvalidDataException($"{where}: only a step, an undo or a redo reads its changes.");
                var each = new JsonObject();
                foreach (var reached in changes.Sheets) each[reached.Name] = ChangeJson(changes.For(reached));
                actual = each;
                break;
            }
            case "refusal":
                if (!outcome.Checked) throw new InvalidDataException($"{where}: only a check reads a refusal.");
                actual = outcome.Refusal is { } refusal
                    ? new JsonObject { ["reason"] = SheetCases.Camel(refusal.Reason), ["saying"] = refusal.Message }
                    : null;
                break;
            case "clockReads":
                actual = _clockReads - outcome.ClockReadsBefore;
                break;
            case "stepSheet":
                // The Sheet a step was done on, or made: an inserted or copied Sheet is its step's.
                actual = outcome.Step?.Sheet.Name ?? throw new InvalidDataException($"{where}: no step to read.");
                break;
            case "changesSheets":
                actual = outcome.WorkbookChange?.ChangesSheets ?? throw new InvalidDataException($"{where}: only a step, an undo or a redo reads whether it changed the Sheets.");
                break;
            case "isUndone":
                actual = (outcome.Step ?? throw new InvalidDataException($"{where}: no step to read.")).IsUndone;
                break;
            case "ranges":
                actual = new JsonArray([.. (outcome.Step ?? throw new InvalidDataException($"{where}: no step to read.")).Ranges.Select(r => (JsonNode)r.ToString())]);
                break;
            case "picture":
                actual = _pictures.TryGetValue((string)expected!, out var picture) && picture == Picture() ? expected!.DeepClone() : "a different picture";
                break;
            case "pictureNot":
                actual = _pictures.TryGetValue((string)expected!, out var other) && other != Picture() ? expected!.DeepClone() : "the same picture";
                break;
            case "entryAddresses":
                actual = SheetCases.Addresses(sheet.EntryAddresses);
                break;
            case "valueAddresses":
                actual = SheetCases.Addresses(sheet.ValueAddresses);
                break;
            case "alike":
            {
                // Groups of cells whose Values are one another's, a refusal alike only to a refusal.
                var groups = new JsonArray();
                foreach (var group in expected!.AsArray())
                {
                    var values = group!.AsArray().Select(c => SheetCases.Encode(sheet.GetValue(CellAddress.Parse((string)c!)))?.ToJsonString() ?? "null").ToList();
                    groups.Add(values.Distinct().Count() == 1 ? group.DeepClone() : (JsonNode)$"unlike: {string.Join(", ", values)}");
                }
                actual = groups;
                break;
            }
            case "recalculationPasses":
                actual = sheet.RecalculationPasses;
                break;
            case "sheets":
                actual = new JsonArray([.. Workbook.Sheets.Select(s => (JsonNode)s.Name)]);
                break;
            case "shown":
                actual = Workbook.Shown.Name;
                break;
            case "sheetsReached":
                actual = new JsonArray([.. (outcome.WorkbookChange ?? throw new InvalidDataException($"{where}: only a step, an undo, a redo or a push reads the Sheets it reached.")).Sheets.Select(s => (JsonNode)s.Name)]);
                break;
            case "tableColumns":
                actual = ColumnsJson((outcome.Table ?? throw new InvalidDataException($"{where}: only readTable reads a Linked Table's columns.")).Columns);
                break;
            case "heldTables":
                // Each snapshot readTable held, as it stands now.
                actual = new JsonObject([.. expected!.AsObject().Select(p => new KeyValuePair<string, JsonNode?>(p.Key,
                    ColumnsJson(_tables.TryGetValue(p.Key, out var held) ? held : throw new InvalidDataException($"{where}: no snapshot was held as '{p.Key}'."))))]);
                break;
            case "copies":
            {
                var each = new JsonObject();
                foreach (var (name, _) in expected!.AsObject())
                {
                    var copy = _copies.TryGetValue(name, out var taken) ? taken : throw new InvalidDataException($"{where}: no copy was taken as '{name}'.");
                    each[name] = new JsonObject
                    {
                        ["refusal"] = copy.Refusal is { } refused ? SheetCases.Camel(refused.Reason) : null,
                        ["saying"] = copy.Refusal?.Message,
                        ["text"] = copy.Text,
                        ["html"] = copy.Html,
                        ["block"] = copy.Block is { } block
                            ? new JsonArray([.. Enumerable.Range(0, block.RowCount).Select(r =>
                                (JsonNode)new JsonArray([.. Enumerable.Range(0, block.ColumnCount).Select(c => SheetCases.EntryOf(block.EntryAt(r, c)))]))])
                            : null,
                    };
                }
                actual = each;
                break;
            }
            case "workbook":
                actual = new JsonObject
                {
                    ["culture"] = Workbook.Culture.Name,
                    ["today"] = Workbook.Today?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["hasVolatileFormulas"] = Workbook.HasVolatileFormulas,
                    ["changes"] = Workbook.Changes,
                    ["seed"] = Workbook.Seed?.ToString(CultureInfo.InvariantCulture),
                    ["linkedTables"] = new JsonArray([.. Workbook.LinkedTables.Select(t => (JsonNode)new JsonObject
                    {
                        ["name"] = t.Name,
                        ["columns"] = new JsonArray([.. t.Columns.Select(c => (JsonNode)c)]),
                        ["keyColumns"] = new JsonArray([.. t.KeyColumns.Select(c => (JsonNode)c)]),
                        ["key"] = t.Key,
                        ["isWaiting"] = t.IsWaiting,
                        ["rowCount"] = t.RowCount,
                    })]),
                };
                break;
            default:
                throw new InvalidDataException($"{where}: '{key}' is no reading.");
        }
        SheetCases.Compare($"{where}.{key}", expected, actual, Differences);
    }

    private static JsonObject PerRange(JsonNode? expected, Func<CellRange, JsonNode?> read)
    {
        var answer = new JsonObject();
        foreach (var (range, _) in expected!.AsObject())
            answer[range] = read(CellRange.Parse(range));
        return answer;
    }

    private static JsonObject PerCell(JsonNode? expected, Func<CellAddress, JsonNode?> read)
    {
        var answer = new JsonObject();
        foreach (var (address, _) in expected!.AsObject())
            answer[address] = read(CellAddress.Parse(address));
        return answer;
    }
}
