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
    [InlineData("## Signed off", "Only a version without a suffix carries '## Signed off'")]
    public void A_heading_out_of_place_is_an_error(string extra, string message)
    {
        var text = Version("0.1.0-beta.2").Replace("## Data\n", extra + "\n\n## Data\n");
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        Assert.Contains(Notices(run), d => d.Id == "EXDOCS008" && d.GetMessage().Contains(message));
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
    [InlineData("Breaking changes", "- **ExChart:** Something broke.", "begins with the products it reaches in bold")]
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
    [InlineData("Published: 2026-10-04\nPackages: ExGrid, ExChart\nTarget: net10.0\n", 4, "'ExChart' is not one")]
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

    [Fact] // ADR-0190: a version without a suffix is a release, and names its sign-off and its SHOULD criteria accepted failing
    public void A_release_names_its_sign_off_and_its_accepted_should_criteria()
    {
        var bare = Run(Valid(Notes("0.1.0.md", Version("0.1.0", "pending"))));
        var errors = Notices(bare);
        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, e => e.GetMessage().Contains("'## Signed off' is missing. A version without a suffix is a release"));
        Assert.Contains(errors, e => e.GetMessage().Contains("'## SHOULD criteria accepted failing' is missing."));

        var unrecorded = Version("0.1.0", "pending") + "\n## Signed off\n\nOn 2026-11-02.\n\n## SHOULD criteria accepted failing\n\nNone.\n";
        var error = One(Run(Valid(Notes("0.1.0.md", unrecorded))), "EXDOCS008");
        Assert.Contains("Signed off names the verification/<date>/results.md", error.GetMessage());

        var signed = Version("0.1.0", "pending")
            + "\n## Signed off\n\nAgainst `verification/2026-11-02-windows/results.md`.\n\n## SHOULD criteria accepted failing\n\n- VZ-12: measured at 18 ms, accepted in writing.\n";
        var run = Run(Valid(Notes("0.1.0.md", signed)));
        Assert.Empty(Notices(run));
        Assert.Contains("global::ExGrid.Docs.ReleaseSectionKind.SignedOff", run.Source("ReleaseNotes.g.cs"));
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
    public void A_construct_outside_the_subset_is_an_error_at_its_line(string paragraph, string message)
    {
        var text = Version("0.1.0-beta.2", replace: ("ExSheet", "No change. Its packages are republished at this version.\n\n" + paragraph));
        var run = Run(Valid().Where(f => !f.Item1.EndsWith("0.1.0-beta.2.md")).Append(Notes("0.1.0-beta.2.md", text)));

        var error = Notices(run).First(d => d.GetMessage().Contains(message));
        Assert.Equal("EXDOCS007", error.Id);
        Assert.Equal(text.Split('\n').ToList().IndexOf(paragraph) + 1, Line(error));
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
    [InlineData("Product: ExChart\nKind: Added\n\nText.", "Product is one of ExGrid, ExSheet, ExPivot, Data, and 'ExChart' is not")]
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
}
