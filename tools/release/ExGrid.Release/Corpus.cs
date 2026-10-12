using System.Text.Json;

namespace ExGrid.Release;

/// <summary>
/// ADR-0208's corpus check: a version without a suffix, and a release candidate, are refused while
/// any of ExSheet's Excel cases is still <c>uncertain</c> or <c>documented</c> without a written
/// reason it cannot be asked (<c>oracleSkip</c>). The release asks Excel nothing; it checks that no
/// question is open.
/// </summary>
public static class Corpus
{
    /// <summary>Where the cases are: one file per area, besides <c>fixtures.json</c>.</summary>
    public const string Directory = "tests/ExSheet.Engine.Tests/ExcelCases";

    /// <summary>
    /// The field that holds a written reason a case cannot be put to Excel: why <c>oracle.ps1</c>
    /// cannot ask it, as an undo cannot go through COM (ADR-0208). It is the only field that excuses a
    /// case. <c>engineDiffersByDecision</c> does not: <c>oracle.ps1</c> asks such a case, comparing
    /// Excel's answer with its <c>excelExpect</c>, or recording it when there is none, so its question
    /// is open until a run answers it.
    /// </summary>
    public const string Reason = "oracleSkip";

    /// <summary>
    /// The words that make an <c>oracleSkip</c> a case <c>oracle.ps1</c> asks by keys: blocked through
    /// COM, and asked when <c>-Keys</c> types it. Matched as <c>oracle.ps1</c> matches them, ignoring
    /// case. Such a case can be asked, so it is open.
    /// </summary>
    public const string AskByKeys = "ask by keys";

    /// <summary>
    /// A case still to be asked of Excel: its file, its id, its source, and whether <c>oracle.ps1</c>
    /// asks it only under <c>-Keys</c>, its <c>oracleSkip</c> saying "ask by keys".
    /// </summary>
    public sealed record OpenCase(string File, string Id, string Source, bool ByKeys = false);

    /// <summary>Whether an <c>oracleSkip</c> names a case <c>oracle.ps1 -Keys</c> asks.</summary>
    public static bool AsksByKeys(string? oracleSkip) =>
        oracleSkip is not null && oracleSkip.Contains(AskByKeys, StringComparison.OrdinalIgnoreCase);

    /// <summary>The open cases in <paramref name="tree"/>, refusing every file that cannot be read.</summary>
    public static IReadOnlyList<OpenCase> Open(ISourceTree tree, Report report)
    {
        var files = tree.Files(Directory).Where(f => f.EndsWith(".json", StringComparison.Ordinal) && !f.EndsWith("/fixtures.json", StringComparison.Ordinal)).ToList();
        if (files.Count == 0)
        {
            report.Refuse($"{tree.Name} has no case files under {Directory}, so whether any question to Excel is open cannot be checked (ADR-0208).");
            return [];
        }

        var open = new List<OpenCase>();
        foreach (var file in files)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(tree.ReadText(file) ?? "");
            }
            catch (JsonException e)
            {
                report.Refuse($"{file} is not JSON ({e.Message}), so its cases cannot be checked (ADR-0208).", file);
                continue;
            }
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("cases", out var cases)
                    || cases.ValueKind != JsonValueKind.Array)
                {
                    report.Refuse($"{file} holds no 'cases' array, so its cases cannot be checked (ADR-0208).", file);
                    continue;
                }
                var index = 0;
                foreach (var item in cases.EnumerateArray())
                {
                    index++;
                    var id = Text(item, "id") ?? $"case {index}";
                    var source = Text(item, "source");
                    if (source == "observed") continue;
                    var skip = Text(item, Reason);
                    var byKeys = AsksByKeys(skip);
                    if (source is "uncertain" or "documented" && !string.IsNullOrWhiteSpace(skip) && !byKeys)
                        continue;
                    open.Add(new OpenCase(file, id, source ?? "(no source)", byKeys));
                }
            }
        }
        return open;
    }

    /// <summary>Refuses the tag when any case is open, naming each by file, and the runs of <c>oracle.ps1</c> that answer them.</summary>
    public static void Check(ISourceTree tree, Report report)
    {
        var refusedBefore = report.Refusals.Count;
        var open = Open(tree, report);
        if (open.Count == 0)
        {
            if (report.Refusals.Count == refusedBefore)
                report.Notice($"Every case under {Directory} is observed, or says in {Reason} why it cannot be asked (ADR-0208).");
            return;
        }
        foreach (var file in open.GroupBy(c => c.File))
        {
            var bySource = string.Join("; ", file.GroupBy(c => c.Source).Select(s => $"{s.Key}: {string.Join(", ", s.Select(c => c.ByKeys ? $"{c.Id} (by keys)" : c.Id))}"));
            report.Refuse($"{file.Count()} case{(file.Count() == 1 ? "" : "s")} in {file.Key} still to be asked of Excel ({bySource}).", file.Key);
        }

        static string Areas(IEnumerable<OpenCase> cases) =>
            string.Join(',', cases.Select(c => Path.GetFileNameWithoutExtension(c.File)).Distinct(StringComparer.Ordinal));
        var files = open.Select(c => c.File).Distinct(StringComparer.Ordinal).Count();
        var runs = new List<string>();
        if (open.Any(c => !c.ByKeys)) runs.Add($"oracle.ps1 -Area {Areas(open.Where(c => !c.ByKeys))} -Update");
        if (open.Any(c => c.ByKeys)) runs.Add($"oracle.ps1 -Area {Areas(open.Where(c => c.ByKeys))} -Keys -Update for the cases it asks by keys");
        report.Refuse(
            $"{open.Count} of ExSheet's Excel cases, in {files} file{(files == 1 ? "" : "s")}, are uncertain or documented without a written reason they cannot be asked ({Reason}). "
            + $"An {Reason} that says \"{AskByKeys}\" is no such reason, and neither is engineDiffersByDecision: oracle.ps1 asks both. "
            + $"A version without a suffix, and a release candidate, are tagged only once none is open: ask Excel with {string.Join(", and ", runs)}. "
            + "A case Excel disagrees with, or whose answer it only records (engineDiffersByDecision without excelExpect), is settled by a person, who marks it observed; "
            + $"a case that cannot be asked says why in {Reason} (ADR-0208).");
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
