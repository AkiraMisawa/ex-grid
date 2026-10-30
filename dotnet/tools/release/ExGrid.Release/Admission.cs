namespace ExGrid.Release;

/// <summary>
/// What a tag is, and what its release does: the release workflow's first step, before any
/// check of what the tagged commit holds.
/// </summary>
/// <param name="Version">The tag, read.</param>
/// <param name="Commit">The tagged commit, in full.</param>
/// <param name="Long">Whether CI's long run gates it: the soak on both browsers and both hosts, and ST-1 at 10⁶ rows (ADR-0205).</param>
/// <param name="PublishesSite">Whether its release publishes the Docs Site (ADR-0110, ADR-0205).</param>
public sealed record Admission(TagVersion Version, string Commit, bool Long, bool PublishesSite)
{
    /// <summary>The outputs the release workflow's jobs read, as <c>name=value</c> lines.</summary>
    public IEnumerable<string> Outputs()
    {
        yield return $"version={Version.Version}";
        yield return $"kind={Version.Kind.ToString().ToLowerInvariant()}";
        yield return $"long={(Long ? "true" : "false")}";
        yield return $"site={(PublishesSite ? "true" : "false")}";
    }

    /// <summary>
    /// Reads the tag and refuses it when its version is malformed or its commit is on neither
    /// <c>main</c> nor a <c>release/*</c> branch (ADR-0042, ADR-0205). Decides, besides, whether the
    /// long run gates it — a stable version and a release candidate — and whether it publishes the
    /// Docs Site, which describes the latest stable version once there is one (ADR-0205): a stable
    /// version when no stable tag is above it by SemVer precedence, so that a patch to an older line
    /// (<c>v0.1.1</c> from <c>release/0.1</c> after <c>v0.2.0</c>) leaves the newer version's site in
    /// place; a prerelease only while no stable version exists. A stable version exists once the
    /// repository has a tag without a suffix.
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

        var stable = git.Tags().Where(TagVersion.IsStableTag).Order(Comparer<string>.Create(TagVersion.CompareNumbers)).ToList();
        bool site;
        if (version.Kind == ReleaseKind.Stable)
        {
            var above = stable.Where(t => TagVersion.CompareNumbers(t, tag) > 0).ToList();
            site = above.Count == 0;
            if (!site)
                report.Notice($"{string.Join(", ", above)} {(above.Count == 1 ? "is" : "are")} tagged above {tag}, so {tag} publishes no Docs Site: the site describes the latest stable version, and a patch to an older line leaves it in place. Its notes are on its GitHub release (ADR-0205).");
        }
        else
        {
            site = stable.Count == 0;
            if (!site)
                report.Notice($"{string.Join(", ", stable)} {(stable.Count == 1 ? "is" : "are")} tagged, so {tag}, a prerelease, publishes no Docs Site: the site describes the latest stable version, and a prerelease's notes are on its GitHub release (ADR-0205).");
        }

        return new Admission(version, full, version.RehearsesStable, site);

        string Short(string name) => name.StartsWith($"refs/remotes/{git.Remote}/", StringComparison.Ordinal) ? name[$"refs/remotes/{git.Remote}/".Length..] : name;
    }
}
