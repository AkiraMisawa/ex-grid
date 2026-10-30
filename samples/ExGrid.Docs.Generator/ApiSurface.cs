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
/// The API reference's list (ADR-0201, ADR-0110), read at build time from the shipped assemblies the site
/// references: every public type of each package of the family, in ADR-0042's order, and which of them are
/// the contract. A type marked <c>[EditorBrowsable(EditorBrowsableState.Never)]</c> — a Razor component a
/// Consumer does not place, a type only the grid's own script names — is public and not the contract, and so
/// is every type nested in one. On a type that is the contract, a member so marked, or one the grid's own
/// script calls (<c>[JSInvokable]</c>), is public and not the contract. The site lists the two apart, and
/// says of the second that it may change in any version.
/// </summary>
public static class ApiSurface
{
    /// <summary>The route of the site's API reference.</summary>
    public const string Route = "reference/api";

    private const string SurfaceAdr = "0201";

    private const string Adr = "ADR-" + SurfaceAdr;

    /// <summary>The API reference cannot list the family's packages, or has no section for one of its products.</summary>
    public static readonly DiagnosticDescriptor CannotList = new(
        id: "EXDOCS021",
        title: "The API reference cannot list the family's packages",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The API reference lists every public type of the packages ADR-0042's table names and release.yml publishes, read from the assemblies the site references, under a section for each product of the table (ADR-0201, ADR-0110).");

    /// <summary>A public type of a package, as the API reference lists it.</summary>
    /// <param name="Package">The package, which is its assembly's name.</param>
    /// <param name="Namespace">Its namespace.</param>
    /// <param name="Name">How a reader writes it: <c>ExGrid&lt;TRow&gt;</c>, <c>Outer.Inner</c>.</param>
    /// <param name="Unbound">Its full name with its type parameters left out, as a page's <c>typeof</c> names it: <c>ExGrid.Script.ExGrid&lt;&gt;</c>.</param>
    /// <param name="Id">Its documentation id: <c>T:ExGrid.Script.ExGrid`1</c>.</param>
    /// <param name="Kind">What it is: <c>component</c>, <c>record</c>, <c>enum</c>, …</param>
    /// <param name="Contract">Whether it is the contract, rather than public and not the contract.</param>
    /// <param name="TypeOf">The C# that names it in a <c>typeof</c>, from the global namespace.</param>
    internal sealed record SurfaceType(
        string Package, string Namespace, string Name, string Unbound, string Id, string Kind, bool Contract, string TypeOf);

    /// <summary>A member of a contract type that is public and not the contract.</summary>
    /// <param name="Package">The package.</param>
    /// <param name="TypeId">Its type's documentation id.</param>
    /// <param name="Type">Its type, as a reader writes it.</param>
    /// <param name="Name">The member, as a reader writes it: <c>OnKeyAsync(string, bool)</c>.</param>
    /// <param name="Id">Its documentation id.</param>
    /// <param name="Interop">Whether the grid's own script calls it (<c>[JSInvokable]</c>), rather than it being hidden.</param>
    internal sealed record SurfaceMember(string Package, string TypeId, string Type, string Name, string Id, bool Interop);

    private static readonly SymbolDisplayFormat TypeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat MemberFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>Reads, checks and emits the API reference's list.</summary>
    /// <param name="files">ADR-0042 and the release workflow, which name the family's packages (<see cref="Packages.RepoPathOf"/>).</param>
    /// <param name="pages">The site's routed pages: a site with an API reference needs every package, and a section per product.</param>
    /// <param name="compilation">The site's compilation, which references the shipped assemblies.</param>
    /// <param name="adrs">The decision records' file names, by number: the page cites ADR-0201.</param>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<RecordInput> files,
        ImmutableArray<PageInput> pages,
        Compilation compilation,
        ImmutableDictionary<string, string> adrs)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var page = pages.FirstOrDefault(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase));
        var types = new List<SurfaceType>();
        var members = new List<SurfaceMember>();

        void Report(Location location, string message)
        {
            if (page is not null)
                diagnostics.Add(Diagnostic.Create(CannotList, location, message));
        }

        if (!adrs.TryGetValue(SurfaceAdr, out var record))
        {
            record = "";
            Report(PageLocation(page),
                $"The API reference cites {Adr}, which decides what is public and what is the contract, and no docs/adr/{SurfaceAdr}-*.md reached the build. "
                + "ExGrid.Docs.csproj hands docs/adr/*.md over as AdditionalFiles; restore that item, or the record.");
        }

        var family = Packages.Family(files);
        if (family is null)
        {
            Report(PageLocation(page),
                $"The API reference lists the packages ADR-0042's table names and {Packages.ReleaseWorkflow} publishes, and could not read them. "
                + $"The Packages page's error says why; fix that, and this page lists them ({Adr}).");
        }
        else
        {
            if (page is not null)
            {
                foreach (var (product, _) in family)
                {
                    if (!page.Scan.Page.Sections.Any(s => s.Title == product))
                    {
                        Report(PageLocation(page),
                            $"The API reference has no section titled \"{product}\", the product ADR-0042's table lists, so that product's packages "
                            + $"would not be listed. Add <DocsSection Title=\"{product}\"> holding its packages ({Adr}).");
                    }
                }
            }
            var assemblies = compilation.SourceModule.ReferencedAssemblySymbols
                .GroupBy(a => a.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var id in family.SelectMany(f => f.Ids))
            {
                if (!assemblies.TryGetValue(id, out var assembly))
                {
                    Report(PageLocation(page),
                        $"The Docs Site does not reference the assembly {id}, so the API reference cannot list its types. "
                        + $"Add a ProjectReference to src/{id}/{id}.csproj in ExGrid.Docs.csproj ({Adr}).");
                    continue;
                }
                var (packageTypes, packageMembers) = Read(assembly, id);
                types.AddRange(packageTypes);
                members.AddRange(packageMembers);
            }
        }
        return (diagnostics.ToImmutable(), Emit(types, members, record.Length == 0 ? "" : "docs/adr/" + record));
    }

    /// <summary>Every public type of <paramref name="assembly"/>, and the members of its contract types that are not the contract.</summary>
    /// <param name="assembly">A shipped assembly.</param>
    /// <param name="package">Its package id.</param>
    internal static (List<SurfaceType> Types, List<SurfaceMember> Members) Read(IAssemblySymbol assembly, string package)
    {
        var types = new List<SurfaceType>();
        var members = new List<SurfaceMember>();

        void Visit(INamedTypeSymbol type, bool outerContract)
        {
            if (type.DeclaredAccessibility != Accessibility.Public || type.IsImplicitlyDeclared || type.Name.Length == 0 || type.Name[0] == '<')
                return;
            var contract = outerContract && !Hidden(type);
            types.Add(new SurfaceType(
                package,
                type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString(),
                type.ToDisplayString(TypeFormat),
                Unbound(type, escape: false),
                IdOf(type),
                KindOf(type),
                contract,
                "global::" + Unbound(type, escape: true)));
            if (contract)
            {
                foreach (var member in type.GetMembers())
                {
                    if (member.DeclaredAccessibility != Accessibility.Public || member.IsImplicitlyDeclared || member is INamedTypeSymbol)
                        continue;
                    if (member is IMethodSymbol { MethodKind: not (MethodKind.Ordinary or MethodKind.Constructor) })
                        continue;
                    var interop = Interop(member);
                    if (interop || Hidden(member))
                    {
                        members.Add(new SurfaceMember(
                            package,
                            IdOf(type),
                            type.ToDisplayString(TypeFormat),
                            member.ToDisplayString(MemberFormat),
                            IdOf(member),
                            interop));
                    }
                }
            }
            foreach (var nested in type.GetTypeMembers())
                Visit(nested, contract);
        }

        void Walk(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
                Visit(type, outerContract: true);
            foreach (var inner in ns.GetNamespaceMembers())
                Walk(inner);
        }

        Walk(assembly.GlobalNamespace);
        types.Sort((a, b) =>
        {
            var byNamespace = string.CompareOrdinal(a.Namespace, b.Namespace);
            return byNamespace != 0 ? byNamespace : string.CompareOrdinal(a.Name, b.Name);
        });
        members.Sort((a, b) =>
        {
            var byType = string.CompareOrdinal(a.TypeId, b.TypeId);
            return byType != 0 ? byType : string.CompareOrdinal(a.Id, b.Id);
        });
        return (types, members);
    }

    // The documentation id the compiler writes for a symbol in its XML documentation, which keys its summary;
    // DocumentationCommentId.CreateDeclarationId would add a method's return type.
    private static string IdOf(ISymbol symbol) => symbol.GetDocumentationCommentId() ?? "";

    // Marked [EditorBrowsable(EditorBrowsableState.Never)]: hidden from a Consumer's editor.
    private static bool Hidden(ISymbol symbol) => symbol.GetAttributes().Any(a =>
        a.AttributeClass?.ToDisplayString() == "System.ComponentModel.EditorBrowsableAttribute"
        && a.ConstructorArguments.Length == 1
        && a.ConstructorArguments[0].Value is int state
        && state == 1);

    // Called by the grid's own script.
    private static bool Interop(ISymbol symbol) => symbol.GetAttributes().Any(a =>
        a.AttributeClass?.ToDisplayString() == "Microsoft.JSInterop.JSInvokableAttribute");

    private static string KindOf(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Class && type.AllInterfaces.Any(i => i.ToDisplayString() == "Microsoft.AspNetCore.Components.IComponent"))
            return "component";
        return type.TypeKind switch
        {
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
            _ => type.IsRecord ? "record" : type.IsStatic ? "static class" : type.IsAbstract ? "abstract class" : "class",
        };
    }

    // ExGrid.Script.ExGrid<>, Outer<>.Inner: the name a typeof takes, type parameters left out.
    private static string Unbound(INamedTypeSymbol type, bool escape)
    {
        var name = escape && SyntaxFacts.GetKeywordKind(type.Name) != SyntaxKind.None ? "@" + type.Name : type.Name;
        if (type.Arity > 0)
            name += "<" + new string(',', type.Arity - 1) + ">";
        if (type.ContainingType is { } outer)
            return Unbound(outer, escape) + "." + name;
        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }

    private static Location PageLocation(PageInput? page)
    {
        if (page is null || page.Scan.RouteSpans.IsEmpty)
            return Location.None;
        var (start, length) = page.Scan.RouteSpans[0];
        var span = new TextSpan(start, length);
        return Location.Create(page.FullPath, span, page.Text.Lines.GetLinePositionSpan(span));
    }

    private static SourceText Emit(List<SurfaceType> types, List<SurfaceMember> members, string record)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from the public types of the shipped assemblies (ADR-0201, ADR-0110).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class ApiSurface");
        code.AppendLine("{");
        code.AppendLine("    /// <summary>ADR-0201, which decides what is public and what is the contract, from the repository's root.</summary>");
        code.Append("    public const string RecordPath = ").Append(Literal(record)).AppendLine(";");
        code.AppendLine();
        code.AppendLine("    private static partial global::ExGrid.Docs.ApiType[] ReadTypes() =>");
        code.AppendLine("    [");
        foreach (var t in types)
        {
            code.Append("        new(").Append(Literal(t.Package)).Append(", ").Append(Literal(t.Namespace)).Append(", ")
                .Append(Literal(t.Name)).Append(", ").Append(Literal(t.Unbound)).Append(", ").Append(Literal(t.Id)).Append(", ")
                .Append(Literal(t.Kind)).Append(", ").Append(t.Contract ? "true" : "false").Append(", typeof(")
                .Append(t.TypeOf).AppendLine(")),");
        }
        code.AppendLine("    ];");
        code.AppendLine();
        code.AppendLine("    private static partial global::ExGrid.Docs.ApiMember[] ReadNotContract() =>");
        code.AppendLine("    [");
        foreach (var m in members)
        {
            code.Append("        new(").Append(Literal(m.Package)).Append(", ").Append(Literal(m.TypeId)).Append(", ")
                .Append(Literal(m.Type)).Append(", ").Append(Literal(m.Name)).Append(", ").Append(Literal(m.Id)).Append(", ")
                .Append(m.Interop ? "true" : "false").AppendLine("),");
        }
        code.AppendLine("    ];");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string Literal(string text) => SymbolDisplay.FormatLiteral(text, quote: true);
}
