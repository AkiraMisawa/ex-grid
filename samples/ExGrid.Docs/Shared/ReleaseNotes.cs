namespace ExGrid.Docs;

/// <summary>What a section of a version's Release Notes holds (ADR-0190).</summary>
internal enum ReleaseSectionKind
{
    /// <summary>At most five lines, each linking the page that shows it.</summary>
    Highlights,

    /// <summary>What a Consumer must change.</summary>
    Breaking,

    /// <summary>The same call giving a different result.</summary>
    Behaviour,

    /// <summary>A product's Added and Fixed lines.</summary>
    Product,

    /// <summary>A release's known limitations, each naming its products and linking the page that says it in full (ADR-0202).</summary>
    KnownLimitations,

    /// <summary>
    /// A release's sign-off: the record, the commit X, and the toolchain, OS and browser versions it was made with (ADR-0205).
    /// A release candidate's says, in its pending form, that the sign-off is being made against it (2026-10-12).
    /// </summary>
    SignedOff,

    /// <summary>A release's SHOULD criteria accepted failing, each with its reason.</summary>
    ShouldAccepted,

    /// <summary>The Definition of Done's §21 items a release accepted in writing as out of its scope (ADR-0205).</summary>
    OutOfScope,

    /// <summary>What a release supports: its hosts, browsers, target framework and packages (ADR-0205).</summary>
    Supported,
}

/// <summary>What a line of the notes, or a Change Note, says its change is.</summary>
internal enum ReleaseKind
{
    /// <summary>A line that is none of the others: a paragraph, a highlight.</summary>
    None,

    /// <summary>Something new.</summary>
    Added,

    /// <summary>Something that was wrong and is now right.</summary>
    Fixed,

    /// <summary>The same call giving a different result.</summary>
    Behaviour,

    /// <summary>Something a Consumer must change.</summary>
    Breaking,
}

/// <summary>A line of a version's notes: a bullet or a paragraph, rendered at build time.</summary>
/// <param name="Html">What the page shows.</param>
/// <param name="Text">The same as plain text, for the search.</param>
/// <param name="Products">The products it reaches: those a breaking or behaviour line names, or its product's.</param>
/// <param name="Kind">Added or Fixed under a product, Breaking or Behaviour under those headings.</param>
/// <param name="Links">The routes of the site it links, with their anchors.</param>
/// <param name="Bullet">Whether it is a bullet; otherwise a paragraph.</param>
internal sealed record ReleaseItem(string Html, string Text, IReadOnlyList<string> Products, ReleaseKind Kind, IReadOnlyList<string> Links, bool Bullet);

/// <summary>A heading of a version's notes and what is under it.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Kind">What it holds.</param>
/// <param name="Product">The product, for a product's heading.</param>
/// <param name="IsNone">Whether it says "None."</param>
/// <param name="Items">Its lines.</param>
internal sealed record ReleaseSection(string Title, ReleaseSectionKind Kind, string? Product, bool IsNone, IReadOnlyList<ReleaseItem> Items);

/// <summary>A published version's Release Notes, <c>docs/releases/&lt;version&gt;.md</c>.</summary>
/// <param name="Version">The version, as <c>0.1.0-beta.2</c>.</param>
/// <param name="PublishedInFile">The UTC day its file records, or <see langword="null"/> while it reads <c>pending</c>.</param>
/// <param name="Packages">The package IDs it published.</param>
/// <param name="Target">The packages' target framework.</param>
/// <param name="Sections">Its headings, in order.</param>
internal sealed record ReleaseVersion(string Version, string? PublishedInFile, IReadOnlyList<string> Packages, string Target, IReadOnlyList<ReleaseSection> Sections)
{
    /// <summary>Its anchor on the Release notes page: <c>0-1-0-beta-2</c>.</summary>
    public string Anchor => DocsAnchor.Of(Version);

    /// <summary>Whether it carries a prerelease suffix (ADR-0042).</summary>
    public bool Prerelease => Version.Contains('-');

