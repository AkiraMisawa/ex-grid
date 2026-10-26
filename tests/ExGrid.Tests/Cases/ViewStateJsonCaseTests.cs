using System.Globalization;
using System.Text.Json;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The View State's JSON (<c>tests/cases/view-state</c>, kind <c>viewStateJson</c>; ADR-0207, ADR-0370 section
/// 5), run against <see cref="GridViewStateJson"/>, <see cref="GridViewState"/> and <see cref="GridQuery"/>'s
/// equality. Every case runs under the culture the run starts in, under de-DE, whose decimal separator is a
/// comma, and under th-TH, whose calendar is not the Gregorian: the form is the invariant culture's, whatever
/// the current one is.
/// </summary>
public class ViewStateJsonCaseTests
{
    private const string Area = "view-state";
    private static readonly string[] Cultures = ["", "de-DE", "th-TH"];
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "viewStateJson");

    [Theory, MemberData(nameof(Cases))] // ADR-0207: a View State JSON case holds on the C# core, under every culture
    public void ViewStateJsonCase(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var columns = document.GetProperty("columns").EnumerateArray().Select(column => column.GetString()!).ToArray();
        foreach (var culture in Cultures)
            InCulture(culture, () => Run(entry, columns));
    }

    private static void Run(JsonElement entry, string[] columns)
    {
        if (entry.TryGetProperty("read", out var read))
        {
            var text = read.GetString()!;
            if (entry.TryGetProperty("refused", out var refused))
            {
                AssertRefused(Assert.Throws<JsonException>(() => GridViewStateJson.Read(text, columns)), refused);
                return;
            }
            var view = GridViewStateJson.Read(text, columns);
            var written = GridViewStateJson.Write(view);
            Assert.Equal(entry.GetProperty("writes").GetString(), written);
            Assert.Equal(view, GridViewStateJson.Read(written, columns));
        }
        else if (entry.TryGetProperty("readQuery", out var readQuery))
        {
            var text = readQuery.GetString()!;
            if (entry.TryGetProperty("refused", out var refused))
            {
                AssertRefused(Assert.Throws<JsonException>(() => GridViewStateJson.ReadQuery(text)), refused);
                return;
            }
            var query = GridViewStateJson.ReadQuery(text);
            var written = GridViewStateJson.WriteQuery(query);
            Assert.Equal(entry.GetProperty("writes").GetString(), written);
            Assert.Equal(query, GridViewStateJson.ReadQuery(written));
        }
        else if (entry.TryGetProperty("write", out var write))
        {
            if (entry.TryGetProperty("refused", out var refused))
            {
                Refused(() => GridViewStateJson.Write(View(write)), refused);
                return;
            }
            var view = View(write);
            var written = GridViewStateJson.Write(view);
            Assert.Equal(entry.GetProperty("writes").GetString(), written);
            var back = GridViewStateJson.Read(written, columns);
            Assert.Equal(view, back);
            Assert.Equal(view.GetHashCode(), back.GetHashCode());
            // What equality does not see, and the form keeps: a decimal's places, a DateTime's kind, an offset.
            Assert.Equal(written, GridViewStateJson.Write(back));
        }
        else if (entry.TryGetProperty("writeQuery", out var writeQuery))
        {
            if (entry.TryGetProperty("refused", out var refused))
            {
                Refused(() => GridViewStateJson.WriteQuery(Query(writeQuery)), refused);
                return;
            }
            var query = Query(writeQuery);
            var written = GridViewStateJson.WriteQuery(query);
            Assert.Equal(entry.GetProperty("writes").GetString(), written);
            var back = GridViewStateJson.ReadQuery(written);
            Assert.Equal(query, back);
            Assert.Equal(query.GetHashCode(), back.GetHashCode());
            Assert.Equal(written, GridViewStateJson.WriteQuery(back));
        }
        else if (entry.TryGetProperty("of", out var of))
        {
            GridViewState Of() => GridViewState.Of(
                [.. of.GetProperty("columns").EnumerateArray().Select(GridColumnOf)],
                of.GetProperty("pinnedColumnCount").GetInt32(),
                [.. of.GetProperty("sorts").EnumerateArray().Select(Sort)],
                null);
            if (entry.TryGetProperty("refused", out var refused))
            {
                Refused(() => Of(), refused);
                return;
            }
            Assert.Equal(entry.GetProperty("writes").GetString(), GridViewStateJson.Write(Of()));
        }
        else if (entry.TryGetProperty("construct", out var construct))
        {
            Refused(() => View(construct), entry.GetProperty("refused"));
        }
        else if (entry.TryGetProperty("equal", out var equal))
        {
            AssertEqual(View(equal[0]), View(equal[1]), entry.GetProperty("is").GetBoolean());
        }
        else if (entry.TryGetProperty("equalQuery", out var equalQuery))
        {
            AssertEqual(Query(equalQuery[0]), Query(equalQuery[1]), entry.GetProperty("is").GetBoolean());
        }
        else
        {
            throw new InvalidOperationException($"The case {entry} names no action a viewStateJson case takes.");
        }
    }

    private static void AssertEqual<T>(T a, T b, bool equal)
        where T : notnull
    {
        Assert.Equal(equal, a.Equals(b));
        Assert.Equal(equal, b.Equals(a));
        if (equal)
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>A reading refused: at the path named, or none where it is <c>null</c>, saying exactly
    /// <c>says</c>, or holding <c>saying</c>.</summary>
    private static void AssertRefused(JsonException refusal, JsonElement refused)
    {
        var path = refused.GetProperty("path");
        if (path.ValueKind == JsonValueKind.Null)
            Assert.Null(refusal.Path);
        else
            Assert.Equal(path.GetString(), refusal.Path);
        if (refused.TryGetProperty("says", out var says))
            Assert.Equal(says.GetString(), refusal.Message);
        else
            Assert.Contains(refused.GetProperty("saying").GetString()!, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>A writing or a making refused, by its kind: <c>notSupported</c>, or a kind
    /// <see cref="SharedCases.Refused(Action, string, string?)"/> names.</summary>
    private static void Refused(Action action, JsonElement refused)
    {
        var kind = refused.GetProperty("kind").GetString()!;
        var saying = refused.TryGetProperty("saying", out var text) ? text.GetString() : null;
        if (kind == "notSupported")
        {
            var thrown = Assert.Throws<NotSupportedException>(action);
            if (saying is not null)
                Assert.Contains(saying, thrown.Message, StringComparison.Ordinal);
            return;
        }
        SharedCases.Refused(action, kind, saying);
    }

    // ---- What a case builds -----------------------------------------------------------------------

    private static GridViewState View(JsonElement built) => new(
        [.. built.GetProperty("columns").EnumerateArray().Select(column => column.ValueKind == JsonValueKind.Null
            ? null!
            : new ColumnViewState(column.GetProperty("column").GetString()!, Width(column.GetProperty("width")), column.GetProperty("pinned").GetBoolean()))],
        [.. built.GetProperty("sorts").EnumerateArray().Select(Sort)],
        built.TryGetProperty("filter", out var filter) ? Filter(filter) : null);

    private static GridQuery Query(JsonElement built)
    {
        var range = built.GetProperty("range");
        return new GridQuery(
            range.ValueKind == JsonValueKind.String && range.GetString() == "default"
                ? default
                : new RowRange(range.GetProperty("start").GetInt32(), range.GetProperty("count").GetInt32()),
            [.. built.GetProperty("sorts").EnumerateArray().Select(Sort)],
            built.TryGetProperty("filter", out var filter) ? Filter(filter) : null);
    }

    private static GridColumn<object> GridColumnOf(JsonElement built) => new(
        built.GetProperty("name").GetString()!, ColumnType.Text, _ => null,
        width: built.TryGetProperty("width", out var width) ? new ColumnWidthSpec(Width(width)) : null);

    private static ColumnWidth Width(JsonElement width)
        => width.ValueKind == JsonValueKind.String && width.GetString() == "Auto" ? ColumnWidth.Auto : ColumnWidth.Fixed(width.GetDouble());

    private static SortSpec Sort(JsonElement built) => built.ValueKind == JsonValueKind.Null
        ? null!
        : new SortSpec(built.GetProperty("column").GetString()!, Named<SortDirection>(built.GetProperty("direction")));

    private static GridFilter Filter(JsonElement built)
    {
        var columns = new Dictionary<string, FilterSpec>(StringComparer.Ordinal);
        foreach (var column in built.GetProperty("columns").EnumerateObject())
            columns.Add(column.Name, column.Value.ValueKind == JsonValueKind.Null ? null! : Spec(column.Value));
        return new GridFilter(columns, built.TryGetProperty("opaque", out var opaque) ? opaque.GetString() : null);
    }

    private static FilterSpec Spec(JsonElement built)
    {
        var clauses = built.GetProperty("clauses");
        return new FilterSpec(
            clauses.ValueKind == JsonValueKind.Null ? null! : [.. clauses.EnumerateArray().Select(Clause)],
            built.TryGetProperty("combinator", out var combinator) ? Named<FilterCombinator>(combinator) : FilterCombinator.And);
    }

    private static FilterClause Clause(JsonElement built) => built.ValueKind == JsonValueKind.Null
        ? null!
        : new FilterClause(
            Named<FilterOperator>(built.GetProperty("operator")),
            built.TryGetProperty("value", out var value) ? Operand(value) : null,
            built.TryGetProperty("values", out var values) ? [.. values.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Null ? null : Operand(v))] : null);

    /// <summary>An enum member by its name, or, written as a number, the value no name names.</summary>
    private static T Named<T>(JsonElement built)
        where T : struct, Enum
        => built.ValueKind == JsonValueKind.Number ? (T)Enum.ToObject(typeof(T), built.GetInt32()) : Enum.Parse<T>(built.GetString()!);

    /// <summary>An operand as its tag's .NET type, read from its text in the invariant culture by .NET's own
    /// parse, not the reader's.</summary>
    private static object Operand(JsonElement built)
    {
        var text = built.GetProperty("value").GetString()!;
        return built.GetProperty("type").GetString() switch
        {
            "String" => text,
            "Boolean" => text == "true",
            "Decimal" => decimal.Parse(text, NumberStyles.Number, Invariant),
            "Double" => double.Parse(text, NumberStyles.Float, Invariant),
            "Single" => float.Parse(text, NumberStyles.Float, Invariant),
            "Int64" => long.Parse(text, Invariant),
            "Int32" => int.Parse(text, Invariant),
            "Int16" => short.Parse(text, Invariant),
            "SByte" => sbyte.Parse(text, Invariant),
            "UInt64" => ulong.Parse(text, Invariant),
            "UInt32" => uint.Parse(text, Invariant),
            "UInt16" => ushort.Parse(text, Invariant),
            "Byte" => byte.Parse(text, Invariant),
            "DateTime" => DateTime.SpecifyKind(DateTime.ParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffff", Invariant),
                Enum.Parse<DateTimeKind>(built.GetProperty("kind").GetString()!)),
            "DateOnly" => DateOnly.ParseExact(text, "yyyy-MM-dd", Invariant),
            "DateTimeOffset" => DateTimeOffset.ParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz", Invariant),
            var other => throw new InvalidOperationException($"'{other}' is not an operand type a case builds."),
        };
    }

    private static void InCulture(string culture, Action act)
    {
        var previous = CultureInfo.CurrentCulture;
        if (culture.Length > 0)
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            act();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
