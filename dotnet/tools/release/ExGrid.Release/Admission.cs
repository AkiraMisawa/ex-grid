namespace ExGrid.Release;

/// <summary>
/// What a tag is, and what its release does: the release workflow's first step, before any
/// check of what the tagged commit holds.
/// </summary>
/// <param name="Version">The tag, read.</param>
/// <param name="Commit">The tagged commit, in full.</param>
/// <param name="Long">Whether CI's long run gates it: the soak on both browsers and both hosts, and ST-1 at 10⁶ rows (ADR-0205).</param>
/// <param name="PublishesSite">Whether its release publishes the Docs Site (ADR-0110, ADR-0205).</param>
/// <param name="Latest">Whether npm's dist-tag <c>latest</c> moves to it: a version without a suffix that no stable tag is above (ADR-0042).</param>
public sealed record Admission(TagVersion Version, string Commit, bool Long, bool PublishesSite, bool Latest)
{
    /// <summary>The outputs the release workflow's jobs read, as <c>name=value</c> lines.</summary>
    public IEnumerable<string> Outputs()
    {
        yield return $"version={Version.Version}";
        yield return $"kind={Version.Kind.ToString().ToLowerInvariant()}";
        yield return $"long={(Long ? "true" : "false")}";
        yield return $"site={(PublishesSite ? "true" : "false")}";
        yield return $"latest={(Latest ? "true" : "false")}";
    }

    /// <summary>
    /// Reads the tag and refuses it when its version is malformed or its commit is on neither
    /// <c>main</c> nor a <c>release/*</c> branch (ADR-0042, ADR-0205). Decides, besides, whether the
    /// long run gates it — a stable version and a release candidate — whether it publishes the Docs
    /// Site (<see cref="SiteIsPublishedBy"/>), and whether npm's
    /// <c>latest</c> moves to it: a version without a suffix that no stable tag is above, so that a patch
    /// to an older line (<c>v0.1.1</c> from <c>release/0.1</c> after <c>v0.2.0</c>) takes
    /// <c>release-0.1</c> instead.
    /// </summary>
    public static Admission? Admit(Git git, string tag, string commit, Report report)
    {
        var version = TagVersion.Read(tag, report);
        var full = git.Commit(commit);
        if (full is null)
        {
            report.Refuse($"{commit} is not a commit of this repository, so {tag} cannot be checked.");
            return null;
        }

        var lines = git.Refs($"refs/remotes/{git.Remote}/main")
            .Concat(git.Refs($"refs/remotes/{git.Remote}/release/"))
            .ToList();
        if (!lines.Any(line => git.IsAncestor(full, line)))
        {
            var seen = lines.Count == 0 ? "no such branch was fetched" : $"the branches fetched are {string.Join(", ", lines.Select(Short))}";
            report.Refuse($"{tag} points at {full[..12]}, which is on neither main nor a release/* branch ({seen}). A version is tagged on main or on its release branch (ADR-0042, ADR-0205).");
        }

        if (version is null) return null;

        var tags = git.Tags();
        var site = SiteIsPublishedBy(tags, version, report);
        var latest = version.Kind == ReleaseKind.Stable
            && !tags.Where(TagVersion.IsStableTag).Any(t => TagVersion.CompareNumbers(t, tag) > 0);

        return new Admission(version, full, version.RehearsesStable, site, latest);

        string Short(string name) => name.StartsWith($"refs/remotes/{git.Remote}/", StringComparison.Ordinal) ? name[$"refs/remotes/{git.Remote}/".Length..] : name;
    }

    /// <summary>
    /// Whether <paramref name="version"/>'s release publishes the Docs Site, which describes the newest
    /// version tagged (ADR-0205, rewritten 2026-11-03): it does when no tag in <paramref name="tags"/> is
    /// above it by SemVer precedence, a prerelease included. So <c>v0.2.0-beta.3</c> publishes it over
    /// <c>v0.1.0</c>'s, and a version some tag is above publishes none: a patch to an older line
    /// (<c>v0.1.1</c> after <c>v0.2.0-beta.1</c> or <c>v0.2.0</c>), or a tag of an older prerelease
    /// published again. A tag that names no version is not compared; the tag itself, re-run, is not above
    /// itself. Says so when it publishes none.
    /// </summary>
    public static bool SiteIsPublishedBy(IReadOnlyList<string> tags, TagVersion version, Report report)
    {
        var above = tags
            .Where(TagVersion.IsVersionTag)
            .Where(t => TagVersion.ComparePrecedence(t, version.Tag) > 0)
            .Order(Comparer<string>.Create(TagVersion.ComparePrecedence))
            .ToList();
        if (above.Count == 0) return true;
        report.Notice($"{string.Join(", ", above)} {(above.Count == 1 ? "is" : "are")} tagged above {version.Tag}, so {version.Tag} publishes no Docs Site: the site describes the newest version tagged, and its pages would replace a later version's. Its notes are on its GitHub release (ADR-0205).");
        return false;
    }
}
