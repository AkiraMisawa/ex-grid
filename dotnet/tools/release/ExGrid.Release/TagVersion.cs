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
    /// writes it, and no leading zero in the three numbers, since NuGet would publish <c>v01.2.0</c> as
    /// <c>1.2.0</c>, a version the tag does not name, nor in a numeric identifier of the suffix, which SemVer
    /// forbids and which would give <c>-beta.01</c> and <c>-beta.1</c> one precedence. A suffix that begins with <c>rc</c> is a release candidate, written
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
            // A candidate misspelt with a leading zero, -rc.01, is still said as a candidate's spelling.
            if (Loose().Match(version) is { Success: true } loose && loose.Groups["suffix"].Value.StartsWith("rc", StringComparison.OrdinalIgnoreCase))
                report.Refuse($"{tag} looks like a release candidate, and a candidate is written -rc.N with N from 1, as v0.1.0-rc.1. Spelt otherwise it would publish as a plain prerelease, without the long run and the checks a candidate rehearses (ADR-0205).");
            else
                report.Refuse($"{tag} does not name a version: write MAJOR.MINOR.PATCH, with no leading zero in any number, and an optional suffix, as v0.1.0 or v0.1.0-rc.1 (ADR-0042).");
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

    /// <summary>Whether <paramref name="tag"/> names a version as <see cref="Read"/> takes it, with or without a suffix.</summary>
    public static bool IsVersionTag(string tag) => tag.StartsWith('v') && Shape().IsMatch(tag[1..]);

    /// <summary>
    /// Orders two tags by SemVer 2's whole precedence: the three numbers as <see cref="CompareNumbers"/>
    /// orders them, then a version without a suffix above every version of the same numbers with one, then
    /// the suffixes' dot-separated identifiers from the left, a numeric one as a number and below any
    /// alphanumeric one, an alphanumeric one by ASCII, and a shorter list below a longer one it begins
    /// (SemVer 2, §11). So <c>v0.2.0-beta.3</c> is above <c>v0.1.0</c>, <c>v0.2.0-beta.10</c> above
    /// <c>v0.2.0-beta.9</c>, <c>v0.2.0-rc.1</c> above both, and <c>v0.2.0</c> above all three. Negative
    /// when <paramref name="a"/> is below <paramref name="b"/>.
    /// </summary>
    public static int ComparePrecedence(string a, string b)
    {
        var numbers = CompareNumbers(a, b);
        if (numbers != 0) return numbers;
        var left = Suffix(a);
        var right = Suffix(b);
        if (left.Length == 0) return right.Length == 0 ? 0 : 1;
        if (right.Length == 0) return -1;
        var leftIds = left.Split('.');
        var rightIds = right.Split('.');
        for (var i = 0; i < Math.Min(leftIds.Length, rightIds.Length); i++)
        {
            var order = CompareIdentifiers(leftIds[i], rightIds[i]);
            if (order != 0) return order;
        }
        return Math.Sign(leftIds.Length.CompareTo(rightIds.Length));

        static string Suffix(string tag) =>
            tag.StartsWith('v') && Shape().Match(tag[1..]) is { Success: true } m
                ? m.Groups["suffix"].Value
                : throw new ArgumentException($"{tag} does not name a version.", nameof(tag));

        static int CompareIdentifiers(string x, string y)
        {
            var xNumeric = x.All(char.IsAsciiDigit);
            var yNumeric = y.All(char.IsAsciiDigit);
            if (xNumeric && yNumeric)
            {
                // A numeric identifier has no leading zero (Read refuses one) and may be long: the longer is the larger.
                return x.Length != y.Length ? Math.Sign(x.Length.CompareTo(y.Length)) : Math.Sign(string.CompareOrdinal(x, y));
            }
            if (xNumeric) return -1;
            if (yNumeric) return 1;
            return Math.Sign(string.CompareOrdinal(x, y));
        }
    }

    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?<suffix>(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    // Shape with a numeric identifier of the suffix allowed a leading zero, only to say what such a tag looks like.
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?<suffix>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Loose();

    [GeneratedRegex(@"^rc\.[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Candidate();
}