    /// <summary>
    /// Whether it is a release candidate, <c>-rc.N</c>: a prerelease that rehearses its release, whose notes open
    /// with the candidate's note rather than the prerelease note (ADR-0205, 2026-10-12). The build refuses any other
    /// spelling of one.
    /// </summary>
    public bool Candidate
    {
        get
        {
            var dash = Version.IndexOf('-');
            var suffix = dash < 0 ? "" : Version[(dash + 1)..];
            return suffix.Length > 3 && suffix.StartsWith("rc.", StringComparison.Ordinal) && suffix[3] != '0' && suffix[3..].All(char.IsAsciiDigit);
        }
    }

    /// <summary>Its tag.</summary>
    public string Tag => "v" + Version;

    /// <summary>
    /// The UTC day nuget.org received it: what its file records, or, on the site the release publishes
    /// for it, the day the release hands over (ADR-0190). <see langword="null"/> when neither knows.
    /// </summary>
    public string? PublishedOn => PublishedInFile
        ?? (DocsBuild.Kind == DocsBuildKind.Tag && DocsBuild.Version == Version && DocsBuild.PublishedOn.Length > 0 ? DocsBuild.PublishedOn : null);

    /// <summary>The core package at this version on nuget.org.</summary>
    public string NuGetUrl => $"https://www.nuget.org/packages/ExGrid/{Version}";

    /// <summary>The GitHub release for its tag.</summary>
    public string ReleaseUrl => $"{DocsBuild.Repository}/releases/tag/{Tag}";

    /// <summary>The repository at its tag.</summary>
    public string SourceUrl => $"{DocsBuild.Repository}/tree/{Tag}";
}

/// <summary>A decision a Change Note names, and where it is read.</summary>
/// <param name="Number">Its four digits.</param>
/// <param name="Url">The record on GitHub, at the ref the site was built from.</param>
internal sealed record AdrLink(string Number, string Url);

/// <summary>A Change Note, <c>docs/releases/unreleased/&lt;slug&gt;.md</c>: one change on its way to the next version.</summary>
/// <param name="Slug">Its file's name.</param>
/// <param name="Product">The product it changes.</param>
/// <param name="Kind">Whether it adds, fixes, changes behaviour or breaks.</param>
/// <param name="Docs">The route and anchor of the page that shows it, when one does.</param>
/// <param name="Adrs">The decisions it follows.</param>
/// <param name="Html">Its sentence or two.</param>
/// <param name="Text">The same as plain text, for the search.</param>
/// <param name="Links">The routes of the site it links, its Docs line first.</param>
internal sealed record ChangeNote(
    string Slug, string Product, ReleaseKind Kind, string? Docs, IReadOnlyList<AdrLink> Adrs, string Html, string Text, IReadOnlyList<string> Links);

/// <summary>A chip a section or a page carries because the Release Notes link it.</summary>
/// <param name="Text">What it says.</param>
/// <param name="Href">Where it leads: the version's notes, or what is unreleased.</param>
/// <param name="Title">Its tooltip.</param>
/// <param name="Unreleased">Whether it marks a change that is not on nuget.org.</param>
public sealed record DocsChip(string Text, string Href, string Title, bool Unreleased);

/// <summary>
/// The Release Notes (ADR-0190), read from <c>docs/releases/</c> when the site is built: every published
/// version, newest first, and the Change Notes for what is not published yet. The Release notes page, the
/// start page's latest version, the chips on the sections they link and the search read them here.
/// </summary>
internal static partial class ReleaseNotes
{
    /// <summary>The page's route.</summary>
    public const string Route = "release-notes";

    /// <summary>The anchor of the page's Unreleased section.</summary>
    public const string UnreleasedAnchor = "unreleased";

    /// <summary>The products, in the order the notes' headings put them.</summary>
    public static IReadOnlyList<string> Products { get; } = ["ExGrid", "ExSheet", "ExPivot", "Data"];

    /// <summary>Every published version, newest first.</summary>
    public static IReadOnlyList<ReleaseVersion> Versions { get; } = ReadVersions();

    /// <summary>The Change Notes waiting for the next version, by product, then breaking first.</summary>
    public static IReadOnlyList<ChangeNote> ChangeNotes { get; } = ReadChangeNotes();

    /// <summary>The newest version, if any was published.</summary>
    public static ReleaseVersion? Newest => Versions.Count > 0 ? Versions[0] : null;

