using System.ComponentModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Docs.Generator.Tests;

/// <summary>
/// The API reference's list as the compiler reads it (ADR-0201, ADR-0110): every public type of the family's
/// packages, read from the assemblies the site references, and which are the contract. A type marked
/// <c>[EditorBrowsable(Never)]</c>, and every type nested in one, is public and not the contract; so is a
/// member of a contract type that is so marked or that the grid's own script calls (<c>[JSInvokable]</c>).
/// </summary>
public class ApiSurfaceTests
{
    private const string Repo = "/repo/";

    private const string Page = """
        @page "/reference/api"
        <DocsPage Title="API reference" Lead="Every public type.">
            <DocsSection Title="Grid"></DocsSection>
            <DocsSection Title="Data"></DocsSection>
            <DocsSection Title="Not the contract"></DocsSection>
        </DocsPage>
        """;

    private const string Family = """
        # Prereleases ship before sign-off

        | Product | Packages |
        |---|---|
        | Grid | `Alpha` |
        | Data | `Beta` |
        """;

    private const string Release = """
        jobs:
          publish:
            steps:
              - run: |
                  ids="Alpha Beta"
        """;

    // The seams a product's surface is read by: a component, the editor's hiding, and the script's calls.
    private const string Seams = """
        namespace Microsoft.AspNetCore.Components { public interface IComponent { } }
        namespace Microsoft.JSInterop { public sealed class JSInvokableAttribute : System.Attribute { } }
        """;

    private const string Alpha = Seams + """
        namespace Alpha
        {
            using System.ComponentModel;

            /// <summary>A grid.</summary>
            public class Grid<TRow> : Microsoft.AspNetCore.Components.IComponent
            {
                public int Rows { get; set; }
                [Microsoft.JSInterop.JSInvokable] public void OnKey(string key, bool ctrl) { }
                [EditorBrowsable(EditorBrowsableState.Never)] public int Painted { get; set; }
                [EditorBrowsable(EditorBrowsableState.Advanced)] public int Advanced { get; set; }
            }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public class GridRow<TRow> : Microsoft.AspNetCore.Components.IComponent
            {
                public class Cell { }
                [Microsoft.JSInterop.JSInvokable] public void Ping() { }
            }

            public sealed record Payload(string Kind);
            public readonly record struct Point(int Row, int Column);
            public enum Mode { Off, On }
            public interface IChrome { }
            public static class Rules { }
            public delegate void Changed();
            public abstract class Source { }
            public class Outer<T> { public struct Inner { } }
            public class @event { }
            internal sealed class Machinery { }
        }
        namespace Alpha.Keys
        {
            public struct Key { }
        }
        """;

    private const string Beta = """
        namespace Beta
        {
            public sealed class Snapshot { }
        }
        """;

    private static (string, string)[] Fixture(params (string, string)[] replace)
    {
        var files = new Dictionary<string, string>
        {
            [$"{GeneratorHarness.SiteDir}/Pages/ApiReferencePage.razor"] = Page,
            [Repo + "docs/adr/0042-prereleases-ship-before-sign-off.md"] = Family,
            [Repo + "docs/adr/0201-the-public-surface.md"] = "# The public surface\n",
            [Repo + ".github/workflows/release.yml"] = Release,
        };
        foreach (var (path, text) in replace)
        {
            if (text.Length == 0)
                files.Remove(path);
            else
                files[path] = text;
        }
        return files.Select(f => (f.Key, f.Value)).ToArray();
    }

    private static MetadataReference[] Assemblies() =>
    [
        GeneratorHarness.Assembly("Alpha", "A grid.", Alpha),
        GeneratorHarness.Assembly("Beta", "The data.", Beta),
    ];

    private static GeneratorRun Run(IEnumerable<(string, string)> files, IEnumerable<MetadataReference>? references = null) =>
        GeneratorHarness.Run([], files, references: references ?? Assemblies());

    private static string Source(GeneratorRun run) => run.Source("ApiSurface.g.cs").Replace("\r\n", "\n");

    private static Diagnostic One(GeneratorRun run)
    {
        var error = Assert.Single(run.Diagnostics.Where(d => d.Id == "EXDOCS021"));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        return error;
    }

