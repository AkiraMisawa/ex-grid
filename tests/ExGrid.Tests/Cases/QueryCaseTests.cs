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
                Member<ColumnType>(column.GetProperty("type")),
                row => Value(row[index]),
                DateType: column.TryGetProperty("dateType", out var dateType) ? Member<DateType>(dateType) : DateType.DateTime))
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

    [Fact] // FL-6, ADR-0023/0009: every operator on every column type is either offered, with its cases, or refused naming the column
    public void FL6_every_operator_on_every_column_type_is_either_tested_or_refused()
    {
        var document = SharedCases.Read(Area, "operators.json");
        var columns = document.GetProperty("columns").EnumerateArray()
            .Select(column => (Name: column.GetProperty("name").GetString()!, Type: Member<ColumnType>(column.GetProperty("type"))))
            .ToArray();
        var cases = document.GetProperty("cases").EnumerateArray().Select(entry =>
        {
            var filtered = entry.GetProperty("filter").GetProperty("columns").EnumerateObject().Single();
            var index = Array.FindIndex(columns, column => column.Name == filtered.Name);
            var clause = filtered.Value.GetProperty("clauses").EnumerateArray().Single();
            return (
                columns[index].Type,
                Operator: Member<FilterOperator>(clause.GetProperty("operator")),
                Column: filtered.Name,
                Blank: entry.GetProperty("rows").EnumerateArray().Any(row => row[index].ValueKind == JsonValueKind.Null),
                Kept: entry.TryGetProperty("keeps", out var keeps) ? keeps.GetArrayLength() > 0 : (bool?)null,
                Throws: entry.TryGetProperty("throws", out var throws) ? throws.GetString() : null,
                Saying: entry.TryGetProperty("saying", out var saying) ? saying.GetString() : null);
        }).ToArray();

        foreach (var type in Enum.GetValues<ColumnType>())
        {
            Assert.Contains(columns, column => column.Type == type);
            foreach (var op in Enum.GetValues<FilterOperator>())
            {
                var pair = cases.Where(c => c.Type == type && c.Operator == op).ToArray();
                if (FilterOperators.AllowedFor(type).Contains(op))
                {
                    // Offered: at least one case that keeps, one that drops, and a Blank.
                    Assert.True(pair.Any(c => c.Kept == true) && pair.Any(c => c.Kept == false),
                        $"{op} on {type} needs a matching and a non-matching case");
                    Assert.True(pair.Any(c => c.Blank), $"{op} on {type} needs a Blank case");
                }
                else
                {
                    Assert.True(pair.Length > 0 && pair.All(c => c.Throws == "invalidOperation" && c.Saying == c.Column),
                        $"{op} on {type} is not offered, so it needs a case refused naming the column");
                }
            }
        }
    }

    /// <summary>A value as a case writes it: JSON's own, a JSON number a double (the Script Core's number),
    /// <c>{"bigint": text}</c> a long, <c>{"double": text}</c> a double JSON cannot hold, <c>{"dateTime": text}</c>
    /// a DateTime, <c>{"dateOnly": text}</c> a DateOnly, and <c>{"dateTimeOffset": text}</c> and
    /// <c>{"jsDate": text}</c> a DateTimeOffset.</summary>
    internal static object? Value(JsonElement written) => written.ValueKind switch
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
        JsonValueKind.Object when written.TryGetProperty("dateTime", out var text)
            => DateTime.Parse(text.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        JsonValueKind.Object when written.TryGetProperty("dateOnly", out var text)
            => DateOnly.Parse(text.GetString()!, CultureInfo.InvariantCulture),
        JsonValueKind.Object when written.TryGetProperty("dateTimeOffset", out var text)
            => DateTimeOffset.Parse(text.GetString()!, CultureInfo.InvariantCulture),
        JsonValueKind.Object when written.TryGetProperty("jsDate", out var text)
            => DateTimeOffset.Parse(text.GetString()!, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"{written} is not a value a query case writes."),
    };

    /// <summary>An enum's member as a case names it, or a number cast in from outside the vocabulary.</summary>
    internal static T Member<T>(JsonElement written) where T : struct, Enum
        => written.ValueKind == JsonValueKind.Number
            ? (T)Enum.ToObject(typeof(T), written.GetInt32())
            : Enum.Parse<T>(written.GetString()!, ignoreCase: true);

    internal static GridFilter FilterOf(JsonElement filter)
    {
        var columns = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
        foreach (var column in filter.GetProperty("columns").EnumerateObject())
        {
            var written = column.Value.GetProperty("clauses");
            // Clauses written as null are absent, as a Query that came through JSON writes them.
            var clauses = written.ValueKind == JsonValueKind.Null ? null! : written.EnumerateArray().Select(clause => new FilterClause(
                Member<FilterOperator>(clause.GetProperty("operator")),
                clause.TryGetProperty("value", out var value) ? Value(value) : null,
                clause.TryGetProperty("values", out var values) && values.ValueKind != JsonValueKind.Null
                    ? values.EnumerateArray().Select(Value).ToArray()
                    : null)).ToArray();
            // A combinator written as null is absent, as a Query that came through JSON writes it.
            columns[column.Name] = column.Value.TryGetProperty("combinator", out var combinator) && combinator.ValueKind != JsonValueKind.Null
                ? new FilterSpec(clauses, Member<FilterCombinator>(combinator))
                : new FilterSpec(clauses);
        }
        return new GridFilter(columns, filter.TryGetProperty("opaque", out var opaque) ? Value(opaque) : null);
    }
}
