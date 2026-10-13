using System.Xml;
using System.Xml.Linq;

namespace ExGrid.Release;

/// <summary>How a test the Definition of Done's §22 Step 2 allows is left out of Step 1's run.</summary>
public enum AllowedKind
{
    /// <summary>A <c>[Fact(Explicit = true)]</c> measurement, not asked for: xUnit v3's TRX writes it <c>NotRunnable</c>.</summary>
    Explicit,

    /// <summary>A test that skips itself by name behind a switch: the TRX writes it <c>NotExecuted</c>, its reason as its output.</summary>
    Skipped,
}

/// <summary>
/// A test Step 2 allows not to run: the test project whose <c>.trx</c> holds it, its name as the TRX writes it
/// (<c>testName</c>, the class in full and the method, with a Theory's arguments), how it is left out, the
/// criterion that names it, and for a skip the reason its test gives, which is how the reader knows the skip
/// came from the switch Step 2 names and not from another.
/// </summary>
public sealed record AllowedSkip(string Project, string Test, AllowedKind Kind, string Criterion, string? Reason = null)
{
    /// <summary>How a message names it.</summary>
    public string Name => $"{Test} ({Project})";
}

/// <summary>One <c>.trx</c> file of Step 1's run: the test project it is named for, where it was read from, and its text, null when it could not be read.</summary>
public sealed record TrxFile(string Project, string Path, string? Text);

/// <summary>
/// The Definition of Done's §22 Step 2, read mechanically: every test of every test project ran and passed, but
/// the ones Step 2 names, each found by name in its project's <c>.trx</c>. Step 1 writes one <c>results.trx</c> per
/// test project with xUnit v3's <c>--report-xunit-trx</c>, copied out as <c>verification/&lt;date&gt;/&lt;project&gt;.trx</c>.
/// <para>The allowed tests are a list in this code, <see cref="Allowed"/>, written as Step 2 names them; a layer-1
/// test holds it to Step 2's text and to the tests in the source, so the two cannot drift (2026-10-12, the release
/// tool's choice). How a TRX shows each was read from a real run of Step 1's command on 2026-10-12: an explicit
/// test not asked for is <c>outcome="NotRunnable"</c> with no output; a test that skips itself is
/// <c>outcome="NotExecuted"</c>, its reason in <c>Output/StdOut</c>; a test that ran is <c>Passed</c> or
/// <c>Failed</c>, and the summary reads <c>Completed</c> whether a test failed or not. Any other outcome is refused, as is a skip whose reason is not the one its test gives.</para>
/// <para>Never passing silently: a test project of <c>ExGrid.slnx</c> with no <c>.trx</c>, a <c>.trx</c> of no test
/// project, a file that is not a TRX, one with no result summary, and one whose counters disagree with its
/// results each refuse the run.</para>
/// <para>Run by the person making the sign-off, as Step 2 (<c>skips --trx verification/&lt;date&gt;</c>). The release
/// workflow does not call it: it runs no Step 1, and CI's run of layers 1 and 2 writes coverage, not these files.</para>
/// </summary>
public static class Skips
{
    /// <summary>The solution whose test projects Step 1 runs.</summary>
    public const string Solution = "ExGrid.slnx";

    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// What Step 2 allows, as it names them: ST-1's 10⁶-row case; the ten explicit measurements, DA-17's two,
    /// ADR-0065's one, PV-21's four and SH-19's three; and <c>CsvClockTests</c>' one test where the platform does
    /// not let a process move its time zone through <c>TZ</c>.
    /// </summary>
    public static readonly IReadOnlyList<AllowedSkip> Allowed =
    [
        new("ExGrid.Components", "ExGrid.Components.Tests.ConsistencyTests.Five_hundred_random_operations_leave_every_invariant_standing(seed: 20260901, totalRows: 1000000)",
            AllowedKind.Skipped, "ST-1", "ST-1 at 10⁶ rows runs with EXGRID_ST1_MILLION=1 (Definition of Done §22 Step 5)"),
        new("ExGrid.Data.Tests", "ExGrid.Data.Tests.MeasureTests.DA17_a_million_records_built_from_objects", AllowedKind.Explicit, "DA-17"),
        new("ExGrid.Data.Tests", "ExGrid.Data.Tests.MeasureTests.DA17_a_million_rows_read_from_a_CSV", AllowedKind.Explicit, "DA-17"),
        new("ExGrid.Data.Arrow.Tests", "ExGrid.Data.Arrow.Tests.MeasureTests.Measure_a_million_trades", AllowedKind.Explicit, "ADR-0065"),
        new("ExPivot.Engine.Tests", "ExPivot.Engine.Tests.Measurements.Building_the_snapshot_from_typed_declarations", AllowedKind.Explicit, "PV-21"),
        new("ExPivot.Engine.Tests", "ExPivot.Engine.Tests.Measurements.Asking_a_million_records", AllowedKind.Explicit, "PV-21"),
        new("ExPivot.Engine.Tests", "ExPivot.Engine.Tests.Measurements.Laying_out_the_answer_held", AllowedKind.Explicit, "PV-21"),
        new("ExPivot.Engine.Tests", "ExPivot.Engine.Tests.Measurements.Folding_a_thousand_changes_into_a_million_records", AllowedKind.Explicit, "PV-21"),
        new("ExSheet.Engine.Tests", "ExSheet.Engine.Tests.Measurements.SH19_a_full_recalculation", AllowedKind.Explicit, "SH-19"),
        new("ExSheet.Engine.Tests", "ExSheet.Engine.Tests.Measurements.SH19_one_edits_propagation", AllowedKind.Explicit, "SH-19"),
        new("ExSheet.Engine.Tests", "ExSheet.Engine.Tests.Measurements.SH19_a_row_inserted_near_the_top", AllowedKind.Explicit, "SH-19"),
        new("ExGrid.Data.Tests", "ExGrid.Data.Tests.CsvClockTests.A_date_marked_utc_is_held_as_its_clock_in_any_time_zone",
            AllowedKind.Skipped, "ADR-0064", "The platform does not let a process move its local time zone through TZ."),
    ];