    /// <summary>Whether a version without a suffix, a release, has been published.</summary>
    public static bool AnyRelease => Versions.Any(v => !v.Prerelease);

    /// <summary>
    /// Whether the site shows what is unreleased: every build but a tag's, whose notes hold all it
    /// publishes (ADR-0190).
    /// </summary>
    public static bool ShowsUnreleased => DocsBuild.Unreleased;

    private static readonly string PrereleaseNoteHtml = ReadPrereleaseNote();

    private static readonly string CandidateNoteHtml = ReadCandidateNote();

    /// <summary>
    /// The note a prerelease's notes open with, at the version's tag: the candidate's note, held once in
    /// <c>docs/releases/candidate-note.md</c>, for a release candidate (2026-10-12); the prerelease note, held once in
    /// <c>docs/releases/prerelease-note.md</c>, for any other prerelease.
    /// </summary>
    public static string PrereleaseNote(ReleaseVersion version) =>
        (version.Candidate ? CandidateNoteHtml : PrereleaseNoteHtml).Replace("{ref}", version.Tag);

    /// <summary>The version published before <paramref name="version"/>, if any.</summary>
    public static ReleaseVersion? Previous(ReleaseVersion version)
    {
        for (var k = 0; k + 1 < Versions.Count; k++)
        {
            if (Versions[k] == version)
                return Versions[k + 1];
        }
        return null;
    }

    /// <summary>The address comparing <paramref name="version"/> with the one before it, if any.</summary>
    public static string? CompareUrl(ReleaseVersion version) =>
        Previous(version) is { } previous ? $"{DocsBuild.Repository}/compare/{previous.Tag}...{version.Tag}" : null;

    /// <summary>What the site says of a version's day: the day, or that it is not recorded yet.</summary>
    public static string PublishedText(ReleaseVersion version) => version.PublishedOn is { } day
        ? $"Published to nuget.org on {day} (UTC)"
        : "Its day on nuget.org is written into these notes at the next cut";

    /// <summary>What this build is, as the Unreleased section names it.</summary>
    public static string UnreleasedSource => DocsBuild.Kind switch
    {
        DocsBuildKind.Branch => $"{DocsBuild.Branch} at {DocsBuild.ShortCommit}",
        DocsBuildKind.Experiment => $"{DocsBuild.Branch} at {DocsBuild.ShortCommit}",
        _ => "this working tree",
    };

    // The chips: the newest version's links, and on a build not from a tag the Change Notes'.
    private static readonly Dictionary<string, DocsChip> Released = ChipsOfNewest();
    private static readonly Dictionary<string, DocsChip> Unreleased = ChipsOfChangeNotes();

    /// <summary>
    /// The chips the section <paramref name="anchor"/> of the page <paramref name="route"/> carries, or
    /// the page itself when <paramref name="anchor"/> is <see langword="null"/>: "New in" or "Changed in"
    /// the newest version, when its notes link it, and "Not on nuget.org yet" when a Change Note does.
    /// Read from the notes' links, so there is no second record of what is new (ADR-0190).
    /// </summary>
    public static IReadOnlyList<DocsChip> ChipsFor(string route, string? anchor)
    {
        var key = Key(route, anchor);
        var chips = new List<DocsChip>(2);
        if (Released.TryGetValue(key, out var released))
            chips.Add(released);
        if (Unreleased.TryGetValue(key, out var unreleased))
            chips.Add(unreleased);
        return chips;
    }

    /// <summary>The Release notes page's sections, for its rail and the search: Unreleased, then each version.</summary>
    public static IEnumerable<SiteSection> PageSections()
    {
        if (ShowsUnreleased)
            yield return new SiteSection("Unreleased", UnreleasedAnchor, [], []);
        foreach (var version in Versions)
            yield return new SiteSection(version.Version, version.Anchor, [], []);
    }

