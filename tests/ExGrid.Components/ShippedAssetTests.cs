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
    private static readonly string[] Packages = ["ExGrid", "ExGrid.MudBlazor", "ExPivot", "ExPivot.MudBlazor", "ExSheet", "ExSheet.MudBlazor"];

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

    private static string Wwwroot(string package) => Path.Combine(AssetSources.Repository, "src", package, "wwwroot");

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

    [Fact] // ADR-0123: a package serves nothing but minified files and their maps, and every one has a source
    public void Each_package_serves_only_what_a_source_builds()
    {
        foreach (var package in Packages)
        {
            var expected = Directory.EnumerateFiles(AssetSources.Folder(package))
                .Select(file => MinifiedName(Path.GetFileName(file)))
                .SelectMany(name => new[] { name, $"{name}.map" })
                .Order(StringComparer.Ordinal);
            var served = Directory.EnumerateFiles(Wwwroot(package))
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal);
            Assert.Equal(expected, served);
        }
    }
}
