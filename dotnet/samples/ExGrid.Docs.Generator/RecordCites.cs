using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace ExGrid.Docs.Generator;

/// <summary>A record a hand-written row cites, as the build found it.</summary>
/// <param name="Name">The citation as written: <c>ADR-0031</c>, <c>DoD §24</c>, <c>DoD Step 7</c>.</param>
/// <param name="Title">What a link to it says.</param>
/// <param name="Path">Its file, from the repository's root.</param>
/// <param name="Anchor">The heading it names in that file, as GitHub anchors it; <see langword="null"/> for the whole file.</param>
public sealed record RecordCite(string Name, string Title, string Path, string? Anchor);

/// <summary>
/// The records a hand-written row of a reference page may cite, and where each is read (ADR-0110): a
/// decision record by its number, as <c>ADR-0031</c>; the Definition of Done by a section or a step, as
/// <c>DoD §21.11</c> or <c>DoD Step 7</c>; or one of the files the build reads by its path. A citation
/// that leads nowhere is answered with why, for the build's error.
/// </summary>
/// <param name="adrs">The decision records' file names, by number.</param>
/// <param name="files">The repository's files the build reads: the Definition of Done among them.</param>
/// <param name="decision">The decision that requires the citations, named at the end of each message: <c>ADR-0191</c>.</param>
public sealed class RecordCites(ImmutableDictionary<string, string> adrs, ImmutableArray<RecordInput> files, string decision)
{
    /// <summary>The Definition of Done, which a row may cite by a section or a step.</summary>
    public const string DefinitionOfDone = "docs/definition-of-done.md";

    /// <summary>What the Definition of Done still owes, which a row may cite by its path.</summary>
    public const string ImplementationStatus = "docs/implementation-status.md";

    private static readonly Regex AdrCite = new(@"^ADR-([0-9]{4})$", RegexOptions.CultureInvariant);

    private static readonly Regex DodCite = new(@"^DoD (§([0-9]+(?:\.[0-9]+[a-z]?)*)|Step ([0-9]+))$", RegexOptions.CultureInvariant);

    private static readonly Regex PathCite = new(@"^docs/[A-Za-z0-9._/-]+\.md$", RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownLink = new(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.CultureInvariant);

    private List<(string Text, string Anchor)>? _dodHeadings;

    /// <summary>
    /// The anchor GitHub gives a Markdown heading: its text in lower case, every character that is not a
    /// letter, a digit, a hyphen or an underscore dropped, and each space made a hyphen. "Step 7 — Sign-off"
    /// is <c>step-7--sign-off</c>.
    /// </summary>
    public static string GitHubAnchor(string heading)
    {
        var text = MarkdownLink.Replace(heading, "$1").Replace("`", "").Replace("**", "");
        var anchor = new StringBuilder();
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                anchor.Append(c);
            else if (c == ' ')
                anchor.Append('-');
        }
        return anchor.ToString();
    }

    /// <summary>The record <paramref name="cite"/> names, or <see langword="null"/> with why not.</summary>
    public RecordCite? Resolve(string cite, out string why)
    {
        why = "";
        if (AdrCite.Match(cite) is { Success: true } adr)
        {
            var number = adr.Groups[1].Value;
            if (adrs.TryGetValue(number, out var file))
                return new RecordCite(cite, cite, "docs/adr/" + file, null);
            why = $"{cite} names no decision record: docs/adr/ has no file {number}-….md. Correct the number ({decision}).";
            return null;
        }
        if (DodCite.Match(cite) is { Success: true } dod)
        {
            var headings = DodHeadings();
            if (headings is null)
            {
                why = $"{cite} cites the Definition of Done, and {DefinitionOfDone} did not reach the build. ExGrid.Docs.csproj hands it over as an AdditionalFiles item; restore it ({decision}).";
                return null;
            }
            var section = dod.Groups[2].Success ? dod.Groups[2].Value : null;
            var prefix = section is not null
                ? new Regex("^" + Regex.Escape(section) + @"\.?(\s|$)", RegexOptions.CultureInvariant)
                : new Regex("^Step " + dod.Groups[3].Value + @"(\s|$)", RegexOptions.CultureInvariant);
            var match = headings.FirstOrDefault(h => prefix.IsMatch(h.Text));
            if (match.Text is null)
            {
                why = $"{cite} names no heading of the Definition of Done: none begins with '{(section ?? "Step " + dod.Groups[3].Value)}'. Cite a section by its number, as DoD §21.11, or a step, as DoD Step 7 ({decision}).";
                return null;
            }
            var title = section is not null ? $"Definition of Done §{section}" : $"Definition of Done, Step {dod.Groups[3].Value}";
            return new RecordCite(cite, title, DefinitionOfDone, match.Anchor);
        }
        if (PathCite.IsMatch(cite))
        {
            if (files.Any(f => f.RepoPath == cite))
                return new RecordCite(cite, cite, cite, null);
            why = $"{cite} is not a file the build reads. A hand-written row may cite {DefinitionOfDone} and {ImplementationStatus}, which ExGrid.Docs.csproj hands over; "
                + $"restore that item, or cite an ADR ({decision}).";
            return null;
        }
        why = $"'{cite}' is not a citation the build reads. Write an ADR as ADR-0031, the Definition of Done as DoD §21.11 or DoD Step 7, or a record as {ImplementationStatus} ({decision}).";
        return null;
    }

    /// <summary>
    /// The citations as C#, for a site class's <c>ReadCites()</c>: each record the rows cite, by the name
    /// they write.
    /// </summary>
    /// <param name="code">Where the C# goes.</param>
    /// <param name="cites">The records, by name.</param>
    /// <param name="siteType">The site's record type, which takes a name, a title, a path and an anchor.</param>
    public static void EmitReadCites(StringBuilder code, IEnumerable<RecordCite> cites, string siteType)
    {
        code.Append("    private static partial ").Append(siteType).AppendLine("[] ReadCites() =>");
        code.AppendLine("    [");
        foreach (var cite in cites)
        {
            code.Append("        new(").Append(Literal(cite.Name)).Append(", ").Append(Literal(cite.Title)).Append(", ")
                .Append(Literal(cite.Path)).Append(", ").Append(Literal(cite.Anchor)).AppendLine("),");
        }
        code.AppendLine("    ];");
    }

    private static string Literal(string? text) =>
        text is null ? "null" : SymbolDisplay.FormatLiteral(text, quote: true);

    // The Definition of Done's headings with GitHub's anchors, outside code fences; null when it did not reach the build.
    private List<(string Text, string Anchor)>? DodHeadings()
    {
        if (_dodHeadings is not null)
            return _dodHeadings;
        var file = files.FirstOrDefault(f => f.RepoPath == DefinitionOfDone);
        if (file is null)
            return null;
        var headings = new List<(string, string)>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var fenced = false;
        foreach (var line in file.Text.Lines)
        {
            var raw = line.ToString();
            if (raw.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }
            if (fenced)
                continue;
            var match = Regex.Match(raw, @"^#{1,6}\s+(.+?)(?:\s+#+)?\s*$");
            if (!match.Success)
                continue;
            var text = match.Groups[1].Value;
            var anchor = GitHubAnchor(text);
            if (seen.TryGetValue(anchor, out var count))
            {
                seen[anchor] = count + 1;
                anchor = $"{anchor}-{count + 1}";
            }
            else
            {
                seen[anchor] = 0;
            }
            headings.Add((text.Replace("`", "").Replace("**", ""), anchor));
        }
        return _dodHeadings = headings;
    }
}
