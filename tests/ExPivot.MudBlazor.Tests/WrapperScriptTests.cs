using System.Text.RegularExpressions;
using ExPivot.MudBlazor.Tests.Support;
using Xunit;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// ADR-0062/0021: the Wrapper adds no script — no <c>.js</c> of its own and no interop call of its
/// own. What MudBlazor's controls run for themselves is MudBlazor's, loaded by the Consumer's
/// choice of it; focus goes through Blazor's own <c>FocusAsync</c>.
/// </summary>
public class WrapperScriptTests
{
    private static readonly Regex Interop = new(
        @"\b(IJSRuntime|IJSInProcessRuntime|IJSObjectReference|IJSInProcessObjectReference|DotNetObjectReference|JSInvokable|JSImport|JSExport)\b",
        RegexOptions.CultureInvariant);

    private static List<string> SourceFiles()
    {
        var root = Path.Combine(Repository.Root(), "src", "ExPivot.MudBlazor");
        Assert.True(File.Exists(Path.Combine(root, "ExPivot.MudBlazor.csproj")), $"the Wrapper is not at {root}");
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .ToList();
        Assert.Contains(files, file => file.EndsWith(".razor", StringComparison.Ordinal));
        Assert.Contains(files, file => file.EndsWith(".cs", StringComparison.Ordinal));
        return files;
    }

    [Fact] // ADR-0062/0021: no .js file in the Wrapper's source
    public void The_Wrapper_source_holds_no_script_file()
        => Assert.DoesNotContain(SourceFiles(), file => Path.GetExtension(file) is ".js" or ".mjs" or ".cjs");

    [Fact] // ADR-0062/0021: no interop type named in the Wrapper's source
    public void The_Wrapper_source_names_no_interop_type()
    {
        var offenders = SourceFiles()
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, index, line)))
            .Where(entry => Interop.IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.index + 1} {entry.line.Trim()}")
            .ToList();

        Assert.Empty(offenders);
    }
}
