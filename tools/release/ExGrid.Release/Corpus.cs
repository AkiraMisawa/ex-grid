using System.Text.Json;

namespace ExGrid.Release;

/// <summary>
/// ADR-0208's corpus check: a version without a suffix, and a release candidate, are refused while
/// any of ExSheet's Excel cases is still <c>uncertain</c> or <c>documented</c> without a written
/// reason it cannot be asked. The release asks Excel nothing; it checks that no question is open.
/// </summary>
public static class Corpus
{
    /// <summary>Where the cases are: one file per area, besides <c>fixtures.json</c>.</summary>
    public const string Directory = "tests/ExSheet.Engine.Tests/ExcelCases";

    /// <summary>
    /// The fields that hold a written reason a case cannot be put to Excel, or need not be:
    /// <c>oracleSkip</c>, why <c>oracle.ps1</c> cannot ask it (an undo, which COM cannot reach), and
    /// <c>engineDiffersByDecision</c>, the decision by which the engine answers otherwise than Excel.
    /// Reading the second as a written reason is this check's, and is the orchestrator's to confirm.
    /// </summary>
    public static readonly IReadOnlyList<string> Reasons = ["oracleSkip", "engineDiffersByDecision"];

    /// <summary>A case still to be asked of Excel: its file and its id.</summary>
    public sealed record OpenCase(string File, string Id, string Source);

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
                    if (source is "uncertain" or "documented" && Reasons.Any(reason => !string.IsNullOrWhiteSpace(Text(item, reason))))
                        continue;
                    open.Add(new OpenCase(file, id, source ?? "(no source)"));
                }
            }
        }
        return open;
    }

    /// <summary>Refuses the tag when any case is open, naming each by file, and the run of <c>oracle.ps1</c> that answers them.</summary>
    public static void Check(ISourceTree tree, Report report)
    {
        var refusedBefore = report.Refusals.Count;
        var open = Open(tree, report);
        if (open.Count == 0)
        {
            if (report.Refusals.Count == refusedBefore)
                report.Notice($"Every case under {Directory} is observed, or says why it cannot be asked (ADR-0208).");
            return;
        }
        foreach (var file in open.GroupBy(c => c.File))
        {
            var bySource = string.Join("; ", file.GroupBy(c => c.Source).Select(s => $"{s.Key}: {string.Join(", ", s.Select(c => c.Id))}"));
            report.Refuse($"{file.Count()} case{(file.Count() == 1 ? "" : "s")} in {file.Key} still to be asked of Excel ({bySource}).", file.Key);
        }
        var areas = open.Select(c => Path.GetFileNameWithoutExtension(c.File)).Distinct(StringComparer.Ordinal).ToList();
        report.Refuse($"{open.Count} of ExSheet's Excel cases, in {areas.Count} files, are uncertain or documented without a written reason they cannot be asked (oracleSkip, or engineDiffersByDecision). A version without a suffix, and a release candidate, are tagged only once none is: ask Excel with oracle.ps1 -Area {string.Join(',', areas)} -Update, or write why a case cannot be asked (ADR-0208).");
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
