using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>
/// The Keyboard reference's rows (ADR-0110), read at build time from the site's <c>KeyboardByHand</c>:
/// every key of the three products, one <c>KeyRow</c> each, under the property named for its product. Each
/// row links the section of the page that explains it, and cites the decision behind it. A row that links
/// a page or a section the site does not have, or cites a record that does not exist, stops the build.
/// So does a key a row names in the grid's canonical form (<c>Canonical</c>) that is not written in it. That
/// the rows name every key the products' tables take is read from the tables themselves, which run only
/// outside the compiler: <c>KeyboardCompletenessTests</c> holds the rows against them.
/// </summary>
public static class KeyboardReference
{
    /// <summary>The route of the site's Keyboard page.</summary>
    public const string Route = "reference/keyboard";

    /// <summary>The class the rows are declared in.</summary>
    public const string ClassName = "KeyboardByHand";

    /// <summary>The record each row constructs.</summary>
    public static readonly ImmutableHashSet<string> Types = ImmutableHashSet.Create("KeyRow");

    /// <summary>The products whose keys the page lists, each the name of the property holding its rows.</summary>
    public static readonly ImmutableArray<string> Products = ["ExGrid", "ExSheet", "ExPivot"];

    private const string Adr = "ADR-0110";

    /// <summary>A Keyboard reference row cannot be read, links nowhere, or cites nothing.</summary>
    public static readonly DiagnosticDescriptor RowWrong = new(
        id: "EXDOCS020",
        title: "A Keyboard reference row cannot be read, links nowhere or cites nothing",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every key on the Keyboard page is a KeyRow under ExGrid, ExSheet or ExPivot, written as literals, linking the section of the page that explains it and citing the decision behind it, and naming the keys it explains in the grid's canonical form (ADR-0110).");

    /// <summary>Whether <paramref name="node"/> declares the rows.</summary>
    public static bool IsHand(SyntaxNode node) =>
        node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax { Identifier.ValueText: ClassName };

    /// <summary>Reads, checks and emits the citations of the Keyboard reference's rows.</summary>
    /// <param name="hand">The rows <c>KeyboardByHand</c> constructs.</param>
    /// <param name="files">The repository's files the build reads, the Definition of Done among them.</param>
    /// <param name="adrs">The decision records' file names, by number.</param>
    /// <param name="pages">The site's routed pages, whose sections the rows link.</param>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<HandRow> hand,
        ImmutableArray<RecordInput> files,
        ImmutableDictionary<string, string> adrs,
        ImmutableArray<PageInput> pages)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var records = new RecordCites(adrs, files, Adr);
        var cites = new SortedDictionary<string, RecordCite>(StringComparer.Ordinal);
        void Report(Location location, string message) => diagnostics.Add(Diagnostic.Create(RowWrong, location, message));