    /// <summary>
    /// The test projects of <paramref name="solution"/>'s text, by their directory under <c>tests/</c>, which is
    /// the name Step 1's loop gives each project's <c>.trx</c>; null, refused, when it cannot be read.
    /// </summary>
    public static IReadOnlyList<string>? TestProjects(string? solution, Report report)
    {
        if (solution is null)
        {
            report.Refuse($"There is no {Solution}, so which test projects Step 1 ran cannot be said (the Definition of Done's §22 Step 2).");
            return null;
        }
        try
        {
            var projects = XDocument.Parse(solution).Descendants("Project")
                .Select(p => ((string?)p.Attribute("Path") ?? "").Replace('\\', '/').Split('/'))
                .Where(parts => parts.Length == 3 && parts[0] == "tests" && parts[2].EndsWith(".csproj", StringComparison.Ordinal))
                .Select(parts => parts[1])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            if (projects.Count == 0) report.Refuse($"{Solution} names no project under tests/, so which test projects Step 1 ran cannot be said.");
            return projects.Count == 0 ? null : projects;
        }
        catch (XmlException e)
        {
            report.Refuse($"{Solution} cannot be read ({e.Message}).", Solution);
            return null;
        }
    }

    /// <summary>
    /// The <c>.trx</c> files directly inside <paramref name="directory"/>, each named for its project; a file that
    /// cannot be read has no text. Refused, and empty, when the directory is not there.
    /// </summary>
    public static IReadOnlyList<TrxFile> Read(string directory, Report report)
    {
        if (!Directory.Exists(directory))
        {
            report.Refuse($"{directory} is not a directory, so no .trx of Step 1's run can be read (the Definition of Done's §22 Step 2).");
            return [];
        }
        return Directory.GetFiles(directory, "*.trx")
            .Order(StringComparer.Ordinal)
            .Select(path =>
            {
                string? text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { text = null; }
                return new TrxFile(Path.GetFileNameWithoutExtension(path), path, text);
            })
            .ToList();
    }

