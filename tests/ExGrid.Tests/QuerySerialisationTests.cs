using System.Reflection;
using System.Text.Json;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// FN-13 and ST-4: the Query and the View State travel as a serialisable model (ADR-0002). What ExGrid's own
/// JSON entry point writes and reads back (ADR-0207), and when two Queries or two View States are the same,
/// are the shared cases of <c>tests/cases/view-state</c> (ADR-0370 section 5), which
/// <c>Cases/ViewStateJsonCaseTests</c> runs here under three cultures and Vitest runs against the Script
/// Core. What stays here has no counterpart there: a <see cref="RowRange"/> and a
/// <see cref="ColumnWidthChange"/> through <c>System.Text.Json</c> directly, and the public signatures read by
/// reflection.
/// </summary>
public class QuerySerialisationTests
{
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

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

    [Fact] // ADR-0016 / ST-4: a drag's result, which the Consumer records as the Fixed width, round-trips too
    public void ST4_a_column_width_change_round_trips()
    {
        var change = new ColumnWidthChange("Notional", 512.5);

        Assert.Equal(change, RoundTrip(change));
    }
}
