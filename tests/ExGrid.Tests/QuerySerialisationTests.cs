using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// FN-13 and ST-4: the Query and the View State travel as a serialisable model (ADR-0002), through
/// ExGrid's own JSON entry point (ADR-0207), so what a Consumer writes — to a server, or to a stored
/// personal setting — reads back as what it wrote. "Equal" is structural here: the records hold
/// lists, which a record compares by reference, so a round trip is held part by part, and an
/// operand by its CLR type as well as its value, because the engine reads the type (ADR-0023).
/// What the entry point refuses is held in <see cref="GridViewStateJsonTests"/>.
/// </summary>
public class QuerySerialisationTests
{
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private static GridQuery RoundTrip(GridQuery query) => GridViewStateJson.ReadQuery(GridViewStateJson.WriteQuery(query));

    /// <summary>The grid's columns, every one a View State in these tests may name.</summary>
    internal static readonly string[] GridColumns = ["Notional", "Book", "AsOf", "Active", "Settles", "Stamped", "Hidden"];

    private static GridViewState RoundTrip(GridViewState view) => GridViewStateJson.Read(GridViewStateJson.Write(view), GridColumns);

    // ---- FN-13: the Query ------------------------------------------------------------------------

    [Fact] // ADR-0002 / ADR-0207 / FN-13: a Query's range, sorts, columns, operators and combinators come back as they went
    public void FN13_a_query_round_trips_through_the_entry_point()
    {
        var query = new GridQuery(
            new RowRange(120, 60),
            [new SortSpec("Book", SortDirection.Ascending), new SortSpec("Notional", SortDirection.Descending)],
            new GridFilter(new Dictionary<string, FilterSpec>
            {
                ["Book"] = new([new FilterClause(FilterOperator.IsBlank), new FilterClause(FilterOperator.IsNotBlank)], FilterCombinator.Or),
                ["Notional"] = new([new FilterClause(FilterOperator.IsNotBlank)]),
            }));

        AssertSameQuery(query, RoundTrip(query));
    }

    [Fact] // ADR-0002 / ADR-0207 / FN-13: no filter and no sort travel as themselves, not as something empty that means otherwise
    public void FN13_a_query_with_no_filter_and_no_sort_round_trips()
    {
        var query = new GridQuery(new RowRange(0, 1), [], null);

        AssertSameQuery(query, RoundTrip(query));
    }

