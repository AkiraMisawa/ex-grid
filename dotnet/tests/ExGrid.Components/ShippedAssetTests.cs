using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0123: every package serves its JavaScript and CSS minified, with a source map, built from
/// <c>src/&lt;Package&gt;/Assets</c> by <c>tools/assets/minify.mjs</c> and committed. Each minified file
/// names the SHA-256 of the source it was built from, so a source edited without building it again
/// fails here, by name, without Node in the build.
/// </summary>
public class ShippedAssetTests
{
    private static readonly string[] Packages = ["ExChart", "ExGrid", "ExGrid.MudBlazor", "ExPivot", "ExPivot.MudBlazor", "ExSheet", "ExSheet.MudBlazor"];

    private static readonly Regex Banner = new(@"^/\*! (?<name>[\w.-]+) sha256:(?<hash>[0-9a-f]{64}) ", RegexOptions.CultureInvariant);

    public static TheoryData<string, string> Sources()
    {
        var data = new TheoryData<string, string>();
        foreach (var package in Packages)
        {
            foreach (var file in Directory.EnumerateFiles(AssetSources.Folder(package)).Order(StringComparer.Ordinal))
            {
                data.Add(package, Path.GetFileName(file));
            }
        }
        return data;
    }

    private static string Wwwroot(string package) => Path.Combine(AssetSources.Repository, "dotnet", "src", package, "wwwroot");

    private static string MinifiedName(string source) =>
        $"{Path.GetFileNameWithoutExtension(source)}.min{Path.GetExtension(source)}";

