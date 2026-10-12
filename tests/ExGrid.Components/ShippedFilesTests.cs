using Bunit;
using ExGrid.Chrome;
using ExGrid.Components.Tests.Support;
using ExGrid.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0123 (rewritten 2026-10-11): each package asks for the files it was built with. The grid
/// imports its script, and <see cref="ExGridStylesheet"/> links its stylesheet, at an address that
/// changes with the shipped file's content: the host's fingerprinted address where
/// <see cref="ComponentBase.Assets"/> knows it (a Blazor Web App on <c>MapStaticAssets</c>), else the
/// plain one with the first twelve hex digits of the file's SHA-256 as <c>?v=</c> (ERR-9). And the
/// root carries the stamp of its stylesheet in a notice only another version's stylesheet shows
/// (ERR-11).
/// </summary>
public class ShippedFilesTests : GridTestContext
{
    /// <summary>The first twelve hex digits of the SHA-256 of the script as shipped.</summary>
    private static readonly string Version = Hash(Shipped("ex-grid.min.js"))[..12];

    private static string Hash(string text) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));

    private static string Shipped(string file) => File.ReadAllText(Path.Combine(AssetSources.Repository, "src", "ExGrid", "wwwroot", file));

    [Fact] // ADR-0123 / ERR-9: where the host knows no fingerprinted address, the script is imported at the plain path with the shipped file's version
    public void ERR9_the_script_is_imported_with_its_version_where_the_host_fingerprints_nothing()
    {
        Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(100)));

        var import = Assert.Single(JSInterop.Invocations["import"]);
        Assert.Equal($"./_content/ExGrid/ex-grid.min.js?v={Version}", import.Arguments[0]);
        // The version is the shipped file's own content: the next build of the file is another address.
        Assert.Equal(ShippedScript.Address, import.Arguments[0]);
    }

    [Fact] // ADR-0123 / ERR-9: the address per hosting model — the host's fingerprinted address as it is, else the plain path with ?v=
    public void ERR9_the_address_is_the_hosts_fingerprinted_one_where_it_knows_one()
    {
        var hosted = new ResourceAssetCollection(
        [
            new ResourceAsset("_content/ExGrid/ex-grid.min.f9tmpy02a2.js", [new ResourceAssetProperty("label", "_content/ExGrid/ex-grid.min.js")]),
        ]);

        // A Blazor Web App on MapStaticAssets: the file's fingerprinted address, served immutable.
        Assert.Equal("_content/ExGrid/ex-grid.min.f9tmpy02a2.js",
            PackageStylesheet.AddressOf(hosted, "_content/ExGrid/ex-grid.min.js", "0123456789ab"));
        // A file that host does not know, and a standalone application, whose host page .NET does
        // not render: the plain path with the version.
        Assert.Equal("_content/ExGrid/ex-grid.min.css?v=0123456789ab",
            PackageStylesheet.AddressOf(hosted, "_content/ExGrid/ex-grid.min.css", "0123456789ab"));
        Assert.Equal("_content/ExGrid/ex-grid.min.js?v=0123456789ab",
            PackageStylesheet.AddressOf(ResourceAssetCollection.Empty, "_content/ExGrid/ex-grid.min.js", "0123456789ab"));
    }

    [Fact] // ADR-0123 / ERR-9: ExGrid's stylesheet component links the plain path with the shipped file's version where the host fingerprints nothing
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<ExGridStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"_content/ExGrid/ex-grid.min.css?v={Hash(Shipped("ex-grid.min.css"))[..12]}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-11: the root carries the stamp of the stylesheet this build ships, out of the way until a stylesheet's rules decide, in the built-in Chrome's words
    public void ERR11_the_root_carries_its_stylesheets_notice()
    {
        var cut = Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(100)));

        var notice = cut.Find(".ex-grid > [data-ex-notice='stylesheet']");
        Assert.Equal("ex-grid.css", notice.GetAttribute("data-ex-stylesheet"));
        Assert.Equal(Hash(AssetSources.Read("ExGrid", "ex-grid.css")), notice.GetAttribute("data-ex-stamp"));
        // Hidden inline, so a page whose stylesheet has not arrived shows nothing; and the shipped
        // stylesheet's own two rules — the only ones that can show it — hide it for this stamp.
        Assert.Equal("display: none", notice.GetAttribute("style"));
        var css = Shipped("ex-grid.min.css");
        Assert.Contains("[data-ex-stylesheet=\"ex-grid.css\"]{display:block!important}", css, StringComparison.Ordinal);
        Assert.Contains($"[data-ex-stylesheet=\"ex-grid.css\"][data-ex-stamp=\"{notice.GetAttribute("data-ex-stamp")}\"]{{display:none!important}}", css, StringComparison.Ordinal);
        Assert.Equal(BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet), notice.TextContent);
        // Nothing is said about it: the stylesheet's check takes no script and no live region.
        Assert.Equal("", cut.Find(".ex-announce").TextContent);
    }

    [Fact] // ADR-0123 / ERR-11 (2026-10-11): a Chrome declares the stylesheets it brings, and the grid renders a notice for each beside its own, with the same markup and words; the built-in Chrome brings none
    public void ERR11_the_grid_renders_a_notice_for_each_stylesheet_its_chrome_brings()
    {
        Assert.Empty(((IGridChrome)new BringsNothing()).Stylesheets);
        var plain = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.Chrome, new BringsNothing()));
        Assert.Equal(["ex-grid.css"], plain.FindAll(".ex-grid > [data-ex-notice='stylesheet']").Select(n => n.GetAttribute("data-ex-stylesheet")));

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.Chrome, new BringsOne())
            .Add(g => g.CommandLabel, id => id == ReloadNoticeLabelIds.Stylesheet ? "Rechargez la page." : null));

        var notices = cut.FindAll(".ex-grid > [data-ex-notice='stylesheet']");
        Assert.Equal(["ex-grid.css", "acme-grid.css"], notices.Select(n => n.GetAttribute("data-ex-stylesheet")));
        Assert.Equal(BringsOne.Stamp, notices[1].GetAttribute("data-ex-stamp"));
        Assert.Equal("display: none", notices[1].GetAttribute("style"));
        Assert.Equal("Rechargez la page.", notices[1].TextContent);
    }

    /// <summary>A Chrome that answers every seam with the core's own, as the built-in does.</summary>
    private class BringsNothing : IGridChrome
    {
        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    /// <summary>A Wrapper's Chrome, bringing a stylesheet of its own.</summary>
    private sealed class BringsOne : BringsNothing, IGridChrome
    {
        internal const string Stamp = "5e1f0c7a2b9d4e6f8a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f";

        public IReadOnlyList<ChromeStylesheet> Stylesheets { get; } = [new("acme-grid.css", Stamp)];
    }

    [Fact] // ADR-0123 / ERR-11 / ADR-0203: the notice's words are the Consumer's, through the label function, where it gives them
    public void ERR11_the_notice_is_worded_through_the_label_function()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.CommandLabel, id => id == ReloadNoticeLabelIds.Stylesheet ? "Rechargez la page." : null));

        Assert.Equal("Rechargez la page.", cut.Find("[data-ex-notice='stylesheet']").TextContent);
    }

    [Fact] // ADR-0123 / ERR-11 / ADR-0203: a substituted Chrome words the notice, as it words a refusal, ahead of the label function
    public void ERR11_a_substituted_chrome_words_the_notice()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.Chrome, new WordsTheNotice())
            .Add(g => g.CommandLabel, id => id == ReloadNoticeLabelIds.Stylesheet ? "Not these words." : null));

        Assert.All(cut.FindAll(".ex-grid > [data-ex-notice='stylesheet']"), notice => Assert.Equal(WordsTheNotice.Words, notice.TextContent));
    }

    /// <summary>A Wrapper's Chrome that brings a stylesheet and words its notice.</summary>
    private sealed class WordsTheNotice : BringsNothing, IGridChrome
    {
        internal const string Words = "La page contient une autre feuille de style. Rechargez-la.";

        public IReadOnlyList<ChromeStylesheet> Stylesheets { get; } = [new("acme-grid.css", BringsOne.Stamp)];

        string? IGridChrome.Words(string id) => id == ReloadNoticeLabelIds.Stylesheet ? Words : null;
    }
}

