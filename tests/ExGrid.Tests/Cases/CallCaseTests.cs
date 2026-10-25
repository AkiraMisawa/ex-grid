using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The shared cases of kind <c>calls</c> (ADR-0350, section 5), run against the C# core: a subject made
/// from named arguments, and what its members answer. <c>tests/cases/README.md</c> says what a file
/// holds. Members are found by name, so a case names a member as the Script Core spells it (camelCase)
/// and the C# member is its PascalCase; a result is compared as the JSON it serialises to. A case reads
/// what the core's own code can, public and internal members, and never a private one.
/// </summary>
public class CallCaseTests
{
    /// <summary>The subjects a case may name, by the name it uses.</summary>
    private static readonly Dictionary<string, Type> Subjects = new()
    {
        ["CellTextMetrics"] = typeof(CellTextMetrics),
        ["CompletionCandidate"] = typeof(ExGrid.Cells.CompletionCandidate),
        ["EditorCompletion"] = typeof(ExGrid.Cells.EditorCompletion),
        ["EditorHint"] = typeof(ExGrid.Cells.EditorHint),
        ["EditorReference"] = typeof(ExGrid.Cells.EditorReference),
        ["EditorTextRules"] = typeof(ExGrid.Cells.EditorTextRules),
        ["ClipboardParse"] = typeof(ExGrid.Clipboard.ClipboardParse),
        ["AutoWidth"] = typeof(AutoWidth),
        ["ColumnGeometry"] = typeof(ColumnGeometry),
        ["ColumnWidth"] = typeof(ColumnWidth),
        ["ColumnWidthSpec"] = typeof(ColumnWidthSpec),
        ["GlyphWidthTable"] = typeof(GlyphWidthTable),
        ["GridEditingKeys"] = typeof(ExGrid.Keys.GridEditingKeys),
        ["HeaderReorder"] = typeof(HeaderReorder),
        ["GridKeys"] = typeof(ExGrid.Keys.GridKeys),
        ["GridMetrics"] = typeof(GridMetrics),
        ["GridPresentationDefaults"] = typeof(GridPresentationDefaults),
        ["HeaderGroup"] = typeof(HeaderGroup),
        ["HeaderGroupLayout"] = typeof(HeaderGroupLayout),
        ["MenuKeys"] = typeof(ExGrid.Chrome.MenuKeys),
        ["OverflowRules"] = typeof(OverflowRules),
        ["ReferenceColour"] = typeof(ExGrid.Cells.ReferenceColour),
        ["ReferenceColouring"] = typeof(ExGrid.Cells.ReferenceColouring),
        ["RowRange"] = typeof(RowRange),
        ["ViewportBox"] = typeof(ViewportBox),
        ["ViewportGeometry"] = typeof(ViewportGeometry),
    };

    private const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic;

