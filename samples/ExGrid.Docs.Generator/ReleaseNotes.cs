using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ExGrid.Docs.Generator;

/// <summary>A file under <c>docs/releases/</c>, as the build hands it over.</summary>
/// <param name="FullPath">Its path on disk, where an error points.</param>
/// <param name="Name">Its path under <c>docs/releases/</c>: <c>0.1.0-beta.2.md</c>, <c>unreleased/row-key.md</c>.</param>
/// <param name="Text">Its contents.</param>
public sealed record NotesInput(string FullPath, string Name, SourceText Text);

/// <summary>
/// The Release Notes (ADR-0190): each published version's file, <c>docs/releases/&lt;version&gt;.md</c>;
/// the Change Notes under <c>docs/releases/unreleased/</c>, written with each change; and the prerelease
/// note every prerelease's notes begin with. They are read in the notes' Markdown subset
/// (<see cref="NotesMarkdown"/>), checked against their shape, the decision records and the site map,
/// and emitted for the site's Release notes page.
/// </summary>
public static class ReleaseNotes
{
    /// <summary>The products a note names, in the order the notes' headings put them.</summary>
    public static readonly ImmutableArray<string> Products = ["ExGrid", "ExSheet", "ExPivot", "Data"];

    /// <summary>What a Change Note says its change is.</summary>
    public static readonly ImmutableArray<string> Kinds = ["Added", "Fixed", "Behaviour", "Breaking"];

    /// <summary>The family's packages (ADR-0042), which a version's <c>Packages:</c> line names.</summary>
    public static readonly ImmutableArray<string> Packages =
    [
        "ExGrid", "ExGrid.MudBlazor",
        "ExSheet.Engine", "ExSheet", "ExSheet.MudBlazor",
        "ExGrid.Data", "ExGrid.Data.Arrow",
        "ExPivot.Engine", "ExPivot", "ExPivot.MudBlazor",
    ];

    /// <summary>A prerelease's headings, in order (ADR-0190).</summary>
    public static readonly ImmutableArray<string> Headings =
        ["Highlights", "Breaking changes", "Behaviour changes", "ExGrid", "ExSheet", "ExPivot", "Data"];

    /// <summary>
    /// The headings only a version without a suffix carries: Known limitations (ADR-0202), Signed off and
    /// SHOULD criteria accepted failing (ADR-0190, the Definition of Done's §1 and Step 7), Out of scope for
    /// this version and Supported (ADR-0205).
    /// </summary>
    public static readonly ImmutableArray<string> StableHeadings =
        ["Known limitations", "Signed off", "SHOULD criteria accepted failing", "Out of scope for this version", "Supported"];

    /// <summary>
    /// A version without a suffix's headings, in order: ADR-0190's, with Known limitations after Behaviour
    /// changes (ADR-0202), and the release's own blocks after the products (ADR-0190, ADR-0205).
    /// </summary>
    public static readonly ImmutableArray<string> StableOrder =
    [
        "Highlights", "Breaking changes", "Behaviour changes", "Known limitations",
        "ExGrid", "ExSheet", "ExPivot", "Data",
        "Signed off", "SHOULD criteria accepted failing", "Out of scope for this version", "Supported",
    ];

    /// <summary>
    /// The lines of a release's Signed off block, in order, each beginning with its label in bold (ADR-0205).
    /// The release workflow reads the commit X from the <c>Commit:</c> line and the record from <c>Record:</c>.
    /// </summary>
    public static readonly ImmutableArray<string> SignedOffLabels = ["Record", "Commit", "Toolchain", "OS", "Browsers"];

    /// <summary>The lines of a release's Supported block, in order, each beginning with its label in bold (ADR-0205).</summary>
    public static readonly ImmutableArray<string> SupportedLabels = ["Hosts", "Browsers", "Target framework", "Packages"];

    /// <summary>The prerelease note's file under <c>docs/releases/</c>.</summary>
    public const string PrereleaseNoteName = "prerelease-note.md";

    /// <summary>The folder of the Change Notes under <c>docs/releases/</c>.</summary>
    public const string UnreleasedFolder = "unreleased/";

    /// <summary>The route of the site's Release notes page.</summary>
    public const string Route = "release-notes";

    /// <summary>The anchor of the page's Unreleased section.</summary>
    public const string UnreleasedAnchor = "unreleased";

    /// <summary>What a product with nothing new says (ADR-0190).</summary>
    public const string NoChange = "No change. Its packages are republished at this version.";

    private const string Adr = "ADR-0190";

