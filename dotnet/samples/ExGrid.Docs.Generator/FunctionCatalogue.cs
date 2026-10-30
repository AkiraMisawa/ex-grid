using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>
/// ExSheet's function catalogue as the Functions page groups it (ADR-0110, ADR-0047): each <c>###</c>
/// category under the catalogue's <c>## Catalogue</c> heading, with each function's name and status, read
/// from <c>docs/specs/exsheet-functions/spec.md</c> at build time. The page takes each supported function's
/// signature and description from the engine's own list, <c>DeclaredFunction.All</c>; the catalogue says
/// only where each one belongs, and which are not supported yet.
/// </summary>
public static class FunctionCatalogue
{
    /// <summary>The route of the site's Functions page.</summary>
    public const string Route = "exsheet/functions";

    /// <summary>The catalogue, from the repository's root.</summary>
    public const string Path = "docs/specs/exsheet-functions/spec.md";

    /// <summary>The statuses a catalogue row carries (the catalogue's own Status table).</summary>
    public static readonly ImmutableArray<string> Statuses = ["Supported", "Ready", "Observe", "Decide", "Blocked"];

    private const string Adr = "ADR-0047";

    /// <summary>The catalogue cannot be read for the Functions page.</summary>
    public static readonly DiagnosticDescriptor Unreadable = new(
        id: "EXDOCS019",
        title: "ExSheet's function catalogue cannot be read for the Functions page",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Functions page groups the engine's functions by the ### categories under the catalogue's ## Catalogue heading, each row | `NAME` | Status | Priority | Notes |, with one of the catalogue's statuses, and each function once (ADR-0047, ADR-0110).");

    private static readonly Regex Row = new(@"^\|\s*`([A-Z][A-Z0-9.]*)`\s*\|\s*([A-Za-z]+)\s*\|", RegexOptions.CultureInvariant);

    /// <summary>A category of the catalogue, with its functions in the catalogue's order.</summary>
    internal sealed record Category(string Name, List<(string Name, string Status)> Functions, Location Location);

    /// <summary>Reads, checks and emits the catalogue's categories.</summary>
    /// <param name="files">The files the reference pages read; the catalogue among them.</param>
    /// <param name="pages">The site's routed pages: a site with a Functions page needs the catalogue.</param>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(ImmutableArray<RecordInput> files, ImmutableArray<PageInput> pages)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var hasPage = pages.Any(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase));
        void Report(Location location, string message)
        {
            if (hasPage)
                diagnostics.Add(Diagnostic.Create(Unreadable, location, message));
        }

        var categories = new List<Category>();
        var file = files.FirstOrDefault(f => f.RepoPath == Path);
        if (file is null)
        {
            Report(Location.None,
                $"The site has a Functions page (/{Route}), and {Path}, whose categories group its functions, did not reach the build. "
                + $"ExGrid.Docs.csproj hands it over as an AdditionalFiles item; restore it ({Adr}).");
            return (diagnostics.ToImmutable(), Emit(categories));
        }

        var lines = file.Text.Lines;
        var inCatalogue = false;
        var fenced = false;
        Category? current = null;
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var raw = line.ToString();
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }
            if (fenced)
                continue;
            if (raw.StartsWith("## ", StringComparison.Ordinal))
            {
                inCatalogue = raw.Substring(3).Trim() == "Catalogue";
                current = null;
                continue;
            }
            if (!inCatalogue)
                continue;
            if (raw.StartsWith("### ", StringComparison.Ordinal))
            {
                current = new Category(raw.Substring(4).Trim(), [], At(file, line.Start, line.Span.Length));
                categories.Add(current);
                continue;
            }
            if (Row.Match(raw) is not { Success: true } row)
                continue;
            var name = row.Groups[1].Value;
            var status = row.Groups[2].Value;
            var location = At(file, line.Start, line.Span.Length);
            if (current is null)
            {
                Report(location, $"The function {name} is catalogued before any ### category, so the Functions page cannot place it. Put it under its category ({Adr}).");
                continue;
            }
            if (!Statuses.Contains(status))
            {
                Report(location,
                    $"The function {name} has the status '{status}', which is not one of the catalogue's: {string.Join(", ", Statuses)} ({Adr}).");
                continue;
            }
            if (seen.TryGetValue(name, out var first))
            {
                Report(location, $"The function {name} is catalogued twice, here and under {first}. A function has one row, in one category ({Adr}).");
                continue;
            }
            seen[name] = current.Name;
            current.Functions.Add((name, status));
        }

        if (categories.Count == 0)
        {
            Report(At(file, 0, 1),
                $"{Path} has no ### category under a ## Catalogue heading, so the Functions page cannot group the functions. Restore the heading as written ({Adr}).");
        }
        foreach (var empty in categories.Where(c => c.Functions.Count == 0))
            Report(empty.Location, $"The category '{empty.Name}' catalogues no function, so the Functions page would show it empty. Catalogue its functions, or remove it ({Adr}).");

        return (diagnostics.ToImmutable(), Emit(categories.Where(c => c.Functions.Count > 0).ToList()));
    }

    private static SourceText Emit(List<Category> categories)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from docs/specs/exsheet-functions/spec.md (ADR-0047, ADR-0110).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class FunctionCatalogue");
        code.AppendLine("{");
        code.AppendLine("    private static partial global::ExGrid.Docs.FunctionCategory[] Read() =>");
        code.AppendLine("    [");
        foreach (var category in categories)
        {
            code.Append("        new(").Append(Literal(category.Name)).Append(", ").Append(Literal(DocsAnchor.Of(category.Name))).AppendLine(",");
            code.Append("            [")
                .Append(string.Join(", ", category.Functions.Select(f => "new(" + Literal(f.Name) + ", " + Literal(f.Status) + ")")))
                .AppendLine("]),");
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