    /// <summary>As the script hashes it: the text with LF line endings, so a Windows checkout hashes the same.</summary>
    private static string HashOf(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));

    [Theory, MemberData(nameof(Sources))] // ADR-0123: what ships was built from the source as it stands
    public void The_minified_file_was_built_from_the_source_as_it_stands(string package, string source)
    {
        var minified = Path.Combine(Wwwroot(package), MinifiedName(source));
        Assert.True(File.Exists(minified), $"{package} ships no {MinifiedName(source)}: run tools/assets/minify.mjs");
        var banner = Banner.Match(File.ReadAllText(minified));
        Assert.True(banner.Success, $"{minified} does not open with the banner tools/assets/minify.mjs writes");
        Assert.Equal(source, banner.Groups["name"].Value);
        Assert.True(HashOf(AssetSources.Read(package, source)) == banner.Groups["hash"].Value,
            $"src/{package}/Assets/{source} changed since {MinifiedName(source)} was built: run tools/assets/minify.mjs and commit what it writes");
    }

    [Theory, MemberData(nameof(Sources))] // ADR-0123: each minified file names its source map, and the map carries the source
    public void Each_minified_file_carries_its_source_map(string package, string source)
    {
        var minified = Path.Combine(Wwwroot(package), MinifiedName(source));
        Assert.Contains($"sourceMappingURL={MinifiedName(source)}.map", File.ReadAllText(minified));
        var map = File.ReadAllText($"{minified}.map");
        Assert.Contains($"\"sources\":[\"{source}\"]", map);
        Assert.Contains("\"sourcesContent\":[", map);
    }

    /// <summary>The value of one constant in a package's <c>ShippedAssets.g.cs</c>, as written; null
    /// when the file has none of that name.</summary>
    private static string? Recorded(string package, string name)
    {
        var file = Path.Combine(AssetSources.Repository, "dotnet", "src", package, "ShippedAssets.g.cs");
        Assert.True(File.Exists(file), $"src/{package} has no ShippedAssets.g.cs: run tools/assets/minify.mjs and commit what it writes");
        var match = Regex.Match(File.ReadAllText(file), $@"internal const string {name} = ""(?<value>[^""]*)"";");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string Kind(string source) => Path.GetExtension(source) == ".js" ? "Script" : "Stylesheet";

    [Theory, MemberData(nameof(Sources))] // ADR-0123 / ERR-9: the package records where it serves each file, the version its address carries and the stamp it is checked by, and each is true of the files
    public void ERR9_the_package_records_each_files_address_version_and_stamp(string package, string source)
    {
        var minified = Path.Combine(Wwwroot(package), MinifiedName(source));
        var kind = Kind(source);
        var stale = $"src/{package}/ShippedAssets.g.cs does not match {MinifiedName(source)}: run tools/assets/minify.mjs and commit what it writes";

        Assert.Equal($"_content/{package}/{MinifiedName(source)}", Recorded(package, $"{kind}Path"));
        // The version is the shipped file's own content, read as the minifier wrote it: a new build
        // of the file is a new address in every hosting model.
        Assert.True(HashOf(File.ReadAllText(minified))[..12] == Recorded(package, $"{kind}Version"), stale);
        // The stamp is the source's SHA-256, which the shipped file's banner names.
        Assert.True(Banner.Match(File.ReadAllText(minified)).Groups["hash"].Value == Recorded(package, $"{kind}Stamp"), stale);
    }

    [Fact] // ADR-0123 / ERR-9: what ExGrid was compiled with is what the files say: its stylesheet's address, version and stamp
    public void ERR9_ExGrid_is_built_with_the_recorded_stylesheet()
    {
        var shipped = typeof(global::ExGrid.Components.ExGrid<>).Assembly.GetType("ExGrid.ShippedAssets", throwOnError: true)!;
        string Compiled(string name) => (string)shipped.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
        foreach (var name in new[] { "StylesheetPath", "StylesheetVersion", "StylesheetStamp", "StylesheetName" })
            Assert.Equal(Recorded("ExGrid", name), Compiled(name));
    }

    [Theory, MemberData(nameof(Sources))] // ADR-0123 / ERR-11: each stylesheet shows its product's notice for any stamp and hides it for its own, and nothing after them undoes that
    public void ERR11_each_stylesheet_ends_with_the_rules_of_its_notice(string package, string source)
    {
        if (Kind(source) != "Stylesheet")
            return;
        var minified = File.ReadAllText(Path.Combine(Wwwroot(package), MinifiedName(source)));
        var stamp = HashOf(AssetSources.Read(package, source));
        var notice = $@"[data-ex-stylesheet=""{source}""]";

        Assert.EndsWith(
            $"{notice}{{display:block!important}}{notice}[data-ex-stamp=\"{stamp}\"]{{display:none!important}}\n/*# sourceMappingURL={MinifiedName(source)}.map */\n",
            minified.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal(source, Recorded(package, "StylesheetName"));
        // The selector is the contract between versions, so no rule of the source names it: the
        // two rules above are its only ones, besides the look every notice shares.
        Assert.DoesNotContain("data-ex-stylesheet", AssetSources.Read(package, source));
    }

    [Fact] // ADR-0123: a package serves nothing but minified files and their maps, and every one has a source
    public void Each_package_serves_only_what_a_source_builds()
    {
        foreach (var package in Packages)
        {
            var expected = Directory.EnumerateFiles(AssetSources.Folder(package))
                .Select(file => MinifiedName(Path.GetFileName(file)))
                .Concat(package == "ExGrid" ? [ScriptCoreModule, SharedScript] : [])
                .SelectMany(name => new[] { name, $"{name}.map" })
                .Order(StringComparer.Ordinal);
            var served = Directory.EnumerateFiles(Wwwroot(package))
                .Select(Path.GetFileName)
                .Where(name => package != "ExPivot" || name?.StartsWith(PivotScriptModule, StringComparison.Ordinal) != true)
                .Where(name => package != "ExSheet" || name?.StartsWith(SheetScriptModule, StringComparison.Ordinal) != true)
                .Where(name => package != "ExChart" || name?.StartsWith(ChartScriptModule, StringComparison.Ordinal) != true)
                .Order(StringComparer.Ordinal);
            Assert.Equal(expected, served);
        }
    }

    /// <summary>The Script Core's module, which ExGrid's build bundles from <c>packages/</c> rather than from an
    /// <c>Assets</c> source (ADR-0123's note of 2026-10-26).</summary>
    private const string ScriptCoreModule = "ex-grid-core.min.js";

    /// <summary>The Shared Script, the core and the data it reads, which ExGrid's build bundles beside its bridge and
    /// every product's Blazor module imports (ADR-0411).</summary>
    private const string SharedScript = "ex-grid-script.min.js";

    /// <summary>ExPivot's Script Core's module, which ExPivot's build bundles from <c>packages/</c> as ExGrid's does
    /// (ADR-0370 section 1). This project does not build ExPivot, so the module is there or not as ExPivot was last
    /// built: ExPivot's own tests, which build it, hold it to the stamp its component is built with.</summary>
    private const string PivotScriptModule = "ex-pivot.min.js";

    /// <summary>ExSheet's, bundled as ExSheet builds and held to its stamp by ExSheet's own tests, as ExPivot's is.</summary>
    private const string SheetScriptModule = "ex-sheet.min.js";

    /// <summary>ExChart's Script Core's module, which ExChart's build bundles the same way; held to its stamp by
    /// ExChart's own tests.</summary>
    private const string ChartScriptModule = "ex-chart-core.min.js";

    [Fact] // ADR-0123 / ADR-0350 s7: the bundled module answers the stamp the component is built with, at the version its address carries
    public void The_script_core_module_is_the_one_the_component_was_built_with()
    {
        var stamp = global::ExGrid.Components.ScriptCoreAsset.Stamp;
        var module = Path.Combine(Wwwroot("ExGrid"), ScriptCoreModule);
        var shipped = File.ReadAllText(module);
        var banner = Banner.Match(shipped);
        Assert.True(banner.Success, $"{module} does not open with the banner tools/scripts/build-blazor.mjs writes");
        Assert.Equal("ex-grid-core.js", banner.Groups["name"].Value);
        Assert.Equal(stamp, banner.Groups["hash"].Value);
        Assert.Contains($"\"{stamp}\"", shipped, StringComparison.Ordinal);
        Assert.Equal(global::ExGrid.Components.ScriptCoreAsset.Version, HashOf(shipped)[..12]);
        Assert.Contains($"sourceMappingURL={ScriptCoreModule}.map", shipped, StringComparison.Ordinal);
    }

    [Fact] // ADR-0411 / ADR-0123: the bridge imports the Shared Script shipped beside it, at an address its stamp names, and expects that stamp
    public void The_bridge_imports_the_shared_script_shipped_beside_it()
    {
        var bridge = File.ReadAllText(Path.Combine(Wwwroot("ExGrid"), ScriptCoreModule));
        var shared = File.ReadAllText(Path.Combine(Wwwroot("ExGrid"), SharedScript));
        var banner = Banner.Match(shared);
        Assert.True(banner.Success, $"{SharedScript} does not open with the banner tools/scripts/build-blazor.mjs writes");
        Assert.Equal("ex-grid-script.js", banner.Groups["name"].Value);
        var stamp = banner.Groups["hash"].Value;
        // The Shared Script answers its stamp, and the bridge expects that stamp of the one it is handed.
        Assert.Contains($"\"{stamp}\"", shared, StringComparison.Ordinal);
        Assert.Contains($"\"{stamp}\"", bridge, StringComparison.Ordinal);
        Assert.Contains($"from\"./{SharedScript}?v={stamp[..12]}\"", bridge, StringComparison.Ordinal);
        Assert.Contains($"sourceMappingURL={SharedScript}.map", shared, StringComparison.Ordinal);
    }
}
