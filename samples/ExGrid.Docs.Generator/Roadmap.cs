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

/// <summary>A file of the repository the Roadmap reads: a ticket, a scope table's decision record, the Definition of Done.</summary>
/// <param name="FullPath">Its path on disk, where an error points.</param>
/// <param name="RepoPath">Its path from the repository's root: <c>docs/specs/exsheet/issues/31-drag-a-reference-outline.md</c>.</param>
/// <param name="Text">Its contents.</param>
public sealed record RecordInput(string FullPath, string RepoPath, SourceText Text);

/// <summary>
/// The Roadmap (ADR-0191), read at build time: the open tickets under <c>docs/specs/*/issues/</c> by their
/// <c>Status:</c> and <c>Roadmap:</c> lines; the scope tables of ADR-0046 (ExSheet) and ADR-0059 (ExPivot),
/// whose "Later" and refused rows it lists; and the rows the site writes by hand in <c>RoadmapByHand</c>,
/// each citing the record that requires or refused it. What it cannot place, or what would make it a
/// promise of time, stops the build with the file, the line and the fix.
/// </summary>
public static class Roadmap
{
    /// <summary>The route of the site's Roadmap page.</summary>
    public const string Route = "roadmap";

    /// <summary>The ticket roles (<c>docs/agents/issue-tracker.md</c>).</summary>
    public static readonly ImmutableArray<string> Roles =
        ["needs-triage", "needs-info", "ready-for-agent", "ready-for-human", "done", "wontfix"];

    /// <summary>
    /// The Roadmap page's six sections, in ADR-0191's order, each with the name the site's <c>Roadmap</c>
    /// class gives its title. The page's sections are checked against these, and the search reads them.
    /// </summary>
    public static readonly ImmutableArray<(string Name, string Title)> Sections =
    [
        ("OnMain", "On main, not in a version yet"),
        ("Decided", "Decided, ready to build"),
        ("Waiting", "Waiting on a decision"),
        ("Later", "Later"),
        ("TowardStable", "Toward the next stable version"),
        ("NotPlanned", "Not planned"),
    ];

    /// <summary>The decision records whose scope tables the Roadmap reads, and the product each table is of.</summary>
    public static readonly ImmutableDictionary<string, string> ScopeTables =
        new Dictionary<string, string> { ["0046"] = "ExSheet", ["0059"] = "ExPivot" }.ToImmutableDictionary();

    /// <summary>The Definition of Done, which a hand-written row may cite by a section or a step.</summary>
    public const string DefinitionOfDone = RecordCites.DefinitionOfDone;

    /// <summary>What the Definition of Done still owes, which the page links rather than copies.</summary>
    public const string ImplementationStatus = RecordCites.ImplementationStatus;

    private const string Adr = "ADR-0191";

