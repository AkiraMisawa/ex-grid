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
