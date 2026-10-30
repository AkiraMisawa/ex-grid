using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Release Notes as the compiler reads them (ADR-0190): each version's file, the Change Notes and
/// the prerelease note under docs/releases/, read in the notes' Markdown subset, checked against their
/// shape, the decision records and the site map, and held to the version a build publishes.
/// </summary>
public class ReleaseNotesTests
{
    private const string Releases = "/repo/docs/releases/";

    private static (string, string) Site(string path, string text) => ($"{GeneratorHarness.SiteDir}/{path}", text);

    private static (string, string) Notes(string name, string text) => (Releases + name, text);

    private static readonly (string, string)[] Pages =
    [
        Site("Pages/Home.razor", "@page \"/\"\n<DocsSection Title=\"Try it\"></DocsSection>\n"),
        Site("Pages/Grid/GridSelectionAndKeys.razor", """
            @page "/exgrid/selection"
            <DocsPage Product="ExGrid" Title="Selection and keyboard">
                <DocsSection Title="Status-bar figures"></DocsSection>
                <DocsSection Title="Bulk changes, Delete and Undo"></DocsSection>
            </DocsPage>
            """),
        Site("Pages/ReleaseNotesPage.razor", "@page \"/release-notes\"\n<DocsPage Title=\"Release notes\"></DocsPage>\n"),
    ];

    private static readonly (string, string)[] Adrs =
    [
        ("/repo/docs/adr/0042-prereleases-ship-before-sign-off.md", "# 0042\n"),
        ("/repo/docs/adr/0130-the-selection-summary.md", "# 0130\n"),
    ];

    private const string PrereleaseNote =
        "A prerelease (ADR-0042): what it still owes is [at this commit](https://github.com/AkiraMisawa/ex-grid/blob/{ref}/docs/implementation-status.md).\n";

    private const string CandidateNote =
        "A release candidate (ADR-0042): the sign-off is being made against it; what it still owes is [at this commit](https://github.com/AkiraMisawa/ex-grid/blob/{ref}/docs/implementation-status.md).\n";

    // A version's notes with every heading; `replace` swaps a heading's "None." for other lines.
    private static string Version(string name, string published = "2026-10-04", params (string Heading, string Body)[] replace)
    {
        var bodies = new Dictionary<string, string>
        {
            ["Highlights"] = "- The Selection Summary ([Status-bar figures](exgrid/selection#status-bar-figures)).",
            ["Breaking changes"] = "None.",
            ["Behaviour changes"] = "None.",
            ["ExGrid"] = "- **Added:** The Selection Summary (ADR-0130, #65).",
            ["ExSheet"] = "No change. Its packages are republished at this version.",
            ["ExPivot"] = "No change. Its packages are republished at this version.",
            ["Data"] = "No change. Its packages are republished at this version.",
        };
        foreach (var (heading, body) in replace)
            bodies[heading] = body;
        var text = $"# {name}\n\nPublished: {published}\nPackages: ExGrid, ExGrid.MudBlazor\nTarget: net10.0\n";
        foreach (var heading in bodies.Keys)
            text += $"\n## {heading}\n\n{bodies[heading]}\n";
        return text;
    }

    private const string ChangeNote = """
        Product: ExGrid
        Kind: Added
        Docs: exgrid/selection#status-bar-figures
        ADR: 0130

        The Selection Summary: Excel's status-bar figures over the selection (#65).
        """;

    private static (string, string)[] Valid(params (string, string)[] more) =>
    [
        Notes("prerelease-note.md", PrereleaseNote),
        Notes("candidate-note.md", CandidateNote),
        Notes("0.1.0-beta.1.md", Version("0.1.0-beta.1", "2026-09-25")),
        Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2")),
        .. more,
    ];

    private static GeneratorRun Run(IEnumerable<(string, string)> notes, string version = "dev", string docsRef = "", string publishedOn = "") =>
        GeneratorHarness.Run([], [.. Pages, .. Adrs, .. notes], version, docsRef, publishedOn);

    private static Diagnostic[] Notices(GeneratorRun run) =>
        run.Diagnostics.Where(d => d.Id is "EXDOCS007" or "EXDOCS008" or "EXDOCS009" or "EXDOCS010").ToArray();