    /// <summary>A ticket or a hand-written row cannot be read, or is open with no Roadmap line.</summary>
    public static readonly DiagnosticDescriptor RowMissing = new(
        id: "EXDOCS011",
        title: "A Roadmap row is missing or cannot be read",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every ticket that is not done or wontfix carries Roadmap: <Products> — <one sentence a Consumer understands>, or Roadmap: internal, beside its Status: line; a hand-written row is plain text written as a literal (ADR-0191).");

    /// <summary>A row names a product outside the four.</summary>
    public static readonly DiagnosticDescriptor ProductOutside = new(
        id: "EXDOCS012",
        title: "A Roadmap row names a product outside the four",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Roadmap's products are ExGrid, ExSheet, ExPivot and Data, which its filter chips select (ADR-0191).");

    /// <summary>A row cites a record that does not exist.</summary>
    public static readonly DiagnosticDescriptor CiteNowhere = new(
        id: "EXDOCS013",
        title: "A Roadmap citation leads nowhere",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An ADR the Roadmap names has a file under docs/adr/, a section of the Definition of Done it cites has a heading, and every hand-written row cites the record that requires or refused it (ADR-0191).");

    /// <summary>A scope table's row does not carry exactly one mark, or the table is missing.</summary>
    public static readonly DiagnosticDescriptor ScopeMark = new(
        id: "EXDOCS014",
        title: "A scope-table row does not carry exactly one mark",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Roadmap reads ADR-0046's and ADR-0059's scope tables by their header, | | First version | Later | Why later |, and each row carries one mark: ✓ under First version or Later, or ✗ for refused (ADR-0191).");

    /// <summary>The Roadmap page does not have ADR-0191's six sections, in order.</summary>
    public static readonly DiagnosticDescriptor PageSections = new(
        id: "EXDOCS016",
        title: "The Roadmap page's sections are not ADR-0191's six",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Roadmap has six sections, in this order, each read from one source: On main, not in a version yet; Decided, ready to build; Waiting on a decision; Later; Toward the next stable version; Not planned (ADR-0191).");

    /// <summary>A Roadmap sentence or a hand-written row promises a time.</summary>
    public static readonly DiagnosticDescriptor Timing = new(
        id: "EXDOCS015",
        title: "A Roadmap row carries a date, a quarter or a version",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Roadmap has no dates, quarters or target versions: work is ordered by the state of its decision, never by a calendar (ADR-0191).");

    private static readonly Regex Heading1 = new(@"^#\s+([0-9]+):\s+(.+?)\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex StatusLine = new(@"^Status:[ \t]*(.*)$", RegexOptions.CultureInvariant);

    private static readonly Regex RoadmapLine = new(@"^Roadmap:[ \t]*(.*)$", RegexOptions.CultureInvariant);

    private static readonly Regex HeaderLine = new(@"^[A-Z][A-Za-z ]*:(\s|$)", RegexOptions.CultureInvariant);

    private static readonly Regex ScopeHeader = new(
        @"^\s*\|\s*\|\s*First version\s*\|\s*Later\s*\|\s*Why later\s*\|\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex ScopeSeparator = new(@"^\s*\|(\s*:?-{3,}:?\s*\|){4}\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex AdrMention = new(@"\bADR-([0-9]{4})\b", RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownLink = new(@"\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.CultureInvariant);

    private static readonly Regex AdrLinkTarget = new(@"^(?:\.{1,2}/)*(?:docs/)?(?:adr/)?([0-9]{4})-[^/#]*\.md(?:#.*)?$", RegexOptions.CultureInvariant);

    // "above" as a "Why later" cell points into its record: the whole cell, its last words after a comma,
    // or in brackets. Elsewhere — "the Pivot Toolbar above the report" — it is the feature's own words.
    private static readonly Regex Above = new(@"(?<=^|, |\()above(?=\)|$)", RegexOptions.CultureInvariant);

    private const string Months =
        "January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sept|Sep|Oct|Nov|Dec";

    /// <summary>
    /// What a Roadmap sentence, or a hand-written row, may not hold: a date, a year, a quarter or a half,
    /// a version number, or a target version (ADR-0191). Each with what it is, for the message.
    /// </summary>
    private static readonly ImmutableArray<(Regex Pattern, string What)> Promises =
    [
        (new(@"\b[0-9]{4}-[0-9]{1,2}(?:-[0-9]{1,2})?\b", RegexOptions.CultureInvariant), "a date"),
        (new(@"\b[0-9]{1,2}/[0-9]{1,2}(?:/[0-9]{2,4})?\b", RegexOptions.CultureInvariant), "a date"),
        (new(@"\b(?:" + Months + @")\.?\s+[0-9]{1,4}\b", RegexOptions.CultureInvariant), "a date"),
        (new(@"\b[0-9]{1,2}(?:st|nd|rd|th)?\s+(?:" + Months + @")\b", RegexOptions.CultureInvariant), "a date"),
        (new(@"(?<!ADR-)\b(?:19|20)[0-9]{2}\b", RegexOptions.CultureInvariant), "a year"),
        (new(@"\b[QH][1-4]\b", RegexOptions.CultureInvariant), "a quarter"),
        (new(@"\b(?:first|second|third|fourth|last|next|this|coming)\s+(?:quarter|half)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "a quarter"),
        (new(@"\bv[0-9]+(?:\.[0-9]+)*\b", RegexOptions.CultureInvariant), "a version"),
        (new(@"\b[0-9]+\.[0-9]+(?:\.[0-9]+)*(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?\b", RegexOptions.CultureInvariant), "a version"),
        (new(@"\b(?:alpha|beta|rc|preview)[ .-]?[0-9]+\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "a version"),
        (new(@"\bversion\s+[0-9]+\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "a version"),
        (new(@"\b(?:next|this|coming|following|upcoming)\s+(?:(?:stable|major|minor)\s+)?(?:version|release|beta|prerelease)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase), "a target version"),
    ];

    /// <summary>
    /// The first date, quarter, version number or target version <paramref name="text"/> holds, as its
    /// index, its length and what it is; <see langword="null"/> when it holds none (ADR-0191).
    /// </summary>
    public static (int Index, int Length, string What)? FindPromise(string text)
    {
        (int Index, int Length, string What)? first = null;
        foreach (var (pattern, what) in Promises)
        {
            var match = pattern.Match(text);
            if (match.Success && (first is null || match.Index < first.Value.Index))
                first = (match.Index, match.Length, what);
        }
        return first;
    }

    /// <summary>
    /// The path from the repository's root of a file the Roadmap reads — a ticket, ADR-0046 or ADR-0059,
    /// the Definition of Done or the implementation status — or <see langword="null"/> for any other file.
    /// </summary>
    /// <param name="path">The file's path on disk, with forward slashes.</param>
    public static string? RepoPathOf(string path)
    {
        if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return null;
        var specs = path.LastIndexOf("/docs/specs/", StringComparison.Ordinal);
        if (specs >= 0)
        {
            var repo = path.Substring(specs + 1);
            var parts = repo.Split('/');
            return parts.Length == 5 && parts[3] == "issues" ? repo : null;
        }
        var adr = path.LastIndexOf("/docs/adr/", StringComparison.Ordinal);
        if (adr >= 0)
        {
            var name = path.Substring(adr + "/docs/adr/".Length);
            return name.Length > 5 && name[4] == '-' && ScopeTables.ContainsKey(name.Substring(0, 4)) && name.IndexOf('/') < 0
                ? "docs/adr/" + name
                : null;
        }
        if (path.EndsWith("/" + DefinitionOfDone, StringComparison.Ordinal))
            return DefinitionOfDone;
        if (path.EndsWith("/" + ImplementationStatus, StringComparison.Ordinal))
            return ImplementationStatus;
        return null;
    }

    /// <summary>The anchor GitHub gives a Markdown heading (<see cref="RecordCites.GitHubAnchor"/>).</summary>
    public static string GitHubAnchor(string heading) => RecordCites.GitHubAnchor(heading);

    /// <summary>Reads, checks and emits the Roadmap.</summary>
    /// <param name="files">The tickets and the records the Roadmap reads (<see cref="RepoPathOf"/>).</param>
    /// <param name="hand">The rows <c>RoadmapByHand</c> constructs.</param>
    /// <param name="pages">The site's routed pages: a site with a Roadmap page needs every source.</param>
    /// <param name="adrs">The decision records' file names, by number.</param>
    /// <param name="build">What the site is built from, which an ADR link points at.</param>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<RecordInput> files,
        ImmutableArray<RoadmapHandRow> hand,
        ImmutableArray<PageInput> pages,
        ImmutableDictionary<string, string> adrs,
        DocsBuildInfo build)
    {
        var reader = new Reader(files, hand, pages, adrs, build);
        reader.Run();
        return (reader.Diagnostics.ToImmutable(), Emit(reader));
    }

    /// <summary>An open ticket, as the page lists it.</summary>
    internal sealed record Ticket(
        string Feature, string Number, string TitleHtml, string Title, ImmutableArray<string> Products,
        string SentenceHtml, string Sentence, string State, string Status, string Path);

    /// <summary>A row of a scope table.</summary>
    internal sealed record ScopeRow(
        string Product, string Adr, string AdrFile, string Scope, string LabelHtml, string Label, string WhyHtml, string Why);

    private sealed class Reader(
        ImmutableArray<RecordInput> files,
        ImmutableArray<RoadmapHandRow> hand,
        ImmutableArray<PageInput> pages,
        ImmutableDictionary<string, string> adrs,
        DocsBuildInfo build)
    {
        public ImmutableArray<Diagnostic>.Builder Diagnostics { get; } = ImmutableArray.CreateBuilder<Diagnostic>();

        public List<Ticket> Tickets { get; } = [];

        public List<ScopeRow> Scope { get; } = [];

        public SortedDictionary<string, RecordCite> Cites { get; } = new(StringComparer.Ordinal);

        private NotesContext Context => new(adrs, build.GitRef, AllowRef: false);

        private readonly RecordCites _records = new(adrs, files, Adr);

        public void Run()
        {
            var page = pages.FirstOrDefault(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase));
            var hasPage = page is not null;
            if (page is not null)
                CheckPage(page);
            var tickets = files.Where(f => f.RepoPath.StartsWith("docs/specs/", StringComparison.Ordinal))
                .OrderBy(f => f.RepoPath, StringComparer.Ordinal).ToList();
            if (hasPage && tickets.Count == 0)
            {
                Diagnostics.Add(Diagnostic.Create(RowMissing, Location.None,
                    $"The site has a Roadmap page (/{Route}), and no ticket under docs/specs/*/issues/ reached the build. "
                    + $"ExGrid.Docs.csproj hands them over as AdditionalFiles; restore that item ({Adr})."));
            }
            foreach (var ticket in tickets)
                ReadTicket(ticket);

            foreach (var table in ScopeTables.OrderBy(t => t.Key, StringComparer.Ordinal))
            {
                var file = files.FirstOrDefault(f => f.RepoPath.StartsWith($"docs/adr/{table.Key}-", StringComparison.Ordinal));
                if (file is not null)
                    ReadScope(file, table.Key, table.Value);
                else if (hasPage)
                {
                    Diagnostics.Add(Diagnostic.Create(ScopeMark, Location.None,
                        $"The site has a Roadmap page (/{Route}), and ADR-{table.Key}, whose scope table holds {table.Value}'s Later and refused rows, did not reach the build. "
                        + $"ExGrid.Docs.csproj hands docs/adr/*.md over as AdditionalFiles; restore that item ({Adr})."));
                }
            }

            foreach (var row in hand)
                CheckHand(row);
        }

        // The page's sections are the six, in order: the search and a link to one name them by these titles.
        private void CheckPage(PageInput page)
        {
            var titles = page.Scan.Page.Sections.Select(s => s.Title).ToList();
            if (titles.SequenceEqual(Sections.Select(s => s.Title)))
                return;
            var expected = string.Join("; ", Sections.Select(s => s.Title));
            var span = page.Scan.RouteSpans.IsEmpty ? (Start: 0, Length: 1) : page.Scan.RouteSpans[0];
            var end = Math.Min(span.Start + span.Length, page.Text.Length);
            var location = Location.Create(page.FullPath, TextSpan.FromBounds(span.Start, end), page.Text.Lines.GetLinePositionSpan(TextSpan.FromBounds(span.Start, end)));
            Diagnostics.Add(Diagnostic.Create(PageSections, location,
                $"{page.Scan.Page.Path} has the sections {(titles.Count == 0 ? "none" : string.Join("; ", titles))}. The Roadmap has six, in this order, "
                + $"each a DocsSection with its title as text: {expected} ({Adr})."));
        }

        private void Add(DiagnosticDescriptor descriptor, RecordInput file, int start, int length, string message) =>
            Diagnostics.Add(Diagnostic.Create(descriptor, At(file, start, length), message));

        private void ReadTicket(RecordInput file)
        {
            var lines = file.Text.Lines;
            var statusAt = -1;
            for (var k = 0; k < lines.Count; k++)
            {
                var raw = lines[k].ToString();
                if (raw.StartsWith("## ", StringComparison.Ordinal))
                    break;
                if (StatusLine.IsMatch(raw))
                {
                    statusAt = k;
                    break;
                }
            }
            if (statusAt < 0)
            {
                Add(RowMissing, file, 0, Math.Max(1, lines.Count > 0 ? lines[0].Span.Length : 1),
                    "This ticket has no Status: line before its first ## heading, so the Roadmap cannot tell whether it is open. "
                    + $"Write Status: and one of {string.Join(", ", Roles)} near the top, as docs/agents/issue-tracker.md says ({Adr}).");
                return;
            }
            var statusLine = lines[statusAt];
            var statusText = StatusLine.Match(statusLine.ToString()).Groups[1].Value;
            var role = new string(statusText.TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
            var roleStart = statusLine.Start + "Status:".Length + (statusLine.ToString().Length - "Status:".Length - statusText.Length);
            if (!Roles.Contains(role))
            {
                Add(RowMissing, file, roleStart, Math.Max(1, role.Length),
                    $"'{role}' is not a ticket role, so the Roadmap cannot place this ticket. The roles are {string.Join(", ", Roles)} (docs/agents/issue-tracker.md).");
                return;
            }
            if (role is "done" or "wontfix")
                return;

            // The Roadmap line stands beside Status:, among the header lines that follow it.
            var roadmapAt = -1;
            for (var k = statusAt + 1; k < lines.Count && lines[k].ToString().Trim().Length > 0; k++)
            {
                if (RoadmapLine.IsMatch(lines[k].ToString()))
                {
                    roadmapAt = k;
                    break;
                }
            }
            if (roadmapAt < 0)
            {
                for (var k = 0; k < lines.Count; k++)
                {
                    var raw = lines[k].ToString();
                    if (raw.StartsWith("## Comments", StringComparison.Ordinal))
                        break;
                    if (RoadmapLine.IsMatch(raw))
                    {
                        Add(RowMissing, file, lines[k].Start, "Roadmap:".Length,
                            $"This Roadmap: line stands away from Status:. Move it to the line after Status:, where the Roadmap and a reader look for it ({Adr}).");
                        return;
                    }
                }
                Add(RowMissing, file, statusLine.Start, statusLine.Span.Length,
                    $"This ticket is open (Status: {role}) and has no Roadmap: line. On the line after Status:, write "
                    + "Roadmap: <Products> — <one sentence a Consumer understands>, the products from ExGrid, ExSheet, ExPivot, ExChart, ExBoard and Data, "
                    + $"or Roadmap: internal for work a Consumer would not care about, such as a verification run or a process change ({Adr}).");
                return;
            }

            var heading = lines.Count > 0 ? Heading1.Match(lines[0].ToString()) : Match.Empty;
            if (!heading.Success)
            {
                Add(RowMissing, file, 0, Math.Max(1, lines.Count > 0 ? lines[0].Span.Length : 1),
                    "A ticket begins with # <number>: <title>, as # 31: Drag a Reference Outline to rewrite its Reference; the Roadmap lists an open ticket by that title.");
                return;
            }

            // The line, with any lines it wraps onto: they run to a blank line or another header line.
            var first = lines[roadmapAt];
            var value = RoadmapLine.Match(first.ToString()).Groups[1];
            var text = new NotesText();
            text.AddLine(value.Value, first.Start + value.Index);
            for (var k = roadmapAt + 1; k < lines.Count; k++)
            {
                var raw = lines[k].ToString();
                if (raw.Trim().Length == 0 || HeaderLine.IsMatch(raw))
                    break;
                var indent = raw.Length - raw.TrimStart().Length;
                text.AddLine(raw.Substring(indent).TrimEnd(), lines[k].Start + indent);
            }
            var whole = text.Text.TrimEnd();
            if (whole == "internal")
                return;

            var dash = whole.IndexOf(" — ", StringComparison.Ordinal);
            if (dash < 0 && whole.EndsWith(" —", StringComparison.Ordinal))
            {
                Add(RowMissing, file, text.OffsetOf(whole.Length - 1), 1,
                    $"This Roadmap line has no sentence after its products. Say in one sentence a Consumer understands what the work gives them ({Adr}).");
                return;
            }
            if (dash < 0)
            {
                Add(RowMissing, file, text.OffsetOf(0), Math.Max(1, whole.Length),
                    "A Roadmap line is Roadmap: <Products> — <one sentence a Consumer understands>, the two parted by a spaced em dash (—), "
                    + $"or Roadmap: internal alone ({Adr}).");
                return;
            }

            var products = ImmutableArray.CreateBuilder<string>();
            var offset = 0;
            foreach (var part in whole.Substring(0, dash).Split(','))
            {
                var name = part.Trim();
                var at = offset + (part.Length - part.TrimStart().Length);
                offset += part.Length + 1;
                if (!ReleaseNotes.Products.Contains(name))
                {
                    var hint = name == "internal" ? " A ticket a Consumer would not care about says Roadmap: internal alone, with no sentence." : "";
                    Add(ProductOutside, file, text.OffsetOf(at), Math.Max(1, name.Length),
                        $"'{name}' is not one of the Roadmap's products, which are {string.Join(", ", ReleaseNotes.Products)}; "
                        + $"list those the work reaches, parted by commas.{hint}");
                }
                else if (products.Contains(name))
                {
                    Add(ProductOutside, file, text.OffsetOf(at), name.Length, $"{name} is listed twice on this Roadmap line.");
                }
                else
                {
                    products.Add(name);
                }
            }

            var sentenceFrom = dash + 3;
            if (whole.Substring(sentenceFrom).Trim().Length == 0)
            {
                Add(RowMissing, file, text.OffsetOf(dash), 1,
                    $"This Roadmap line has no sentence after its products. Say in one sentence a Consumer understands what the work gives them ({Adr}).");
                return;
            }
            var problems = new List<NotesProblem>();
            var sentence = NotesMarkdown.Render(text, Context, problems, sentenceFrom);
            foreach (var link in sentence.Links)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, link.Start, link.Length,
                    "A Roadmap sentence links nothing: its row links the ticket, and an ADR it names, as ADR-0181, is linked for it. Write the ADR's number, not a link."));
            }
            foreach (var problem in problems)
            {
                Add(problem.Kind == NotesProblemKind.Link ? CiteNowhere : RowMissing, file, problem.Start, problem.Length, problem.Message);
            }
            CheckPromise(file, text, sentenceFrom, whole);

            var title = heading.Groups[2];
            var titleText = new NotesText();
            titleText.AddLine(title.Value, lines[0].Start + title.Index);
            var titleInline = NotesMarkdown.Render(titleText, Context, []);
            var parts = file.RepoPath.Split('/');
            Tickets.Add(new Ticket(
                parts[2], heading.Groups[1].Value, titleInline.Html, titleInline.Text, products.ToImmutable(),
                sentence.Html, sentence.Text,
                role is "ready-for-agent" or "ready-for-human" ? "Decided" : "Waiting",
                role, file.RepoPath));
        }

        private void CheckPromise(RecordInput file, NotesText text, int from, string whole)
        {
            if (FindPromise(whole.Substring(from)) is not { } promise)
                return;
            var found = whole.Substring(from + promise.Index, promise.Length);
            Add(Timing, file, text.OffsetOf(from + promise.Index), promise.Length,
                $"This Roadmap sentence holds {promise.What}, '{found}'. The Roadmap has no dates, quarters or target versions: work is ordered by "
                + $"the state of its decision, never by a calendar, and the tag that ships it says when ({Adr}). Say what the work gives a Consumer, and leave out when.");
        }

        private void ReadScope(RecordInput file, string number, string product)
        {
            var lines = file.Text.Lines;
            var header = -1;
            for (var k = 0; k < lines.Count; k++)
            {
                if (ScopeHeader.IsMatch(lines[k].ToString()))
                {
                    header = k;
                    break;
                }
            }
            if (header < 0)
            {
                Add(ScopeMark, file, 0, 1,
                    $"ADR-{number} has no scope table with the header | | First version | Later | Why later |, which the Roadmap reads for {product}'s Later and refused rows. "
                    + $"Restore the header as written, or move the table with a decision that says where it now lives ({Adr}).");
                return;
            }
            if (header + 1 >= lines.Count || !ScopeSeparator.IsMatch(lines[header + 1].ToString()))
            {
                Add(ScopeMark, file, lines[header].Start, lines[header].Span.Length,
                    $"ADR-{number}'s scope table has no |---|---|---|---| line under its header, so it is not a table. Restore it ({Adr}).");
                return;
            }
            var adrFile = file.RepoPath.Substring("docs/adr/".Length);
            for (var k = header + 2; k < lines.Count; k++)
            {
                var line = lines[k];
                var raw = line.ToString();
                if (!raw.TrimStart().StartsWith("|", StringComparison.Ordinal))
                    break;
                var cells = Cells(raw);
                if (cells.Count != 4)
                {
                    Add(ScopeMark, file, line.Start, line.Span.Length,
                        $"This row of ADR-{number}'s scope table has {cells.Count} cells, and the table has four: what, First version, Later, Why later ({Adr}).");
                    continue;
                }
                var marks = cells[1].Count(c => c is '✓' or '✗') + cells[2].Count(c => c is '✓' or '✗');
                if (marks != 1)
                {
                    var found = marks == 0 ? "no mark" : $"{marks} marks";
                    Add(ScopeMark, file, line.Start, line.Span.Length,
                        $"This row of ADR-{number}'s scope table carries {found}. A row carries exactly one: ✓ under First version or under Later, or ✗ for what is refused, "
                        + $"so the Roadmap can say which it is ({Adr}).");
                    continue;
                }
                var scope = cells[1].Contains('✗') || cells[2].Contains('✗') ? "NotPlanned"
                    : cells[1].Contains('✓') ? "FirstVersion"
                    : "Later";
                var problems = new List<NotesProblem>();
                var label = Cell(cells[0], number, problems, why: false);
                var why = Cell(cells[3], number, problems, why: true);
                foreach (var problem in problems.Where(p => p.Kind == NotesProblemKind.Link))
                    Add(CiteNowhere, file, line.Start, line.Span.Length, $"In this row of ADR-{number}'s scope table: {problem.Message}");
                Scope.Add(new ScopeRow(product, number, adrFile, scope, label.Html, label.Text, why.Html, why.Text));
            }
        }

        // A cell as the page shows it. A link to a decision record becomes its number, which is linked at
        // the ref the site was built from; another relative link becomes its text; a "Why later" of
        // "above", which points into the decision record, names it.
        private NotesInline Cell(string cell, string number, List<NotesProblem> problems, bool why)
        {
            var text = MarkdownLink.Replace(cell, m =>
            {
                var label = m.Groups[1].Value;
                var target = m.Groups[2].Value;
                if (AdrLinkTarget.Match(target) is { Success: true } adr)
                    return label.Contains("ADR-" + adr.Groups[1].Value) ? label : $"{label} (ADR-{adr.Groups[1].Value})";
                return target.StartsWith("https://", StringComparison.Ordinal) ? m.Value : label;
            });
            if (why)
                text = Above.Replace(text.Trim(), "in ADR-" + number);
            var notes = new NotesText();
            notes.AddLine(text.Trim(), 0);
            return NotesMarkdown.Render(notes, Context, problems);
        }

        // A table row's cells, without the outer pipes; a pipe inside a code span or escaped does not part them.
        private static List<string> Cells(string row)
        {
            var trimmed = row.Trim();
            if (trimmed.StartsWith("|", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|", StringComparison.Ordinal) && !trimmed.EndsWith("\\|", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            var cells = new List<string>();
            var current = new StringBuilder();
            var code = false;
            for (var i = 0; i < trimmed.Length; i++)
            {
                var c = trimmed[i];
                if (c == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
                {
                    current.Append('|');
                    i++;
                    continue;
                }
                if (c == '`')
                    code = !code;
                if (c == '|' && !code)
                {
                    cells.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            cells.Add(current.ToString().Trim());
            return cells;
        }

        private void CheckHand(RoadmapHandRow row)
        {
            var what = row.IsRecord ? "This RoadmapRecord" : "This Roadmap row";
            CheckHandText(what, row.IsRecord ? "title" : "text", row.Text);
            if (row.About is { } about)
                CheckHandText(what, "sentence", about);

            if (!row.IsRecord)
            {
                if (row.Products.IsEmpty)
                {
                    Diagnostics.Add(Diagnostic.Create(ProductOutside, row.Location,
                        $"This Roadmap row names no product. List those it reaches, from {string.Join(", ", ReleaseNotes.Products)}, so the filter can place it ({Adr})."));
                }
                foreach (var product in row.Products)
                {
                    if (product.Value is null || !ReleaseNotes.Products.Contains(product.Value))
                    {
                        Diagnostics.Add(Diagnostic.Create(ProductOutside, product.Location,
                            $"{(product.Value is null ? "This product is not written as text" : $"'{product.Value}' is not one of the Roadmap's products")}: "
                            + $"they are {string.Join(", ", ReleaseNotes.Products)}, each a string literal ({Adr})."));
                    }
                }
            }

            if (row.Cites.IsEmpty)
            {
                Diagnostics.Add(Diagnostic.Create(CiteNowhere, row.Location,
                    $"This Roadmap row cites nothing. Every hand-written row cites the record that requires or refused it: an ADR, as ADR-0031, "
                    + $"or the Definition of Done, as DoD Step 7 or DoD §21.11 ({Adr})."));
            }
            foreach (var cite in row.Cites)
            {
                if (cite.Value is null)
                {
                    Diagnostics.Add(Diagnostic.Create(CiteNowhere, cite.Location, $"This citation is not written as text, so the build cannot check that its record exists. Write it as a string literal ({Adr})."));
                    continue;
                }
                if (_records.Resolve(cite.Value, out var why) is { } resolved)
                    Cites[cite.Value] = resolved;
                else
                    Diagnostics.Add(Diagnostic.Create(CiteNowhere, cite.Location, why));
            }
        }

        // A hand-written text: a literal, plain, and with no promise of time.
        private void CheckHandText(string what, string part, RoadmapHandValue value)
        {
            var text = value.Value;
            if (text is null || text.Trim().Length == 0)
            {
                Diagnostics.Add(Diagnostic.Create(RowMissing, value.Location,
                    $"{what}'s {part} is not written as text, so the build cannot check it. Write it as a string literal ({Adr})."));
            }
            else if (text.Contains('`') || text.Contains("**") || text.Contains('[') || text.Contains('<'))
            {
                Diagnostics.Add(Diagnostic.Create(RowMissing, value.Location,
                    $"{what}'s {part} is plain text, and this one holds Markdown or markup the page would show as it is. Write the words alone ({Adr})."));
            }
            else if (FindPromise(text) is { } promise)
            {
                Diagnostics.Add(Diagnostic.Create(Timing, value.Location,
                    $"{what}'s {part} holds {promise.What}, '{text.Substring(promise.Index, promise.Length)}'. The Roadmap has no dates, quarters or target versions: "
                    + $"work is ordered by the state of its decision, never by a calendar ({Adr}). Say what is required or refused, and leave out when."));
            }
        }

        private static Location At(RecordInput file, int start, int length)
        {
            start = Math.Max(0, Math.Min(start, file.Text.Length));
            var end = Math.Min(start + Math.Max(length, 0), file.Text.Length);
            var span = TextSpan.FromBounds(start, end);
            return Location.Create(file.FullPath, span, file.Text.Lines.GetLinePositionSpan(span));
        }
    }

    private static SourceText Emit(Reader reader)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from the tickets under docs/specs/, the scope tables of ADR-0046 and ADR-0059, and the records RoadmapByHand cites (ADR-0191).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class Roadmap");
        code.AppendLine("{");
        foreach (var (name, title) in Sections)
        {
            code.Append("    /// <summary>The section titled \"").Append(title).AppendLine("\".</summary>");
            code.Append("    public const string ").Append(name).Append(" = ").Append(Literal(title)).AppendLine(";");
            code.AppendLine();
        }
        code.AppendLine("    private static partial global::ExGrid.Docs.RoadmapTicket[] ReadTickets() =>");
        code.AppendLine("    [");
        foreach (var ticket in reader.Tickets
                     .OrderBy(t => ReleaseNotes.Products.IndexOf(t.Products.FirstOrDefault() ?? ""))
                     .ThenBy(t => t.Feature, StringComparer.Ordinal)
                     .ThenBy(t => t.Number.Length)
                     .ThenBy(t => t.Number, StringComparer.Ordinal))
        {
            code.Append("        new(").Append(Literal(ticket.Feature)).Append(", ").Append(Literal(ticket.Number)).Append(", ")
                .Append(Literal(ticket.TitleHtml)).Append(", ").Append(Literal(ticket.Title)).Append(", ")
                .Append(Strings(ticket.Products)).Append(", ")
                .Append(Literal(ticket.SentenceHtml)).Append(", ").Append(Literal(ticket.Sentence)).Append(", ")
                .Append("global::ExGrid.Docs.RoadmapState.").Append(ticket.State).Append(", ")
                .Append(Literal(ticket.Status)).Append(", ").Append(Literal(ticket.Path)).AppendLine("),");
        }
        code.AppendLine("    ];");
        code.AppendLine();
        code.AppendLine("    private static partial global::ExGrid.Docs.RoadmapScopeRow[] ReadScope() =>");
        code.AppendLine("    [");
        foreach (var row in reader.Scope)
        {
            code.Append("        new(").Append(Literal(row.Product)).Append(", ").Append(Literal(row.Adr)).Append(", ").Append(Literal(row.AdrFile)).Append(", ")
                .Append("global::ExGrid.Docs.RoadmapScope.").Append(row.Scope).Append(", ")
                .Append(Literal(row.LabelHtml)).Append(", ").Append(Literal(row.Label)).Append(", ")
                .Append(Literal(row.WhyHtml)).Append(", ").Append(Literal(row.Why)).AppendLine("),");
        }
        code.AppendLine("    ];");
        code.AppendLine();
        RecordCites.EmitReadCites(code, reader.Cites.Values, "global::ExGrid.Docs.RoadmapCite");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string Strings(IEnumerable<string> values) =>
        "[" + string.Join(", ", values.Select(Literal)) + "]";

    private static string Literal(string? text) =>
        text is null ? "null" : SymbolDisplay.FormatLiteral(text, quote: true);
}