    private static bool Reachable(MethodBase member) => member.IsPublic || member.IsAssembly || member.IsFamilyOrAssembly;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new CommandFromCase(), new TupleAsArray(), new RangeAsCase(), new ColourAsPlace() },
    };

    /// <summary>A menu's command as a case writes it, <c>{"id": …, "enabled": …}</c>: what the menu's keys
    /// read of it. A case never runs one.</summary>
    private sealed class CommandFromCase : JsonConverter<ExGrid.Chrome.GridCommand>
    {
        public override ExGrid.Chrome.GridCommand Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var given = JsonElement.ParseValue(ref reader);
            return new(given.GetProperty("id").GetString()!, given.GetProperty("enabled").GetBoolean(),
                () => throw new InvalidOperationException("A case's command is never run."));
        }

        public override void Write(Utf8JsonWriter writer, ExGrid.Chrome.GridCommand value, JsonSerializerOptions options)
            => throw new NotSupportedException("A command is an argument, never an answer.");
    }

    /// <summary>A range as a case writes it and the Script Core holds one, its four numbers: the derived
    /// members C# serialises besides (<c>bottomRow</c>, <c>cellCount</c>, …) are none of the case's.</summary>
    private sealed class RangeAsCase : JsonConverter<ExGrid.Selection.SelectionRange>
    {
        public override ExGrid.Selection.SelectionRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var given = JsonElement.ParseValue(ref reader);
            return new(given.GetProperty("topRow").GetInt32(), given.GetProperty("leftColumn").GetInt32(),
                given.GetProperty("rowCount").GetInt32(), given.GetProperty("columnCount").GetInt32());
        }

        public override void Write(Utf8JsonWriter writer, ExGrid.Selection.SelectionRange value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("topRow", value.TopRow);
            writer.WriteNumber("leftColumn", value.LeftColumn);
            writer.WriteNumber("rowCount", value.RowCount);
            writer.WriteNumber("columnCount", value.ColumnCount);
            writer.WriteEndObject();
        }
    }

    /// <summary>A Reference colour as its place in the palette, the number the Script Core's colour is.</summary>
    private sealed class ColourAsPlace : JsonConverter<ExGrid.Cells.ReferenceColour>
    {
        public override ExGrid.Cells.ReferenceColour Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, ExGrid.Cells.ReferenceColour value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.Place);
    }

    /// <summary>A tuple answered as the array of its elements, as the Script Core answers one: System.Text.Json
    /// would write it as an empty object, since its elements are fields.</summary>
    private sealed class TupleAsArray : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsGenericType && typeToConvert.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            => (JsonConverter)Activator.CreateInstance(typeof(Elements<>).MakeGenericType(typeToConvert))!;

        private sealed class Elements<T> : JsonConverter<T>
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException("A tuple argument is read element by element.");

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();
                foreach (var field in typeof(T).GetFields())
                    JsonSerializer.Serialize(writer, field.GetValue(value), field.FieldType, options);
                writer.WriteEndArray();
            }
        }
    }

    public static TheoryData<string, string, string> Cases()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var area in Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "cases")).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(area);
            foreach (var file in SharedCases.FilesOf(name, "calls"))
                foreach (var entry in SharedCases.Read(name, file).GetProperty("cases").EnumerateArray())
                    rows.Add(name, file, entry.GetProperty("name").GetString()!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a calls case holds on the C# core
    public void Calls(string area, string file, string name)
    {
        var (document, entry) = SharedCases.Find(area, file, name);
        var subjectName = document.GetProperty("subject").GetString()!;
        if (!Subjects.TryGetValue(subjectName, out var type))
            throw new InvalidOperationException($"\"{subjectName}\" is not a subject this runner knows.");

        object? subject = null;
        if (entry.TryGetProperty("create", out var create))
        {
            var arguments = Merged(document.TryGetProperty("defaults", out var defaults) ? defaults : default, create);
            var via = entry.TryGetProperty("via", out var factory) ? factory.GetString() : null;
            if (entry.TryGetProperty("throws", out var refusal))
            {
                Refused(() => Create(type, via, arguments), refusal.GetString()!, Saying(entry));
                Assert.False(entry.TryGetProperty("expect", out _), $"The case \"{name}\" is refused at its creation, so it can expect nothing after it.");
                return;
            }
            subject = Create(type, via, arguments);
        }

        var checks = entry.GetProperty("expect");
        Assert.NotEqual(0, checks.GetArrayLength());
        foreach (var check in checks.EnumerateArray())
            Holds(type, subject, check);
    }

    /// <summary>The file's defaults with the case's own arguments over them.</summary>
    private static Dictionary<string, JsonElement> Merged(JsonElement defaults, JsonElement own)
    {
        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (defaults.ValueKind == JsonValueKind.Object)
            foreach (var property in defaults.EnumerateObject())
                merged[property.Name] = property.Value;
        foreach (var property in own.EnumerateObject())
            merged[property.Name] = property.Value;
        return merged;
    }

    /// <summary>
    /// The subject, made by the constructor (or, with <c>via</c>, the static factory) whose parameters are
    /// exactly the arguments named, less those it gives a default. An argument no parameter takes, or a
    /// parameter with no default that no argument names, is a defect in the case.
    /// </summary>
    private static object Create(Type type, string? via, Dictionary<string, JsonElement> arguments)
    {
        // A value the type holds, such as ColumnWidth.Auto, is made through no arguments at all.
        if (via is not null && arguments.Count == 0
            && type.GetProperty(Pascal(via), Visible | BindingFlags.Static) is { GetMethod: { } held } && Reachable(held))
            return held.Invoke(null, BindingFlags.DoNotWrapExceptions, null, null, CultureInfo.InvariantCulture)!;
        IEnumerable<MethodBase> candidates = via is null
            ? type.GetConstructors(Visible | BindingFlags.Instance).Where(Reachable)
            : type.GetMethods(Visible | BindingFlags.Static).Where(m => m.Name == Pascal(via) && Reachable(m));
        var (method, values) = Bind(candidates, arguments)
            ?? throw new InvalidOperationException(
                $"{type.Name} has no {(via is null ? "constructor" : $"factory {via}")} taking exactly {string.Join(", ", arguments.Keys)}.");
        return method is ConstructorInfo constructor
            ? constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, values, CultureInfo.InvariantCulture)
            : method.Invoke(null, BindingFlags.DoNotWrapExceptions, null, values, CultureInfo.InvariantCulture)!;
    }

    /// <summary>The one candidate the named arguments bind to, and the values they bind as: every argument
    /// names a parameter, and every parameter is named or has a default.</summary>
    private static (MethodBase Method, object?[] Values)? Bind(IEnumerable<MethodBase> candidates, Dictionary<string, JsonElement> arguments)
    {
        var bound = candidates
            .Where(candidate =>
            {
                var parameters = candidate.GetParameters();
                return arguments.Keys.All(key => parameters.Any(p => NameOf(p) == key))
                    && parameters.All(p => arguments.ContainsKey(NameOf(p)) || p.HasDefaultValue);
            })
            .ToArray();
        if (bound.Length > 1)
            throw new InvalidOperationException($"{string.Join(", ", arguments.Keys)} bind to {bound.Length} overloads; name the arguments of one.");
        if (bound.Length == 0)
            return null;
        var values = bound[0].GetParameters()
            .Select(p => arguments.TryGetValue(NameOf(p), out var value) ? Convert(value, p.ParameterType) : p.DefaultValue)
            .ToArray();
        return (bound[0], values);
    }

    /// <summary>A parameter's name as a case writes it: camelCase, which a record's positional parameter
    /// (<c>Text</c>) is not.</summary>
    private static string NameOf(ParameterInfo parameter) => char.ToLowerInvariant(parameter.Name![0]) + parameter.Name[1..];

    /// <summary>One check: a member read or called, and what it answers or how it is refused.</summary>
    private static void Holds(Type type, object? subject, JsonElement check)
    {
        var isStatic = check.TryGetProperty("static", out var member);
        if (!isStatic && !check.TryGetProperty("get", out member) && !check.TryGetProperty("call", out member))
            throw new InvalidOperationException($"The check {check} names no member: get, call or static.");
        if (!isStatic && subject is null)
            throw new InvalidOperationException($"The check {check} reads an instance, and the case creates none.");
        JsonElement? args = check.TryGetProperty("args", out var given) ? given : null;
        var label = args is { } shown ? $"{member.GetString()}({shown})" : member.GetString()!;

        object? read() => Invoke(type, isStatic ? null : subject, Pascal(member.GetString()!), args);
        if (check.TryGetProperty("throws", out var refusal))
        {
            Refused(() => read(), refusal.GetString()!, Saying(check));
            return;
        }
        var result = read();
        if (check.TryGetProperty("then", out var then))
        {
            if (result is null)
                throw new InvalidOperationException($"{label} answered nothing for \"then\" to read.");
            foreach (var next in then.EnumerateArray())
                Holds(result.GetType(), result, next);
            return;
        }
        var has = check.TryGetProperty("has", out var held);
        var lacks = check.TryGetProperty("lacks", out var missing);
        if (has || lacks)
        {
            if (JsonSerializer.SerializeToNode(result, Json) is not JsonArray members)
                throw new InvalidOperationException($"{label} answered no list for \"has\" or \"lacks\" to search.");
            if (has)
                foreach (var wanted in held.EnumerateArray())
                    Assert.True(members.Any(m => Same(JsonNode.Parse(wanted.GetRawText()), m)), $"{label}: expected it to hold {wanted}.");
            if (lacks)
                foreach (var unwanted in missing.EnumerateArray())
                    Assert.False(members.Any(m => Same(JsonNode.Parse(unwanted.GetRawText()), m)), $"{label}: expected it not to hold {unwanted}.");
            return;
        }
        var atLeast = check.TryGetProperty("atLeast", out var least);
        var atMost = check.TryGetProperty("atMost", out var most);
        if (atLeast || atMost)
        {
            // Only a number is bounded: Convert.ToDouble would make a null 0 and read a string, where the
            // Script Core's runner refuses both.
            if (result is not (double or float or int or long or decimal))
                throw new InvalidOperationException($"{label} answered no number to bound.");
            var got = System.Convert.ToDouble(result, CultureInfo.InvariantCulture);
            if (atLeast)
                Assert.True(got >= Number(JsonNode.Parse(least.GetRawText())!)!.Value, $"{label}: expected at least {least}, got {got}.");
            if (atMost)
                Assert.True(got <= Number(JsonNode.Parse(most.GetRawText())!)!.Value, $"{label}: expected at most {most}, got {got}.");
            return;
        }
        if (!check.TryGetProperty("is", out var expected))
            throw new InvalidOperationException($"The check {check} says neither what it is nor how it is refused.");
        if (check.TryGetProperty("near", out var near))
        {
            var want = Number(JsonNode.Parse(expected.GetRawText())!)!.Value;
            var got = System.Convert.ToDouble(result, CultureInfo.InvariantCulture);
            Assert.True(Math.Abs(got - want) <= near.GetDouble(), $"{label}: expected {want} within {near.GetDouble()}, got {got}.");
            return;
        }
        var actual = JsonSerializer.SerializeToNode(result, Json);
        Assert.True(Same(JsonNode.Parse(expected.GetRawText()), actual),
            $"{label}: expected {expected.GetRawText()}, got {actual?.ToJsonString() ?? "null"}.");
    }

    private static object? Invoke(Type type, object? subject, string name, JsonElement? args)
    {
        var flags = Visible | (subject is null ? BindingFlags.Static : BindingFlags.Instance);
        var kind = subject is null ? "static " : "";
        if (args is not { } given)
        {
            // Invoked rather than read with GetValue, which would wrap what a getter throws.
            if (type.GetProperty(name, flags) is { GetMethod: { } getter } && Reachable(getter))
                return getter.Invoke(subject, BindingFlags.DoNotWrapExceptions, null, null, CultureInfo.InvariantCulture);
            if (type.GetField(name, flags) is { } field && (field.IsPublic || field.IsAssembly || field.IsFamilyOrAssembly))
                return field.GetValue(subject);
            throw new InvalidOperationException($"{type.Name} has no {kind}property {name}.");
        }
        var methods = type.GetMethods(flags).Where(m => m.Name == name && Reachable(m));
        if (given.ValueKind == JsonValueKind.Object)
        {
            var named = given.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            var (method, values) = Bind(methods, named)
                ?? throw new InvalidOperationException($"{type.Name} has no {kind}method {name} taking exactly {string.Join(", ", named.Keys)}.");
            return method.Invoke(subject, BindingFlags.DoNotWrapExceptions, null, values, CultureInfo.InvariantCulture);
        }
        // Positional: the overload of that many parameters whose types take the arguments, where one name
        // has several (EstimatePx of a text or of a count).
        var positional = given.EnumerateArray().ToArray();
        var fits = methods
            .Where(m => m.GetParameters().Length == positional.Length)
            .Select(m => (Method: m, Values: TryConvert(positional, m.GetParameters())))
            .Where(fit => fit.Values is not null)
            .ToArray();
        if (fits.Length != 1)
            throw new InvalidOperationException($"{type.Name} has {fits.Length} {kind}methods {name} taking {given}; a check names one.");
        return fits[0].Method.Invoke(subject, BindingFlags.DoNotWrapExceptions, null, fits[0].Values, CultureInfo.InvariantCulture);
    }

    private static object?[]? TryConvert(JsonElement[] args, ParameterInfo[] parameters)
    {
        try
        {
            return [.. parameters.Select((p, i) => Convert(args[i], p.ParameterType))];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A JSON value as the parameter's type. A non-finite number is written as the string
    /// <c>"Infinity"</c>, <c>"-Infinity"</c> or <c>"NaN"</c>.</summary>
    /// <remarks>An object <c>{"new": subject, "with": {…}}</c> is that subject, made as a case makes one;
    /// a tuple is read from an array, and a collection of tuples or subjects element by element.</remarks>
    private static object? Convert(JsonElement value, Type type)
    {
        // The Script Core's runner has no parameter types to go by, so it reads these strings as numbers
        // wherever they stand; a case passing one as text would hold in one core and not the other.
        if (type == typeof(string) && value.ValueKind == JsonValueKind.String && value.GetString() is "Infinity" or "-Infinity" or "NaN")
            throw new InvalidOperationException($"{value} stands for a number in a case, and cannot be passed as text (tests/cases/README.md).");
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("new", out var subjectName))
        {
            var subject = Subjects[subjectName.GetString()!];
            var with = value.TryGetProperty("with", out var given) ? Merged(default, given) : [];
            var made = Create(subject, value.TryGetProperty("via", out var via) ? via.GetString() : null, with);
            if (value.TryGetProperty("after", out var after))
            {
                foreach (var step in after.EnumerateArray())
                {
                    if (!step.TryGetProperty("call", out var call) || !step.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Array)
                        throw new InvalidOperationException($"An \"after\" step {step} is a call with positional args.");
                    made = Invoke(made.GetType(), made, Pascal(call.GetString()!), args)
                        ?? throw new InvalidOperationException($"An \"after\" step {step} answered nothing.");
                }
            }
            return type.IsInstanceOfType(made) || Nullable.GetUnderlyingType(type)?.IsInstanceOfType(made) == true
                ? made
                : throw new JsonException($"A {subject.Name} is not a {type.Name}.");
        }
        if (type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            var items = value.EnumerateArray().ToArray();
            var parts = type.GetGenericArguments();
            if (items.Length != parts.Length)
                throw new JsonException($"{value} is not a tuple of {parts.Length}.");
            return Activator.CreateInstance(type, [.. items.Select((item, i) => Convert(item, parts[i]))]);
        }
        var element = ElementOf(type);
        if (element is not null && value.ValueKind == JsonValueKind.Array && NeedsReading(element))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
            foreach (var item in value.EnumerateArray())
                list.Add(Convert(item, element));
            if (!type.IsArray)
                return list;
            var array = Array.CreateInstance(element, list.Count);
            list.CopyTo(array, 0);
            return array;
        }
        return JsonSerializer.Deserialize(value.GetRawText(), type, Json);
    }

    private static Type? ElementOf(Type type) => type.IsArray
        ? type.GetElementType()
        : type.IsGenericType && type.GetGenericArguments().Length == 1 && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            ? type.GetGenericArguments()[0]
            : null;

    private static bool NeedsReading(Type element) =>
        (element.IsGenericType && element.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        || Subjects.ContainsValue(element);

    /// <summary>Whether two JSON values are the same: numbers by value (so 2000000 is 2000000.0, and
    /// <c>"Infinity"</c> is infinity), objects by every key, arrays in order.</summary>
    private static bool Same(JsonNode? expected, JsonNode? actual)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null;
        if (Number(expected) is { } want && Number(actual) is { } got)
            return want.Equals(got);
        return (expected, actual) switch
        {
            // A key one side lacks is null there: "none" is null in a case, and a language may leave it out.
            (JsonObject e, JsonObject a) => e.Select(p => p.Key).Union(a.Select(p => p.Key)).All(key =>
                Same(e.TryGetPropertyValue(key, out var want) ? want : null, a.TryGetPropertyValue(key, out var got) ? got : null)),
            (JsonArray e, JsonArray a) => e.Count == a.Count && e.Zip(a).All(pair => Same(pair.First, pair.Second)),
            (JsonValue e, JsonValue a) => e.ToJsonString() == a.ToJsonString(),
            _ => false,
        };
    }

    private static double? Number(JsonNode node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue<double>(out var number))
            return number;
        if (value.TryGetValue<JsonElement>(out var element) && element.ValueKind == JsonValueKind.Number)
            return element.GetDouble();
        if (value.TryGetValue<string>(out var text))
        {
            return text switch
            {
                "Infinity" => double.PositiveInfinity,
                "-Infinity" => double.NegativeInfinity,
                "NaN" => double.NaN,
                _ => null,
            };
        }
        return null;
    }

    private static string? Saying(JsonElement entry) => entry.TryGetProperty("saying", out var text) ? text.GetString() : null;

    /// <summary>
    /// The refusals of <see cref="SharedCases.Refused"/>, and <c>argument</c>: an argument of a kind the
    /// member does not take. With <c>saying</c>, the message holds that text, as the Script Core's does.
    /// </summary>
    private static void Refused(Action action, string refusal, string? saying)
    {
        if (refusal == "argument")
        {
            var thrown = Assert.ThrowsAny<ArgumentException>(action);
            Assert.IsNotType<ArgumentOutOfRangeException>(thrown);
            if (saying is not null)
                Assert.Contains(saying, thrown.Message, StringComparison.Ordinal);
            return;
        }
        SharedCases.Refused(action, refusal);
        if (saying is not null)
        {
            var thrown = Assert.ThrowsAny<Exception>(action);
            Assert.Contains(saying, thrown.Message, StringComparison.Ordinal);
        }
    }

    private static string Pascal(string name) => char.ToUpperInvariant(name[0]) + name[1..];
}
