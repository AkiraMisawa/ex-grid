using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Testing;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>
/// ADR-0123 for ExPivot: its stylesheet component links the file this build ships at an address that
/// changes with its content (ERR-9), and the Script Core module the component imports is the one it was built with.
/// </summary>
public class ShippedFilesTests : BunitContext
{
    [Fact] // ADR-0123 / ERR-9: ExPivot's stylesheet component links the plain path with the shipped file's version where the host fingerprints nothing
    public void ERR9_the_stylesheet_component_links_the_shipped_files_version()
    {
        var cut = Render<ExPivotStylesheet>();

        var link = cut.Find("link");
        Assert.Equal("stylesheet", link.GetAttribute("rel"));
        Assert.Equal($"_content/ExPivot/ex-pivot.min.css?v={ShippedFiles.Version("ExPivot", "ex-pivot.min.css")}", link.GetAttribute("href"));
    }

    [Fact] // ADR-0123 / ADR-0370 s1 / ADR-0411: the bundled module ExPivot.Components.ExPivot imports answers the stamp the component is built with, at the version its address carries
    public void The_script_core_module_is_the_one_the_component_was_built_with()
    {
        const string module = "ex-pivot.min.js";
        var stamp = global::ExPivot.Components.ScriptCoreAsset.Stamp;
        var shipped = File.ReadAllText(ShippedFiles.PathOf("ExPivot", module));
        Assert.StartsWith($"/*! ex-pivot.js sha256:{stamp} ", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{stamp}\"", shipped, StringComparison.Ordinal);
        Assert.Equal(global::ExPivot.Components.ScriptCoreAsset.Version, ShippedFiles.Version("ExPivot", module));
        Assert.Contains($"sourceMappingURL={module}.map", shipped, StringComparison.Ordinal);
        // The grid and the data come from the Shared Script ExGrid ships, at the address its stamp names, which the
        // module expects of it (ADR-0411).
        var shared = Regex.Match(File.ReadAllText(ShippedFiles.PathOf("ExGrid", "ex-grid-script.min.js")), "^/\\*! ex-grid-script\\.js sha256:(?<stamp>[0-9a-f]{64}) ").Groups["stamp"].Value;
        Assert.Matches("^[0-9a-f]{64}$", shared);
        Assert.Contains($"from\"../ExGrid/ex-grid-script.min.js?v={shared[..12]}\"", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{shared}\"", shipped, StringComparison.Ordinal);
    }
}
