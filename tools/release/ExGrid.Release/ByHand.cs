using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExGrid.Release;

/// <summary>The latest record of a row checked by hand: the commit it was made at, the file that holds it, and its verdict.</summary>
public sealed record ByHandRecord(string Commit, string Path, string Verdict);

/// <summary>
/// A row the Definition of Done checks by hand, where it is checked, the paths whose change could
/// change its result, and its latest record, or none when it has never been recorded.
/// </summary>
public sealed record ByHandRow(string Row, string Where, IReadOnlyList<string> Paths, ByHandRecord? Record)
{
    /// <summary>How a message names the row: <c>FN-12e on macOS</c>.</summary>
    public string Name => $"{Row} on {Where}";
}

/// <summary>
/// ADR-0208's check of the rows the Definition of Done verifies by hand. A row's record holds while
/// the code it covers is unchanged since the commit the record was made at: the check compares the
/// row's paths between that commit and the one being signed off (<c>git diff --quiet</c>), and refuses
/// the tag when they differ, when the row has no record, or when its latest record is not a pass.
/// The rows and their paths are in two places that must agree: the Definition of Done's Step 4 table,
/// "Rows checked by hand, and the code each covers", which a reader reads; and the record index,
/// <c>verification/by-hand.json</c>, which this check reads, beside each row's latest record.
/// </summary>
public static partial class ByHand
{
    /// <summary>The record index.</summary>
    public const string IndexPath = "verification/by-hand.json";

    /// <summary>The verdicts a record may carry. Only a pass lets a row hold.</summary>
    public static readonly IReadOnlyList<string> Verdicts = ["pass", "fail", "not run"];

