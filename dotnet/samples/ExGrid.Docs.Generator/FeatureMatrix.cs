using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>
/// The Feature matrix's hand-written parts and its Chrome table (ADR-0110), read at build time from the
/// site's <c>FeatureMatrixByHand</c>: where the family runs, each row citing the record that decides it;
/// and which surfaces each Wrapper draws. The surfaces are read from the shipped assemblies: every seam
/// a Chrome interface declares, and whether the Wrapper's Chrome implements it or leaves it to the
/// built-in. A seam with no title on the page, a title for no seam, and a citation that leads nowhere
/// stop the build.
/// </summary>
public static class FeatureMatrix
{
    /// <summary>The route of the site's Feature matrix page.</summary>
    public const string Route = "features";

    /// <summary>The class the page's hand-written rows are declared in.</summary>
    public const string ClassName = "FeatureMatrixByHand";

    /// <summary>The records it constructs.</summary>
    public static readonly ImmutableHashSet<string> Types = ImmutableHashSet.Create("MatrixFact", "MatrixWrapper", "MatrixSurface");

    private const string Adr = "ADR-0110";

    /// <summary>A Feature matrix row cannot be read, cites nothing, or names no seam.</summary>
    public static readonly DiagnosticDescriptor RowWrong = new(
        id: "EXDOCS018",
        title: "A Feature matrix row cannot be read or cites nothing",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every hand-written row of the Feature matrix is plain text written as a literal and cites the record that decides it; every seam a Wrapper's Chrome interface declares has a title, and every title names a seam (ADR-0110).");

    /// <summary>Whether <paramref name="node"/> declares the page's hand-written rows.</summary>
    public static bool IsHand(SyntaxNode node) =>
        node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax { Identifier.ValueText: ClassName };

    /// <summary>A seam of a Wrapper's Chrome interface, and whether the Wrapper draws it.</summary>
    internal sealed record Seam(string Package, string Key, bool Filled);

    /// <summary>Reads, checks and emits the Feature matrix's seams and citations.</summary>
    /// <param name="hand">The rows <c>FeatureMatrixByHand</c> constructs.</param>
    /// <param name="files">The repository's files the build reads, the Definition of Done among them.</param>
    /// <param name="adrs">The decision records' file names, by number.</param>
    /// <param name="pages">The site's routed pages.</param>
    /// <param name="compilation">The site's compilation, which references the Wrappers.</param>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<HandRow> hand,
        ImmutableArray<RecordInput> files,
        ImmutableDictionary<string, string> adrs,
        ImmutableArray<PageInput> pages,
        Compilation compilation)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var records = new RecordCites(adrs, files, Adr);
        var cites = new SortedDictionary<string, RecordCite>(StringComparer.Ordinal);
        var seams = new List<Seam>();
        void Report(Location location, string message) => diagnostics.Add(Diagnostic.Create(RowWrong, location, message));

