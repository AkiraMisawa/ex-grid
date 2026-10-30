using System.Security;
using System.Text;
using Xunit;

namespace ExGrid.Release.Tests;

/// <summary>
/// The Definition of Done's §22 Step 2 read from Step 1's <c>.trx</c> files: every test passed, or is one Step 2
/// allows not to run, left out as Step 2 says. The samples are written in the shape xUnit v3's
/// <c>--report-xunit-trx</c> gave on 2026-10-12, read from a real run of Step 1's command: an explicit test not
/// asked for is <c>NotRunnable</c> with no output, a skip <c>NotExecuted</c> with its reason as <c>StdOut</c>, a
/// failure <c>Failed</c> with an <c>ErrorInfo</c>, and the summary <c>Completed</c> whether a test failed or not.
/// </summary>
public class SkipsTests
{
    private static readonly string[] Projects = ["ExGrid.Components", "ExGrid.Data.Tests", "ExGrid.Tests"];

    /// <summary>One result as the TRX writes it: the test's full name, its outcome, and a skip's reason or a failure's message.</summary>
    private sealed record Result(string Test, string Outcome = "Passed", string? Output = null);

    /// <summary>A TRX as xUnit v3 writes it, holding these results; <paramref name="total"/> and <paramref name="summary"/> as given, else as written.</summary>
    private static string Trx(IEnumerable<Result> results, int? total = null, string summary = "Completed")
    {
        var list = results.ToList();
        var text = new StringBuilder();
        text.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        text.Append("<TestRun id=\"d37d0904-703b-426e-a0e4-fe9895e07f33\" name=\"akira@nix-shell-env 10/12/2026 23:19:52\" runUser=\"akira\" xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">");
        text.Append("<Times creation=\"2026-10-12T22:19:50.6717909+00:00\" queuing=\"2026-10-12T22:19:50.6717909+00:00\" start=\"2026-10-12T22:19:50.6717909+00:00\" finish=\"2026-10-12T23:19:52.3677029+01:00\" />");
        text.Append("<TestSettings name=\"default\" id=\"6c4d5628-128d-4c3b-a1a4-ab366a4594ad\" /><Results>");
        foreach (var r in list)
        {
            var id = Guid.NewGuid();
            text.Append($"<UnitTestResult testType=\"13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b\" testListId=\"8c84fa94-04c1-424b-9868-57a2d4851a1d\" testName=\"{SecurityElement.Escape(r.Test)}\" testId=\"{id}\" executionId=\"{id}\" computerName=\"nix-shell-env\" outcome=\"{r.Outcome}\" duration=\"00:00:00.0000000\" startTime=\"2026-10-12T22:20:10.3419583+00:00\" endTime=\"2026-10-12T22:20:10.3429425+00:00\"");
            text.Append(r.Output is null ? " />"
                : r.Outcome == "Failed" ? $"><Output><ErrorInfo><Message>{SecurityElement.Escape(r.Output)}</Message><StackTrace>   at Probe.T.Fails()</StackTrace></ErrorInfo></Output></UnitTestResult>"
                : $"><Output><StdOut>{SecurityElement.Escape(r.Output)}</StdOut></Output></UnitTestResult>");
        }
        text.Append("</Results><TestDefinitions /><TestEntries /><TestLists />");
        int Count(string outcome) => list.Count(r => r.Outcome == outcome);
        text.Append($"<ResultSummary outcome=\"{summary}\"><Counters total=\"{total ?? list.Count}\" executed=\"{Count("Passed") + Count("Failed")}\" passed=\"{Count("Passed")}\" failed=\"{Count("Failed")}\" error=\"0\" timeout=\"0\" aborted=\"0\" inconclusive=\"0\" passedButRunAborted=\"0\" notRunnable=\"{Count("NotRunnable")}\" notExecuted=\"{Count("NotExecuted")}\" disconnected=\"0\" warning=\"0\" completed=\"0\" inProgress=\"0\" pending=\"0\" /></ResultSummary></TestRun>");
        return text.ToString();
    }

    private const string Clock = "ExGrid.Data.Tests.CsvClockTests.A_date_marked_utc_is_held_as_its_clock_in_any_time_zone";
    private const string ClockReason = "The platform does not let a process move its local time zone through TZ.";

    /// <summary>A run as Step 1 leaves it on a machine where TZ moves the zone: DA-17's two explicit, and CsvClockTests' one passed.</summary>
    private static List<TrxFile> Run(params (string Project, Result Result)[] besides)
    {
        var results = new Dictionary<string, List<Result>>
        {
            ["ExGrid.Components"] = [new("ExGrid.Components.Tests.SortWiringTests.A_click_past_the_last_column_is_ignored")],
            ["ExGrid.Data.Tests"] =
            [
                new("ExGrid.Data.Tests.MeasureTests.DA17_a_million_records_built_from_objects", "NotRunnable"),
                new("ExGrid.Data.Tests.MeasureTests.DA17_a_million_rows_read_from_a_CSV", "NotRunnable"),
                new(Clock),
            ],
            ["ExGrid.Tests"] = [new("ExGrid.Tests.FocusTests.Enter_moves_down")],
        };
        foreach (var (project, result) in besides) results[project].Add(result);
        return results.Select(p => new TrxFile(p.Key, $"verification/2026-11-02/{p.Key}.trx", Trx(p.Value))).ToList();
    }

    private static Report Check(IReadOnlyList<TrxFile> files, IReadOnlyList<string>? projects = null)
    {
        var report = new Report();
        Skips.Check(files, projects ?? Projects, report);
        return report;
    }

    [Fact] // DoD §22 Step 2: a run where every test passed or is one Step 2 allows, left out as it says, passes, and names each allowed one
    public void A_run_with_only_the_allowed_tests_not_run_passes()
    {
        var report = Check(Run());

        Assert.Empty(report.Refusals);
        var named = Assert.Single(report.Notices, n => n.StartsWith("Not run, as Step 2 allows", StringComparison.Ordinal));
        Assert.Contains("ExGrid.Data.Tests.MeasureTests.DA17_a_million_rows_read_from_a_CSV (DA-17, explicit, not asked for)", named);
        Assert.DoesNotContain("CsvClockTests", named);
        Assert.Contains(report.Notices, n => n.Contains("Step 2: 5 tests in 3 test projects, every one passed or allowed"));
    }

    [Fact] // DoD §22 Step 2: CsvClockTests' one test may skip by name where TZ cannot move the zone, giving its reason
    public void CsvClockTests_skip_is_allowed_with_its_reason()
    {
        var files = Run();
        files[1] = files[1] with
        {
            Text = Trx([new("ExGrid.Data.Tests.MeasureTests.DA17_a_million_records_built_from_objects", "NotRunnable"), new(Clock, "NotExecuted", ClockReason)]),
        };

        var report = Check(files);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains($"{Clock} (ADR-0064, skipped: {ClockReason})"));
        Assert.Contains(report.Notices, n => n.Contains("CsvClockTests' skip is allowed only where a process cannot move its time zone through TZ"));
    }

