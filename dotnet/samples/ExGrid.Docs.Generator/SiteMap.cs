using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>A page as the generator reads it: the file it came from, and what was read.</summary>
/// <param name="FullPath">The file's path on disk, where an error points.</param>
/// <param name="Text">The file's contents, for the lines an error names.</param>
/// <param name="Scan">What was read.</param>
public sealed record PageInput(string FullPath, SourceText Text, PageScan Scan);

/// <summary>
/// The site map (ADR-0110): every routed page with its sections and Examples, checked against the
/// navigation, and emitted for the site's rail, search and page frame.
/// </summary>
public static class SiteMap
{
    /// <summary>A navigation entry names a route no page has.</summary>
    public static readonly DiagnosticDescriptor EntryWithoutPage = new(
        id: "EXDOCS002",
        title: "A navigation entry has no page behind it",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An entry appears only with a page behind it: an entry with nothing behind it would be quietly wrong (ADR-0110).");

    /// <summary>A routed page is in no group of the navigation, and not left out on purpose.</summary>
    public static readonly DiagnosticDescriptor PageNotListed = new(
        id: "EXDOCS003",
        title: "A page is in no navigation group",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every routed page is listed in a group of DocsNav, or named in DocsNav.Unlisted as reached only by its address (ADR-0110).");

    /// <summary>A title the site map reads is not a literal.</summary>
    public static readonly DiagnosticDescriptor NotLiteral = new(
        id: "EXDOCS004",
        title: "A title the site map reads is not text",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The site map is read at build time, so the titles and addresses it holds are written as text: the anchor it makes then equals the one the page renders (ADR-0110).");

    /// <summary>Two sections of a page have one anchor.</summary>
    public static readonly DiagnosticDescriptor DuplicateAnchor = new(
        id: "EXDOCS005",
        title: "Two sections of a page have one anchor",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A link to a section's anchor must reach that section (ADR-0110).");

    /// <summary>A page's markup cannot be read.</summary>
    public static readonly DiagnosticDescriptor Unreadable = new(
        id: "EXDOCS006",
        title: "A page cannot be read for the site map",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The site map reads each routed page's markup: a tag, attribute or comment left open, or a section outside the page's structure, stops it (ADR-0110).");

    /// <summary>
    /// What is wrong with the pages and the navigation together: each page's own problems, then
    /// every entry with no page behind it and every page in no group. With no navigation, only the
    /// pages' own problems.
    /// </summary>
    public static ImmutableArray<Diagnostic> Check(ImmutableArray<PageInput> pages, NavScan? nav)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var page in pages.OrderBy(p => p.Scan.Page.Path, StringComparer.Ordinal))
        {
            foreach (var problem in page.Scan.Problems)
            {
                var descriptor = problem.Kind switch
                {
                    PageProblemKind.TitleNotLiteral => NotLiteral,
                    PageProblemKind.DuplicateAnchor => DuplicateAnchor,
                    _ => Unreadable,
                };
                diagnostics.Add(Diagnostic.Create(descriptor, At(page, problem.Start, problem.Length), problem.Message));
            }
        }
        if (nav is null)
            return diagnostics.ToImmutable();

        var routes = new HashSet<string>(
            pages.SelectMany(p => p.Scan.Page.Routes),
            StringComparer.OrdinalIgnoreCase);
        foreach (var link in nav.Links)
        {
            if (link.Href is null)
            {
                diagnostics.Add(Diagnostic.Create(NotLiteral, link.Location,
                    $"The DocsLink \"{link.Title}\"'s Href is not a literal, so the build cannot check that a page has it. Write the address as text: \"exgrid/columns\"."));
                continue;
            }
            switch (link.Kind)
            {
                case "Page" or "Showcase":
                    var route = RouteOf(link.Href);
                    if (!routes.Contains(route))
                    {
                        diagnostics.Add(Diagnostic.Create(EntryWithoutPage, link.Location,
                            $"DocsNav links \"{link.Title}\" to '{link.Href}', and no page has the route /{route}. "
                            + "Add the page, or remove the entry until the page exists (ADR-0110)."));
                    }
                    break;
                case "External":
                    if (!link.Href.StartsWith("https://", StringComparison.Ordinal))
                    {
                        diagnostics.Add(Diagnostic.Create(EntryWithoutPage, link.Location,
                            $"DocsNav links \"{link.Title}\" outside the site to '{link.Href}', which is not an https:// address. "
                            + "Give the whole address, or make it a Page link to one of the site's routes."));
                    }
                    break;
                case "Repository":
                    if (link.Href.StartsWith("/", StringComparison.Ordinal) || link.Href.Contains("://"))
                    {
                        diagnostics.Add(Diagnostic.Create(EntryWithoutPage, link.Location,
                            $"DocsNav links \"{link.Title}\" into the repository with '{link.Href}'. "
                            + "A Repository link is a path from the repository's root, as docs/adr/ or LICENSE."));
                    }
                    break;
                default:
                    diagnostics.Add(Diagnostic.Create(NotLiteral, link.Location,
                        $"The DocsLink \"{link.Title}\"'s Kind is not a DocsLinkKind member, so the build cannot tell what it links. Write it as DocsLinkKind.Page."));
                    break;
            }
        }
        foreach (var unlisted in nav.Unlisted)
        {
            if (!routes.Any(unlisted.Matches))
            {
                diagnostics.Add(Diagnostic.Create(EntryWithoutPage, unlisted.Location,
                    $"DocsNav.Unlisted names '{unlisted.Pattern}', and no page has a route it matches. Remove it."));
            }
        }

        var listed = new HashSet<string>(
            nav.Links.Where(l => l.InGroup && l.IsRoute && l.Href is not null).Select(l => RouteOf(l.Href!)),
            StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages.OrderBy(p => p.Scan.Page.Path, StringComparer.Ordinal))
        {
            var routesOfPage = page.Scan.Page.Routes;
            for (var k = 0; k < routesOfPage.Length; k++)
            {
                var route = routesOfPage[k];
                if (listed.Contains(route) || nav.Unlisted.Any(u => u.Matches(route)))
                    continue;
                var (start, length) = page.Scan.RouteSpans[k];
                diagnostics.Add(Diagnostic.Create(PageNotListed, At(page, start, length),
                    $"{page.Scan.Page.Path} has the route /{route}, and no group of DocsNav lists it. "
                    + "Add it to the group it belongs to in Layout/DocsNav.cs, or name it in DocsNav.Unlisted if a reader reaches it only by its address (ADR-0110)."));
            }
        }
        return diagnostics.ToImmutable();
    }

    /// <summary>A link's route: its address without a query or an anchor, and without a leading slash.</summary>
    public static string RouteOf(string href)
    {
        var end = href.IndexOfAny(['?', '#']);
        return (end < 0 ? href : href.Substring(0, end)).Trim('/');
    }

    /// <summary>The site map as C#, for the site's <c>SiteMap</c> class: every routed page, by path.</summary>
    public static SourceText Emit(ImmutableArray<PageInput> pages)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from the site's routed pages (ADR-0110).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class SiteMap");
        code.AppendLine("{");
        code.AppendLine("    private static partial global::ExGrid.Docs.SitePage[] Read() =>");
        code.AppendLine("    [");
        foreach (var page in pages.Select(p => p.Scan.Page).OrderBy(p => p.Path, StringComparer.Ordinal))
        {
            code.Append("        new(").Append(Literal(page.Path)).Append(", [")
                .Append(string.Join(", ", page.Routes.Select(Literal))).Append("], ")
                .Append(Literal(page.Product)).Append(", ")
                .Append(Literal(page.Title)).Append(", ")
                .Append(Literal(page.Lead)).AppendLine(",");
            code.AppendLine("        [");
            foreach (var section in page.Sections)
            {
                code.Append("            new(").Append(Literal(section.Title)).Append(", ").Append(Literal(section.Anchor)).Append(", [")
                    .Append(string.Join(", ", section.Examples.Select(e => "new(" + Literal(e.Title) + ", " + (e.HasMud ? "true" : "false") + ")")))
                    .Append("], [")
                    .Append(string.Join(", ", section.Types.Select(Literal)))
                    .AppendLine("]),");
            }
            code.AppendLine("        ]),");
        }
        code.AppendLine("    ];");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string Literal(string? text) =>
        text is null ? "null" : SymbolDisplay.FormatLiteral(text, quote: true);

    private static Location At(PageInput page, int start, int length)
    {
        var end = Math.Min(start + Math.Max(length, 0), page.Text.Length);
        start = Math.Min(start, page.Text.Length);
        var span = TextSpan.FromBounds(start, end);
        return Location.Create(page.FullPath, span, page.Text.Lines.GetLinePositionSpan(span));
    }
}
