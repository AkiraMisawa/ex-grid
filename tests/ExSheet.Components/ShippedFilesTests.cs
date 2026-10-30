using Bunit;
using ExGrid.Testing;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ADR-0123 for ExSheet: its stylesheet component links the file this build ships at an address that
/// changes with its content (ERR-9), and the Sheet's module is the one the component was built with.
/// </summary>
public class ShippedFilesTests : BunitContext
{
    [Fact] // ADR-0123 / ERR-9: ExSheet's stylesheet component links the plain path with the shipped file's version where the host fingerprints nothing
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<ExSheetStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"_content/ExSheet/ex-sheet.min.css?v={ShippedFiles.Version("ExSheet", "ex-sheet.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ADR-0370 s1 / ADR-0411: the bundled module ExSheet.Script.ExSheet imports answers the stamp the component is built with, at the version its address carries
    public void The_script_core_module_is_the_one_the_component_was_built_with()
    {
        const string module = "ex-sheet.min.js";
        var stamp = global::ExSheet.Script.ScriptCoreAsset.Stamp;
        var shipped = File.ReadAllText(ShippedFiles.PathOf("ExSheet", module));
        Assert.StartsWith($"/*! ex-sheet.js sha256:{stamp} ", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{stamp}\"", shipped, StringComparison.Ordinal);
        Assert.Equal(global::ExSheet.Script.ScriptCoreAsset.Version, ShippedFiles.Version("ExSheet", module));
        Assert.Contains($"sourceMappingURL={module}.map", shipped, StringComparison.Ordinal);
        // The grid core and the data come from the Shared Script ExGrid ships, at the address its stamp names, which the
        // module expects (ADR-0411).
        var shared = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(ShippedFiles.PathOf("ExGrid", "ex-grid-script.min.js")),
            "^/\\*! ex-grid-script\\.js sha256:(?<stamp>[0-9a-f]{64}) ").Groups["stamp"].Value;
        Assert.Matches("^[0-9a-f]{64}$", shared);
        Assert.Contains($"from\"../ExGrid/ex-grid-script.min.js?v={shared[..12]}\"", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{shared}\"", shipped, StringComparison.Ordinal);
    }
}
