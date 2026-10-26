using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ExSheet.Engine.Tests.Cases;

/// <summary>
/// The shared cases of kind <c>sheetCalls</c> (<c>tests/cases/README.md</c>; ADR-0370 sections 2 and 5): one call of
/// the engine's surface, its arguments, and what it answers or refuses, run against ExSheet.Engine as Vitest runs them
/// against the Script Core (<c>src/npm/sheet-engine/test/sheet-calls.ts</c>). A member is found by name, the case's
/// camelCase as C#'s PascalCase, among the overloads whose parameters the arguments convert to; a case reads what
/// the engine's own code can, public and internal members, and never a private one.
/// </summary>
public class SheetCallCaseTests
{
    /// <summary>The subjects a case may name, by the name it uses.</summary>
    private static readonly Dictionary<string, Type> Subjects = new()
    {
        ["CellAddress"] = typeof(CellAddress),
        ["CellRange"] = typeof(CellRange),
        ["DeclaredFunction"] = typeof(DeclaredFunction),
        ["FormulaEntry"] = typeof(FormulaEntry),
    };

    private const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic;

    private static bool Reachable(MethodBase member) => member.IsPublic || member.IsAssembly || member.IsFamilyOrAssembly;

    public static TheoryData<string, string> Calls()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (file, name) in SheetCases.Of("sheetCalls"))
            rows.Add(file, name);
        return rows;
    }

    [Theory, MemberData(nameof(Calls))] // ADR-0370: the engine's surface, called as the shared cases call it
    public void Sheet_call(string file, string name)
    {
        var (document, entry) = SheetCases.Find(file, name);
        var differences = new List<string>();
        var call = (string)entry["call"]!;
        var args = entry["args"]?.AsArray();
        // A case or its file may name the culture the thread is in, so that a call that leaked it would show.
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        if ((string?)(entry["culture"] ?? document["culture"]) is { } culture)
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = SheetCases.Culture(culture);
        try
        {
            Hold("call", () => Read(call, args), entry, differences, outermost: true);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
        }
        Assert.True(differences.Count == 0, $"sheet/{file}, \"{name}\":\n  {string.Join("\n  ", differences)}");
    }

    private static object? Read(string call, JsonArray? args)
    {
        if (call.StartsWith("new ", StringComparison.Ordinal))
        {
            var type = SubjectNamed(call[4..]);
            var constructors = type.GetConstructors(BindingFlags.Instance | Visible).Where(Reachable);
            return Invoke(constructors, args ?? throw new InvalidDataException($"{call} is called with args, [] for none."), (c, a) => ((ConstructorInfo)c).Invoke(a), call);
        }
        var dot = call.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
            throw new InvalidDataException($"'{call}' is no call: Subject.member, or new Subject.");
        return Member(SubjectNamed(call[..dot]), null, call[(dot + 1)..], args);
    }

    private static Type SubjectNamed(string name) =>
        Subjects.TryGetValue(name, out var type) ? type : throw new InvalidDataException($"'{name}' is no subject this runner knows.");

    private static string Pascal(string member) => char.ToUpperInvariant(member[0]) + member[1..];

    /// <summary>A property read, or a method called, on the instance, or on the type for a static.</summary>
    private static object? Member(Type type, object? instance, string member, JsonArray? args)
    {
        var flags = Visible | (instance is null ? BindingFlags.Static : BindingFlags.Instance);
        var name = Pascal(member);
        if (args is null)
        {
            if (type.GetProperty(name, flags) is { } property && property.GetMethod is { } getter && Reachable(getter))
                return property.GetValue(instance);
            if (type.GetField(name, flags) is { } field && (field.IsPublic || field.IsAssembly))
                return field.GetValue(instance);
            throw new InvalidDataException($"{type.Name} has no {member} to read.");
        }
        var methods = type.GetMethods(flags).Where(m => m.Name == name && Reachable(m));
        return Invoke(methods, args, (m, a) => ((MethodInfo)m).Invoke(instance, a), $"{type.Name}.{member}");
    }

    /// <summary>
    /// Calls the first overload whose parameters the arguments convert to. A trailing <c>out</c> parameter the case
    /// does not write is the answer, as the Script Core's <c>tryParse</c> answers it: what it holds when the method
    /// answers true, and <c>null</c> when it answers false.
    /// </summary>
    private static object? Invoke(IEnumerable<MethodBase> overloads, JsonArray args, Func<MethodBase, object?[], object?> call, string what)
    {
        foreach (var overload in overloads.OrderBy(o => o.GetParameters().Length))
        {
            var parameters = overload.GetParameters();
            var outward = parameters.Length == args.Count + 1 && parameters[^1].IsOut;
            if (parameters.Length != args.Count && !outward && !(parameters.Length > args.Count && parameters.Skip(args.Count).All(p => p.HasDefaultValue)))
                continue;
            var values = new object?[parameters.Length];
            var fits = true;
            for (var i = 0; i < parameters.Length && fits; i++)
            {
                if (i >= args.Count)
                    values[i] = parameters[i].IsOut ? null : parameters[i].DefaultValue;
                else if (!TryArgument(args[i], parameters[i].ParameterType, out values[i]))
                    fits = false;
            }
            if (!fits)
                continue;
            try
            {
                var answer = call(overload, values);
                return outward ? (answer is true ? values[^1] : null) : answer;
            }
            catch (TargetInvocationException e) when (e.InnerException is { } inner)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        }
        throw new InvalidDataException($"{what} takes no arguments {args.ToJsonString()}.");
    }

    /// <summary>An argument as a case writes it, converted to the parameter's type: JSON as it stands, or one of the engine's own values, tagged.</summary>
    private static bool TryArgument(JsonNode? node, Type type, out object? value)
    {
        value = null;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (node is null)
            return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        if (node is JsonObject tagged)
        {
            if (tagged.Count != 1)
                throw new InvalidDataException($"{tagged.ToJsonString()} is no argument: an object is one tag.");
            var (tag, given) = tagged.First();
            switch (tag)
            {
                case "value" when underlying == typeof(Value):
                    value = SheetCases.Decode(given);
                    return value is not null || type != underlying;
                case "address" when underlying == typeof(CellAddress):
                    value = CellAddress.Parse((string)given!);
                    return true;
                case "range" when underlying == typeof(CellRange):
                    value = CellRange.Parse((string)given!);
                    return true;
                case "culture" when type == typeof(CultureInfo):
                    value = SheetCases.Culture((string)given!);
                    return true;
                case "lookup" when type == typeof(Func<string, IReadOnlyList<string>?>):
                    var map = given!.AsObject();
                    value = new Func<string, IReadOnlyList<string>?>(n =>
                        map.FirstOrDefault(p => string.Equals(p.Key, n, StringComparison.OrdinalIgnoreCase)).Value is JsonArray columns
                            ? [.. columns.Select(c => (string)c!)]
                            : null);
                    return true;
                case "value" or "address" or "range" or "culture" or "lookup":
                    return false;
                default:
                    throw new InvalidDataException($"'{tag}' is no tag an argument takes.");
            }
        }
        if (node is JsonArray items)
        {
            var element = ElementOf(type);
            if (element is null)
                return false;
            var array = Array.CreateInstance(element, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                if (!TryArgument(items[i], element, out var item))
                    return false;
                array.SetValue(item, i);
            }
            value = array;
            return type.IsAssignableFrom(array.GetType());
        }
        var kind = node.GetValueKind();
        if (underlying == typeof(string) && kind == JsonValueKind.String)
            value = (string)node!;
        else if (underlying == typeof(bool) && kind is JsonValueKind.True or JsonValueKind.False)
            value = (bool)node;
        else if (underlying == typeof(int) && kind == JsonValueKind.Number && node.AsValue().TryGetValue<int>(out var whole))
            value = whole;
        else if (underlying == typeof(long) && kind == JsonValueKind.Number && node.AsValue().TryGetValue<long>(out var wide))
            value = wide;
        else if (underlying == typeof(double) && (kind == JsonValueKind.Number || kind == JsonValueKind.String && (string)node! is "Infinity" or "-Infinity" or "NaN"))
            value = SheetCases.Number(node);
        else
            return false;
        return true;
    }

    /// <summary>The element type of an array or a list parameter, or null for neither.</summary>
    private static Type? ElementOf(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition
            && (definition == typeof(IReadOnlyList<>) || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IList<>)))
            return type.GetGenericArguments()[0];
        return null;
    }

    /// <summary>
    /// The answer, as JSON in the shape the case expects it: a Value as a sheet case writes one, an address or a range
    /// as its text unless the case reads its members, a list or a tuple element by element, and an object by the keys the case writes, each read as a member.
    /// </summary>
    private static JsonNode? Project(object? answer, JsonNode? expected)
    {
        switch (answer)
        {
            case null:
                return null;
            case Value v:
                return SheetCases.Encode(v);
            case CellAddress or CellRange when expected is not JsonObject:
                return answer.ToString();
            case string s:
                return s;
            case bool b:
                return b;
            case int or long or double:
                return JsonValue.Create(Convert.ToDouble(answer, CultureInfo.InvariantCulture));
            case char c:
                return c.ToString();
            case Enum e:
                var name = e.ToString();
                return char.ToLowerInvariant(name[0]) + name[1..];
            case ITuple tuple:
                return new JsonArray([.. Enumerable.Range(0, tuple.Length).Select(i => Project(tuple[i], (expected as JsonArray)?.ElementAtOrDefault(i)))]);
            case IEnumerable items:
                return new JsonArray([.. items.Cast<object?>().Select((item, i) => Project(item, (expected as JsonArray)?.ElementAtOrDefault(i)))]);
        }
        if (expected is not JsonObject shape)
            throw new InvalidDataException($"An answer of {answer} is read as an object, and the case names none of its members.");
        var read = new JsonObject();
        foreach (var (key, value) in shape)
            read[key] = Project(Member(answer.GetType(), answer, key, null), value);
        return read;
    }

    private static readonly HashSet<string> Parts = ["call", "get", "args", "is", "throws", "then", "name", "note"];

    /// <summary>Holds the case's expectation of one reading: its answer (<c>is</c>), or its refusal (<c>throws</c>), then the checks on the answer.</summary>
    private static void Hold(string where, Func<object?> read, JsonObject check, List<string> differences, bool outermost = false)
    {
        if (check.Select(p => p.Key).FirstOrDefault(k => !Parts.Contains(k) && !(outermost && k == "culture")) is { } stray)
            throw new InvalidDataException($"{where}: '{stray}' is no part of a call.");
        object? answer;
        try
        {
            answer = read();
        }
        catch (Exception e) when (check["throws"] is not null && e is not InvalidDataException)
        {
            SheetCases.Compare($"{where}.throws", check["throws"], Run.Thrown(e), differences);
            return;
        }
        if (check["throws"] is not null)
        {
            differences.Add($"{where}: expected a refusal, got {Project(answer, check["is"])?.ToJsonString() ?? "null"}");
            return;
        }
        if (check["is"] is null && !check.ContainsKey("is") && check["then"] is null)
            throw new InvalidDataException($"{where} checks nothing.");
        if (check.ContainsKey("is"))
            SheetCases.Compare($"{where}.is", check["is"], Project(answer, check["is"]), differences);
        if (check["then"] is JsonArray then)
        {
            for (var i = 0; i < then.Count; i++)
            {
                var next = then[i]!.AsObject();
                var subject = answer ?? throw new InvalidDataException($"{where}: an answer of null has no members.");
                var member = (string)(next["call"] ?? next["get"])!;
                var args = next.ContainsKey("call")
                    ? next["args"]?.AsArray() ?? throw new InvalidDataException($"{where}.then[{i}]: a call is written with its args, [] for none.")
                    : null;
                Hold($"{where}.then[{i}]", () => Member(subject.GetType(), subject, member, args), next, differences);
            }
        }
    }
}
