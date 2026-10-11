using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The site map's reader of a page's markup (ADR-0110): what it takes from a page, what it steps
/// over, and what it refuses, with the line a reader is sent to.
/// </summary>
public class PageReaderTests
{
    private const string Overview = """
        @page "/exgrid"
        @using ExGrid.Docs.Examples.Grid

        <DocsPage Product="ExGrid" Title="Overview"
                  Lead="A grid for reading large tables of numbers. Your application holds the rows.">

            <DocsSection Title="A first grid">
                <p>Hand the grid a source.</p>
                <DocsExample Builtin="typeof(FirstGrid)" Mud="typeof(FirstGridMud)" Files="@(["Examples/Data/Trade.cs"])" CodeOpen="true" />
            </DocsSection>

            <DocsSection Title="Rectangles, a Focus and an Extent">
                <DocsExample Title="Reading the selection" Builtin="typeof(SelectionStatus)"
                             Files="@(["Examples/Data/Trade.cs", "Examples/Grid/Position.cs"])" Mud="typeof(SelectionStatusMud)">
                    What <code>SelectionChanged</code> hands over.
                </DocsExample>
                <DocsExample Title="Built-in only" Builtin="typeof(EngineOnly)" />
            </DocsSection>

            <DocsSection Title="Which row am I on?" />

            <DocsSection Title="API">
                <ApiTable Type="typeof(ExGrid<>)" />
                <ApiTable Type="typeof(global::ExSheet.Engine.Sheet)" />
            </DocsSection>
        </DocsPage>
        """;

    [Fact] // ADR-0110: the site map holds a page's route, header, sections, Examples and tabled types
    public void A_page_is_read_whole()
    {
        var scan = PageReader.Read("Pages/Grid/GridOverview.razor", Overview);

        Assert.Empty(scan.Problems);
        var page = scan.Page;
        Assert.Equal("Pages/Grid/GridOverview.razor", page.Path);
        Assert.Equal(["exgrid"], page.Routes);
        Assert.Equal(1, page.Line);
        Assert.Equal("ExGrid", page.Product);
        Assert.Equal("Overview", page.Title);
        Assert.Equal("A grid for reading large tables of numbers. Your application holds the rows.", page.Lead);
        Assert.Equal(
            ["A first grid", "Rectangles, a Focus and an Extent", "Which row am I on?", "API"],
            page.Sections.Select(s => s.Title));
        Assert.Equal(
            ["a-first-grid", "rectangles--a-focus-and-an-extent", "which-row-am-i-on-", "api"],
            page.Sections.Select(s => s.Anchor));
        Assert.Equal([7, 12, 20, 22], page.Sections.Select(s => s.Line));
    }

    [Fact] // ADR-0110: an Example belongs to its section, with its title and whether it has a MudBlazor twin
    public void Examples_are_read_into_their_sections()
    {
        var page = PageReader.Read("Pages/Grid/GridOverview.razor", Overview).Page;

        var first = Assert.Single(page.Sections[0].Examples);
        Assert.Null(first.Title);
        Assert.True(first.HasMud);
        Assert.Equal(9, first.Line);
        // A C# expression holding quotes inside an attribute does not end the tag: Mud, after it, is read.
        Assert.Equal(
            [("Reading the selection", true), ("Built-in only", false)],
            page.Sections[1].Examples.Select(e => (e.Title, e.HasMud)));
        Assert.Empty(page.Sections[2].Examples);
    }

    [Fact] // ADR-0110: a tabled type is named as the page names it, without global::
    public void Tabled_types_are_read_into_their_section()
    {
        var page = PageReader.Read("Pages/Grid/GridOverview.razor", Overview).Page;

        Assert.Equal(["ExGrid<>", "ExSheet.Engine.Sheet"], page.Sections[3].Types);
        Assert.All(page.Sections.Take(3), s => Assert.Empty(s.Types));
    }

    [Fact] // ADR-0110: the site map's anchors are the ones DocsSection renders, from the one shared function
    public void Anchors_are_the_shared_function_of_the_title()
    {
        Assert.Equal("status-bar-figures", DocsAnchor.Of("Status-bar figures"));
        Assert.Equal("number-formats-and-----", DocsAnchor.Of("Number formats and ####"));
        Assert.Equal("0-1-0-beta-2", DocsAnchor.Of("0.1.0-beta.2"));

        var page = PageReader.Read("Pages/X.razor", "@page \"/x\"\n<DocsSection Title=\"Number formats and ####\"></DocsSection>\n").Page;
        Assert.Equal(DocsAnchor.Of("Number formats and ####"), Assert.Single(page.Sections).Anchor);
    }

