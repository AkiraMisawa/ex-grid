using Bunit;
using ExGrid.Testing;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// ADR-0123 / ERR-9 for ExGrid.MudBlazor: its stylesheet component links the file this build of the Wrapper
/// ships, at an address that changes with the file's content: the host's fingerprinted address where
/// it knows one, else the plain path with the first twelve hex digits of the file's SHA-256 as
/// <c>?v=</c>. Placed in the head, it needs nothing of the Wrapper's services.
/// </summary>
public class ShippedStylesheetComponentTests : BunitContext
{
    private const string Plain = "_content/ExGrid.MudBlazor/mud-ex-grid.min.css";

    [Fact] // ADR-0123 / ERR-9: where the host fingerprints nothing, the plain path with the shipped file's version
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<MudExGridStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"{Plain}?v={ShippedFiles.Version("ExGrid.MudBlazor", "mud-ex-grid.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-9: where the host knows the file's fingerprinted address, that address as it is
    public void ERR9_the_stylesheet_component_links_the_hosts_fingerprinted_address()
    {
        AddAsset("_content/ExGrid.MudBlazor/mud-ex-grid.min.0a1b2c3d4e.css", Plain);

        var cut = Render<MudExGridStylesheet>();

        Assert.Equal("_content/ExGrid.MudBlazor/mud-ex-grid.min.0a1b2c3d4e.css", cut.Find("link").GetAttribute("href"));
    }
}

/// <summary>
/// ADR-0123 / ERR-11 for ExGrid.MudBlazor: the Wrapper has no root of its own (ADR-0062), so its
/// Chrome declares the stylesheet it brings, and the grid's root renders that stylesheet's notice
/// beside its own, with the same markup, which only another version's <c>mud-ex-grid.css</c> shows.
/// </summary>
public class WrapperStylesheetNoticeTests : MudTestContext
{
    [Fact] // ADR-0123 / ERR-11: MudGridChrome brings mud-ex-grid.css with the stamp this build ships, and the grid is handed it for its notice
    public void ERR11_the_grid_is_handed_the_wrappers_stylesheet()
    {
        var expected = new ExGrid.Chrome.ChromeStylesheet("mud-ex-grid.css", ShippedFiles.Stamp("ExGrid.MudBlazor", "mud-ex-grid.css"));
        Assert.Equal([expected], MudGridChrome.Default.Stylesheets);
        Assert.Equal([expected], new MudGridChrome { Label = _ => null }.Stylesheets);

        Render<global::ExGrid.Components.ExGrid<Trade>>(ps => ps
            .Add(g => g.Rows, Rows(5))
            .Add(g => g.Columns, Columns())
            .Add(g => g.Chrome, MudGridChrome.Default));

        // The grid's root places the notice beside its own, in the browser (ERR-11); what it is handed is this.
        var sent = Assert.Single(Attached().GetProperty("stylesheets").EnumerateArray());
        Assert.Equal("mud-ex-grid.css", sent.GetProperty("name").GetString());
        Assert.Equal(expected.Stamp, sent.GetProperty("stamp").GetString());
    }
}
