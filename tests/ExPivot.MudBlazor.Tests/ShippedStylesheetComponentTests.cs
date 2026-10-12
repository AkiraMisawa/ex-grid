using Bunit;
using ExGrid.Testing;
using Xunit;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// ADR-0123 / ERR-9 for ExPivot.MudBlazor: its stylesheet component links the file this build of the Wrapper
/// ships, at an address that changes with the file's content: the host's fingerprinted address where
/// it knows one, else the plain path with the first twelve hex digits of the file's SHA-256 as
/// <c>?v=</c>. Placed in the head, it needs nothing of the Wrapper's services.
/// </summary>
public class ShippedStylesheetComponentTests : BunitContext
{
    private const string Plain = "_content/ExPivot.MudBlazor/mud-ex-pivot.min.css";

    [Fact] // ADR-0123 / ERR-9: where the host fingerprints nothing, the plain path with the shipped file's version
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<MudExPivotStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"{Plain}?v={ShippedFiles.Version("ExPivot.MudBlazor", "mud-ex-pivot.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-9: where the host knows the file's fingerprinted address, that address as it is
    public void ERR9_the_stylesheet_component_links_the_hosts_fingerprinted_address()
    {
        AddAsset("_content/ExPivot.MudBlazor/mud-ex-pivot.min.0a1b2c3d4e.css", Plain);

        var cut = Render<MudExPivotStylesheet>();

        Assert.Equal("_content/ExPivot.MudBlazor/mud-ex-pivot.min.0a1b2c3d4e.css", cut.Find("link").GetAttribute("href"));
    }
}

/// <summary>
/// ADR-0123 / ERR-11 for ExPivot.MudBlazor: the Wrapper has no root of its own (ADR-0062), so its
/// Chrome declares the stylesheet it brings and ExPivot's root renders its notice beside its own;
/// the report grid's Chrome, ExGrid.MudBlazor's, brings <c>mud-ex-grid.css</c>, whose notice the
/// report grid renders.
/// </summary>
public class WrapperStylesheetNoticeTests : ExPivot.MudBlazor.Tests.Support.MudPivotTestContext
{
    [Fact] // ADR-0123 / ERR-11: MudPivotChrome brings mud-ex-pivot.css and ExPivot's root renders its notice; the report grid renders mud-ex-grid.css's
    public void ERR11_the_pivot_renders_the_wrappers_stylesheet_notices()
    {
        var pivot = new global::ExGrid.Chrome.ChromeStylesheet("mud-ex-pivot.css", ShippedFiles.Stamp("ExPivot.MudBlazor", "mud-ex-pivot.css"));
        Assert.Equal([pivot], MudPivotChrome.Default.Stylesheets);

        var cut = RenderPivot(new ExPivot.Engine.PivotLayout { Rows = [new("Region")], Values = [Sum("Amount")] });

        var notices = cut.FindAll(".ex-pivot > [data-ex-notice='stylesheet']");
        Assert.Equal(["ex-pivot.css", "mud-ex-pivot.css"], notices.Select(n => n.GetAttribute("data-ex-stylesheet")));
        Assert.Equal(pivot.Stamp, notices[1].GetAttribute("data-ex-stamp"));
        Assert.Equal("display: none", notices[1].GetAttribute("style"));
        cut.WaitForAssertion(() => Assert.Equal(["ex-grid.css", "mud-ex-grid.css"],
            cut.FindAll(".ex-pivot-sheet .ex-grid > [data-ex-notice='stylesheet']").Select(n => n.GetAttribute("data-ex-stylesheet"))));
        Assert.Equal(ShippedFiles.Stamp("ExGrid.MudBlazor", "mud-ex-grid.css"),
            cut.Find(".ex-pivot-sheet .ex-grid > [data-ex-stylesheet='mud-ex-grid.css']").GetAttribute("data-ex-stamp"));
    }

    [Fact] // ADR-0123 / ERR-11: ExPivot's built-in markup brings no stylesheet, so only ExPivot's own notice stands in its root
    public void ERR11_the_built_in_markup_brings_no_stylesheet()
    {
        var cut = RenderPivot(new ExPivot.Engine.PivotLayout { Rows = [new("Region")], Values = [Sum("Amount")] }, chrome: BuiltIn);

        Assert.Equal(["ex-pivot.css"], cut.FindAll(".ex-pivot > [data-ex-notice='stylesheet']").Select(n => n.GetAttribute("data-ex-stylesheet")));
    }
}
