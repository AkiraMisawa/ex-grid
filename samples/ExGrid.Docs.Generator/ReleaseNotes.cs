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

    /// <summary>Every version's headings, in order (ADR-0190).</summary>
    public static readonly ImmutableArray<string> Headings =
        ["Highlights", "Breaking changes", "Behaviour changes", "ExGrid", "ExSheet", "ExPivot", "Data"];

    /// <summary>The headings a version without a suffix carries besides (Definition of Done §1 and Step 7).</summary>
    public static readonly ImmutableArray<string> StableHeadings = ["Signed off", "SHOULD criteria accepted failing"];

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
        description: "A version's notes name it on their first line, carry Published, Packages and Target, and every heading in order, each saying None. when it has nothing; a Change Note names its Product and Kind (ADR-0190).");

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

    private static readonly Regex SignedOffRecord = new(@"verification/[^/\s`]+/results\.md", RegexOptions.CultureInvariant);

    /// <summary>
    /// Reads, checks and emits the notes. <paramref name="pages"/> and <paramref name="adrs"/> are what
    /// links are checked against; <paramref name="build"/> says whether this build publishes a version,
    /// whose notes must then be cut.
    /// </summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, SourceText Source) Build(
        ImmutableArray<NotesInput> files,
        ImmutableArray<PageInput> pages,
        ImmutableDictionary<string, string> adrs,
        DocsBuildInfo build)
    {
        var reader = new Reader(files, pages, adrs, build);
        reader.Run();
        return (reader.Diagnostics.ToImmutable(), Emit(reader));
    }

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
        DocsBuildInfo build)
    {
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
            var sections = ReadSections(file, blocks, stable, problems);
            Versions.Add(new NotesVersion(file, name, published, publishedStart, publishedLength, packages, target, sections));
        }

        private ImmutableArray<Section> ReadSections(NotesInput file, ImmutableArray<NotesBlock> blocks, bool stable, List<NotesProblem> problems)
        {
            var expected = stable ? Headings.AddRange(StableHeadings) : Headings;
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
                        ? $"Only a version without a suffix carries '## {heading.Title}' (the Definition of Done's §1 and Step 7). A prerelease is not signed off."
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
                {
                    problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, heading.Title.Length + 3,
                        $"'## {expected[missing]}' is missing before '## {heading.Title}'. Every heading is present, in order, and one with nothing under it says None. ({Adr})."));
                }
                next = index + 1;
                sections.Add(ReadSection(heading, content, problems));
            }
            for (var missing = next; missing < expected.Length; missing++)
            {
                var end = Math.Max(0, file.Text.Length - 1);
                var why = StableHeadings.Contains(expected[missing])
                    ? $"'## {expected[missing]}' is missing. A version without a suffix is a release, and its notes name its sign-off and every SHOULD accepted failing (the Definition of Done's §1 and Step 7; {Adr})."
                    : $"'## {expected[missing]}' is missing. Every heading is present, in order, and one with nothing under it says None. ({Adr}).";
                problems.Add(new NotesProblem(NotesProblemKind.Shape, end, 1, why));
            }
            return sections.ToImmutable();
        }

        private Section ReadSection(NotesHeading heading, List<NotesBlock> content, List<NotesProblem> problems)
        {
            var title = heading.Title;
            var product = Products.Contains(title) ? title : null;
            var kind = title switch
            {
                "Highlights" => "Highlights",
                "Breaking changes" => "Breaking",
                "Behaviour changes" => "Behaviour",
                "Signed off" => "SignedOff",
                "SHOULD criteria accepted failing" => "ShouldAccepted",
                _ => "Product",
            };
            var headingSpan = title.Length + 3;

            if (content.Count == 0)
            {
                var say = product is null ? "None." : NoChange;
                problems.Add(new NotesProblem(NotesProblemKind.Shape, heading.Start, headingSpan,
                    $"'## {title}' has nothing under it. Write '{say}', so a reader can tell nothing from not written ({Adr})."));
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
                        "A release is signed off: name the verification/<date>/results.md the sign-off was made against (the Definition of Done's Step 7)."));
                }
                return new Section(title, kind, product, true, []);
            }

            var items = ImmutableArray.CreateBuilder<Item>();
            var highlights = 0;
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
                    case "Breaking" or "Behaviour":
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
                        items.Add(Bullet(change, lead.Length, problems) with
                        {
                            Products = named.ToImmutableArray(),
                            Kind = kind,
                        });
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
                    default:
                        var item = block is NotesParagraph other ? Paragraph(other, problems) : Bullet((NotesBullet)block, 0, problems);
                        if (kind == "SignedOff" && !SignedOffRecord.IsMatch(item.Text))
                        {
                            problems.Add(new NotesProblem(NotesProblemKind.Shape, block.Start, 1,
                                "Signed off names the verification/<date>/results.md the sign-off was made against (the Definition of Done's Step 7)."));
                        }
                        items.Add(item);
                        break;
                }
            }
            return new Section(title, kind, product, false, items.ToImmutable());
        }

        private Item Paragraph(NotesParagraph paragraph, List<NotesProblem> problems)
        {
            var inline = Render(paragraph.Text, problems);
            return new Item(inline.Html, inline.Text, [], "", inline.Links.Where(l => !l.External).Select(l => l.Target).ToImmutableArray(), Bullet: false);
        }

        // A bullet, from `from` in its text, with its nested bullets.
        private Item Bullet(NotesBullet bullet, int from, List<NotesProblem> problems)
        {
            var inline = Render(bullet.Text, problems, from: from);
            var html = new StringBuilder(inline.Html);
            var text = new StringBuilder(inline.Text);
            var links = inline.Links.Where(l => !l.External).Select(l => l.Target).ToList();
            if (!bullet.Children.IsEmpty)
            {
                html.Append("<ul>");
                foreach (var child in bullet.Children)
                {
                    var nested = Render(child.Text, problems);
                    html.Append("<li>").Append(nested.Html).Append("</li>");
                    text.Append(' ').Append(nested.Text);
                    links.AddRange(nested.Links.Where(l => !l.External).Select(l => l.Target));
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