    [Fact] // ADR-0110: a commented-out section, and C# in a code block, are not the page's sections
    public void Comments_and_code_blocks_are_stepped_over()
    {
        var scan = PageReader.Read("Pages/X.razor", """"
            @page "/x"
            @* <DocsSection Title="Commented out"> *@
            <!-- <DocsSection Title="Also commented out"> -->
            <DocsSection Title="Kept">
                @if (_open)
                {
                    <DocsExample Title="Inside an if" Builtin="typeof(A)" />
                }
            </DocsSection>
            @code {
                private bool _open = true;
                private const string Fake = "<DocsSection Title=\"In a string\">";
                private static readonly string Braces = "}}}";
                private char _c = '}';
                // <DocsSection Title="In a comment"> }
                /* } */
                private string Interpolated => $"{(_open ? "}" : "{")}";
                private string Verbatim => @"a ""}"" b";
                private string Raw => """
                    <DocsSection Title="In a raw string"> }
                    """;
            }
            <DocsSection Title="After the code block" />
            """");

        Assert.Empty(scan.Problems);
        Assert.Equal(["Kept", "After the code block"], scan.Page.Sections.Select(s => s.Title));
        Assert.Equal("Inside an if", Assert.Single(scan.Page.Sections[0].Examples).Title);
    }

    [Fact] // ADR-0110: a Showcase's page takes its title from its frame
    public void A_showcase_is_titled_by_its_frame()
    {
        var page = PageReader.Read("Showcases/Blotter.razor", """
            @page "/showcase/blotter"
            @layout BareLayout

            <ShowcaseFrame Title="Trade blotter">
                <div class="blotter"></div>
            </ShowcaseFrame>
            """).Page;

        Assert.Equal(["showcase/blotter"], page.Routes);
        Assert.Equal("Trade blotter", page.Title);
        Assert.Null(page.Product);
        Assert.Empty(page.Sections);
    }

    [Fact] // ADR-0110: every @page is a route; one written mid-line, in prose, is not a directive
    public void Every_route_is_read_and_only_directives_count()
    {
        var page = PageReader.Read("Pages/X.razor", """
            @page "/"
            @page "/start"
            <p>Write @page "/elsewhere" at the top of a file.</p>
            """).Page;

        Assert.Equal(["", "start"], page.Routes);
    }

    [Fact] // ADR-0110: a component with no @page is not a page of the site map
    public void A_file_without_a_route_has_none()
    {
        var scan = PageReader.Read("Shared/Card.razor", "<DocsSection Title=\"In a component\"></DocsSection>");

        Assert.Empty(scan.Page.Routes);
    }

    [Fact] // ADR-0110: @@ in a title is an escaped @, and still text
    public void An_escaped_at_sign_is_text()
    {
        var scan = PageReader.Read("Pages/X.razor", "@page \"/x\"\n<DocsSection Title=\"Mail us @@ home\"></DocsSection>\n");

        Assert.Empty(scan.Problems);
        Assert.Equal("Mail us @ home", Assert.Single(scan.Page.Sections).Title);
    }

    [Theory] // ADR-0110: a section title that is not text is a build error, at the title
    [InlineData("Title=\"@_title\"", "C# expression", 21)]
    [InlineData("Title=@_title", "C# expression", 20)]
    [InlineData("Title=\"Totals for @_year\"", "C# expression", 21)]
    [InlineData("Title=\"@(\"Built\")\"", "C# expression", 21)]
    [InlineData("Title=\"Sorting &amp; filtering\"", "character reference &amp;", 29)]
    [InlineData("Title=\"\"", "empty", 21)]
    [InlineData("Class=\"x\"", "has no Title", 1)]
    public void A_title_that_is_not_text_is_refused(string attribute, string why, int column)
    {
        var scan = PageReader.Read("Pages/X.razor", $"@page \"/x\"\n<DocsSection {attribute}></DocsSection>\n");

        var problem = Assert.Single(scan.Problems);
        Assert.Equal(PageProblemKind.TitleNotLiteral, problem.Kind);
        Assert.Contains(why, problem.Message);
        Assert.Equal(2, problem.Line);
        Assert.Equal(column, problem.Column);
    }

