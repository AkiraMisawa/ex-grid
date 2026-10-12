using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Docs Site's build step run as the compiler runs it (ADR-0110): given the build's properties,
/// a decision record and a documentation file, what it emits and what it refuses.
/// </summary>
public class DocsGeneratorTests
{
    private const string Sha = "4a17e3a0c2b14f7e9d3a5b6c7d8e9f0a1b2c3d4e";

    [Fact] // ADR-0110: a version's site cites the decisions as they stood at its tag
    public void An_ADR_in_a_summary_links_the_record_at_the_tag()
    {
        var run = Run(version: "0.1.0-beta.3", docsRef: "v0.1.0-beta.3", publishedOn: "2026-10-12");

        Assert.Empty(run.Diagnostics);
        Assert.Contains(
            "https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0-beta.3/docs/adr/0016-row-height-is-a-parameter.md",
            run.Source("ApiDocs.g.cs"));
    }

    [Fact] // ADR-0110: a site published on demand links at the commit it was built from
    public void An_ADR_in_a_summary_links_the_record_at_the_commit()
    {
        var run = Run(version: "", docsRef: "main@" + Sha, publishedOn: "");

        Assert.Empty(run.Diagnostics);
        Assert.Contains($"/blob/{Sha}/docs/adr/0016-row-height-is-a-parameter.md", run.Source("ApiDocs.g.cs"));
    }

    [Fact] // ADR-0110: a local build links at main
    public void An_ADR_in_a_summary_links_the_record_on_main_locally()
    {
        var run = Run(version: "dev", docsRef: "", publishedOn: "");

        Assert.Empty(run.Diagnostics);
        Assert.Contains("/blob/main/docs/adr/0016-row-height-is-a-parameter.md", run.Source("ApiDocs.g.cs"));
    }

    [Fact] // ADR-0110, ADR-0190: what the site states it was built from reaches the site
    public void The_build_is_emitted_for_the_site()
    {
        var run = Run(version: "0.1.0-beta.3", docsRef: "v0.1.0-beta.3", publishedOn: "2026-10-12");
        var source = run.Source("DocsBuild.g.cs");

        Assert.Contains("Kind => DocsBuildKind.Tag;", source);
        Assert.Contains("Version => \"0.1.0-beta.3\";", source);
        Assert.Contains("GitRef => \"v0.1.0-beta.3\";", source);
        Assert.Contains("PublishedOn => \"2026-10-12\";", source);
        foreach (var kind in Enum.GetNames<DocsBuildKind>())
            Assert.Contains("    " + kind + ",", source);
    }

    [Fact] // ADR-0110: a lab/* site reaches the site as an experiment, with its branch and commit
    public void A_lab_build_is_emitted_as_an_experiment()
    {
        var source = Run(version: "", docsRef: "lab/wsl-scroll@" + Sha, publishedOn: "").Source("DocsBuild.g.cs");

        Assert.Contains("Kind => DocsBuildKind.Experiment;", source);
        Assert.Contains("Branch => \"lab/wsl-scroll\";", source);
        Assert.Contains($"Commit => \"{Sha}\";", source);
    }

    [Fact] // ADR-0110: inputs that disagree stop the build, with an error that says what to pass
    public void Inputs_that_disagree_are_a_build_error()
    {
        var run = Run(version: "0.1.0-beta.2", docsRef: "v0.1.0-beta.3", publishedOn: "");

        var error = Assert.Single(run.Diagnostics);
        Assert.Equal("EXDOCS001", error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("pass -p:DocsVersion=0.1.0-beta.3", error.GetMessage());
        // The site still compiles against what a local build states, so the error is the one reported.
        Assert.Contains("Kind => DocsBuildKind.Local;", run.Source("DocsBuild.g.cs"));
    }

    private static GeneratorRun Run(string version, string docsRef, string publishedOn) =>
        GeneratorHarness.Run(
            ["namespace Shipped { public class Grid { public int RowHeight { get; set; } } }"],
            [
                ("/repo/docs/adr/0016-row-height-is-a-parameter.md", "# Row height is a parameter\n"),
                ("/repo/src/Shipped/bin/Shipped.xml", """
                    <?xml version="1.0"?>
                    <doc>
                      <assembly><name>Shipped</name></assembly>
                      <members>
                        <member name="P:Shipped.Grid.RowHeight"><summary>The row height (ADR-0016).</summary></member>
                      </members>
                    </doc>
                    """),
            ],
            version, docsRef, publishedOn);
}
