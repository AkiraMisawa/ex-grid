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

    /// <summary>The rows ADR-0208 names as checked by hand.</summary>
    // §21.11's screen-reader check left the rows on 2026-10-13: no screen reader is run to verify a
    // release (ADR-0033, ADR-0208's note of that date). VZ-14, VZ-16, FN-12e, SH-42, SH-51, SH-52 and
    // KB-3 on macOS left them on 2026-11-02, run by CI's own desktops at the commit being released, and
    // VZ-18's check on the remote-isolation PC was withdrawn the same day (ADR-0450). WR-10, the Wrappers'
    // MudBlazor floor run, indexed beside them since 2026-10-12, left them on 2026-11-02 too, run by CI's
    // long run (ADR-0450's note of that date), and so did ED-30, run by CI's windows-ime job with a real IME
    // (the same note). None is left; the index stays, read as before, for a row a later decision checks by
    // hand.
    private static readonly string[] IndexedRows = [];

    /// <summary>The rows CI's Windows and macOS desktop jobs, its real-IME job and its MudBlazor floor jobs run in place of a record (ADR-0450).</summary>
    private static readonly string[] RunOnCi = ["VZ-14", "VZ-16", "FN-12e", "SH-42", "SH-51", "SH-52", "KB-3", "WR-10", "ED-30"];

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

    [Fact] // ADR-0208, ADR-0450: every row the decision still leaves to a person is indexed
    public void Every_row_ADR_0208_names_is_indexed()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        Assert.Equal(IndexedRows.Order(StringComparer.Ordinal), index.Select(r => r.Row).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): every record the index names is tagged verified/ followed by its directory's name
    public void Every_record_is_tagged_for_its_directory()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        // Vacuous while the index holds no row (since 2026-11-02, ADR-0450); ByHandTests holds the rule on rows
        // of its own, and this holds it again on the first row a later decision indexes.
        Assert.All(index.Where(r => r.Record is not null), r => Assert.Equal(ByHand.TagFor(r.Record!.Path), r.Record!.Tag));
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

        Assert.Contains("--project tools/release/ExGrid.Release -c Release -- admit", release);
        Assert.Contains("--project tools/release/ExGrid.Release -c Release -- gate", release);
        Assert.Contains("long: ${{ needs.admit.outputs.long == 'true' }}", release);
        Assert.Contains("if: needs.admit.outputs.site == 'true'", release);
        // A candidate's release opens with the candidate's note, any other prerelease's with the prerelease note (ADR-0190, 2026-10-12).
        Assert.Contains("cat docs/releases/candidate-note.md", release);
        Assert.Contains("cat docs/releases/prerelease-note.md", release);
        // npm publishes a tarball named as a path: a bare "dir/file.tgz" is read as a GitHub repository, and the
        // publish of v0.2.0-beta.1 failed on it (2026-11-03).
        Assert.Contains("file=\"./npm-tarballs/$tarball\"", release);
        Assert.Contains("npm publish \"$file\"", release);
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
    }

    [Fact] // DoD §22 Step 2: the release tool's list is the one Step 2 writes: the ten explicit measurements by their classes and CsvClockTests'
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
        Assert.All(projects, p => Assert.True(Tree.IsDirectory($"dotnet/tests/{p}"), p));
        Assert.All(Skips.Allowed, a => Assert.Contains(a.Project, projects));
    }

    [Fact] // ADR-0450 (decided with the user 2026-11-02): the rows CI runs, on its desktops and at the MudBlazor floor, and VZ-18 withdrawn, are neither indexed nor Known limitations
    public void The_rows_CI_runs_are_out_of_the_index_and_none_is_a_known_limitation()
    {
        var report = new Report();
        var index = ByHand.ReadIndex(Tree, report)!;
        var table = ByHandTable.Read(Tree, report)!;

        Assert.True(report.Admitted, string.Join("\n", report.Refusals.Select(r => r.Message)));
        Assert.DoesNotContain(index, r => RunOnCi.Contains(r.Row) || r.Row == "VZ-18");
        Assert.DoesNotContain(table.Rows, r => RunOnCi.Contains(r.Row) || r.Row == "VZ-18");
        Assert.All(index, r => Assert.Null(r.Limitation));
        Assert.All(table.Rows, r => Assert.Null(r.KnownLimitation));
        // What runs them instead: the jobs, in the long run release.yml calls for a release candidate and a stable version.
        var ci = Tree.ReadText(".github/workflows/ci.yml")!;
        foreach (var job in new[] { "windows-desktop", "windows-ime", "macos-desktop", "mudblazor-floor-hosts", "mudblazor-floor", "mudblazor-range" })
            Assert.Contains($"\n  {job}:\n", ci);
        Assert.Contains("long: ${{ needs.admit.outputs.long == 'true' }}", Tree.ReadText(".github/workflows/release.yml")!);
    }

    // Every .cs file of every test project but this one, whose own tests write the patterns as text.
    private static IEnumerable<(string Project, string File, string Text)> TestSources()
    {
        var tests = Path.Combine(RepositoryRoot(), "dotnet", "tests");
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
            if (File.Exists(Path.Combine(dir.FullName, "dotnet", "ExGrid.slnx"))) return dir.FullName;
        }
        throw new InvalidOperationException($"No dotnet/ExGrid.slnx above {AppContext.BaseDirectory}.");
    }
}
