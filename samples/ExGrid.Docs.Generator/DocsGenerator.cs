using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>
/// The Docs Site's build step (ADR-0110). Two inputs reach it as additional files:
/// <list type="bullet">
/// <item>every <c>.razor</c>, <c>.cs</c>, <c>.html</c>, <c>.sh</c>, <c>.css</c>, <c>.ts</c> and <c>.tsx</c> under the site's <c>Examples/</c> folder, which it
/// emits as <c>ExampleSources</c>: each file's text and its highlighted HTML;</item>
/// <item>the shipped assemblies' XML documentation files, which it emits as <c>ApiDocs</c>: each
/// member's summary as HTML, keyed by its documentation id.</item>
/// </list>
/// Three build properties reach it too: <c>DocsVersion</c>, <c>DocsRef</c> and <c>DocsPublishedOn</c>,
/// which it reads together (<see cref="DocsBuildInfo"/>) and emits as <c>DocsBuild</c>, what the site
/// states it was built from. A summary's links into the repository point at that build's ref.
/// <para>
/// It also reads every routed <c>.razor</c> page (<see cref="PageReader"/>) and the navigation,
/// <c>DocsNav</c> (<see cref="NavReader"/>), and emits the <see cref="SiteMap"/>: each page's title,
/// sections and Examples. A navigation entry with no page, a page in no group, a title that is not text
/// and two sections with one anchor stop the build.
/// </para>
/// <para>
/// And it reads the Release Notes under <c>docs/releases/</c> (<see cref="ReleaseNotes"/>), checks them,
/// and emits them as <c>ReleaseNotes</c> for the Release notes page, the latest version on the start page
/// and the "New in" chips (ADR-0190).
/// </para>
/// <para>
/// And the Roadmap (<see cref="Roadmap"/>): the open tickets by their <c>Status:</c> and <c>Roadmap:</c>
/// lines, the scope tables of ADR-0046 and ADR-0059, and the rows <c>RoadmapByHand</c> writes, each
/// citing its record. It emits them as <c>Roadmap</c> for the Roadmap page and the search (ADR-0191).
/// </para>
/// <para>
/// And what the reference pages list (ADR-0110): the family's packages for the Packages page
/// (<see cref="Packages"/>), from ADR-0042's table checked against the ids <c>release.yml</c> publishes,
/// each package's description from its assembly and its family dependencies from its project; the
/// Feature matrix's rows and each Wrapper's Chrome seams (<see cref="FeatureMatrix"/>); the categories of
/// ExSheet's function catalogue (<see cref="FunctionCatalogue"/>); and the Keyboard reference's rows, each
/// linking a section the site map holds (<see cref="KeyboardReference"/>). A hand-written row of these
/// pages cites a record that exists, which <see cref="RecordCites"/> resolves.
/// </para>
/// <para>
/// And the API reference's list (<see cref="ApiSurface"/>): every public type of those packages, read from the
/// assemblies the site references, with what is public and not the contract apart (ADR-0201).
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DocsGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projectDir = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var dir) ? dir : "");

        // What the build was made from: the tag, or the branch and its commit, or nothing here.
        var build = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
        {
            string Property(string name) =>
                options.GlobalOptions.TryGetValue("build_property." + name, out var value) ? value : "";
            var info = DocsBuildInfo.Parse(Property("DocsVersion"), Property("DocsRef"), Property("DocsPublishedOn"), out var error);
            return new BuildInput(info ?? DocsBuildInfo.Local, error);
        });

        var examples = context.AdditionalTextsProvider
            .Combine(projectDir)
            .Select((pair, cancel) =>
            {
                var (file, dir) = pair;
                var relative = Relative(dir, file.Path);
                if (relative is null || !relative.StartsWith("Examples/", StringComparison.Ordinal))
                    return null;
                var extension = Path.GetExtension(relative);
                if (extension is not (".razor" or ".cs" or ".html" or ".sh" or ".css" or ".ts" or ".tsx"))
                    return null;
                var text = file.GetText(cancel)?.ToString().Replace("\r\n", "\n") ?? "";
                var html = extension switch
                {
                    ".razor" or ".html" => Highlighter.Razor(text),
                    ".cs" => Highlighter.CSharp(text),
                    ".css" => Highlighter.Css(text),
                    ".ts" => Highlighter.TypeScript(text, jsx: false),
                    ".tsx" => Highlighter.TypeScript(text, jsx: true),
                    _ => Highlighter.Shell(text),
                };
                return new Example(relative, extension.Substring(1), text, html);
            })
            .Where(example => example is not null)
            .Collect();

        // The decision records' file names, so a summary's "ADR-0016" links to the record. Only the files
        // under docs/adr/: a Change Note may begin with four digits too.
        var adrs = context.AdditionalTextsProvider
            .Select((file, _) => file.Path.Replace('\\', '/'))
            .Where(path => path.Contains(AdrFolder))
            .Select((path, _) => Path.GetFileName(path))
            .Where(name => AdrFile.IsMatch(name))
            .Collect()
            .Select((names, _) => names.Distinct().ToImmutableDictionary(n => n.Substring(0, 4), n => n));

        var gitRef = build.Select((input, _) => input.Info.GitRef);

        var docs = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Combine(adrs.Combine(gitRef))
            .Select((pair, cancel) => Summaries(pair.Left.GetText(cancel)?.ToString() ?? "", pair.Right.Left, pair.Right.Right))
            .Collect();

        context.RegisterSourceOutput(build, (spc, input) =>
        {
            if (input.Error is not null)
                spc.ReportDiagnostic(Diagnostic.Create(BuildInputsDisagree, Location.None, input.Error));
            spc.AddSource("DocsBuild.g.cs", EmitBuild(input.Info));
        });
        context.RegisterSourceOutput(examples, (spc, all) => spc.AddSource("ExampleSources.g.cs", EmitExamples(all!)));
        // Only what a Consumer can reach: the documentation files also describe internal members.
        var publicDocs = docs.Combine(context.CompilationProvider).Select((pair, _) =>
        {
            var (files, compilation) = pair;
            return files.Select(file => file.Where(entry => IsPublic(entry.Key, compilation)).ToImmutableArray()).ToImmutableArray();
        });

        context.RegisterSourceOutput(publicDocs, (spc, all) => spc.AddSource("ApiDocs.g.cs", EmitDocs(all)));

        // The site map: every routed page the site's own folders hold, read from its markup, and the
        // navigation read from DocsNav's C#. Each is checked against the other.
        var pages = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Combine(projectDir)
            .Select((pair, cancel) =>
            {
                var (file, dir) = pair;
                var relative = Relative(dir, file.Path);
                if (relative is null || relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal))
                    return null;
                var text = file.GetText(cancel);
                if (text is null)
                    return null;
                var scan = PageReader.Read(relative, text.ToString());
                return scan.Page.Routes.IsEmpty ? null : new PageInput(file.Path, text, scan);
            })
            .Where(page => page is not null)
            .Collect();
        var nav = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => NavReader.IsNav(node),
                (syntax, cancel) => NavReader.Read((ClassDeclarationSyntax)syntax.Node, syntax.SemanticModel, cancel))
            .Collect();
        context.RegisterSourceOutput(pages.Combine(nav), (spc, pair) =>
        {
            var (all, navs) = pair;
            var scans = all.Select(p => p!).ToImmutableArray();
            var declared = navs.IsEmpty
                ? null
                : new NavScan(navs.SelectMany(n => n.Links).ToImmutableArray(), navs.SelectMany(n => n.Unlisted).ToImmutableArray(), navs[0].Location);
            foreach (var diagnostic in SiteMap.Check(scans, declared))
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("SiteMap.g.cs", SiteMap.Emit(scans));
        });

        // The Release Notes (ADR-0190): every file under docs/releases/, read in the notes' Markdown subset
        // and checked against the decision records, the site map and the version this build publishes.
        var notes = context.AdditionalTextsProvider
            .Select((file, cancel) =>
            {
                var path = file.Path.Replace('\\', '/');
                var at = path.LastIndexOf(ReleasesFolder, StringComparison.Ordinal);
                if (at < 0 || !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    return null;
                var text = file.GetText(cancel);
                return text is null ? null : new NotesInput(file.Path, path.Substring(at + ReleasesFolder.Length), text);
            })
            .Where(file => file is not null)
            .Collect();
        // A release's SHOULD criteria accepted failing and its §21 items out of scope are read against the
        // Definition of Done (ADR-0190, ADR-0205).
        var definitionOfDone = context.AdditionalTextsProvider
            .Where(file => file.Path.Replace('\\', '/').EndsWith("/" + RecordCites.DefinitionOfDone, StringComparison.Ordinal))
            .Select((file, cancel) => file.GetText(cancel))
            .Collect()
            .Select((texts, _) => texts.FirstOrDefault(text => text is not null));
        context.RegisterSourceOutput(notes.Combine(pages).Combine(adrs).Combine(definitionOfDone).Combine(build), (spc, all) =>
        {
            var ((((files, scans), records), dod), input) = all;
            var (diagnostics, source) = ReleaseNotes.Build(
                files.Select(f => f!).ToImmutableArray(),
                scans.Select(p => p!).ToImmutableArray(),
                records,
                input.Info,
                dod);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("ReleaseNotes.g.cs", source);
        });

        // The Roadmap (ADR-0191): the tickets under docs/specs/*/issues/, the scope tables of ADR-0046 and
        // ADR-0059, and the rows the site writes by hand in RoadmapByHand, with the records those cite.
        var roadmapFiles = context.AdditionalTextsProvider
            .Select((file, cancel) =>
            {
                var path = file.Path.Replace('\\', '/');
                var repo = Roadmap.RepoPathOf(path);
                if (repo is null)
                    return null;
                var text = file.GetText(cancel);
                return text is null ? null : new RecordInput(file.Path, repo, text);
            })
            .Where(file => file is not null)
            .Collect();
        var hand = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => RoadmapHandReader.IsHand(node),
                (syntax, cancel) => RoadmapHandReader.Read((ClassDeclarationSyntax)syntax.Node, syntax.SemanticModel, cancel))
            .Collect();
        context.RegisterSourceOutput(roadmapFiles.Combine(hand).Combine(pages).Combine(adrs).Combine(build), (spc, all) =>
        {
            var ((((files, rows), scans), names), input) = all;
            var (diagnostics, source) = Roadmap.Build(
                files.Select(f => f!).ToImmutableArray(),
                rows.SelectMany(r => r).ToImmutableArray(),
                scans.Select(p => p!).ToImmutableArray(),
                names,
                input.Info);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("Roadmap.g.cs", source);
        });

        // The reference pages (ADR-0110). Packages: ADR-0042's table of the family, the ids release.yml
        // publishes, each package's project, and each one's description read from its assembly.
        // Functions: the catalogue's categories. Both are checked when the site has the page.
        var referenceFiles = context.AdditionalTextsProvider
            .Select((file, cancel) =>
            {
                var path = file.Path.Replace('\\', '/');
                var repo = Packages.RepoPathOf(path)
                    ?? (path.EndsWith("/" + FunctionCatalogue.Path, StringComparison.Ordinal) ? FunctionCatalogue.Path : null);
                if (repo is null)
                    return null;
                var text = file.GetText(cancel);
                return text is null ? null : new RecordInput(file.Path, repo, text);
            })
            .Where(file => file is not null)
            .Collect()
            .Select((files, _) => files.Select(f => f!).ToImmutableArray());
        var scannedPages = pages.Select((all, _) => all.Select(p => p!).ToImmutableArray());
        var descriptions = context.CompilationProvider.Select((compilation, _) => compilation.SourceModule.ReferencedAssemblySymbols
            .GroupBy(a => a.Name, StringComparer.Ordinal)
            .ToImmutableDictionary(g => g.Key, g => Description(g.First()), StringComparer.Ordinal));
        context.RegisterSourceOutput(referenceFiles.Combine(scannedPages).Combine(descriptions), (spc, all) =>
        {
            var ((files, scans), described) = all;
            var (diagnostics, source) = Packages.Build(files, scans, described);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("Packages.g.cs", source);
        });
        // The API reference (ADR-0201): every public type of the packages the Packages page lists, read from
        // the assemblies the site references, the contract apart from what is public and not the contract.
        context.RegisterSourceOutput(referenceFiles.Combine(scannedPages).Combine(adrs).Combine(context.CompilationProvider), (spc, all) =>
        {
            var (((files, scans), names), compilation) = all;
            var (diagnostics, source) = ApiSurface.Build(files, scans, compilation, names);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("ApiSurface.g.cs", source);
        });
        context.RegisterSourceOutput(referenceFiles.Combine(scannedPages), (spc, all) =>
        {
            var (diagnostics, source) = FunctionCatalogue.Build(all.Left, all.Right);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("FunctionCatalogue.g.cs", source);
        });

        // The Feature matrix: the rows FeatureMatrixByHand writes, and each Wrapper's Chrome read from the
        // compilation. The Keyboard reference: the rows KeyboardByHand writes, checked against the site map.
        var records = roadmapFiles.Select((files, _) => files.Select(f => f!).ToImmutableArray());
        var matrixHand = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => FeatureMatrix.IsHand(node),
                (syntax, cancel) => HandReader.Read((ClassDeclarationSyntax)syntax.Node, syntax.SemanticModel, FeatureMatrix.Types, cancel))
            .Collect()
            .Select((rows, _) => rows.SelectMany(r => r).ToImmutableArray());
        context.RegisterSourceOutput(matrixHand.Combine(records).Combine(adrs).Combine(scannedPages).Combine(context.CompilationProvider), (spc, all) =>
        {
            var ((((rows, files), names), scans), compilation) = all;
            var (diagnostics, source) = FeatureMatrix.Build(rows, files, names, scans, compilation);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("FeatureMatrix.g.cs", source);
        });
        var keyHand = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => KeyboardReference.IsHand(node),
                (syntax, cancel) => HandReader.Read((ClassDeclarationSyntax)syntax.Node, syntax.SemanticModel, KeyboardReference.Types, cancel))
            .Collect()
            .Select((rows, _) => rows.SelectMany(r => r).ToImmutableArray());
        context.RegisterSourceOutput(keyHand.Combine(records).Combine(adrs).Combine(scannedPages), (spc, all) =>
        {
            var (((rows, files), names), scans) = all;
            var (diagnostics, source) = KeyboardReference.Build(rows, files, names, scans);
            foreach (var diagnostic in diagnostics)
                spc.ReportDiagnostic(diagnostic);
            spc.AddSource("Keyboard.g.cs", source);
        });
    }

    // An assembly's description, which the SDK writes from the project's <Description>: what nuget.org shows.
    private static string? Description(IAssemblySymbol assembly)
    {
        var attribute = assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyDescriptionAttribute");
        return attribute is { ConstructorArguments.Length: 1 } && attribute.ConstructorArguments[0].Value is string text ? text : null;
    }

    private const string AdrFolder = "/docs/adr/";

    private const string ReleasesFolder = "/docs/releases/";

    // Whether the member a documentation id names is visible outside its assembly.
    private static bool IsPublic(string id, Compilation compilation)
    {
        var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation);
        for (var s = symbol; s is not null and not INamespaceSymbol; s = s.ContainingSymbol)
        {
            if (s.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                return false;
        }
        return symbol is not null;
    }

    private sealed record Example(string Path, string Language, string Text, string Html);

    private sealed record BuildInput(DocsBuildInfo Info, string? Error);

    /// <summary>The build's inputs disagree: the site would state one thing and link another.</summary>
    public static readonly DiagnosticDescriptor BuildInputsDisagree = new(
        id: "EXDOCS001",
        title: "The Docs Site's build inputs disagree",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "DocsVersion, DocsRef and DocsPublishedOn must describe one build: a tag and its version, a branch and its commit, or a local build (ADR-0110).");

    // The build's inputs as the site reads them. The kind is emitted with them, so that the site's
    // DocsBuildKind and the generator's cannot differ.
    private static SourceText EmitBuild(DocsBuildInfo info)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from DocsVersion, DocsRef and DocsPublishedOn (ADR-0110).");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("/// <summary>What a Docs Site build was made from (ADR-0110).</summary>");
        code.AppendLine("internal enum DocsBuildKind");
        code.AppendLine("{");
        foreach (var name in Enum.GetNames(typeof(DocsBuildKind)))
            code.Append("    ").Append(name).AppendLine(",");
        code.AppendLine("}");
        code.AppendLine();
        code.AppendLine("internal static partial class DocsBuild");
        code.AppendLine("{");
        code.Append("    public static DocsBuildKind Kind => DocsBuildKind.").Append(info.Kind).AppendLine(";");
        code.Append("    public static string Version => ").Append(Literal(info.Version)).AppendLine(";");
        code.Append("    public static string Branch => ").Append(Literal(info.Branch)).AppendLine(";");
        code.Append("    public static string Commit => ").Append(Literal(info.Commit)).AppendLine(";");
        code.Append("    public static string GitRef => ").Append(Literal(info.GitRef)).AppendLine(";");
        code.Append("    public static string PublishedOn => ").Append(Literal(info.PublishedOn)).AppendLine(";");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string? Relative(string dir, string path)
    {
        if (dir.Length == 0)
            return null;
        var full = path.Replace('\\', '/');
        var root = dir.Replace('\\', '/').TrimEnd('/') + "/";
        return full.StartsWith(root, StringComparison.Ordinal) ? full.Substring(root.Length) : null;
    }

    private static SourceText EmitExamples(ImmutableArray<Example> all)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from the files under Examples/ (ADR-0110).");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine("internal static partial class ExampleSources");
        code.AppendLine("{");
        code.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<string, ExampleSource> All = Build();");
        code.AppendLine("    private static global::System.Collections.Generic.Dictionary<string, ExampleSource> Build() => new()");
        code.AppendLine("    {");
        foreach (var example in all.OrderBy(e => e.Path, StringComparer.Ordinal))
        {
            code.Append("        [").Append(Literal(example.Path)).Append("] = new(")
                .Append(Literal(example.Path)).Append(", ")
                .Append(Literal(example.Language)).Append(", ")
                .Append(Literal(example.Text)).Append(", ")
                .Append(Literal(example.Html)).AppendLine("),");
        }
        code.AppendLine("    };");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static SourceText EmitDocs(ImmutableArray<ImmutableArray<KeyValuePair<string, string>>> files)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from the shipped assemblies' XML documentation (ADR-0110).");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine("internal static partial class ApiDocs");
        code.AppendLine("{");
        code.AppendLine("    private static readonly global::System.Collections.Generic.Dictionary<string, string> Summaries = Build();");
        code.AppendLine("    private static global::System.Collections.Generic.Dictionary<string, string> Build() => new()");
        code.AppendLine("    {");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in files.SelectMany(f => f).OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (seen.Add(entry.Key))
                code.Append("        [").Append(Literal(entry.Key)).Append("] = ").Append(Literal(entry.Value)).AppendLine(",");
        }
        code.AppendLine("    };");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    // Each member's summary, as HTML. A file that is not a documentation file yields nothing.
    private static readonly Regex AdrFile = new(@"^\d{4}-.+\.md$", RegexOptions.Compiled);
    private static readonly Regex AdrMention = new(@"\bADR-(\d{4})\b", RegexOptions.Compiled);

    // "ADR-0016" as a link to the record on GitHub, where the build knows its file: at the tag or
    // the commit the site was built from (ADR-0110), so a version's site cites the decisions as
    // they stood then.
    private static string LinkAdrs(string html, ImmutableDictionary<string, string> adrs, string gitRef) =>
        AdrMention.Replace(html, m => adrs.TryGetValue(m.Groups[1].Value, out var file)
            ? "<a href=\"https://github.com/AkiraMisawa/ex-grid/blob/" + gitRef + "/docs/adr/" + file + "\" target=\"_blank\" rel=\"noopener\">" + m.Value + "</a>"
            : m.Value);

    private static ImmutableArray<KeyValuePair<string, string>> Summaries(string xml, ImmutableDictionary<string, string> adrs, string gitRef)
    {
        if (xml.Length == 0)
            return ImmutableArray<KeyValuePair<string, string>>.Empty;
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            return ImmutableArray<KeyValuePair<string, string>>.Empty;
        }
        var members = document.Root?.Element("members");
        if (document.Root?.Name != "doc" || members is null)
            return ImmutableArray<KeyValuePair<string, string>>.Empty;
        var result = ImmutableArray.CreateBuilder<KeyValuePair<string, string>>();
        foreach (var member in members.Elements("member"))
        {
            var id = (string?)member.Attribute("name");
            var summary = member.Element("summary");
            if (id is null || summary is null)
                continue;
            var html = new StringBuilder();
            Render(summary, html);
            result.Add(new KeyValuePair<string, string>(id, LinkAdrs(Collapse(html.ToString()), adrs, gitRef)));
        }
        return result.ToImmutable();
    }

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private static string Collapse(string html) => Spaces.Replace(html, " ").Trim();

    private static void Render(XElement element, StringBuilder html)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    Highlighter.Text(html, text.Value);
                    break;
                case XElement child:
                    switch (child.Name.LocalName)
                    {
                        case "c":
                            html.Append("<code>");
                            Highlighter.Text(html, child.Value);
                            html.Append("</code>");
                            break;
                        case "see":
                        case "seealso":
                            html.Append("<code>");
                            Highlighter.Text(html, Reference(child));
                            html.Append("</code>");
                            break;
                        case "paramref":
                        case "typeparamref":
                            html.Append("<code>");
                            Highlighter.Text(html, (string?)child.Attribute("name") ?? "");
                            html.Append("</code>");
                            break;
                        case "para":
                            html.Append(" <br /> ");
                            Render(child, html);
                            break;
                        case "list":
                            html.Append("<ul>");
                            foreach (var item in child.Elements("item"))
                            {
                                html.Append("<li>");
                                Render(item, html);
                                html.Append("</li>");
                            }
                            html.Append("</ul>");
                            break;
                        default:
                            Render(child, html);
                            break;
                    }
                    break;
            }
        }
    }

    // What a <see> names, as a reader would write it: the member's own name, its type's for a
    // constructor, the keyword for a langword, or the element's text.
    private static string Reference(XElement see)
    {
        if (!string.IsNullOrWhiteSpace(see.Value))
            return see.Value;
        if ((string?)see.Attribute("langword") is { } word)
            return word;
        var cref = (string?)see.Attribute("cref") ?? (string?)see.Attribute("href") ?? "";
        var colon = cref.IndexOf(':');
        if (colon == 1)
            cref = cref.Substring(2);
        var paren = cref.IndexOf('(');
        if (paren >= 0)
            cref = cref.Substring(0, paren);
        var parts = cref.Split('.');
        var name = parts[parts.Length - 1];
        if (name == "#ctor" && parts.Length > 1)
            name = parts[parts.Length - 2];
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }
}