    [Fact] // DoD §22 Step 2: a test that skipped and is not one Step 2 names fails the run, named by project and test
    public void A_skip_outside_the_allowed_set_is_refused_by_name()
    {
        var report = Check(Run(("ExGrid.Tests", new Result("ExGrid.Tests.FocusTests.Tab_wraps", "NotExecuted", "Not on this platform"))));

        var refusal = Assert.Single(report.Refusals);
        Assert.Contains("ExGrid.Tests.FocusTests.Tab_wraps (ExGrid.Tests) did not run (outcome NotExecuted, saying 'Not on this platform'), and it is not one of the tests Step 2 allows", refusal.Message);
        Assert.Equal("verification/2026-11-02/ExGrid.Tests.trx", refusal.File);
    }

    [Fact] // DoD §22 Step 2: an explicit test Step 2 does not name, not asked for, fails the run
    public void An_explicit_test_outside_the_allowed_set_is_refused()
    {
        var report = Check(Run(("ExGrid.Tests", new Result("ExGrid.Tests.Measure.A_new_measurement", "NotRunnable"))));

        Assert.Contains("ExGrid.Tests.Measure.A_new_measurement (ExGrid.Tests) did not run (outcome NotRunnable)", Assert.Single(report.Refusals).Message);
    }

    [Fact] // DoD §22 Step 2: an allowed test is allowed in its own project only
    public void An_allowed_test_in_another_project_is_refused()
    {
        var report = Check(Run(("ExGrid.Tests", new Result(Clock, "NotExecuted", ClockReason))));

        Assert.Contains($"{Clock} (ExGrid.Tests) did not run", Assert.Single(report.Refusals).Message);
    }

