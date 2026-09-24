using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// WR-8: the Wrapper adds no script — no <c>.js</c> of its own and no interop call of its
/// own (ADR-0021, as narrowed by ADR-0039). What MudBlazor's components run for themselves
/// is MudBlazor's, loaded by the Consumer's choice of it, and is not counted here. The
/// Definition of Done states the verification as "inspect the package; grep for
/// <c>IJSRuntime</c>, <c>IJSObjectReference</c>, <c>.js</c>", and these are that
/// inspection and that grep: over the package's source tree, and over what the build
/// itself records the package as serving.
/// </summary>
public class WrapperScriptTests
{
    private static readonly string[] ScriptExtensions = [".js", ".mjs", ".cjs"];

    /// <summary>
    /// The types a component needs to call into JavaScript or to be called from it. Two
    /// things are deliberately absent: <c>ElementReference.FocusAsync</c>, which is
    /// Blazor's own and is how a Chrome focuses its control (ADR-0021/0030), and
    /// <c>JSException</c>, which the editor catches around that call without making one.
    /// </summary>
    private static readonly Regex Interop = new(
        @"\b(IJSRuntime|IJSInProcessRuntime|IJSUnmarshalledRuntime|IJSObjectReference|IJSInProcessObjectReference|DotNetObjectReference|JSInvokable|JSImport|JSExport)\b",
        RegexOptions.CultureInvariant);

    /// <summary>The Wrapper's project directory, found from the repository root rather
    /// than guessed from the output path's depth, and refused if it is not there — a scan
    /// of a directory that does not exist would pass by finding nothing.</summary>
    private static string WrapperDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");

        var wrapper = Path.Combine(directory!.FullName, "src", "ExGrid.MudBlazor");
        Assert.True(File.Exists(Path.Combine(wrapper, "ExGrid.MudBlazor.csproj")), $"the Wrapper is not at {wrapper}");
        return wrapper;
    }

    /// <summary>Every file of the package's source, its build output left out.</summary>
    private static List<string> SourceFiles()
    {
        var root = WrapperDirectory();
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .ToList();
        // What the scan must at least have seen, or it proves nothing.
        Assert.Contains(files, file => file.EndsWith(".razor", StringComparison.Ordinal));
        Assert.Contains(files, file => file.EndsWith(".cs", StringComparison.Ordinal));
        return files;
    }

    [Fact] // WR-8, ADR-0021/0039: no .js file in the Wrapper's source
    public void The_Wrapper_source_holds_no_script_file()
    {
        var root = WrapperDirectory();
        var scripts = SourceFiles()
            .Where(file => ScriptExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.Empty(scripts);
    }

    [Fact] // WR-8, ADR-0021/0039: no interop call of the Wrapper's own — no IJSRuntime, no IJSObjectReference
    public void The_Wrapper_source_names_no_interop_type()
    {
        var root = WrapperDirectory();
        var offenders = SourceFiles()
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file)
                .Select((line, index) => (File: Path.GetRelativePath(root, file), Number: index + 1, Line: line.Trim())))
            .Where(entry => Interop.IsMatch(entry.Line))
            .Select(entry => $"{entry.File}:{entry.Number} {entry.Line}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact] // WR-8, ADR-0021/0039: the package serves no script — read from the build's own static-web-asset manifest
    public void The_Wrapper_serves_no_script_of_its_own()
    {
        // The manifest the build writes for the Wrapper: its own assets sit at the root,
        // and what it merely references — the core's, MudBlazor's — under _content/.
        // Only the Wrapper's own are this criterion's; MudBlazor's script is MudBlazor's.
        var manifest = Path.Combine(AppContext.BaseDirectory, "ExGrid.MudBlazor.staticwebassets.runtime.json");
        Assert.True(File.Exists(manifest), $"the build recorded no static assets for the Wrapper ({manifest})");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var own = new List<string>();
        foreach (var child in document.RootElement.GetProperty("Root").GetProperty("Children").EnumerateObject())
        {
            if (child.Name != "_content")
                Collect(child.Name, child.Value, own);
        }

        // The stylesheet is the one asset the Wrapper is known to ship; not finding it
        // means this read the wrong thing, not that the package is clean.
        Assert.Contains("mud-ex-grid.css", own);
        Assert.DoesNotContain(own, path => ScriptExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
    }

    private static void Collect(string path, JsonElement node, List<string> into)
    {
        if (node.TryGetProperty("Asset", out var asset) && asset.ValueKind == JsonValueKind.Object)
            into.Add(path);
        if (node.TryGetProperty("Children", out var children) && children.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in children.EnumerateObject())
                Collect(path + "/" + child.Name, child.Value, into);
        }
    }
}
