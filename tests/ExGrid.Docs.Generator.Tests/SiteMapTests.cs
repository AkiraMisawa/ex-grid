using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The site map run as the compiler runs it (ADR-0110): the pages read from their markup and the
/// navigation read from DocsNav's C#, checked against each other, and emitted for the site.
/// </summary>
public class SiteMapTests
{
    // The navigation's shape, as Layout/DocsNav.cs declares it.
    private const string NavTypes = """
        namespace Site;

        public enum DocsLinkKind { Page, Showcase, Repository, External }

        public sealed record DocsLink(string Title, string Href, DocsLinkKind Kind = DocsLinkKind.Page);

        public enum DocsNavSection { Docs, Showcases, Reference, Project }

        public sealed record DocsGroup(string Title, DocsNavSection Section, System.Collections.Generic.IReadOnlyList<DocsLink> Links);
        """;

    private static string Nav(string groups, string unlisted = "\"figure/*\"", string more = "") => $$"""
        namespace Site;

        public static class DocsNav
        {
            private const string Repository = "https://github.com/AkiraMisawa/ex-grid";

            public static System.Collections.Generic.IReadOnlyList<DocsGroup> Groups { get; } =
            [
        {{groups}}
            ];

            public static System.Collections.Generic.IReadOnlyList<string> Unlisted { get; } = [{{unlisted}}];
        {{more}}
        }
        """;

    private const string Groups = """
                new("Getting started", DocsNavSection.Docs,
                [
                    new("Introduction", ""),
                    new("Columns", "exgrid/columns"),
                ]),
                new("Showcases", DocsNavSection.Showcases,
                [
                    new("Trade blotter", "showcase/blotter", DocsLinkKind.Showcase),
                ]),
                new("Project", DocsNavSection.Project,
                [
                    new("Decision records", "docs/adr/", DocsLinkKind.Repository),
                    new("Report a problem", Repository + "/issues", DocsLinkKind.External),
                ]),
        """;

    private static (string, string) Page(string path, string text) => ($"{GeneratorHarness.SiteDir}/{path}", text);

    private static readonly (string, string)[] Pages =
    [
        Page("Pages/Home.razor", "@page \"/\"\n<DocsSection Title=\"Try it\"></DocsSection>\n"),
        Page("Pages/Grid/GridColumns.razor", """
            @page "/exgrid/columns"

            <DocsPage Product="ExGrid" Title="Columns" Lead="A column says how to read a value.">
                <DocsSection Title="Declaring columns">
                    <DocsExample Title="Types, widths, formats and tones" Builtin="typeof(ColumnTypes)" Mud="typeof(ColumnTypesMud)" />
                </DocsSection>
                <DocsSection Title="API">
                    <ApiTable Type="typeof(GridColumn<>)" />
                </DocsSection>
            </DocsPage>
            """),
        Page("Showcases/Blotter.razor", "@page \"/showcase/blotter\"\n<ShowcaseFrame Title=\"Trade blotter\"></ShowcaseFrame>\n"),
        Page("Figures/GridAnatomy.razor", "@page \"/figure/grid-anatomy\"\n<ShowcaseFrame Title=\"The parts of a grid\"></ShowcaseFrame>\n"),
        // A component, not a page: it has no route, so the site map leaves it out.
        Page("Shared/DocsSection.razor", "<section class=\"docs-section\" id=\"@Id\">@ChildContent</section>\n"),
    ];

    private static GeneratorRun Run(string nav, params (string, string)[] pages) =>
        GeneratorHarness.Run([NavTypes, nav], pages);

