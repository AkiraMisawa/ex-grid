using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The Keyboard reference as the compiler reads it (ADR-0110): every KeyRow KeyboardByHand writes, under the
/// property named for its product, linking a section the site has and citing a record that exists.
/// </summary>
public class KeyboardReferenceTests
{
    private const string Repo = "/repo/";

    private const string KeyboardPage = """
        @page "/reference/keyboard"
        <DocsPage Title="Keyboard" Lead="Every key.">
            <DocsSection Title="ExGrid"></DocsSection>
        </DocsPage>
        """;

    private const string SelectionPage = """
        @page "/exgrid/selection"
        <DocsPage Product="ExGrid" Title="Selection and keyboard" Lead="Selection works as it does in Excel.">
            <DocsSection Title="The keyboard"></DocsSection>
        </DocsPage>
        """;

    private const string Hand = """
        namespace ExGrid.Docs
        {
            internal sealed record KeyRow(string Group, string Keys, string What, string Explained, System.Collections.Generic.IReadOnlyList<string> Cites,
                System.Collections.Generic.IReadOnlyList<string>? Canonical = null);

            internal static class KeyboardByHand
            {
                private const string Selection = "exgrid/selection#the-keyboard";

                public static System.Collections.Generic.IReadOnlyList<KeyRow> ExGrid { get; } =
                [
                    new("Moving and selecting", "[Ctrl] + [A]", "Select everything.", Selection, ["ADR-0012"], Canonical: ["Control+a"]),
                    new("Moving and selecting", "[Home] / [End]", "The row's first or last cell. With [Shift], extend to it.", Selection, ["ADR-0012", "DoD §10"]),
                ];

                public static System.Collections.Generic.IReadOnlyList<KeyRow> ExSheet { get; } = [];

                public static System.Collections.Generic.IReadOnlyList<KeyRow> ExPivot { get; } = [];
            }
        }
        """;

    private const string DefinitionOfDone = """
        # Definition of Done

        ## 10. Keyboard (KB)
        """;

    private static (string, string)[] Fixture(bool keyboardPage = true) =>
    [
        .. keyboardPage ? new[] { ($"{GeneratorHarness.SiteDir}/Pages/KeyboardPage.razor", KeyboardPage) } : [],
        ($"{GeneratorHarness.SiteDir}/Pages/Grid/GridSelectionAndKeys.razor", SelectionPage),
        (Repo + "docs/adr/0012-anchor-focus-and-keyboard-navigation.md", "# 0012\n"),
        (Repo + "docs/definition-of-done.md", DefinitionOfDone),
    ];

    private static GeneratorRun Run(string hand = Hand, bool keyboardPage = true) =>
        GeneratorHarness.Run(hand.Length == 0 ? [] : [hand], Fixture(keyboardPage));

    private static Diagnostic One(GeneratorRun run)
    {
        var error = Assert.Single(run.Diagnostics.Where(d => d.Id == "EXDOCS020"));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    private static string Text(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan);

    [Fact] // ADR-0110: well-formed rows build, and the records they cite are emitted
    public void Well_formed_rows_build_and_their_citations_are_emitted()
    {
        var run = Run();

        Assert.Empty(run.Diagnostics);
        var source = run.Source("Keyboard.g.cs");
        Assert.Contains("""new("ADR-0012", "ADR-0012", "docs/adr/0012-anchor-focus-and-keyboard-navigation.md", null),""", source);
        Assert.Contains("""new("DoD §10", "Definition of Done §10", "docs/definition-of-done.md", "10-keyboard-kb"),""", source);
    }

    [Theory] // ADR-0110: a row links the section that explains it, which the site has
    [InlineData("exgrid/selection#the-keys", "has no section with the anchor 'the-keys'. Its sections are the-keyboard")]
    [InlineData("exgrid/keys#the-keyboard", "no page has the route /exgrid/keys")]
    [InlineData("exgrid/selection", "a page without a section")]
    public void A_link_that_leads_nowhere_stops_the_build(string href, string message)
    {
        var run = Run(Hand.Replace("\"exgrid/selection#the-keyboard\"", $"\"{href}\""));

        var errors = run.Diagnostics.Where(d => d.Id == "EXDOCS020").ToList();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, e => Assert.Contains(message, e.GetMessage()));
    }

