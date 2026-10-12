using Bunit;
using ExGrid.Chrome;
using ExPivot.Chrome;
using ExGrid.Testing;
using ExPivot.Components.Tests.Support;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>
/// ADR-0123 for ExPivot: its stylesheet component links the file this build ships at an address that
/// changes with its content (ERR-9), and the pivot's root carries the stamp of that stylesheet in a
/// notice only another version's stylesheet shows (ERR-11). The report's grid carries ExGrid's own.
/// </summary>
public class ShippedFilesTests : PivotTestContext
{
    [Fact] // ADR-0123 / ERR-9: ExPivot's stylesheet component links the plain path with the shipped file's version where the host fingerprints nothing
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<ExPivotStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"_content/ExPivot/ex-pivot.min.css?v={ShippedFiles.Version("ExPivot", "ex-pivot.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-11: the pivot carries the stamp of ExPivot's stylesheet, hidden until a stylesheet's rules decide, in the words its Label gives
    public void ERR11_the_pivot_carries_its_stylesheets_notice()
    {
        var cut = RenderPivot(parameters: ps => ps.Add(p => p.Label, id => id == ReloadNoticeLabelIds.Stylesheet ? "Rechargez." : null));

        var notice = cut.Find(".ex-pivot > [data-ex-stylesheet='ex-pivot.css']");
        Assert.Equal("stylesheet", notice.GetAttribute("data-ex-notice"));
        Assert.Equal(ShippedFiles.Stamp("ExPivot", "ex-pivot.css"), notice.GetAttribute("data-ex-stamp"));
        Assert.Equal("display: none", notice.GetAttribute("style"));
        Assert.Equal("Rechargez.", notice.TextContent);
    }

    [Fact] // ADR-0123 / ERR-11 (2026-10-11): a Pivot Chrome declares the stylesheets it brings, and ExPivot's root renders a notice for each beside its own; the built-in markup brings none
    public void ERR11_the_pivot_renders_a_notice_for_each_stylesheet_its_chrome_brings()
    {
        Assert.Empty(((IPivotChrome)new BringsNothing()).Stylesheets);
        Assert.Equal(["ex-pivot.css"], RenderPivot().FindAll(".ex-pivot > [data-ex-notice='stylesheet']").Select(n => n.GetAttribute("data-ex-stylesheet")));

        var cut = RenderPivot(parameters: ps => ps.Add(p => p.PivotChrome, new BringsOne()));

        var notices = cut.FindAll(".ex-pivot > [data-ex-notice='stylesheet']");
        Assert.Equal(["ex-pivot.css", "acme-pivot.css"], notices.Select(n => n.GetAttribute("data-ex-stylesheet")));
        Assert.Equal(BringsOne.Stamp, notices[1].GetAttribute("data-ex-stamp"));
        Assert.Equal("display: none", notices[1].GetAttribute("style"));
        Assert.Equal(BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet), notices[1].TextContent);
    }

    /// <summary>A Pivot Chrome that draws nothing of its own.</summary>
    private sealed class BringsNothing : IPivotChrome;

    /// <summary>A Wrapper's Pivot Chrome, bringing a stylesheet of its own.</summary>
    private sealed class BringsOne : IPivotChrome
    {
        internal const string Stamp = "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0";

        public IReadOnlyList<ChromeStylesheet> Stylesheets { get; } = [new("acme-pivot.css", Stamp)];
    }

    [Fact] // ADR-0123 / ERR-11 / ADR-0203: without a word of the Consumer's, the notice is the built-in Chrome's English
    public void ERR11_the_notice_is_the_built_in_words_by_default()
    {
        var cut = RenderPivot();

        Assert.Equal(BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet),
            cut.Find(".ex-pivot > [data-ex-notice='stylesheet']").TextContent);
    }
}
