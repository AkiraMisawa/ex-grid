using Bunit;
using ExGrid.Chrome;
using ExGrid.Testing;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ADR-0123 for ExSheet: its stylesheet component links the file this build ships at an address that
/// changes with its content (ERR-9), and the Sheet's root carries the stamp of that stylesheet in a
/// notice only another version's stylesheet shows (ERR-11). The grid inside carries ExGrid's own.
/// </summary>
public class ShippedFilesTests : SheetTestContext
{
    [Fact] // ADR-0123 / ERR-9: ExSheet's stylesheet component links the plain path with the shipped file's version where the host fingerprints nothing
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<ExSheetStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"_content/ExSheet/ex-sheet.min.css?v={ShippedFiles.Version("ExSheet", "ex-sheet.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ERR-11: the Sheet carries the stamp of ExSheet's stylesheet, hidden until a stylesheet's rules decide, and its grid carries ExGrid's
    public void ERR11_the_sheet_carries_its_stylesheets_notice()
    {
        var cut = RenderSheet();

        var notice = cut.Find(".ex-sheet > [data-ex-stylesheet='ex-sheet.css']");
        Assert.Equal("stylesheet", notice.GetAttribute("data-ex-notice"));
        Assert.Equal(ShippedFiles.Stamp("ExSheet", "ex-sheet.css"), notice.GetAttribute("data-ex-stamp"));
        Assert.Equal("display: none", notice.GetAttribute("style"));
        Assert.Equal(BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet), notice.TextContent);
        var grids = cut.FindAll(".ex-grid > [data-ex-stylesheet='ex-grid.css']");
        Assert.Equal(ShippedFiles.Stamp("ExGrid", "ex-grid.css"), Assert.Single(grids).GetAttribute("data-ex-stamp"));
    }

    [Fact] // ADR-0123 / ADR-0370 s1: the bundled module ExSheet.Script.ExSheet imports answers the stamp the component is built with, at the version its address carries
    public void The_script_core_module_is_the_one_the_component_was_built_with()
    {
        const string module = "ex-sheet.min.js";
        var stamp = global::ExSheet.Script.ScriptCoreAsset.Stamp;
        var shipped = File.ReadAllText(ShippedFiles.PathOf("ExSheet", module));
        Assert.StartsWith($"/*! ex-sheet.js sha256:{stamp} ", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{stamp}\"", shipped, StringComparison.Ordinal);
        Assert.Equal(global::ExSheet.Script.ScriptCoreAsset.Version, ShippedFiles.Version("ExSheet", module));
        Assert.Contains($"sourceMappingURL={module}.map", shipped, StringComparison.Ordinal);
    }

    [Fact] // ADR-0123 / ERR-11 / ADR-0203: the Sheet's notice is worded through its label function, as its grid's words are
    public void ERR11_the_notice_is_worded_through_the_label_function()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.CommandLabel, id => id == ReloadNoticeLabelIds.Stylesheet ? "Bitte neu laden." : null));

        Assert.Equal("Bitte neu laden.", cut.Find(".ex-sheet > [data-ex-notice='stylesheet']").TextContent);
        Assert.Equal("Bitte neu laden.", cut.Find(".ex-grid > [data-ex-notice='stylesheet']").TextContent);
    }
}
