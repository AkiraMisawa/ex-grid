using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// A React Example's code (ADR-0350 section 12): its .tsx and .ts files under Examples/ are read like a Blazor
/// Example's, and highlighted in the same classes, JSX included.
/// </summary>
public class TypeScriptExampleTests
{
    [Fact] // ADR-0350 section 12: a React Example's .tsx file is an Example source, with its JSX highlighted
    public void A_tsx_example_is_read_and_its_jsx_highlighted()
    {
        const string tsx = """
            import { ExSheet } from '@ex-grid/sheet-react';

            export default function First(): ReactElement {
                // The document is yours.
                const cells = count < 3 ? 1 : 2;
                return <><ExSheet viewportHeight={300} viewportWidth="stretch" /><p>Holds {cells} cells.</p></>;
            }
            """;

        var html = Highlight(tsx);

        Assert.Contains("<span class=\"tk-kw\">import</span>", html);
        Assert.Contains("<span class=\"tk-str\">'@ex-grid/sheet-react'</span>", html);
        Assert.Contains("<span class=\"tk-com\">// The document is yours.</span>", html);
        Assert.Contains("<span class=\"tk-type\">ReactElement</span>", html);
        // A '<' after a name is a comparison, not an element.
        Assert.Contains("count &lt; <span class=\"tk-num\">3</span>", html);
        Assert.Contains("<span class=\"tk-comp\">ExSheet</span> <span class=\"tk-attr\">viewportHeight</span>", html);
        Assert.Contains("<span class=\"tk-num\">300</span>", html);
        Assert.Contains("<span class=\"tk-aval\">&quot;stretch&quot;</span>", html);
        // Text among an element's children is text, and code in its braces is code again.
        Assert.Contains("<span class=\"tk-tag\">p</span><span class=\"tk-punc\">&gt;</span>Holds {cells} cells.", html);
        Assert.DoesNotContain("tk-kw\">in", html);
    }

    [Fact] // ADR-0350 section 12: a dotted element name is one name, whether it stands in code or among children
    public void A_dotted_element_name_is_one_name()
    {
        var html = Highlight("const a = <Theme.Provider value={t}><Theme.Provider /></Theme.Provider>;");

        Assert.Equal(3, html.Split("<span class=\"tk-comp\">Theme.Provider</span>").Length - 1);
    }

    [Fact] // ADR-0350 section 12: in a .ts file a '<' where an expression starts is a type, never an element
    public void A_ts_file_has_no_elements()
    {
        var html = Highlight("const id = <T>(x: T) => x;\nexport const n = 1;", "Ids.ts");

        Assert.DoesNotContain("tk-tag", html);
        Assert.Contains("<span class=\"tk-kw\">export</span> <span class=\"tk-kw\">const</span>", html);
    }

    [Fact] // ADR-0350 section 12: a .ts file the Example reads is an Example source too
    public void A_ts_file_is_an_example_source()
    {
        var html = Highlight("export function expenses(): WorkbookDocument { return workbook.toDocument(); }", "Expenses.ts");

        Assert.Contains("<span class=\"tk-kw\">export</span> <span class=\"tk-kw\">function</span> expenses()", html);
        Assert.Contains(".toDocument()", html);
    }

    private static string Highlight(string text, string name = "First.tsx")
    {
        var run = GeneratorHarness.Run([], [($"{GeneratorHarness.SiteDir}/Examples/Sheet/{name}", text)]);
        Assert.Empty(run.Diagnostics);
        var source = run.Source("ExampleSources.g.cs");
        Assert.Contains($"\"Examples/Sheet/{name}\"", source);
        return Decoded(source);
    }

    // The emitted file holds each source's HTML as a C# string literal; the assertions read the HTML itself.
    private static string Decoded(string source) => source.Replace("\\\"", "\"").Replace("\\n", "\n");
}