    [Fact] // ADR-0201: every public type is listed, the contract marked true, in the family's order, then by namespace and name
    public void Every_public_type_is_listed_with_whether_it_is_the_contract()
    {
        var run = Run(Fixture());

        Assert.Empty(run.Diagnostics);
        var source = Source(run);
        Assert.Contains("""new("Alpha", "Alpha", "Grid<TRow>", "Alpha.Grid<>", "T:Alpha.Grid`1", "component", true, typeof(global::Alpha.Grid<>)),""", source);
        Assert.Contains("""new("Alpha", "Alpha", "Payload", "Alpha.Payload", "T:Alpha.Payload", "record", true, typeof(global::Alpha.Payload)),""", source);
        Assert.Contains("""new("Alpha", "Alpha.Keys", "Key", "Alpha.Keys.Key", "T:Alpha.Keys.Key", "struct", true, typeof(global::Alpha.Keys.Key)),""", source);
        Assert.Contains("""new("Beta", "Beta", "Snapshot", "Beta.Snapshot", "T:Beta.Snapshot", "class", true, typeof(global::Beta.Snapshot)),""", source);
        Assert.True(source.IndexOf("\"Alpha.Keys\"", StringComparison.Ordinal) < source.IndexOf("new(\"Beta\"", StringComparison.Ordinal));
        Assert.True(source.IndexOf("\"Alpha.Grid<>\"", StringComparison.Ordinal) < source.IndexOf("\"Alpha.Keys.Key\"", StringComparison.Ordinal));
    }

    [Fact] // ADR-0201: an internal type is not part of the surface, so the reference never lists it
    public void An_internal_type_is_not_listed()
    {
        Assert.DoesNotContain("Machinery", Source(Run(Fixture())));
    }

    [Fact] // ADR-0201: a type hidden from a Consumer's editor is public and not the contract, and so is every type nested in it
    public void A_hidden_type_and_what_it_holds_are_not_the_contract()
    {
        var source = Source(Run(Fixture()));

        Assert.Contains("""new("Alpha", "Alpha", "GridRow<TRow>", "Alpha.GridRow<>", "T:Alpha.GridRow`1", "component", false, typeof(global::Alpha.GridRow<>)),""", source);
        Assert.Contains("""new("Alpha", "Alpha", "GridRow<TRow>.Cell", "Alpha.GridRow<>.Cell", "T:Alpha.GridRow`1.Cell", "class", false, typeof(global::Alpha.GridRow<>.Cell)),""", source);
        // A hidden type is not the contract whole: its own members are not listed apart.
        Assert.DoesNotContain("Ping", source);
    }

    [Fact] // ADR-0201: on a contract type, a member the script calls or one hidden is listed apart; Advanced is not hidden
    public void A_contract_types_interop_and_hidden_members_are_listed_apart()
    {
        var source = Source(Run(Fixture()));

        Assert.Contains("""new("Alpha", "T:Alpha.Grid`1", "Grid<TRow>", "OnKey(string, bool)", "M:Alpha.Grid`1.OnKey(System.String,System.Boolean)", true),""", source);
        Assert.Contains("""new("Alpha", "T:Alpha.Grid`1", "Grid<TRow>", "Painted", "P:Alpha.Grid`1.Painted", false),""", source);
        Assert.DoesNotContain("\"Advanced\"", source);
        Assert.DoesNotContain("\"Rows\"", source);
    }

    [Theory] // ADR-0110: each type says what it is
    [InlineData("Point", "record struct")]
    [InlineData("Mode", "enum")]
    [InlineData("IChrome", "interface")]
    [InlineData("Rules", "static class")]
    [InlineData("Changed", "delegate")]
    [InlineData("Source", "abstract class")]
    public void Each_type_says_what_it_is(string name, string kind)
    {
        Assert.Contains($"\"T:Alpha.{name}\", \"{kind}\", true", Source(Run(Fixture())));
    }

    [Fact] // ADR-0110: a type is named for typeof with its type parameters left out, nested and escaped as C# writes it
    public void A_type_is_named_as_typeof_takes_it()
    {
        var source = Source(Run(Fixture()));

        Assert.Contains("typeof(global::Alpha.Outer<>.Inner)", source);
        Assert.Contains("typeof(global::Alpha.@event)", source);
        Assert.Contains("\"Outer<T>.Inner\", \"Alpha.Outer<>.Inner\"", source);
    }

    [Fact] // ADR-0201: the page cites the decision at its file
    public void The_decision_is_cited_at_its_file()
    {
        Assert.Contains("public const string RecordPath = \"docs/adr/0201-the-public-surface.md\";", Source(Run(Fixture())));
    }

    [Fact] // ADR-0110: a product of ADR-0042's table with no section on the page stops the build, at the page's route
    public void A_product_without_a_section_stops_the_build()
    {
        var run = Run(Fixture(($"{GeneratorHarness.SiteDir}/Pages/ApiReferencePage.razor", Page.Replace("<DocsSection Title=\"Data\"></DocsSection>", ""))));

        var error = One(run);
        Assert.EndsWith("ApiReferencePage.razor", error.Location.GetLineSpan().Path);
        Assert.Contains("no section titled \"Data\"", error.GetMessage());
    }

