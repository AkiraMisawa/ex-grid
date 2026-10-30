using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Packages page's list as the compiler reads it (ADR-0110, ADR-0042): ADR-0042's table of the family by
/// product, checked against the ids release.yml publishes; each package's description read from its assembly;
/// and the family packages each one's project references.
/// </summary>
public class PackagesTests
{
    private const string Repo = "/repo/";

    private const string Page = """
        @page "/getting-started/packages"
        <DocsPage Title="Packages" Lead="The family's packages.">
            <DocsSection Title="The packages"></DocsSection>
        </DocsPage>
        """;

    private const string Family = """
        # Prereleases ship before sign-off

        ### The family ships together

        | Product | Packages |
        |---|---|
        | Grid | `Alpha`, `Alpha.Wrapper` |
        | Data | `Beta` |

        - **One version, not one per product.**
        """;

    private const string Release = """
        jobs:
          publish:
            steps:
              - name: Exactly the packages the package job tested
                run: |
                  ids="Alpha Alpha.Wrapper Beta"
                  for id in $ids; do
        """;

    private static string Project(params string[] references) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <ItemGroup>\n"
        + string.Concat(references.Select(r => $"    <ProjectReference Include=\"..\\{r}\\{r}.csproj\" />\n"))
        + "  </ItemGroup>\n</Project>\n";

