using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExGrid.Docs.Generator;

/// <summary>A value a hand-written row is given: text, or <see langword="null"/> when it is not a literal.</summary>
/// <param name="Value">The text.</param>
/// <param name="Location">Where it is written.</param>
public sealed record HandValue(string? Value, Location Location);

/// <summary>
/// A row a reference page writes by hand in C# (ADR-0110), as the generator reads it: the record it
/// constructs, the property it is declared in, and each argument by its parameter's name. A text argument
/// is one value; a list argument (<c>["ADR-0022"]</c>) is each of its elements.
/// </summary>
/// <param name="Type">The record constructed: <c>MatrixFact</c>, <c>KeyRow</c>.</param>
/// <param name="Property">The property it is declared in: <c>WhereItRuns</c>, <c>ExGrid</c>.</param>
/// <param name="Arguments">Each argument by its parameter's name.</param>
/// <param name="Location">Where the row is written.</param>
public sealed record HandRow(
    string Type,
    string Property,
    ImmutableDictionary<string, ImmutableArray<HandValue>> Arguments,
    Location Location)
{
    /// <summary>The text argument <paramref name="name"/>; a value with no text when it is missing.</summary>
    public HandValue Text(string name) =>
        Arguments.TryGetValue(name, out var values) && values.Length == 1 ? values[0] : new HandValue(null, Location);

    /// <summary>The list argument <paramref name="name"/>; empty when it is missing.</summary>
    public ImmutableArray<HandValue> List(string name) =>
        Arguments.TryGetValue(name, out var values) ? values : ImmutableArray<HandValue>.Empty;
}

/// <summary>
/// Reads the rows a reference page's hand-written class constructs, as the navigation is read from
/// <c>DocsNav</c> and the Roadmap's from <c>RoadmapByHand</c>: every construction of the named records,
/// with each argument read as the literal it is written as, so that the build can check what it says and
/// what it cites (ADR-0110).
/// </summary>
public static class HandReader
{
    /// <summary>The rows <paramref name="declaration"/> constructs of the records <paramref name="types"/> names.</summary>
    public static ImmutableArray<HandRow> Read(
        ClassDeclarationSyntax declaration, SemanticModel model, ImmutableHashSet<string> types, CancellationToken cancel)
    {
        var rows = ImmutableArray.CreateBuilder<HandRow>();
        foreach (var creation in declaration.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            cancel.ThrowIfCancellationRequested();
            if (model.GetSymbolInfo(creation, cancel).Symbol is not IMethodSymbol constructor
                || !types.Contains(constructor.ContainingType.Name))
            {
                continue;
            }
            var arguments = ImmutableDictionary.CreateBuilder<string, ImmutableArray<HandValue>>();
            var list = creation.ArgumentList?.Arguments ?? default;
            for (var k = 0; k < list.Count; k++)
            {
                var argument = list[k];
                var parameter = argument.NameColon is { } named
                    ? constructor.Parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.ValueText)
                    : k < constructor.Parameters.Length ? constructor.Parameters[k] : null;
                if (parameter is null)
                    continue;
                arguments[parameter.Name] = parameter.Type.SpecialType == SpecialType.System_String
                    ? [Constant(argument.Expression, model, cancel)]
                    : Elements(argument.Expression, model, cancel);
            }
            var property = creation.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";
            rows.Add(new HandRow(constructor.ContainingType.Name, property, arguments.ToImmutable(), creation.GetLocation()));
        }
        return rows.ToImmutable();
    }

    private static HandValue Constant(ExpressionSyntax expression, SemanticModel model, CancellationToken cancel) =>
        new(model.GetConstantValue(expression, cancel) is { HasValue: true, Value: string value } ? value : null, expression.GetLocation());

    // A list written as a collection expression or an array of literals; anything else is one value that
    // is not text, so that the check names it.
    private static ImmutableArray<HandValue> Elements(ExpressionSyntax expression, SemanticModel model, CancellationToken cancel)
    {
        var elements = expression switch
        {
            CollectionExpressionSyntax collection => collection.Elements
                .Select(e => e is ExpressionElementSyntax element ? element.Expression : null)
                .ToList(),
            ImplicitArrayCreationExpressionSyntax array => array.Initializer.Expressions.Select(e => (ExpressionSyntax?)e).ToList(),
            ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions.Select(e => (ExpressionSyntax?)e).ToList(),
            _ => null,
        };
        if (elements is null)
            return [new HandValue(null, expression.GetLocation())];
        return elements
            .Select(e => e is null ? new HandValue(null, expression.GetLocation()) : Constant(e, model, cancel))
            .ToImmutableArray();
    }
}