    /// <summary>
    /// Refuses the run unless every test project of <paramref name="projects"/> has its <c>.trx</c> among
    /// <paramref name="files"/>, each is read in full, and every test in them passed, or is one Step 2 allows, left
    /// out as Step 2 says. Each allowed test found not run is named in a notice.
    /// </summary>
    public static void Check(IReadOnlyList<TrxFile> files, IReadOnlyList<string> projects, Report report)
    {
        foreach (var missing in projects.Where(p => files.All(f => f.Project != p)))
            report.Refuse($"{missing} is a test project of {Solution}, and no {missing}.trx is here, so whether its tests ran cannot be read. Step 1 writes one .trx for each test project (the Definition of Done's §22 Step 1).");
        foreach (var stray in files.Where(f => !projects.Contains(f.Project)))
            report.Refuse($"{stray.Path} is named for no test project of {Solution}. Step 1's loop names each .trx for its project's directory under tests/.", stray.Path);

        var allowedRun = new List<string>();
        var testCount = 0;
        foreach (var file in files.Where(f => projects.Contains(f.Project)))
        {
            var results = Results(file, report);
            if (results is null) continue;
            testCount += results.Count;
            foreach (var (test, outcome, output) in results)
            {
                if (outcome == "Passed") continue;
                var allowed = Allowed.FirstOrDefault(a => a.Project == file.Project && a.Test == test);
                var expected = allowed?.Kind switch
                {
                    AllowedKind.Explicit => "NotRunnable",
                    AllowedKind.Skipped => "NotExecuted",
                    _ => null,
                };
                if (outcome == "Failed")
                {
                    report.Refuse($"{test} ({file.Project}) failed. Step 2 passes on zero failures in every project.", file.Path);
                }
                else if (allowed is null)
                {
                    report.Refuse($"{test} ({file.Project}) did not run ({Says(outcome, output)}), and it is not one of the tests Step 2 allows not to run. A test that did not run, skipped or explicit and not asked for, is a failure unless Step 2 names it (the Definition of Done's §22 Step 2).", file.Path);
                }
                else if (outcome != expected)
                {
                    report.Refuse($"{allowed.Name} is allowed by Step 2 as {(allowed.Kind == AllowedKind.Explicit ? "an explicit measurement not asked for, which the TRX writes NotRunnable" : "a skip by name, which the TRX writes NotExecuted")}, and its .trx says {Says(outcome, output)}.", file.Path);
                }
                else if (allowed.Kind == AllowedKind.Skipped && output?.Trim() != allowed.Reason)
                {
                    report.Refuse($"{allowed.Name} skipped saying '{output?.Trim()}', and Step 2 allows its skip only for '{allowed.Reason}', the reason its test gives behind the switch Step 2 names.", file.Path);
                }
                else
                {
                    allowedRun.Add(allowed.Kind == AllowedKind.Explicit
                        ? $"{allowed.Test} ({allowed.Criterion}, explicit, not asked for)"
                        : $"{allowed.Test} ({allowed.Criterion}, skipped: {allowed.Reason})");
                }
            }
        }

        if (allowedRun.Count > 0)
            report.Notice($"Not run, as Step 2 allows: {string.Join("; ", allowedRun)}."
                + (allowedRun.Any(a => a.Contains(".CsvClockTests.", StringComparison.Ordinal))
                    ? " CsvClockTests' skip is allowed only where a process cannot move its time zone through TZ, a Windows machine whose own zone's base offset is zero: its reason says the zone did not move. The machine is not in the .trx: whoever signs off confirms from the run that it was such a machine."
                    : "")
                + " (The Definition of Done's §22 Step 2.)");
        if (report.Admitted)
            report.Notice($"Step 2: {testCount} tests in {projects.Count} test projects, every one passed or allowed (the Definition of Done's §22 Step 2). CON-7's count of unobserved exceptions is not in the .trx, and is read from layers-1-2.log.");
    }

    // A TRX's results, as (testName, outcome, the output's StdOut); null, refused, when the file cannot be read as
    // a whole TRX.
    private static List<(string Test, string Outcome, string? Output)>? Results(TrxFile file, Report report)
    {
        if (file.Text is null)
        {
            report.Refuse($"{file.Path} cannot be read, so whether {file.Project}'s tests ran cannot be said.", file.Path);
            return null;
        }
        XDocument document;
        try
        {
            document = XDocument.Parse(file.Text);
        }
        catch (XmlException e)
        {
            report.Refuse($"{file.Path} is not XML ({e.Message}), so whether {file.Project}'s tests ran cannot be said.", file.Path);
            return null;
        }
        var root = document.Root;
        if (root is null || root.Name != Trx + "TestRun")
        {
            report.Refuse($"{file.Path} is not a TRX: its root is not a TestRun of {Trx.NamespaceName}.", file.Path);
            return null;
        }
        var summary = root.Element(Trx + "ResultSummary");
        var counters = summary?.Element(Trx + "Counters");
        if (summary is null || counters is null || !int.TryParse((string?)counters.Attribute("total"), out var total))
        {
            report.Refuse($"{file.Path} has no result summary with its count of tests, so it may have been cut short: a run stopped by its timeout, or a file copied while it was written.", file.Path);
            return null;
        }
        var results = root.Elements(Trx + "Results").Elements(Trx + "UnitTestResult")
            .Select(r => ((string?)r.Attribute("testName") ?? "", (string?)r.Attribute("outcome") ?? "", (string?)r.Element(Trx + "Output")?.Element(Trx + "StdOut")))
            .ToList();
        if (results.Count != total)
        {
            report.Refuse($"{file.Path} holds {results.Count} results, and its summary counts {total} tests, so it is not read as whole.", file.Path);
            return null;
        }
        if (results.Count == 0)
        {
            report.Refuse($"{file.Path} holds no test, and every test project of {Solution} has tests.", file.Path);
            return null;
        }
        // A run that completed reads Completed, failures or not: a failed test is its own result's outcome.
        if ((string?)summary.Attribute("outcome") is var outcome && outcome != "Completed")
            report.Refuse($"{file.Path}'s run reads '{outcome}', not Completed, so it is not read as a whole run (the Definition of Done's §22 Step 2).", file.Path);
        return results;
    }

    private static string Says(string outcome, string? output) =>
        output is { Length: > 0 } ? $"outcome {outcome}, saying '{output.Trim()}'" : $"outcome {outcome}";
}
