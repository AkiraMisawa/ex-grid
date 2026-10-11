using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Roadmap as the compiler reads it (ADR-0191): the open tickets by their Status: and Roadmap: lines,
/// the scope tables of ADR-0046 and ADR-0059, and the rows RoadmapByHand writes with the records they
/// cite. Every row is placed by the state of its decision, and none carries a date, a quarter or a version.
/// </summary>
public class RoadmapTests
{
    private const string Repo = "/repo/";

    private static (string, string) Site(string path, string text) => ($"{GeneratorHarness.SiteDir}/{path}", text);

    private static (string, string) File(string path, string text) => (Repo + path, text);

    private static (string, string) Ticket(string feature, string name, string text) => File($"docs/specs/{feature}/issues/{name}", text);

    private const string Page = """
        @page "/roadmap"
        <DocsPage Title="Roadmap" Lead="What is decided next.">
            <DocsSection Title="On main, not in a version yet"></DocsSection>
            <DocsSection Title="Decided, ready to build"></DocsSection>
            <DocsSection Title="Waiting on a decision"></DocsSection>
            <DocsSection Title="Later"></DocsSection>
            <DocsSection Title="Toward the next stable version"></DocsSection>
            <DocsSection Title="Not planned"></DocsSection>
        </DocsPage>
        """;

    private const string SheetScope = """
        # ExSheet is a general-purpose sheet

        - **What the first version holds**, beyond this ADR's own decisions:

          | | First version | Later | Why later |
          |---|---|---|---|
          | Inserting and deleting rows and columns | ✓ | | |
          | Frozen rows | | ✓ | ExGrid has no pinned rows; a core change |
          | Several Sheets (a workbook) | | ✓ | above |
          | Protecting cells | | ✓ | `Editable` is per column ([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)); per cell is a core change |

        - **ExSheet is built alongside ExGrid.**
        """;

    private const string PivotScope = """
        # ExPivot is a pivot table

        ## What the first version holds

        | | First version | Later | Why later |
        |---|---|---|---|
        | The **Layout menu** that sets them, and the **Pivot Toolbar** above the report | ✓ | | |
        | A dropdown on the report's headers ("Row Labels ▾") | | ✓ | ExGrid's header menus first (above) |
        | Editing values in the report (writeback) | | ✗ | a report is computed; Excel refuses it too |

        ## Consequences
        """;

    private const string DefinitionOfDone = """
        # Definition of Done — when ExGrid is finished

        ## 21. What was undecided

        ```
        ### Step 7 — not a heading inside a fence
        ```

        ### 21.11 Reserved with a named trigger — not open, and not to be mistaken for oversights

        ## 22. The final verification checklist

        ### Step 7 — Sign-off
        """;

    private const string Hand = """
        namespace ExGrid.Docs;

        internal sealed record RoadmapNote(string Text, System.Collections.Generic.IReadOnlyList<string> Products, System.Collections.Generic.IReadOnlyList<string> Cites);

        internal sealed record RoadmapRecord(string Title, string Cite, string About);

        internal static class RoadmapByHand
        {
            public static System.Collections.Generic.IReadOnlyList<RoadmapNote> TowardStable { get; } =
            [
                new("The Definition of Done is signed off.", ["ExGrid", "ExSheet", "ExPivot", "Data"], ["DoD Step 7"]),
            ];

            public static System.Collections.Generic.IReadOnlyList<RoadmapRecord> Linked { get; } =
            [
                new("What the Definition of Done still owes", "docs/implementation-status.md", "A snapshot of what is left."),
                new("Reserved with a named trigger", "DoD §21.11", "Questions a decision settles when their trigger is built."),
            ];

            public static System.Collections.Generic.IReadOnlyList<RoadmapNote> NotPlanned { get; } =
            [
                new("Right-to-left layout.", ["ExGrid", "ExSheet", "ExPivot"], ["ADR-0031"]),
            ];
        }
        """;

    private const string Let = """
        # 14: LET, as Excel's keyboard accepts it

        Status: ready-for-agent
        Roadmap: ExSheet — LET, to name a value once and use it across one Formula (ADR-0181).

        **What to build:** `LET`.
        """;

    private const string Drag = """
        # 31: Drag a Reference Outline to rewrite its Reference

        Status: needs-triage
        Roadmap: ExSheet — Dragging a Reference Outline to move the Reference it outlines,
        by the corner squares Excel draws (ADR-0057).

        **Needs a decision.**
        """;