    [Fact] // ADR-0110: pages and a navigation that agree build cleanly, and every routed page reaches the site map
    public void A_navigation_that_agrees_with_the_pages_builds_and_emits_the_site_map()
    {
        var run = Run(Nav(Groups), Pages);

        Assert.Empty(run.Diagnostics);
        var map = run.Source("SiteMap.g.cs");
        Assert.Contains("private static partial global::ExGrid.Docs.SitePage[] Read() =>", map);
        Assert.Contains("""new("Pages/Grid/GridColumns.razor", ["exgrid/columns"], "ExGrid", "Columns", "A column says how to read a value.",""", map);
        Assert.Contains("""new("Declaring columns", "declaring-columns", [new("Types, widths, formats and tones", true)], []),""", map);
        Assert.Contains("""new("API", "api", [], ["GridColumn<>"]),""", map);
        Assert.Contains("""new("Pages/Home.razor", [""], null, null, null,""", map);
        Assert.Contains("""new("Showcases/Blotter.razor", ["showcase/blotter"], null, "Trade blotter", null,""", map);
        Assert.Contains("""new("Figures/GridAnatomy.razor", ["figure/grid-anatomy"],""", map);
        Assert.DoesNotContain("Shared/DocsSection.razor", map);
        // By file, so that the map's order does not depend on the order the compiler hands files over.
        Assert.True(map.IndexOf("Figures/GridAnatomy.razor", StringComparison.Ordinal) < map.IndexOf("Pages/Grid/GridColumns.razor", StringComparison.Ordinal));
    }