    [Fact] // ADR-0110: a package the site does not reference cannot be listed, and stops the build
    public void A_package_the_site_does_not_reference_stops_the_build()
    {
        var run = Run(Fixture(), [GeneratorHarness.Assembly("Alpha", "A grid.", Alpha)]);

        Assert.Contains("does not reference the assembly Beta", One(run).GetMessage());
    }

    [Fact] // ADR-0110: without ADR-0042's table the family's packages cannot be named, and the page says where to look
    public void A_family_that_cannot_be_read_stops_the_build()
    {
        var run = Run(Fixture((Repo + ".github/workflows/release.yml", "")));

        Assert.Contains("could not read them", One(run).GetMessage());
    }

    [Fact] // ADR-0201: without the decision record the page could not cite it, and stops the build
    public void A_missing_decision_stops_the_build()
    {
        var run = Run(Fixture((Repo + "docs/adr/0201-the-public-surface.md", "")));

        Assert.Contains("no docs/adr/0201-*.md reached the build", One(run).GetMessage());
    }

    [Fact] // ADR-0110: a site without the API reference is not held to its sources
    public void A_site_without_the_page_is_not_checked()
    {
        var run = Run(Fixture(($"{GeneratorHarness.SiteDir}/Pages/ApiReferencePage.razor", ""), (Repo + ".github/workflows/release.yml", "")));

        Assert.Empty(run.Diagnostics);
    }

    // The real assemblies, as the Docs Site reads them: every assembly this test runs with, so that a
    // component's base and an attribute's class resolve as they do in the site's compilation.
    private static readonly string[] Shipped = ["ExGrid", "ExGrid.Data", "ExSheet.Engine", "ExSheet", "ExPivot.Engine", "ExPivot"];

    private static GeneratorRun RunReal()
    {
        var platform = new HashSet<string>(["System.Private.CoreLib.dll", "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll"]);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            .Where(p => !platform.Contains(Path.GetFileName(p)))
            .GroupBy(Path.GetFileName)
            .Select(g => g.First());
        var family = "| Product | Packages |\n|---|---|\n" + string.Concat(Shipped.Select(id => $"| {id} | `{id}` |\n"));
        var release = $"      ids=\"{string.Join(" ", Shipped)}\"\n";
        return Run(
            [(Repo + "docs/adr/0042-prereleases-ship-before-sign-off.md", family), (Repo + ".github/workflows/release.yml", release)],
            paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToArray());
    }

    private static readonly Lazy<string> Real = new(() => Source(RunReal()));

    private static IEnumerable<Assembly> ShippedAssemblies() =>
        Shipped.Select(id => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == id) ?? Assembly.Load(id));

    [Fact] // ADR-0201: every type the shipped assemblies export is listed, and nothing else
    public void Every_exported_type_of_the_real_packages_is_listed()
    {
        var source = Real.Value;
        var listed = source.Split('\n').Count(l => l.Contains(", typeof(global::", StringComparison.Ordinal));
        var exported = ShippedAssemblies().SelectMany(a => a.GetExportedTypes()).ToList();

        Assert.Equal(exported.Count, listed);
        foreach (var type in exported)
            Assert.Contains($"\"T:{type.FullName!.Replace('+', '.')}\"", source);
    }

    [Fact] // ADR-0201: a type the real packages hide is not the contract, and the grid itself is
    public void The_real_packages_hidden_types_are_not_the_contract()
    {
        var source = Real.Value;
        var exported = ShippedAssemblies().SelectMany(a => a.GetExportedTypes()).ToList();
        Assert.NotEmpty(exported);
        // None of these packages hides a type since the grid drawn in .NET and its parts were removed (ADR-0370); the
        // Wrappers' hidden slot parts are not read here. One hidden again is listed as not the contract.
        foreach (var type in exported.Where(t => t.GetCustomAttribute<EditorBrowsableAttribute>() is { State: EditorBrowsableState.Never }))
            Assert.Contains(", false, typeof(", LineOf(source, "\"T:" + type.FullName!.Replace('+', '.') + "\""));
        Assert.Contains("\"T:ExGrid.Components.ExGrid`1\", \"component\", true", source);
    }

    [Fact] // ADR-0201 / ADR-0350 section 7: the grid's public surface carries no method its script calls: the browser calls a bridge of its own
    public void The_grid_carries_no_interop_method()
    {
        Assert.DoesNotContain(typeof(global::ExGrid.Components.ExGrid<>).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            m => m.GetCustomAttribute<JSInvokableAttribute>() is not null);
        Assert.DoesNotContain("\"M:ExGrid.Components.ExGrid`1.Heard", Real.Value);
    }

    // The one emitted line that holds <paramref name="text"/>.
    private static string LineOf(string source, string text) =>
        Assert.Single(source.Split('\n'), l => l.Contains(text, StringComparison.Ordinal)).TrimEnd();
}