    [Fact] // DoD §22 Step 2: zero failures in every project
    public void A_failed_test_is_refused()
    {
        var report = Check(Run(("ExGrid.Tests", new Result("ExGrid.Tests.FocusTests.Tab_wraps", "Failed", "boom"))));

        Assert.Contains("ExGrid.Tests.FocusTests.Tab_wraps (ExGrid.Tests) failed", Assert.Single(report.Refusals).Message);
    }

    [Theory] // DoD §22 Step 2: an outcome that is neither a pass nor a failure nor the two Step 2 allows is refused, never read as a pass
    [InlineData("Timeout")]
    [InlineData("Aborted")]
    [InlineData("Inconclusive")]
    [InlineData("")]
    public void Any_other_outcome_is_refused(string outcome)
    {
        var report = Check(Run(("ExGrid.Tests", new Result("ExGrid.Tests.FocusTests.Tab_wraps", outcome))));

        Assert.Contains($"did not run (outcome {outcome})", Assert.Single(report.Refusals).Message);
    }

    [Fact] // DoD §22 Step 2: an explicit measurement Step 2 names must be left out as explicit, a switch-gated test as a skip
    public void An_allowed_test_left_out_otherwise_than_step_2_says_is_refused()
    {
        var files = Run();
        files[1] = files[1] with
        {
            Text = Trx(
            [
                new("ExGrid.Data.Tests.MeasureTests.DA17_a_million_records_built_from_objects", "NotRunnable"),
                new("ExGrid.Data.Tests.MeasureTests.DA17_a_million_rows_read_from_a_CSV", "NotExecuted", "Skipped by hand"),
                new(Clock, "NotRunnable"),
            ]),
        };

        var refusals = Check(files).Refusals;

        Assert.Equal(2, refusals.Count);
        Assert.Contains("DA17_a_million_rows_read_from_a_CSV (ExGrid.Data.Tests) is allowed by Step 2 as an explicit measurement not asked for, which the TRX writes NotRunnable, and its .trx says outcome NotExecuted", refusals[0].Message);
        Assert.Contains($"{Clock} (ExGrid.Data.Tests) is allowed by Step 2 as a skip by name, which the TRX writes NotExecuted, and its .trx says outcome NotRunnable", refusals[1].Message);
    }

    [Fact] // DoD §22 Step 2: an allowed skip holds only where Step 2 allows it, known by the reason its test gives
    public void An_allowed_skip_with_another_reason_is_refused()
    {
        var files = Run();
        files[1] = files[1] with
        {
            Text = Trx([new("ExGrid.Data.Tests.MeasureTests.DA17_a_million_records_built_from_objects", "NotRunnable"), new(Clock, "NotExecuted", "Something else")]),
        };

        Assert.Contains($"skipped saying 'Something else', and Step 2 allows its skip only for '{ClockReason}'", Assert.Single(Check(files).Refusals).Message);
    }

