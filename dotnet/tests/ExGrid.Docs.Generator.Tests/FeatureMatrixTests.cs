using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Feature matrix as the compiler reads it (ADR-0110): the rows FeatureMatrixByHand writes, each citing the
/// record that decides it, and each Wrapper's Chrome read from the compilation — every seam its interface
/// declares, and whether the Wrapper draws it or leaves it to the built-in.
/// </summary>
public class FeatureMatrixTests
{
    private const string Repo = "/repo/";

    private const string Page = """
        @page "/features"
        <DocsPage Title="Feature matrix" Lead="Where the family runs.">
            <DocsSection Title="Where it runs"></DocsSection>
        </DocsPage>
        """;

    private const string DefinitionOfDone = """
        # Definition of Done

        ## 23. The Wrapper — `ExGrid.MudBlazor` (WR)

        ## 24. Blazor Server (SRV)
        """;

    // A Chrome interface with three seams, one of them with a default, and a Wrapper that draws two.
    private const string Products = """
        namespace Grid.Chrome
        {
            public interface IGridChrome
            {
                string? FilterPanel(int context);
                string? CellEditor(int context);
                string? CellMessage(int context) => null;
                static string Name => "grid";
            }
        }

        namespace Grid.Wrapper
        {
            public sealed class WrapperChrome : Grid.Chrome.IGridChrome
            {
                public string? FilterPanel(int context) => "panel";
                public string? CellEditor(int context) => "editor";
            }
        }
        """;

    private const string Records = """
        namespace ExGrid.Docs
        {
            internal sealed record MatrixFact(string Topic, string Text, System.Collections.Generic.IReadOnlyList<string> Cites);
            internal sealed record MatrixWrapper(string Package, string Chrome, string Seams, System.Collections.Generic.IReadOnlyList<string> Cites);
            internal sealed record MatrixSurface(string Seam, string Title, System.Collections.Generic.IReadOnlyList<string> Cites);
        }
        """;

    private const string Hand = """
        namespace ExGrid.Docs
        {
            internal static class FeatureMatrixByHand
            {
                public static System.Collections.Generic.IReadOnlyList<MatrixFact> WhereItRuns { get; } =
                [
                    new("Browsers", "Chrome and Edge.", ["ADR-0017"]),
                    new("Blazor Server", "Every browser test also runs under Blazor Server.", ["DoD §24"]),
                ];

                public static System.Collections.Generic.IReadOnlyList<MatrixWrapper> Wrappers { get; } =
                [
                    new("Grid.Wrapper", "Grid.Wrapper.WrapperChrome", "Grid.Chrome.IGridChrome", ["DoD §23"]),
                ];

                public static System.Collections.Generic.IReadOnlyList<MatrixSurface> Surfaces { get; } =
                [
                    new("IGridChrome.FilterPanel", "The filter panel", ["ADR-0009"]),
                    new("IGridChrome.CellEditor", "The Cell Editor", ["ADR-0010"]),
                    new("IGridChrome.CellMessage", "A cell's message", ["ADR-0034"]),
                ];
            }
        }
        """;

    private static (string, string)[] Fixture(params (string, string)[] replace)
    {
        var files = new Dictionary<string, string>
        {
            [$"{GeneratorHarness.SiteDir}/Pages/FeatureMatrixPage.razor"] = Page,
            [Repo + "docs/adr/0009-filter-panel-contract.md"] = "# 0009\n",
            [Repo + "docs/adr/0010-chrome-seams.md"] = "# 0010\n",
            [Repo + "docs/adr/0017-target-chromium-browsers-only.md"] = "# 0017\n",
            [Repo + "docs/adr/0034-validation.md"] = "# 0034\n",
            [Repo + "docs/definition-of-done.md"] = DefinitionOfDone,
        };
        foreach (var (path, text) in replace)
        {
            if (text.Length == 0)
                files.Remove(path);
            else
                files[path] = text;
        }
        return files.Select(f => (f.Key, f.Value)).ToArray();
    }

    private static GeneratorRun Run(string hand = Hand, (string, string)[]? files = null) =>
        GeneratorHarness.Run([Products, Records, hand], files ?? Fixture());

