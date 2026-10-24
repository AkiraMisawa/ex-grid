using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The shared cases of kind <c>calls</c> (ADR-0350, section 5), run against the C# core: a subject made
/// from named arguments, and what its members answer. <c>tests/cases/README.md</c> says what a file
/// holds. Members are found by name, so a case names a member as the Script Core spells it (camelCase)
/// and the C# member is its PascalCase; a result is compared as the JSON it serialises to.
/// </summary>
public class CallCaseTests
{
    /// <summary>The subjects a case may name, by the name it uses.</summary>
    private static readonly Dictionary<string, Type> Subjects = new()
    {
        ["RowRange"] = typeof(RowRange),
        ["ViewportBox"] = typeof(ViewportBox),
        ["ViewportGeometry"] = typeof(ViewportGeometry),
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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
        IEnumerable<MethodBase> candidates = via is null
            ? type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            : type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == Pascal(via));
        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            if (arguments.Keys.Any(key => parameters.All(p => p.Name != key)))
                continue;
            if (parameters.Any(p => !arguments.ContainsKey(p.Name!) && !p.HasDefaultValue))
                continue;
            var values = parameters
                .Select(p => arguments.TryGetValue(p.Name!, out var value) ? Convert(value, p.ParameterType) : p.DefaultValue)
                .ToArray();
            return candidate is ConstructorInfo constructor
                ? constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, values, CultureInfo.InvariantCulture)
                : candidate.Invoke(null, BindingFlags.DoNotWrapExceptions, null, values, CultureInfo.InvariantCulture)!;
        }
        throw new InvalidOperationException(
            $"{type.Name} has no {(via is null ? "constructor" : $"factory {via}")} taking exactly {string.Join(", ", arguments.Keys)}.");
    }

    /// <summary>One check: a member read or called, and what it answers or how it is refused.</summary>
    private static void Holds(Type type, object? subject, JsonElement check)
    {
        var isStatic = check.TryGetProperty("static", out var member);
        if (!isStatic && !check.TryGetProperty("get", out member) && !check.TryGetProperty("call", out member))
            throw new InvalidOperationException($"The check {check} names no member: get, call or static.");
        if (!isStatic && subject is null)
            throw new InvalidOperationException($"The check {check} reads an instance, and the case creates none.");
        var args = check.TryGetProperty("args", out var given) ? [.. given.EnumerateArray()] : (JsonElement[]?)null;
        var label = $"{member.GetString()}({(args is null ? "" : string.Join(", ", args.Select(a => a.ToString())))})";

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

    private static object? Invoke(Type type, object? subject, string name, JsonElement[]? args)
    {
        var flags = BindingFlags.Public | (subject is null ? BindingFlags.Static : BindingFlags.Instance);
        if (args is null)
        {
            if (type.GetProperty(name, flags) is { } property)
                return property.GetValue(subject);
            if (type.GetField(name, flags) is { } field)
                return field.GetValue(subject);
            throw new InvalidOperationException($"{type.Name} has no {(subject is null ? "static " : "")}property {name}.");
        }
        var method = type.GetMethods(flags).SingleOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length)
            ?? throw new InvalidOperationException($"{type.Name} has no {(subject is null ? "static " : "")}method {name} of {args.Length} argument(s).");
        var values = method.GetParameters().Select((p, i) => Convert(args[i], p.ParameterType)).ToArray();
        return method.Invoke(subject, BindingFlags.DoNotWrapExceptions, null, values, CultureInfo.InvariantCulture);
    }

    /// <summary>A JSON value as the parameter's type. A non-finite number is written as the string
    /// <c>"Infinity"</c>, <c>"-Infinity"</c> or <c>"NaN"</c>.</summary>
    private static object? Convert(JsonElement value, Type type) => JsonSerializer.Deserialize(value.GetRawText(), type, Json);

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
            (JsonObject e, JsonObject a) => e.Count == a.Count && e.All(p => a.TryGetPropertyValue(p.Key, out var v) && Same(p.Value, v)),
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
