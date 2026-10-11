using System.Reflection;
using System.Text.Json;
using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// FN-13: the Query travels as a serialisable model (ADR-0002), so what a Consumer writes through
/// <c>System.Text.Json</c> to a server reads back as what it wrote. "Equal" is structural here: the
/// records hold lists, which a record compares by reference, so a round trip is held part by part.
///
/// <para>Not asserted here, and not met: an operand's round trip, which FN-13 and ST-4 both need,
/// and the rest of ST-4 — a <see cref="ColumnWidth"/> has no JSON form yet. Which form an operand
/// and a width are written in is a decision no ADR records; the tests that are their pass
/// conditions wait for it, and land with it.</para>
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