/// <summary>
/// ADR-0123 / ERR-9 in a host that fingerprints its files: the grid imports the address the host
/// gives, which it serves <c>immutable</c>, and the stylesheet component links the host's address
/// too. bUnit's renderer is told the files as <c>MapStaticAssets</c> tells a Blazor Web App's.
/// </summary>
public class FingerprintedFilesTests : BunitContext
{
    private const string Script = "_content/ExGrid/ex-grid.min.f9tmpy02a2.js";
    private const string Stylesheet = "_content/ExGrid/ex-grid.min.vmw8r6y1tt.css";

    public FingerprintedFilesTests()
    {
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        AddAsset(Script, "_content/ExGrid/ex-grid.min.js");
        AddAsset(Stylesheet, "_content/ExGrid/ex-grid.min.css");
    }

    [Fact] // ADR-0123 / ERR-9: where the host knows the script's fingerprinted address, that is the address imported, and the grid attaches to it
    public void ERR9_the_script_is_imported_at_the_hosts_fingerprinted_address()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./" + Script);
        module.Setup<string>("stamp").SetResult(ShippedScript.Stamp);
        module.SetupModule("attach", _ => true);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(10))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100));

        var import = Assert.Single(JSInterop.Invocations["import"]);
        Assert.Equal("./" + Script, import.Arguments[0]);
        Assert.Single(module.Invocations["attach"]);
        cut.WaitForAssertion(() => Assert.Null(cut.Find(".ex-grid").GetAttribute("aria-busy")));
        Assert.Empty(cut.FindAll("[data-ex-notice='script']"));
    }

    [Fact] // ADR-0123 / ERR-9: where the host knows the stylesheet's fingerprinted address, the component links it as it is
    public void ERR9_the_stylesheet_component_links_the_hosts_fingerprinted_address()
    {
        var cut = Render<ExGridStylesheet>();

        Assert.Equal(Stylesheet, cut.Find("link").GetAttribute("href"));
    }
}