    [Fact] // ADR-0110: a navigation entry with no page behind it is a build error, at the entry
    public void A_navigation_entry_with_no_page_is_a_build_error()
    {
        var groups = Groups.Replace("""new("Columns", "exgrid/columns"),""", """new("Columns", "exgrid/columns"), new("Rows", "exgrid/rows"),""");
        var run = Run(Nav(groups), Pages);

        var error = Assert.Single(run.Diagnostics);
        Assert.Equal("EXDOCS002", error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("DocsNav links \"Rows\" to 'exgrid/rows', and no page has the route /exgrid/rows", error.GetMessage());
        Assert.EndsWith("Source1.cs", error.Location.GetLineSpan().Path);
        Assert.Equal(12, error.Location.GetLineSpan().StartLinePosition.Line + 1);
    }

    [Fact] // ADR-0110: a routed page that no group lists is a build error, at its @page
    public void A_page_in_no_group_is_a_build_error()
    {
        var run = Run(Nav(Groups), [.. Pages, Page("Pages/Grid/GridRows.razor", "\n@page \"/exgrid/rows\"\n<DocsPage Title=\"Rows\"></DocsPage>\n")]);

        var error = Assert.Single(run.Diagnostics);
        Assert.Equal("EXDOCS003", error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Pages/Grid/GridRows.razor has the route /exgrid/rows, and no group of DocsNav lists it", error.GetMessage());
        var at = error.Location.GetLineSpan();
        Assert.Equal($"{GeneratorHarness.SiteDir}/Pages/Grid/GridRows.razor", at.Path);
        Assert.Equal(2, at.StartLinePosition.Line + 1);
    }

    [Fact] // ADR-0110: a link outside the drawer's groups needs a page too, but does not list one
    public void A_link_outside_the_groups_needs_a_page_and_lists_none()
    {
        var more = """
                public static DocsLink GetStarted { get; } = new("Get started", "getting-started");
            """;
        var run = Run(Nav(Groups, more: more), Pages);

        var missing = Assert.Single(run.Diagnostics);
        Assert.Equal("EXDOCS002", missing.Id);
        Assert.Contains("'getting-started'", missing.GetMessage());

        run = Run(Nav(Groups, more: more), [.. Pages, Page("Pages/GettingStarted.razor", "@page \"/getting-started\"\n")]);

        var unlisted = Assert.Single(run.Diagnostics);
        Assert.Equal("EXDOCS003", unlisted.Id);
        Assert.Contains("/getting-started", unlisted.GetMessage());
    }

    [Fact] // ADR-0110: a route left out on purpose is named in Unlisted; a name that matches no page is an error
    public void Unlisted_routes_are_exempt_and_must_exist()
    {
        var withoutFigures = Run(Nav(Groups, unlisted: ""), Pages);
        Assert.Equal("EXDOCS003", Assert.Single(withoutFigures.Diagnostics).Id);

        var stale = Run(Nav(Groups, unlisted: "\"figure/*\", \"{*path}\""), Pages);
        var error = Assert.Single(stale.Diagnostics);
        Assert.Equal("EXDOCS002", error.Id);
        Assert.Contains("DocsNav.Unlisted names '{*path}', and no page has a route it matches", error.GetMessage());

        var catchAll = Run(Nav(Groups, unlisted: "\"figure/*\", \"{*path}\""), [.. Pages, Page("Pages/NotFound.razor", "@page \"/{*path}\"\n")]);
        Assert.Empty(catchAll.Diagnostics);
    }

    [Fact] // ADR-0110: an address that is not a literal cannot be checked, so it is refused
    public void A_navigation_address_that_is_not_a_literal_is_a_build_error()
    {
        var more = """
                private static string Where() => "exgrid/columns";
                public static DocsLink Columns { get; } = new("Columns", Where());
            """;
        var run = Run(Nav(Groups, more: more), Pages);

        var error = Assert.Single(run.Diagnostics);
        Assert.Equal("EXDOCS004", error.Id);
        Assert.Contains("\"Columns\"'s Href is not a literal", error.GetMessage());
    }

    [Fact] // ADR-0110: links into the repository and out of the site are not routes; each is checked for its shape
    public void Repository_and_external_links_are_checked_for_their_shape()
    {
        var groups = Groups
            .Replace("\"docs/adr/\"", "\"/docs/adr/\"")
            .Replace("Repository + \"/issues\"", "\"github.com/AkiraMisawa/ex-grid/issues\"");
        var run = Run(Nav(groups), Pages);

        Assert.Equal(["EXDOCS002", "EXDOCS002"], run.Diagnostics.Select(d => d.Id));
        Assert.Contains(run.Diagnostics, d => d.GetMessage().Contains("A Repository link is a path from the repository's root"));
        Assert.Contains(run.Diagnostics, d => d.GetMessage().Contains("which is not an https:// address"));
    }

    [Fact] // ADR-0110: a page's own problems reach the build as errors at their line
    public void A_page_s_problems_are_build_errors_at_their_line()
    {
        var run = Run(Nav(Groups), [.. Pages.Where(p => !p.Item1.EndsWith("GridColumns.razor")), Page("Pages/Grid/GridColumns.razor", """
            @page "/exgrid/columns"
            <DocsSection Title="@Heading"></DocsSection>
            <DocsSection Title="API"></DocsSection>
            <DocsSection Title="Api"></DocsSection>
            <DocsExample Builtin="typeof(A)" />
            """)]);

        Assert.Equal(
            [("EXDOCS004", 2), ("EXDOCS005", 4), ("EXDOCS006", 5)],
            run.Diagnostics.Select(d => (d.Id, d.Location.GetLineSpan().StartLinePosition.Line + 1)));
        Assert.All(run.Diagnostics, d =>
        {
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
            Assert.Equal($"{GeneratorHarness.SiteDir}/Pages/Grid/GridColumns.razor", d.Location.GetLineSpan().Path);
        });
        // The site still compiles against what was read; the errors are what is reported.
        Assert.Contains("Pages/Grid/GridColumns.razor", run.Source("SiteMap.g.cs"));
    }

    [Fact] // ADR-0110: with no navigation to check against, the pages are still read and their problems reported
    public void Without_a_navigation_only_the_pages_are_checked()
    {
        var run = GeneratorHarness.Run([], [.. Pages, Page("Pages/Bad.razor", "@page \"/bad\"\n<DocsSection Title=\"@x\"></DocsSection>\n")]);

        Assert.Equal("EXDOCS004", Assert.Single(run.Diagnostics).Id);
        Assert.Contains("Pages/Grid/GridColumns.razor", run.Source("SiteMap.g.cs"));
    }

    [Fact] // ADR-0110: a page outside the site's folder is not the site's
    public void Pages_outside_the_site_folder_are_not_read()
    {
        var run = Run(Nav(Groups), [.. Pages, ("/repo/samples/Other/Pages/Stray.razor", "@page \"/stray\"\n")]);

        Assert.Empty(run.Diagnostics);
        Assert.DoesNotContain("Stray", run.Source("SiteMap.g.cs"));
    }
}
