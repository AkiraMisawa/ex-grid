using System.Text.RegularExpressions;
using Xunit;

namespace ExGrid.Release.Tests;

/// <summary>
/// The repository's own files, as the release check will read them at a tag: the record index of the
/// rows checked by hand held to the Definition of Done's table (or its proposal, until the table lands),
/// every path they name present, and the corpus readable. A drift here would otherwise first show on a
/// tag, after the sign-off.
/// </summary>
public partial class RepositoryTests
{
    private static readonly DirectoryTree Tree = new(RepositoryRoot());

    /// <summary>
    /// The rows ADR-0208 names as checked by hand, and WR-10, the Wrappers' MudBlazor floor run, which a
    /// person makes once and its record carries forward (ADR-0205's notes of 2026-10-12).
    /// </summary>
    private static readonly string[] IndexedRows = ["VZ-14", "VZ-16", "FN-12e", "ED-30", "SH-42", "SH-51", "SH-52", "KB-3", "VZ-18", "§21.11", "WR-10"];

    [Fact] // ADR-0208: the record index and the Definition of Done's table cannot drift
    public void The_record_index_and_the_table_name_the_same_rows_and_paths()
    {
        var report = new Report();
        var index = ByHand.ReadIndex(Tree, report);
        var table = ByHandTable.Read(Tree, report);
        Assert.NotNull(index);
        Assert.NotNull(table);

        ByHand.Agree(index, table, report);

        Assert.True(report.Admitted, string.Join("\n", report.Refusals.Select(r => r.Message)));
        Assert.Equal(index.Count, table.Rows.Count);
    }

    [Fact] // ADR-0208, ADR-0205 (2026-10-12): every row the decision names is indexed, and WR-10's floor run beside them
    public void Every_row_ADR_0208_names_and_WR_10_are_indexed()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        Assert.Equal(IndexedRows.Order(StringComparer.Ordinal), index.Select(r => r.Row).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0208 (2026-10-12): FN-12e is a Windows row and a macOS row, and WR-10 reads every file below each Wrapper and the demo pages
    public void FN_12e_is_two_rows_and_WR_10_names_the_wrappers_and_the_demo_pages()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        Assert.Equal(["Windows", "macOS"], index.Where(r => r.Row == "FN-12e").Select(r => r.Where));
        Assert.All(index.Where(r => r.Row == "FN-12e"), r =>
        {
            Assert.Contains("src/ExGrid.MudBlazor/MudExGridPresentation.cs", r.Paths);
            Assert.Contains("src/ExGrid.MudBlazor/RobotoGlyphTable.cs", r.Paths);
        });
        var wr10 = Assert.Single(index, r => r.Row == "WR-10");
        Assert.Equal([":(glob)src/*.MudBlazor/**", "samples/ExGrid.DemoPages/"], wr10.Paths);
        var wrappers = Tree.Matches(wr10.Paths[0]);
        foreach (var wrapper in new[] { "src/ExGrid.MudBlazor/", "src/ExSheet.MudBlazor/", "src/ExPivot.MudBlazor/" })
            Assert.Contains(wrappers, f => f.StartsWith(wrapper, StringComparison.Ordinal) && f.EndsWith(".csproj", StringComparison.Ordinal));
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): the release check can read the MudBlazor range each Wrapper declares, which WR-10's record is held to
    public void The_Wrappers_MudBlazor_range_is_readable_by_the_release_check()
    {
        var report = new Report();
        var declared = WrapperRange.Read(Tree, report, WrapperRange.Row);

        Assert.True(report.Admitted, string.Join("\n", report.Refusals.Select(r => r.Message)));
        Assert.Equal(
            ["src/ExGrid.MudBlazor/ExGrid.MudBlazor.csproj", "src/ExPivot.MudBlazor/ExPivot.MudBlazor.csproj", "src/ExSheet.MudBlazor/ExSheet.MudBlazor.csproj"],
            declared!.Select(d => d.Project).Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): every record the index names is tagged verified/ followed by its directory's name
    public void Every_record_is_tagged_for_its_directory()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        Assert.All(index.Where(r => r.Record is not null), r => Assert.Equal(ByHand.TagFor(r.Record!.Path), r.Record!.Tag));
        Assert.Equal("verified/2026-10-01-windows-browser-11", Assert.Single(index, r => r.Row == "SH-42").Record!.Tag);
    }

    [Fact] // ADR-0208: a path that names nothing is a list nobody reviewed when its code moved
    public void Every_path_and_record_the_index_names_is_in_the_repository()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        var missing = index
            .SelectMany(row => row.Paths.Select(path => (row, path)))
            .Where(p => !ByHand.Names(Tree, p.path))
            .Select(p => $"{p.row.Name}: {p.path}")
            .Concat(index.Where(r => r.Record is { } record && !Tree.IsFile(record.Path)).Select(r => $"{r.Name}'s record: {r.Record!.Path}"))
            .Concat(index.Where(r => r.Record is { } record && Tree.ReadText(record.Path) is { } text && !SignOff.Names(text, record.Commit))
                .Select(r => $"{r.Name}'s record {r.Record!.Path} does not name {r.Record.Commit}"))
            .ToList();