    /// <summary>Reads the record index, refusing what is malformed; null when it cannot be read at all.</summary>
    public static IReadOnlyList<ByHandRow>? ReadIndex(ISourceTree tree, Report report)
    {
        var text = tree.ReadText(IndexPath);
        if (text is null)
        {
            report.Refuse($"{tree.Name} has no {IndexPath}, so the rows checked by hand cannot be judged (ADR-0208).");
            return null;
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            report.Refuse($"{IndexPath} is not JSON ({e.Message}) (ADR-0208).", IndexPath);
            return null;
        }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                report.Refuse($"{IndexPath} holds no 'rows' array (ADR-0208).", IndexPath);
                return null;
            }
            var read = new List<ByHandRow>();
            var number = 0;
            foreach (var item in rows.EnumerateArray())
            {
                number++;
                var row = Text(item, "row");
                var where = Text(item, "where");
                if (string.IsNullOrWhiteSpace(row) || string.IsNullOrWhiteSpace(where))
                {
                    report.Refuse($"Row {number} of {IndexPath} does not say which row it is and where it is checked ('row' and 'where').", IndexPath);
                    continue;
                }
                var name = $"{row} on {where}";
                var paths = new List<string>();
                if (item.TryGetProperty("paths", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    foreach (var path in list.EnumerateArray())
                    {
                        var value = path.ValueKind == JsonValueKind.String ? path.GetString()! : "";
                        if (!IsPath(value))
                            report.Refuse($"{name} in {IndexPath} names '{value}', which is not a path from the repository's root: a file, or a directory ending in /, with no glob and no '..'.", IndexPath);
                        else
                            paths.Add(value);
                    }
                }
                if (paths.Count == 0)
                    report.Refuse($"{name} in {IndexPath} names no paths. Every row names the code it covers (ADR-0208).", IndexPath);

                ByHandRecord? record = null;
                if (!item.TryGetProperty("record", out var held))
                {
                    report.Refuse($"{name} in {IndexPath} has no 'record'. A row never recorded says so with null.", IndexPath);
                }
                else if (held.ValueKind == JsonValueKind.Object)
                {
                    var commit = Text(held, "commit") ?? "";
                    var at = Text(held, "path") ?? "";
                    var verdict = Text(held, "verdict") ?? "";
                    var good = true;
                    if (!FullCommit().IsMatch(commit))
                    {
                        report.Refuse($"{name}'s record in {IndexPath} names its commit as '{commit}'. Write the commit the record was made at in full, forty hex digits.", IndexPath);
                        good = false;
                    }
                    if (!at.StartsWith("verification/", StringComparison.Ordinal) || !IsPath(at) || at.EndsWith('/'))
                    {
                        report.Refuse($"{name}'s record in {IndexPath} is '{at}', and a record is a file under verification/.", IndexPath);
                        good = false;
                    }
                    if (!Verdicts.Contains(verdict))
                    {
                        report.Refuse($"{name}'s record in {IndexPath} has the verdict '{verdict}', and a verdict is one of {string.Join(", ", Verdicts)}.", IndexPath);
                        good = false;
                    }
                    if (good) record = new ByHandRecord(commit, at, verdict);
                }
                else if (held.ValueKind != JsonValueKind.Null)
                {
                    report.Refuse($"{name}'s 'record' in {IndexPath} is neither a record nor null.", IndexPath);
                }

                if (read.Any(r => r.Name == name))
                    report.Refuse($"{name} is written twice in {IndexPath}.", IndexPath);
                read.Add(new ByHandRow(row, where, paths, record));
            }
            return read;
        }
    }

    /// <summary>
    /// Refuses the tag unless every row checked by hand holds at <paramref name="target"/>: the commit
    /// being signed off for a stable version, the tagged commit for a release candidate. The index and
    /// the table are read from <paramref name="tree"/>, the tag's.
    /// </summary>
    public static void Check(Git git, ISourceTree tree, string target, Report report, bool fetchMissing)
    {
        var refusedBefore = report.Refusals.Count;
        var index = ReadIndex(tree, report);
        if (index is null) return;
        var table = ByHandTable.Read(tree, report);
        if (table is not null) Agree(index, table, report);

        var targetTree = new CommitTree(git, target);
        var holding = new List<string>();
        var toRun = new List<string>();
        foreach (var row in index)
        {
            var missing = row.Paths.Where(p => p.EndsWith('/') ? !targetTree.IsDirectory(p) : !targetTree.IsFile(p)).ToList();
            if (missing.Count > 0)
                report.Refuse($"{row.Name} names {string.Join(", ", missing)}, which {targetTree.Name} does not have. A row's paths are reviewed in any change that moves the code they name (ADR-0208).", IndexPath);

            if (row.Record is null)
            {
                report.Refuse($"{row.Name} has never been recorded. Run it at the commit to be signed off and record it in {IndexPath} (ADR-0208).", IndexPath);
                toRun.Add(row.Name);
                continue;
            }
            var record = row.Record;
            if (!tree.IsFile(record.Path))
                report.Refuse($"{row.Name}'s record, {record.Path}, is not at the tag.", IndexPath);
            if (record.Verdict != "pass")
            {
                report.Refuse($"{row.Name}'s latest record, {record.Path}, reads '{record.Verdict}', and a row holds only on a pass (ADR-0208; the Definition of Done's Step 7).", IndexPath);
                toRun.Add(row.Name);
                continue;
            }
            var commit = git.Commit(record.Commit) ?? (fetchMissing && git.TryFetch(record.Commit) ? git.Commit(record.Commit) : null);
            if (commit is null)
            {
                report.Refuse($"{row.Name}'s record was made at {record.Commit[..12]}, which this repository does not have, so whether its code changed since cannot be checked. Keep a branch or a tag on that commit (ADR-0208).", IndexPath);
                continue;
            }
            var changed = git.ChangedPaths(commit, target, row.Paths);
            if (changed.Count > 0)
            {
                var shown = string.Join(", ", changed.Take(12)) + (changed.Count > 12 ? $", and {changed.Count - 12} more" : "");
                report.Refuse($"{row.Name}: the code it covers changed between its record ({record.Path}, made at {commit[..12]}) and {target[..12]}: {shown}. Run it again at the commit to be signed off, and record it (ADR-0208).", IndexPath);
                toRun.Add(row.Name);
                continue;
            }
            holding.Add(row.Name);
        }

        if (holding.Count > 0)
            report.Notice($"Holding on their records, their code unchanged since: {string.Join(", ", holding)} (ADR-0208).");
        if (toRun.Count > 0)
            report.Notice($"To run by hand at {target[..12]}: {string.Join(", ", toRun)} (ADR-0208).");
        else if (report.Refusals.Count == refusedBefore)
            report.Notice($"Every row checked by hand holds at {target[..12]} (ADR-0208).");
    }

    /// <summary>Refuses every difference between the index's rows and paths and the table's.</summary>
    public static void Agree(IReadOnlyList<ByHandRow> index, ByHandTable.Table table, Report report)
    {
        foreach (var row in index)
        {
            var listed = table.Rows.FirstOrDefault(r => r.Name == row.Name);
            if (listed is null)
            {
                report.Refuse($"{row.Name} is in {IndexPath} and not in {table.Source}'s table. The two list the same rows (ADR-0208).", IndexPath);
                continue;
            }
            var onlyIndex = row.Paths.Except(listed.Paths, StringComparer.Ordinal).ToList();
            var onlyTable = listed.Paths.Except(row.Paths, StringComparer.Ordinal).ToList();
            if (onlyIndex.Count > 0 || onlyTable.Count > 0)
            {
                var parts = new List<string>();
                if (onlyIndex.Count > 0) parts.Add($"only {IndexPath} names {string.Join(", ", onlyIndex)}");
                if (onlyTable.Count > 0) parts.Add($"only {table.Source} names {string.Join(", ", onlyTable)}");
                report.Refuse($"{row.Name}: the paths differ, {string.Join("; ", parts)}. The table and the index name the same paths (ADR-0208).", table.Source);
            }
        }
        foreach (var listed in table.Rows.Where(l => index.All(r => r.Name != l.Name)))
            report.Refuse($"{listed.Name} is in {table.Source}'s table and not in {IndexPath}. The two list the same rows (ADR-0208).", table.Source, listed.Line);
    }

    /// <summary>A path from the repository's root: no glob, no backslash, no '..', not absolute.</summary>
    public static bool IsPath(string path) =>
        path.Length > 0
        && !path.StartsWith('/')
        && !path.Contains('\\')
        && path.IndexOfAny(['*', '?', '[', ']']) < 0
        && path.TrimEnd('/').Split('/').All(part => part.Length > 0 && part != "." && part != "..");

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex FullCommit();
}