    /// <summary>
    /// Each line of the notes, and each Change Note on a build not from a tag, as places the search
    /// leads to. The whole line is the entry's title, so that every word of it is found.
    /// </summary>
    public static IEnumerable<SiteSearchEntry> SearchEntries()
    {
        if (ShowsUnreleased)
        {
            foreach (var note in ChangeNotes)
            {
                yield return new SiteSearchEntry(note.Text, $"Release notes › Unreleased › {note.Product} · {KindText(note.Kind)}",
                    $"{Route}#{UnreleasedAnchor}", SiteSearchKind.ReleaseNote);
            }
        }
        foreach (var version in Versions)
        {
            foreach (var section in version.Sections)
            {
                foreach (var item in section.Items)
                {
                    yield return new SiteSearchEntry(item.Text, $"Release notes › {version.Version} › {section.Title}",
                        $"{Route}#{version.Anchor}", SiteSearchKind.ReleaseNote);
                }
            }
        }
    }

    /// <summary>A line's kind as a reader reads it.</summary>
    public static string KindText(ReleaseKind kind) => kind switch
    {
        ReleaseKind.Behaviour => "Behaviour change",
        ReleaseKind.Breaking => "Breaking change",
        _ => kind.ToString(),
    };

    /// <summary>A long line cut at a word for a list that shows many: the search's results.</summary>
    public static string Shorten(string text)
    {
        const int Most = 110;
        if (text.Length <= Most)
            return text;
        var cut = text.LastIndexOf(' ', Most);
        return text[..(cut > 40 ? cut : Most)] + "…";
    }

    private static string Key(string route, string? anchor) =>
        (route.Trim('/') + (anchor is null ? "" : "#" + anchor)).ToLowerInvariant();

    // A link's route and anchor, without a query.
    private static string KeyOf(string link)
    {
        var hash = link.IndexOf('#');
        var anchor = hash < 0 ? null : link[(hash + 1)..];
        var end = link.IndexOfAny(['?', '#']);
        return Key(end < 0 ? link : link[..end], anchor);
    }

    private static Dictionary<string, DocsChip> ChipsOfNewest()
    {
        var chips = new Dictionary<string, DocsChip>(StringComparer.Ordinal);
        if (Newest is not { } newest)
            return chips;
        var href = $"{Route}#{newest.Anchor}";
        var when = newest.PublishedOn is { } day ? $", on nuget.org since {day} (UTC)" : "";
        foreach (var section in newest.Sections)
        {
            foreach (var item in section.Items)
            {
                // Only a Highlight, an Added line, or a Breaking, Behaviour or Fixed line marks a section (ADR-0190): a
                // page a release's Known limitations, sign-off or support lines link has not changed for linking it.
                var isNew = section.Kind == ReleaseSectionKind.Highlights || item.Kind == ReleaseKind.Added;
                if (!isNew && item.Kind is not (ReleaseKind.Breaking or ReleaseKind.Behaviour or ReleaseKind.Fixed))
                    continue;
                var chip = isNew
                    ? new DocsChip($"New in {newest.Version}", href, $"New in {newest.Version}{when}: its release notes", false)
                    : new DocsChip($"Changed in {newest.Version}", href, $"Changed in {newest.Version}{when}: its release notes", false);
                foreach (var link in item.Links)
                {
                    var key = KeyOf(link);
                    if (!chips.TryGetValue(key, out var held) || (isNew && !held.Text.StartsWith("New", StringComparison.Ordinal)))
                        chips[key] = chip;
                }
            }
        }
        return chips;
    }

    private static Dictionary<string, DocsChip> ChipsOfChangeNotes()
    {
        var chips = new Dictionary<string, DocsChip>(StringComparer.Ordinal);
        if (!ShowsUnreleased)
            return chips;
        var since = Newest is { } newest ? $" since {newest.Version}" : "";
        var chip = new DocsChip("Not on nuget.org yet", $"{Route}#{UnreleasedAnchor}",
            $"Changed on {UnreleasedSource}{since}, and not on nuget.org yet: what is unreleased", true);
        foreach (var link in ChangeNotes.SelectMany(n => n.Links))
            chips[KeyOf(link)] = chip;
        return chips;
    }

    // The build writes these from docs/releases/ (ExGrid.Docs.Generator).
    private static partial ReleaseVersion[] ReadVersions();

    private static partial ChangeNote[] ReadChangeNotes();

    private static partial string ReadPrereleaseNote();

    private static partial string ReadCandidateNote();
}
