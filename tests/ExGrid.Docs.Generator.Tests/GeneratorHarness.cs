using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>What one run of the generator reported and emitted.</summary>
internal sealed record GeneratorRun(ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<GeneratedSourceResult> Sources)
{
    public string Source(string hintName) =>
        Sources.Single(s => s.HintName == hintName).SourceText.ToString();
}

/// <summary>
/// Runs the Docs Site's generator as the compiler does: over C# sources, additional files under a
/// site folder, and the build's properties.
/// </summary>
internal static class GeneratorHarness
{
    /// <summary>The site's folder, as the build passes MSBuildProjectDirectory.</summary>
    public const string SiteDir = "/repo/samples/ExGrid.Docs";

    /// <summary>The platform's assemblies a test compilation references.</summary>
    public static readonly ImmutableArray<MetadataReference> Platform =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Private.CoreLib.dll" or "System.Runtime.dll" or "System.Collections.dll" or "System.Linq.dll")
            .Select(p => MetadataReference.CreateFromFile(p)),
    ];

    public static GeneratorRun Run(
        IEnumerable<string> sources,
        IEnumerable<(string Path, string Text)> files,
        string version = "dev",
        string docsRef = "",
        string publishedOn = "",
        IEnumerable<MetadataReference>? references = null)
    {
        var compilation = CSharpCompilation.Create(
            "Docs",
            sources.Select((s, k) => CSharpSyntaxTree.ParseText(s, path: $"{SiteDir}/Source{k}.cs")),
            [.. Platform, .. references ?? []],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new Options(new Dictionary<string, string>
        {
            ["build_property.MSBuildProjectDirectory"] = SiteDir,
            ["build_property.DocsVersion"] = version,
            ["build_property.DocsRef"] = docsRef,
            ["build_property.DocsPublishedOn"] = publishedOn,
        });
        var driver = CSharpGeneratorDriver.Create(
            [new DocsGenerator().AsSourceGenerator()],
            additionalTexts: files.Select(f => (AdditionalText)new InMemoryFile(f.Path, f.Text)),
            optionsProvider: options);
        var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
        return new GeneratorRun(result.Diagnostics, result.GeneratedSources);
    }

    /// <summary>
    /// An assembly the site references, compiled from <paramref name="source"/>, as a shipped package is: its
    /// name and, unless <paramref name="description"/> is null, an <c>AssemblyDescriptionAttribute</c>.
    /// </summary>
    public static MetadataReference Assembly(string name, string? description, string source = "")
    {
        var attribute = description is null
            ? ""
            : $"[assembly: System.Reflection.AssemblyDescription({SymbolDisplay.FormatLiteral(description, quote: true)})]\n";
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(attribute + source)],
            Platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.ToMetadataReference();
    }

    private sealed class InMemoryFile(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class Options(Dictionary<string, string> global) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(global);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Values.None;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Values.None;
    }

    private sealed class Values(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public static Values None { get; } = new([]);

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