    /// <summary>A file breaks the notes' Markdown subset.</summary>
    public static readonly DiagnosticDescriptor OutsideSubset = new(
        id: "EXDOCS007",
        title: "Release Notes use Markdown outside their subset",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The notes are the site's page and the GitHub release's text, so they use only what both render alike: ## headings, bullets with one nested level, paragraphs, bold, code spans and links (ADR-0190).");

    /// <summary>A file breaks the notes' shape.</summary>
    public static readonly DiagnosticDescriptor WrongShape = new(
        id: "EXDOCS008",
        title: "Release Notes break their shape",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A version's notes name it on their first line, carry Published, Packages and Target, and every heading in order, each saying None. when it has nothing; a version without a suffix also carries Known limitations, Signed off, SHOULD criteria accepted failing, Out of scope for this version and Supported, and a prerelease none of them; a Change Note names its Product and Kind (ADR-0190, ADR-0202, ADR-0205).");

    /// <summary>A link leads nowhere.</summary>
    public static readonly DiagnosticDescriptor LinkNowhere = new(
        id: "EXDOCS009",
        title: "A Release Notes link leads nowhere",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An ADR the notes name has a file under docs/adr/, and a link into the site names a route and an anchor its pages have (ADR-0190).");

    /// <summary>The version this build publishes is not cut.</summary>
    public static readonly DiagnosticDescriptor NotCut = new(
        id: "EXDOCS010",
        title: "The version this build publishes is not cut",
        messageFormat: "{0}",
        category: "ExGrid.Docs",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A build given the version it publishes needs that version's notes, the newest, and no Change Note left: a tag publishes all of main, so its notes hold all of main (ADR-0190).");

    private static readonly Regex VersionPattern = new(
        @"^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant);

    private static readonly Regex HeaderLine = new(@"^([A-Za-z][A-Za-z ]*):(.*)$", RegexOptions.CultureInvariant);

    private static readonly Regex Slug = new(@"^[a-z0-9][a-z0-9-]*\.md$", RegexOptions.CultureInvariant);

    private static readonly Regex Target = new(@"^net[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant);

    private static readonly Regex AdrNumber = new(@"^[0-9]{4}$", RegexOptions.CultureInvariant);

    private static readonly Regex Lead = new(@"^\*\*([^*\n]+?):\*\*(?:[ \n]+|$)", RegexOptions.CultureInvariant);

    private static readonly Regex SignedOffRecord = new(@"verification/[^/\s`\]\[()]+/results\.md", RegexOptions.CultureInvariant);

    private static readonly Regex SignedOffCommit = new(@"^\s*\[?`([0-9A-Za-z]*)`", RegexOptions.CultureInvariant);

    private static readonly Regex FullCommit = new(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant);

    private static readonly Regex AnyFullCommit = new(@"(?<![0-9A-Za-z])[0-9a-f]{40}(?![0-9A-Za-z])", RegexOptions.CultureInvariant);

    private static readonly Regex CriterionId = new(@"^[A-Z][A-Z0-9]*-[0-9]+[a-z]?$", RegexOptions.CultureInvariant);

    private static readonly Regex Section21Item = new(@"§(21\.[0-9]+[a-z]?)(?![0-9A-Za-z])", RegexOptions.CultureInvariant);

    private static readonly Regex AnyLink = new(@"\]\([^)\s]+\)", RegexOptions.CultureInvariant);

    private static readonly Regex PackageToken = new(@"[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)*", RegexOptions.CultureInvariant);

    // The Definition of Done's criterion rows, `| **ID** | LEVEL | …`, and its §21 items, `### 21.N` and `| **21.N** |`.
    private static readonly Regex CriterionRow = new(@"^\| \*\*([A-Z][A-Z0-9]*-[0-9]+[a-z]?)\*\* \| *(MUST|SHOULD|OBSERVATIONAL)\b", RegexOptions.CultureInvariant);

    private static readonly Regex Section21Row = new(@"^(?:### |\| \*\*)(21\.[0-9]+[a-z]?)(?![0-9A-Za-z])", RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads, checks and emits the notes. <paramref name="pages"/> and <paramref name="adrs"/> are what
    /// links are checked against; <paramref name="build"/> says whether this build publishes a version,
    /// whose notes must then be cut. <paramref name="definitionOfDone"/> is what the build that publishes a
    /// release checks that release's SHOULD criteria accepted failing and its §21 items out of scope against.
    /// </summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<NotesInput> files,
        ImmutableArray<PageInput> pages,
        ImmutableDictionary<string, string> adrs,
        DocsBuildInfo build,
        SourceText? definitionOfDone = null)
    {
        var reader = new Reader(files, pages, adrs, build, definitionOfDone);
        reader.Run();
        return (reader.Diagnostics.ToImmutable(), Emit(reader));
    }

    // Where each heading only a version without a suffix carries comes from, and what it says.
    private static string StableSource(string heading) => heading switch
    {
        "Known limitations" => "ADR-0202",
        "Signed off" or "SHOULD criteria accepted failing" => "the Definition of Done's §1 and Step 7; ADR-0190, ADR-0205",
        _ => "ADR-0205",
    };

    private static string StableWhat(string heading) => heading switch
    {
        "Known limitations" => "what it does not do, each line linking the page of the site that says it in full, or None.",
        "Signed off" => "the verification/<date>/results.md its sign-off was made against, the commit X, and the toolchain, OS and browser versions used",
        "SHOULD criteria accepted failing" => "each SHOULD criterion that fails with the written reason it was accepted, or None.",
        "Out of scope for this version" => "each of the Definition of Done's §21 items accepted in writing as out of its scope, linked, or None.",
        _ => "the hosts, the browsers, the target framework and the packages it supports",
    };

    /// <summary>
    /// Orders two versions by SemVer 2 precedence: a prerelease before its release, and a prerelease's
    /// identifiers compared one by one, numbers as numbers.
    /// </summary>
    public static int CompareVersions(string a, string b)
    {
        var (coreA, preA) = Split(a);
        var (coreB, preB) = Split(b);
        for (var k = 0; k < 3; k++)
        {
            var order = CompareNumbers(coreA[k], coreB[k]);
            if (order != 0)
                return order;
        }
        if (preA.Length == 0 || preB.Length == 0)
            return (preA.Length == 0 ? 1 : 0) - (preB.Length == 0 ? 1 : 0);
        for (var k = 0; k < Math.Min(preA.Length, preB.Length); k++)
        {
            var numberA = preA[k].All(char.IsDigit);
            var numberB = preB[k].All(char.IsDigit);
            var order = numberA && numberB ? CompareNumbers(preA[k], preB[k])
                : numberA ? -1
                : numberB ? 1
                : string.CompareOrdinal(preA[k], preB[k]);
            if (order != 0)
                return Math.Sign(order);
        }
        return preA.Length.CompareTo(preB.Length);
    }

    private static (string[] Core, string[] Pre) Split(string version)
    {
        var dash = version.IndexOf('-');
        var core = (dash < 0 ? version : version.Substring(0, dash)).Split('.');
        var pre = dash < 0 ? [] : version.Substring(dash + 1).Split('.');
        return (core, pre);
    }

    private static int CompareNumbers(string a, string b)
    {
        a = a.TrimStart('0');
        b = b.TrimStart('0');
        return a.Length != b.Length ? a.Length.CompareTo(b.Length) : Math.Sign(string.CompareOrdinal(a, b));
    }

    /// <summary>A section of a version, as read.</summary>
    internal sealed record Section(string Title, string Kind, string? Product, bool IsNone, ImmutableArray<Item> Items);

    /// <summary>A bullet or a paragraph of a section, rendered.</summary>
    internal sealed record Item(string Html, string Text, ImmutableArray<string> Products, string Kind, ImmutableArray<string> Links, bool Bullet);

    /// <summary>A version's notes, as read.</summary>
    internal sealed record NotesVersion(
        NotesInput File, string Name, string? PublishedOn, int PublishedStart, int PublishedLength,
        ImmutableArray<string> Packages, string Target, ImmutableArray<Section> Sections);

    /// <summary>A Change Note, as read.</summary>
    internal sealed record NotesChange(
        NotesInput File, string Slug, string Product, string Kind, string? Docs, ImmutableArray<(string Number, string Url)> Adrs,
        string Html, string Text, ImmutableArray<string> Links);

    private sealed class Reader(
        ImmutableArray<NotesInput> files,
        ImmutableArray<PageInput> pages,
        ImmutableDictionary<string, string> adrs,
        DocsBuildInfo build,
        SourceText? definitionOfDone)
    {
        // The Definition of Done's criteria by ID with their level, and its §21 items; null when it did not reach the build.
        private readonly Dictionary<string, string>? _criteria = definitionOfDone is null ? null : ReadCriteria(definitionOfDone);

        private readonly HashSet<string>? _section21 = definitionOfDone is null ? null : ReadSection21(definitionOfDone);

        private bool _toldNoDefinitionOfDone;

        public ImmutableArray<Diagnostic>.Builder Diagnostics { get; } = ImmutableArray.CreateBuilder<Diagnostic>();

        public List<NotesVersion> Versions { get; } = [];

        public List<NotesChange> Changes { get; } = [];

        public string PrereleaseNote { get; private set; } = "";

        private Dictionary<string, HashSet<string>> _site = new(StringComparer.OrdinalIgnoreCase);

        private NotesContext Context(bool allowRef) => new(adrs, build.GitRef, allowRef);

        public void Run()
        {
            if (files.IsEmpty)
            {
                // Nothing reached the build. Only a site with a Release notes page needs the notes.
                if (pages.Any(p => p.Scan.Page.Routes.Contains(Route, StringComparer.OrdinalIgnoreCase)))
                {
                    Diagnostics.Add(Diagnostic.Create(NotCut, Location.None,
                        $"The site has a Release notes page (/{Route}), and no file under docs/releases/ reached the build. "
                        + $"ExGrid.Docs.csproj hands them over as AdditionalFiles; restore that item ({Adr})."));
                }
                return;
            }

            var versionNames = files
                .Select(f => f.Name)
                .Where(n => n.EndsWith(".md", StringComparison.Ordinal) && n.IndexOf('/') < 0 && VersionPattern.IsMatch(n.Substring(0, n.Length - 3)))
                .Select(n => n.Substring(0, n.Length - 3))
                .ToList();
            BuildSite(versionNames);

            foreach (var file in files.OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                var problems = new List<NotesProblem>();
                if (file.Name == PrereleaseNoteName)
                    ReadPrereleaseNote(file, problems);
                else if (file.Name.StartsWith(UnreleasedFolder, StringComparison.Ordinal))
                    ReadChange(file, problems);
                else if (file.Name.EndsWith(".md", StringComparison.Ordinal) && VersionPattern.IsMatch(file.Name.Substring(0, file.Name.Length - 3)))
                    ReadVersion(file, problems);
                else
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, 1,
                        $"docs/releases/ holds a version's notes (<version>.md, as 0.1.0-beta.2.md), the prerelease note ({PrereleaseNoteName}) "
                        + $"and Change Notes ({UnreleasedFolder}<slug>.md), and {file.Name} is none of them. Rename it or move it out ({Adr})."));
                }
                Report(file, problems);
            }

            CheckAcross();
        }

        private static Dictionary<string, string> ReadCriteria(SourceText text)
        {
            var criteria = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in text.Lines)
            {
                if (CriterionRow.Match(line.ToString()) is { Success: true } row && !criteria.ContainsKey(row.Groups[1].Value))
                    criteria[row.Groups[1].Value] = row.Groups[2].Value;
            }
            return criteria;
        }

        private static HashSet<string> ReadSection21(SourceText text)
        {
            var items = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in text.Lines)
            {
                if (Section21Row.Match(line.ToString()) is { Success: true } row)
                    items.Add(row.Groups[1].Value);
            }
            return items;
        }

        // A release's SHOULD criteria and §21 items are read against the Definition of Done by the build that
        // publishes it; when it did not reach that build they cannot be, and that is said once rather than passed.
        private bool HasDefinitionOfDone(int start, int length, List<NotesProblem> problems)
        {
            if (definitionOfDone is not null)
                return true;
            if (!_toldNoDefinitionOfDone)
            {
                _toldNoDefinitionOfDone = true;
                problems.Add(new NotesProblem(NotesProblemKind.Shape, start, length,
                    "docs/definition-of-done.md did not reach the build, so the criteria and §21 items this release names cannot be checked. "
                    + "ExGrid.Docs.csproj hands it over as AdditionalFiles; restore that item (ADR-0190)."));
            }
            return false;
        }

        private void Report(NotesInput file, List<NotesProblem> problems)
        {
            foreach (var problem in problems.OrderBy(p => p.Start))
            {
                var descriptor = problem.Kind switch
                {
                    NotesProblemKind.Subset => OutsideSubset,
                    NotesProblemKind.Link => LinkNowhere,
                    NotesProblemKind.Cut => NotCut,
                    _ => WrongShape,
                };
                Diagnostics.Add(Diagnostic.Create(descriptor, At(file, problem.Start, problem.Length), problem.Message));
            }
        }

        // Each route of the site with the anchors its sections have. The Release notes page's anchors are
        // its versions' and its Unreleased section's, which it renders from the notes.
        private void BuildSite(List<string> versionNames)
        {
            foreach (var page in pages)
            {
                foreach (var route in page.Scan.Page.Routes)
                {
                    if (!_site.TryGetValue(route, out var anchors))
                        _site[route] = anchors = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var section in page.Scan.Page.Sections)
                        anchors.Add(section.Anchor);
                    if (string.Equals(route, Route, StringComparison.OrdinalIgnoreCase))
                    {
                        anchors.Add(UnreleasedAnchor);
                        foreach (var version in versionNames)
                            anchors.Add(DocsAnchor.Of(version));
                    }
                }
            }
        }

        // A link into the site names a route a page has, and an anchor that page has.
        private void CheckSiteLink(string target, int start, int length, List<NotesProblem> problems)
        {
            var hash = target.IndexOf('#');
            var route = SiteMap.RouteOf(target);
            var anchor = hash < 0 ? null : target.Substring(hash + 1);
            if (!_site.TryGetValue(route, out var anchors))
            {
                problems.Add(new NotesProblem(NotesProblemKind.Link, start, length,
                    $"No page of the site has the route /{route}. Link a page the site has, as exgrid/selection#status-bar-figures, or write a whole https:// address."));
                return;
            }
            if (anchor is not null && !anchors.Contains(anchor))
            {
                var some = anchors.OrderBy(a => a, StringComparer.Ordinal).Take(8).Select(a => "#" + a);
                problems.Add(new NotesProblem(NotesProblemKind.Link, start, length,
                    $"The page /{route} has no section #{anchor}. Its sections are {string.Join(", ", some)}{(anchors.Count > 8 ? ", …" : "")}."));
            }
        }

        private NotesInline Render(NotesText text, List<NotesProblem> problems, bool allowRef = false, int from = 0)
        {
            var inline = NotesMarkdown.Render(text, Context(allowRef), problems, from);
            foreach (var link in inline.Links.Where(l => !l.External && !l.Target.Contains("://") && !l.Target.StartsWith("/", StringComparison.Ordinal)))
                CheckSiteLink(link.Target, link.Start, link.Length, problems);
            return inline;
        }

        // Header lines in the tickets' Status: style, from line `from` to the first blank line.
        private static List<(string Key, string Value, int Start, int ValueStart, int Line)> ReadHeaders(
            SourceText text, ref int line, List<NotesProblem> problems)
        {
            var headers = new List<(string, string, int, int, int)>();
            for (; line < text.Lines.Count; line++)
            {
                var current = text.Lines[line];
                var raw = current.ToString();
                if (raw.Trim().Length == 0)
                    break;
                var match = HeaderLine.Match(raw);
                if (!match.Success)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, current.Start, raw.Length,
                        "The header lines are Key: value, one to a line, and a blank line ends them. Write this as a header line, or leave a blank line before it."));
                    continue;
                }
                var value = match.Groups[2].Value;
                var valueStart = current.Start + match.Groups[2].Index + (value.Length - value.TrimStart().Length);
                headers.Add((match.Groups[1].Value, value.Trim(), current.Start, valueStart, line + 1));
            }
            return headers;
        }

        private static int SkipBlank(SourceText text, int line)
        {
            while (line < text.Lines.Count && text.Lines[line].ToString().Trim().Length == 0)
                line++;
            return line;
        }

        private void ReadPrereleaseNote(NotesInput file, List<NotesProblem> problems)
        {
            var blocks = NotesMarkdown.ReadBlocks(file.Text, 0, problems);
            var html = new StringBuilder();
            var sawRef = false;
            foreach (var block in blocks)
            {
                if (block is not NotesParagraph paragraph)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                        "The prerelease note is a paragraph or two, which every prerelease's notes begin with: no heading and no bullet."));
                    continue;
                }
                sawRef |= paragraph.Text.Text.Contains("{ref}");
                html.Append("<p>").Append(Render(paragraph.Text, problems, allowRef: true).Html).Append("</p>");
            }
            if (blocks.IsEmpty)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, 1,
                    "The prerelease note is empty. It says what ADR-0042 promises of a prerelease, with {ref} where the commit goes."));
            }
            else if (!sawRef)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, 1,
                    "The prerelease note names no {ref}: it links what the Definition of Done still owes at the commit a version was built from, "
                    + "as https://github.com/AkiraMisawa/ex-grid/blob/{ref}/docs/implementation-status.md (ADR-0042)."));
            }
            PrereleaseNote = html.ToString();
        }

        private void ReadChange(NotesInput file, List<NotesProblem> problems)
        {
            var name = file.Name.Substring(UnreleasedFolder.Length);
            if (!Slug.IsMatch(name))
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, 1,
                    $"A Change Note is named for its change in lower case, as {UnreleasedFolder}row-key.md, directly under {UnreleasedFolder}; {file.Name} is not."));
            }
            var line = 0;
            var headers = ReadHeaders(file.Text, ref line, problems);
            string? product = null, kind = null, docs = null;
            var adrList = ImmutableArray.CreateBuilder<(string, string)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (key, value, start, valueStart, _) in headers)
            {
                if (!seen.Add(key))
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, key.Length + 1, $"{key}: is written twice. A Change Note has one of each header line."));
                    continue;
                }
                switch (key)
                {
                    case "Product":
                        product = value;
                        if (!Products.Contains(value))
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.Length),
                                $"Product is one of {string.Join(", ", Products)}, and '{value}' is not. A change that reaches two products has a note for each."));
                        }
                        break;
                    case "Kind":
                        kind = value;
                        if (!Kinds.Contains(value))
                        {
                            var hint = value == "Behavior" ? " (British spelling: Behaviour)" : "";
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.Length),
                                $"Kind is one of {string.Join(", ", Kinds)}, and '{value}' is not{hint}."));
                        }
                        break;
                    case "Docs":
                        docs = value;
                        if (value.Length == 0 || value.Any(char.IsWhiteSpace) || value.Contains("://") || value.StartsWith("/", StringComparison.Ordinal))
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Link, valueStart, Math.Max(1, value.Length),
                                "Docs: is one route of the site and its anchor, as exgrid/selection#status-bar-figures, without a leading slash. Leave the line out when no page shows the change."));
                        }
                        else
                        {
                            CheckSiteLink(value, valueStart, value.Length, problems);
                        }
                        break;
                    case "ADR":
                        var offset = 0;
                        foreach (var part in value.Split(','))
                        {
                            var number = part.Trim();
                            var at = valueStart + offset + (part.Length - part.TrimStart().Length);
                            offset += part.Length + 1;
                            if (number.StartsWith("ADR-", StringComparison.Ordinal))
                                number = number.Substring(4);
                            if (!AdrNumber.IsMatch(number))
                            {
                                problems.Add(new NotesProblem(NotesProblemKind.Shape, at, Math.Max(1, part.Trim().Length),
                                    $"ADR: lists the decisions' four-digit numbers, as 0130 or 0140, 0141; '{part.Trim()}' is not one."));
                            }
                            else if (!adrs.TryGetValue(number, out var adrFile))
                            {
                                problems.Add(new NotesProblem(NotesProblemKind.Link, at, part.Trim().Length,
                                    $"ADR-{number} names no decision record: docs/adr/ has no file {number}-….md. Correct the number."));
                            }
                            else
                            {
                                adrList.Add((number, $"{NotesMarkdown.Repository}/blob/{build.GitRef}/docs/adr/{adrFile}"));
                            }
                        }
                        break;
                    default:
                        problems.Add(new NotesProblem(NotesProblemKind.Shape, start, key.Length + 1,
                            $"{key}: is not a Change Note's header line. They are Product:, Kind:, Docs: and ADR: ({Adr})."));
                        break;
                }
            }
            if (product is null)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, 1,
                    $"This Change Note names no Product: write Product: and one of {string.Join(", ", Products)} on its first lines."));
            }
            if (kind is null)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, 1,
                    $"This Change Note names no Kind: write Kind: and one of {string.Join(", ", Kinds)} on its first lines."));
            }

            var blocks = NotesMarkdown.ReadBlocks(file.Text, line, problems);
            NotesInline? body = null;
            foreach (var block in blocks)
            {
                if (block is NotesParagraph paragraph && body is null)
                {
                    body = Render(paragraph.Text, problems);
                    continue;
                }
                problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                    "A Change Note is one paragraph of a sentence or two, after its header lines and a blank line: no heading, no bullet and no second paragraph."));
            }
            if (body is null)
            {
                var end = file.Text.Length;
                problems.Add(new NotesProblem(NotesProblemKind.Shape, Math.Max(0, end - 1), 1,
                    "This Change Note says nothing. After its header lines and a blank line, say in a sentence or two what changed — and, when it breaks or behaves differently, what a Consumer changes or will see."));
            }
            if (product is not null && kind is not null && body is not null)
            {
                var links = body.Links.Where(l => !l.External).Select(l => l.Target).ToList();
                if (docs is not null)
                    links.Insert(0, docs);
                Changes.Add(new NotesChange(file, name.Substring(0, Math.Max(0, name.Length - 3)), product, kind, docs, adrList.ToImmutable(),
                    body.Html, body.Text, links.Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray()));
            }
        }

        private void ReadVersion(NotesInput file, List<NotesProblem> problems)
        {
            var name = file.Name.Substring(0, file.Name.Length - 3);
            var text = file.Text;
            var stable = name.IndexOf('-') < 0;
            var first = text.Lines.Count > 0 ? text.Lines[0].ToString() : "";
            if (first != "# " + name)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, 0, Math.Max(1, first.Length),
                    $"A version's notes begin with the line '# {name}', the file's name, and this one begins '{first}' ({Adr})."));
            }

            var line = SkipBlank(text, 1);
            var headers = ReadHeaders(text, ref line, problems);
            string? published = null;
            int publishedStart = 0, publishedLength = 0;
            var packages = ImmutableArray<string>.Empty;
            var target = "";
            var expected = new[] { "Published", "Packages", "Target" };
            var position = 0;
            foreach (var (key, value, start, valueStart, _) in headers)
            {
                var index = Array.IndexOf(expected, key);
                if (index < 0)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, key.Length + 1,
                        $"{key}: is not a version's header line. They are Published:, Packages: and Target:, in that order ({Adr})."));
                    continue;
                }
                if (index < position)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, key.Length + 1,
                        $"{key}: is written twice, or out of order. A version's header lines are Published:, Packages: and Target:, in that order."));
                    continue;
                }
                for (var missing = position; missing < index; missing++)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, key.Length + 1,
                        $"{expected[missing]}: is missing before {key}:. A version's header lines are Published:, Packages: and Target:, in that order."));
                }
                position = index + 1;
                switch (key)
                {
                    case "Published":
                        publishedStart = valueStart;
                        publishedLength = Math.Max(1, value.Length);
                        if (value == "pending")
                        {
                            published = null;
                        }
                        else if (Regex.IsMatch(value, "^[0-9]{4}-[0-9]{2}-[0-9]{2}$")
                                 && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        {
                            published = value;
                        }
                        else
                        {
                            published = null;
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, publishedLength,
                                $"Published: is the UTC day the release pushed the packages to nuget.org, as 2026-10-04, or pending until the next cut writes it; '{value}' is neither ({Adr})."));
                        }
                        break;
                    case "Packages":
                        var names = value.Split(',').Select(p => p.Trim()).ToList();
                        foreach (var package in names)
                        {
                            if (!Packages.Contains(package))
                            {
                                problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.Length),
                                    $"Packages: names the package IDs the version published, from the family's ten (ADR-0042); '{package}' is not one."));
                            }
                        }
                        if (names.Distinct().Count() != names.Count)
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.Length), "Packages: names a package twice."));
                        packages = names.Where(n => n.Length > 0).ToImmutableArray();
                        break;
                    case "Target":
                        target = value;
                        if (!Target.IsMatch(value))
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.Length),
                                $"Target: is the packages' target framework, as net10.0 (ADR-0022); '{value}' is not one."));
                        }
                        break;
                }
            }
            var headerEnd = line < text.Lines.Count ? text.Lines[line].Start : text.Length;
            for (var missing = position; missing < expected.Length; missing++)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, Math.Max(0, headerEnd - 1), 1,
                    $"{expected[missing]}: is missing. A version's notes carry Published:, Packages: and Target: after their first line ({Adr})."));
            }

            var blocks = NotesMarkdown.ReadBlocks(text, line, problems);
            var header = new VersionHeader(name, stable, packages, Target.IsMatch(target) ? target : null, Publishing: build.Version == name);
            var sections = ReadSections(file, blocks, header, problems);
            Versions.Add(new NotesVersion(file, name, published, publishedStart, publishedLength, packages, target, sections));
        }

        // What the sections of a version are read against: its name, whether it is a release, its header lines, and
        // whether this build publishes it. Only that build reads a release's criteria and §21 items against the
        // Definition of Done: at the tag the Definition of Done is the one the release was signed off against, and a
        // version already published stays true of its own day however the Definition of Done is restated after it.
        private sealed record VersionHeader(string Name, bool Stable, ImmutableArray<string> Packages, string? Target, bool Publishing);

        private static string Missing(string heading, string? before, bool stable)
        {
            var where = before is null ? "" : $" before '## {before}'";
            return stable && StableHeadings.Contains(heading)
                ? $"'## {heading}' is missing{where}. A version without a suffix is a release, and its notes say {StableWhat(heading)} ({StableSource(heading)})."
                : $"'## {heading}' is missing{where}. Every heading is present, in order, and one with nothing under it says None. ({Adr}).";
        }

        private ImmutableArray<Section> ReadSections(NotesInput file, ImmutableArray<NotesBlock> blocks, VersionHeader header, List<NotesProblem> problems)
        {
            var stable = header.Stable;
            var expected = stable ? StableOrder : Headings;
            var groups = new List<(NotesHeading Heading, List<NotesBlock> Blocks)>();
            foreach (var block in blocks)
            {
                if (block is NotesHeading heading)
                    groups.Add((heading, []));
                else if (groups.Count == 0)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                        "Everything after the header lines sits under a heading, and the first is ## Highlights. Move this under the heading it belongs to."));
                }
                else
                {
                    groups[groups.Count - 1].Blocks.Add(block);
                }
            }

            var sections = ImmutableArray.CreateBuilder<Section>();
            var next = 0;
            foreach (var (heading, content) in groups)
            {
                var index = expected.IndexOf(heading.Title);
                if (index < 0)
                {
                    var why = !stable && StableHeadings.Contains(heading.Title)
                        ? $"Only a version without a suffix carries '## {heading.Title}' ({StableSource(heading.Title)}). A prerelease is not signed off, "
                          + "and its prerelease note links what the Definition of Done still owes: remove the heading."
                        : $"'## {heading.Title}' is not one of the notes' headings, which are {string.Join(", ", expected)}, in that order ({Adr}).";
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, heading.Title.Length + 3, why));
                    continue;
                }
                if (index < next)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, heading.Title.Length + 3,
                        $"'## {heading.Title}' is written twice, or out of order: the headings are {string.Join(", ", expected)}, in that order ({Adr})."));
                    continue;
                }
                for (var missing = next; missing < index; missing++)
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, heading.Title.Length + 3, Missing(expected[missing], heading.Title, stable)));
                next = index + 1;
                sections.Add(ReadSection(heading, content, header, problems));
            }
            for (var missing = next; missing < expected.Length; missing++)
                problems.Add(new NotesProblem(NotesProblemKind.Shape, Math.Max(0, file.Text.Length - 1), 1, Missing(expected[missing], null, stable)));
            return sections.ToImmutable();
        }

        private Section ReadSection(NotesHeading heading, List<NotesBlock> content, VersionHeader header, List<NotesProblem> problems)
        {
            var title = heading.Title;
            var product = Products.Contains(title) ? title : null;
            var kind = title switch
            {
                "Highlights" => "Highlights",
                "Breaking changes" => "Breaking",
                "Behaviour changes" => "Behaviour",
                "Known limitations" => "KnownLimitations",
                "Signed off" => "SignedOff",
                "SHOULD criteria accepted failing" => "ShouldAccepted",
                "Out of scope for this version" => "OutOfScope",
                "Supported" => "Supported",
                _ => "Product",
            };
            var headingSpan = title.Length + 3;
            // Signed off and Supported are never empty: a release was signed off on something, and supports something.
            var labelled = kind switch
            {
                "SignedOff" => SignedOffLabels,
                "Supported" => SupportedLabels,
                _ => ImmutableArray<string>.Empty,
            };

            if (content.Count == 0)
            {
                var say = product is null ? "None." : NoChange;
                problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, headingSpan, labelled.IsEmpty
                    ? $"'## {title}' has nothing under it. Write '{say}', so a reader can tell nothing from not written ({Adr})."
                    : $"'## {title}' has nothing under it. Write its lines, {LabelList(labelled)}, one each, in that order ({StableSource(title)})."));
                return new Section(title, kind, product, false, []);
            }
            if (content.Count == 1 && content[0] is NotesParagraph only && only.Text.Text.Trim() == "None.")
            {
                if (product is not null)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, only.Start, 5,
                        $"A product with nothing new says '{NoChange}' ({Adr})."));
                }
                if (kind == "SignedOff")
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, only.Start, 5,
                        $"A release is signed off: write its lines, {LabelList(labelled)}, naming the verification/<date>/results.md the sign-off was made against and the commit X (the Definition of Done's Step 7; ADR-0205)."));
                }
                if (kind == "Supported")
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, only.Start, 5,
                        $"A release supports something: write its lines, {LabelList(labelled)} (ADR-0205)."));
                }
                return new Section(title, kind, product, true, []);
            }

            var items = ImmutableArray.CreateBuilder<Item>();
            if (!labelled.IsEmpty)
            {
                ReadLabelled(heading, content, labelled, header, items, problems);
                return new Section(title, kind, product, false, items.ToImmutable());
            }
            var highlights = 0;
            var accepted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var block in content)
            {
                if (block is NotesParagraph paragraph && paragraph.Text.Text.Trim() == "None.")
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, paragraph.Start, 5,
                        "None. stands alone: a heading with something under it does not also say None."));
                    continue;
                }
                switch (kind)
                {
                    case "Highlights":
                        if (block is not NotesBullet highlight)
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                                "The Highlights are bullets, at most five, each linking the page of the site that shows it."));
                            continue;
                        }
                        highlights++;
                        if (highlights == 6)
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, highlight.Start, 1,
                                $"The Highlights are at most five lines ({Adr}). Keep the five a reader most needs; the rest are under their products."));
                        }
                        var rendered = Bullet(highlight, 0, problems);
                        if (rendered.Links.IsEmpty)
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, highlight.Start, 1,
                                "Each Highlight links the page of the site that shows it, as [Status-bar figures](exgrid/selection#status-bar-figures)."));
                        }
                        items.Add(rendered);
                        break;
                    case "Breaking" or "Behaviour" or "KnownLimitations":
                        if (block is not NotesBullet change)
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                                $"Each of the {title} is a bullet that begins with the products it reaches in bold, as - **ExGrid:** or - **ExGrid, ExSheet:**."));
                            continue;
                        }
                        var lead = Lead.Match(change.Text.Text);
                        var named = lead.Success ? lead.Groups[1].Value.Split(',').Select(p => p.Trim()).ToList() : [];
                        if (!lead.Success || named.Count == 0 || named.Any(p => !Products.Contains(p)))
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, change.Start, 1,
                                $"Each of the {title} begins with the products it reaches in bold, from {string.Join(", ", Products)}, as - **ExGrid:** or - **ExGrid, ExSheet:**, so the page can show it under each."));
                            items.Add(Bullet(change, 0, problems));
                            continue;
                        }
                        var reached = Bullet(change, lead.Length, problems) with
                        {
                            Products = named.ToImmutableArray(),
                            // A known limitation is not a change, so it gives the page it links no "Changed in" chip.
                            Kind = kind == "KnownLimitations" ? "" : kind,
                        };
                        if (kind == "KnownLimitations" && reached.Links.IsEmpty)
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, change.Start, 1,
                                "Each of the Known limitations names the limitation in a sentence and links the page of the site that says it in full, "
                                + "as - **ExSheet:** `IRR` is `#NAME?` ([Functions](exsheet/functions)) (ADR-0202)."));
                        }
                        items.Add(reached);
                        break;
                    case "ShouldAccepted":
                        if (ReadAccepted(block, header, accepted, problems) is { } should)
                            items.Add(should);
                        break;
                    case "OutOfScope":
                        if (ReadOutOfScope(block, header, problems) is { } outOfScope)
                            items.Add(outOfScope);
                        break;
                    case "Product":
                        if (block is NotesParagraph note)
                        {
                            items.Add(Paragraph(note, problems) with { Products = [product!] });
                            break;
                        }
                        if (block is not NotesBullet line)
                            continue;
                        var label = Lead.Match(line.Text.Text);
                        var what = label.Success ? label.Groups[1].Value : "";
                        if (what is not ("Added" or "Fixed"))
                        {
                            var why = what is "Behaviour" or "Breaking"
                                ? $"A {what.ToLowerInvariant()} change goes under '## {(what == "Breaking" ? "Breaking changes" : "Behaviour changes")}', beginning with its products in bold."
                                : $"Each line under '## {title}' begins with what it is, **Added:** or **Fixed:** ({Adr}).";
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, line.Start, 1, why));
                            items.Add(Bullet(line, 0, problems) with { Products = [product!] });
                            continue;
                        }
                        items.Add(Bullet(line, label.Length, problems) with { Products = [product!], Kind = what });
                        break;
                }
            }
            return new Section(title, kind, product, false, items.ToImmutable());
        }

        private static string LabelList(ImmutableArray<string> labels) =>
            string.Join(", ", labels.Take(labels.Length - 1).Select(l => $"**{l}:**")) + $" and **{labels[labels.Length - 1]}:**";

        // What a labelled line says when it is missing or empty: what to write, and why.
        private static string LabelHint(string section, string label, VersionHeader header) => (section, label) switch
        {
            ("Signed off", "Record") =>
                $"Write - **Record:** [verification/<date>/results.md]({NotesMarkdown.Repository}/blob/v{header.Name}/verification/<date>/results.md), "
                + "the record the sign-off was made against, linked at this version's tag (the Definition of Done's Step 7; ADR-0205).",
            ("Signed off", "Commit") =>
                "Write - **Commit:** and the commit X every step of §22 ran at, its 40 characters in a code span: the release workflow reads it, "
                + "and refuses a tag that differs from it outside verification/ and docs/releases/ (ADR-0205).",
            ("Signed off", "Toolchain") => "Name the versions of the toolchain the sign-off ran with: the .NET SDK, Node and Playwright (the Definition of Done's Step 7).",
            ("Signed off", "OS") => "Name each operating system the sign-off ran on, with its version (the Definition of Done's Step 7).",
            ("Signed off", _) => "Name each browser the sign-off ran, with its version (the Definition of Done's Step 7).",
            ("Supported", "Hosts") => "Name the hosts it supports, as WebAssembly and Blazor Server (ADR-0205).",
            ("Supported", "Browsers") => "Name the browsers it supports, as Chrome and Edge (ADR-0017, ADR-0205).",
            ("Supported", "Target framework") => $"Name its target framework, as its Target: line does{(header.Target is null ? "" : ": " + header.Target)} (ADR-0205).",
            _ => "Name the packages it supports: those its Packages: line names (ADR-0205).",
        };

        // Signed off and Supported: bullets, each beginning with its label in bold, one each, in order. Each label's
        // line is checked for what it must say.
        private void ReadLabelled(NotesHeading heading, List<NotesBlock> content, ImmutableArray<string> labels, VersionHeader header,
            ImmutableArray<Item>.Builder items, List<NotesProblem> problems)
        {
            var title = heading.Title;
            var source = StableSource(title);
            var next = 0;
            NotesBullet? recordLine = null;
            foreach (var block in content)
            {
                if (block is not NotesBullet bullet)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                        block is NotesParagraph { Text.Text: var none } && none.Trim() == "None."
                            ? $"'## {title}' is never None.: write its lines, {LabelList(labels)} ({source})."
                            : $"'## {title}' holds bullets, each beginning with its label in bold, {LabelList(labels)}, one each, in that order ({source}). Make this one of them, or leave it out."));
                    continue;
                }
                var lead = Lead.Match(bullet.Text.Text);
                var label = lead.Success ? lead.Groups[1].Value : null;
                var index = label is null ? -1 : labels.IndexOf(label);
                var links = new List<NotesLink>();
                var item = Bullet(bullet, 0, problems, links);
                items.Add(item);
                if (index < 0)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, 1,
                        $"Each line under '## {title}' begins with its label in bold, {LabelList(labels)}, one each, in that order ({source}); "
                        + (label is null ? "this one begins with none." : $"**{label}:** is not one of them.")));
                    continue;
                }
                if (index < next)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, 1,
                        $"**{label}:** is written twice, or out of order: the lines under '## {title}' are {LabelList(labels)}, in that order ({source})."));
                    continue;
                }
                for (var missing = next; missing < index; missing++)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, 1,
                        $"**{labels[missing]}:** is missing before **{label}:**. {LabelHint(title, labels[missing], header)}"));
                }
                next = index + 1;

                var value = bullet.Text.Text.Substring(lead.Length);
                var valueStart = bullet.Text.OffsetOf(lead.Length);
                var plain = Regex.Replace(item.Text, @"^\s*" + Regex.Escape(label + ":") + @"\s*", "");
                if (value.Trim().Length == 0 && bullet.Children.IsEmpty)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, Math.Max(1, lead.Length),
                        $"**{label}:** says nothing. {LabelHint(title, label!, header)}"));
                    continue;
                }
                switch ((title, label))
                {
                    case ("Signed off", "Record"):
                        recordLine = bullet;
                        CheckRecord(bullet, plain, links, header, valueStart, Math.Max(1, value.TrimEnd().Length), problems);
                        break;
                    case ("Signed off", "Commit"):
                        var commit = SignedOffCommit.Match(value);
                        if (!commit.Success || !FullCommit.IsMatch(commit.Groups[1].Value))
                        {
                            var said = commit.Success ? $"'{commit.Groups[1].Value}' is not one" : "this line names none";
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.TrimEnd().Length),
                                $"**Commit:** is the commit X the sign-off was made against, its full 40 characters in lower case in a code span, as "
                                + $"`0123456789abcdef0123456789abcdef01234567`, and {said}. The release workflow reads it, and refuses a tag that differs from it "
                                + "outside verification/ and docs/releases/ (ADR-0205)."));
                        }
                        break;
                    case ("Supported", "Target framework"):
                        if (header.Target is { } target && !Regex.IsMatch(plain, @"(?<![0-9A-Za-z.])" + Regex.Escape(target) + @"(?![0-9A-Za-z])"))
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.TrimEnd().Length),
                                $"**Target framework:** names {target}, as the Target: line does; the two cannot say different things (ADR-0205)."));
                        }
                        break;
                    case ("Supported", "Packages"):
                        var named = new HashSet<string>(PackageToken.Matches(plain).Cast<Match>().Select(m => m.Value).Where(p => Packages.Contains(p)), StringComparer.Ordinal);
                        var left = header.Packages.Where(p => !named.Contains(p)).ToList();
                        var extra = named.Where(p => !header.Packages.Contains(p)).OrderBy(p => Packages.IndexOf(p)).ToList();
                        if (left.Count > 0 || extra.Count > 0)
                        {
                            var why = left.Count > 0
                                ? $"leaves out {string.Join(", ", left)}, which the Packages: line names"
                                : $"names {string.Join(", ", extra)}, which the Packages: line does not";
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, Math.Max(1, value.TrimEnd().Length),
                                $"**Packages:** {why}. The two name the same packages, by their IDs (ADR-0205)."));
                        }
                        break;
                }
            }
            for (var missing = next; missing < labels.Length; missing++)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, title.Length + 3,
                    $"'## {title}' names no **{labels[missing]}:**. {LabelHint(title, labels[missing], header)}"));
            }
            if (title == "Signed off")
                CheckSignedOffNamesOne(content, recordLine, problems);
        }

        // The release workflow reads the whole Signed off block for the one record and the one commit X it names
        // (ADR-0205), so a second of either anywhere in it, a link's address included, is refused here first. The
        // record named on the Record line itself is CheckRecord's; a nested bullet that merely begins **Record:**
        // is another line, read here like any other.
        private static void CheckSignedOffNamesOne(List<NotesBlock> content, NotesBullet? recordLine, List<NotesProblem> problems)
        {
            var lines = content.OfType<NotesBullet>().SelectMany(b => b.Children.Prepend(b)).ToList();
            var commits = new List<(string Value, int Start)>();
            foreach (var line in lines)
            {
                foreach (Match match in AnyFullCommit.Matches(line.Text.Text))
                    commits.Add((match.Value, line.Text.OffsetOf(match.Index)));
            }
            if (commits.Select(c => c.Value).Distinct(StringComparer.Ordinal).Skip(1).FirstOrDefault() is { } second)
            {
                var at = commits.First(c => c.Value == second);
                problems.Add(new NotesProblem(NotesProblemKind.Shape, at.Start, second.Length,
                    $"'## Signed off' names a second commit, {second}: the release workflow reads the one commit X the block names, "
                    + "and refuses a block that names two. Name X once, on its **Commit:** line (ADR-0205)."));
            }
            foreach (var line in lines.Where(l => !ReferenceEquals(l, recordLine)))
            {
                if (SignedOffRecord.Match(line.Text.Text) is { Success: true } record)
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, line.Text.OffsetOf(record.Index), record.Length,
                        $"{record.Value} is named outside the **Record:** line: the release workflow reads the one record the block names, "
                        + "and refuses a block that names two. Name the record once, on its **Record:** line (ADR-0205)."));
                }
            }
        }

        // The Record line names one verification/<dir>/results.md and links it at this version's tag: the record is
        // added after the commit it signs off, so it exists at the tag and not at X (ADR-0205). The line is read twice:
        // as it renders, for the record it names, and as it is written, as the release workflow reads it, where a
        // link's address and a backslash escape are text too. The two must find the same one record.
        private static void CheckRecord(NotesBullet line, string plain, List<NotesLink> links, VersionHeader header, int valueStart, int span, List<NotesProblem> problems)
        {
            var records = SignedOffRecord.Matches(plain).Cast<Match>().Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();
            if (records.Count != 1)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, span,
                    (records.Count == 0 ? "**Record:** names no verification/<date>/results.md. " : $"**Record:** names {records.Count} records, {string.Join(", ", records)}, and a sign-off is made against one. ")
                    + LabelHint("Signed off", "Record", header)));
                return;
            }
            var expected = $"{NotesMarkdown.Repository}/blob/v{header.Name}/{records[0]}";
            foreach (var written in SignedOffRecord.Matches(line.Text.Text).Cast<Match>()
                .Where(m => m.Value != records[0]).GroupBy(m => m.Value, StringComparer.Ordinal).Select(g => g.First()))
            {
                var at = line.Text.OffsetOf(written.Index);
                var inAddress = links.Any(l => at >= l.Start && at < l.Start + l.Length);
                problems.Add(new NotesProblem(NotesProblemKind.Shape, at, written.Length,
                    (inAddress
                        ? $"**Record:** names a second record, {written.Value}, in a link's address. "
                        : $"**Record:** writes {written.Value}, which the site renders as {records[0]}, and the release workflow reads what is written: to it, that is a second record. ")
                    + "The workflow reads the block as it is written, link addresses, escapes and entities included, and refuses a block that names two records. "
                    + $"Link only the record, as [{records[0]}]({expected}), and write its path plainly (ADR-0205)."));
            }
            var link = links.FirstOrDefault(l => l.External);
            if (link is null)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, valueStart, span,
                    $"**Record:** links the record at this version's tag, as [{records[0]}]({expected}): it is added after the commit X it signs off, "
                    + "so it is read at the tag (ADR-0205)."));
            }
            else if (link.Target != expected)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Link, link.Start, link.Length,
                    $"This link leads to {link.Target}, and the record this line names is {expected}, at this version's tag. Link that address (ADR-0205)."));
            }
        }

        // A SHOULD criterion accepted failing: a bullet that begins with its ID in bold, then the written reason. The ID
        // is a SHOULD of the Definition of Done: a MUST that fails stops the release, and an OBSERVATIONAL number never fails.
        private Item? ReadAccepted(NotesBlock block, VersionHeader header, HashSet<string> accepted, List<NotesProblem> problems)
        {
            const string Form = "Each SHOULD criterion accepted failing is a bullet that begins with its ID in bold, then the written reason it was accepted, "
                + "as - **UX-10:** … (the Definition of Done's §1).";
            if (block is not NotesBullet bullet)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1, Form));
                return null;
            }
            var item = Bullet(bullet, 0, problems);
            var lead = Lead.Match(bullet.Text.Text);
            var ids = lead.Success ? lead.Groups[1].Value.Split(',').Select(p => p.Trim()).ToList() : [];
            if (!lead.Success || ids.Any(id => !CriterionId.IsMatch(id)))
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, 1, Form));
                return item;
            }
            if (bullet.Text.Text.Substring(lead.Length).Trim().Length == 0 && bullet.Children.IsEmpty)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, Math.Max(1, lead.Length),
                    $"**{lead.Groups[1].Value}:** gives no reason. A SHOULD that fails is accepted only with a written reason, naming why (the Definition of Done's §1)."));
            }
            var at = lead.Groups[1].Index;
            foreach (var part in lead.Groups[1].Value.Split(','))
            {
                var id = part.Trim();
                var start = bullet.Text.OffsetOf(at + (part.Length - part.TrimStart().Length));
                at += part.Length + 1;
                if (!accepted.Add(id))
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, id.Length,
                        $"{id} is accepted twice. Give each criterion one line, with its reason."));
                    continue;
                }
                if (!header.Publishing || !HasDefinitionOfDone(start, id.Length, problems))
                    continue;
                if (!_criteria!.TryGetValue(id, out var level))
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Link, start, id.Length,
                        $"{id} names no criterion: the Definition of Done has no row **{id}**. Correct the ID."));
                }
                else if (level == "MUST")
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, id.Length,
                        $"{id} is a MUST, and no reason accepts a MUST that fails: release is not possible while it fails (the Definition of Done's §1). "
                        + "Make it pass before the tag; only a SHOULD is accepted failing."));
                }
                else if (level == "OBSERVATIONAL")
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, start, id.Length,
                        $"{id} is OBSERVATIONAL: it is recorded and never a gate, so it does not fail and is not accepted (the Definition of Done's §1). Leave it out."));
                }
            }
            return item;
        }

        // A §21 item out of scope: a bullet that names the item, as §21.11, and links where it was accepted in writing.
        private Item? ReadOutOfScope(NotesBlock block, VersionHeader header, List<NotesProblem> problems)
        {
            const string Form = "Each line under '## Out of scope for this version' is a bullet that names the Definition of Done's §21 item accepted in writing "
                + "as out of this version's scope, as §21.11, and links where it was accepted (the Definition of Done's Step 7; ADR-0205).";
            if (block is not NotesBullet bullet)
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1, Form));
                return null;
            }
            var item = Bullet(bullet, 0, problems);
            var raw = bullet.Text.Text + string.Concat(bullet.Children.Select(c => "\n" + c.Text.Text));
            var named = Section21Item.Matches(bullet.Text.Text).Cast<Match>().ToList();
            if (named.Count == 0 || !AnyLink.IsMatch(raw))
            {
                problems.Add(new NotesProblem(NotesProblemKind.Shape, bullet.Start, 1, Form));
                return item;
            }
            foreach (var match in named)
            {
                var start = bullet.Text.OffsetOf(match.Index);
                if (!header.Publishing || !HasDefinitionOfDone(start, match.Length, problems))
                    break;
                if (!_section21!.Contains(match.Groups[1].Value))
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Link, start, match.Length,
                        $"{match.Value} names no item of the Definition of Done's §21, whose items are "
                        + $"{string.Join(", ", _section21.OrderBy(s => s, Comparer<string>.Create(CompareItems)).Select(s => "§" + s))}. Correct the number."));
                }
            }
            return item;
        }

        private static int CompareItems(string a, string b)
        {
            int Number(string s) => int.Parse(new string(s.Substring(3).TakeWhile(char.IsDigit).ToArray()), CultureInfo.InvariantCulture);
            var order = Number(a).CompareTo(Number(b));
            return order != 0 ? order : string.CompareOrdinal(a, b);
        }

        private Item Paragraph(NotesParagraph paragraph, List<NotesProblem> problems)
        {
            var inline = Render(paragraph.Text, problems);
            return new Item(inline.Html, inline.Text, [], "", inline.Links.Where(l => !l.External).Select(l => l.Target).ToImmutableArray(), Bullet: false);
        }

        // A bullet, from `from` in its text, with its nested bullets. `allLinks`, when given, receives every link
        // it holds, those that leave the site included.
        private Item Bullet(NotesBullet bullet, int from, List<NotesProblem> problems, List<NotesLink>? allLinks = null)
        {
            var inline = Render(bullet.Text, problems, from: from);
            var html = new StringBuilder(inline.Html);
            var text = new StringBuilder(inline.Text);
            var links = inline.Links.Where(l => !l.External).Select(l => l.Target).ToList();
            allLinks?.AddRange(inline.Links);
            if (!bullet.Children.IsEmpty)
            {
                html.Append("<ul>");
                foreach (var child in bullet.Children)
                {
                    var nested = Render(child.Text, problems);
                    html.Append("<li>").Append(nested.Html).Append("</li>");
                    text.Append(' ').Append(nested.Text);
                    links.AddRange(nested.Links.Where(l => !l.External).Select(l => l.Target));
                    allLinks?.AddRange(nested.Links);
                }
                html.Append("</ul>");
            }
            return new Item(html.ToString(), text.ToString(), [], "", links.ToImmutableArray(), Bullet: true);
        }

        private void CheckAcross()
        {
            var ordered = Versions.OrderByDescending(v => v.Name, Comparer<string>.Create(CompareVersions)).ToList();
            Versions.Clear();
            Versions.AddRange(ordered);
            var newest = ordered.FirstOrDefault();

            foreach (var version in ordered.Skip(1).Where(v => v.PublishedOn is null && v.PublishedLength > 0))
            {
                Diagnostics.Add(Diagnostic.Create(WrongShape, At(version.File, version.PublishedStart, version.PublishedLength),
                    $"{version.Name} still reads Published: pending, and {newest!.Name} is newer. Only the newest version waits for its day; "
                    + $"the cut of the next version writes this one's, the UTC day nuget.org received it ({Adr})."));
            }

            if (PrereleaseNote.Length == 0 && !files.Any(f => f.Name == PrereleaseNoteName) && ordered.Any(v => v.Name.IndexOf('-') >= 0))
            {
                Diagnostics.Add(Diagnostic.Create(WrongShape, Location.None,
                    $"docs/releases/{PrereleaseNoteName} is missing, and every prerelease's notes begin with it: what ADR-0042 promises of a prerelease, with {{ref}} where the commit goes ({Adr})."));
            }

            if (build.Version == "dev")
                return;
            var published = ordered.FirstOrDefault(v => v.Name == build.Version);
            if (published is null)
            {
                Diagnostics.Add(Diagnostic.Create(NotCut, Location.None,
                    $"This build publishes {build.Version} (DocsVersion), and docs/releases/{build.Version}.md does not exist. "
                    + $"Cut the version first: gather the Change Notes into that file under its headings, write its Highlights, delete the Change Notes, and write Published: pending ({Adr})."));
            }
            else if (!ReferenceEquals(published, newest))
            {
                Diagnostics.Add(Diagnostic.Create(NotCut, At(newest!.File, 0, newest.Name.Length + 2),
                    $"This build publishes {build.Version} (DocsVersion), and docs/releases/{newest.Name}.md is newer: the site would list a version nuget.org does not have ({Adr})."));
            }
            foreach (var change in Changes.OrderBy(c => c.File.Name, StringComparer.Ordinal).Select(c => c.File)
                         .Concat(files.Where(f => f.Name.StartsWith(UnreleasedFolder, StringComparison.Ordinal) && !Changes.Any(c => c.File == f))))
            {
                Diagnostics.Add(Diagnostic.Create(NotCut, At(change, 0, 1),
                    $"This Change Note is still under {UnreleasedFolder}, and this build publishes {build.Version} (DocsVersion). "
                    + $"A tag publishes all of main, so the cut gathers every Change Note into docs/releases/{build.Version}.md and deletes it ({Adr})."));
            }
        }

        private static Location At(NotesInput file, int start, int length)
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
        code.AppendLine("// <auto-generated/> by ExGrid.Docs.Generator from docs/releases/ (ADR-0190).");
        code.AppendLine("#nullable enable");
        code.AppendLine("namespace ExGrid.Docs;");
        code.AppendLine();
        code.AppendLine("internal static partial class ReleaseNotes");
        code.AppendLine("{");
        code.AppendLine("    private static partial global::ExGrid.Docs.ReleaseVersion[] ReadVersions() =>");
        code.AppendLine("    [");
        foreach (var version in reader.Versions)
        {
            code.Append("        new(").Append(Literal(version.Name)).Append(", ")
                .Append(Literal(version.PublishedOn)).Append(", ")
                .Append(Strings(version.Packages)).Append(", ")
                .Append(Literal(version.Target)).AppendLine(",");
            code.AppendLine("        [");
            foreach (var section in version.Sections)
            {
                code.Append("            new(").Append(Literal(section.Title)).Append(", global::ExGrid.Docs.ReleaseSectionKind.").Append(section.Kind)
                    .Append(", ").Append(Literal(section.Product)).Append(", ").Append(section.IsNone ? "true" : "false").AppendLine(",");
                code.AppendLine("            [");
                foreach (var item in section.Items)
                {
                    code.Append("                new(").Append(Literal(item.Html)).Append(", ").Append(Literal(item.Text)).Append(", ")
                        .Append(Strings(item.Products)).Append(", ").Append(KindOf(item.Kind)).Append(", ")
                        .Append(Strings(item.Links)).Append(", ").Append(item.Bullet ? "true" : "false").AppendLine("),");
                }
                code.AppendLine("            ]),");
            }
            code.AppendLine("        ]),");
        }
        code.AppendLine("    ];");
        code.AppendLine();
        code.AppendLine("    private static partial global::ExGrid.Docs.ChangeNote[] ReadChangeNotes() =>");
        code.AppendLine("    [");
        foreach (var change in reader.Changes
                     .OrderBy(c => Products.IndexOf(c.Product))
                     .ThenBy(c => c.Kind switch { "Breaking" => 0, "Behaviour" => 1, "Added" => 2, _ => 3 })
                     .ThenBy(c => c.Slug, StringComparer.Ordinal))
        {
            code.Append("        new(").Append(Literal(change.Slug)).Append(", ").Append(Literal(change.Product)).Append(", ")
                .Append(KindOf(change.Kind)).Append(", ").Append(Literal(change.Docs)).Append(", [")
                .Append(string.Join(", ", change.Adrs.Select(a => "new(" + Literal(a.Number) + ", " + Literal(a.Url) + ")")))
                .Append("], ").Append(Literal(change.Html)).Append(", ").Append(Literal(change.Text)).Append(", ")
                .Append(Strings(change.Links)).AppendLine("),");
        }
        code.AppendLine("    ];");
        code.AppendLine();
        code.Append("    private static partial string ReadPrereleaseNote() => ").Append(Literal(reader.PrereleaseNote)).AppendLine(";");
        code.AppendLine("}");
        return SourceText.From(code.ToString(), Encoding.UTF8);
    }

    private static string KindOf(string kind) =>
        "global::ExGrid.Docs.ReleaseKind." + (kind.Length == 0 ? "None" : kind);

    private static string Strings(IEnumerable<string> values) =>
        "[" + string.Join(", ", values.Select(Literal)) + "]";

    private static string Literal(string? text) =>
        text is null ? "null" : SymbolDisplay.FormatLiteral(text, quote: true);
}
