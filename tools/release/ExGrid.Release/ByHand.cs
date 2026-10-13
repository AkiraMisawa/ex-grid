using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExGrid.Release;

/// <summary>
/// The latest record of a row checked by hand: the commit it was made at; the <c>verified/*</c> tag its run
/// put on that commit, <c>verified/</c> followed by the name of the record's directory under
/// <c>verification/</c>, through which the release workflow fetches it; the file that holds it; its verdict;
/// and, on WR-10's records only, the MudBlazor versions their layer-2 runs covered.
/// </summary>
public sealed record ByHandRecord(string Commit, string Tag, string Path, string Verdict, IReadOnlyList<string>? Versions = null);

/// <summary>
/// A row put out of a version by decision, as a Known limitation of it, rather than left unrecorded: the version
/// without a suffix it is a Known limitation of, the day it was decided, and the decision's words. The index writes it
/// as <c>knownLimitation</c> on a row with no record; it reaches that version's release candidates and the version,
/// and no other (2026-10-12, the release tool's choice).
/// </summary>
public sealed record KnownLimitation(string Version, string Decided, string Decision);

/// <summary>
/// A row the Definition of Done checks by hand, where it is checked, the paths whose change could
/// change its result, and its latest record, or none when it has never been recorded. WR-10 alone may name
/// besides, in <see cref="Later"/>, the records of layer-2 runs at a MudBlazor version published after its
/// floor record, each a run of its own. A row with no record may be marked, in <see cref="Limitation"/>, a Known
/// limitation of a version by decision.
/// </summary>
public sealed record ByHandRow(string Row, string Where, IReadOnlyList<string> Paths, ByHandRecord? Record, IReadOnlyList<ByHandRecord>? Later = null, KnownLimitation? Limitation = null)
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
/// <para>A record names the commit its run verified and the <c>verified/*</c> tag the run put on it, and
/// the commit is fetched through that tag: a squash merge or a deleted branch cannot take it away, and a
/// commit that cannot be fetched refuses the tag as unchecked, never lets it pass (2026-10-12). The tag is
/// <c>verified/</c> followed by the name of the record's directory under <c>verification/</c>, and nothing
/// else, one tag per record directory, whatever the platform or the run's purpose (decided with the user,
/// 2026-10-12): <c>verification/2026-10-01-windows-browser-11/keys.md</c> is tagged
/// <c>verified/2026-10-01-windows-browser-11</c>. WR-10's floor run, an automated run a person makes once
/// and its record carries forward, is indexed beside the rows, with its paths written as a <c>:(glob)</c>
/// pattern where a directory name varies (ADR-0205's note of 2026-10-12); its record names the MudBlazor
/// versions its layer-2 runs covered. A layer-2 run at a MudBlazor 9.x published after the floor record is a
/// run of its own, with a record in a directory of its own, tagged by that directory, which WR-10's entry names
/// besides the floor record (decided with the user, 2026-10-12); the index names it in <c>laterRecords</c>. Every record the entry
/// names is checked: each is a pass, names its commit, and is fetched through its tag, which names that commit;
/// and each holds while the row's paths are unchanged between its own commit and the target, as every record
/// checked by hand is held. The versions the records
/// cover between them are held to those published within the Wrappers' declared range before X
/// (<see cref="WrapperRange"/>, 2026-10-12).</para>
/// <para>A row put out of a version by decision is marked in the index with <c>knownLimitation</c>: the version, the
/// day it was decided and the decision's words. For that version's release candidates and the version itself the row
/// is reported in a notice, not refused, and it never holds; for any other version, or when no version is given, it is
/// refused as a row never recorded. The mark is refused when the row also names a record, which would leave two
/// answers to whether it holds, when its decision or its date is missing, and when the Definition of Done's table does
/// not say the row is a Known limitation of that version, so the index alone cannot put a row out (2026-10-12, the
/// release tool's choice).</para>
/// </summary>
public static partial class ByHand
{
    /// <summary>The record index.</summary>
    public const string IndexPath = "verification/by-hand.json";

    /// <summary>The verdicts a record may carry. Only a pass lets a row hold.</summary>
    public static readonly IReadOnlyList<string> Verdicts = ["pass", "fail", "not run"];

    /// <summary>How a record's tag is named, as a refusal says it (ADR-0208, decided with the user on 2026-10-12).</summary>
    public const string TagRule = "'verified/' followed by the name of the record's directory under verification/, and nothing else";

