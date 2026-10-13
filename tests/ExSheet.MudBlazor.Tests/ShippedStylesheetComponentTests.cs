using Bunit;
using ExGrid.Testing;
using Xunit;

namespace ExSheet.MudBlazor.Tests;

/// <summary>
/// ADR-0123 / ERR-9 for ExSheet.MudBlazor: its stylesheet component links the file this build of the Wrapper
/// ships, at an address that changes with the file's content: the host's fingerprinted address where
/// it knows one, else the plain path with the first twelve hex digits of the file's SHA-256 as
/// <c>?v=</c>. Placed in the head, it needs nothing of the Wrapper's services.
/// </summary>
public class ShippedStylesheetComponentTests : BunitContext
{
    private const string Plain = "_content/ExSheet.MudBlazor/mud-ex-sheet.min.css";

    [Fact] // ADR-0123 / ERR-9: where the host fingerprints nothing, the plain path with the shipped file's version
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<MudExSheetStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"{Plain}?v={ShippedFiles.Version("ExSheet.MudBlazor", "mud-ex-sheet.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-9: where the host knows the file's fingerprinted address, that address as it is
    public void ERR9_the_stylesheet_component_links_the_hosts_fingerprinted_address()
    {
        AddAsset("_content/ExSheet.MudBlazor/mud-ex-sheet.min.0a1b2c3d4e.css", Plain);

        var cut = Render<MudExSheetStylesheet>();

        Assert.Equal("_content/ExSheet.MudBlazor/mud-ex-sheet.min.0a1b2c3d4e.css", cut.Find("link").GetAttribute("href"));
    }
}

/// <summary>
/// ADR-0123 / ERR-11 for ExSheet.MudBlazor: the Wrapper has no root of its own (ADR-0062), so its
/// Chrome declares the stylesheets the Sheet's grid is dressed with — its own and ExGrid.MudBlazor's
/// — and the Sheet's grid renders a notice for each beside its own, beside the Sheet's.
/// </summary>
public class WrapperStylesheetNoticeTests : MudSheetTestContext
{
    [Fact] // ADR-0123 / ERR-11: MudSheetChrome brings mud-ex-sheet.css and its grid Chrome's mud-ex-grid.css, and the Sheet's grid renders both notices
    public void ERR11_the_sheets_grid_renders_the_wrappers_stylesheet_notices()
    {
        var sheet = new global::ExGrid.Chrome.ChromeStylesheet("mud-ex-sheet.css", ShippedFiles.Stamp("ExSheet.MudBlazor", "mud-ex-sheet.css"));
        var grid = new global::ExGrid.Chrome.ChromeStylesheet("mud-ex-grid.css", ShippedFiles.Stamp("ExGrid.MudBlazor", "mud-ex-grid.css"));
        Assert.Equal([sheet, grid], MudSheetChrome.Default.Stylesheets);

        var page = RenderPage();

        Assert.Equal(["ex-sheet.css"], page.FindAll(".ex-sheet > [data-ex-notice='stylesheet']").Select(n => n.GetAttribute("data-ex-stylesheet")));
        var notices = page.FindAll(".ex-sheet .ex-grid > [data-ex-notice='stylesheet']");
        Assert.Equal(["ex-grid.css", "mud-ex-sheet.css", "mud-ex-grid.css"], notices.Select(n => n.GetAttribute("data-ex-stylesheet")));
        Assert.Equal([sheet.Stamp, grid.Stamp], notices.Skip(1).Select(n => n.GetAttribute("data-ex-stamp")));
        Assert.All(notices, n => Assert.Equal("display: none", n.GetAttribute("style")));
    }
}
