using System.Text.RegularExpressions;

namespace ExGrid.Release;

/// <summary>
/// A stable version's sign-off, as its Release Notes name it, and the structural check that the
/// tag is what was signed off (ADR-0205): the "Signed off" block names a
/// <c>verification/&lt;date&gt;/results.md</c> that exists at the tag, and the commit X the sign-off
/// was made against; the tag differs from X only under <c>verification/</c> and <c>docs/releases/</c>.
/// </summary>
public static partial class SignOff
{
    /// <summary>The heading of the block, as ADR-0190's notes write it.</summary>
    public const string Heading = "## Signed off";

    /// <summary>What may change between the signed-off commit and the tag: the records, and the notes.</summary>
    public static readonly IReadOnlyList<string> AfterSignOff = ["verification/", "docs/releases/"];

    /// <summary>
    /// Checks the sign-off of <paramref name="version"/>, tagged at <paramref name="tagCommit"/>, and
    /// returns the signed-off commit X when the notes name one this repository holds, refused or not,
    /// so that the rows checked by hand can be judged against it.
    /// </summary>
    public static string? Check(Git git, ISourceTree tag, TagVersion version, string tagCommit, Report report)
    {
        var notes = $"docs/releases/{version.Version}.md";
        var text = tag.ReadText(notes);
        if (text is null)
        {
            report.Refuse($"{version.Tag} is a version without a suffix, and its notes, {notes}, are not at the tag, so nothing names its sign-off (ADR-0205).");
            return null;
        }

        var (block, line) = Block(text);
        if (block is null)
        {
            report.Refuse($"{notes} has no '{Heading}' block. A version without a suffix is published only once it is signed off, and its notes name the results.md and the commit X the sign-off was made against (ADR-0205; the Definition of Done's Step 7).", notes);
            return null;
        }

        var records = RecordPath().Matches(block).Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();
        var commits = FullCommit().Matches(block).Select(m => m.Value).Distinct(StringComparer.Ordinal).ToList();

        if (records.Count != 1)
        {
            report.Refuse(records.Count == 0
                ? $"'{Heading}' in {notes} names no verification/<date>/results.md: it names the record the sign-off was made against (ADR-0205)."
                : $"'{Heading}' in {notes} names {records.Count} records ({string.Join(", ", records)}), and a sign-off is made against one (ADR-0205).", notes, line);
        }
        if (commits.Count != 1)
        {
            report.Refuse(commits.Count == 0
                ? $"'{Heading}' in {notes} names no commit in full. Write the commit X the sign-off was made against as its forty hex digits, which no other commit can share (ADR-0205)."
                : $"'{Heading}' in {notes} names {commits.Count} commits ({string.Join(", ", commits.Select(c => c[..12]))}), and a sign-off is made against one, X (ADR-0205).", notes, line);
        }

        string? x = null;
        if (commits.Count == 1)
        {
            x = git.Commit(commits[0]);
            if (x is null)
                report.Refuse($"'{Heading}' in {notes} names {commits[0]}, which is not a commit of this repository (ADR-0205).", notes, line);
        }

        if (records.Count == 1)
        {
            var record = records[0];
            var results = tag.ReadText(record);
            if (results is null)
            {
                report.Refuse($"'{Heading}' in {notes} names {record}, which is not at the tag. The record is committed between X and the tag, under verification/ (ADR-0205).", notes, line);
            }
            else if (x is not null && !Names(results, x))
            {
                report.Refuse($"{record} does not name the commit {x[..12]} that {notes} says it was made against. results.md names X, as the sign-off's every step ran at it (ADR-0205).", record);
            }
        }

        if (x is null) return null;

        if (!git.IsAncestor(x, tagCommit))
        {
            report.Refuse($"The signed-off commit {x[..12]} is not {version.Tag} or an ancestor of it, so the tag cannot be X plus its records and notes (ADR-0205).", notes, line);
            return x;
        }

        var outside = git.ChangedPaths(x, tagCommit)
            .Where(path => !AfterSignOff.Any(allowed => path.StartsWith(allowed, StringComparison.Ordinal)))
            .ToList();
        if (outside.Count > 0)
        {
            var shown = string.Join(", ", outside.Take(20)) + (outside.Count > 20 ? $", and {outside.Count - 20} more" : "");
            report.Refuse($"{version.Tag} differs from the signed-off commit {x[..12]} outside verification/ and docs/releases/: {shown}. A fix after X is a new X and a new sign-off of what it touches (ADR-0205).");
        }
        else
        {
            report.Notice($"{version.Tag} is the signed-off commit {x[..12]} plus only its records and notes.");
        }
        return x;
    }

    /// <summary>The block under <see cref="Heading"/>, up to the next heading, and the line it begins on; null when there is none.</summary>
    public static (string? Block, int Line) Block(string notes)
    {
        var lines = notes.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() != Heading) continue;
            var end = i + 1;
            while (end < lines.Length && !lines[end].StartsWith("## ", StringComparison.Ordinal)) end++;
            return (string.Join('\n', lines[(i + 1)..end]), i + 1);
        }
        return (null, 0);
    }

    // A record names the commit by its full name or by a prefix of seven digits or more, as the
    // Windows runs' reports write "Verified commit: `1655299`".
    private static bool Names(string results, string commit) =>
        HexWord().Matches(results).Any(m => commit.StartsWith(m.Value, StringComparison.Ordinal));

    [GeneratedRegex(@"verification/[^/\s`()\[\]]+/results\.md", RegexOptions.CultureInvariant)]
    private static partial Regex RecordPath();

    [GeneratedRegex(@"(?<![0-9A-Za-z])[0-9a-f]{40}(?![0-9A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex FullCommit();

    [GeneratedRegex(@"(?<![0-9A-Za-z])[0-9a-f]{7,40}(?![0-9A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex HexWord();
}