    [Fact] // ADR-0110: a page's own title, and an Example's, are text too
    public void A_page_or_Example_title_that_is_not_text_is_refused()
    {
        var scan = PageReader.Read("Pages/X.razor", """
            @page "/x"
            <DocsPage Title="@Name" Lead="@Lead">
                <DocsSection Title="One">
                    <DocsExample Title="@ExampleTitle" Builtin="typeof(A)" />
                </DocsSection>
            </DocsPage>
            """);

        Assert.Equal([2, 2, 4], scan.Problems.Select(p => p.Line));
        Assert.All(scan.Problems, p => Assert.Equal(PageProblemKind.TitleNotLiteral, p.Kind));
        Assert.Null(scan.Page.Title);
        Assert.Null(scan.Page.Lead);
    }

    [Fact] // ADR-0110: two sections with one anchor are a build error at the second, naming the first
    public void Two_sections_with_one_anchor_are_refused()
    {
        var scan = PageReader.Read("Pages/X.razor", """
            @page "/x"
            <DocsSection Title="API"></DocsSection>
            <DocsSection Title="Totals"></DocsSection>
            <DocsSection Title="Api"></DocsSection>
            """);

        var problem = Assert.Single(scan.Problems);
        Assert.Equal(PageProblemKind.DuplicateAnchor, problem.Kind);
        Assert.Equal(4, problem.Line);
        Assert.Contains("#api", problem.Message);
        Assert.Contains("\"API\" at line 2", problem.Message);
    }

    [Theory] // ADR-0110: markup the reader cannot follow is a build error that names its line and the fix
    [InlineData("<DocsSection Title=\"Open\">\n<p>never closed</p>\n", 2, "never closed. Close it with </DocsSection>")]
    [InlineData("</DocsSection>\n", 2, "never opened")]
    [InlineData("<DocsSection Title=\"One\">\n<DocsExample Builtin=\"typeof(A)\"\n", 3, "<DocsExample> tag is never closed")]
    [InlineData("<DocsSection Title=\"One\n", 2, "never closed, so the rest of the page cannot be read. Close its value")]
    [InlineData("<DocsSection Title=\"One\" Files=\"@([\"a.cs\"\" />\n", 2, "never closed")]
    [InlineData("@* a comment\n<DocsSection Title=\"One\" />\n", 2, "Razor comment is never closed")]
    [InlineData("<!-- a comment\n", 2, "HTML comment is never closed")]
    [InlineData("<DocsExample Builtin=\"typeof(A)\" />\n", 2, "outside every DocsSection")]
    [InlineData("<ApiTable Type=\"typeof(A)\" />\n", 2, "outside every DocsSection")]
    [InlineData("<DocsSection Title=\"One\"\n<p>\n</DocsSection>\n", 2, "never closed before the next tag")]
    [InlineData("@code {\n    void M() { if (true) {\n}\n", 2, "braces never balance")]
    public void Markup_that_cannot_be_read_is_refused(string body, int line, string message)
    {
        var scan = PageReader.Read("Pages/X.razor", "@page \"/x\"\n" + body);

        var problem = scan.Problems.First();
        Assert.Equal(PageProblemKind.Unreadable, problem.Kind);
        Assert.Equal(line, problem.Line);
        Assert.Contains(message, problem.Message);
    }

    [Theory] // ADR-0110: a route the reader cannot take is refused, not guessed
    [InlineData("@page /x\n", "no route in quotes")]
    [InlineData("@page \"/x\n", "never closed")]
    public void A_route_that_cannot_be_read_is_refused(string text, string message)
    {
        var scan = PageReader.Read("Pages/X.razor", text);

        var problem = Assert.Single(scan.Problems);
        Assert.Equal(PageProblemKind.Unreadable, problem.Kind);
        Assert.Equal(1, problem.Line);
        Assert.Contains(message, problem.Message);
        Assert.Empty(scan.Page.Routes);
    }

    [Fact] // ADR-0110: a file with Windows line endings is located by the same lines
    public void Lines_are_counted_under_either_line_ending()
    {
        var scan = PageReader.Read("Pages/X.razor", "@page \"/x\"\r\n\r\n<DocsSection Title=\"@x\"></DocsSection>\r\n");

        var problem = Assert.Single(scan.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Equal(21, problem.Column);
    }
}
