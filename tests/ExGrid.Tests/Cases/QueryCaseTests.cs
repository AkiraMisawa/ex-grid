using System.Globalization;
using System.Text.Json;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The reference implementation's Query semantics (<c>tests/cases/query</c>, kind <c>query</c>; ADR-0350
/// section 5), run against <see cref="GridQueryEngine"/>: rows filtered and sorted, and which stay, in
/// which order, or how the Query is refused.
/// </summary>
public class QueryCaseTests
{
    private const string Area = "query";

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "query");

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a query case holds on the C# core
    public void Query(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var columns = document.GetProperty("columns").EnumerateArray()
            .Select((column, index) => new ColumnInfo<JsonElement>(
                column.GetProperty("name").GetString()!,
                Enum.Parse<ColumnType>(column.GetProperty("type").GetString()!, ignoreCase: true),
                row => Value(row[index])))
            .ToArray();
        var rows = entry.GetProperty("rows").EnumerateArray().ToArray();
        var filter = entry.TryGetProperty("filter", out var f) ? FilterOf(f) : null;
        var sorts = entry.TryGetProperty("sorts", out var s)
            ? s.EnumerateArray().Select(sort => new SortSpec(sort.GetProperty("column").GetString()!, Member<SortDirection>(sort.GetProperty("direction")))).ToArray()
            : null;

        int[] run() => GridQueryEngine.ApplyPositions(rows, columns, filter, sorts);
        if (entry.TryGetProperty("throws", out var refusal))
        {
            SharedCases.Refused(() => run(), refusal.GetString()!, entry.TryGetProperty("saying", out var saying) ? saying.GetString() : null);
            return;
        }
        if (!entry.TryGetProperty("keeps", out var keeps))
            throw new InvalidOperationException($"The case \"{name}\" says neither \"keeps\" nor \"throws\".");
        Assert.Equal(keeps.EnumerateArray().Select(k => k.GetInt32()).ToArray(), run());
    }

    /// <summary>A value as a case writes it: JSON's own, a JSON number a double (the Script Core's number),
    /// <c>{"bigint": text}</c> a long, <c>{"double": text}</c> a double JSON cannot hold.</summary>
    private static object? Value(JsonElement written) => written.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => written.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => written.GetDouble(),
        JsonValueKind.Object when written.TryGetProperty("bigint", out var text)
            => long.Parse(text.GetString()!, CultureInfo.InvariantCulture),
        JsonValueKind.Object when written.TryGetProperty("double", out var text)
            => double.Parse(text.GetString()!, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"{written} is not a value a query case writes."),
    };

    /// <summary>An enum's member as a case names it, or a number cast in from outside the vocabulary.</summary>
    private static T Member<T>(JsonElement written) where T : struct, Enum
        => written.ValueKind == JsonValueKind.Number
            ? (T)Enum.ToObject(typeof(T), written.GetInt32())
            : Enum.Parse<T>(written.GetString()!, ignoreCase: true);

    private static GridFilter FilterOf(JsonElement filter)
    {
        var columns = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
        foreach (var column in filter.GetProperty("columns").EnumerateObject())
        {
            var clauses = column.Value.GetProperty("clauses").EnumerateArray().Select(clause => new FilterClause(
                Member<FilterOperator>(clause.GetProperty("operator")),
                clause.TryGetProperty("value", out var value) ? Value(value) : null,
                clause.TryGetProperty("values", out var values) ? values.EnumerateArray().Select(Value).ToArray() : null)).ToArray();
            columns[column.Name] = column.Value.TryGetProperty("combinator", out var combinator)
                ? new FilterSpec(clauses, Member<FilterCombinator>(combinator))
                : new FilterSpec(clauses);
        }
        return new GridFilter(columns, filter.TryGetProperty("opaque", out var opaque) ? Value(opaque) : null);
    }
}