    [Fact] // DoD §1, §22 Steps 1 and 2: a test project with no .trx cannot be read, and a .trx of no test project is not Step 1's
    public void A_missing_or_a_stray_trx_is_refused()
    {
        var files = Run().Where(f => f.Project != "ExGrid.Tests").Append(new TrxFile("ExGrid.Old.Tests", "verification/2026-11-02/ExGrid.Old.Tests.trx", Trx([new("A.B")]))).ToList();

        var refusals = Check(files).Refusals;

        Assert.Equal(2, refusals.Count);
        Assert.Contains("ExGrid.Tests is a test project of dotnet/ExGrid.slnx, and no ExGrid.Tests.trx is here", refusals[0].Message);
        Assert.Contains("verification/2026-11-02/ExGrid.Old.Tests.trx is named for no test project of dotnet/ExGrid.slnx", refusals[1].Message);
    }

    public static TheoryData<string?, string> Unreadable => new()
    {
        { null, "cannot be read" },
        { "", "is not XML" },
        { "<TestRun", "is not XML" },
        { "<TestRun><Results /></TestRun>", "is not a TRX" },
        { "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results /></TestRun>", "has no result summary" },
        { Trx([new("ExGrid.Tests.FocusTests.Enter_moves_down")], total: 2), "holds 1 results, and its summary counts 2 tests" },
        { Trx([]), "holds no test" },
        { Trx([new("ExGrid.Tests.FocusTests.Enter_moves_down")], summary: "Aborted"), "run reads 'Aborted', not Completed" },
    };

    [Theory] // DoD §22 Step 2: a .trx that cannot be read whole refuses the run, never passes it
    [MemberData(nameof(Unreadable))]
    public void A_trx_that_cannot_be_read_whole_is_refused(string? text, string expected)
    {
        var files = Run();
        files[2] = files[2] with { Text = text };

        var report = Check(files);

        Assert.Contains(expected, Assert.Single(report.Refusals).Message);
        Assert.DoesNotContain(report.Notices, n => n.StartsWith("Step 2:", StringComparison.Ordinal));
    }

    [Fact] // DoD §22 Step 1: each .trx is named for its project's directory under tests/, as the solution names it
    public void The_test_projects_are_the_solutions_projects_under_tests()
    {
        var report = new Report();
        var projects = Skips.TestProjects("""
            <Solution>
              <Project Path="src/ExGrid/ExGrid.csproj" />
              <Project Path="tests/ExPivot.Components/ExPivot.Components.csproj" />
              <Project Path="tests/ExGrid.Tests/ExGrid.Tests.csproj" />
              <Project Path="tools/release/ExGrid.Release/ExGrid.Release.csproj" />
            </Solution>
            """, report);

        Assert.Empty(report.Refusals);
        Assert.Equal(["ExGrid.Tests", "ExPivot.Components"], projects);
        Assert.Null(Skips.TestProjects(null, report));
        Assert.Null(Skips.TestProjects("<Solution", report));
        Assert.Equal(2, report.Refusals.Count);
    }

    [Fact] // DoD §22 Step 2: the .trx files are read from the directory Step 1 copied them into, each named for its project
    public void The_trx_files_are_read_from_a_directory()
    {
        var directory = Directory.CreateTempSubdirectory("exgrid-release-trx-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "ExGrid.Tests.trx"), Trx([new("ExGrid.Tests.FocusTests.Enter_moves_down")]));
            File.WriteAllText(Path.Combine(directory.FullName, "layers-1-2.log"), "not a trx");
            var report = new Report();

            var files = Skips.Read(directory.FullName, report);
            Skips.Check(files, ["ExGrid.Tests"], report);

            Assert.Equal("ExGrid.Tests", Assert.Single(files).Project);
            Assert.Empty(report.Refusals);

            var missing = new Report();
            Assert.Empty(Skips.Read(Path.Combine(directory.FullName, "absent"), missing));
            Assert.Contains("is not a directory", Assert.Single(missing.Refusals).Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