        var hasPage = pages.Any(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase));
        if (hasPage && hand.IsEmpty)
        {
            Report(Location.None,
                $"The site has a Keyboard page (/{Route}), and no KeyRow reached the build: the page's rows are written in {ClassName}. "
                + $"Restore the class, or its rows ({Adr}).");
        }

        foreach (var row in hand)
        {
            if (!Products.Contains(row.Property))
            {
                Report(row.Location,
                    $"This KeyRow is declared in '{row.Property}', and the Keyboard page lists the keys of {string.Join(", ", Products)}, each under the "
                    + $"property named for it. Move it there ({Adr}).");
            }
            Plain(row.Text("Group"), "group", keys: false);
            Plain(row.Text("Keys"), "keys", keys: true);
            Plain(row.Text("What"), "description", keys: true);
            Explained(row.Text("Explained"));
            foreach (var key in row.List("Canonical"))
                Canonical(key);
            var list = row.List("Cites");
            if (list.IsEmpty)
            {
                Report(row.Location,
                    $"This KeyRow cites nothing. Every key cites the decision behind it, as ADR-0012, or the Definition of Done, as DoD §10 ({Adr}).");
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

        return (diagnostics.ToImmutable(), Emit(cites.Values));

        // Plain text, where a key may be written in brackets, as [Ctrl], for the page to draw as a key.
        void Plain(HandValue value, string part, bool keys)
        {
            var text = value.Value;
            if (text is null || text.Trim().Length == 0)
            {
                Report(value.Location, $"This KeyRow's {part} is not written as text, so the build cannot check it. Write it as a string literal ({Adr}).");
                return;
            }
            if (text.Contains('`') || text.Contains("**") || text.Contains('<') || (!keys && (text.Contains('[') || text.Contains(']'))))
            {
                Report(value.Location,
                    $"This KeyRow's {part} is plain text{(keys ? ", with each key in brackets as [Ctrl]" : "")}, and this one holds Markdown or markup the page "
                    + $"would show as it is. Write the words alone ({Adr}).");
                return;
            }
            if (keys && !Bracketed(text))
            {
                Report(value.Location,
                    $"This KeyRow's {part} has a bracket left open or a key with no name. Write each key as [Ctrl], [Shift] or [↓], one pair of brackets each ({Adr}).");
            }
        }

        // A key as the grid's key table and a Consumer's declared keys name it: the modifiers Control, Meta, Shift
        // and Alt in that order, each at most once, then the key as KeyboardEvent.key reports it (GridKeys.Canonical).
        void Canonical(HandValue value)
        {
            if (value.Value is not { } key)
            {
                Report(value.Location, $"This KeyRow's canonical key is not written as text, so the build cannot check it. Write it as a string literal ({Adr}).");
                return;
            }
            if (!IsCanonicalForm(key))
            {
                Report(value.Location,
                    $"This KeyRow names the key '{key}', which is not in the grid's canonical form: [Control+][Meta+][Shift+][Alt+]{{key}}, the key as "
                    + $"KeyboardEvent.key reports it, as Control+Shift+ArrowDown, Shift+F10 or Control+ (a space). Write it as the grid's key table names it ({Adr}).");
            }
        }

        void Explained(HandValue value)
        {
            var href = value.Value;
            if (href is null || href.Trim().Length == 0)
            {
                Report(value.Location,
                    $"This KeyRow's link is not written as text, so the build cannot check that its page explains it. Write it as \"exgrid/selection#the-keyboard\" ({Adr}).");
                return;
            }
            var hash = href.IndexOf('#');
            if (hash < 0)
            {
                Report(value.Location,
                    $"This KeyRow links '{href}', a page without a section. Link the section that explains the key, as \"exgrid/selection#the-keyboard\" ({Adr}).");
                return;
            }
            var route = href.Substring(0, hash).Trim('/');
            var anchor = href.Substring(hash + 1);
            var page = pages.FirstOrDefault(p => p.Scan.Page.Routes.Contains(route, StringComparer.OrdinalIgnoreCase));
            if (page is null)
            {
                Report(value.Location, $"This KeyRow links '{href}', and no page has the route /{route}. Link the page that explains the key ({Adr}).");
                return;
            }
            if (!page.Scan.Page.Sections.Any(s => s.Anchor == anchor))
            {
                Report(value.Location,
                    $"This KeyRow links '{href}', and {page.Scan.Page.Path} has no section with the anchor '{anchor}'. Its sections are "
                    + $"{string.Join(", ", page.Scan.Page.Sections.Select(s => s.Anchor))} ({Adr}).");
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="key"/> is in the canonical form <c>GridKeys</c> names keys in: the modifiers in its
    /// order, each at most once, then a key that is not a modifier, whose name holds no <c>+</c> unless it is
    /// <c>+</c> itself. <c>Alt+Alt</c>, the Alt key's own press, is the one modifier that may stand as the key.
    /// </summary>
    public static bool IsCanonicalForm(string key)
    {
        var rest = key ?? "";
        var control = TakePrefix(ref rest, "Control+");
        var meta = TakePrefix(ref rest, "Meta+");
        var shift = TakePrefix(ref rest, "Shift+");
        var alt = TakePrefix(ref rest, "Alt+");
        if (rest == "Alt" && alt && !control && !meta && !shift)
            return true;
        return rest.Length > 0
            && (rest == "+" || rest.IndexOf('+') < 0)
            && rest is not ("Control" or "Meta" or "Shift" or "Alt");
    }

    private static bool TakePrefix(ref string rest, string prefix)
    {
        if (!rest.StartsWith(prefix, StringComparison.Ordinal) || rest.Length == prefix.Length)
            return false;
        rest = rest.Substring(prefix.Length);
        return true;
    }

    // Whether every bracket in the text closes, holds a name, and holds no other bracket.
    private static bool Bracketed(string text)
    {
        var open = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '[')
            {
                if (open >= 0)
                    return false;
                open = i;
            }
            else if (text[i] == ']')
            {
                if (open < 0 || i == open + 1)
                    return false;
                open = -1;
            }
        }
        return open < 0;
    }

    private static SourceText Emit(IEnumerable<RecordCite> cites)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from KeyboardByHand (ADR-0110).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class Keyboard");
        code.AppendLine("{");
        RecordCites.EmitReadCites(code, cites, "global::ExGrid.Docs.DocsCite");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }
}
