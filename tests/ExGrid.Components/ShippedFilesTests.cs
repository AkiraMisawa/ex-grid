using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0123 (rewritten 2026-10-11): <see cref="ExGridStylesheet"/> links the stylesheet this build ships, at an
/// address that changes with the file's content: the host's fingerprinted address where
/// <see cref="ComponentBase.Assets"/> knows it (a Blazor Web App on <c>MapStaticAssets</c>), else the plain one with
/// the first twelve hex digits of the file's SHA-256 as <c>?v=</c> (ERR-9).
/// </summary>
public class ShippedFilesTests : BunitContext
{
    private static string Hash(string text) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));

    private static string Shipped(string file) => File.ReadAllText(Path.Combine(AssetSources.Repository, "src", "ExGrid", "wwwroot", file));

    [Fact] // ADR-0123 / ERR-9: ExGrid's stylesheet component links the plain path with the shipped file's version where the host fingerprints nothing
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<ExGridStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"_content/ExGrid/ex-grid.min.css?v={Hash(Shipped("ex-grid.min.css"))[..12]}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-9: where the host knows the stylesheet's fingerprinted address, the component links it as it is
    public void ERR9_the_stylesheet_component_links_the_hosts_fingerprinted_address()
    {
        AddAsset("_content/ExGrid/ex-grid.min.vmw8r6y1tt.css", "_content/ExGrid/ex-grid.min.css");

        var cut = Render<ExGridStylesheet>();

        Assert.Equal("_content/ExGrid/ex-grid.min.vmw8r6y1tt.css", cut.Find("link").GetAttribute("href"));
    }
}
