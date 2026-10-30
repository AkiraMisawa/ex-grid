using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>
/// The Packages page's list (ADR-0110, ADR-0042), read at build time: the family's packages by product,
/// in the order of ADR-0042's table; each one's description, read from its assembly's
/// <c>AssemblyDescriptionAttribute</c>, which is the text nuget.org shows; and the packages of the family
/// each one depends on, read from its project's <c>ProjectReference</c>s. The list is checked against the
/// ids the release workflow publishes, so that the page names exactly what a tag ships.
/// </summary>
public static class Packages
{
    /// <summary>The route of the site's Packages page.</summary>
    public const string Route = "getting-started/packages";

    /// <summary>The release workflow, whose <c>ids="…"</c> line names the packages a tag publishes.</summary>
    public const string ReleaseWorkflow = ".github/workflows/release.yml";

    /// <summary>The decision record whose table names the family's packages by product.</summary>
    public const string FamilyAdr = "0042";

    private const string Adr = "ADR-0042";

    /// <summary>The Packages page's list disagrees with the release, or cannot be read.</summary>
    public static readonly DiagnosticDescriptor ListDisagrees = new(
        id: "EXDOCS017",
        title: "The Packages page's list disagrees with what a tag publishes",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Packages page lists ADR-0042's table of the family's packages, which must be the ids release.yml publishes, in the same order; each package's description is read from its assembly, and its family dependencies from its project (ADR-0042, ADR-0110).");

    private static readonly Regex TableHeader = new(@"^\s*\|\s*Product\s*\|\s*Packages\s*\|\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex TableSeparator = new(@"^\s*\|(\s*:?-{3,}:?\s*\|){2}\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex CodeSpan = new(@"`([^`]+)`", RegexOptions.CultureInvariant);

    private static readonly Regex IdsLine = new(@"^\s*ids=""([^""]*)""\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The path from the repository's root of a file the Packages page reads — ADR-0042, the release
    /// workflow, or a shipped project's <c>src/&lt;Package&gt;/&lt;Package&gt;.csproj</c> — or
    /// <see langword="null"/> for any other file.
    /// </summary>
    /// <param name="path">The file's path on disk, with forward slashes.</param>
    public static string? RepoPathOf(string path)
    {
        if (path.EndsWith("/" + ReleaseWorkflow, StringComparison.Ordinal))
            return ReleaseWorkflow;
        var adr = path.LastIndexOf("/docs/adr/" + FamilyAdr + "-", StringComparison.Ordinal);
        if (adr >= 0 && path.EndsWith(".md", StringComparison.Ordinal))
        {
            var name = path.Substring(adr + "/docs/adr/".Length);
            return name.IndexOf('/') < 0 ? "docs/adr/" + name : null;
        }
        var src = path.LastIndexOf("/src/", StringComparison.Ordinal);
        if (src >= 0 && path.EndsWith(".csproj", StringComparison.Ordinal))
        {
            var parts = path.Substring(src + 1).Split('/');
            return parts.Length == 3 && parts[2] == parts[1] + ".csproj" ? string.Join("/", parts) : null;
        }
        return null;
    }

    /// <summary>A package, as the page lists it.</summary>
    internal sealed record Package(string Product, string Id, string Description, ImmutableArray<string> DependsOn);

    /// <summary>Reads, checks and emits the Packages page's list.</summary>
    /// <param name="files">ADR-0042, the release workflow and the shipped projects (<see cref="RepoPathOf"/>).</param>
    /// <param name="pages">The site's routed pages: a site with a Packages page needs every source.</param>
    /// <param name="descriptions">Each assembly the site references, by name, with its description;
    /// <see langword="null"/> where it carries none.</param>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<RecordInput> files,
        ImmutableArray<PageInput> pages,
        ImmutableDictionary<string, string?> descriptions)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var hasPage = pages.Any(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase));
        var packages = new List<Package>();

        void Report(Location location, string message)
        {
            if (hasPage)
                diagnostics.Add(Diagnostic.Create(ListDisagrees, location, message));
        }

        var table = ReadTable(files, Report);
        var ids = ReadIds(files, Report);
        if (table is not null && ids is not null)
        {
            var listed = table.Value.Rows.SelectMany(r => r.Ids).ToList();
            if (!listed.SequenceEqual(ids.Value.Ids, StringComparer.Ordinal))
            {
                Report(ids.Value.Location,
                    $"{ReleaseWorkflow} publishes {string.Join(" ", ids.Value.Ids)}, and {Adr}'s table of the family lists "
                    + $"{string.Join(" ", listed)}. The Packages page lists the table, and a tag publishes the ids: make them the same packages, "
                    + $"in the same order, so that the page names exactly what a tag ships ({Adr}).");
            }
            else
            {
                var family = new HashSet<string>(listed, StringComparer.Ordinal);
                foreach (var (product, rowIds, location) in table.Value.Rows)
                {
                    foreach (var id in rowIds)
                    {
                        var description = Describe(id, descriptions, location, Report);
                        var dependsOn = DependsOn(id, files, family, location, Report);
                        packages.Add(new Package(product, id, description, dependsOn));
                    }
                }
            }
        }
        var record = files.FirstOrDefault(f => f.RepoPath.StartsWith($"docs/adr/{FamilyAdr}-", StringComparison.Ordinal))?.RepoPath ?? "";
        return (diagnostics.ToImmutable(), Emit(packages, record));
    }

