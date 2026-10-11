using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExGrid.Docs.Generator;

/// <summary>A link the site's navigation declares, as the generator reads it from <c>DocsNav</c>.</summary>
/// <param name="Title">What the navigation calls it; empty when it is not a literal.</param>
/// <param name="Href">Its address; <see langword="null"/> when it is not a literal.</param>
/// <param name="Kind">The name of its <c>DocsLinkKind</c>: <c>Page</c>, <c>Showcase</c>, <c>Repository</c> or <c>External</c>.</param>
/// <param name="InGroup">Whether it is one of the drawer's groups' links, which is what lists a page.</param>
/// <param name="Location">Where it is written.</param>
public sealed record NavLink(string Title, string? Href, string Kind, bool InGroup, Location Location)
{
    /// <summary>Whether it names one of this site's routes, which a page must claim.</summary>
    public bool IsRoute => Kind is "Page" or "Showcase";
}

/// <summary>A route the navigation leaves out on purpose: a page reached only by its address.</summary>
/// <param name="Pattern">The route, or a prefix ending in <c>*</c>: <c>figure/*</c>.</param>
/// <param name="Location">Where it is written.</param>
public sealed record NavUnlisted(string Pattern, Location Location)
{
    /// <summary>Whether <paramref name="route"/> is one this entry leaves out.</summary>
    public bool Matches(string route) => Pattern.EndsWith("*", System.StringComparison.Ordinal)
        ? route.StartsWith(Pattern.Substring(0, Pattern.Length - 1), System.StringComparison.OrdinalIgnoreCase)
        : string.Equals(route, Pattern, System.StringComparison.OrdinalIgnoreCase);
}

/// <summary>The navigation as read from <c>DocsNav</c>.</summary>
/// <param name="Links">Every <c>DocsLink</c> it constructs, in the order written.</param>
/// <param name="Unlisted">Its <c>Unlisted</c> routes.</param>
/// <param name="Location">Where the class is declared.</param>
public sealed record NavScan(ImmutableArray<NavLink> Links, ImmutableArray<NavUnlisted> Unlisted, Location Location);

/// <summary>
/// Reads the site's one ordered navigation list, <c>DocsNav</c> (ADR-0110), from its C#: every
/// <c>DocsLink</c> it makes — the drawer's groups, the app bar's and the footer's — and the routes it
/// leaves out on purpose. The site map then checks the two against the routes the pages declare.
/// </summary>
public static class NavReader
{
    /// <summary>The class the navigation is declared in.</summary>
    public const string ClassName = "DocsNav";

    /// <summary>Whether <paramref name="node"/> declares the navigation.</summary>
    public static bool IsNav(SyntaxNode node) =>
        node is ClassDeclarationSyntax { Identifier.ValueText: ClassName };

    /// <summary>Reads the navigation declared by <paramref name="declaration"/>.</summary>
    public static NavScan Read(ClassDeclarationSyntax declaration, SemanticModel model, CancellationToken cancel)
    {
        var links = ImmutableArray.CreateBuilder<NavLink>();
        foreach (var creation in declaration.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            cancel.ThrowIfCancellationRequested();
            if (model.GetSymbolInfo(creation, cancel).Symbol is not IMethodSymbol { ContainingType.Name: "DocsLink" } constructor)
                continue;
            string title = "";
            string? href = null;
            var kind = "Page";
            var arguments = creation.ArgumentList?.Arguments ?? default;
            for (var k = 0; k < arguments.Count; k++)
            {
                var argument = arguments[k];
                var parameter = argument.NameColon is { } named
                    ? constructor.Parameters.FirstOrDefault(p => p.Name == named.Name.Identifier.ValueText)
                    : k < constructor.Parameters.Length ? constructor.Parameters[k] : null;
                switch (parameter?.Name)
                {
                    case "Title":
                        title = model.GetConstantValue(argument.Expression, cancel) is { HasValue: true, Value: string t } ? t : "";
                        break;
                    case "Href":
                        href = model.GetConstantValue(argument.Expression, cancel) is { HasValue: true, Value: string h } ? h : null;
                        break;
                    case "Kind":
                        kind = model.GetSymbolInfo(argument.Expression, cancel).Symbol is IFieldSymbol field ? field.Name : "?";
                        break;
                }
            }
            var inGroup = creation.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText == "Groups";
            links.Add(new NavLink(title, href, kind, inGroup, creation.GetLocation()));
        }

        var unlisted = ImmutableArray.CreateBuilder<NavUnlisted>();
        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.Identifier.ValueText != "Unlisted" || property.Initializer is null)
                continue;
            foreach (var literal in property.Initializer.DescendantNodes().OfType<LiteralExpressionSyntax>())
            {
                if (literal.IsKind(SyntaxKind.StringLiteralExpression))
                    unlisted.Add(new NavUnlisted(literal.Token.ValueText, literal.GetLocation()));
            }
        }

        return new NavScan(links.ToImmutable(), unlisted.ToImmutable(), declaration.Identifier.GetLocation());
    }
}