    /// <summary>
    /// The tag a record at <paramref name="recordPath"/> is kept by: <c>verified/</c> followed by the first
    /// path segment under <c>verification/</c>, the record's directory, as
    /// <c>verification/2026-10-01-windows-15/report.md</c> is tagged <c>verified/2026-10-01-windows-15</c>;
    /// null when the path is not a file in a directory under <c>verification/</c>.
    /// </summary>
    public static string? TagFor(string recordPath)
    {
        var parts = recordPath.Split('/');
        return parts.Length >= 3 && parts[0] == "verification" && parts[1].Length > 0 && parts[^1].Length > 0
            ? $"verified/{parts[1]}"
            : null;
    }

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
                            report.Refuse($"{name} in {IndexPath} names '{value}', which is not a path from the repository's root: a file, or a directory ending in /, with no wildcard and no '..', or a pattern written {Git.GlobMagic}<pattern>.", IndexPath);
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
                    record = Record(held, row, name, $"{name}'s record", report);
                }
                else if (held.ValueKind != JsonValueKind.Null)
                {
                    report.Refuse($"{name}'s 'record' in {IndexPath} is neither a record nor null.", IndexPath);
                }

                KnownLimitation? limitation = null;
                if (item.TryGetProperty(KnownLimitationKey, out var limited))
                {
                    limitation = Limitation(limited, name, report);
                    if (held.ValueKind != JsonValueKind.Null && held.ValueKind != JsonValueKind.Undefined)
                        report.Refuse($"{name} in {IndexPath} names a record and '{KnownLimitationKey}'. A row put out of a version by decision has no record, and a row with a record is held by it; the two together would leave whether it holds to whichever is read (2026-10-12).", IndexPath);
                }

                var later = new List<ByHandRecord>();
                if (item.TryGetProperty(LaterRecords, out var laterList))
                {
                    if (row != WrapperRange.Row)
                    {
                        report.Refuse($"{name} in {IndexPath} names '{LaterRecords}', and only {WrapperRange.Row}'s entry does: the records of layer-2 runs at a {WrapperRange.Package} version published after its floor record (2026-10-12).", IndexPath);
                    }
                    else if (laterList.ValueKind != JsonValueKind.Array)
                    {
                        report.Refuse($"{name}'s '{LaterRecords}' in {IndexPath} is not a list of records.", IndexPath);
                    }
                    else
                    {
                        // A floor record that is there but malformed has been refused already.
                        if ((!item.TryGetProperty("record", out var floor) || floor.ValueKind == JsonValueKind.Null) && laterList.GetArrayLength() > 0)
                            report.Refuse($"{name} in {IndexPath} names records in '{LaterRecords}' and no floor record. A later layer-2 run stands beside the floor run, never in its place (ADR-0205, 2026-10-12).", IndexPath);
                        var number2 = 0;
                        foreach (var entry in laterList.EnumerateArray())
                        {
                            number2++;
                            if (entry.ValueKind != JsonValueKind.Object)
                            {
                                report.Refuse($"Record {number2} of {name}'s '{LaterRecords}' in {IndexPath} is not a record.", IndexPath);
                                continue;
                            }
                            if (Record(entry, row, name, $"{name}'s later record {number2}", report) is { } each) later.Add(each);
                        }
                        // One tag per record directory (2026-10-12): two records the entry names in one directory would share it.
                        var all = (record is null ? later : [record, .. later]).ToList();
                        foreach (var shared in all.GroupBy(r => r.Tag, StringComparer.Ordinal).Where(g => g.Count() > 1))
                            report.Refuse($"{name} in {IndexPath} names {string.Join(", ", shared.Select(r => r.Path))} in one directory, tagged {shared.Key}. A layer-2 run at a later version is a run of its own, whose record goes in a directory of its own, one tag for each record directory (2026-10-12).", IndexPath);
                    }
                }

                if (read.Any(r => r.Name == name))
                    report.Refuse($"{name} is written twice in {IndexPath}.", IndexPath);
                read.Add(new ByHandRow(row, where, paths, record, later, limitation));
            }
            return read;
        }
    }

    /// <summary>
    /// Refuses the tag unless every row checked by hand holds at <paramref name="target"/>: the commit
    /// being signed off for a stable version, the tagged commit for a release candidate. The index and
    /// the table are read from <paramref name="tree"/>, the tag's. WR-10's records, once the floor record
    /// otherwise holds, are held besides to the MudBlazor versions <paramref name="mudBlazor"/> gives as published
    /// within the range the Wrappers declare at <paramref name="target"/> before its commit time, or with no date, and listed;
    /// with no list, or one that cannot be read, the row is refused as unchecked. <paramref name="version"/> is the
    /// version being released, with or without its suffix: a row the index marks a Known limitation of its three
    /// numbers is reported, not refused; with no version, no such mark applies.
    /// </summary>
    public static void Check(Git git, ISourceTree tree, string target, Report report, bool fetchMissing, IPublishedVersions? mudBlazor = null, string? version = null)
    {
        var refusedBefore = report.Refusals.Count;
        var index = ReadIndex(tree, report);
        if (index is null) return;
        var table = ByHandTable.Read(tree, report);
        if (table is not null) Agree(index, table, report);

        var targetTree = new CommitTree(git, target);
        var holding = new List<string>();
        var toRun = new List<string>();
        var limitations = new List<string>();
        var released = version?.Split('-', 2)[0];
        foreach (var row in index)
        {
            var missing = row.Paths.Where(p => !Names(targetTree, p)).ToList();
            if (missing.Count > 0)
                report.Refuse($"{row.Name} names {string.Join(", ", missing)}, which {targetTree.Name} does not have. A row's paths are reviewed in any change that moves the code they name (ADR-0208).", IndexPath);

            if (row.Record is null && row.Limitation is { } limitation && limitation.Version == released)
            {
                limitations.Add($"{row.Name}, a Known limitation of {limitation.Version} by the decision of {limitation.Decided}: \"{limitation.Decision}\"");
                continue;
            }
            if (row.Record is null)
            {
                var known = row.Limitation is { } other
                    ? $" It is a Known limitation of {other.Version} by the decision of {other.Decided}, which does not reach {(released is null ? "a check given no version" : released)}."
                    : "";
                report.Refuse($"{row.Name} has never been recorded. Run it at the commit to be signed off and record it in {IndexPath} (ADR-0208).{known}", IndexPath);
                toRun.Add(row.Name);
                continue;
            }
            var record = row.Record;
            if (!Verified(git, tree, row, record, "record", fetchMissing, report))
            {
                if (record.Verdict != "pass") toRun.Add(row.Name);
                continue;
            }
            var commit = record.Commit;
            var changed = git.ChangedPaths(commit, target, row.Paths);
            if (changed.Count > 0)
            {
                var shown = string.Join(", ", changed.Take(12)) + (changed.Count > 12 ? $", and {changed.Count - 12} more" : "");
                report.Refuse($"{row.Name}: the code it covers changed between its record ({record.Path}, made at {commit[..12]}) and {target[..12]}: {shown}. Run it again at the commit to be signed off, and record it (ADR-0208).", IndexPath);
                toRun.Add(row.Name);
                continue;
            }
            if (row.Row == WrapperRange.Row)
            {
                // Every later record the entry names is read as the floor record is: a pass, naming its commit, fetched
                // through its tag, which names that commit, and its paths unchanged between its commit and the target, as
                // every record checked by hand is held (ADR-0208; 2026-10-12). The versions are the union of those the
                // records that hold cover.
                var covered = new List<string>(record.Versions ?? []);
                var laterHold = true;
                foreach (var later in row.Later ?? [])
                {
                    var verified = Verified(git, tree, row, later, "later record", fetchMissing, report);
                    if (verified && LaterHolds(git, row, later, target, report))
                    {
                        covered.AddRange(later.Versions ?? []);
                        continue;
                    }
                    laterHold = false;
                    if (later.Verdict != "pass") toRun.Add($"{row.Name}'s later record {later.Path}, which reads '{later.Verdict}'");
                    else if (verified) toRun.Add($"{row.Name}'s later record {later.Path}, whose paths changed since its commit");
                }
                // A MudBlazor version published after the floor record and before X asks for the layer-2 runs at it (2026-10-12).
                var unrun = WrapperRange.Check(row, covered.Distinct(StringComparer.Ordinal).ToList(), targetTree, git.CommitTime(target), git.CommitTime(commit), mudBlazor, report);
                if (unrun is { Count: > 0 })
                    toRun.Add($"{row.Name}'s layer-2 runs at {WrapperRange.Package} {string.Join(", ", unrun.Select(u => u.Version))}");
                // A later record that does not hold keeps the row from holding, though no version is missing.
                if (unrun is not { Count: 0 } || !laterHold) continue;
            }
            holding.Add(row.Name);
        }

        if (holding.Count > 0)
            report.Notice($"Holding on their records, their code unchanged since: {string.Join(", ", holding)} (ADR-0208).");
        // Not run, and not holding. The version's notes are to say it among their Known limitations (ADR-0202, ADR-0190);
        // this check does not read them.
        if (limitations.Count > 0)
            report.Notice($"Not run, and not holding, as a Known limitation of {released} by decision: {string.Join("; ", limitations)}.");
        if (toRun.Count > 0)
            report.Notice($"To run by hand at {target[..12]}: {string.Join(", ", toRun)} (ADR-0208).");
        else if (report.Refusals.Count == refusedBefore)
            report.Notice(limitations.Count > 0
                ? $"Every row checked by hand holds at {target[..12]} but the Known limitations of {released} named above (ADR-0208)."
                : $"Every row checked by hand holds at {target[..12]} (ADR-0208).");
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
            if (row.Limitation?.Version != listed.KnownLimitation)
                report.Refuse(row.Limitation is null
                    ? $"{row.Name}: {table.Source}'s table says it is a Known limitation of {listed.KnownLimitation}, and {IndexPath} does not mark it '{KnownLimitationKey}'. The two say the same (2026-10-12)."
                    : $"{row.Name}: {IndexPath} marks it a Known limitation of {row.Limitation.Version}, and {table.Source}'s table {(listed.KnownLimitation is null ? "does not say so" : $"says {listed.KnownLimitation}")}. A row is put out of a version where the Definition of Done says it, never by the index alone (2026-10-12).", table.Source, listed.Line);
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

    // Refuses a record whose file at the tag does not name its commit, and returns whether the rest of the record
    // can be judged: its verdict is a pass, and its commit is had through its tag, which names it (2026-10-12), asked
    // of the remote when this clone does not have the tag. Each failure is refused.
    private static bool Verified(Git git, ISourceTree tree, ByHandRow row, ByHandRecord record, string what, bool fetchMissing, Report report)
    {
        var held = tree.ReadText(record.Path);
        if (held is null)
            report.Refuse($"{row.Name}'s {what}, {record.Path}, is not at the tag.", IndexPath);
        else if (!SignOff.Names(held, record.Commit))
            report.Refuse($"{row.Name}'s {what}, {record.Path}, does not name the commit {record.Commit[..12]} that {IndexPath} says it was made at. A record names the commit it verified.", record.Path);
        if (record.Verdict != "pass")
        {
            report.Refuse($"{row.Name}'s {(what == "record" ? "latest record" : what)}, {record.Path}, reads '{record.Verdict}', and a row holds only on a pass (ADR-0208; the Definition of Done's Step 7).", IndexPath);
            return false;
        }
        var tagged = git.TagCommit(record.Tag) ?? (fetchMissing ? git.FetchTag(record.Tag) : null);
        if (tagged is null)
        {
            report.Refuse($"{row.Name}'s {what} was made at {record.Commit[..12]}, and the tag {record.Tag} that keeps that commit is not in this repository"
                + (fetchMissing ? $", and {git.Remote} did not give it" : " (fetch the tags, or run with --fetch-missing)")
                + ", so whether its code changed since cannot be checked. The run that made the record tags the commit it verified and pushes the tag (ADR-0208, 2026-10-12).", IndexPath);
            return false;
        }
        if (tagged != record.Commit)
        {
            report.Refuse($"{row.Name}'s {what} was made at {record.Commit[..12]}, and its tag {record.Tag} names {tagged[..12]}. A run's tag names the commit it verified and is never moved, so one of the two is wrong (ADR-0208, 2026-10-12).", IndexPath);
            return false;
        }
        return true;
    }

    // Whether a later record's paths, the row's, are unchanged between its commit and the target, as a record checked by
    // hand is held (ADR-0208); refused, naming the files, when they are not.
    private static bool LaterHolds(Git git, ByHandRow row, ByHandRecord later, string target, Report report)
    {
        var changed = git.ChangedPaths(later.Commit, target, row.Paths);
        if (changed.Count == 0) return true;
        var shown = string.Join(", ", changed.Take(12)) + (changed.Count > 12 ? $", and {changed.Count - 12} more" : "");
        report.Refuse($"{row.Name}: the code it covers changed between its later record ({later.Path}, made at {later.Commit[..12]}) and {target[..12]}: {shown}. A later record is held as every record checked by hand is, while its paths are unchanged (ADR-0208, 2026-10-12).", IndexPath);
        return false;
    }

    /// <summary>The key of a row that marks it a Known limitation of a version by decision.</summary>
    public const string KnownLimitationKey = "knownLimitation";

    /// <summary>Whether <paramref name="version"/> is a version without a suffix, as <c>0.1.0</c>.</summary>
    public static bool IsStableVersion(string version) => StableVersion().IsMatch(version);

    // A row's knownLimitation: a version without a suffix, the day it was decided, and the decision's words. Null,
    // refused, when any of it is missing or malformed.
    private static KnownLimitation? Limitation(JsonElement item, string name, Report report)
    {
        var what = $"{name}'s '{KnownLimitationKey}' in {IndexPath}";
        if (item.ValueKind != JsonValueKind.Object)
        {
            report.Refuse($"{what} is not an object naming the version, the day it was decided and the decision ('version', 'decided', 'decision').", IndexPath);
            return null;
        }
        var version = Text(item, "version") ?? "";
        var decided = Text(item, "decided") ?? "";
        var decision = Text(item, "decision") ?? "";
        var good = true;
        if (!IsStableVersion(version))
        {
            report.Refuse($"{what} names the version '{version}', and a Known limitation is of a version without a suffix, as 0.1.0; its release candidates are reached through it.", IndexPath);
            good = false;
        }
        if (!DateOnly.TryParseExact(decided, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
        {
            report.Refuse($"{what} gives '{decided}' as the day it was decided. Write the day, as 2026-10-12: a row is put out of a version by a decision, and the decision has a date.", IndexPath);
            good = false;
        }
        if (string.IsNullOrWhiteSpace(decision))
        {
            report.Refuse($"{what} gives no 'decision'. A row is put out of a version by a decision, and its words are written here, so that a row never stops being asked for without saying why.", IndexPath);
            good = false;
        }
        return good ? new KnownLimitation(version, decided, decision.Trim()) : null;
    }

    /// <summary>The key of WR-10's entry that names the records of its later layer-2 runs.</summary>
    public const string LaterRecords = "laterRecords";

    // A record as the index writes it: commit, tag, path, verdict, and on WR-10 the versions. Null, refused, when
    // any of it is malformed.
    private static ByHandRecord? Record(JsonElement held, string row, string name, string what, Report report)
    {
        var commit = Text(held, "commit") ?? "";
        var tag = Text(held, "tag") ?? "";
        var at = Text(held, "path") ?? "";
        var verdict = Text(held, "verdict") ?? "";
        var good = true;
        var expected = IsPath(at) ? TagFor(at) : null;
        if (!FullCommit().IsMatch(commit))
        {
            report.Refuse($"{what} in {IndexPath} names its commit as '{commit}'. Write the commit the record was made at in full, forty hex digits.", IndexPath);
            good = false;
        }
        if (tag.Length == 0)
        {
            report.Refuse($"{what} in {IndexPath} names no 'tag'. A run tags the commit it verified with {TagRule}{(expected is null ? "" : $", here {expected}")}, and pushes the tag; the record names it, and the release workflow fetches the commit through it (ADR-0208, 2026-10-12).", IndexPath);
            good = false;
        }
        else if (expected is not null && tag != expected)
        {
            report.Refuse($"{what} in {IndexPath} is {at} and names the tag '{tag}'. A record's tag is {TagRule}: {expected}, one tag for each record directory (ADR-0208, 2026-10-12).", IndexPath);
            good = false;
        }
        if (expected is null || at.EndsWith('/'))
        {
            report.Refuse($"{what} in {IndexPath} is '{at}', and a record is a file in its run's directory under verification/, whose name its tag carries.", IndexPath);
            good = false;
        }
        if (!Verdicts.Contains(verdict))
        {
            report.Refuse($"{what} in {IndexPath} has the verdict '{verdict}', and a verdict is one of {string.Join(", ", Verdicts)}.", IndexPath);
            good = false;
        }
        IReadOnlyList<string>? versions = null;
        if (row == WrapperRange.Row)
        {
            // Each of WR-10's records names the MudBlazor versions its layer-2 runs covered, so that a version
            // published after the floor record can be asked for (ADR-0205, ADR-0208, 2026-10-12).
            versions = Versions(held, what, report);
            if (versions is null) good = false;
        }
        return good ? new ByHandRecord(commit, tag, at, verdict, versions) : null;
    }

    /// <summary>
    /// A path from the repository's root, not absolute, with no backslash and no '..': a file, or a
    /// directory ending in <c>/</c>, with no wildcard; or a pattern written <c>:(glob)&lt;pattern&gt;</c>,
    /// whose <c>*</c> stays within a directory and <c>**</c> crosses them, as
    /// <c>:(glob)src/*.MudBlazor/**</c> reaches every file below each Wrapper.
    /// </summary>
    public static bool IsPath(string path)
    {
        var glob = path.StartsWith(Git.GlobMagic, StringComparison.Ordinal);
        var body = glob ? path[Git.GlobMagic.Length..] : path;
        return body.Length > 0
            && !body.StartsWith('/')
            && !body.StartsWith(':')
            && !body.Contains('\\')
            && (glob ? body.IndexOfAny(['*', '?', '[']) >= 0 : body.IndexOfAny(['*', '?', '[', ']']) < 0)
            && body.TrimEnd('/').Split('/').All(part => part.Length > 0 && part != "." && part != "..");
    }

    /// <summary>
    /// Whether <paramref name="tree"/> has what <paramref name="path"/> names: the file, the directory, or,
    /// for a <c>:(glob)</c> pattern, at least one file it matches.
    /// </summary>
    public static bool Names(ISourceTree tree, string path) =>
        path.StartsWith(Git.GlobMagic, StringComparison.Ordinal) ? tree.Matches(path).Count > 0
        : path.EndsWith('/') ? tree.IsDirectory(path)
        : tree.IsFile(path);

    // WR-10's 'versions': the stable MudBlazor versions its layer-2 runs covered, at least one.
    private static IReadOnlyList<string>? Versions(JsonElement held, string what, Report report)
    {
        if (!held.TryGetProperty("versions", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            report.Refuse($"{what} in {IndexPath} names no 'versions'. Each of WR-10's records names the {WrapperRange.Package} versions its layer-2 runs covered, as [\"9.6.0\", \"9.7.0\"], and a stable version published within the Wrappers' declared range before X that none of them names refuses the tag (ADR-0205, ADR-0208, 2026-10-12).", IndexPath);
            return null;
        }
        var versions = new List<string>();
        var good = true;
        foreach (var item in list.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString()! : item.ToString();
            if (item.ValueKind != JsonValueKind.String || PackageVersion.Parse(value) is not { IsStable: true })
            {
                report.Refuse($"{what} in {IndexPath} names '{value}' among its 'versions', and each is a stable {WrapperRange.Package} version its layer-2 runs covered, as 9.6.0.", IndexPath);
                good = false;
                continue;
            }
            versions.Add(value);
        }
        return good ? versions : null;
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex FullCommit();

    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex StableVersion();
}

/// <summary>
/// The Definition of Done's Step 4 table, "Rows checked by hand, and the code each covers": a
/// Markdown table under a heading of that name, whose columns are Row, Where, Checked by hand and
/// The code it covers; each path in the last column is a code span. A row whose "Checked by hand" cell says
/// <c>Known limitation of `x.y.z`</c> is put out of that version by decision, as the index marks it. Until the table is in
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
    public sealed record Listed(string Row, string Where, IReadOnlyList<string> Paths, int Line, string? KnownLimitation = null)
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
            if (rows.Any(r => r.Row == row && r.Where == where))
                report.Refuse($"{row} on {where} is written twice in the table '{Heading}' in {source}.", source, i + 1);
            var limited = KnownLimitationOf().Matches(cells[2]).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
            if (limited.Count > 1)
                report.Refuse($"Line {i + 1} of {source} says {row} on {where} is a Known limitation of {string.Join(" and ", limited)}; a row names one version it is put out of.", source, i + 1);
            rows.Add(new Listed(row, where, paths, i + 1, limited.Count == 1 ? limited[0] : null));
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

    [GeneratedRegex(@"Known limitation of `([0-9]+\.[0-9]+\.[0-9]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex KnownLimitationOf();
}