    private static Diagnostic One(GeneratorRun run, string id)
    {
        var error = Assert.Single(Notices(run));
        Assert.Equal(id, error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    private static int Line(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;

    private static string FileOf(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().Path;

    [Fact] // ADR-0190: well-formed notes build cleanly, and are emitted newest first, with the Change Notes and the prerelease note
    public void Well_formed_notes_build_and_are_emitted_newest_first()
    {
        var run = Run(Valid(Notes("unreleased/selection-summary.md", ChangeNote)));

        Assert.Empty(run.Diagnostics);
        var source = run.Source("ReleaseNotes.g.cs");
        Assert.True(source.IndexOf("\"0.1.0-beta.2\"", StringComparison.Ordinal) < source.IndexOf("\"0.1.0-beta.1\"", StringComparison.Ordinal));
        Assert.Contains("""new("0.1.0-beta.2", "2026-10-04", ["ExGrid", "ExGrid.MudBlazor"], "net10.0",""", source);
        Assert.Contains("""new("Highlights", global::ExGrid.Docs.ReleaseSectionKind.Highlights, null, false,""", source);
        Assert.Contains("""new("Breaking changes", global::ExGrid.Docs.ReleaseSectionKind.Breaking, null, true,""", source);
        Assert.Contains("""new("ExGrid", global::ExGrid.Docs.ReleaseSectionKind.Product, "ExGrid", false,""", source);
        Assert.Contains("""global::ExGrid.Docs.ReleaseKind.Added, [], true),""", source);
        Assert.Contains("""["exgrid/selection#status-bar-figures"], true),""", source);
        Assert.Contains("""new("selection-summary", "ExGrid", global::ExGrid.Docs.ReleaseKind.Added, "exgrid/selection#status-bar-figures", [new("0130", "https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0130-the-selection-summary.md")],""", source);
        Assert.Contains("private static partial string ReadPrereleaseNote() => \"<p>A prerelease (", source);
        Assert.Contains("private static partial string ReadCandidateNote() => \"<p>A release candidate (", source);
        Assert.Contains("/blob/{ref}/docs/implementation-status.md", source);
    }

    [Fact] // ADR-0190: the notes' inline Markdown renders as the site shows it, ADRs and pull requests linked at the build's ref
    public void Inline_markdown_renders_with_its_links()
    {
        var line = "- **Added:** `<ExGrid>` & **bold** [a page](exgrid/selection#status-bar-figures), [out](https://example.com/x), ADR-0130 and (#65).\n  - A nested line.";
        var run = Run(Valid(Notes("0.1.0-beta.3.md", Version("0.1.0-beta.3", "pending", ("ExGrid", line))))
            .Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2"))),
            version: "0.1.0-beta.3", docsRef: "v0.1.0-beta.3", publishedOn: "2026-10-12");

        Assert.Empty(run.Diagnostics);
        var source = run.Source("ReleaseNotes.g.cs");
        Assert.Contains("<code>&lt;ExGrid&gt;</code> &amp; <strong>bold</strong> <a href=\\\"exgrid/selection#status-bar-figures\\\">a page</a>", source);
        Assert.Contains("<a href=\\\"https://example.com/x\\\" target=\\\"_blank\\\" rel=\\\"noopener\\\">out</a>", source);
        Assert.Contains("<a href=\\\"https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0-beta.3/docs/adr/0130-the-selection-summary.md\\\" target=\\\"_blank\\\" rel=\\\"noopener\\\">ADR-0130</a>", source);
        Assert.Contains("<a href=\\\"https://github.com/AkiraMisawa/ex-grid/pull/65\\\" target=\\\"_blank\\\" rel=\\\"noopener\\\">#65</a>", source);
        Assert.Contains("<ul><li>A nested line.</li></ul>", source);
        // The plain text the search reads.
        Assert.Contains("\"<ExGrid> & bold a page, out, ADR-0130 and (#65). A nested line.\"", source);
        Assert.Contains("""new("0.1.0-beta.3", null,""", source);
    }

    [Fact] // ADR-0190: versions are ordered by SemVer precedence, so the newest is found whatever the names
    public void Versions_are_ordered_by_semver_precedence()
    {
        Assert.True(ReleaseNotes.CompareVersions("0.1.0-beta.10", "0.1.0-beta.2") > 0);
        Assert.True(ReleaseNotes.CompareVersions("0.1.0", "0.1.0-rc.1") > 0);
        Assert.True(ReleaseNotes.CompareVersions("0.1.0-rc.1", "0.1.0-beta.9") > 0);
        Assert.True(ReleaseNotes.CompareVersions("1.0.0-alpha", "1.0.0-alpha.1") < 0);
        Assert.True(ReleaseNotes.CompareVersions("1.0.0-1", "1.0.0-alpha") < 0);
        Assert.True(ReleaseNotes.CompareVersions("0.10.0", "0.9.0") > 0);
        Assert.Equal(0, ReleaseNotes.CompareVersions("0.1.0-beta.2", "0.1.0-beta.2"));
    }

    [Fact] // ADR-0190: a version's first line names it, as its file does
    public void A_first_line_that_differs_from_the_file_name_is_an_error()
    {
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.1.md")).Append(Notes("0.1.0-beta.1.md", Version("0.1.0-beta.11", "2026-09-25"))));

        var error = One(run, "EXDOCS008");
        Assert.Contains("begin with the line '# 0.1.0-beta.1'", error.GetMessage());
        Assert.EndsWith("docs/releases/0.1.0-beta.1.md", FileOf(error));
        Assert.Equal(1, Line(error));
    }

    [Theory] // ADR-0190: every heading is present, in order; a missing, repeated, unknown or out-of-order one is an error at its line
    [InlineData("## Breaking changes\n\nNone.\n\n## Highlights", "'## Highlights' is written twice, or out of order")]
    [InlineData("## Breaking changes\n\nNone.\n\n## Upgrading", "'## Upgrading' is not one of the notes' headings")]
    [InlineData("## Signed off", "Only a version without a suffix, or a release candidate (-rc.N) rehearsing it, carries '## Signed off'")]
    public void A_heading_out_of_place_is_an_error(string extra, string message)
    {
        var text = Version("0.1.0-beta.2").Replace("## Data\n", extra + "\n\n## Data\n");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        Assert.Contains(Notices(run), d => d.Id == "EXDOCS008" && d.GetMessage().Contains(message));
    }

    [Fact] // ADR-0190, ADR-0280: a version that publishes ExChart's packages carries its heading
    public void A_version_publishing_exchart_carries_its_heading()
    {
        var text = Version("0.1.0-beta.2").Replace("Packages: ExGrid, ExGrid.MudBlazor", "Packages: ExGrid, ExGrid.MudBlazor, ExChart.Engine, ExChart");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));
        Assert.Contains("'## ExChart' is missing before '## Data'", One(run, "EXDOCS008").GetMessage());

        var carried = text.Replace("## Data\n", "## ExChart\n\n- **Added:** The chart (ADR-0130).\n\n## Data\n");
        Assert.Empty(Notices(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", carried)))));
    }

    [Fact] // ADR-0190, ADR-0280: a version that never published ExChart has no heading for it
    public void A_version_without_exchart_has_no_heading_for_it()
    {
        var text = Version("0.1.0-beta.2").Replace("## Data\n", "## ExChart\n\nNo change. Its packages are republished at this version.\n\n## Data\n");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));
        Assert.Contains("'## ExChart' is not one of the notes' headings", One(run, "EXDOCS008").GetMessage());
    }

    [Fact] // ADR-0190, ADR-0340: a version that publishes ExBoard carries its heading, and one that never did has none
    public void ExBoard_has_a_heading_only_in_a_version_that_publishes_it()
    {
        var text = Version("0.1.0-beta.2").Replace("Packages: ExGrid, ExGrid.MudBlazor", "Packages: ExGrid, ExGrid.MudBlazor, ExBoard");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));
        Assert.Contains("'## ExBoard' is missing before '## Data'", One(run, "EXDOCS008").GetMessage());

        var carried = text.Replace("## Data\n", "## ExBoard\n\n- **Added:** The Board (ADR-0130).\n\n## Data\n");
        Assert.Empty(Notices(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", carried)))));

        var never = Version("0.1.0-beta.2").Replace("## Data\n", "## ExBoard\n\nNo change.\n\n## Data\n");
        run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", never)));
        Assert.Contains("'## ExBoard' is not one of the notes' headings", One(run, "EXDOCS008").GetMessage());
    }

    [Fact] // ADR-0190: a heading left out is an error at the heading that follows it
    public void A_missing_heading_is_an_error_where_it_belongs()
    {
        var text = Version("0.1.0-beta.2").Replace("## Behaviour changes\n\nNone.\n", "");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = One(run, "EXDOCS008");
        Assert.Contains("'## Behaviour changes' is missing before '## ExGrid'", error.GetMessage());
        Assert.Equal(text.Split('\n').ToList().IndexOf("## ExGrid") + 1, Line(error));
    }

    [Fact] // ADR-0190: a heading with nothing under it says None., and a product says its packages are republished
    public void An_empty_heading_is_an_error_that_says_what_to_write()
    {
        var text = Version("0.1.0-beta.2").Replace("## Breaking changes\n\nNone.\n", "## Breaking changes\n").Replace("## Data\n\nNo change. Its packages are republished at this version.\n", "## Data\n");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var errors = Notices(run);
        Assert.All(errors, e => Assert.Equal("EXDOCS008", e.Id));
        Assert.Contains(errors, e => e.GetMessage().Contains("'## Breaking changes' has nothing under it. Write 'None.'"));
        Assert.Contains(errors, e => e.GetMessage().Contains("'## Data' has nothing under it. Write 'No change. Its packages are republished at this version.'"));
    }

    [Theory] // ADR-0190: what a heading holds is checked against what it is for
    [InlineData("ExGrid", "None.", "A product with nothing new says")]
    [InlineData("Breaking changes", "None.\n\n- **ExGrid:** Something.", "None. stands alone")]
    [InlineData("Breaking changes", "- Something broke.", "begins with the products it reaches in bold")]
    [InlineData("Breaking changes", "- **ExPlot:** Something broke.", "begins with the products it reaches in bold")]
    [InlineData("Behaviour changes", "A paragraph.", "is a bullet that begins with the products it reaches")]
    [InlineData("ExGrid", "- Something new.", "begins with what it is, **Added:** or **Fixed:**")]
    [InlineData("ExGrid", "- **Behaviour:** It paints otherwise.", "A behaviour change goes under '## Behaviour changes'")]
    [InlineData("Highlights", "- A line that links nothing.", "Each Highlight links the page of the site that shows it")]
    [InlineData("Highlights", "A paragraph.", "The Highlights are bullets")]
    public void A_line_that_does_not_fit_its_heading_is_an_error(string heading, string body, string message)
    {
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2", replace: (heading, body)))));

        var error = One(run, "EXDOCS008");
        Assert.Contains(message, error.GetMessage());
    }

    [Fact] // ADR-0190: at most five Highlights
    public void A_sixth_highlight_is_an_error()
    {
        var six = string.Join("\n", Enumerable.Range(1, 6).Select(k => $"- Line {k} ([Status-bar figures](exgrid/selection#status-bar-figures))."));
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2", replace: ("Highlights", six)))));

        var error = One(run, "EXDOCS008");
        Assert.Contains("at most five lines", error.GetMessage());
        Assert.Equal(14, Line(error));
    }

    [Fact] // ADR-0190: a breaking line names the products it reaches, and the page files it under each
    public void A_breaking_line_names_its_products()
    {
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md",
            Version("0.1.0-beta.2", replace: ("Breaking changes", "- **ExGrid, ExSheet:** The target is `net10.0` (#21).")))));