    [Fact] // ADR-0001 / FN-13: a range read back is the range written — and one no range may be is refused on the way in
    public void FN13_a_row_range_round_trips_and_an_impossible_one_is_refused()
    {
        Assert.Equal(new RowRange(7, 3), RoundTrip(new RowRange(7, 3)));

        // A RowRange is validated at construction (ADR-0001); reading one back must not be a way
        // round that, or a server would face a shape no grid can send.
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<RowRange>("""{"Start":0,"Count":0}"""));
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<RowRange>("""{"Start":-1,"Count":5}"""));
    }

    [Theory]
    [MemberData(nameof(OperandsUnderCultures))] // ADR-0023 / ADR-0207 / FN-13: every operand the engine takes comes back in a Query as the type it went as, whatever the current culture
    public void FN13_every_operand_comes_back_as_the_type_it_went_as(string culture, string column, FilterClause clause)
        => InCulture(culture, () =>
        {
            var query = new GridQuery(new RowRange(0, 50), [],
                new GridFilter(new Dictionary<string, FilterSpec> { [column] = new([clause]) }));

            AssertSameQuery(query, RoundTrip(query));
        });

    [Theory]
    [MemberData(nameof(OperandsUnderCultures))] // ADR-0023 / ADR-0207 / ST-4: every operand the engine takes comes back in a View State as the type it went as, whatever the current culture
    public void ST4_every_operand_comes_back_as_the_type_it_went_as(string culture, string column, FilterClause clause)
        => InCulture(culture, () =>
        {
            var view = new GridViewState([new ColumnViewState(column, ColumnWidth.Auto)], [],
                new GridFilter(new Dictionary<string, FilterSpec> { [column] = new([clause]) }));

            AssertSameFilter(view.Filter, RoundTrip(view).Filter);
        });

    /// <summary>The cultures the form is held under, besides the one the run starts in (named ""): one
    /// whose decimal separator is a comma, and one whose calendar is not the Gregorian (ADR-0207: the
    /// form is the invariant culture's, whatever the current one is).</summary>
    internal static readonly string[] Cultures = ["", "de-DE", "th-TH"];

    /// <summary>Runs <paramref name="act"/> under <paramref name="culture"/> as the current culture, and
    /// puts the one it found back after; "" leaves the current culture as it is.</summary>
    internal static void InCulture(string culture, Action act)
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

    /// <summary><see cref="Operands"/>, each under every one of <see cref="Cultures"/>.</summary>
    public static TheoryData<string, string, FilterClause> OperandsUnderCultures()
    {
        var data = new TheoryData<string, string, FilterClause>();
        foreach (var culture in Cultures)
        {
            foreach (var row in Operands())
                data.Add(culture, row.Data.Item1, row.Data.Item2);
        }
        return data;
    }

    /// <summary>Every operand type the filter model admits (ADR-0023): text, a Boolean, every number the
    /// engine takes, the three date types — a DateTime of each kind, a DateTimeOffset away from UTC —
    /// and an <c>In</c> list with Blanks, at their edges where an edge could be lost.</summary>
    public static TheoryData<string, FilterClause> Operands() => new()
    {
        { "Notional", new FilterClause(FilterOperator.GreaterThanOrEqual, 25_000_000.50m) },
        { "Notional", new FilterClause(FilterOperator.LessThan, 79_228_162_514_264_337_593_543_950_335m) },
        { "Notional", new FilterClause(FilterOperator.GreaterThan, -0.0000000000000000000000000001m) },
        { "Notional", new FilterClause(FilterOperator.Equals, 0.1d) },
        { "Notional", new FilterClause(FilterOperator.Equals, double.MaxValue) },
        { "Notional", new FilterClause(FilterOperator.Equals, -double.Epsilon) },
        { "Notional", new FilterClause(FilterOperator.Equals, 0.1f) },
        { "Notional", new FilterClause(FilterOperator.Equals, long.MinValue) },
        { "Notional", new FilterClause(FilterOperator.Equals, int.MaxValue) },
        { "Notional", new FilterClause(FilterOperator.Equals, (short)-12) },
        { "Notional", new FilterClause(FilterOperator.Equals, (sbyte)-128) },
        { "Notional", new FilterClause(FilterOperator.Equals, ulong.MaxValue) },
        { "Notional", new FilterClause(FilterOperator.Equals, uint.MaxValue) },
        { "Notional", new FilterClause(FilterOperator.Equals, (ushort)65_535) },
        { "Notional", new FilterClause(FilterOperator.Equals, (byte)255) },
        { "Book", new FilterClause(FilterOperator.Contains, "Rates") },
        { "Book", new FilterClause(FilterOperator.Equals, "2026-01-05") },
        { "Book", new FilterClause(FilterOperator.Equals, "") },
        { "Book", new FilterClause(FilterOperator.StartsWith, "\"Ünïcödé\" — 日本語, tab\tand newline\n") },
        { "Active", new FilterClause(FilterOperator.Equals, true) },
        { "Active", new FilterClause(FilterOperator.Equals, false) },
        { "AsOf", new FilterClause(FilterOperator.LessThan, new DateTime(2026, 1, 5, 9, 30, 0)) },
        { "AsOf", new FilterClause(FilterOperator.LessThan, new DateTime(2026, 1, 5, 9, 30, 0, DateTimeKind.Utc).AddTicks(1)) },
        { "AsOf", new FilterClause(FilterOperator.LessThan, new DateTime(2026, 1, 5, 9, 30, 0, DateTimeKind.Local)) },
        { "AsOf", new FilterClause(FilterOperator.LessThan, DateTime.MaxValue) },
        { "Settles", new FilterClause(FilterOperator.Equals, new DateOnly(2026, 1, 5)) },
        { "Settles", new FilterClause(FilterOperator.Equals, DateOnly.MinValue) },
        { "Stamped", new FilterClause(FilterOperator.GreaterThan, new DateTimeOffset(2026, 1, 5, 9, 30, 0, TimeSpan.FromHours(9))) },
        { "Stamped", new FilterClause(FilterOperator.GreaterThan, new DateTimeOffset(2026, 1, 5, 9, 30, 0, TimeSpan.FromMinutes(-570)).AddTicks(7)) },
        { "Notional", new FilterClause(FilterOperator.In, Values: [1m, null, 2.5m]) },
        { "Book", new FilterClause(FilterOperator.In, Values: [null]) },
        { "Active", new FilterClause(FilterOperator.In, Values: []) },
    };

    [Fact] // ADR-0023 / ADR-0207 / FN-13: every operator and combinator the model has comes back as itself
    public void FN13_every_operator_and_combinator_round_trips()
    {
        foreach (var combinator in Enum.GetValues<FilterCombinator>())
        {
            var clauses = Enum.GetValues<FilterOperator>().Select(op => new FilterClause(op, op == FilterOperator.In ? null : "x",
                op == FilterOperator.In ? ["x"] : null)).ToArray();
            var query = new GridQuery(new RowRange(0, 1), [],
                new GridFilter(new Dictionary<string, FilterSpec> { ["Book"] = new(clauses, combinator) }));

            AssertSameQuery(query, RoundTrip(query));
        }
        foreach (var direction in Enum.GetValues<SortDirection>())
        {
            var query = new GridQuery(new RowRange(0, 1), [new SortSpec("Book", direction)], null);
            Assert.Equal(query.Sorts, RoundTrip(query).Sorts);
        }
    }

    [Fact] // ADR-0207 / FN-13: a decimal is written as its exact digits in a string, never as a JSON number a reader could take for a double
    public void FN13_a_decimal_operand_is_its_exact_digits_in_a_string()
    {
        var query = new GridQuery(new RowRange(0, 1), [], new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Notional"] = new([new FilterClause(FilterOperator.Equals, 1234567890123456789.10m)]),
        }));

        using var written = JsonDocument.Parse(GridViewStateJson.WriteQuery(query));
        var operand = written.RootElement.GetProperty("filter").GetProperty("columns").GetProperty("Notional")
            .GetProperty("clauses")[0].GetProperty("value");
        Assert.Equal("Decimal", operand.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.String, operand.GetProperty("value").ValueKind);
        Assert.Equal("1234567890123456789.10", operand.GetProperty("value").GetString());
    }

    [Fact] // ADR-0207 / FN-13: the form is versioned from its first release
    public void FN13_a_query_carries_its_version()
    {
        using var written = JsonDocument.Parse(GridViewStateJson.WriteQuery(new GridQuery(new RowRange(0, 1), [], null)));

        Assert.Equal(GridViewStateJson.Version, written.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(1, GridViewStateJson.Version);
    }

    [Fact] // ADR-0002 / FN-13: no Expression type in any public signature — the filter is a model, never a LINQ expression
    public void FN13_no_public_signature_mentions_an_expression_type()
    {
        var offenders = new List<string>();
        foreach (var type in typeof(GridQuery).Assembly.GetExportedTypes())
        {
            void Check(Type? mentioned, string where)
            {
                if (mentioned is not null && MentionsExpression(mentioned))
                    offenders.Add($"{type.FullName}.{where}: {mentioned}");
            }

            Check(type.BaseType, "(base)");
            foreach (var implemented in type.GetInterfaces())
                Check(implemented, "(interface)");

            const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(Declared).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
                Check(field.FieldType, field.Name);
            foreach (var property in type.GetProperties(Declared))
            {
                var accessors = property.GetAccessors(nonPublic: true);
                if (!accessors.Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly))
                    continue;
                Check(property.PropertyType, property.Name);
                foreach (var index in property.GetIndexParameters())
                    Check(index.ParameterType, $"{property.Name}[{index.Name}]");
            }
            foreach (var @event in type.GetEvents(Declared).Where(e => e.AddMethod is { } add && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly)))
                Check(@event.EventHandlerType, @event.Name);
            foreach (var method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
                         .Where(m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly))
            {
                if (method is MethodInfo info)
                    Check(info.ReturnType, method.Name);
                foreach (var parameter in method.GetParameters())
                    Check(parameter.ParameterType, $"{method.Name}({parameter.Name})");
                if (method.IsGenericMethodDefinition)
                {
                    foreach (var argument in method.GetGenericArguments())
                    {
                        foreach (var constraint in argument.GetGenericParameterConstraints())
                            Check(constraint, $"{method.Name}<{argument.Name}>");
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }

    private static bool MentionsExpression(Type type)
    {
        if (type.HasElementType)
            return MentionsExpression(type.GetElementType()!);
        if (type.Namespace == "System.Linq.Expressions")
            return true;
        return type.IsGenericType && type.GetGenericArguments().Any(MentionsExpression);
    }

    // ---- ST-4: the View State --------------------------------------------------------------------

    [Fact] // CONTEXT.md (View State) / ADR-0002 / ADR-0016 / ADR-0207 / ST-4: widths, order, pinned count, sort and filter come back as they went
    public void ST4_view_state_round_trips_widths_order_pins_sort_and_filter()
    {
        var view = new GridViewState(
            [
                new ColumnViewState("Notional", ColumnWidth.Fixed(137.5), pinned: true),
                new ColumnViewState("Book", ColumnWidth.Auto),
                new ColumnViewState("AsOf", ColumnWidth.Fixed(400.25)),
            ],
            [new SortSpec("AsOf", SortDirection.Descending), new SortSpec("Hidden", SortDirection.Ascending)],
            new GridFilter(new Dictionary<string, FilterSpec>
            {
                ["Book"] = new([new FilterClause(FilterOperator.IsNotBlank)]),
                ["Notional"] = new([new FilterClause(FilterOperator.GreaterThan, 1_000_000m), new FilterClause(FilterOperator.LessThan, 0m)],
                    FilterCombinator.Or),
                ["AsOf"] = new([new FilterClause(FilterOperator.GreaterThanOrEqual, new DateTime(2026, 10, 1))]),
            }));

        var back = RoundTrip(view);

        // A hidden column is one the order leaves out; a sort may stand on it.
        Assert.Equal(view.Columns, back.Columns);
        Assert.Equal(1, back.PinnedColumnCount);
        Assert.Equal(view.Sorts, back.Sorts);
        AssertSameFilter(view.Filter, back.Filter);
    }

    [Fact] // ADR-0016 / ADR-0207 / ST-4: an Auto width is persisted as the intent alone, never as a width
    public void ST4_an_auto_width_is_written_as_the_intent_and_no_width()
    {
        var json = GridViewStateJson.Write(new GridViewState([new ColumnViewState("Book", ColumnWidth.Auto)], [], null));

        using var written = JsonDocument.Parse(json);
        var width = written.RootElement.GetProperty("columns")[0].GetProperty("width");
        Assert.Equal(JsonValueKind.String, width.ValueKind);
        Assert.Equal("Auto", width.GetString());
        Assert.DoesNotMatch("[0-9]", width.GetRawText());
        Assert.True(GridViewStateJson.Read(json, GridColumns).Columns[0].Width.IsAuto);
    }

    [Fact] // ADR-0016 / ADR-0207 / ST-4: a Fixed width is a number of pixels and comes back Fixed at them — never quietly Auto
    public void ST4_a_fixed_width_comes_back_fixed_at_its_pixels()
    {
        var json = GridViewStateJson.Write(new GridViewState([new ColumnViewState("Book", ColumnWidth.Fixed(88.75))], [], null));

        using var written = JsonDocument.Parse(json);
        Assert.Equal(88.75, written.RootElement.GetProperty("columns")[0].GetProperty("width").GetDouble());
        var back = GridViewStateJson.Read(json, GridColumns).Columns[0].Width;
        Assert.False(back.IsAuto);
        Assert.Equal(88.75, back.FixedPx);
    }

    [Theory] // ADR-0016 / ADR-0207 / ST-4: what no column may declare is refused on the way in, as it is at construction, naming the column
    [InlineData("0")]
    [InlineData("-12")]
    [InlineData("1e999")]
    [InlineData("\"auto\"")]
    [InlineData("\"auto-ish\"")]
    [InlineData("\"120\"")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("null")]
    public void ST4_a_width_no_column_may_declare_is_refused_when_read(string width)
    {
        var json = $$"""{"version":1,"columns":[{"column":"Book","width":{{width}},"pinned":false}],"sorts":[]}""";

        var refusal = Assert.Throws<JsonException>(() => GridViewStateJson.Read(json, GridColumns));
        Assert.Contains("Book", refusal.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0016 / ST-4: a drag's result, which the Consumer records as the Fixed width, round-trips too
    public void ST4_a_column_width_change_round_trips()
    {
        var change = new ColumnWidthChange("Notional", 512.5);

        Assert.Equal(change, RoundTrip(change));
    }

    [Fact] // ADR-0001 / ADR-0004 / ADR-0016 / ADR-0207 / ST-4: the View State a grid is shown under is its columns' names and width intents, the leading ones pinned, and the sort and filter in force
    public void ST4_the_view_state_of_a_grid_is_what_it_is_shown_under()
    {
        GridColumn<object>[] columns =
        [
            new("Notional", ColumnType.Number, _ => 1m, width: new ColumnWidthSpec(ColumnWidth.Fixed(120))),
            new("Book", ColumnType.Text, _ => "x"),
            new("AsOf", ColumnType.Date, _ => DateTime.MinValue, width: new ColumnWidthSpec(ColumnWidth.Fixed(90))),
        ];
        var filter = new GridFilter(new Dictionary<string, FilterSpec> { ["Book"] = new([new FilterClause(FilterOperator.IsBlank)]) });

        var view = GridViewState.Of(columns, 2, [new SortSpec("AsOf", SortDirection.Ascending)], filter);

        Assert.Equal(
            [
                new ColumnViewState("Notional", ColumnWidth.Fixed(120), pinned: true),
                new ColumnViewState("Book", ColumnWidth.Auto, pinned: true),
                new ColumnViewState("AsOf", ColumnWidth.Fixed(90)),
            ],
            view.Columns);
        Assert.Equal(2, view.PinnedColumnCount);
        Assert.Equal([new SortSpec("AsOf", SortDirection.Ascending)], view.Sorts);
        Assert.Same(filter, view.Filter);
        Assert.Throws<ArgumentOutOfRangeException>(() => GridViewState.Of(columns, 4, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridViewState.Of(columns, -1, null, null));
    }

    [Fact] // ADR-0004 / ADR-0207: a View State built in code is held to what one read back is — pinned columns lead, a column stands in one place
    public void ST4_a_view_state_refuses_a_pinned_column_after_an_unpinned_one_and_a_column_twice()
    {
        Assert.Throws<ArgumentException>(() => new GridViewState(
            [new ColumnViewState("Book", ColumnWidth.Auto), new ColumnViewState("AsOf", ColumnWidth.Auto, pinned: true)], [], null));
        Assert.Throws<ArgumentException>(() => new GridViewState(
            [new ColumnViewState("Book", ColumnWidth.Auto), new ColumnViewState("Book", ColumnWidth.Fixed(50))], [], null));
        Assert.Throws<ArgumentException>(() => new ColumnViewState("", ColumnWidth.Auto));
    }

    // ---- Structural comparison -------------------------------------------------------------------

    private static void AssertSameQuery(GridQuery expected, GridQuery actual)
    {
        Assert.Equal(expected.Range, actual.Range);
        Assert.Equal(expected.Sorts, actual.Sorts);
        AssertSameFilter(expected.Filter, actual.Filter);
    }

    private static void AssertSameFilter(GridFilter? expected, GridFilter? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.NotNull(actual);
        Assert.Equal(expected.Columns.Keys.Order(StringComparer.Ordinal), actual.Columns.Keys.Order(StringComparer.Ordinal));
        foreach (var (column, spec) in expected.Columns)
        {
            var back = actual.Columns[column];
            Assert.Equal(spec.Combinator, back.Combinator);
            Assert.Equal(spec.Clauses.Count, back.Clauses.Count);
            for (var i = 0; i < spec.Clauses.Count; i++)
            {
                Assert.Equal(spec.Clauses[i].Operator, back.Clauses[i].Operator);
                AssertSameOperand(spec.Clauses[i].Value, back.Clauses[i].Value);
                if (spec.Clauses[i].Values is null)
                {
                    Assert.Null(back.Clauses[i].Values);
                }
                else
                {
                    Assert.NotNull(back.Clauses[i].Values);
                    Assert.Equal(spec.Clauses[i].Values!.Count, back.Clauses[i].Values!.Count);
                    for (var j = 0; j < spec.Clauses[i].Values!.Count; j++)
                        AssertSameOperand(spec.Clauses[i].Values![j], back.Clauses[i].Values![j]);
                }
            }
        }
        AssertSameOperand(expected.Opaque, actual.Opaque);
    }

    private static void AssertSameOperand(object? expected, object? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.NotNull(actual);
        // The engine decides by the operand's type: a DateTime and a DateOnly of the same day,
        // or a decimal and a double of the same value, are different conditions (ADR-0023).
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected, actual);
        // What equality does not see, and the form keeps (ADR-0207): a decimal's exact digits, a
        // DateTime's kind, a DateTimeOffset's offset.
        switch (expected)
        {
            case decimal number:
                Assert.Equal(number.ToString(CultureInfo.InvariantCulture), ((decimal)actual).ToString(CultureInfo.InvariantCulture));
                break;
            case DateTime date:
                Assert.Equal(date.Kind, ((DateTime)actual).Kind);
                Assert.Equal(date.Ticks, ((DateTime)actual).Ticks);
                break;
            case DateTimeOffset date:
                Assert.Equal(date.Offset, ((DateTimeOffset)actual).Offset);
                Assert.Equal(date.Ticks, ((DateTimeOffset)actual).Ticks);
                break;
        }
    }
}