    private static Diagnostic One(GeneratorRun run)
    {
        var error = Assert.Single(run.Diagnostics.Where(d => d.Id == "EXDOCS018"));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    [Fact] // ADR-0110: each seam is read from the Wrapper's Chrome, drawn by it or left to the built-in, and every citation resolves
    public void Seams_are_read_from_the_wrappers_chrome()
    {
        var run = Run();

        Assert.Empty(run.Diagnostics);
        var source = run.Source("FeatureMatrix.g.cs").Replace("\r\n", "\n");
        Assert.Contains("""
                    new("Grid.Wrapper", "IGridChrome.FilterPanel", true),
                    new("Grid.Wrapper", "IGridChrome.CellEditor", true),
                    new("Grid.Wrapper", "IGridChrome.CellMessage", false),
                ];
            """.Replace("\r\n", "\n"), source);
        Assert.DoesNotContain("IGridChrome.Name", source);
        Assert.Contains("""new("ADR-0017", "ADR-0017", "docs/adr/0017-target-chromium-browsers-only.md", null),""", source);
        Assert.Contains("""new("DoD §24", "Definition of Done §24", "docs/definition-of-done.md", "24-blazor-server-srv"),""", source);
        Assert.Contains("""new("DoD §23", "Definition of Done §23", "docs/definition-of-done.md", "23-the-wrapper--exgridmudblazor-wr"),""", source);
    }

    [Fact] // ADR-0110: a seam the Feature matrix has no title for stops the build, so a new seam reaches the page
    public void A_seam_without_a_title_stops_the_build()
    {
        var run = Run(Hand.Replace("""new("IGridChrome.CellMessage", "A cell's message", ["ADR-0034"]),""", ""));

        Assert.Contains("IGridChrome.CellMessage is a seam a Wrapper can draw, and the Feature matrix has no title for it", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a title for a seam no interface declares stops the build, at the seam's name
    public void A_title_for_no_seam_stops_the_build()
    {
        var run = Run(Hand.Replace("""new("IGridChrome.CellMessage", "A cell's message", ["ADR-0034"]),""",
            """new("IGridChrome.CellMessage", "A cell's message", ["ADR-0034"]), new("IGridChrome.Loading", "The loading bar", ["ADR-0010"]),"""));

        var error = One(run);
        Assert.Contains("'IGridChrome.Loading' names no seam of the Wrappers' Chrome interfaces", error.GetMessage());
        Assert.Equal("\"IGridChrome.Loading\"", error.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(error.Location.SourceSpan));
    }

    [Theory] // ADR-0110: every hand-written row cites a record that exists
    [InlineData("""["ADR-0017"]""", """["ADR-0018"]""", "ADR-0018 names no decision record")]
    [InlineData("""["DoD §24"]""", """["DoD §25"]""", "DoD §25 names no heading of the Definition of Done")]
    [InlineData("""["ADR-0017"]""", "[]", "This Feature matrix row cites nothing")]
    [InlineData("""["ADR-0017"]""", """["the browser decision"]""", "'the browser decision' is not a citation the build reads")]
    public void A_citation_that_leads_nowhere_stops_the_build(string from, string to, string message)
    {
        var run = Run(Hand.Replace(from, to));

        Assert.Contains(message, One(run).GetMessage());
    }

    [Fact] // ADR-0110: a row's text is a literal, so the build can check it
    public void Text_that_is_not_a_literal_stops_the_build()
    {
        var run = Run(Hand.Replace("\"Chrome and Edge.\"", "string.Concat(\"Chrome \", \"and Edge.\")"));

        Assert.Contains("This Feature matrix row's text is not written as text", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a row's text is plain, not Markdown the page would show as it is
    public void Markdown_in_a_row_stops_the_build()
    {
        var run = Run(Hand.Replace("\"Chrome and Edge.\"", "\"**Chrome** and Edge.\""));

        Assert.Contains("holds Markdown or markup", One(run).GetMessage());
    }

    [Theory] // ADR-0110: a Wrapper whose Chrome cannot be read stops the build, rather than leave its table empty
    [InlineData("\"Grid.Wrapper.WrapperChrome\"", "\"Grid.Wrapper.Missing\"", "Grid.Wrapper.Missing is not a type the Docs Site can see")]
    [InlineData("\"Grid.Chrome.IGridChrome\"", "\"System.IDisposable\"", "does not implement the interface System.IDisposable")]
    public void A_wrapper_that_cannot_be_read_stops_the_build(string from, string to, string message)
    {
        var run = Run(Hand.Replace(from, to));

        Assert.Contains(message, One(run).GetMessage());
    }

    [Fact] // ADR-0110: a site with a Feature matrix page and no Wrapper rows stops the build
    public void A_page_without_its_rows_stops_the_build()
    {
        var run = GeneratorHarness.Run([Products, Records], Fixture());

        Assert.Contains("no MatrixWrapper reached the build", One(run).GetMessage());
    }
}
