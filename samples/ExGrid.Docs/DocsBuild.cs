namespace ExGrid.Docs;

/// <summary>
/// What the site states it was built from (ADR-0110). The generator reads the build's
/// <c>DocsVersion</c>, <c>DocsRef</c> and <c>DocsPublishedOn</c> together, refuses inputs that
/// disagree, and emits <see cref="Kind"/>, <see cref="Version"/>, <see cref="Branch"/>,
/// <see cref="Commit"/>, <see cref="GitRef"/> and <see cref="PublishedOn"/>; this half says what
/// the frame shows of them.
/// </summary>
internal static partial class DocsBuild
{
    /// <summary>The repository the site documents.</summary>
    public const string Repository = "https://github.com/AkiraMisawa/ex-grid";

    /// <summary>The commit's first seven characters, as GitHub shows it.</summary>
    public static string ShortCommit => Commit.Length > 7 ? Commit[..7] : Commit;

    /// <summary>Whether a tag's version carries a prerelease suffix (ADR-0042).</summary>
    public static bool Prerelease => Version.Contains('-');

    /// <summary>
    /// Whether the site says, above every page, that it is not a version's site: every build but
    /// a tag's. A local build says it in its own words, and only on the topic pages: its Showcases
    /// and figure pages are what <c>tools/readme-media</c> records, and show the product alone.
    /// </summary>
    public static bool Unreleased => Kind != DocsBuildKind.Tag;

    /// <summary>The version chip's text in the app bar.</summary>
    public static string Chip => Kind switch
    {
        DocsBuildKind.Tag => Prerelease ? $"{Version} · prerelease" : Version,
        DocsBuildKind.Branch => $"{Branch} · {ShortCommit} · not on nuget.org",
        DocsBuildKind.Experiment => $"{Branch} · {ShortCommit} · experiment",
        _ => "dev",
    };

    /// <summary>What the chip's tooltip says of it.</summary>
    public static string ChipTitle => Kind switch
    {
        DocsBuildKind.Tag => PublishedOn.Length > 0
            ? $"{Version}, on nuget.org since {PublishedOn}: its release notes"
            : $"{Version}, on nuget.org: its release notes",
        DocsBuildKind.Branch => $"Published from {Branch} at {ShortCommit}: what is not on nuget.org yet",
        DocsBuildKind.Experiment => $"An experiment, published from {Branch} at {ShortCommit}: what is not on nuget.org",
        _ => "A local build: what is not on nuget.org yet",
    };

    /// <summary>
    /// Where the chip and the banner lead: the version's own Release Notes, or the Unreleased
    /// section of the Release Notes (ADR-0190).
    /// </summary>
    public static string ReleaseNotesHref => Kind == DocsBuildKind.Tag
        ? "release-notes#" + DocsAnchor.Of(Version)
        : "release-notes#unreleased";

    /// <summary>The commit on GitHub; empty when the build names none.</summary>
    public static string CommitUrl => Commit.Length > 0 ? $"{Repository}/commit/{Commit}" : "";

    /// <summary>A file of the repository, at the tag or the commit the site was built from.</summary>
    /// <param name="path">The path from the repository's root, as <c>docs/adr/0016-….md</c>.</param>
    public static string BlobUrl(string path) => $"{Repository}/blob/{GitRef}/{path}";

    /// <summary>A folder of the repository, at the tag or the commit the site was built from.</summary>
    /// <param name="path">The path from the repository's root, as <c>docs/adr</c>.</param>
    public static string TreeUrl(string path) => $"{Repository}/tree/{GitRef}/{path}";
}