        var hasPage = pages.Any(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase));
        var wrappers = hand.Where(r => r.Type == "MatrixWrapper").ToList();
        if (hasPage && wrappers.Count == 0)
        {
            Report(Location.None,
                $"The site has a Feature matrix page (/{Route}), and no MatrixWrapper reached the build: the page's Chrome table names each Wrapper in "
                + $"{ClassName}. Restore the class, or the rows ({Adr}).");
        }

        foreach (var row in hand.Where(r => r.Type == "MatrixFact"))
        {
            Text(row.Text("Topic"), "topic");
            Text(row.Text("Text"), "text");
            Cites(row);
        }

        // Every seam each Wrapper's interface declares, keyed as the titles name them: IGridChrome.FilterPanel.
        var declared = new Dictionary<string, Location>(StringComparer.Ordinal);
        var unread = false;
        foreach (var row in wrappers)
        {
            var package = Text(row.Text("Package"), "package");
            var chromeName = Text(row.Text("Chrome"), "Chrome");
            var seamsName = Text(row.Text("Seams"), "interface");
            Cites(row);
            if (package is null || chromeName is null || seamsName is null)
            {
                unread = true;
                continue;
            }
            var chrome = compilation.GetTypeByMetadataName(chromeName);
            var seamsType = compilation.GetTypeByMetadataName(seamsName);
            if (chrome is null || seamsType is null)
            {
                Report(row.Location,
                    $"{(chrome is null ? chromeName : seamsName)} is not a type the Docs Site can see, so the Feature matrix cannot read which surfaces "
                    + $"{package} draws. Write the type's full name, and reference its package from ExGrid.Docs.csproj ({Adr}).");
                unread = true;
                continue;
            }
            if (seamsType.TypeKind != TypeKind.Interface || !chrome.AllInterfaces.Contains(seamsType, SymbolEqualityComparer.Default))
            {
                Report(row.Location, $"{chromeName} does not implement the interface {seamsName}, so it fills none of its seams ({Adr}).");
                unread = true;
                continue;
            }
            foreach (var member in seamsType.GetMembers().Where(IsSeam))
            {
                var key = seamsType.Name + "." + member.Name;
                if (!declared.ContainsKey(key))
                    declared[key] = row.Location;
                var implementation = chrome.FindImplementationForInterfaceMember(member);
                var filled = implementation is not null
                    && !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, seamsType);
                seams.Add(new Seam(package, key, filled));
            }
        }

        var titled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in hand.Where(r => r.Type == "MatrixSurface"))
        {
            var seam = Text(row.Text("Seam"), "seam");
            Text(row.Text("Title"), "title");
            Cites(row);
            if (seam is null)
                continue;
            titled.Add(seam);
            // A title is checked against the seams only when every Wrapper could be read: one that could not is
            // reported already, and its seams' titles would each look like a title for nothing.
            if (wrappers.Count > 0 && !unread && !declared.ContainsKey(seam))
            {
                Report(row.Text("Seam").Location,
                    $"'{seam}' names no seam of the Wrappers' Chrome interfaces: {string.Join(", ", declared.Keys)}. Correct it, or remove the row ({Adr}).");
            }
        }
        foreach (var missing in declared.Where(d => !titled.Contains(d.Key)))
        {
            Report(missing.Value,
                $"{missing.Key} is a seam a Wrapper can draw, and the Feature matrix has no title for it. Add a MatrixSurface to {ClassName}.Surfaces "
                + $"naming it, with what a reader calls it and the decision that made it ({Adr}).");
        }

        return (diagnostics.ToImmutable(), Emit(seams, cites.Values));

        string? Text(HandValue value, string part)
        {
            var text = value.Value;
            if (text is null || text.Trim().Length == 0)
            {
                Report(value.Location, $"This Feature matrix row's {part} is not written as text, so the build cannot check it. Write it as a string literal ({Adr}).");
                return null;
            }
            if (text.Contains('`') || text.Contains("**") || text.Contains('[') || text.Contains('<'))
            {
                Report(value.Location, $"This Feature matrix row's {part} is plain text, and this one holds Markdown or markup the page would show as it is. Write the words alone ({Adr}).");
                return null;
            }
            return text;
        }

        void Cites(HandRow row)
        {
            var list = row.List("Cites");
            if (list.IsEmpty)
            {
                Report(row.Location,
                    $"This Feature matrix row cites nothing. Every hand-written row cites the record that decides it: an ADR, as ADR-0017, or the "
                    + $"Definition of Done, as DoD §24 or DoD Step 7 ({Adr}).");
            }
            foreach (var cite in list)
            {
                if (cite.Value is null)
                {
                    Report(cite.Location, $"This citation is not written as text, so the build cannot check that its record exists. Write it as a string literal ({Adr}).");
                    continue;
                }
                if (records.Resolve(cite.Value, out var why) is { } resolved)
                    cites[cite.Value] = resolved;
                else
                    Report(cite.Location, why);
            }
        }
    }

    // A member a Chrome answers for a surface: an instance method or property, abstract or with a default.
    private static bool IsSeam(ISymbol member) =>
        !member.IsStatic && member is IMethodSymbol { MethodKind: MethodKind.Ordinary } or IPropertySymbol;

    private static SourceText Emit(List<Seam> seams, IEnumerable<RecordCite> cites)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from FeatureMatrixByHand and the Wrappers' Chrome (ADR-0110).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class FeatureMatrix");
        code.AppendLine("{");
        code.AppendLine("    private static partial global::ExGrid.Docs.MatrixSeam[] ReadSeams() =>");
        code.AppendLine("    [");
        foreach (var seam in seams)
        {
            code.Append("        new(").Append(Literal(seam.Package)).Append(", ").Append(Literal(seam.Key)).Append(", ")
                .Append(seam.Filled ? "true" : "false").AppendLine("),");
        }
        code.AppendLine("    ];");
        code.AppendLine();
        RecordCites.EmitReadCites(code, cites, "global::ExGrid.Docs.DocsCite");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);
}
