using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExGrid.Docs.Generator;

/// <summary>A value a hand-written Roadmap row is given: text, or <see langword="null"/> when it is not a literal.</summary>
/// <param name="Value">The text.</param>
/// <param name="Location">Where it is written.</param>
public sealed record RoadmapHandValue(string? Value, Location Location);

/// <summary>
/// A row of the Roadmap written by hand (ADR-0191), as the generator reads it from <c>RoadmapByHand</c>:
/// a <c>RoadmapNote</c> — a line of "Toward the next stable version" or "Not planned", with its products
/// and the records it cites — or a <c>RoadmapRecord</c>, a record the page links rather than copies.
/// </summary>
/// <param name="Property">The property it is declared in: <c>TowardStable</c>, <c>NotPlanned</c>, <c>Linked</c>.</param>
/// <param name="IsRecord">Whether it is a <c>RoadmapRecord</c>, which has a title and one cite and no products.</param>
/// <param name="Text">Its text, or a record's title.</param>
/// <param name="About">A record's sentence about what a reader finds there; <see langword="null"/> for a note.</param>
/// <param name="Products">The products it reaches; empty for a record.</param>
/// <param name="Cites">The records it cites: <c>ADR-0031</c>, <c>DoD Step 7</c>, <c>DoD §21.11</c>, <c>docs/implementation-status.md</c>.</param>
/// <param name="Location">Where the row is written.</param>
public sealed record RoadmapHandRow(
    string Property,
    bool IsRecord,
    RoadmapHandValue Text,
    RoadmapHandValue? About,
    ImmutableArray<RoadmapHandValue> Products,
    ImmutableArray<RoadmapHandValue> Cites,
    Location Location);

/// <summary>
/// Reads the Roadmap's hand-written rows from the site's <c>RoadmapByHand</c> class, as the navigation is
/// read from <c>DocsNav</c>: every <c>RoadmapNote</c> and <c>RoadmapRecord</c> it constructs, with the
/// text, products and cites written as literals, so that the build can check each one (ADR-0191).
/// </summary>
public static class RoadmapHandReader
{
    /// <summary>The class the hand-written rows are declared in.</summary>
    public const string ClassName = "RoadmapByHand";

    /// <summary>Whether <paramref name="node"/> declares the hand-written rows.</summary>
    public static bool IsHand(SyntaxNode node) =>
        node is ClassDeclarationSyntax { Identifier.ValueText: ClassName };

    /// <summary>Reads the rows <paramref name="declaration"/> constructs.</summary>
    public static ImmutableArray<RoadmapHandRow> Read(ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancel)
    {
        var rows = ImmutableArray.CreateBuilder<RoadmapHandRow>();
        foreach (var creation in declaration.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            cancel.ThrowIfCancellationRequested();
            if (model.GetSymbolInfo(creation, cancel).Symbol is not IMethodSymbol { ContainingType.Name: "RoadmapNote" or "RoadmapRecord" } constructor)
                continue;
            var isRecord = constructor.ContainingType.Name == "RoadmapRecord";
            var text = new RoadmapHandValue(null, creation.GetLocation());
            RoadmapHandValue? about = isRecord ? new RoadmapHandValue(null, creation.GetLocation()) : null;
            var products = ImmutableArray<RoadmapHandValue>.Empty;
            var cites = ImmutableArray<RoadmapHandValue>.Empty;
            var arguments = creation.ArgumentList?.Arguments ?? default;
            for (var k = 0; k < arguments.Count; k++)
            {
                var argument = arguments[k];
                var parameter = argument.NameColon is { } named
                    ? constructor.Parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.ValueText)
                    : k < constructor.Parameters.Length ? constructor.Parameters[k] : null;
                switch (parameter?.Name)
                {
                    case "Text" or "Title":
                        text = Constant(argument.Expression, model, cancel);
                        break;
                    case "About":
                        about = Constant(argument.Expression, model, cancel);
                        break;
                    case "Products":
                        products = List(argument.Expression, model, cancel);
                        break;
                    case "Cites":
                        cites = List(argument.Expression, model, cancel);
                        break;
                    case "Cite":
                        cites = [Constant(argument.Expression, model, cancel)];
                        break;
                }
            }
            var property = creation.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";
            rows.Add(new RoadmapHandRow(property, isRecord, text, about, products, cites, creation.GetLocation()));
        }
        return rows.ToImmutable();
    }

    private static RoadmapHandValue Constant(ExpressionSyntax expression, SemanticModel model, CancellationToken cancel) =>
        new(model.GetConstantValue(expression, cancel) is { HasValue: true, Value: string value } ? value : null, expression.GetLocation());

    // A list written as a collection expression or an array of literals; anything else is one value that
    // is not text, so that the check names it.
    private static ImmutableArray<RoadmapHandValue> List(ExpressionSyntax expression, SemanticModel model, CancellationToken cancel)
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
            return [new RoadmapHandValue(null, expression.GetLocation())];
        return elements
            .Select(e => e is null ? new RoadmapHandValue(null, expression.GetLocation()) : Constant(e, model, cancel))
            .ToImmutableArray();
    }
}