        Assert.Empty(Notices(run));
        Assert.Contains("""["ExGrid", "ExSheet"], global::ExGrid.Docs.ReleaseKind.Breaking, [], true),""", run.Source("ReleaseNotes.g.cs"));
        Assert.Contains("\"The target is <code>net10.0</code> (<a href=", run.Source("ReleaseNotes.g.cs"));
    }

    [Theory] // ADR-0190: a version's header lines are Published, Packages and Target, in order, each of its form
    [InlineData("Published: 2026-10-04\nPackages: ExGrid\n", 4, "Target: is missing")]
    [InlineData("Published: 2026-10-04\nTarget: net10.0\n", 4, "Packages: is missing before Target:")]
    [InlineData("Published: 2026-10-04\nPackages: ExGrid\nTarget: net10.0\nCommit: 92bdfae\n", 6, "Commit: is not a version's header line")]
    [InlineData("Published: 4 October 2026\nPackages: ExGrid\nTarget: net10.0\n", 3, "Published: is the UTC day")]
    [InlineData("Published: 2026-02-30\nPackages: ExGrid\nTarget: net10.0\n", 3, "Published: is the UTC day")]
    [InlineData("Published: 2026-10-04\nPackages: ExGrid, ExPlot\nTarget: net10.0\n", 4, "'ExPlot' is not one")]
    [InlineData("Published: 2026-10-04\nPackages: ExGrid\nTarget: .NET 10\n", 5, "Target: is the packages' target framework")]
    [InlineData("Published: 2026-10-04\nPackages: ExGrid\nTarget: net10.0\nThis line is prose.\n", 6, "The header lines are Key: value")]
    public void A_header_line_out_of_form_is_an_error_at_its_line(string headers, int line, string message)
    {
        var text = Version("0.1.0-beta.2");
        text = text.Replace("Published: 2026-10-04\nPackages: ExGrid, ExGrid.MudBlazor\nTarget: net10.0\n", headers);
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = One(run, "EXDOCS008");
        Assert.Contains(message, error.GetMessage());
        Assert.Equal(line, Line(error));
    }

    [Fact] // ADR-0190: only the newest version may still read Published: pending
    public void Pending_on_any_version_but_the_newest_is_an_error()
    {
        Assert.Empty(Notices(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2", "pending"))))));

        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.1.md")).Append(Notes("0.1.0-beta.1.md", Version("0.1.0-beta.1", "pending"))));

        var error = One(run, "EXDOCS008");
        Assert.Contains("0.1.0-beta.1 still reads Published: pending, and 0.1.0-beta.2 is newer", error.GetMessage());
        Assert.EndsWith("0.1.0-beta.1.md", FileOf(error));
        Assert.Equal(3, Line(error));
    }

    [Theory] // ADR-0190: a construct outside the notes' Markdown subset is an error at its line
    [InlineData("### A smaller heading", "Only ## headings")]
    [InlineData("* A star bullet.", "A bullet is written with -")]
    [InlineData("+ A plus bullet.", "A bullet is written with -")]
    [InlineData("1. A numbered line.", "A numbered list")]
    [InlineData("> A quotation.", "A quotation")]
    [InlineData("```", "A fenced code block")]
    [InlineData("| a | b |", "A table")]
    [InlineData("<b>bold</b>", "HTML is not in the notes' subset")]
    [InlineData("---", "A rule or an underlined heading")]
    [InlineData("    indented code", "A line indented by four spaces is a code block")]
    [InlineData("A *starred* word.", "A single * makes italics")]
    [InlineData("An _underlined_ word.", "makes italics")]
    [InlineData("A ~~struck~~ word.", "Strikethrough")]
    [InlineData("An ![image](exgrid/selection).", "An image is not in the notes' subset")]
    [InlineData("A bare https://example.com address.", "A bare address")]
    [InlineData("A <b>tag</b> inline.", "HTML, and an address in <angle brackets>")]
    [InlineData("An &amp; reference.", "The character reference &amp;")]
    [InlineData("An `open code span.", "This code span is never closed")]
    [InlineData("A ``double`` span.", "two backticks")]
    [InlineData("A **bold that never ends.", "This bold is never closed")]
    [InlineData("A [reference][link].", "A reference link")]
    [InlineData("A [bracket] alone.", "This [ starts no link")]
    [InlineData("A [link](exgrid/selection with a space.", "never closed")]
    [InlineData("A\ttab.", "A tab is not in the notes' subset")]
    [InlineData("A bare www.example.com address.", "A bare address")]
    public void A_construct_outside_the_subset_is_an_error_at_its_line(string paragraph, string message)
    {
        var text = Version("0.1.0-beta.2", replace: ("ExSheet", "No change. Its packages are republished at this version.\n\n" + paragraph));
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = Notices(run).First(d => d.GetMessage().Contains(message));
        Assert.Equal("EXDOCS007", error.Id);
        Assert.Equal(text.Split('\n').ToList().IndexOf(paragraph) + 1, Line(error));
    }

    [Theory] // ADR-0190: what the release workflow's rewrite, or GitHub alone, would render otherwise than the site is refused, so the two say one thing
    [InlineData("See [ADR-0130 and the summary](exgrid/selection).", "ADR-0130 inside a link's text")]
    [InlineData("See [the summary, ADR-0130](exgrid/selection).", "ADR-0130 inside a link's text")]
    [InlineData("Read `ADR-0130` first.", "ADR-0130 in a code span")]
    [InlineData("Write `[a](exgrid/selection)` as it is.", "A ]( in a code span")]
    [InlineData("Read x/ADR-0130 first.", "ADR-0130 is joined to the text beside it by '/'")]
    [InlineData("Read \\[ADR-0130 first.", "ADR-0130 is joined to the text beside it by '['")]
    [InlineData("Read ADR-0130_x first.", "ADR-0130 is joined to the text beside it by '_'")]
    [InlineData("Ask @AkiraMisawa about it.", "An @ joined to a name is a mention")]
    [InlineData("Write to someone@example.com.", "An @ joined to a name is a mention")]
    public void What_the_release_text_would_render_otherwise_is_an_error_at_its_line(string paragraph, string message)
    {
        var text = Version("0.1.0-beta.2", replace: ("ExSheet", "No change. Its packages are republished at this version.\n\n" + paragraph));
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = Notices(run).Single(d => d.GetMessage().Contains(message));
        Assert.Equal("EXDOCS007", error.Id);
        Assert.Equal(text.Split('\n').ToList().IndexOf(paragraph) + 1, Line(error));
    }

    [Fact] // ADR-0190: the forms beside those refused are ones the site and the release text render alike
    public void The_forms_beside_them_build_cleanly()
    {
        var paragraph = "[ADR-free text](exgrid/selection) (ADR-0130), `@bind` and `x](y`, Ctrl+Shift+@ and (ADR-0042, ADR-0130).";
        var text = Version("0.1.0-beta.2", replace: ("ExSheet", "No change. Its packages are republished at this version.\n\n" + paragraph));
        Assert.Empty(Notices(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)))));
    }

    [Fact] // ADR-0190: bullets nest one level and no deeper, and a bullet holds one paragraph
    public void Bullets_nest_one_level_and_hold_one_paragraph()
    {
        var deeper = "- **Added:** One.\n  - Nested.\n    - Nested again.";
        var error = One(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2", replace: ("ExGrid", deeper))))), "EXDOCS007");
        Assert.Contains("one nested level", error.GetMessage());

        var paragraphs = "- **Added:** One.\n\n  A second paragraph.";
        error = One(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2", replace: ("ExGrid", paragraphs))))), "EXDOCS007");
        Assert.Contains("A second paragraph inside a bullet", error.GetMessage());

        var wrapped = "- **Added:** One line\n  that wraps.\n  - Nested,\n    and wrapped too.";
        Assert.Empty(Notices(Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2", replace: ("ExGrid", wrapped)))))));
    }

    [Theory] // ADR-0190: a link leads somewhere: an ADR with a file, a route and an anchor the site has
    [InlineData("See ADR-0999.", "ADR-0999 names no decision record")]
    [InlineData("See [a page](exgrid/rows).", "No page of the site has the route /exgrid/rows")]
    [InlineData("See [a section](exgrid/selection#rows).", "The page /exgrid/selection has no section #rows. Its sections are #bulk-changes--delete-and-undo, #status-bar-figures.")]
    [InlineData("See [a page](/exgrid/selection).", "without its leading slash")]
    [InlineData("See [a page](http://example.com).", "neither a route of the site nor an https:// address")]
    public void A_link_that_leads_nowhere_is_an_error(string paragraph, string message)
    {
        var text = Version("0.1.0-beta.2", replace: ("ExPivot", "No change. Its packages are republished at this version.\n\n" + paragraph));
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = One(run, "EXDOCS009");
        Assert.Contains(message, error.GetMessage());
        Assert.Equal(text.Split('\n').ToList().IndexOf(paragraph) + 1, Line(error));
    }

    [Fact] // ADR-0190: the Release notes page's own anchors are its versions and Unreleased
    public void The_release_notes_page_anchors_are_its_versions()
    {
        var text = Version("0.1.0-beta.2", replace: ("ExPivot",
            "No change. Its packages are republished at this version.\n\nSee [beta.1](release-notes#0-1-0-beta-1), [what is next](release-notes#unreleased) and [the start](/)."));
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = One(run, "EXDOCS009");
        Assert.Contains("without its leading slash", error.GetMessage());
    }

    [Theory] // ADR-0190: a Change Note names its Product and its Kind, and says what changed in a paragraph
    [InlineData("Kind: Added\n\nText.", "names no Product")]
    [InlineData("Product: ExGrid\n\nText.", "names no Kind")]
    [InlineData("Product: ExPlot\nKind: Added\n\nText.", "Product is one of ExGrid, ExSheet, ExPivot, ExChart, ExBoard, Data, and 'ExPlot' is not")]
    [InlineData("Product: ExGrid\nKind: Behavior\n\nText.", "(British spelling: Behaviour)")]
    [InlineData("Product: ExGrid\nKind: Added\nPR: 65\n\nText.", "PR: is not a Change Note's header line")]
    [InlineData("Product: ExGrid\nProduct: ExSheet\nKind: Added\n\nText.", "Product: is written twice")]
    [InlineData("Product: ExGrid\nKind: Added\n", "This Change Note says nothing")]
    [InlineData("Product: ExGrid\nKind: Added\n\nOne.\n\nTwo.", "A Change Note is one paragraph")]
    [InlineData("Product: ExGrid\nKind: Added\n\n- A bullet.", "A Change Note is one paragraph")]
    [InlineData("# A title\nProduct: ExGrid\nKind: Added\n\nText.", "The header lines are Key: value")]
    [InlineData("Product: ExGrid\nKind: Added\nADR: 130\n\nText.", "ADR: lists the decisions' four-digit numbers")]
    [InlineData("Product: ExGrid\nKind: Added\nADR: 0130, 0999\n\nText.", "ADR-0999 names no decision record")]
    [InlineData("Product: ExGrid\nKind: Added\nDocs: /exgrid/selection\n\nText.", "Docs: is one route of the site and its anchor")]
    [InlineData("Product: ExGrid\nKind: Added\nDocs: exgrid/selection#rows\n\nText.", "The page /exgrid/selection has no section #rows")]
    public void A_change_note_out_of_form_is_an_error(string text, string message)
    {
        var run = Run(Valid(Notes("unreleased/a-change.md", text)));

        var error = Assert.Single(Notices(run), d => d.GetMessage().Contains(message));
        Assert.EndsWith("docs/releases/unreleased/a-change.md", FileOf(error));
    }

    [Theory] // ADR-0190: one file per change, named for it, directly under unreleased/
    [InlineData("unreleased/Row Key.md")]
    [InlineData("unreleased/live/row-key.md")]
    public void A_change_note_is_named_for_its_change(string name)
    {
        var error = One(Run(Valid(Notes(name, ChangeNote))), "EXDOCS008");
        Assert.Contains("A Change Note is named for its change in lower case", error.GetMessage());
    }

    [Fact] // ADR-0190: a Change Note's name may begin with digits without being taken for a decision record
    public void A_change_note_named_with_digits_is_not_a_decision_record()
    {
        var note = "Product: ExGrid\nKind: Fixed\n\nAs ADR-0999 says.";
        var error = One(Run(Valid(Notes("unreleased/0999-reveal.md", note))), "EXDOCS009");
        Assert.Contains("ADR-0999 names no decision record", error.GetMessage());
    }

    [Fact] // ADR-0190: the prerelease note is held once, with {ref} where the commit goes, and nowhere else
    public void The_prerelease_note_carries_ref_and_nothing_else_does()
    {
        var noRef = One(Run(Valid().Where(f => !f.Item1.EndsWith("prerelease-note.md")).Append(Notes("prerelease-note.md", "A prerelease (ADR-0042).\n"))), "EXDOCS008");
        Assert.Contains("The prerelease note names no {ref}", noRef.GetMessage());

        var heading = One(Run(Valid().Where(f => !f.Item1.EndsWith("prerelease-note.md")).Append(Notes("prerelease-note.md", "## A prerelease\n\n" + PrereleaseNote))), "EXDOCS008");
        Assert.Contains("no heading and no bullet", heading.GetMessage());

        var missing = One(Run(Valid().Where(f => !f.Item1.EndsWith("prerelease-note.md"))), "EXDOCS008");
        Assert.Contains("docs/releases/prerelease-note.md is missing", missing.GetMessage());
        Assert.Equal(Location.None, missing.Location);

        var elsewhere = One(Run(Valid(Notes("unreleased/a-change.md", "Product: ExGrid\nKind: Added\n\nAt `{ref}`."))), "EXDOCS008");
        Assert.Contains("{ref} is the prerelease note's place for the commit", elsewhere.GetMessage());
    }

    [Fact] // ADR-0190: docs/releases/ holds versions, Change Notes and the prerelease note, and nothing else
    public void A_file_that_is_none_of_them_is_an_error()
    {
        var error = One(Run(Valid(Notes("README.md", "# Release notes\n"))), "EXDOCS008");
        Assert.Contains("README.md is none of them", error.GetMessage());
    }

    [Fact] // ADR-0190: a build given the version it publishes needs that version's file
    public void A_build_for_a_version_without_its_notes_is_an_error()
    {
        var error = One(Run(Valid(), version: "0.1.0-beta.3"), "EXDOCS010");
        Assert.Contains("This build publishes 0.1.0-beta.3 (DocsVersion), and docs/releases/0.1.0-beta.3.md does not exist", error.GetMessage());

        var tag = One(Run(Valid(), version: "0.1.0-beta.3", docsRef: "v0.1.0-beta.3", publishedOn: "2026-10-12"), "EXDOCS010");
        Assert.Contains("docs/releases/0.1.0-beta.3.md does not exist", tag.GetMessage());
    }

    [Fact] // ADR-0190: a build given the version it publishes refuses every Change Note still left, at the note
    public void A_build_for_a_version_with_change_notes_left_is_an_error()
    {
        var run = Run(Valid(Notes("unreleased/a.md", ChangeNote), Notes("unreleased/b.md", ChangeNote)), version: "0.1.0-beta.2");

        var errors = Notices(run);
        Assert.Equal(2, errors.Length);
        Assert.All(errors, e => Assert.Equal("EXDOCS010", e.Id));
        Assert.All(errors, e => Assert.Contains("is still under unreleased/, and this build publishes 0.1.0-beta.2", e.GetMessage()));
        Assert.Equal(["/repo/docs/releases/unreleased/a.md", "/repo/docs/releases/unreleased/b.md"], errors.Select(FileOf));

        // A local build, and the same notes, build cleanly.
        Assert.Empty(Notices(Run(Valid(Notes("unreleased/a.md", ChangeNote), Notes("unreleased/b.md", ChangeNote)))));
    }

    [Fact] // ADR-0190: the version a build publishes is the newest of the notes
    public void A_build_for_a_version_that_is_not_the_newest_is_an_error()
    {
        var error = One(Run(Valid(), version: "0.1.0-beta.1"), "EXDOCS010");
        Assert.Contains("docs/releases/0.1.0-beta.2.md is newer", error.GetMessage());

        Assert.Empty(Notices(Run(Valid(), version: "0.1.0-beta.2", docsRef: "v0.1.0-beta.2", publishedOn: "2026-10-04")));
    }

    [Fact] // ADR-0190: a site with a Release notes page and no notes at all lost them on the way, and says so
    public void A_release_notes_page_with_no_notes_is_an_error()
    {
        var error = One(Run([]), "EXDOCS010");
        Assert.Contains("no file under docs/releases/ reached the build", error.GetMessage());

        // A site without the page needs none.
        var bare = GeneratorHarness.Run([], [Pages[0]]);
        Assert.Empty(Notices(bare));
    }

    [Fact] // ADR-0190: a file written with CRLF line ends is read the same, and its errors name the same lines
    public void Crlf_files_read_the_same()
    {
        var text = Version("0.1.0-beta.2", replace: ("ExPivot", "No change. Its packages are republished at this version.\n\nSee ADR-0999.")).Replace("\n", "\r\n");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = One(run, "EXDOCS009");
        Assert.Equal(text.Split("\r\n").ToList().IndexOf("See ADR-0999.") + 1, Line(error));
    }

    // A release: a version without a suffix, whose notes carry Known limitations (ADR-0202) and the release's own
    // blocks (ADR-0190, ADR-0205), which a prerelease's never do.

    private const string DefinitionOfDone = """
        # Definition of Done

        ## 4. UI / UX and the presentation contract (UX)

        | ID | Level | Statement | Verification | Pass |
        |---|---|---|---|---|
        | **UX-10** | SHOULD | `--ex-scrollbar-width` narrows the bar | Layer 3 | the Focus stays inside |
        | **FN-20** | SHOULD | The focused cell's full value is available to Chrome | Layer 2 | the raw value |
        | **VZ-12** | MUST | Under a Stretch height, a reported size of 0 paints nothing | Layer 2 | no throw |
        | **PF-6** | OBSERVATIONAL | Frame time while scrolling | Layer 3 | recorded |

        ## 21. What was undecided, and where each answer now lives

        | | Question | Settled as | Recorded in |
        |---|---|---|---|
        | **21.1** | What ships in the release | every ADR | §2 |
        | **21.2** | The ARIA surface | the root owns it | ADR-0033 |

        ### 21.7a What the digit-width measurement actually found

        ### 21.11 Reserved with a named trigger — not open, and not to be mistaken for oversights
        """;

    private static readonly (string, string) Dod = ("/repo/docs/definition-of-done.md", DefinitionOfDone);

    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly string SignedOff = SignedOffAt("0.1.0");

    // A release candidate's Signed off block, its pending form: the sign-off is being made against the candidate, named by its tag.
    private static string PendingAt(string name) =>
        $"- **Pending:** the Definition of Done is being signed off against this release candidate, `v{name}`. The release's notes will name the record and this candidate's commit.";

    // The Signed off block of the version `name`, its record linked at that version's tag.
    private static string SignedOffAt(string name) => string.Join("\n",
        $"- **Record:** [verification/2026-11-02/results.md](https://github.com/AkiraMisawa/ex-grid/blob/v{name}/verification/2026-11-02/results.md)",
        $"- **Commit:** `{Commit}`",
        "- **Toolchain:** .NET SDK 10.0.203, Node 22.11.0, Playwright 1.56.0",
        "- **OS:** Ubuntu 24.04 (CI), Windows 11 24H2 and macOS 15.1 (by hand)",
        "- **Browsers:** Chrome 141 and Edge 141");

    private static readonly string Supported = string.Join("\n",
        "- **Hosts:** WebAssembly and Blazor Server.",
        "- **Browsers:** Chrome and Edge (ADR-0042).",
        "- **Target framework:** `net10.0`, which every newer runtime loads.",
        "- **Packages:** ExGrid and ExGrid.MudBlazor.");

    // A release's notes with every heading, in order; `replace` swaps a heading's body, and `drop` leaves headings out.
    private static string Release(string name = "0.1.0", string published = "pending", string[]? drop = null, params (string Heading, string Body)[] replace)
    {
        var bodies = new List<(string Heading, string Body)>
        {
            ("Highlights", "- The Selection Summary ([Status-bar figures](exgrid/selection#status-bar-figures))."),
            ("Breaking changes", "None."),
            ("Behaviour changes", "None."),
            ("Known limitations", "- **ExGrid, ExSheet:** Find All and Replace are not offered ([Bulk changes, Delete and Undo](exgrid/selection#bulk-changes--delete-and-undo))."),
            ("ExGrid", "- **Added:** The Selection Summary (ADR-0130, #65)."),
            ("ExSheet", "No change. Its packages are republished at this version."),
            ("ExPivot", "No change. Its packages are republished at this version."),
            ("Data", "No change. Its packages are republished at this version."),
            ("Signed off", ReleaseNotes.VersionKindOf(name) == ReleaseNotes.VersionKind.Candidate ? PendingAt(name) : SignedOffAt(name)),
            ("SHOULD criteria accepted failing", "- **UX-10:** The bar's width follows the token on Chrome and not on Edge's overlay scrollbars; accepted in writing on 2026-11-01."),
            ("Out of scope for this version", "- Find All and Replace, a reservation of §21.11 ([accepted in writing](https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/docs/definition-of-done.md#2111-reserved-with-a-named-trigger--not-open-and-not-to-be-mistaken-for-oversights))."),
            ("Supported", Supported),
        };
        var text = $"# {name}\n\nPublished: {published}\nPackages: ExGrid, ExGrid.MudBlazor\nTarget: net10.0\n";
        foreach (var (heading, body) in bodies)
        {
            if (drop?.Contains(heading) == true)
                continue;
            var swapped = replace.Where(r => r.Heading == heading).Select(r => r.Body).DefaultIfEmpty(body).First();
            text += $"\n## {heading}\n\n{swapped}\n";
        }
        return text;
    }

    // A release's notes as the build that publishes it reads them, at its tag, or as any other build does (`publishing: false`).
    private static GeneratorRun RunRelease(string text, bool dod = true, bool publishing = true) => RunVersion("0.1.0", text, dod, publishing);

    // The notes of the version `name`, beside the two betas, as the build that publishes it reads them or as any other build does.
    private static GeneratorRun RunVersion(string name, string text, bool dod = true, bool publishing = true) =>
        Run(dod ? Valid(Notes(name + ".md", text), Dod) : Valid(Notes(name + ".md", text)),
            publishing ? name : "dev", publishing ? "v" + name : "", publishing ? "2026-11-03" : "");

    private static int LineOf(string text, string line) => text.Split('\n').ToList().FindIndex(l => l.StartsWith(line, StringComparison.Ordinal)) + 1;

    [Fact] // ADR-0190, ADR-0202, ADR-0205: a release's notes with every block, in order, build cleanly and are emitted with each block's kind
    public void A_release_with_every_block_builds_and_is_emitted()
    {
        var run = RunRelease(Release());

        Assert.Empty(Notices(run));
        var source = run.Source("ReleaseNotes.g.cs");
        Assert.True(source.IndexOf("\"0.1.0\"", StringComparison.Ordinal) < source.IndexOf("\"0.1.0-beta.2\"", StringComparison.Ordinal));
        var kinds = new[] { "Highlights", "Breaking", "Behaviour", "KnownLimitations", "Product", "Product", "Product", "Product", "SignedOff", "ShouldAccepted", "OutOfScope", "Supported" };
        var at = source.IndexOf("new(\"0.1.0\",", StringComparison.Ordinal);
        foreach (var kind in kinds)
        {
            at = source.IndexOf("global::ExGrid.Docs.ReleaseSectionKind." + kind + ",", at + 1, StringComparison.Ordinal);
            Assert.True(at > 0, $"the section {kind} is emitted in its place");
        }
        // A known limitation names its products for the page's filter, and is no change, so it marks no section.
        Assert.Contains("""["ExGrid", "ExSheet"], global::ExGrid.Docs.ReleaseKind.None, ["exgrid/selection#bulk-changes--delete-and-undo"], true),""", source);
        // The labelled lines keep their labels, and the record and the commit read as written.
        Assert.Contains("<strong>Record:</strong> <a href=\\\"https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-11-02/results.md\\\"", source);
        Assert.Contains($"<strong>Commit:</strong> <code>{Commit}</code>", source);
        Assert.Contains("<strong>UX-10:</strong> The bar", source);
        Assert.Contains("<strong>Packages:</strong> ExGrid and ExGrid.MudBlazor.", source);
    }

    [Theory] // ADR-0202, ADR-0205: a version without a suffix carries each block, and a missing one is an error that says what it holds
    [InlineData("Known limitations", "'## Known limitations' is missing before '## ExGrid'. A version without a suffix is a release, and its notes say what it does not do", "ADR-0202")]
    [InlineData("Signed off", "'## Signed off' is missing before '## SHOULD criteria accepted failing'. A version without a suffix is a release, and its notes say the verification/<date>/results.md", "Step 7")]
    [InlineData("SHOULD criteria accepted failing", "'## SHOULD criteria accepted failing' is missing before '## Out of scope for this version'", "§1 and Step 7")]
    [InlineData("Out of scope for this version", "'## Out of scope for this version' is missing before '## Supported'", "ADR-0205")]
    [InlineData("Supported", "'## Supported' is missing. A version without a suffix is a release, and its notes say the hosts, the browsers, the target framework and the packages", "ADR-0205")]
    public void A_release_without_a_block_is_an_error(string heading, string message, string source)
    {
        var text = Release(drop: [heading]);
        var error = One(RunRelease(text), "EXDOCS008");

        Assert.Contains(message, error.GetMessage());
        Assert.Contains(source, error.GetMessage());
        Assert.EndsWith("docs/releases/0.1.0.md", FileOf(error));
    }

    [Fact] // ADR-0190, ADR-0202, ADR-0205: a release's blocks stand in their order, Known limitations after Behaviour changes and the release's own after the products
    public void A_release_block_out_of_order_is_an_error_at_its_line()
    {
        var text = Release(drop: ["Known limitations"]).Replace("## Supported\n",
            "## Known limitations\n\n- **ExGrid:** A column band is not drawn ([Status-bar figures](exgrid/selection#status-bar-figures)).\n\n## Supported\n");
        var errors = Notices(RunRelease(text));

        Assert.All(errors, e => Assert.Equal("EXDOCS008", e.Id));
        var missing = Assert.Single(errors, e => e.GetMessage().Contains("'## Known limitations' is missing before '## ExGrid'"));
        Assert.Equal(LineOf(text, "## ExGrid"), Line(missing));
        var late = Assert.Single(errors, e => e.GetMessage().Contains("'## Known limitations' is written twice, or out of order"));
        Assert.Equal(LineOf(text, "## Known limitations"), Line(late));
        Assert.Contains("Known limitations, ExGrid, ExSheet, ExPivot, Data, Signed off, SHOULD criteria accepted failing, Out of scope for this version, Supported", late.GetMessage());
    }

    // What a version's suffix makes its notes (ADR-0190 and ADR-0205, 2026-10-12): a version without a suffix and a
    // release candidate carry a release's blocks, checked alike; an alpha, a beta or any other prerelease carries none.

    [Theory] // ADR-0190, ADR-0205: a version is a release, a release candidate or a prerelease by its suffix, as the release workflow reads its tag
    [InlineData("0.1.0", ReleaseNotes.VersionKind.Release)]
    [InlineData("1.12.3", ReleaseNotes.VersionKind.Release)]
    [InlineData("0.1.0-rc.1", ReleaseNotes.VersionKind.Candidate)]
    [InlineData("0.1.1-rc.12", ReleaseNotes.VersionKind.Candidate)]
    [InlineData("0.1.0-beta.2", ReleaseNotes.VersionKind.Prerelease)]
    [InlineData("0.2.0-alpha.1", ReleaseNotes.VersionKind.Prerelease)]
    [InlineData("1.0.0-alpha", ReleaseNotes.VersionKind.Prerelease)]
    [InlineData("0.1.0-preview.1", ReleaseNotes.VersionKind.Prerelease)]
    [InlineData("0.1.0-rc1", ReleaseNotes.VersionKind.Prerelease)]
    [InlineData("0.1.0-rc.1.2", ReleaseNotes.VersionKind.Prerelease)]
    public void A_version_s_suffix_says_what_it_is(string version, ReleaseNotes.VersionKind kind)
    {
        Assert.Equal(kind, ReleaseNotes.VersionKindOf(version));
        Assert.Equal(version is "0.1.0-rc1" or "0.1.0-rc.1.2", ReleaseNotes.LooksLikeCandidate(version));
    }

    [Theory] // ADR-0190, ADR-0205 (2026-10-12): a release candidate rehearses its release's notes, so its notes with every block build, on the build that publishes it and on any other
    [InlineData("0.1.0-rc.1")]
    [InlineData("0.1.0-rc.12")]
    public void A_release_candidate_with_every_block_builds(string name)
    {
        var run = RunVersion(name, Release(name));

        Assert.Empty(Notices(run));
        Assert.Empty(Notices(RunVersion(name, Release(name), publishing: false)));
        var source = run.Source("ReleaseNotes.g.cs");
        var at = source.IndexOf($"new(\"{name}\",", StringComparison.Ordinal);
        Assert.True(at > 0);
        foreach (var kind in new[] { "KnownLimitations", "SignedOff", "ShouldAccepted", "OutOfScope", "Supported" })
        {
            at = source.IndexOf("global::ExGrid.Docs.ReleaseSectionKind." + kind + ",", at + 1, StringComparison.Ordinal);
            Assert.True(at > 0, $"the candidate's section {kind} is emitted in its place");
        }
        Assert.Contains($"<strong>Pending:</strong> the Definition of Done is being signed off against this release candidate, <code>v{name}</code>.", source);
        Assert.DoesNotContain("<strong>Record:</strong>", source);
    }

    [Theory] // ADR-0190, ADR-0202, ADR-0205 (2026-10-12): a release candidate missing one of a release's blocks is refused, saying what the block holds
    [InlineData("Known limitations", "'## Known limitations' is missing before '## ExGrid'", "what it does not do", "(ADR-0202)")]
    [InlineData("Signed off", "'## Signed off' is missing before '## SHOULD criteria accepted failing'", "in its pending form, one line beginning **Pending:**, that the sign-off is being made against this candidate", "(the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205)")]
    [InlineData("SHOULD criteria accepted failing", "'## SHOULD criteria accepted failing' is missing before '## Out of scope for this version'", "each SHOULD criterion that fails", "(the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205)")]
    [InlineData("Out of scope for this version", "'## Out of scope for this version' is missing before '## Supported'", "§21 items", "(ADR-0205)")]
    [InlineData("Supported", "'## Supported' is missing.", "the hosts, the browsers, the target framework and the packages", "(ADR-0205)")]
    public void A_release_candidate_without_a_block_is_an_error(string heading, string missing, string what, string source)
    {
        var text = Release("0.1.0-rc.1", drop: [heading]);

        foreach (var publishing in new[] { true, false })
        {
            var error = One(RunVersion("0.1.0-rc.1", text, publishing: publishing), "EXDOCS008");
            Assert.Contains(missing, error.GetMessage());
            Assert.Contains("A release candidate rehearses its release's notes (ADR-0190, ADR-0205), so they carry every block a version without a suffix does", error.GetMessage());
            Assert.Contains(what, error.GetMessage());
            Assert.Contains(source, error.GetMessage());
            Assert.EndsWith("docs/releases/0.1.0-rc.1.md", FileOf(error));
        }
    }

    [Fact] // ADR-0190, ADR-0205 (2026-10-12): a release candidate written as a beta is, with none of a release's blocks, is refused for each
    public void A_release_candidate_written_as_a_beta_is_an_error_for_each_block()
    {
        var text = Version("0.1.0-rc.1", "pending");
        var errors = Notices(RunVersion("0.1.0-rc.1", text));

        Assert.Equal(5, errors.Length);
        Assert.All(errors, e => Assert.Equal("EXDOCS008", e.Id));
        Assert.All(errors, e => Assert.Contains("A release candidate rehearses its release's notes", e.GetMessage()));
        foreach (var heading in ReleaseNotes.StableHeadings)
            Assert.Single(errors, e => e.GetMessage().StartsWith($"'## {heading}' is missing", StringComparison.Ordinal));
    }

    [Theory] // ADR-0190, ADR-0205 (2026-10-12): a release candidate's blocks are checked as a release's are, and against the Definition of Done by the build that publishes it; its Signed off block, in its pending form, is the tests below
    [InlineData("SHOULD criteria accepted failing", "- **VZ-12:** Measured, and accepted in writing.", "EXDOCS008", "VZ-12 is a MUST, and no reason accepts a MUST that fails")]
    [InlineData("Out of scope for this version", "- A column band, §21.12 ([accepted](https://example.com/accepted)).", "EXDOCS009", "§21.12 names no item of the Definition of Done's §21")]
    [InlineData("Supported", "- **Hosts:** WebAssembly and Blazor Server.\n- **Browsers:** Chrome and Edge.\n- **Target framework:** `net8.0`.\n- **Packages:** ExGrid and ExGrid.MudBlazor.", "EXDOCS008", "**Target framework:** names net10.0, as the Target: line does")]
    [InlineData("Known limitations", "- **ExGrid:** Find All and Replace are not offered.", "EXDOCS008", "links the page of the site that says it in full")]
    public void A_release_candidate_s_blocks_are_checked_as_a_release_s_are(string heading, string body, string id, string message)
    {
        const string Name = "0.1.0-rc.1";
        var error = One(RunVersion(Name, Release(Name, replace: (heading, body))), id);

        Assert.Contains(message, error.GetMessage());
        Assert.EndsWith($"docs/releases/{Name}.md", FileOf(error));
    }

    [Theory] // ADR-0190, ADR-0202, ADR-0205 (2026-10-12): an alpha or a beta is not signed off, and carries none of a release's blocks
    [InlineData("0.1.0-beta.2", "Known limitations", "(ADR-0202)")]
    [InlineData("0.1.0-beta.2", "Signed off", "(the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205)")]
    [InlineData("0.1.0-beta.2", "SHOULD criteria accepted failing", "(the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205)")]
    [InlineData("0.1.0-beta.2", "Out of scope for this version", "(ADR-0205)")]
    [InlineData("0.1.0-beta.2", "Supported", "(ADR-0205)")]
    [InlineData("0.2.0-alpha.1", "Known limitations", "(ADR-0202)")]
    [InlineData("0.2.0-alpha.1", "Signed off", "(the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205)")]
    [InlineData("0.2.0-alpha.1", "SHOULD criteria accepted failing", "(the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205)")]
    [InlineData("0.2.0-alpha.1", "Out of scope for this version", "(ADR-0205)")]
    [InlineData("0.2.0-alpha.1", "Supported", "(ADR-0205)")]
    public void An_alpha_or_a_beta_with_a_release_block_is_an_error_at_its_heading(string name, string heading, string source)
    {
        var text = Version(name, "pending").Replace("## Data\n", $"## {heading}\n\nNone.\n\n## Data\n");
        var notes = Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", Version("0.1.0-beta.2")))
            .Where(f => !f.Item1.EndsWith(name + ".md")).Append(Notes(name + ".md", text));

        foreach (var publishing in new[] { true, false })
        {
            var run = Run(publishing ? notes.Append(Dod) : notes, publishing ? name : "dev", publishing ? "v" + name : "", publishing ? "2026-11-03" : "");
            var error = One(run, "EXDOCS008");
            Assert.Contains($"Only a version without a suffix, or a release candidate (-rc.N) rehearsing it, carries '## {heading}' {source}. "
                + $"{name} is another prerelease, as an alpha or a beta is: it is not signed off", error.GetMessage());
            Assert.Equal(LineOf(text, "## " + heading), Line(error));
            Assert.EndsWith($"docs/releases/{name}.md", FileOf(error));
        }
    }

    [Theory] // ADR-0190, ADR-0205 (2026-10-12): an alpha or a beta written with every block a release carries is refused at each of them
    [InlineData("0.2.0-beta.1")]
    [InlineData("0.2.0-alpha.1")]
    public void An_alpha_or_a_beta_written_as_a_release_is_an_error_for_each_block(string name)
    {
        var text = Release(name);
        var errors = Notices(RunVersion(name, text));

        Assert.Equal(5, errors.Length);
        Assert.All(errors, e => Assert.Equal("EXDOCS008", e.Id));
        foreach (var heading in ReleaseNotes.StableHeadings)
        {
            var error = Assert.Single(errors, e => e.GetMessage().Contains($"carries '## {heading}'"));
            Assert.Equal(LineOf(text, "## " + heading), Line(error));
        }
    }

    [Fact] // ADR-0190, ADR-0202, ADR-0205: a release written as a beta is, with none of its blocks, is refused for each
    public void A_release_written_as_a_beta_is_an_error_for_each_block()
    {
        var errors = Notices(RunRelease(Version("0.1.0", "pending")));

        Assert.Equal(5, errors.Length);
        Assert.All(errors, e => Assert.Equal("EXDOCS008", e.Id));
        Assert.All(errors, e => Assert.Contains("A version without a suffix is a release, and its notes say", e.GetMessage()));
        foreach (var heading in ReleaseNotes.StableHeadings)
            Assert.Single(errors, e => e.GetMessage().StartsWith($"'## {heading}' is missing", StringComparison.Ordinal));
    }

    [Theory] // ADR-0190, ADR-0205: a suffix that begins rc and is not -rc.N would be read as a beta's notes, so the name is refused
    [InlineData("0.1.0-rc1")]
    [InlineData("0.1.0-RC.1")]
    [InlineData("0.1.0-rc.0")]
    [InlineData("0.1.0-rc")]
    public void A_release_candidate_named_otherwise_than_rc_n_is_an_error(string name)
    {
        // Dated, and read by a build that publishes no version: -RC.1 sorts below the betas, as SemVer compares text.
        var error = One(RunVersion(name, Version(name), publishing: false), "EXDOCS008");

        Assert.Contains($"{name} looks like a release candidate, and a candidate is written -rc.N with N from 1, as 0.1.0-rc.1", error.GetMessage());
        Assert.Equal(1, Line(error));
    }

    [Theory] // ADR-0205: Signed off names the record at the tag, the full commit X the release workflow reads, and the toolchain, OS and browsers, one line each in order
    [InlineData("- **Commit:** `0123456`", "**Commit:** is the commit X the sign-off was made against, its full 40 characters", "- **Commit:**")]
    [InlineData("- **Commit:** `0123456789ABCDEF0123456789ABCDEF01234567`", "its full 40 characters in lower case", "- **Commit:**")]
    [InlineData("- **Commit:** 0123456789abcdef0123456789abcdef01234567", "and this line names none", "- **Commit:**")]
    [InlineData("- **Record:** `verification/2026-11-02/results.md`", "**Record:** links the record at this version's tag, as [verification/2026-11-02/results.md](https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-11-02/results.md)", "- **Record:**")]
    [InlineData("- **Record:** [the record](https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-11-02/results.md)", "**Record:** names no verification/<date>/results.md", "- **Record:**")]
    [InlineData("- **Record:** `verification/a/results.md` and `verification/b/results.md`", "names 2 records", "- **Record:**")]
    [InlineData("- **Toolchain:**", "**Toolchain:** says nothing. Name the versions of the toolchain", "- **Toolchain:**")]
    [InlineData("- **Date:** 2026-11-02", "**Date:** is not one of them", "- **Date:**")]
    [InlineData("- On 2026-11-02.", "this one begins with none", "- On 2026-11-02.")]
    [InlineData("Signed off by the user.", "'## Signed off' holds bullets, each beginning with its label in bold", "Signed off by the user.")]
    public void A_signed_off_line_out_of_form_is_an_error_at_its_line(string line, string message, string at)
    {
        var lines = SignedOff.Split('\n').ToList();
        var label = line.StartsWith("- **", StringComparison.Ordinal) ? line.Substring(0, line.IndexOf(":**", StringComparison.Ordinal) + 3) : null;
        var index = label is null ? -1 : lines.FindIndex(l => l.StartsWith(label, StringComparison.Ordinal));
        if (index >= 0)
            lines[index] = line;
        else
            lines.Add(line.StartsWith("- ", StringComparison.Ordinal) ? line : "\n" + line);
        var text = Release(replace: ("Signed off", string.Join("\n", lines)));

        var error = One(RunRelease(text), "EXDOCS008");
        Assert.Contains(message, error.GetMessage());
        Assert.Equal(LineOf(text, at), Line(error));
    }

    [Fact] // ADR-0205: the record is linked at this version's tag, where it exists; a link elsewhere is an error at the link
    public void A_record_linked_elsewhere_than_the_tag_is_an_error()
    {
        var text = Release(replace: ("Signed off", SignedOff.Replace("/blob/v0.1.0/", "/blob/v0.1.0-rc.2/")));

        var error = One(RunRelease(text), "EXDOCS009");
        Assert.Contains("This link leads to https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0-rc.2/verification/2026-11-02/results.md, and the record this line names is https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-11-02/results.md", error.GetMessage());
        Assert.Equal(LineOf(text, "- **Record:**"), Line(error));
    }

    [Theory] // ADR-0205: the release workflow reads the one record and the one commit the block names, as written, so a second of either is refused first here
    [InlineData("- **OS:** Ubuntu 24.04, as fedcba9876543210fedcba9876543210fedcba98 recorded.", "'## Signed off' names a second commit, fedcba9876543210fedcba9876543210fedcba98", "- **OS:**")]
    [InlineData("- **OS:** Ubuntu 24.04, as `verification/2026-10-30/results.md` says.", "verification/2026-10-30/results.md is named outside the **Record:** line", "- **OS:**")]
    [InlineData("- **OS:** Ubuntu 24.04\n  - **Record:** verification/2026-10-30/results.md, nested", "verification/2026-10-30/results.md is named outside the **Record:** line", "  - **Record:**")]
    [InlineData("- **Record:** [verification/2026-11-02/results.md](https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-11-02/results.md), whose by-hand rows stand on "
        + "[the seventeenth Windows run](https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-10-11-windows-17/results.md)",
        "**Record:** names a second record, verification/2026-10-11-windows-17/results.md, in a link's address", "- **Record:**")]
    [InlineData(@"- **Record:** [verification/2026\-11\-02/results.md](https://github.com/AkiraMisawa/ex-grid/blob/v0.1.0/verification/2026-11-02/results.md)",
        @"**Record:** writes verification/2026\-11\-02/results.md, which the site renders as verification/2026-11-02/results.md", "- **Record:**")]
    public void A_signed_off_block_that_names_two_commits_or_records_is_an_error(string line, string message, string at)
    {
        // The line given replaces the line of the block with its label.
        var label = line.Substring(0, line.IndexOf(":**", StringComparison.Ordinal) + 3);
        var text = Release(replace: ("Signed off", string.Join("\n", SignedOff.Split('\n').Select(l => l.StartsWith(label, StringComparison.Ordinal) ? line : l))));

        var error = One(RunRelease(text), "EXDOCS008");
        Assert.Contains(message, error.GetMessage());
        Assert.Equal(LineOf(text, at), Line(error));

        // The commit linked on its own line names it once.
        var linked = SignedOff.Replace($"`{Commit}`", $"[`{Commit}`](https://github.com/AkiraMisawa/ex-grid/commit/{Commit})");
        Assert.Empty(Notices(RunRelease(Release(replace: ("Signed off", linked)))));
    }

    [Fact] // ADR-0205: Signed off's lines are one each, in order, and each one left out says what to write
    public void A_signed_off_line_missing_or_out_of_order_is_an_error()
    {
        var lines = SignedOff.Split('\n');
        var noCommit = Release(replace: ("Signed off", string.Join("\n", lines.Where(l => !l.StartsWith("- **Commit:**", StringComparison.Ordinal)))));
        var missing = One(RunRelease(noCommit), "EXDOCS008");
        Assert.Contains("**Commit:** is missing before **Toolchain:**. Write - **Commit:** and the commit X every step of §22 ran at", missing.GetMessage());

        var noBrowsers = Release(replace: ("Signed off", string.Join("\n", lines.Take(4))));
        var last = One(RunRelease(noBrowsers), "EXDOCS008");
        Assert.Contains("'## Signed off' names no **Browsers:**. Name each browser the sign-off ran, with its version", last.GetMessage());
        Assert.Equal(LineOf(noBrowsers, "## Signed off"), Line(last));

        var swapped = Release(replace: ("Signed off", string.Join("\n", new[] { lines[1], lines[0] }.Concat(lines.Skip(2)))));
        var errors = Notices(RunRelease(swapped));
        Assert.Contains(errors, e => e.GetMessage().Contains("**Record:** is missing before **Commit:**"));
        Assert.Contains(errors, e => e.GetMessage().Contains("**Record:** is written twice, or out of order"));

        var none = One(RunRelease(Release(replace: ("Signed off", "None."))), "EXDOCS008");
        Assert.Contains("A release is signed off: write its lines, **Record:**, **Commit:**, **Toolchain:**, **OS:** and **Browsers:**", none.GetMessage());

        var empty = One(RunRelease(Release().Replace("## Signed off\n\n" + SignedOff + "\n", "## Signed off\n")), "EXDOCS008");
        Assert.Contains("'## Signed off' has nothing under it. Write its lines", empty.GetMessage());
    }

    [Theory] // DoD §1: a SHOULD criterion accepted failing names a SHOULD of the Definition of Done, with its written reason; a MUST is never accepted
    [InlineData("- **VZ-12:** Measured, and accepted in writing.", "EXDOCS008", "VZ-12 is a MUST, and no reason accepts a MUST that fails")]
    [InlineData("- **PF-6:** Slower than last time.", "EXDOCS008", "PF-6 is OBSERVATIONAL: it is recorded and never a gate")]
    [InlineData("- **UX-99:** A criterion nobody wrote.", "EXDOCS009", "UX-99 names no criterion: the Definition of Done has no row **UX-99**")]
    [InlineData("- **UX-10:**", "EXDOCS008", "**UX-10:** gives no reason")]
    [InlineData("- UX-10: the bar's width.", "EXDOCS008", "begins with its ID in bold, then the written reason")]
    [InlineData("- **Scrollbars:** the bar's width.", "EXDOCS008", "begins with its ID in bold, then the written reason")]
    [InlineData("UX-10 fails.", "EXDOCS008", "begins with its ID in bold, then the written reason")]
    [InlineData("- **UX-10:** One reason.\n- **FN-20, UX-10:** Another.", "EXDOCS008", "UX-10 is accepted twice")]
    [InlineData("- **FN-20, VZ-12:** Both measured.", "EXDOCS008", "VZ-12 is a MUST")]
    public void A_should_criterion_out_of_form_is_an_error_at_its_line(string body, string id, string message)
    {
        var text = Release(replace: ("SHOULD criteria accepted failing", body));

        var error = One(RunRelease(text), id);
        Assert.Contains(message, error.GetMessage());
        var lines = body.Split('\n');
        Assert.Equal(LineOf(text, lines[lines.Length - 1]), Line(error));
    }

    [Fact] // DoD §1: None. when no SHOULD fails, and two SHOULDs may share one reason
    public void Should_criteria_say_none_or_share_a_reason()
    {
        Assert.Empty(Notices(RunRelease(Release(replace: ("SHOULD criteria accepted failing", "None.")))));
        Assert.Empty(Notices(RunRelease(Release(replace: ("SHOULD criteria accepted failing", "- **FN-20, UX-10:** Both read on Chrome only.")))));
    }

    [Fact] // DoD §1, ADR-0205: the criteria are read against the Definition of Done by the build that publishes the release, at its tag;
           // another build reads their form only, so a version already published stays true of its own day
    public void Only_the_build_that_publishes_a_release_reads_its_criteria_against_the_definition_of_done()
    {
        var text = Release(replace: [
            ("SHOULD criteria accepted failing", "- **VZ-12:** Measured, and accepted in writing."),
            ("Out of scope for this version", "- A column band, §21.12 ([accepted](https://example.com/accepted))."),
        ]);

        var errors = Notices(RunRelease(text));
        Assert.Contains(errors, e => e.Id == "EXDOCS008" && e.GetMessage().Contains("VZ-12 is a MUST"));
        Assert.Contains(errors, e => e.Id == "EXDOCS009" && e.GetMessage().Contains("§21.12 names no item"));
        Assert.Empty(Notices(RunRelease(text, publishing: false)));
        Assert.Empty(Notices(RunRelease(text, dod: false, publishing: false)));

        // The form is read by every build.
        var form = One(RunRelease(Release(replace: ("SHOULD criteria accepted failing", "- VZ-12: measured.")), publishing: false), "EXDOCS008");
        Assert.Contains("begins with its ID in bold, then the written reason", form.GetMessage());
    }

    [Fact] // DoD §1: the build that publishes a release reads its criteria against the Definition of Done, and says so when it did not reach the build rather than passing them
    public void A_release_built_without_the_definition_of_done_is_an_error()
    {
        var error = One(RunRelease(Release(), dod: false), "EXDOCS008");

        Assert.Contains("docs/definition-of-done.md did not reach the build", error.GetMessage());
        Assert.Contains("restore that item", error.GetMessage());
        Assert.Empty(Notices(RunRelease(Release(replace: [("SHOULD criteria accepted failing", "None."), ("Out of scope for this version", "None.")]), dod: false)));
    }

    [Theory] // ADR-0205, DoD Step 7: each line out of scope names the §21 item accepted in writing, and links where it was
    [InlineData("- Find All and Replace ([accepted](https://example.com/accepted)).", "EXDOCS008", "names the Definition of Done's §21 item accepted in writing")]
    [InlineData("- Find All and Replace, §21.11.", "EXDOCS008", "names the Definition of Done's §21 item accepted in writing")]
    [InlineData("Find All and Replace, §21.11 ([accepted](https://example.com/accepted)).", "EXDOCS008", "is a bullet that names")]
    [InlineData("- A column band, §21.12 ([accepted](https://example.com/accepted)).", "EXDOCS009", "§21.12 names no item of the Definition of Done's §21, whose items are §21.1, §21.2, §21.7a, §21.11")]
    public void A_line_out_of_scope_out_of_form_is_an_error_at_its_line(string body, string id, string message)
    {
        var text = Release(replace: ("Out of scope for this version", body));

        var error = One(RunRelease(text), id);
        Assert.Contains(message, error.GetMessage());
        Assert.Equal(LineOf(text, body), Line(error));
    }

    [Fact] // ADR-0205: None. when nothing was left out, and an item may be linked on the site or at the repository
    public void Out_of_scope_says_none_or_links_its_items()
    {
        Assert.Empty(Notices(RunRelease(Release(replace: ("Out of scope for this version", "None.")))));
        Assert.Empty(Notices(RunRelease(Release(replace: ("Out of scope for this version",
            "- The Cell Editor's richer ARIA contract (§21.11, §21.2), as [the selection page](exgrid/selection#status-bar-figures) says.")))));
    }

    [Theory] // ADR-0205: Supported names the hosts, browsers, target framework and packages, as the header lines do
    [InlineData("- **Browsers:** Chrome and Edge.\n- **Target framework:** `net10.0`.\n- **Packages:** ExGrid and ExGrid.MudBlazor.", "**Hosts:** is missing before **Browsers:**. Name the hosts it supports")]
    [InlineData("- **Hosts:** WebAssembly and Blazor Server.\n- **Browsers:** Chrome and Edge.\n- **Target framework:** `net8.0`.\n- **Packages:** ExGrid and ExGrid.MudBlazor.", "**Target framework:** names net10.0, as the Target: line does")]
    [InlineData("- **Hosts:** WebAssembly and Blazor Server.\n- **Browsers:** Chrome and Edge.\n- **Target framework:** `net10.0`.\n- **Packages:** ExGrid.", "**Packages:** leaves out ExGrid.MudBlazor, which the Packages: line names")]
    [InlineData("- **Hosts:** WebAssembly and Blazor Server.\n- **Browsers:** Chrome and Edge.\n- **Target framework:** `net10.0`.\n- **Packages:** ExGrid, ExGrid.MudBlazor and ExSheet.", "**Packages:** names ExSheet, which the Packages: line does not")]
    [InlineData("- **Hosts:** WebAssembly and Blazor Server.\n- **Browsers:** Chrome and Edge.\n- **Target framework:** `net10.0`.\n- **Packages:** ExGrid and ExGrid.MudBlazor.\n- **Platforms:** Linux.", "**Platforms:** is not one of them")]
    [InlineData("None.", "A release supports something: write its lines, **Hosts:**, **Browsers:**, **Target framework:** and **Packages:**")]
    public void A_supported_line_out_of_form_is_an_error(string body, string message)
    {
        var error = One(RunRelease(Release(replace: ("Supported", body))), "EXDOCS008");

        Assert.Contains(message, error.GetMessage());
    }

    [Theory] // ADR-0202: each known limitation names its products in bold and links the page of the site that says it in full
    [InlineData("- Find All and Replace are not offered ([Bulk changes](exgrid/selection#bulk-changes--delete-and-undo)).", "EXDOCS008", "begins with the products it reaches in bold")]
    [InlineData("- **ExGrid:** Find All and Replace are not offered.", "EXDOCS008", "links the page of the site that says it in full")]
    [InlineData("- **ExGrid:** Find All and Replace are not offered ([a ticket](https://example.com/tickets/1)).", "EXDOCS008", "links the page of the site that says it in full")]
    [InlineData("Find All and Replace are not offered.", "EXDOCS008", "is a bullet that begins with the products it reaches in bold")]
    [InlineData("- **ExGrid:** Find All and Replace are not offered ([Find](exgrid/find)).", "EXDOCS009", "No page of the site has the route /exgrid/find")]
    public void A_known_limitation_out_of_form_is_an_error_at_its_line(string body, string id, string message)
    {
        var text = Release(replace: ("Known limitations", body));

        var error = One(RunRelease(text), id);
        Assert.Contains(message, error.GetMessage());
        Assert.Equal(LineOf(text, body), Line(error));
    }

    [Fact] // ADR-0202: a release with no known limitation says None.
    public void Known_limitations_say_none_when_there_are_none()
    {
        Assert.Empty(Notices(RunRelease(Release(replace: ("Known limitations", "None.")))));
        var empty = One(RunRelease(Release().Replace("## Known limitations\n\n- **ExGrid, ExSheet:** Find All and Replace are not offered ([Bulk changes, Delete and Undo](exgrid/selection#bulk-changes--delete-and-undo)).\n", "## Known limitations\n")), "EXDOCS008");
        Assert.Contains("'## Known limitations' has nothing under it. Write 'None.'", empty.GetMessage());
    }

    [Fact] // ADR-0190: a release's notes build cleanly on a build that publishes no version, a site published from main among them
    public void A_release_builds_cleanly_on_a_build_that_publishes_no_version()
    {
        var run = RunRelease(Release(), publishing: false);

        Assert.Empty(Notices(run));
        Assert.Contains("global::ExGrid.Docs.ReleaseSectionKind.Supported", run.Source("ReleaseNotes.g.cs"));
    }

    // A release candidate's Signed off block (ADR-0190 and ADR-0205, 2026-10-12): its own pending form, saying the sign-off is
    // being made against this candidate, and no other; a version without a suffix never takes it.

    [Theory] // ADR-0190, ADR-0205 (2026-10-12): a release candidate with the pending Signed off form builds, on the build that publishes it and on any other
    [InlineData(true)]
    [InlineData(false)]
    public void A_release_candidate_with_the_pending_signed_off_form_builds(bool publishing)
    {
        var run = RunVersion("0.1.0-rc.2", Release("0.1.0-rc.2"), publishing: publishing);

        Assert.Empty(Notices(run));
        var source = run.Source("ReleaseNotes.g.cs");
        Assert.Contains("""new("Signed off", global::ExGrid.Docs.ReleaseSectionKind.SignedOff, null, false,""", source);
        Assert.Contains("<strong>Pending:</strong> the Definition of Done is being signed off against this release candidate, <code>v0.1.0-rc.2</code>.", source);
    }

    [Fact] // ADR-0190, ADR-0205 (2026-10-12): a version without a suffix is signed off, so the pending form is refused on it, saying which form it takes
    public void A_stable_version_with_the_pending_signed_off_form_is_an_error()
    {
        var text = Release(replace: ("Signed off", PendingAt("0.1.0")));

        foreach (var publishing in new[] { true, false })
        {
            var error = One(RunRelease(text, publishing: publishing), "EXDOCS008");
            Assert.Contains("**Pending:** is a release candidate's Signed off form, which says the sign-off is being made against it. A version without a suffix is published once it is signed off", error.GetMessage());
            Assert.Contains("**Record:**, **Commit:**, **Toolchain:**, **OS:** and **Browsers:**", error.GetMessage());
            Assert.Equal(LineOf(text, "- **Pending:**"), Line(error));
        }
    }

    [Fact] // ADR-0190, ADR-0205 (2026-10-12): a release candidate's sign-off is still being made, so a Signed off block in the stable form is refused on it
    public void A_release_candidate_with_a_stable_form_signed_off_is_an_error()
    {
        var text = Release("0.1.0-rc.1", replace: ("Signed off", SignedOffAt("0.1.0-rc.1")));

        foreach (var publishing in new[] { true, false })
        {
            var error = One(RunVersion("0.1.0-rc.1", text, publishing: publishing), "EXDOCS008");
            Assert.Contains("A release candidate's Signed off block takes its pending form, one line. Write - **Pending:** the Definition of Done is being signed off against this release candidate, `v0.1.0-rc.1`.", error.GetMessage());
            Assert.Contains("are a release's lines, written in the notes of the version without a suffix once its sign-off is made against this candidate", error.GetMessage());
            Assert.Equal(LineOf(text, "- **Record:**"), Line(error));
        }
    }

    [Theory] // ADR-0190, ADR-0205 (2026-10-12): the pending line names the candidate by its tag, and no record and no commit, in one line
    [InlineData("- **Pending:** the Definition of Done is being signed off against this release candidate.", "**Pending:** names the candidate the sign-off is being made against by its tag, `v0.1.0-rc.1`, in a code span.")]
    [InlineData("- **Pending:** the Definition of Done is being signed off against `v0.1.0-rc.2`.", "**Pending:** names the candidate the sign-off is being made against by its tag, `v0.1.0-rc.1`, in a code span.")]
    [InlineData("- **Pending:** being signed off against `v0.1.0-rc.1`, to be recorded in verification/2026-11-02/results.md.", "**Pending:** names verification/2026-11-02/results.md, and a release candidate's block names no record")]
    [InlineData("- **Pending:** being signed off against `v0.1.0-rc.1`, at `0123456789abcdef0123456789abcdef01234567`.", "**Pending:** names the commit 0123456789abcdef0123456789abcdef01234567, and a release candidate's block names none")]
    [InlineData("- **Pending:** being signed off against `v0.1.0-rc.1`.\n  - On Windows first.", "**Pending:** is one line, without lines nested under it")]
    [InlineData("- **Pending:** being signed off against `v0.1.0-rc.1`.\n- **Pending:** and on macOS.", "**Pending:** is written twice, or out of order")]
    [InlineData("None.", "A release candidate's sign-off is being made against it, and its block says so. Write - **Pending:**")]
    [InlineData("Being signed off against `v0.1.0-rc.1`.", "'## Signed off' holds bullets, each beginning with its label in bold, **Pending:**")]
    public void A_pending_line_out_of_form_is_an_error(string body, string message)
    {
        var text = Release("0.1.0-rc.1", replace: ("Signed off", body));
        var errors = Notices(RunVersion("0.1.0-rc.1", text));

        var error = Assert.Single(errors, e => e.GetMessage().Contains(message));
        Assert.Equal("EXDOCS008", error.Id);
        Assert.EndsWith("docs/releases/0.1.0-rc.1.md", FileOf(error));
    }

    [Fact] // ADR-0190, ADR-0205 (2026-10-12): a release candidate's notes open with the candidate's note, held once with {ref}, and not the prerelease note
    public void The_candidate_note_is_held_once_and_needed_by_a_release_candidate()
    {
        var candidate = Notes("0.1.0-rc.1.md", Release("0.1.0-rc.1"));
        var withoutNote = Valid(candidate, Dod).Where(f => !f.Item1.EndsWith("candidate-note.md"));

        var missing = One(Run(withoutNote), "EXDOCS008");
        Assert.Contains("docs/releases/candidate-note.md is missing, and every release candidate's notes begin with it, not with the prerelease note", missing.GetMessage());
        Assert.Equal(Location.None, missing.Location);

        // An alpha's or a beta's notes need only the prerelease note.
        Assert.Empty(Notices(Run(Valid().Where(f => !f.Item1.EndsWith("candidate-note.md")))));

        var noRef = One(Run(withoutNote.Append(Notes("candidate-note.md", "A release candidate (ADR-0042).\n"))), "EXDOCS008");
        Assert.Contains("The candidate's note names no {ref}", noRef.GetMessage());

        var bullet = Notices(Run(withoutNote.Append(Notes("candidate-note.md", "## A release candidate\n\n" + CandidateNote))));
        Assert.Contains("The candidate's note is a paragraph or two, which every release candidate's notes begin with: no heading and no bullet.", Assert.Single(bullet).GetMessage());
    }
}