    private static (string, string)[] Fixture(params (string, string)[] replace)
    {
        var files = new Dictionary<string, string>
        {
            [Site("Pages/RoadmapPage.razor", "").Item1] = Page,
            [Repo + "docs/adr/0031-the-grid-lays-out-left-to-right-only.md"] = "# 0031\n",
            [Repo + "docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md"] = "# 0035\n",
            [Repo + "docs/adr/0046-exsheet-is-a-general-purpose-sheet.md"] = SheetScope,
            [Repo + "docs/adr/0057-references-are-outlined.md"] = "# 0057\n",
            [Repo + "docs/adr/0059-expivot-is-a-pivot-table.md"] = PivotScope,
            [Repo + "docs/adr/0181-let-binds-names.md"] = "# 0181\n",
            [Repo + "docs/definition-of-done.md"] = DefinitionOfDone,
            [Repo + "docs/implementation-status.md"] = "# Implementation status\n",
            [Repo + "docs/specs/exsheet-functions/issues/14-let-bindings.md"] = Let,
            [Repo + "docs/specs/exsheet/issues/31-drag-a-reference-outline.md"] = Drag,
            [Repo + "docs/specs/expivot/issues/07-observe-beside-excel.md"] = "# 07: Observe each reading beside Excel\n\nStatus: ready-for-human\nRoadmap: internal\n",
            [Repo + "docs/specs/exgrid/issues/01-done.md"] = "# 01: Done\n\nStatus: done — but for a real IME, which a Windows run checks\n",
            [Repo + "docs/specs/exgrid/issues/02-refused.md"] = "# 02: Refused\n\nStatus: wontfix — built, and replaced\n",
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

    private static GeneratorRun Run(IEnumerable<(string, string)> files, string hand = Hand, string version = "dev", string docsRef = "") =>
        GeneratorHarness.Run(hand.Length == 0 ? [] : [hand], files, version, docsRef);

    private static Diagnostic[] Notices(GeneratorRun run) =>
        run.Diagnostics.Where(d => d.Id is "EXDOCS011" or "EXDOCS012" or "EXDOCS013" or "EXDOCS014" or "EXDOCS015" or "EXDOCS016").ToArray();

    private static Diagnostic One(GeneratorRun run, string id)
    {
        var error = Assert.Single(Notices(run));
        Assert.Equal(id, error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    private static int Line(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;

    private static int Column(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().StartLinePosition.Character + 1;

    private static string FileOf(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().Path;

    private static (string, string) LetSaying(string roadmap) =>
        (Repo + "docs/specs/exsheet-functions/issues/14-let-bindings.md", Let.Replace(
            "Roadmap: ExSheet — LET, to name a value once and use it across one Formula (ADR-0181).", roadmap));

    [Fact] // ADR-0191: the open tickets, the scope tables and the rows written by hand build cleanly and are emitted
    public void A_well_formed_roadmap_builds_and_is_emitted()
    {
        var run = Run(Fixture());

        Assert.Empty(run.Diagnostics);
        var source = run.Source("Roadmap.g.cs");
        Assert.Contains("""public const string Decided = "Decided, ready to build";""", source);
        Assert.Contains("""public const string NotPlanned = "Not planned";""", source);
        Assert.Contains("""new("exsheet-functions", "14", "LET, as Excel's keyboard accepts it", "LET, as Excel's keyboard accepts it", ["ExSheet"], "LET, to name a value once and use it across one Formula (<a href=\"https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0181-let-binds-names.md\" target=\"_blank\" rel=\"noopener\">ADR-0181</a>).", """, source);
        Assert.Contains("""global::ExGrid.Docs.RoadmapState.Decided, "ready-for-agent", "docs/specs/exsheet-functions/issues/14-let-bindings.md"),""", source);
        // A line that wraps is read whole.
        Assert.Contains("\"Dragging a Reference Outline to move the Reference it outlines, by the corner squares Excel draws (ADR-0057).\", global::ExGrid.Docs.RoadmapState.Waiting, \"needs-triage\"", source);
        // Internal, done and wontfix tickets are left off.
        Assert.DoesNotContain("\"expivot\", \"07\"", source);
        Assert.DoesNotContain("\"01\"", source);
        Assert.DoesNotContain("\"02\"", source);
    }

    [Fact] // ADR-0191: every row of the scope tables is read with its mark, and "above" in a Why later names the record
    public void Scope_rows_are_read_with_their_marks()
    {
        var source = Run(Fixture()).Source("Roadmap.g.cs");

        Assert.Contains("""new("ExSheet", "0046", "0046-exsheet-is-a-general-purpose-sheet.md", global::ExGrid.Docs.RoadmapScope.FirstVersion, "Inserting and deleting rows and columns",""", source);
        Assert.Contains("""global::ExGrid.Docs.RoadmapScope.Later, "Frozen rows", "Frozen rows", "ExGrid has no pinned rows; a core change", "ExGrid has no pinned rows; a core change"),""", source);
        Assert.Contains("""global::ExGrid.Docs.RoadmapScope.Later, "Several Sheets (a workbook)", "Several Sheets (a workbook)", "in <a href=\"https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0046-exsheet-is-a-general-purpose-sheet.md\" target=\"_blank\" rel=\"noopener\">ADR-0046</a>", "in ADR-0046"),""", source);
        // A link to a decision record becomes its number, linked at the ref.
        Assert.Contains("""<code>Editable</code> is per column (<a href=\"https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md\" target=\"_blank\" rel=\"noopener\">ADR-0035</a>); per cell is a core change""", source);
        Assert.Contains("\"ExGrid's header menus first (in ADR-0059)\"", source);
        // "above" that is the feature's own words stays.
        Assert.Contains("\"The Layout menu that sets them, and the Pivot Toolbar above the report\"", source);
        Assert.Contains("""global::ExGrid.Docs.RoadmapScope.NotPlanned, "Editing values in the report (writeback)",""", source);
    }

    [Fact] // ADR-0191: a hand-written row's citations resolve to their records, a section of the Definition of Done to GitHub's anchor
    public void Citations_resolve_to_their_records()
    {
        var source = Run(Fixture()).Source("Roadmap.g.cs");

        Assert.Contains("""new("ADR-0031", "ADR-0031", "docs/adr/0031-the-grid-lays-out-left-to-right-only.md", null),""", source);
        Assert.Contains("""new("DoD Step 7", "Definition of Done, Step 7", "docs/definition-of-done.md", "step-7--sign-off"),""", source);
        Assert.Contains("""new("DoD §21.11", "Definition of Done §21.11", "docs/definition-of-done.md", "2111-reserved-with-a-named-trigger--not-open-and-not-to-be-mistaken-for-oversights"),""", source);
        Assert.Contains("""new("docs/implementation-status.md", "docs/implementation-status.md", "docs/implementation-status.md", null),""", source);
    }

    [Fact] // ADR-0191: a site published from a tag links the ADRs a sentence names at the tag
    public void A_tag_build_links_the_adrs_at_the_tag()
    {
        var run = GeneratorHarness.Run([Hand], Fixture(), "0.1.0-beta.3", "v0.1.0-beta.3", "2026-10-12");

        Assert.Empty(Notices(run));
        Assert.Contains("/blob/v0.1.0-beta.3/docs/adr/0181-let-binds-names.md", run.Source("Roadmap.g.cs"));
    }

    [Theory] // ADR-0191: GitHub's anchor for a heading, which a citation of the Definition of Done links
    [InlineData("Step 7 — Sign-off", "step-7--sign-off")]
    [InlineData("21.11 Reserved with a named trigger — not open, and not to be mistaken for oversights", "2111-reserved-with-a-named-trigger--not-open-and-not-to-be-mistaken-for-oversights")]
    [InlineData("23. The Wrapper — `ExGrid.MudBlazor` (WR)", "23-the-wrapper--exgridmudblazor-wr")]
    [InlineData("4.1 Accessibility (A11Y) — ADR-0033", "41-accessibility-a11y--adr-0033")]
    public void Headings_take_githubs_anchor(string heading, string anchor) =>
        Assert.Equal(anchor, Roadmap.GitHubAnchor(heading));

    [Fact] // ADR-0191: an open ticket without a Roadmap line stops the build, at its Status line
    public void An_open_ticket_without_a_roadmap_line_is_refused()
    {
        var run = Run(Fixture(LetSaying("")));

        var error = One(run, "EXDOCS011");
        Assert.EndsWith("14-let-bindings.md", FileOf(error));
        Assert.Equal(3, Line(error));
        Assert.Contains("is open (Status: ready-for-agent) and has no Roadmap: line", error.GetMessage());
        Assert.Contains("Roadmap: internal", error.GetMessage());
    }

    [Fact] // ADR-0191: a done or wontfix ticket needs no Roadmap line, whatever follows its role
    public void A_closed_ticket_needs_no_roadmap_line()
    {
        var run = Run(Fixture(
            File("docs/specs/exgrid/issues/03-done.md", "# 03: Done\n\nStatus: done\n\nBody.\n"),
            File("docs/specs/exgrid/issues/04-done-wrapped.md", "# 04: Done\n\nStatus: done — decided 2026-10-07; built by ticket 07\nand more of the status.\n")));

        Assert.Empty(Notices(run));
    }

    [Fact] // ADR-0191: a Roadmap line away from Status is refused at its line
    public void A_roadmap_line_away_from_status_is_refused()
    {
        var text = "# 14: LET\n\nStatus: ready-for-agent\n\nRoadmap: ExSheet — LET in one Formula.\n";
        var error = One(Run(Fixture((Repo + "docs/specs/exsheet-functions/issues/14-let-bindings.md", text))), "EXDOCS011");

        Assert.Equal(5, Line(error));
        Assert.Contains("stands away from Status:", error.GetMessage());
    }

    [Theory] // ADR-0191: a ticket the Roadmap cannot place stops the build
    [InlineData("# 14: LET\n\nBody with no status.\n", 1, "has no Status: line")]
    [InlineData("# 14: LET\n\nStatus: in-progress\nRoadmap: ExSheet — LET.\n", 3, "'in-progress' is not a ticket role")]
    [InlineData("LET\n\nStatus: ready-for-agent\nRoadmap: ExSheet — LET.\n", 1, "begins with # <number>: <title>")]
    [InlineData("# 14: LET\n\nStatus: ready-for-agent\nRoadmap: ExSheet - LET.\n", 4, "parted by a spaced em dash")]
    [InlineData("# 14: LET\n\nStatus: ready-for-agent\nRoadmap: ExSheet — \n", 4, "has no sentence after its products")]
    [InlineData("# 14: LET\n\nStatus: ready-for-agent\nRoadmap: ExSheet — LET, as [the ADR](https://example.com/x) says.\n", 4, "A Roadmap sentence links nothing")]
    [InlineData("# 14: LET\n\nStatus: ready-for-agent\nRoadmap: ExSheet — LET, _briefly_.\n", 4, "italics")]
    public void A_ticket_the_roadmap_cannot_place_is_refused(string text, int line, string message)
    {
        var error = One(Run(Fixture((Repo + "docs/specs/exsheet-functions/issues/14-let-bindings.md", text))), "EXDOCS011");

        Assert.Equal(line, Line(error));
        Assert.Contains(message, error.GetMessage());
    }

    [Theory] // ADR-0191: a product outside the four is refused where it is written
    [InlineData("Roadmap: ExSheet, Fluxor — LET.", 19, "'Fluxor' is not one of the Roadmap's products")]
    [InlineData("Roadmap: internal — a verification run.", 10, "says Roadmap: internal alone")]
    [InlineData("Roadmap: ExSheet, ExSheet — LET.", 19, "ExSheet is listed twice")]
    public void A_product_outside_the_four_is_refused(string roadmap, int column, string message)
    {
        var error = One(Run(Fixture(LetSaying(roadmap))), "EXDOCS012");

        Assert.Equal(4, Line(error));
        Assert.Equal(column, Column(error));
        Assert.Contains(message, error.GetMessage());
    }

    [Fact] // ADR-0191: an ADR a Roadmap sentence names must have a file
    public void A_sentence_naming_an_adr_with_no_file_is_refused()
    {
        var error = One(Run(Fixture(LetSaying("Roadmap: ExSheet — LET, under ADR-0999."))), "EXDOCS013");

        Assert.Equal(4, Line(error));
        Assert.Equal(31, Column(error));
        Assert.Contains("ADR-0999 names no decision record", error.GetMessage());
    }

    [Theory] // ADR-0191: a Roadmap sentence holds no date, quarter or version
    [InlineData("LET, in Q3.", "a quarter, 'Q3'")]
    [InlineData("LET, in the second half.", "a quarter, 'second half'")]
    [InlineData("LET, by 2027-03-01.", "a date, '2027-03-01'")]
    [InlineData("LET, by March 2027.", "a date, 'March 2027'")]
    [InlineData("LET, by 12 March.", "a date, '12 March'")]
    [InlineData("LET, by 3/12.", "a date, '3/12'")]
    [InlineData("LET, in 2027.", "a year, '2027'")]
    [InlineData("LET, in 0.2.0.", "a version, '0.2.0'")]
    [InlineData("LET, in 0.1.0-beta.4.", "a version, '0.1.0-beta.4'")]
    [InlineData("LET, in v1.", "a version, 'v1'")]
    [InlineData("LET, in beta 4.", "a version, 'beta 4'")]
    [InlineData("LET, in version 2.", "a version, 'version 2'")]
    [InlineData("LET, in the next release.", "a target version, 'next release'")]
    [InlineData("LET, in the next stable version.", "a target version, 'next stable version'")]
    public void A_sentence_promising_a_time_is_refused(string sentence, string found)
    {
        var error = One(Run(Fixture(LetSaying("Roadmap: ExSheet — " + sentence))), "EXDOCS015");

        Assert.Equal(4, Line(error));
        Assert.Contains($"holds {found}.", error.GetMessage());
    }

    [Theory] // ADR-0191: words that only look like a time are not refused
    [InlineData("ERROR.TYPE, the number of an Error Value (ADR-0047).")]
    [InlineData("INDIRECT, for A1-style text, to 15 significant digits.")]
    [InlineData("A margin of rows before the Viewport runs out, as soon as it can.")]
    [InlineData("A range of 2^24 Values, and a refusal that may change with a later decision.")]
    public void Words_that_only_look_like_a_time_are_kept(string sentence)
    {
        var run = Run(Fixture(LetSaying("Roadmap: ExSheet — " + sentence),
            (Repo + "docs/adr/0047-the-formula-engine.md", "# 0047\n")));

        Assert.Empty(Notices(run));
    }

    [Fact] // ADR-0191: a ticket's lines may end in CRLF
    public void Crlf_tickets_are_read()
    {
        var run = Run(Fixture(
            (Repo + "docs/specs/exsheet-functions/issues/14-let-bindings.md", Let.Replace("\n", "\r\n")),
            (Repo + "docs/specs/exsheet/issues/31-drag-a-reference-outline.md", Drag.Replace("\n", "\r\n"))));

        Assert.Empty(run.Diagnostics);
        Assert.Contains("move the Reference it outlines, by the corner squares Excel draws (ADR-0057).\"", run.Source("Roadmap.g.cs"));
    }

    [Theory] // ADR-0191: a scope-table row carries exactly one mark
    [InlineData("| Frozen rows | | | ExGrid has no pinned rows |", "carries no mark")]
    [InlineData("| Frozen rows | ✓ | ✓ | ExGrid has no pinned rows |", "carries 2 marks")]
    [InlineData("| Frozen rows | | ✓✗ | ExGrid has no pinned rows |", "carries 2 marks")]
    [InlineData("| Frozen rows | | ✓ |", "has 3 cells")]
    public void A_scope_row_without_exactly_one_mark_is_refused(string row, string message)
    {
        var text = SheetScope.Replace("| Frozen rows | | ✓ | ExGrid has no pinned rows; a core change |", row);
        var error = One(Run(Fixture((Repo + "docs/adr/0046-exsheet-is-a-general-purpose-sheet.md", text))), "EXDOCS014");

        Assert.EndsWith("0046-exsheet-is-a-general-purpose-sheet.md", FileOf(error));
        Assert.Equal(8, Line(error));
        Assert.Contains(message, error.GetMessage());
    }

    [Fact] // ADR-0191: a scope table that lost its header stops the build rather than empty the Roadmap
    public void A_scope_table_without_its_header_is_refused()
    {
        var text = PivotScope.Replace("| | First version | Later | Why later |", "| | First | Later | Why |");
        var error = One(Run(Fixture((Repo + "docs/adr/0059-expivot-is-a-pivot-table.md", text))), "EXDOCS014");

        Assert.Contains("ADR-0059 has no scope table with the header", error.GetMessage());
    }

    [Fact] // ADR-0191: a site with a Roadmap page needs ADR-0046's and ADR-0059's scope tables, and the tickets
    public void A_roadmap_page_needs_its_sources()
    {
        var withoutPivot = Run(Fixture((Repo + "docs/adr/0059-expivot-is-a-pivot-table.md", "")));
        Assert.Contains("ADR-0059, whose scope table holds ExPivot's Later and refused rows, did not reach the build", One(withoutPivot, "EXDOCS014").GetMessage());

        var withoutTickets = Run(Fixture().Where(f => !f.Item1.Contains("/docs/specs/")));
        Assert.Contains("no ticket under docs/specs/*/issues/ reached the build", One(withoutTickets, "EXDOCS011").GetMessage());
    }

    [Fact] // ADR-0191: a site without a Roadmap page, and without tickets, has nothing to check
    public void No_page_and_no_tickets_build_quietly()
    {
        var run = Run(Fixture().Where(f => !f.Item1.Contains("/docs/specs/") && !f.Item1.EndsWith("RoadmapPage.razor")), hand: "");

        Assert.Empty(Notices(run));
    }

    [Fact] // ADR-0191: the page has the six sections, in ADR-0191's order
    public void The_page_has_the_six_sections_in_order()
    {
        var page = Page.Replace("""<DocsSection Title="Later"></DocsSection>""", "")
            .Replace("""<DocsSection Title="Not planned"></DocsSection>""", """<DocsSection Title="Not planned"></DocsSection><DocsSection Title="Later"></DocsSection>""");
        var error = One(Run(Fixture(Site("Pages/RoadmapPage.razor", page))), "EXDOCS016");

        Assert.EndsWith("RoadmapPage.razor", FileOf(error));
        Assert.Equal(1, Line(error));
        Assert.Contains("Toward the next stable version; Not planned; Later. The Roadmap has six, in this order", error.GetMessage());
    }

    [Theory] // ADR-0191: a hand-written row cites a record that exists, names the four products, and is plain text
    [InlineData("""new("Right-to-left layout.", ["ExGrid"], ["ADR-0999"])""", "EXDOCS013", "ADR-0999 names no decision record")]
    [InlineData("""new("Right-to-left layout.", ["ExGrid"], ["DoD §99.1"])""", "EXDOCS013", "none begins with '99.1'")]
    [InlineData("""new("Right-to-left layout.", ["ExGrid"], ["DoD Step 9"])""", "EXDOCS013", "none begins with 'Step 9'")]
    [InlineData("""new("Right-to-left layout.", ["ExGrid"], ["the RTL decision"])""", "EXDOCS013", "'the RTL decision' is not a citation the build reads")]
    [InlineData("""new("Right-to-left layout.", ["ExGrid"], ["docs/roadmap.md"])""", "EXDOCS013", "docs/roadmap.md is not a file the build reads")]
    [InlineData("""new("Right-to-left layout.", ["ExGrid"], [])""", "EXDOCS013", "This Roadmap row cites nothing")]
    [InlineData("""new("Right-to-left layout.", ["Fluxor"], ["ADR-0031"])""", "EXDOCS012", "'Fluxor' is not one of the Roadmap's products")]
    [InlineData("""new("Right-to-left layout.", [], ["ADR-0031"])""", "EXDOCS012", "This Roadmap row names no product")]
    [InlineData("""new("Right-to-left layout, in Q4.", ["ExGrid"], ["ADR-0031"])""", "EXDOCS015", "holds a quarter, 'Q4'")]
    [InlineData("""new("Right-to-left `dir` layout.", ["ExGrid"], ["ADR-0031"])""", "EXDOCS011", "is plain text")]
    [InlineData("""new(string.Concat("Right-to-left", " layout."), ["ExGrid"], ["ADR-0031"])""", "EXDOCS011", "is not written as text")]
    public void A_hand_written_row_is_checked(string row, string id, string message)
    {
        var hand = Hand.Replace("""new("Right-to-left layout.", ["ExGrid", "ExSheet", "ExPivot"], ["ADR-0031"])""", row);
        var error = One(Run(Fixture(), hand), id);

        Assert.Contains(message, error.GetMessage());
        Assert.Equal(GeneratorHarness.SiteDir + "/Source0.cs", FileOf(error));
    }

    [Fact] // ADR-0191: a record the page links is checked as a row is, its sentence included
    public void A_linked_record_is_checked()
    {
        var hand = Hand.Replace("\"A snapshot of what is left.\"", "\"A snapshot of what is left in 2026.\"");
        var error = One(Run(Fixture(), hand), "EXDOCS015");

        Assert.Contains("This RoadmapRecord's sentence holds a year, '2026'", error.GetMessage());
    }

    [Fact] // ADR-0191: a citation of the Definition of Done needs the file
    public void A_definition_of_done_citation_needs_the_file()
    {
        var errors = Notices(Run(Fixture((Repo + "docs/definition-of-done.md", ""))));

        Assert.Equal(2, errors.Length);
        Assert.All(errors, e => Assert.Equal("EXDOCS013", e.Id));
        Assert.Contains("DoD Step 7 cites the Definition of Done, and docs/definition-of-done.md did not reach the build", errors[0].GetMessage());
        Assert.Contains("DoD §21.11 cites the Definition of Done", errors[1].GetMessage());
    }
}