    /// <summary>
    /// The family's products, each with its packages, in ADR-0042's order — or <see langword="null"/> when
    /// the table or release.yml's ids cannot be read, or disagree. <see cref="Build"/> says why; the API
    /// reference (<see cref="ApiSurface"/>) lists the packages this returns.
    /// </summary>
    /// <param name="files">ADR-0042, the release workflow and the shipped projects (<see cref="RepoPathOf"/>).</param>
    internal static List<(string Product, List<string> Ids)>? Family(ImmutableArray<RecordInput> files)
    {
        var table = ReadTable(files, (_, _) => { });
        var ids = ReadIds(files, (_, _) => { });
        if (table is null || ids is null)
            return null;
        var rows = table.Value.Rows;
        return rows.SelectMany(r => r.Ids).SequenceEqual(ids.Value.Ids, StringComparer.Ordinal)
            ? rows.Select(r => (r.Product, r.Ids)).ToList()
            : null;
    }

    private static (List<(string Product, List<string> Ids, Location Location)> Rows, Location Location)? ReadTable(
        ImmutableArray<RecordInput> files, Action<Location, string> report)
    {
        var file = files.FirstOrDefault(f => f.RepoPath.StartsWith($"docs/adr/{FamilyAdr}-", StringComparison.Ordinal));
        if (file is null)
        {
            report(Location.None,
                $"The site has a Packages page (/{Route}), and {Adr}, whose table names the family's packages by product, did not reach the build. "
                + $"ExGrid.Docs.csproj hands docs/adr/*.md over as AdditionalFiles; restore that item ({Adr}).");
            return null;
        }
        var lines = file.Text.Lines;
        for (var k = 0; k < lines.Count; k++)
        {
            if (!TableHeader.IsMatch(lines[k].ToString()))
                continue;
            if (k + 1 >= lines.Count || !TableSeparator.IsMatch(lines[k + 1].ToString()))
            {
                report(At(file, lines[k].Start, lines[k].Span.Length),
                    $"{Adr}'s | Product | Packages | table has no |---|---| line under its header, so it is not a table. Restore it ({Adr}).");
                return null;
            }
            var rows = new List<(string, List<string>, Location)>();
            for (var r = k + 2; r < lines.Count; r++)
            {
                var raw = lines[r].ToString();
                if (!raw.TrimStart().StartsWith("|", StringComparison.Ordinal))
                    break;
                var cells = raw.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
                var location = At(file, lines[r].Start, lines[r].Span.Length);
                var rowIds = cells.Count == 2 ? CodeSpan.Matches(cells[1]).Cast<Match>().Select(m => m.Groups[1].Value).ToList() : [];
                if (cells.Count != 2 || cells[0].Length == 0 || rowIds.Count == 0)
                {
                    report(location,
                        $"This row of {Adr}'s table of the family is not | Product | `Package`, `Package` |, so the Packages page cannot place its packages ({Adr}).");
                    continue;
                }
                rows.Add((cells[0], rowIds, location));
            }
            return (rows, At(file, lines[k].Start, lines[k].Span.Length));
        }
        report(At(file, 0, 1),
            $"{Adr} has no table with the header | Product | Packages |, which the Packages page reads for the family's packages by product. "
            + $"Restore the header as written, or move the table with a decision that says where it now lives ({Adr}).");
        return null;
    }