        Assert.True(missing.Count == 0, "Not in the repository: " + string.Join(", ", missing));
    }

    [Fact] // ADR-0208: the corpus check can read every file of today's corpus
    public void The_corpus_is_readable_by_the_release_check()
    {
        var report = new Report();
        var open = Corpus.Open(Tree, report);

        Assert.True(report.Admitted, string.Join("\n", report.Refusals.Select(r => r.Message)));
        Assert.All(open, c => Assert.StartsWith(Corpus.Directory + "/", c.File));
    }

    [Fact] // ADR-0042, ADR-0205: the release workflow runs the checks this project pins
    public void The_release_workflow_runs_the_checks()
    {
        var release = Tree.ReadText(".github/workflows/release.yml")!;

        Assert.Contains("tools/release/ExGrid.Release -c Release -- admit", release);
        Assert.Contains("tools/release/ExGrid.Release -c Release -- gate", release);
        // WR-10's records are held to the MudBlazor versions nuget.org's registration index dates (ADR-0205, ADR-0208, 2026-10-12).
        Assert.Contains($"--mudblazor-index {WrapperRange.Registration}", release);
        Assert.Contains("long: ${{ needs.admit.outputs.long == 'true' }}", release);
        Assert.Contains("if: needs.admit.outputs.site == 'true'", release);
        // A candidate's release opens with the candidate's note, any other prerelease's with the prerelease note (ADR-0190, 2026-10-12).
        Assert.Contains("cat docs/releases/candidate-note.md", release);
        Assert.Contains("cat docs/releases/prerelease-note.md", release);
    }

    [Fact] // DoD §22 Step 2: the explicit tests the release tool allows are exactly the [Fact(Explicit = true)] tests in the source, ten
    public void The_allowed_explicit_tests_are_the_explicit_tests_in_the_source()
    {
        var found = new List<string>();
        foreach (var (project, file, text) in TestSources())
        {
            foreach (Match explicitTest in ExplicitTest().Matches(text))
                found.Add($"{project}: {Namespace(text, file)}.{ClassBefore(text, explicitTest.Index, file)}.{explicitTest.Groups[1].Value}");
        }

        var allowed = Skips.Allowed.Where(a => a.Kind == AllowedKind.Explicit).Select(a => $"{a.Project}: {a.Test}");
        Assert.Equal(allowed.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
        Assert.Equal(10, found.Count);
    }

    [Fact] // DoD §22 Step 2: every test that skips itself is one the release tool allows, and the reason it allows is the one the test gives
    public void Every_skip_in_the_source_is_allowed_with_the_reason_it_gives()
    {
        var found = new List<string>();
        foreach (var (project, file, text) in TestSources())
        {
            Assert.DoesNotMatch(@"\[(?:Fact|Theory)\([^\]]*\bSkip\s*=", text);
            foreach (Match skip in SkipCall().Matches(text))
            {
                var reason = Constant().Replace(skip.Groups[1].Value, m => ConstantValue(text, m.Groups[1].Value, file));
                found.Add($"{project}: {Namespace(text, file)}.{ClassBefore(text, skip.Index, file)}: {reason}");
            }
        }

        var allowed = Skips.Allowed.Where(a => a.Kind == AllowedKind.Skipped)
            .Select(a => a.Test.Split('(')[0] is var method ? $"{a.Project}: {method[..method.LastIndexOf('.')]}: {a.Reason}" : "");
        Assert.Equal(allowed.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
        // ST-1's 10⁶ case is the Theory's one row at a million, with the seed the allowed name gives.
        var consistency = Tree.ReadText("tests/ExGrid.Components/ConsistencyTests.cs")!;
        Assert.Single(Regex.Matches(consistency, @"\[InlineData\(\d+, 1_000_000\)\]"));
        Assert.Contains("[InlineData(20260901, 1_000_000)]", consistency);
        Assert.Contains("public async Task Five_hundred_random_operations_leave_every_invariant_standing(int seed, int totalRows)", consistency);
    }

    [Fact] // DoD §22 Step 2: the release tool's list is the one Step 2 writes: the ten explicit measurements by their classes, ST-1's case and CsvClockTests'
    public void The_allowed_tests_are_the_ones_step_2_names()
    {
        var dod = Tree.ReadText(ByHandTable.DefinitionOfDone)!;
        var start = dod.IndexOf("### Step 2 — Reading the run", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Definition of Done has no Step 2.");
        var end = dod.IndexOf("\n### ", start + 1, StringComparison.Ordinal);
        var step2 = dod[start..(end < 0 ? dod.Length : end)];

        Assert.Contains("**the ten `[Fact(Explicit = true)]` measurements**", step2);
        Assert.Equal(10, Skips.Allowed.Count(a => a.Kind == AllowedKind.Explicit));
        foreach (var allowed in Skips.Allowed)
        {
            var parts = allowed.Test.Split('(')[0].Split('.');
            Assert.Contains($"`{allowed.Project}`", step2);
            Assert.Contains(parts[^2], step2);
            // Step 2 names CsvClockTests by its class alone; the others by the criterion that measures or runs them.
            if (allowed.Criterion != "ADR-0064") Assert.Contains(allowed.Criterion, step2);
        }
        foreach (var sh19 in Skips.Allowed.Where(a => a.Criterion == "SH-19"))
            Assert.Contains($"`{sh19.Test.Split('.')[^1]}`", step2);
        Assert.Equal(3, Skips.Allowed.Count(a => a.Criterion == "SH-19"));
        Assert.Equal(4, Skips.Allowed.Count(a => a.Criterion == "PV-21"));
        Assert.Equal(2, Skips.Allowed.Count(a => a.Criterion == "DA-17"));
    }

    [Fact] // DoD §1, §22 Step 1: the release tool reads the solution's test projects, one .trx each, and every allowed test's project is one
    public void The_solutions_test_projects_are_readable_by_the_release_tool()
    {
        var report = new Report();
        var projects = Skips.TestProjects(Tree.ReadText(Skips.Solution), report)!;

        Assert.True(report.Admitted, string.Join("\n", report.Refusals.Select(r => r.Message)));
        Assert.Contains("ExGrid.Release.Tests", projects);
        Assert.All(projects, p => Assert.True(Tree.IsDirectory($"tests/{p}"), p));
        Assert.All(Skips.Allowed, a => Assert.Contains(a.Project, projects));
    }

    [Fact] // ADR-0208 (VZ-18, decided with the user 2026-10-12): VZ-18 is a Known limitation of 0.1.0 in the index and the table, with no record
    public void VZ_18_is_a_known_limitation_of_0_1_0()
    {
        var report = new Report();
        var index = ByHand.ReadIndex(Tree, report)!;
        var table = ByHandTable.Read(Tree, report)!;

        Assert.True(report.Admitted, string.Join("\n", report.Refusals.Select(r => r.Message)));
        var vz18 = Assert.Single(index, r => r.Row == "VZ-18");
        Assert.Null(vz18.Record);
        Assert.Equal(new KnownLimitation("0.1.0", "2026-10-12",
            "VZ-18, the check on the remote-isolation (Citrix) PC, is a Known limitation of 0.1.0. It is not run, and the 2026-10-08 observation, which names no commit, is not taken as its record."), vz18.Limitation);
        Assert.Equal("0.1.0", Assert.Single(table.Rows, r => r.Row == "VZ-18").KnownLimitation);
        Assert.All(index.Where(r => r.Row != "VZ-18"), r => Assert.Null(r.Limitation));
    }

    // Every .cs file of every test project but this one, whose own tests write the patterns as text.
    private static IEnumerable<(string Project, string File, string Text)> TestSources()
    {
        var tests = Path.Combine(RepositoryRoot(), "tests");
        foreach (var project in Directory.GetDirectories(tests).Select(Path.GetFileName).Where(p => p != "ExGrid.Release.Tests"))
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(tests, project!), "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(tests, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                yield return (project!, relative, File.ReadAllText(file));
            }
        }
    }

    private static string Namespace(string text, string file) =>
        Regex.Match(text, @"^namespace ([\w.]+)", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : throw new InvalidOperationException($"{file} has no namespace.");

    private static string ClassBefore(string text, int at, string file) =>
        Regex.Matches(text[..at], @"\bclass (\w+)").LastOrDefault()?.Groups[1].Value ?? throw new InvalidOperationException($"{file} has no class before {at}.");

    private static string ConstantValue(string text, string name, string file) =>
        Regex.Match(text, $@"const string {name} = ""([^""]*)""") is { Success: true } m ? m.Groups[1].Value : throw new InvalidOperationException($"{file} has no constant {name}.");

    // An explicit test, and its method's name.
    [GeneratedRegex(@"\[(?:Fact|Theory)\([^\]]*\bExplicit\s*=\s*true[^\]]*\][^\n]*\n(?:\s*\[[^\n]*\n)*\s*public\s+(?:async\s+)?[\w<>]+\s+(\w+)\(")]
    private static partial Regex ExplicitTest();

    // A test skipping itself, and the reason it gives: Assert.Skip's argument, or SkipWhen's and SkipUnless's after the condition.
    [GeneratedRegex(@"Assert\.Skip(?:(?:When|Unless)\([^;]*?,\s*|\(\s*)\$?""((?:[^""\\]|\\.)*)""", RegexOptions.Singleline)]
    private static partial Regex SkipCall();

    // A constant interpolated into a reason.
    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Constant();

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ExGrid.slnx"))) return dir.FullName;
        }
        throw new InvalidOperationException($"No ExGrid.slnx above {AppContext.BaseDirectory}.");
    }
}