    [Theory] // ADR-0110: keys are written in brackets, one pair each, and the rest is plain text
    [InlineData("\"[Ctrl] + [A]\"", "\"[Ctrl + [A]\"", "has a bracket left open or a key with no name")]
    [InlineData("\"[Ctrl] + [A]\"", "\"[] + [A]\"", "has a bracket left open or a key with no name")]
    [InlineData("\"Select everything.\"", "\"Select <b>everything</b>.\"", "holds Markdown or markup")]
    [InlineData("\"Moving and selecting\", \"[Ctrl] + [A]\"", "\"Moving [and] selecting\", \"[Ctrl] + [A]\"", "This KeyRow's group is plain text")]
    public void Keys_and_text_that_cannot_be_read_stop_the_build(string from, string to, string message)
    {
        var run = Run(Hand.Replace(from, to));

        Assert.Contains(message, One(run).GetMessage());
    }

    [Theory] // ADR-0110: every row cites a record that exists
    [InlineData("""["ADR-0012"]""", "[]", "This KeyRow cites nothing")]
    [InlineData("""["ADR-0012"]""", """["ADR-0013"]""", "ADR-0013 names no decision record")]
    public void A_row_without_a_citation_stops_the_build(string from, string to, string message)
    {
        var run = Run(Hand.Replace(from, to));

        Assert.Contains(message, One(run).GetMessage());
    }

    [Theory] // ADR-0110: a key a row names for the completeness check is written as the grid's key table names it
    [InlineData("Ctrl+A")]
    [InlineData("Shift+Control+a")]
    [InlineData("Control+Control+a")]
    [InlineData("Control+")]
    [InlineData("Control+Shift")]
    [InlineData("")]
    public void A_key_not_in_the_canonical_form_stops_the_build(string key)
    {
        var run = Run(Hand.Replace("Canonical: [\"Control+a\"]", $"Canonical: [\"{key}\"]"));

        Assert.Contains($"names the key '{key}', which is not in the grid's canonical form", One(run).GetMessage());
    }

    [Theory] // ADR-0110: the canonical form is GridKeys': the modifiers in order, then one key, a space, + and the Alt key's own press among them
    [InlineData("Control+Shift+ArrowDown")]
    [InlineData("Control+ ")]
    [InlineData(" ")]
    [InlineData("Control++")]
    [InlineData("Control+Shift++")]
    [InlineData("Alt+Alt")]
    [InlineData("Shift+F10")]
    public void A_key_in_the_canonical_form_builds(string key)
    {
        var run = Run(Hand.Replace("Canonical: [\"Control+a\"]", $"Canonical: [\"{key}\"]"));

        Assert.Empty(run.Diagnostics);
    }

    [Fact] // ADR-0110: a canonical key the build cannot read as text stops it
    public void A_key_not_written_as_text_stops_the_build()
    {
        var run = Run(Hand.Replace("Canonical: [\"Control+a\"]", "Canonical: [string.Empty + \"a\"]"));

        Assert.Contains("canonical key is not written as text", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a row under a property that is not a product cannot be placed on the page
    public void A_row_outside_the_three_products_stops_the_build()
    {
        var run = Run(Hand.Replace("public static System.Collections.Generic.IReadOnlyList<KeyRow> ExPivot { get; } = [];",
            """public static System.Collections.Generic.IReadOnlyList<KeyRow> Data { get; } = [new("Reading", "[Ctrl] + [O]", "Open.", "exgrid/selection#the-keyboard", ["ADR-0012"])];"""));

        Assert.Contains("This KeyRow is declared in 'Data'", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a link written as a constant is read as the constant's text
    public void A_link_written_as_a_constant_is_read()
    {
        var run = Run(Hand.Replace("private const string Selection = \"exgrid/selection#the-keyboard\";", "private const string Selection = \"exgrid/selection#nowhere\";"));

        var errors = run.Diagnostics.Where(d => d.Id == "EXDOCS020").ToList();
        Assert.Equal(2, errors.Count);
        Assert.Equal("Selection", Text(errors[0]));
    }

    [Fact] // ADR-0110: a site with a Keyboard page and no rows stops the build
    public void A_page_without_its_rows_stops_the_build()
    {
        var run = Run(hand: "");

        Assert.Contains("no KeyRow reached the build", One(run).GetMessage());
    }

    [Fact] // ADR-0110: without the page, an empty reference is not an error
    public void A_site_without_the_page_and_without_rows_builds()
    {
        var run = Run(hand: "", keyboardPage: false);

        Assert.Empty(run.Diagnostics);
    }
}
