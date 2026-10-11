using System.Reflection;
using System.Text.Json;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// FN-13 and ST-4: the Query and the View State travel as a serialisable model (ADR-0002), so
/// what a Consumer writes through <c>System.Text.Json</c> — to a server, or to a stored
/// personal setting — reads back as what it wrote. "Equal" is structural here: the records hold
/// lists, which a record compares by reference, so a round trip is held part by part, and an
/// operand by its CLR type as well as its value, because the engine reads the type (ADR-0023).
/// </summary>
public class QuerySerialisationTests
{
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    // ---- FN-13: the Query ------------------------------------------------------------------------

    [Fact] // ADR-0002 / FN-13: a Query's range, sorts, columns, operators and combinators come back as they went
    public void FN13_a_query_round_trips_through_system_text_json()
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

    [Fact] // ADR-0002 / FN-13: no filter and no sort travel as themselves, not as something empty that means otherwise
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

    // FN-13 and ST-4 FAIL here, and are left failing: an operand is typed object, and System.Text.Json
    // reads it back as a JsonElement, which the engine refuses by name (ADR-0023). Which JSON form
    // carries an operand's type is a decision no ADR records yet; this test is its pass condition.
    [Theory]
    [MemberData(nameof(Operands))] // ADR-0002 / ADR-0023 / FN-13 / ST-4: every operand the engine takes comes back as the type it went as
    public void FN13_ST4_every_operand_comes_back_as_the_type_it_went_as(string column, FilterClause clause)
    {
        var query = new GridQuery(new RowRange(0, 50), [],
            new GridFilter(new Dictionary<string, FilterSpec> { [column] = new([clause]) }));

        AssertSameQuery(query, RoundTrip(query));
    }

    public static TheoryData<string, FilterClause> Operands() => new()
    {
        { "Notional", new FilterClause(FilterOperator.GreaterThanOrEqual, 25_000_000.50m) },
        { "Book", new FilterClause(FilterOperator.Contains, "Rates") },
        { "Book", new FilterClause(FilterOperator.Equals, "2026-01-05") },
        { "Active", new FilterClause(FilterOperator.Equals, true) },
        { "AsOf", new FilterClause(FilterOperator.LessThan, new DateTime(2026, 1, 5, 9, 30, 0)) },
        { "Settles", new FilterClause(FilterOperator.Equals, new DateOnly(2026, 1, 5)) },
        { "Stamped", new FilterClause(FilterOperator.GreaterThan, new DateTimeOffset(2026, 1, 5, 9, 30, 0, TimeSpan.FromHours(9))) },
        { "Notional", new FilterClause(FilterOperator.In, Values: [1m, null, 2.5m]) },
    };

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

    /// <summary>
    /// The View State as a Consumer persists it (<c>CONTEXT.md</c>): column widths, column order,
    /// the pinned count, sort and filter. The grid holds none of it — it arrives as parameters and
    /// leaves as notifications (ADR-0001's third consequence) — so the record is the Consumer's, and
    /// what is the grid's is that every part of it is a type that round-trips. A hidden column is
    /// one the order leaves out.
    /// </summary>
    private sealed record SavedView(
        IReadOnlyList<string> Order,
        IReadOnlyDictionary<string, ColumnWidth> Widths,
        int PinnedColumnCount,
        IReadOnlyList<SortSpec> Sorts,
        GridFilter? Filter);

    [Fact] // CONTEXT.md (View State) / ADR-0002 / ADR-0016 / ST-4: widths, order, pinned count, sort and filter come back as they went
    public void ST4_view_state_round_trips_widths_order_pins_sort_and_filter()
    {
        var view = new SavedView(
            Order: ["Notional", "Book", "AsOf"],
            Widths: new Dictionary<string, ColumnWidth>
            {
                ["Notional"] = ColumnWidth.Fixed(137.5),
                ["Book"] = ColumnWidth.Auto,
                ["AsOf"] = ColumnWidth.Fixed(400.25),
            },
            PinnedColumnCount: 1,
            Sorts: [new SortSpec("AsOf", SortDirection.Descending)],
            Filter: new GridFilter(new Dictionary<string, FilterSpec>
            {
                ["Book"] = new([new FilterClause(FilterOperator.IsNotBlank)]),
            }));

        var back = RoundTrip(view);

        Assert.Equal(view.Order, back.Order);
        Assert.Equal(view.Widths.OrderBy(w => w.Key), back.Widths.OrderBy(w => w.Key));
        Assert.Equal(view.PinnedColumnCount, back.PinnedColumnCount);
        Assert.Equal(view.Sorts, back.Sorts);
        AssertSameFilter(view.Filter, back.Filter);
    }

    [Fact] // ADR-0016 / ST-4: an Auto width is persisted as the intent alone, never as a width
    public void ST4_an_auto_width_is_written_as_the_intent_and_no_width()
    {
        var json = JsonSerializer.Serialize(ColumnWidth.Auto);

        using var written = JsonDocument.Parse(json);
        Assert.NotEqual(JsonValueKind.Number, written.RootElement.ValueKind);
        Assert.DoesNotMatch("[0-9]", json);
        Assert.True(JsonSerializer.Deserialize<ColumnWidth>(json).IsAuto);
    }

    [Fact] // ADR-0016 / ST-4: a Fixed width comes back Fixed at its pixels — never quietly Auto
    public void ST4_a_fixed_width_comes_back_fixed_at_its_pixels()
    {
        var back = RoundTrip(ColumnWidth.Fixed(88.75));

        Assert.False(back.IsAuto);
        Assert.Equal(88.75, back.FixedPx);
    }

    [Fact] // ADR-0016 / ST-4: what no column may declare is refused on the way in, as it is at construction
    public void ST4_a_width_no_column_may_declare_is_refused_when_read()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ColumnWidth>("0"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ColumnWidth>("-12"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ColumnWidth>("\"auto-ish\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ColumnWidth>("{}"));
    }

    [Fact] // ADR-0016 / ST-4: a drag's result, which the Consumer records as the Fixed width, round-trips too
    public void ST4_a_column_width_change_round_trips()
    {
        var change = new ColumnWidthChange("Notional", 512.5);

        Assert.Equal(change, RoundTrip(change));
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
    }
}