    private static (List<string> Ids, Location Location)? ReadIds(ImmutableArray<RecordInput> files, Action<Location, string> report)
    {
        var file = files.FirstOrDefault(f => f.RepoPath == ReleaseWorkflow);
        if (file is null)
        {
            report(Location.None,
                $"The site has a Packages page (/{Route}), and {ReleaseWorkflow}, whose ids line names the packages a tag publishes, did not reach the build. "
                + $"ExGrid.Docs.csproj hands it over as an AdditionalFiles item; restore it ({Adr}).");
            return null;
        }
        var found = file.Text.Lines.Where(l => IdsLine.IsMatch(l.ToString())).ToList();
        if (found.Count != 1)
        {
            report(At(file, 0, 1),
                $"{ReleaseWorkflow} has {(found.Count == 0 ? "no" : found.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))} ids=\"…\" line"
                + $"{(found.Count == 0 ? "" : "s")}, and the Packages page checks its list against exactly one: the packages the release publishes ({Adr}).");
            return null;
        }
        var line = found[0];
        var ids = IdsLine.Match(line.ToString()).Groups[1].Value
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).ToList();
        return (ids, At(file, line.Start, line.Span.Length));
    }

    private static string Describe(string id, ImmutableDictionary<string, string?> descriptions, Location location, Action<Location, string> report)
    {
        if (!descriptions.TryGetValue(id, out var description))
        {
            report(location,
                $"The Docs Site does not reference the assembly {id}, so the Packages page cannot read its description. "
                + $"Add a ProjectReference to src/{id}/{id}.csproj in ExGrid.Docs.csproj ({Adr}).");
            return "";
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            report(location,
                $"{id}'s assembly carries no description, which is the text nuget.org shows and the Packages page reads. "
                + $"Give src/{id}/{id}.csproj a <Description> ({Adr}).");
            return "";
        }
        return description!.Trim();
    }

    private static ImmutableArray<string> DependsOn(
        string id, ImmutableArray<RecordInput> files, HashSet<string> family, Location location, Action<Location, string> report)
    {
        var path = $"src/{id}/{id}.csproj";
        var file = files.FirstOrDefault(f => f.RepoPath == path);
        if (file is null)
        {
            report(location,
                $"{path} did not reach the build, so the Packages page cannot say which of the family's packages {id} brings with it. "
                + $"ExGrid.Docs.csproj hands src/*/*.csproj over as AdditionalFiles; restore that item ({Adr}).");
            return ImmutableArray<string>.Empty;
        }
        XDocument project;
        try
        {
            project = XDocument.Parse(file.Text.ToString());
        }
        catch (XmlException e)
        {
            report(At(file, 0, 1), $"{path} cannot be read as XML: {e.Message}");
            return ImmutableArray<string>.Empty;
        }
        var dependsOn = ImmutableArray.CreateBuilder<string>();
        foreach (var reference in project.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
        {
            var include = ((string?)reference.Attribute("Include") ?? "").Replace('\\', '/');
            var name = include.Substring(include.LastIndexOf('/') + 1);
            if (name.EndsWith(".csproj", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - ".csproj".Length);
            if (!family.Contains(name))
            {
                report(At(file, 0, 1),
                    $"{path} references the project {include}, which is not one of the family's packages. A shipped package depends on the family "
                    + $"exactly, and on nothing else of the repository ({Adr}).");
                continue;
            }
            dependsOn.Add(name);
        }
        return dependsOn.ToImmutable();
    }

    private static SourceText Emit(List<Package> packages, string record)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from ADR-0042's table, release.yml's ids and each package's assembly and project (ADR-0042, ADR-0110).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class Packages");
        code.AppendLine("{");
        code.AppendLine("    /// <summary>ADR-0042, whose table the list follows, from the repository's root.</summary>");
        code.Append("    public const string FamilyRecordPath = ").Append(Literal(record)).AppendLine(";");
        code.AppendLine();
        code.AppendLine("    private static partial global::ExGrid.Docs.DocsPackage[] Read() =>");
        code.AppendLine("    [");
        foreach (var package in packages)
        {
            code.Append("        new(").Append(Literal(package.Product)).Append(", ").Append(Literal(package.Id)).Append(", ")
                .Append(Literal(package.Description)).Append(", [")
                .Append(string.Join(", ", package.DependsOn.Select(Literal))).AppendLine("]),");
        }
        code.AppendLine("    ];");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

    private static Location At(RecordInput file, int start, int length)
    {
        start = Math.Max(0, Math.Min(start, file.Text.Length));
        var end = Math.Min(start + Math.Max(length, 0), file.Text.Length);
        var span = TextSpan.FromBounds(start, end);
        return Location.Create(file.FullPath, span, file.Text.Lines.GetLinePositionSpan(span));
    }
}
