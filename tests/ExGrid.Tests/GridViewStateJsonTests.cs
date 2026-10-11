using System.Text.Json;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0207: what ExGrid's JSON entry point for the View State refuses, and that each refusal names
/// what it refuses. Reading never guesses a type and never drops a part it does not understand
/// (principle 1): an unknown tag, a value that does not parse as its tag's type, a width that is
/// neither form, a column key the grid does not have, and a version this reader does not read are
/// refused by name; so is what has no JSON form on the way out.
/// </summary>
public class GridViewStateJsonTests
{
    private static readonly string[] Grid = QuerySerialisationTests.GridColumns;

    private static JsonException Refused(string json) => Assert.Throws<JsonException>(() => GridViewStateJson.Read(json, Grid));

    private static JsonException QueryRefused(string json) => Assert.Throws<JsonException>(() => GridViewStateJson.ReadQuery(json));

    /// <summary>That <paramref name="refusal"/> names <paramref name="member"/> itself: quoted in its
    /// message, or as the last segment of the path it was refused at — never only as part of a longer
    /// name (<c>column</c> within <c>columns</c>, which every View State's path holds).</summary>
    private static void AssertNames(JsonException refusal, string member)
    {
        var path = refusal.Path ?? "";
        var last = path[(path.LastIndexOf('.') + 1)..];
        Assert.True(refusal.Message.Contains($"'{member}'", StringComparison.Ordinal) || last == member,
            $"The refusal does not name '{member}': {refusal.Message} (at {path})");
    }

    /// <summary>A View State whose only filter clause carries <paramref name="operand"/>.</summary>
    private static string WithOperand(string operand)
        => """{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Notional":{"combinator":"And","clauses":[{"operator":"Equals","value":"""
            + operand + "}]}}}}";

    // ---- The version --------------------------------------------------------------------------