/// <summary>
/// The Definition of Done's Step 4 table, "Rows checked by hand, and the code each covers": a
/// Markdown table under a heading of that name, whose columns are Row, Where, Checked by hand and
/// The code it covers; each path in the last column is a code span. Until the table is in
/// <c>docs/definition-of-done.md</c>, it is read from its proposal.
/// </summary>
public static partial class ByHandTable
{
    /// <summary>The table's heading, word for word.</summary>
    public const string Heading = "Rows checked by hand, and the code each covers";

    /// <summary>Where the table belongs.</summary>
    public const string DefinitionOfDone = "docs/definition-of-done.md";

    /// <summary>Where it is read from until the orchestrator commits it to the Definition of Done.</summary>
    public const string Proposal = "docs/agents/proposals/by-hand-rows-table.md";

    /// <summary>The table's columns, in order.</summary>
    public static readonly IReadOnlyList<string> Columns = ["Row", "Where", "Checked by hand", "The code it covers"];

    /// <summary>One row of the table, and the line it is on.</summary>
    public sealed record Listed(string Row, string Where, IReadOnlyList<string> Paths, int Line)
    {
        /// <summary>How a message names the row.</summary>
        public string Name => $"{Row} on {Where}";
    }

    /// <summary>The table, and the file it was read from.</summary>
    public sealed record Table(string Source, IReadOnlyList<Listed> Rows);

    /// <summary>Reads the table from the Definition of Done, or from its proposal while the Definition of Done has none.</summary>
    public static Table? Read(ISourceTree tree, Report report)
    {
        foreach (var source in new[] { DefinitionOfDone, Proposal })
        {
            var text = tree.ReadText(source);
            if (text is null || HeadingLine(text) < 0) continue;
            return Parse(source, text, report);
        }
        report.Refuse($"Neither {DefinitionOfDone} nor {Proposal} holds the table '{Heading}', so the paths of the rows checked by hand cannot be held to it (ADR-0208).");
        return null;
    }

    /// <summary>Parses the table under the heading in <paramref name="text"/>, refusing what does not fit its shape.</summary>
    public static Table? Parse(string source, string text, Report report)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var heading = HeadingLine(text);
        if (heading < 0)
        {
            report.Refuse($"{source} has no heading '{Heading}'.", source);
            return null;
        }
        var start = heading + 1;
        while (start < lines.Length && !lines[start].TrimStart().StartsWith('|'))
        {
            if (lines[start].StartsWith('#'))
            {
                start = lines.Length;
                break;
            }
            start++;
        }
        if (start + 1 >= lines.Length)
        {
            report.Refuse($"{source} has no table under '{Heading}'.", source, heading + 1);
            return null;
        }
        var header = Cells(lines[start]);
        if (!header.SequenceEqual(Columns))
        {
            report.Refuse($"The table under '{Heading}' in {source} has the columns {string.Join(" | ", header)}, and its columns are {string.Join(" | ", Columns)}.", source, start + 1);
            return null;
        }
        var rows = new List<Listed>();
        for (var i = start + 2; i < lines.Length && lines[i].TrimStart().StartsWith('|'); i++)
        {
            var cells = Cells(lines[i]);
            if (cells.Count != Columns.Count)
            {
                report.Refuse($"Line {i + 1} of {source} has {cells.Count} cells, and the table '{Heading}' has {Columns.Count}.", source, i + 1);
                continue;
            }
            var row = cells[0].Replace("**", "").Replace("`", "").Trim();
            var where = cells[1].Replace("**", "").Trim();
            var paths = CodeSpan().Matches(cells[3]).Select(m => m.Groups[1].Value).ToList();
            if (row.Length == 0 || where.Length == 0 || paths.Count == 0)
            {
                report.Refuse($"Line {i + 1} of {source} does not name a row, where it is checked, and the paths it covers as code spans.", source, i + 1);
                continue;
            }
            rows.Add(new Listed(row, where, paths, i + 1));
        }
        return new Table(source, rows);
    }

    private static int HeadingLine(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('#') && !line.StartsWith("**", StringComparison.Ordinal)) continue;
            if (line.TrimStart('#').Trim().Trim('*').Trim() == Heading) return i;
        }
        return -1;
    }

    private static List<string> Cells(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return trimmed.Split('|').Select(c => c.Trim()).ToList();
    }

    [GeneratedRegex(@"`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex CodeSpan();
}
