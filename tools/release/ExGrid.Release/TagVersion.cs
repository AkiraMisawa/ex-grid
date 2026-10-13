using System.Text.RegularExpressions;

namespace ExGrid.Release;

/// <summary>What a tag publishes, by its version's suffix (ADR-0042, ADR-0205).</summary>
public enum ReleaseKind
{
    /// <summary>A suffix such as <c>-beta.3</c>: published on every layer passing in CI, before any sign-off.</summary>
    Prerelease,

    /// <summary><c>-rc.N</c>: a prerelease that runs every check a stable tag runs but the two that read a sign-off, so the stable path is rehearsed; its notes carry a stable version's blocks, its Signed off block in the pending form, which the Docs Site's build checks by form (ADR-0190, ADR-0205, 2026-10-12).</summary>
    Candidate,

    /// <summary>No suffix: a release in the Definition of Done's sense, published only once it is signed off.</summary>
    Stable,
}

/// <summary>A <c>v*</c> tag, read: the version it writes and what kind of version that is.</summary>
public sealed partial record TagVersion(string Tag, string Version, ReleaseKind Kind)
{
    /// <summary>Whether this kind runs the stable path's checks: the corpus, the rows checked by hand, the long run.</summary>
    public bool RehearsesStable => Kind is ReleaseKind.Stable or ReleaseKind.Candidate;

    /// <summary>
    /// Reads a tag, or refuses it. A version is <c>MAJOR.MINOR.PATCH</c> with an optional suffix, as SemVer 2
    /// writes it, and no leading zero in the three numbers: NuGet would publish <c>v01.2.0</c> as <c>1.2.0</c>,
    /// a version the tag does not name. A suffix that begins with <c>rc</c> is a release candidate, written
    /// <c>-rc.N</c> with N from 1; any other spelling of one is refused rather than published as a plain
    /// prerelease without the checks a candidate rehearses.
    /// </summary>
    public static TagVersion? Read(string tag, Report report)
    {
        if (!tag.StartsWith('v'))
        {
            report.Refuse($"The tag {tag} does not begin with v. A version is written in its tag as v0.1.0-beta.1 (ADR-0042).");
            return null;
        }
        var version = tag[1..];
        var match = Shape().Match(version);
        if (!match.Success)
        {
            report.Refuse($"{tag} does not name a version: write MAJOR.MINOR.PATCH, with no leading zero, and an optional suffix, as v0.1.0 or v0.1.0-rc.1 (ADR-0042).");
            return null;
        }
        var suffix = match.Groups["suffix"].Value;
        if (suffix.Length == 0)
            return new TagVersion(tag, version, ReleaseKind.Stable);
        if (Candidate().IsMatch(suffix))
            return new TagVersion(tag, version, ReleaseKind.Candidate);
        if (suffix.StartsWith("rc", StringComparison.OrdinalIgnoreCase))
        {
            report.Refuse($"{tag} looks like a release candidate, and a candidate is written -rc.N with N from 1, as v0.1.0-rc.1. Spelt otherwise it would publish as a plain prerelease, without the long run and the checks a candidate rehearses (ADR-0205).");
            return null;
        }
        return new TagVersion(tag, version, ReleaseKind.Prerelease);
    }

    /// <summary>Whether <paramref name="tag"/> names a version without a suffix: a stable version.</summary>
    public static bool IsStableTag(string tag) => tag.StartsWith('v') && Shape().Match(tag[1..]) is { Success: true } m && m.Groups["suffix"].Value.Length == 0;

    /// <summary>
    /// Orders two tags by the precedence SemVer gives their three numbers: MAJOR, then MINOR, then PATCH,
    /// each compared as a number, so <c>v0.10.0</c> is above <c>v0.9.0</c>. A suffix is not compared:
    /// this orders stable versions. Negative when <paramref name="a"/> is below <paramref name="b"/>.
    /// </summary>
    public static int CompareNumbers(string a, string b)
    {
        var left = Numbers(a);
        var right = Numbers(b);
        for (var i = 0; i < 3; i++)
        {
            // No leading zero, so the longer number is the larger, and two of one length compare as text.
            var order = left[i].Length != right[i].Length
                ? left[i].Length.CompareTo(right[i].Length)
                : string.CompareOrdinal(left[i], right[i]);
            if (order != 0) return Math.Sign(order);
        }
        return 0;

        static string[] Numbers(string tag) =>
            tag.StartsWith('v') && Shape().Match(tag[1..]) is { Success: true } m
                ? [m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value]
                : throw new ArgumentException($"{tag} does not name a version.", nameof(tag));
    }

    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    [GeneratedRegex(@"^rc\.[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Candidate();
}