    [Theory] // ADR-0207 / ST-4: a version this reader does not read — an earlier one, a later one — is refused by name, never half-read
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void ADR0207_a_view_state_of_another_version_is_refused(string version)
    {
        var refusal = Refused($$"""{"version":{{version}},"columns":[],"sorts":[]}""");

        Assert.Contains($"version {version}", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("reads version 1", refusal.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0207 / FN-13: a Query of another version is refused the same way
    [InlineData("0")]
    [InlineData("2")]
    public void ADR0207_a_query_of_another_version_is_refused(string version)
    {
        var refusal = QueryRefused($$"""{"version":{{version}},"range":{"start":0,"count":1},"sorts":[]}""");

        Assert.Contains($"version {version}", refusal.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0207: a document that names no version, or names it as anything but a number, is refused
    [InlineData("""{"columns":[],"sorts":[]}""")]
    [InlineData("""{"version":"1","columns":[],"sorts":[]}""")]
    [InlineData("""{"version":null,"columns":[],"sorts":[]}""")]
    public void ADR0207_a_view_state_that_names_no_version_is_refused(string json)
        => Assert.Contains("version", Refused(json).Message, StringComparison.Ordinal);

    [Theory] // ADR-0207: what is not a View State at all is refused, never read as an empty one
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"version":1,"columns":[],"sorts":[]""")]
    public void ADR0207_text_that_is_not_a_view_state_is_refused(string json) => Refused(json);

    // ---- The document's members ---------------------------------------------------------------

    [Theory] // ADR-0207: a member this version does not have is refused by name — never dropped
    [InlineData("""{"version":1,"columns":[],"sorts":[],"layout":"wide"}""", "layout")]
    [InlineData("""{"version":1,"columns":[{"column":"Book","width":"Auto","pinned":false,"hidden":true}],"sorts":[]}""", "hidden")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{},"opaque":"x"}}""", "opaque")]
    [InlineData("""{"version":1,"columns":[],"sorts":[{"column":"Book","direction":"Ascending","nulls":"first"}]}""", "nulls")]
    public void ADR0207_a_member_this_version_does_not_have_is_refused_by_name(string json, string member)
        => AssertNames(Refused(json), member);

    [Theory] // ADR-0207: a member the form requires, missing or null, is refused by name
    [InlineData("""{"version":1,"sorts":[]}""", "columns")]
    [InlineData("""{"version":1,"columns":[]}""", "sorts")]
    [InlineData("""{"version":1,"columns":[{"column":"Book","pinned":false}],"sorts":[]}""", "width")]
    [InlineData("""{"version":1,"columns":[{"column":"Book","width":"Auto"}],"sorts":[]}""", "pinned")]
    [InlineData("""{"version":1,"columns":[{"column":null,"width":"Auto","pinned":false}],"sorts":[]}""", "column")]
    [InlineData("""{"version":1,"columns":[{"width":"Auto","pinned":false}],"sorts":[]}""", "column")]
    [InlineData("""{"version":1,"columns":[],"sorts":[{"column":"Book"}]}""", "direction")]
    [InlineData("""{"version":1,"columns":[],"sorts":[{"direction":"Ascending"}]}""", "column")]
    public void ADR0207_a_required_member_that_is_missing_is_refused_by_name(string json, string member)
        => AssertNames(Refused(json), member);

    [Fact] // ADR-0207: a member named twice is refused, never settled by whichever came last
    public void ADR0207_a_member_named_twice_is_refused()
    {
        Refused("""{"version":1,"columns":[],"columns":[],"sorts":[]}""");
        Refused("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Book":{"combinator":"And","clauses":[]},"Book":{"combinator":"Or","clauses":[]}}}}""");
    }

    [Theory] // ADR-0207: a member of the wrong JSON type is refused
    [InlineData("""{"version":1,"columns":[{"column":"Book","width":"Auto","pinned":"yes"}],"sorts":[]}""")]
    [InlineData("""{"version":1,"columns":{},"sorts":[]}""")]
    [InlineData("""{"version":1,"columns":[null],"sorts":[]}""")]
    [InlineData("""{"version":1,"columns":[],"sorts":[null]}""")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Book":null}}}""")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Book":{"combinator":"And","clauses":[null]}}}}""")]
    public void ADR0207_a_member_of_the_wrong_shape_is_refused(string json) => Refused(json);

    // ---- Columns --------------------------------------------------------------------------------

    [Theory] // ADR-0207 / ST-4: a column the grid does not have is refused by name — in the columns, the sort and the filter
    [InlineData("""{"version":1,"columns":[{"column":"Gone","width":"Auto","pinned":false}],"sorts":[]}""")]
    [InlineData("""{"version":1,"columns":[],"sorts":[{"column":"Gone","direction":"Ascending"}]}""")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Gone":{"combinator":"And","clauses":[]}}}}""")]
    public void ADR0207_a_column_the_grid_does_not_have_is_refused_by_name(string json)
    {
        var refusal = Refused(json);

        Assert.Contains("'Gone'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("does not have", refusal.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0207: a column the grid has under another case is not that column — names are ordinal
    public void ADR0207_a_column_name_is_matched_exactly()
        => Assert.Contains("'book'", Refused("""{"version":1,"columns":[{"column":"book","width":"Auto","pinned":false}],"sorts":[]}""").Message,
            StringComparison.Ordinal);

    [Fact] // ADR-0207: a column named twice among the columns is refused by name
    public void ADR0207_a_column_named_twice_is_refused()
        => Assert.Contains("'Book' appears twice", Refused(
            """{"version":1,"columns":[{"column":"Book","width":"Auto","pinned":false},{"column":"Book","width":80,"pinned":false}],"sorts":[]}""").Message,
            StringComparison.Ordinal);

    [Fact] // ADR-0004 / ADR-0207: a pinned column after one that is not is refused — the pinned columns are the leading ones
    public void ADR0207_a_pinned_column_after_an_unpinned_one_is_refused()
        => Assert.Contains("'AsOf' is pinned after", Refused(
            """{"version":1,"columns":[{"column":"Book","width":"Auto","pinned":false},{"column":"AsOf","width":"Auto","pinned":true}],"sorts":[]}""").Message,
            StringComparison.Ordinal);

    // ---- Names ----------------------------------------------------------------------------------

    [Theory] // ADR-0207: an operator, a direction or a combinator this version does not name is refused by name — no other case, no number
    [InlineData("""{"version":1,"columns":[],"sorts":[{"column":"Book","direction":"Down"}]}""", "'Down'")]
    [InlineData("""{"version":1,"columns":[],"sorts":[{"column":"Book","direction":"ascending"}]}""", "'ascending'")]
    [InlineData("""{"version":1,"columns":[],"sorts":[{"column":"Book","direction":"1"}]}""", "'1'")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Book":{"combinator":"Xor","clauses":[]}}}}""", "'Xor'")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Book":{"combinator":"And","clauses":[{"operator":"Between"}]}}}}""", "'Between'")]
    [InlineData("""{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Book":{"combinator":"And","clauses":[{"operator":"equals"}]}}}}""", "'equals'")]
    public void ADR0207_a_name_this_version_does_not_have_is_refused_by_name(string json, string named)
        => Assert.Contains(named, Refused(json).Message, StringComparison.Ordinal);

    [Fact] // ADR-0207: a JSON number where a name belongs is refused, never taken for an enum's value
    public void ADR0207_a_number_for_a_name_is_refused()
        => Refused("""{"version":1,"columns":[],"sorts":[{"column":"Book","direction":1}]}""");

    // ---- Operands -------------------------------------------------------------------------------

    [Theory] // ADR-0023 / ADR-0207: an operand type the filter model does not admit is refused by its tag
    [InlineData("Money")]
    [InlineData("decimal")]
    [InlineData("Guid")]
    [InlineData("TimeOnly")]
    [InlineData("Char")]
    [InlineData("")]
    public void ADR0207_an_unknown_operand_tag_is_refused_by_name(string tag)
    {
        var refusal = Refused(WithOperand($$"""{"type":"{{tag}}","value":"1"}"""));

        Assert.Contains($"'{tag}' is not an operand type", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'Notional'", refusal.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0207: a value that does not parse as its tag's type in the invariant culture is refused, naming both
    [InlineData("Decimal", "25,000.50")]
    [InlineData("Decimal", "25.000,50")]
    [InlineData("Decimal", "1e3")]
    [InlineData("Decimal", " 1")]
    [InlineData("Decimal", "")]
    [InlineData("Decimal", "0.00000000000000000000000000001")]
    [InlineData("Decimal", "-0.00000000000000000000000000001")]
    [InlineData("Decimal", "1.00000000000000000000000000001")]
    [InlineData("Decimal", "25000000.500000000000000000000001")]
    [InlineData("Decimal", "+1")]
    [InlineData("Decimal", "01")]
    [InlineData("Decimal", ".5")]
    [InlineData("Decimal", "1.")]
    [InlineData("Decimal", "-0")]
    [InlineData("Double", "NaN")]
    [InlineData("Double", "Infinity")]
    [InlineData("Double", "0,5")]
    [InlineData("Double", "1e-400")]
    [InlineData("Double", "-1e-400")]
    [InlineData("Double", "0.00001e-320")]
    [InlineData("Single", "1e39")]
    [InlineData("Single", "1e-50")]
    [InlineData("Single", "1e-400")]
    [InlineData("Int32", "1.5")]
    [InlineData("Int32", "2147483648")]
    [InlineData("Int64", "1e3")]
    [InlineData("Int16", "40000")]
    [InlineData("SByte", "128")]
    [InlineData("UInt64", "-1")]
    [InlineData("UInt32", "-1")]
    [InlineData("UInt16", "65536")]
    [InlineData("Byte", "256")]
    [InlineData("Boolean", "True")]
    [InlineData("Boolean", "1")]
    [InlineData("Boolean", "yes")]
    [InlineData("DateOnly", "05/01/2026")]
    [InlineData("DateOnly", "2026-01-05T00:00:00.0000000")]
    [InlineData("DateTimeOffset", "2026-01-05T09:30:00.0000000")]
    [InlineData("DateTimeOffset", "2026-01-05T09:30:00.0000000Z")]
    [InlineData("DateTimeOffset", "2026-01-05")]
    public void ADR0207_a_value_that_does_not_parse_as_its_type_is_refused(string tag, string value)
    {
        var refusal = Refused(WithOperand($$"""{"type":"{{tag}}","value":"{{value}}"}"""));

        Assert.Contains($"'{value}' is not a {tag}", refusal.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0207: a DateTime is read in its one form and with its kind — a value that is not that form is refused
    [InlineData("2026-01-05")]
    [InlineData("2026-01-05T09:30:00")]
    [InlineData("2026-01-05T09:30:00.0000000Z")]
    [InlineData("2026-01-05T09:30:00.0000000+09:00")]
    [InlineData("05/01/2026 09:30")]
    public void ADR0207_a_datetime_in_another_form_is_refused(string value)
        => Assert.Contains($"'{value}' is not a DateTime",
            Refused(WithOperand($$"""{"type":"DateTime","value":"{{value}}","kind":"Utc"}""")).Message, StringComparison.Ordinal);

    [Fact] // ADR-0207: a DateTime keeps its kind, so one that names none, or names another, is refused
    public void ADR0207_a_datetime_names_its_kind()
    {
        Assert.Contains("names its kind", Refused(WithOperand("""{"type":"DateTime","value":"2026-01-05T09:30:00.0000000"}""")).Message,
            StringComparison.Ordinal);
        Assert.Contains("'Zulu' is not a DateTime kind",
            Refused(WithOperand("""{"type":"DateTime","value":"2026-01-05T09:30:00.0000000","kind":"Zulu"}""")).Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0207: a kind on anything but a DateTime is a part this form does not have there, and is refused
    public void ADR0207_a_kind_on_another_operand_is_refused()
        => Assert.Contains("a kind belongs to a DateTime",
            Refused(WithOperand("""{"type":"DateOnly","value":"2026-01-05","kind":"Utc"}""")).Message, StringComparison.Ordinal);

    [Theory] // ADR-0207: an operand's value is always its text in a JSON string — a bare JSON number or Boolean is refused, never guessed at
    [InlineData("""{"type":"Decimal","value":25000000.50}""")]
    [InlineData("""{"type":"Boolean","value":true}""")]
    [InlineData("""{"type":"Decimal"}""")]
    [InlineData("""25000000.50""")]
    [InlineData("""{"type":"Decimal","value":"1","unit":"USD"}""")]
    public void ADR0207_an_operand_that_is_not_a_tagged_text_is_refused(string operand) => Refused(WithOperand(operand));

    [Fact] // ADR-0207: an operand among In's values is held to the same — and a null there is a Blank, not a refusal
    public void ADR0207_an_in_list_operand_is_read_as_any_other()
    {
        var json = """{"version":1,"columns":[],"sorts":[],"filter":{"columns":{"Notional":{"combinator":"And","clauses":[{"operator":"In","values":[{"type":"Decimal","value":"1"},null,{"type":"Decimal","value":"x"}]}]}}}}""";

        Assert.Contains("value 3", Refused(json).Message, StringComparison.Ordinal);
    }

    // ---- A Query's range ------------------------------------------------------------------------

    [Theory] // ADR-0001 / ADR-0207 / FN-13: a range no grid could ask for is refused on the way in, as RowRange refuses it
    [InlineData(0, 0)]
    [InlineData(-1, 5)]
    [InlineData(0, -3)]
    public void FN13_a_query_whose_range_no_grid_could_ask_for_is_refused(int start, int count)
        => Assert.Contains("range", QueryRefused($$"""{"version":1,"range":{"start":{{start}},"count":{{count}}},"sorts":[]}""").Message,
            StringComparison.Ordinal);

    [Fact] // ADR-0207 / FN-13: a Query is read without a grid's columns — a server has none — and refuses the rest as a View State does
    public void FN13_a_query_names_any_column_and_refuses_the_rest()
    {
        var query = GridViewStateJson.ReadQuery("""{"version":1,"range":{"start":3,"count":2},"sorts":[{"column":"Anything","direction":"Descending"}]}""");
        Assert.Equal([new SortSpec("Anything", SortDirection.Descending)], query.Sorts);

        QueryRefused("""{"version":1,"range":{"start":3,"count":2},"sorts":[{"column":"","direction":"Descending"}]}""");
        QueryRefused("""{"version":1,"range":{"start":3,"count":2},"sorts":[],"columns":[]}""");
    }

    // ---- What has no JSON form ----------------------------------------------------------------

    public static TheoryData<object> Unwritable() => new()
    {
        Guid.Empty,
        new TimeOnly(9, 30),
        'x',
        double.NaN,
        double.PositiveInfinity,
        float.NegativeInfinity,
        new object(),
    };

    [Theory]
    [MemberData(nameof(Unwritable))] // ADR-0023 / ADR-0207: an operand of a type the filter does not admit, or a number it refuses, has no JSON form and is refused by name
    public void ADR0207_an_operand_the_filter_does_not_admit_is_refused_when_written(object operand)
    {
        var filter = new GridFilter(new Dictionary<string, FilterSpec> { ["Notional"] = new([new FilterClause(FilterOperator.Equals, operand)]) });

        var refusal = Assert.Throws<NotSupportedException>(() => GridViewStateJson.WriteQuery(new GridQuery(new RowRange(0, 1), [], filter)));
        Assert.Contains("'Notional'", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => GridViewStateJson.Write(new GridViewState([], [], filter)));
    }

    [Fact] // ADR-0002 / ADR-0207: an Opaque Filter's meaning is the Consumer's alone — it has no JSON form here and is refused by name, never dropped
    public void ADR0207_an_opaque_filter_is_refused_when_written()
    {
        var filter = new GridFilter(new Dictionary<string, FilterSpec>(), Opaque: "region = EMEA");

        var refusal = Assert.Throws<NotSupportedException>(() => GridViewStateJson.Write(new GridViewState([], [], filter)));
        Assert.Contains("Opaque", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => GridViewStateJson.WriteQuery(new GridQuery(new RowRange(0, 1), [], filter)));
        // Written without it, the rest goes through.
        Assert.Null(GridViewStateJson.Read(GridViewStateJson.Write(new GridViewState([], [], filter with { Opaque = null })), []).Filter!.Opaque);
    }

    [Fact] // ADR-0207: an empty View State round-trips as itself — no columns, no sort, no filter
    public void ADR0207_an_empty_view_state_round_trips()
    {
        var back = GridViewStateJson.Read(GridViewStateJson.Write(new GridViewState([], [], null)), []);

        Assert.Empty(back.Columns);
        Assert.Empty(back.Sorts);
        Assert.Null(back.Filter);
        Assert.Equal(0, back.PinnedColumnCount);
    }

    [Theory] // ADR-0207: the form written is the one documented — a reader in another language can rely on it, whatever the current culture
    [InlineData("")]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    public void ADR0207_the_form_is_the_documented_one(string culture)
        => QuerySerialisationTests.InCulture(culture, TheFormIsTheDocumentedOne);

    private static void TheFormIsTheDocumentedOne()
    {
        var view = new GridViewState(
            [new ColumnViewState("Notional", ColumnWidth.Fixed(120.5), pinned: true), new ColumnViewState("Book", ColumnWidth.Auto)],
            [new SortSpec("Book", SortDirection.Descending)],
            new GridFilter(new Dictionary<string, FilterSpec>
            {
                ["AsOf"] = new([new FilterClause(FilterOperator.LessThan, new DateTime(2026, 1, 5, 9, 30, 0, DateTimeKind.Utc))]),
                ["Notional"] = new([new FilterClause(FilterOperator.In, Values: [1.50m, null])], FilterCombinator.Or),
            }));

        Assert.Equal(
            string.Concat(
                """{"version":1,"columns":[{"column":"Notional","width":120.5,"pinned":true},{"column":"Book","width":"Auto","pinned":false}],""",
                """ "sorts":[{"column":"Book","direction":"Descending"}],"filter":{"columns":{""".TrimStart(),
                """ "AsOf":{"combinator":"And","clauses":[{"operator":"LessThan","value":{"type":"DateTime","value":"2026-01-05T09:30:00.0000000","kind":"Utc"}}]},""".TrimStart(),
                """ "Notional":{"combinator":"Or","clauses":[{"operator":"In","values":[{"type":"Decimal","value":"1.50"},null]}]}}}}""".TrimStart()),
            GridViewStateJson.Write(view));
    }
}