    private static (string, string)[] Fixture(params (string, string)[] replace)
    {
        var files = new Dictionary<string, string>
        {
            [$"{GeneratorHarness.SiteDir}/Pages/PackagesPage.razor"] = Page,
            [Repo + "docs/adr/0042-prereleases-ship-before-sign-off.md"] = Family,
            [Repo + ".github/workflows/release.yml"] = Release,
            [Repo + "src/Alpha/Alpha.csproj"] = Project("Beta"),
            [Repo + "src/Alpha.Wrapper/Alpha.Wrapper.csproj"] = Project("Alpha"),
            [Repo + "src/Beta/Beta.csproj"] = Project(),
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

    private static MetadataReference[] Assemblies(string? alphaDescription = "A grid.") =>
    [
        GeneratorHarness.Assembly("Alpha", alphaDescription),
        GeneratorHarness.Assembly("Alpha.Wrapper", "The grid in a design system."),
        GeneratorHarness.Assembly("Beta", "The data."),
    ];

    private static GeneratorRun Run(IEnumerable<(string, string)> files, IEnumerable<MetadataReference>? references = null) =>
        GeneratorHarness.Run([], files, references: references ?? Assemblies());

    private static Diagnostic One(GeneratorRun run)
    {
        var error = Assert.Single(run.Diagnostics.Where(d => d.Id == "EXDOCS017"));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    private static int Line(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;

    private static string FileOf(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().Path;

    [Fact] // ADR-0042, ADR-0110: the family's packages are listed by product, in the table's order, with their descriptions and what each brings
    public void The_list_is_the_tables_with_each_assemblys_description()
    {
        var run = Run(Fixture());

        Assert.Empty(run.Diagnostics);
        var source = run.Source("Packages.g.cs");
        Assert.Contains("""public const string FamilyRecordPath = "docs/adr/0042-prereleases-ship-before-sign-off.md";""", source);
        Assert.Contains("""
                    new("Grid", "Alpha", "A grid.", ["Beta"]),
                    new("Grid", "Alpha.Wrapper", "The grid in a design system.", ["Alpha"]),
                    new("Data", "Beta", "The data.", []),
            """.Replace("\r\n", "\n"), source.Replace("\r\n", "\n"));
    }

    [Fact] // ADR-0042: a tag that would publish other packages than the table lists stops the build, at release.yml's ids line
    public void Ids_that_differ_from_the_table_stop_the_build()
    {
        var run = Run(Fixture((Repo + ".github/workflows/release.yml", Release.Replace("Alpha Alpha.Wrapper Beta", "Alpha Beta"))));

        var error = One(run);
        Assert.EndsWith(".github/workflows/release.yml", FileOf(error));
        Assert.Equal(6, Line(error));
        Assert.Contains("publishes Alpha Beta, and ADR-0042's table of the family lists Alpha Alpha.Wrapper Beta", error.GetMessage());
    }

    [Fact] // ADR-0042: the same packages in another order are refused too: the page lists them in the order a tag publishes them
    public void Ids_in_another_order_stop_the_build()
    {
        var run = Run(Fixture((Repo + ".github/workflows/release.yml", Release.Replace("Alpha Alpha.Wrapper Beta", "Beta Alpha Alpha.Wrapper"))));

        Assert.Contains("in the same order", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a package whose assembly carries no description stops the build, at its row of the table
    public void A_package_without_a_description_stops_the_build()
    {
        var run = Run(Fixture(), Assemblies(alphaDescription: null));

        var error = One(run);
        Assert.EndsWith("0042-prereleases-ship-before-sign-off.md", FileOf(error));
        Assert.Equal(7, Line(error));
        Assert.Contains("Alpha's assembly carries no description", error.GetMessage());
    }

    [Fact] // ADR-0110: a package the site does not reference cannot be described, and stops the build
    public void A_package_the_site_does_not_reference_stops_the_build()
    {
        var run = Run(Fixture(), [GeneratorHarness.Assembly("Alpha", "A grid."), GeneratorHarness.Assembly("Alpha.Wrapper", "The grid.")]);

        Assert.Contains("does not reference the assembly Beta", One(run).GetMessage());
    }

    [Fact] // ADR-0042: a shipped project that references a project outside the family stops the build
    public void A_reference_outside_the_family_stops_the_build()
    {
        var run = Run(Fixture((Repo + "src/Beta/Beta.csproj", Project("Gamma.Tools"))));

        var error = One(run);
        Assert.EndsWith("src/Beta/Beta.csproj", FileOf(error));
        Assert.Contains("references the project ../Gamma.Tools/Gamma.Tools.csproj, which is not one of the family's packages", error.GetMessage());
    }

    [Theory] // ADR-0110: a site with a Packages page needs ADR-0042's table, release.yml and each package's project
    [InlineData("docs/adr/0042-prereleases-ship-before-sign-off.md", "did not reach the build")]
    [InlineData(".github/workflows/release.yml", "did not reach the build")]
    [InlineData("src/Alpha.Wrapper/Alpha.Wrapper.csproj", "src/Alpha.Wrapper/Alpha.Wrapper.csproj did not reach the build")]
    public void A_missing_source_stops_the_build(string path, string message)
    {
        var run = Run(Fixture((Repo + path, "")));

        Assert.Contains(message, One(run).GetMessage());
    }

    [Fact] // ADR-0042: a table whose header was changed cannot be read, and says so
    public void A_table_without_its_header_stops_the_build()
    {
        var run = Run(Fixture((Repo + "docs/adr/0042-prereleases-ship-before-sign-off.md", Family.Replace("| Product | Packages |", "| Product | Ids |"))));

        Assert.Contains("has no table with the header | Product | Packages |", One(run).GetMessage());
    }

    [Fact] // ADR-0042: release.yml with two ids lines is refused: the page checks against exactly one
    public void Two_ids_lines_stop_the_build()
    {
        var run = Run(Fixture((Repo + ".github/workflows/release.yml", Release + "\n          ids=\"Alpha\"\n")));

        Assert.Contains("has 2 ids=\"…\" lines", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a site without the Packages page is not held to its sources
    public void A_site_without_the_page_is_not_checked()
    {
        var run = Run(Fixture(($"{GeneratorHarness.SiteDir}/Pages/PackagesPage.razor", ""), (Repo + ".github/workflows/release.yml", "")));

        Assert.Empty(run.Diagnostics);
        Assert.DoesNotContain("new(\"Grid\"", run.Source("Packages.g.cs"));
    }
}
