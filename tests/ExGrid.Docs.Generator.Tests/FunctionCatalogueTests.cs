using Microsoft.CodeAnalysis;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// ExSheet's function catalogue as the Functions page reads it (ADR-0110, ADR-0047): the <c>###</c> categories
/// under <c>## Catalogue</c>, each function's name and status, in the catalogue's order.
/// </summary>
public class FunctionCatalogueTests
{
    private const string Repo = "/repo/";

    private const string CataloguePath = Repo + "docs/specs/exsheet-functions/spec.md";

    private const string Page = """
        @page "/exsheet/functions"
        <DocsPage Product="ExSheet" Title="Functions" Lead="Every function.">
        </DocsPage>
        """;

    private const string Catalogue = """
        # ExSheet's function catalogue

        ## Status

        | Status | Meaning | What moves it on |
        |---|---|---|
        | **Supported** | Declared | — |

        ## Catalogue

        ### Aggregation

        | Function | Status | Priority | Notes |
        |---|---|---|---|
        | `SUM` | Supported | — | |
        | `SUBTOTAL` | Ready | P3 | Ticket 16 |

        ```
        | `NOT.A.ROW` | Supported | — | inside a fence |
        ```

        ### Lookup and reference

        | Function | Status | Priority | Notes |
        |---|---|---|---|
        | `XLOOKUP` | Supported | — | |
        | `INDIRECT` | Ready | P3 | Ticket 18 |

        ## Order of work

        | `AFTER` | Supported | — | outside the catalogue |
        """;

    private static (string, string)[] Fixture(string? catalogue = Catalogue, bool page = true)
    {
        var files = new List<(string, string)>();
        if (page)
            files.Add(($"{GeneratorHarness.SiteDir}/Pages/Sheet/SheetFunctions.razor", Page));
        if (catalogue is not null)
            files.Add((CataloguePath, catalogue));
        return files.ToArray();
    }

    private static GeneratorRun Run((string, string)[] files) => GeneratorHarness.Run([], files);

    private static Diagnostic One(GeneratorRun run)
    {
        var error = Assert.Single(run.Diagnostics.Where(d => d.Id == "EXDOCS019"));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    private static int Line(Diagnostic diagnostic) => diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;

    [Fact] // ADR-0047, ADR-0110: the categories under ## Catalogue, with each function's status, in the catalogue's order
    public void The_categories_are_read_in_order()
    {
        var run = Run(Fixture());

        Assert.Empty(run.Diagnostics);
        var source = run.Source("FunctionCatalogue.g.cs").Replace("\r\n", "\n");
        Assert.Contains("""
                    new("Aggregation", "aggregation",
                        [new("SUM", "Supported"), new("SUBTOTAL", "Ready")]),
                    new("Lookup and reference", "lookup-and-reference",
                        [new("XLOOKUP", "Supported"), new("INDIRECT", "Ready")]),
                ];
            """.Replace("\r\n", "\n"), source);
        // A row in a code fence, or outside the catalogue, is not a function of it.
        Assert.DoesNotContain("NOT.A.ROW", source);
        Assert.DoesNotContain("AFTER", source);
    }

    [Fact] // ADR-0047: a status outside the catalogue's own stops the build, at its row
    public void An_unknown_status_stops_the_build()
    {
        var run = Run(Fixture(Catalogue.Replace("| `SUBTOTAL` | Ready |", "| `SUBTOTAL` | Soon |")));

        var error = One(run);
        Assert.Equal(16, Line(error));
        Assert.Contains("The function SUBTOTAL has the status 'Soon'", error.GetMessage());
    }

    [Fact] // ADR-0047: a function catalogued twice stops the build: the page would list it twice
    public void A_function_catalogued_twice_stops_the_build()
    {
        var run = Run(Fixture(Catalogue.Replace("| `INDIRECT` | Ready |", "| `SUM` | Supported |")));

        Assert.Contains("The function SUM is catalogued twice, here and under Aggregation", One(run).GetMessage());
    }

    [Fact] // ADR-0047: a row before any category cannot be placed
    public void A_row_before_any_category_stops_the_build()
    {
        var run = Run(Fixture(Catalogue.Replace("## Catalogue\n\n### Aggregation\n", "## Catalogue\n\n| `ABS` | Supported | — | |\n\n### Aggregation\n")));

        Assert.Contains("The function ABS is catalogued before any ### category", One(run).GetMessage());
    }

    [Fact] // ADR-0047: a category with no function would be an empty section
    public void An_empty_category_stops_the_build()
    {
        var run = Run(Fixture(Catalogue.Replace("## Order of work", "### Random\n\nNothing yet.\n\n## Order of work")));

        Assert.Contains("The category 'Random' catalogues no function", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a catalogue whose heading was changed is not read as empty, and says so
    public void A_catalogue_without_its_heading_stops_the_build()
    {
        var run = Run(Fixture(Catalogue.Replace("## Catalogue", "## The functions")));

        Assert.Contains("has no ### category under a ## Catalogue heading", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a site with a Functions page needs the catalogue
    public void A_missing_catalogue_stops_the_build()
    {
        var run = Run(Fixture(catalogue: null));

        Assert.Contains("docs/specs/exsheet-functions/spec.md, whose categories group its functions, did not reach the build", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a site without the Functions page is not held to the catalogue
    public void A_site_without_the_page_is_not_checked()
    {
        var run = Run(Fixture(Catalogue.Replace("| `SUBTOTAL` | Ready |", "| `SUBTOTAL` | Soon |"), page: false));

        Assert.Empty(run.Diagnostics);
    }

    [Fact] // ADR-0110: CRLF line ends read as LF ones do
    public void A_catalogue_with_crlf_line_ends_reads_the_same()
    {
        var run = Run(Fixture(Catalogue.Replace("\n", "\r\n")));

        Assert.Empty(run.Diagnostics);
        Assert.Contains("""new("XLOOKUP", "Supported"), new("INDIRECT", "Ready")""", run.Source("FunctionCatalogue.g.cs"));
    }
}
