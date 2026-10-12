using Xunit;

namespace ExGrid.Release.Tests;

public class ByHandTests
{
    private static Report Check(Repo repo, string tag, string target, IPublishedVersions? mudBlazor = null)
    {
        var report = new Report();
        ByHand.Check(repo.Git, new CommitTree(repo.Git, tag), target, report, fetchMissing: false, mudBlazor);
        return report;
    }

    /// <summary>
    /// MudBlazor 8.15.0, 9.0.0 to 9.11.0 and the prereleases before 9.0.0, each with the date nuget.org's registration
    /// index gave it on 2026-10-12.
    /// </summary>
    private static readonly (string Version, string Date)[] Published =
    [
        ("8.15.0", "2025-11-23T03:43:31Z"), ("9.0.0-preview.1", "2025-12-20T18:02:36Z"), ("9.0.0-preview.2", "2026-01-14T18:08:31Z"),
        ("9.0.0-rc.1", "2026-02-11T00:24:17Z"), ("9.0.0", "2026-02-19T23:24:23Z"), ("9.1.0", "2026-03-03T21:45:43Z"),
        ("9.2.0", "2026-03-18T01:04:43Z"), ("9.3.0", "2026-04-08T01:26:12Z"), ("9.4.0", "2026-04-22T22:51:21Z"),
        ("9.5.0", "2026-05-26T19:35:44Z"), ("9.6.0", "2026-06-27T02:33:22Z"), ("9.7.0", "2026-07-09T18:04:10Z"),
        ("9.8.0", "2026-08-05T17:06:24Z"), ("9.9.0", "2026-08-24T14:07:55Z"), ("9.10.0", "2026-09-13T23:02:43Z"),
        ("9.11.0", "2026-09-27T16:05:14Z"),
    ];

    /// <summary>The versions a floor record of 2026-10-12 would name: every published one of [9.6.0,10.0.0).</summary>
    private static readonly string[] Covered = ["9.6.0", "9.7.0", "9.8.0", "9.9.0", "9.10.0", "9.11.0"];

    private const string FloorTag = "verified/2026-11-02-linux-mudblazor-floor";

    /// <summary>When a fixture's floor run verified its commit.</summary>
    private static readonly DateTimeOffset FloorTime = DateTimeOffset.Parse("2026-10-12T12:00:00Z");

    /// <summary>X's commit time in a fixture whose floor record is indexed at X.</summary>
    private static readonly DateTimeOffset XTime = DateTimeOffset.Parse("2026-11-02T12:00:00Z");

    /// <summary>An index of WR-10 alone, its record written as given.</summary>
    private static string Wr10Index(string record) =>
        "{\"rows\": [{\"row\": \"WR-10\", \"where\": \"Windows or Linux\", \"paths\": [\"src/\"], \"record\": " + record + "}]}";

    private const string FloorRecord = "verification/2026-11-02-linux-mudblazor-floor/results.md";

    /// <summary>
    /// A repository whose three Wrappers declare <paramref name="ranges"/> (one for all, or one each), and
    /// whose WR-10 row is recorded at the commit their code was last changed, made at <see cref="FloorTime"/>,
    /// naming <paramref name="versions"/>. Returns the commit that indexes it, X, made at <see cref="XTime"/>.
    /// </summary>
    private static string FloorRecorded(Repo repo, string[] versions, params string[] ranges) =>
        FloorRecorded(repo, versions, null, ranges);

    /// <summary>As <see cref="FloorRecorded(Repo, string[], string[])"/>, the index naming the later records <paramref name="later"/> makes.</summary>
    private static string FloorRecorded(Repo repo, string[] versions, Func<Repo, string, Later[]>? later, params string[] ranges)
    {
        var wrappers = new[] { "ExGrid.MudBlazor", "ExSheet.MudBlazor", "ExPivot.MudBlazor" };
        for (var i = 0; i < wrappers.Length; i++)
            repo.Write($"src/{wrappers[i]}/{wrappers[i]}.csproj", Fixture.WrapperProject(ranges[Math.Min(i, ranges.Length - 1)]));
        repo.Date = FloorTime;
        var recorded = repo.Write("samples/ExGrid.DemoPages/Pages/SheetPage.razor", "page").Commit("The Wrappers");
        repo.Tag(FloorTag, recorded);
        var made = later?.Invoke(repo, recorded);
        var row = new Row("WR-10", "Windows or Linux", [":(glob)src/*.MudBlazor/**", "samples/ExGrid.DemoPages/"],
            (recorded, FloorRecord, "pass"), FloorTag, versions, made);
        repo.Date = XTime;
        var x = repo.Write(FloorRecord, $"Verified commit: `{recorded}`")
            .Write("verification/by-hand.json", Fixture.Index(row))
            .Write("docs/definition-of-done.md", Fixture.Table(row))
            .Commit("The floor record");
        repo.Date = null;
        return x;
    }

    /// <summary>
    /// A later layer-2 run at <paramref name="versions"/>, made at <paramref name="at"/> in a directory of its own,
    /// <paramref name="directory"/>, its record naming <paramref name="names"/> (its commit unless given) and its
    /// commit tagged <paramref name="tag"/> (verified/ and the directory unless given; none when empty).
    /// </summary>
    private static Later LaterRun(Repo repo, string at, string directory, string[] versions, string verdict = "pass", string? tag = null, string? names = null)
    {
        var path = $"verification/{directory}/mudblazor-layer2.log";
        tag ??= $"verified/{directory}";
        if (tag.Length > 0) repo.Tag(tag, at);
        repo.Write(path, $"== MudBlazor {string.Join(", ", versions)}, at {names ?? at}");
        return new Later(at, tag.Length > 0 ? tag : $"verified/{directory}", path, verdict, versions);
    }

    [Fact] // ADR-0208: a row's record holds while the code it covers is unchanged since
    public void A_row_whose_code_is_unchanged_holds_on_its_record()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        var later = repo.Write("src/Other/b.cs", "class B { int changedElsewhere; }").Commit("A change the row does not cover");

        var report = Check(repo, later, later);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("Holding on their records, their code unchanged since: VZ-14 on Windows"));
        Assert.Contains(report.Notices, n => n.Contains("Every row checked by hand holds"));
    }

    [Fact] // ADR-0208: a change to a row's paths since its record refuses the tag, naming the files
    public void A_row_whose_code_changed_is_refused()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        var later = repo.Write("src/Code/c.cs", "class C {}").Write("src/Code/a.cs", "class A { int changed; }").Commit("A change the row covers");

        var report = Check(repo, later, later);

        var refusal = Assert.Single(report.Refusals);
        Assert.Contains("VZ-14 on Windows: the code it covers changed between its record", refusal.Message);
        Assert.Contains("src/Code/a.cs, src/Code/c.cs", refusal.Message);
        Assert.Contains(report.Notices, n => n.Contains("To run by hand at") && n.Contains("VZ-14 on Windows"));
    }

    [Fact] // ADR-0208: given a directory inside the working tree, the check still reads the row's paths from the root
    public void A_git_given_a_subdirectory_reads_the_paths_from_the_root()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        var later = repo.Write("src/Code/a.cs", "class A { int changed; }").Commit("A change the row covers");
        var inside = new Git(Path.Combine(repo.Root, "src", "Other"));

        var report = new Report();
        ByHand.Check(inside, new CommitTree(inside, later), later, report, fetchMissing: false);

        Assert.Equal(new Git(repo.Root).Repository, inside.Repository);
        var refusal = Assert.Single(report.Refusals);
        Assert.Contains("VZ-14 on Windows: the code it covers changed between its record", refusal.Message);
        Assert.Contains("src/Code/a.cs", refusal.Message);
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
    }

    [Fact] // ADR-0208: a file path is compared as a file, a directory path as everything under it
    public void Files_and_directories_are_compared_as_named()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a").Write("src/Code/sub/b.cs", "b").Write("src/CodeX/c.cs", "c").Write("src/one.cs", "1").Write("src/one.cs.bak", "x");
        var recorded = repo.Commit();
        var row = new Row("VZ-14", "Windows", ["src/Code/", "src/one.cs"], (recorded, "verification/2026-10-12-windows-17/report.md", "pass"));
        repo.Tag(Fixture.RunTag, recorded);
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var indexed = repo.Commit();
        Assert.Empty(Check(repo, indexed, indexed).Refusals);

        // A sibling whose name begins like the directory, and a file whose name begins like the file, are not the row's.
        var siblings = repo.Write("src/CodeX/c.cs", "c2").Write("src/one.cs.bak", "y").Commit();
        Assert.Empty(Check(repo, siblings, siblings).Refusals);

        var nested = repo.Write("src/Code/sub/b.cs", "b2").Commit();
        Assert.Contains("src/Code/sub/b.cs", Assert.Single(Check(repo, nested, nested).Refusals).Message);
    }

    [Fact] // ADR-0208: a row never recorded refuses the tag
    public void A_row_without_a_record_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a");
        var row = new Row("SH-51", "Windows", ["src/Code/"]);
        repo.Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        var refusal = Assert.Single(Check(repo, commit, commit).Refusals);
        Assert.Contains("SH-51 on Windows has never been recorded", refusal.Message);
    }

    [Theory] // ADR-0208, Step 7: a row holds only on a pass
    [InlineData("fail")]
    [InlineData("not run")]
    public void A_row_whose_latest_record_is_not_a_pass_is_refused(string verdict)
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/Code/a.cs", "a").Commit();
        var row = new Row("ED-30", "Windows", ["src/Code/"], (recorded, "verification/2026-10-12-windows-17/report.md", verdict));
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        Assert.Contains($"reads '{verdict}', and a row holds only on a pass", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }

    [Fact] // ADR-0208 (2026-10-12): a record whose commit cannot be fetched through its tag cannot be compared, and is refused as unchecked
    public void A_record_whose_tag_is_not_here_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a");
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (new string('c', 40), "verification/2026-10-12-windows-17/report.md", "pass"));
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: {new string('c', 40)}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        var refusal = Assert.Single(Check(repo, commit, commit).Refusals).Message;
        Assert.Contains($"the tag {Fixture.RunTag} that keeps that commit is not in this repository", refusal);
        Assert.Contains("whether its code changed since cannot be checked", refusal);
    }

    [Fact] // ADR-0208 (2026-10-12): a run's tag names the commit it verified; one that names another is refused, never read as the record's
    public void A_record_whose_tag_names_another_commit_is_refused()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/Code/a.cs", "a").Commit();
        var later = repo.Write("src/Code/a.cs", "a, later").Commit();
        repo.Tag(Fixture.RunTag, later);
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/2026-10-12-windows-17/report.md", "pass"));
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        var refusal = Assert.Single(Check(repo, commit, commit).Refusals).Message;
        Assert.Contains($"its tag {Fixture.RunTag} names {later[..12]}", refusal);
        Assert.DoesNotContain("the code it covers changed", refusal);
    }

    [Fact] // ADR-0208 (2026-10-12): a record's commit is fetched through its verified/* tag, though no branch of this clone holds it
    public void A_record_naming_a_verified_tag_is_fetched_through_it()
    {
        using var origin = new Repo();
        var main = origin.Write("src/Code/a.cs", "a").Commit("Code");
        // The run's branch was squash-merged and deleted: only its tag still reaches the commit it verified.
        origin.Branch("claude/exsheet-windows-verify-18", main);
        var verified = origin.Write("verification/notes.txt", "the run's own branch").Commit("The run's branch");
        origin.Tag("verified/2026-11-02-windows-18", verified);
        origin.Run("checkout", "--quiet", "main");
        origin.Run("branch", "--quiet", "-D", "claude/exsheet-windows-verify-18");
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (verified, "verification/2026-11-02-windows-18/report.md", "pass"), "verified/2026-11-02-windows-18");
        var tagged = origin.Write("verification/2026-11-02-windows-18/report.md", $"Verified commit: `{verified[..7]}`")
            .Write("verification/by-hand.json", Fixture.Index(row))
            .Write("docs/definition-of-done.md", Fixture.Table(row))
            .Commit("Records the row, squashed");

        using var clone = new Repo();
        clone.Run("remote", "add", "origin", origin.Root);
        clone.Run("fetch", "--quiet", "--no-tags", "origin", "+refs/heads/main:refs/remotes/origin/main");
        Assert.Null(clone.Git.Commit(verified));

        var unfetched = new Report();
        ByHand.Check(clone.Git, new CommitTree(clone.Git, tagged), tagged, unfetched, fetchMissing: false);
        Assert.Contains("is not in this repository (fetch the tags, or run with --fetch-missing)", Assert.Single(unfetched.Refusals).Message);

        var fetched = new Report();
        ByHand.Check(clone.Git, new CommitTree(clone.Git, tagged), tagged, fetched, fetchMissing: true);
        Assert.Empty(fetched.Refusals);
        Assert.Equal(verified, clone.Git.TagCommit("verified/2026-11-02-windows-18"));
        Assert.Contains(fetched.Notices, n => n.Contains("Holding on their records, their code unchanged since: VZ-14 on Windows"));
    }

    [Fact] // ADR-0208 (2026-10-12): a tag the remote does not have refuses the tag as unchecked, never lets the row hold
    public void A_record_whose_tag_the_remote_lacks_is_refused()
    {
        using var origin = new Repo();
        var verified = origin.Write("src/Code/a.cs", "a").Commit("Code");
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (verified, "verification/2026-11-02-windows-18/report.md", "pass"), "verified/2026-11-02-windows-18");
        var tagged = origin.Write("verification/2026-11-02-windows-18/report.md", $"Verified commit: {verified}")
            .Write("verification/by-hand.json", Fixture.Index(row))
            .Write("docs/definition-of-done.md", Fixture.Table(row))
            .Commit("Records the row; its run never pushed its tag");

        using var clone = new Repo();
        clone.Run("remote", "add", "origin", origin.Root);
        clone.Run("fetch", "--quiet", "--no-tags", "origin", "+refs/heads/main:refs/remotes/origin/main");

        var report = new Report();
        ByHand.Check(clone.Git, new CommitTree(clone.Git, tagged), tagged, report, fetchMissing: true);

        Assert.Contains("is not in this repository, and origin did not give it", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0205 (2026-10-12): WR-10's floor record holds while the Wrappers and the demo pages are unchanged, and a change to either refuses the tag
    public void WR_10_s_row_refuses_a_tag_whose_wrapper_paths_changed()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/ExGrid/Grid.cs", "core")
            .Write("src/ExGrid.MudBlazor/MudCellEditor.razor", "editor")
            .Write("src/ExSheet.MudBlazor/Toolbar/MudToolbar.razor", "toolbar")
            .Write("src/ExPivot.MudBlazor/MudPivotChrome.cs", "pivot")
            .Write("src/ExGrid.MudBlazor/ExGrid.MudBlazor.csproj", Fixture.WrapperProject("[9.6.0,10.0.0)"))
            .Write("src/ExSheet.MudBlazor/ExSheet.MudBlazor.csproj", Fixture.WrapperProject("[9.6.0,10.0.0)"))
            .Write("src/ExPivot.MudBlazor/ExPivot.MudBlazor.csproj", Fixture.WrapperProject("[9.6.0,10.0.0)"))
            .Write("samples/ExGrid.DemoPages/Pages/SheetPage.razor", "page")
            .Write("samples/ExGrid.DemoHost/Program.cs", "host")
            .Commit("Code");
        repo.Tag(FloorTag, recorded);
        var row = new Row("WR-10", "Windows or Linux", [":(glob)src/*.MudBlazor/**", "samples/ExGrid.DemoPages/"],
            (recorded, FloorRecord, "pass"), FloorTag, Covered);
        var indexed = repo.Write(FloorRecord, $"Verified commit: `{recorded}`")
            .Write("verification/by-hand.json", Fixture.Index(row))
            .Write("docs/definition-of-done.md", Fixture.Table(row))
            .Commit("The floor record");
        var published = Listed.Dated(Published);
        Assert.Empty(Check(repo, indexed, indexed, published).Refusals);

        var core = repo.Write("src/ExGrid/Grid.cs", "core, changed").Write("samples/ExGrid.DemoHost/Program.cs", "host, changed").Commit("Neither a Wrapper nor a demo page");
        Assert.Empty(Check(repo, core, core, published).Refusals);

        var nested = repo.Write("src/ExSheet.MudBlazor/Toolbar/MudToolbar.razor", "toolbar, changed").Commit("A Wrapper, two directories down");
        var wrapper = Assert.Single(Check(repo, nested, nested, published).Refusals).Message;
        Assert.Contains("WR-10 on Windows or Linux: the code it covers changed", wrapper);
        Assert.Contains("src/ExSheet.MudBlazor/Toolbar/MudToolbar.razor", wrapper);

        var page = repo.Write("src/ExSheet.MudBlazor/Toolbar/MudToolbar.razor", "toolbar")
            .Write("samples/ExGrid.DemoPages/Pages/SheetPage.razor", "page, changed").Commit("A demo page");
        Assert.Contains("samples/ExGrid.DemoPages/Pages/SheetPage.razor", Assert.Single(Check(repo, page, page, published).Refusals).Message);

        var added = repo.Write("samples/ExGrid.DemoPages/Pages/SheetPage.razor", "page")
            .Write("src/ExPivot.MudBlazor/New.cs", "a new Wrapper file").Commit("A file added to a Wrapper");
        Assert.Contains("src/ExPivot.MudBlazor/New.cs", Assert.Single(Check(repo, added, added, published).Refusals).Message);
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): a record's tag is verified/ followed by its directory's name, so a tag naming another directory is refused
    public void A_tag_that_names_another_directory_is_refused()
    {
        using var repo = new Repo();
        var verified = repo.Write("src/Code/a.cs", "a").Commit();
        // The eleventh run's Part B was recorded in its own directory; its run number alone is not its tag.
        repo.Tag("verified/2026-10-01-windows-11", verified);
        var row = new Row("SH-42", "Windows", ["src/Code/"], (verified, "verification/2026-10-01-windows-browser-11/keys.md", "pass"), "verified/2026-10-01-windows-11");
        var commit = repo.Write("verification/2026-10-01-windows-browser-11/keys.md", $"Verified commit: `{verified}`")
            .Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row)).Commit();

        var report = Check(repo, commit, commit);

        var refusal = Assert.Single(report.Refusals, r => r.Message.Contains("names the tag"));
        Assert.Contains("SH-42 on Windows's record in verification/by-hand.json is verification/2026-10-01-windows-browser-11/keys.md and names the tag 'verified/2026-10-01-windows-11'", refusal.Message);
        Assert.Contains("'verified/' followed by the name of the record's directory under verification/, and nothing else: verified/2026-10-01-windows-browser-11", refusal.Message);
        Assert.Contains("(ADR-0208, 2026-10-12)", refusal.Message);
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
    }

    [Theory] // ADR-0208 (decided with the user, 2026-10-12): a tag that is exactly verified/ and its record's directory holds, whatever the platform or the run's purpose
    [InlineData("verification/2026-10-01-windows-browser-11/keys.md", "verified/2026-10-01-windows-browser-11")]
    [InlineData("verification/2026-10-01-windows-15/report.md", "verified/2026-10-01-windows-15")]
    [InlineData("verification/2026-11-02-macos-kb3/part-a/keys.md", "verified/2026-11-02-macos-kb3")]
    public void A_tag_named_for_its_record_directory_holds(string record, string tag)
    {
        Assert.Equal(tag, ByHand.TagFor(record));
        using var repo = new Repo();
        var verified = repo.Write("src/Code/a.cs", "a").Commit();
        repo.Tag(tag, verified);
        var row = new Row("SH-42", "Windows", ["src/Code/"], (verified, record, "pass"), tag);
        var commit = repo.Write(record, $"Verified commit: `{verified}`")
            .Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row)).Commit();

        var report = Check(repo, commit, commit);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("Holding on their records, their code unchanged since: SH-42 on Windows"));
    }

    [Theory] // ADR-0208 (2026-10-12): a record is a file in its run's directory, whose name its tag carries
    [InlineData("verification/keys.md")]
    [InlineData("verification//keys.md")]
    [InlineData("docs/2026-10-01-windows-15/keys.md")]
    public void A_record_outside_a_run_directory_is_refused(string record)
    {
        Assert.Null(ByHand.TagFor(record));
        using var repo = new Repo();
        var held = "{\"commit\": \"" + new string('a', 40) + "\", \"tag\": \"verified/2026-10-01-windows-15\", \"path\": \"" + record + "\", \"verdict\": \"pass\"}";
        var commit = repo.Write(ByHand.IndexPath, Wr10Index(held).Replace("WR-10", "SH-42").Replace("Windows or Linux", "Windows")).Commit();

        var report = new Report();
        ByHand.ReadIndex(new CommitTree(repo.Git, commit), report);

        Assert.Contains("a record is a file in its run's directory under verification/", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): WR-10 holds while its record names every stable MudBlazor published within the Wrappers' range before X
    public void WR_10_holds_on_a_record_that_names_every_published_version_of_the_range()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");

        // Below the floor, above the range, and the prereleases are not asked for.
        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0-preview.1", "2026-10-15T00:00:00Z"), ("10.0.0", "2026-10-20T00:00:00Z"), ("10.1.0", "2026-10-25T00:00:00Z")]));

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("Holding on their records, their code unchanged since: WR-10 on Windows or Linux"));
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): a 9.x published after the floor record and before X asks for the layer-2 runs at it; the check refuses the tag naming it
    public void WR_10_is_refused_naming_each_version_published_after_its_record_and_before_X()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated([.. Published,
            ("9.12.0", "2026-10-20T00:00:00Z"), ("9.13.0-rc.1", "2026-10-25T00:00:00Z"), ("9.13.0", "2026-11-10T00:00:00Z"), ("10.0.0", "2026-10-30T00:00:00Z")]));

        var refusal = Assert.Single(report.Refusals).Message;
        Assert.Contains("WR-10 on Windows or Linux: MudBlazor 9.12.0, published within the range the Wrappers declare ([9.6.0,10.0.0)), is not among the versions its records' layer-2 runs covered (9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0, 9.11.0)", refusal);
        Assert.Contains("9.12.0 (2026-10-20), published after the floor record and before X, which asks for each Wrapper's layer-2 suite at that version only, a run of its own whose record goes in a directory of its own", refusal);
        Assert.Contains("The floor run holds while its paths are unchanged", refusal);
        Assert.DoesNotContain("9.13.0", refusal);
        Assert.DoesNotContain("10.0.0,", refusal);
        Assert.Contains(report.Notices, n => n.Contains("MudBlazor 9.13.0, published within the range the Wrappers declare after X (2026-11-02 12:00:00Z, its commit time), is not asked for"));
        Assert.Contains(report.Notices, n => n.Contains("To run by hand at") && n.Contains("WR-10 on Windows or Linux's layer-2 runs at MudBlazor 9.12.0"));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): a version published after X's commit time is not asked for, the time read from X's commit, not the clock
    public void WR_10_does_not_ask_for_a_version_published_after_X()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");

        // A second after X's commit time, though long before the clock reads this test.
        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-11-02T12:00:01Z")]));

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("MudBlazor 9.12.0, published within the range the Wrappers declare after X"));

        // A second before it is asked for.
        var before = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-11-02T11:59:59Z")]));
        Assert.Contains("MudBlazor 9.12.0, published within the range", Assert.Single(before.Refusals).Message);
    }

    [Fact] // ADR-0205 (2026-10-12): a version published before the floor record that it does not name is asked of the floor run's layer-2 runs, which cover every published version of the range
    public void WR_10_is_refused_on_a_version_published_before_its_floor_record_that_it_does_not_name()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, ["9.6.0", "9.7.0", "9.9.0", "9.10.0", "9.11.0"], "[9.6.0,10.0.0)");

        var refusal = Assert.Single(Check(repo, commit, commit, Listed.Dated(Published)).Refusals).Message;

        Assert.Contains("MudBlazor 9.8.0, published within the range", refusal);
        Assert.Contains("9.8.0 (2026-08-05), published before the floor record's commit, which the floor run's layer-2 runs cover", refusal);
    }

    [Fact] // ADR-0205 (2026-10-12): a listed version nuget.org gives no date for is asked for, since whether it came before X cannot be said
    public void WR_10_asks_for_a_listed_version_without_a_date()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");
        var listed = new Listed([.. Listed.Dated(Published).Versions, new PublishedVersion("9.11.1", null)]);

        var refusal = Assert.Single(Check(repo, commit, commit, listed).Refusals).Message;

        Assert.Contains("9.11.1 (no date given), listed, though nuget.org gives no date for it", refusal);
    }

    [Fact] // ADR-0205, ADR-0208 (decided with the user, 2026-10-12): an unlisted version is not asked for, and the check reports it as out of the check because it is unlisted
    public void WR_10_does_not_ask_for_an_unlisted_version()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");
        var listed = new Listed([.. Listed.Dated(Published).Versions,
            new PublishedVersion("9.11.2", null, Listed: false), new PublishedVersion("9.11.3", DateTimeOffset.Parse("2026-11-20T00:00:00Z"), Listed: false),
            new PublishedVersion("9.5.1", null, Listed: false)]);

        var report = Check(repo, commit, commit, listed);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("WR-10 on Windows or Linux: MudBlazor 9.11.2, 9.11.3, within the range the Wrappers declare, are out of the check because they are unlisted on nuget.org"));
        Assert.DoesNotContain(report.Notices, n => n.Contains("9.5.1"));
        // 9.11.3 has a date after X, and is still named only as unlisted, not as published after X.
        Assert.DoesNotContain(report.Notices, n => n.Contains("not asked for") && n.Contains("9.11.3"));
        Assert.Contains(report.Notices, n => n.Contains("Holding on their records, their code unchanged since: WR-10 on Windows or Linux"));
    }

    [Fact] // ADR-0205, ADR-0208 (decided with the user, 2026-10-12): a layer-2 run at a later 9.x is a run of its own, and WR-10 holds on the versions its records cover between them
    public void WR_10_holds_on_a_later_record_beside_the_floor_record()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            r.Date = DateTimeOffset.Parse("2026-10-25T12:00:00Z");
            var at = r.Write("README.md", "a later commit").Commit("Where the later run was made");
            return [LaterRun(r, at, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"])];
        }, "[9.6.0,10.0.0)");
        var listed = Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z")]);

        var report = Check(repo, commit, commit, listed);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("Holding on their records, their code unchanged since: WR-10 on Windows or Linux"));
        var row = Assert.Single(ByHand.ReadIndex(new CommitTree(repo.Git, commit), new Report())!);
        Assert.Equal("verified/2026-10-25-linux-mudblazor-9.12.0", Assert.Single(row.Later!).Tag);
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): a later record's tag is verified/ followed by its own directory's name
    public void A_later_record_tagged_for_another_directory_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            var at = r.Write("README.md", "later").Commit();
            return [LaterRun(r, at, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"], tag: "verified/2026-10-25-linux-mudblazor")];
        }, "[9.6.0,10.0.0)");

        var report = new Report();
        var row = Assert.Single(ByHand.ReadIndex(new CommitTree(repo.Git, commit), report)!);

        Assert.Empty(row.Later!);
        Assert.Contains("WR-10 on Windows or Linux's later record 1 in verification/by-hand.json is verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log and names the tag 'verified/2026-10-25-linux-mudblazor'", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): a later layer-2 run's record goes in a directory of its own, never the floor record's
    public void A_later_record_in_the_floor_record_s_directory_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            r.Write("verification/2026-11-02-linux-mudblazor-floor/mudblazor-layer2-9.12.0.log", $"at {floor}");
            return [new Later(floor, FloorTag, "verification/2026-11-02-linux-mudblazor-floor/mudblazor-layer2-9.12.0.log", "pass", ["9.12.0"])];
        }, "[9.6.0,10.0.0)");

        var report = new Report();
        ByHand.ReadIndex(new CommitTree(repo.Git, commit), report);

        Assert.Contains("names verification/2026-11-02-linux-mudblazor-floor/results.md, verification/2026-11-02-linux-mudblazor-floor/mudblazor-layer2-9.12.0.log in one directory, tagged verified/2026-11-02-linux-mudblazor-floor", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): a later record's commit is fetched through its tag; a tag that is not there refuses the tag as unchecked
    public void A_later_record_whose_tag_is_missing_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            var at = r.Write("README.md", "later").Commit();
            return [LaterRun(r, at, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"], tag: "")];
        }, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z")]));

        Assert.Contains(report.Refusals, r => r.Message.Contains("WR-10 on Windows or Linux's later record was made at") && r.Message.Contains("the tag verified/2026-10-25-linux-mudblazor-9.12.0 that keeps that commit is not in this repository"));
        // Its versions are not counted: 9.12.0 is asked for besides.
        Assert.Contains(report.Refusals, r => r.Message.Contains("MudBlazor 9.12.0, published within the range"));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
    }

    [Fact] // ADR-0208 (decided with the user, 2026-10-12): each later record's tag names its commit
    public void A_later_record_whose_tag_names_another_commit_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            var at = r.Write("README.md", "later").Commit();
            var other = r.Write("README.md", "another").Commit();
            r.Tag("verified/2026-10-25-linux-mudblazor-9.12.0", other);
            return [LaterRun(r, at, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"], tag: "")];
        }, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z")]));

        Assert.Contains(report.Refusals, r => r.Message.Contains("WR-10 on Windows or Linux's later record was made at") && r.Message.Contains("and its tag verified/2026-10-25-linux-mudblazor-9.12.0 names"));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));

        // Though no version is missing, a later record that does not hold keeps the row from holding.
        var unasked = Check(repo, commit, commit, Listed.Dated(Published));
        Assert.Contains("and its tag verified/2026-10-25-linux-mudblazor-9.12.0 names", Assert.Single(unasked.Refusals).Message);
        Assert.DoesNotContain(unasked.Notices, n => n.Contains("Holding on their records"));
    }

    [Fact] // ADR-0208 (2026-10-12): a later record holds only on a pass, and names its commit; one that does not cover nothing
    public void A_later_record_that_is_not_a_pass_or_does_not_name_its_commit_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            var failed = r.Write("README.md", "later").Commit();
            var unnamed = r.Write("README.md", "later still").Commit();
            return
            [
                LaterRun(r, failed, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"], verdict: "fail"),
                LaterRun(r, unnamed, "2026-10-26-linux-mudblazor-9.12.1", ["9.12.1"], names: "somewhere else"),
            ];
        }, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z"), ("9.12.1", "2026-10-21T00:00:00Z")]));

        Assert.Contains(report.Refusals, r => r.Message.Contains("WR-10 on Windows or Linux's later record, verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log, reads 'fail'"));
        Assert.Contains(report.Refusals, r => r.Message.Contains("WR-10 on Windows or Linux's later record, verification/2026-10-26-linux-mudblazor-9.12.1/mudblazor-layer2.log, does not name the commit"));
        Assert.Contains(report.Refusals, r => r.Message.Contains("MudBlazor 9.12.0, published within the range"));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
        // What is still to run names the version the failed record was to cover, and the record that failed.
        var toRun = Assert.Single(report.Notices, n => n.Contains("To run by hand at"));
        Assert.EndsWith(": WR-10 on Windows or Linux's later record verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log, which reads 'fail', WR-10 on Windows or Linux's layer-2 runs at MudBlazor 9.12.0 (ADR-0208).", toRun);
    }

    [Fact] // ADR-0208 (2026-10-12): a later record's file is at the tag
    public void A_later_record_whose_file_is_not_at_the_tag_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            var at = r.Write("README.md", "later").Commit();
            r.Tag("verified/2026-10-25-linux-mudblazor-9.12.0", at);
            return [new Later(at, "verified/2026-10-25-linux-mudblazor-9.12.0", "verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log", "pass", ["9.12.0"])];
        }, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated(Published));

        Assert.Contains("WR-10 on Windows or Linux's later record, verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log, is not at the tag", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0205 (2026-10-12): later records stand beside a floor record, never in its place
    public void Later_records_without_a_floor_record_are_refused()
    {
        using var repo = new Repo();
        var later = "{\"commit\": \"" + new string('a', 40) + "\", \"tag\": \"verified/2026-10-25-linux-mudblazor-9.12.0\", \"path\": \"verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log\", \"verdict\": \"pass\", \"versions\": [\"9.12.0\"]}";
        var commit = repo.Write(ByHand.IndexPath, Wr10Index("null, \"laterRecords\": [" + later + "]")).Commit();

        var report = new Report();
        var row = Assert.Single(ByHand.ReadIndex(new CommitTree(repo.Git, commit), report)!);

        Assert.Null(row.Record);
        Assert.Contains("names records in 'laterRecords' and no floor record", Assert.Single(report.Refusals).Message);

        // A floor record that is there but malformed is refused for what is wrong with it, not as absent.
        var floor = "{\"commit\": \"" + new string('b', 40) + "\", \"tag\": \"" + FloorTag + "\", \"path\": \"" + FloorRecord + "\", \"verdict\": \"pass\"}";
        var malformed = repo.Write(ByHand.IndexPath, Wr10Index(floor + ", \"laterRecords\": [" + later + "]")).Commit();
        report = new Report();
        ByHand.ReadIndex(new CommitTree(repo.Git, malformed), report);
        Assert.Contains("record in verification/by-hand.json names no 'versions'", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): X's time is its committer time, in UTC, whatever offset the committer wrote and whenever it was authored
    public void X_s_time_is_its_committer_time_in_UTC()
    {
        using var repo = new Repo();
        var floor = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");
        // Committed at 21:00 in Tokyo, 12:00 UTC, on 2026-11-03; authored a month before.
        repo.Date = DateTimeOffset.Parse("2026-11-03T21:00:00+09:00");
        repo.AuthorDate = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var x = repo.Write("README.md", "X").Commit("X");
        repo.Date = null;
        repo.AuthorDate = null;
        Assert.Equal(DateTimeOffset.Parse("2026-11-03T12:00:00Z"), repo.Git.CommitTime(x));
        Assert.Equal(TimeSpan.Zero, repo.Git.CommitTime(x).Offset);

        // Published after the author's date and before the committer's: asked for.
        var asked = Check(repo, x, x, Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z")]));
        Assert.Contains("MudBlazor 9.12.0, published within the range", Assert.Single(asked.Refusals).Message);

        // Published at X's time exactly, and after it in UTC though before it on Tokyo's clock: not asked for.
        var after = Check(repo, x, x, Listed.Dated([.. Published, ("9.12.0", "2026-11-03T12:00:00Z"), ("9.13.0", "2026-11-03T15:00:00Z")]));
        Assert.Empty(after.Refusals);
        Assert.Contains(after.Notices, n => n.Contains("MudBlazor 9.12.0, 9.13.0, published within the range the Wrappers declare after X (2026-11-03 12:00:00Z, its commit time)"));
        _ = floor;
    }

    [Fact] // ADR-0205 (decided with the user, 2026-10-12): the floor record is held by its paths since its commit, whatever later records the entry names
    public void A_later_record_does_not_hold_the_floor_record_s_paths()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            var at = r.Write("src/ExGrid.MudBlazor/MudCellEditor.razor", "a Wrapper changed after the floor run").Commit();
            return [LaterRun(r, at, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"])];
        }, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z")]));

        var refusal = Assert.Single(report.Refusals).Message;
        Assert.Contains("WR-10 on Windows or Linux: the code it covers changed between its record (verification/2026-11-02-linux-mudblazor-floor/results.md", refusal);
        Assert.Contains("src/ExGrid.MudBlazor/MudCellEditor.razor", refusal);
    }

    [Fact] // ADR-0208 (2026-10-12): a later record holds only while the row's paths are unchanged between its commit and X, as every record checked by hand
    public void A_later_record_whose_paths_changed_since_its_commit_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, (r, floor) =>
        {
            // Made on a branch whose Wrapper differs from the floor's, and never merged.
            r.Run("checkout", "--quiet", "-b", "side");
            var at = r.Write("src/ExGrid.MudBlazor/MudCellEditor.razor", "a Wrapper the floor run never saw").Commit("Elsewhere");
            var later = LaterRun(r, at, "2026-10-25-linux-mudblazor-9.12.0", ["9.12.0"]);
            r.Run("checkout", "--quiet", "main");
            r.Write(later.Path, $"== MudBlazor 9.12.0, at {at}");
            return [later];
        }, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, Listed.Dated([.. Published, ("9.12.0", "2026-10-20T00:00:00Z")]));

        Assert.Contains(report.Refusals, r => r.Message.Contains("WR-10 on Windows or Linux: the code it covers changed between its later record (verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log")
            && r.Message.Contains("src/ExGrid.MudBlazor/MudCellEditor.razor"));
        // Its version is not counted, so 9.12.0 is asked for besides; the floor record itself holds.
        Assert.Contains(report.Refusals, r => r.Message.Contains("MudBlazor 9.12.0, published within the range"));
        Assert.DoesNotContain(report.Refusals, r => r.Message.Contains("between its record ("));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
        Assert.Contains(report.Notices, n => n.Contains("To run by hand at") && n.Contains("WR-10 on Windows or Linux's later record verification/2026-10-25-linux-mudblazor-9.12.0/mudblazor-layer2.log, whose paths changed since its commit"));
    }

    [Fact] // ADR-0208 (2026-10-12): only WR-10's entry names later records
    public void Later_records_on_another_row_are_refused()
    {
        using var repo = new Repo();
        var held = "{\"commit\": \"" + new string('a', 40) + "\", \"tag\": \"verified/2026-10-01-windows-15\", \"path\": \"verification/2026-10-01-windows-15/report.md\", \"verdict\": \"pass\"}";
        var commit = repo.Write(ByHand.IndexPath, Wr10Index(held).Replace("WR-10", "SH-42").Replace("Windows or Linux", "Windows").Replace("\"record\":", "\"laterRecords\": [], \"record\":")).Commit();

        var report = new Report();
        ByHand.ReadIndex(new CommitTree(repo.Git, commit), report);

        Assert.Contains("SH-42 on Windows in verification/by-hand.json names 'laterRecords', and only WR-10's entry does", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): a list of published versions that cannot be read refuses the tag as unchecked, never lets WR-10 hold
    public void WR_10_is_refused_when_the_published_versions_cannot_be_read()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");

        var unread = Check(repo, commit, commit, new Unreadable());
        var refusal = Assert.Single(unread.Refusals).Message;
        Assert.Contains("MudBlazor's published versions could not be read from the index (the network is down)", refusal);
        Assert.Contains("refused as unchecked", refusal);
        Assert.DoesNotContain(unread.Notices, n => n.Contains("Holding on their records") || n.Contains("Every row checked by hand holds"));

        var none = Check(repo, commit, commit);
        Assert.Contains("no list of MudBlazor's published versions was given", Assert.Single(none.Refusals).Message);
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): WR-10's records name the versions their layer-2 runs covered, each a stable version
    public void WR_10_s_record_names_stable_versions()
    {
        using var repo = new Repo();
        var record = "{\"commit\": \"" + new string('a', 40) + "\", \"tag\": \"" + FloorTag + "\", \"path\": \"" + FloorRecord + "\", \"verdict\": \"pass\"";
        var missing = repo.Write(ByHand.IndexPath, Wr10Index(record + "}")).Commit();
        var report = new Report();
        Assert.Null(Assert.Single(ByHand.ReadIndex(new CommitTree(repo.Git, missing), report)!).Record);
        Assert.Contains("WR-10 on Windows or Linux's record in verification/by-hand.json names no 'versions'", Assert.Single(report.Refusals).Message);

        var prerelease = repo.Write(ByHand.IndexPath, Wr10Index(record + ", \"versions\": [\"9.6.0\", \"9.12.0-preview.1\", 9.7]}")).Commit();
        report = new Report();
        ByHand.ReadIndex(new CommitTree(repo.Git, prerelease), report);
        Assert.Equal(2, report.Refusals.Count);
        Assert.Contains("names '9.12.0-preview.1' among its 'versions'", report.Refusals[0].Message);
        Assert.Contains("names '9.7' among its 'versions'", report.Refusals[1].Message);

        var named = repo.Write(ByHand.IndexPath, Wr10Index(record + ", \"versions\": [\"9.6.0\", \"9.7.0\"]}")).Commit();
        report = new Report();
        Assert.Equal(["9.6.0", "9.7.0"], Assert.Single(ByHand.ReadIndex(new CommitTree(repo.Git, named), report)!).Record!.Versions);
        Assert.Empty(report.Refusals);
    }

    [Fact] // ADR-0205 (2026-10-12): the range is read from every Wrapper's project, and a version inside any of them is asked for
    public void WR_10_reads_the_range_every_Wrapper_declares()
    {
        using var repo = new Repo();
        // One Wrapper still declaring the floor alone, 9.0.0 and above, asks for 9.0.0 to 9.5.0 and MudBlazor 10 too.
        var commit = FloorRecorded(repo, Covered, "9.0.0", "[9.6.0,10.0.0)");

        var refusal = Assert.Single(Check(repo, commit, commit, Listed.Dated([.. Published, ("10.0.0", "2026-10-01T00:00:00Z")])).Refusals).Message;

        Assert.Contains("MudBlazor 9.0.0, 9.1.0, 9.2.0, 9.3.0, 9.4.0, 9.5.0, 10.0.0, published within the range the Wrappers declare (9.0.0, [9.6.0,10.0.0))", refusal);
    }

    [Fact] // ADR-0205 (2026-10-12): a Wrapper whose MudBlazor range cannot be read refuses the tag, never lets WR-10 hold on a guess
    public void A_Wrapper_whose_range_cannot_be_read_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)", "[9.6.0,10.0.0)", "9.*");

        var refusal = Assert.Single(Check(repo, commit, commit, Listed.Dated(Published)).Refusals);

        Assert.Contains("src/ExPivot.MudBlazor/ExPivot.MudBlazor.csproj declares MudBlazor '9.*', which this check cannot read as a range", refusal.Message);
        Assert.Equal("src/ExPivot.MudBlazor/ExPivot.MudBlazor.csproj", refusal.File);
    }

    [Theory] // ADR-0205 (2026-10-12): a Wrapper's MudBlazor version is read as NuGet reads a range
    [InlineData("9.0.0", "8.15.0", false)]
    [InlineData("9.0.0", "9.0.0", true)]
    [InlineData("9.0.0", "10.0.0", true)]
    [InlineData("[9.6.0,10.0.0)", "9.5.0", false)]
    [InlineData("[9.6.0,10.0.0)", "9.6.0", true)]
    [InlineData("[9.6.0,10.0.0)", "9.6", true)]
    [InlineData("[9.6.0, 10.0.0)", "9.11.0", true)]
    [InlineData("[9.6.0,10.0.0)", "10.0.0", false)]
    [InlineData("[9.6.0,10.0.0)", "10.0.0-preview.1", true)]
    [InlineData("(9.6.0,10.0.0]", "9.6.0", false)]
    [InlineData("(9.6.0,10.0.0]", "10.0.0", true)]
    [InlineData("(,10.0.0)", "1.0.0", true)]
    [InlineData("[9.6.0,)", "42.0.0", true)]
    [InlineData("[9.6.0]", "9.6.0", true)]
    [InlineData("[9.6.0]", "9.6.1", false)]
    public void A_range_contains_what_NuGet_would_take(string range, string version, bool inside) =>
        Assert.Equal(inside, VersionRange.Parse(range)!.Contains(PackageVersion.Parse(version)!));

    [Theory] // ADR-0205 (2026-10-12): what is not a range is not read as one
    [InlineData("")]
    [InlineData("9.*")]
    [InlineData("[9.6.0")]
    [InlineData("(9.6.0)")]
    [InlineData("[9.6.0)")]
    [InlineData("[,]")]
    [InlineData("[9.6.0,10.0.0,11.0.0)")]
    [InlineData("nine")]
    public void What_is_not_a_range_is_not_read(string range) => Assert.Null(VersionRange.Parse(range));

    private const string RegistrationIndex = "https://registration.test/mudblazor/index.json";

    private const string AwayPage = "https://registration.test/mudblazor/page/9.6.0/9.11.0.json";

    private const string Unpublished = "{\"catalogEntry\": {\"version\": \"9.6.2\", \"listed\": false}}";

    private static string Leaf(string version, string published, bool listed = true) =>
        "{\"catalogEntry\": {\"version\": \"" + version + "\", \"published\": \"" + published + "\", \"listed\": " + (listed ? "true" : "false") + "}}";

    [Fact] // ADR-0205 (2026-10-12): nuget.org's registration index is read as it is written, its inlined pages and those read from their address, each version with its published date
    public void A_registration_index_is_read_with_each_version_s_date()
    {
        var addresses = new List<string>();
        var data = new Dictionary<string, string>
        {
            [RegistrationIndex] = $$"""{"count": 2, "items": [{"@id": "inlined", "items": [{{Leaf("9.5.0", "2026-05-26T19:35:44+00:00")}}]}, {"@id": "{{AwayPage}}"}]}""",
            [AwayPage] = $$"""{"items": [{{Leaf("9.6.0", "2026-06-27T02:33:22.123+00:00")}}, {{Leaf("9.6.1", "1900-01-01T00:00:00+00:00", listed: false)}}, {{Leaf("9.6.3", "1900-01-01T00:00:00+00:00")}}, {{Unpublished}}]}""",
        };
        var registration = new NuGetRegistration(RegistrationIndex, address =>
        {
            addresses.Add(address);
            return data[address];
        });

        Assert.True(registration.TryRead(out var versions, out var failure), failure);
        Assert.Equal([RegistrationIndex, AwayPage], addresses);
        Assert.Equal(
            [new PublishedVersion("9.5.0", DateTimeOffset.Parse("2026-05-26T19:35:44Z")), new PublishedVersion("9.6.0", DateTimeOffset.Parse("2026-06-27T02:33:22.123Z")), new PublishedVersion("9.6.1", null, Listed: false), new PublishedVersion("9.6.3", null), new PublishedVersion("9.6.2", null, Listed: false)],
            versions);

        // Read once.
        Assert.True(registration.TryRead(out _, out _));
        Assert.Equal(2, addresses.Count);
    }

    [Theory] // ADR-0205 (2026-10-12): what is not a registration index, or a listed version without a date, is refused, never read as empty or as undated
    [InlineData("""{"versions": ["9.6.0"]}""", "not a registration index")]
    [InlineData("""{"items": [{"count": 1}]}""", "neither its versions nor its address")]
    [InlineData("""{"items": [{"items": [{"catalogEntry": {"version": "9.6.0", "listed": true}}]}]}""", "9.6.0 in the index is listed and gives no date it was published")]
    [InlineData("""{"items": [{"items": [{"catalogEntry": {"version": "9.6.0", "published": "soon"}}]}]}""", "9.6.0 in the index is listed and gives no date it was published")]
    [InlineData("""{"items": [{"items": [{"packageContent": "x"}]}]}""", "has no catalog entry naming it")]
    [InlineData("not json", "not JSON")]
    public void What_is_not_a_registration_index_is_refused(string json, string why)
    {
        Assert.False(new NuGetRegistration(RegistrationIndex, _ => json).TryRead(out _, out var failure));
        Assert.Contains(why, failure);
    }

    [Fact] // ADR-0205 (2026-10-12): a page that cannot be read, or an address that cannot be reached, refuses the list
    public void A_registration_page_that_cannot_be_read_is_refused()
    {
        var index = $$"""{"items": [{"@id": "{{AwayPage}}"}]}""";
        Assert.False(new NuGetRegistration(RegistrationIndex, a => a == RegistrationIndex ? index : """{"count": 1}""").TryRead(out _, out var failure));
        Assert.Contains($"{AwayPage} is not a registration page", failure);

        Assert.False(new NuGetRegistration(RegistrationIndex, a => a == RegistrationIndex ? index : throw new HttpRequestException("the network is down")).TryRead(out _, out failure));
        Assert.Equal("the network is down", failure);
    }

    [Fact] // ADR-0208: a glob that matches no file names nothing, and is refused as a path that names nothing is
    public void A_glob_that_matches_nothing_is_refused()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/ExGrid/Grid.cs", "core").Commit();
        repo.Tag(Fixture.RunTag, recorded);
        var row = new Row("WR-10", "Windows or Linux", [":(glob)src/*.MudBlazor/**", "src/ExGrid/"], (recorded, "verification/2026-10-12-windows-17/results.md", "pass"), Versions: Covered);
        var commit = repo.Write("verification/2026-10-12-windows-17/results.md", $"Verified commit: {recorded}")
            .Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row)).Commit();

        var refusals = Check(repo, commit, commit, Listed.Dated(Published)).Refusals;
        Assert.Equal(2, refusals.Count);
        Assert.Contains("names :(glob)src/*.MudBlazor/**, which", refusals[0].Message);
        // With no Wrapper, the range its layer-2 runs cover cannot be read either.
        Assert.Contains("has no Wrapper project", refusals[1].Message);
    }

    [Fact] // ADR-0208: the record the index names is at the tag
    public void A_record_file_that_is_not_at_the_tag_is_refused()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/Code/a.cs", "a").Commit();
        repo.Tag(Fixture.RunTag, recorded);
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/2026-10-12-windows-17/report.md", "pass"));
        repo.Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        Assert.Contains("verification/2026-10-12-windows-17/report.md, is not at the tag", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }

    [Fact] // ADR-0208: a record names the commit it verified, so a slip in the index cannot move a row's baseline
    public void A_record_that_does_not_name_its_commit_is_refused()
    {
        using var repo = new Repo();
        var verified = repo.Write("src/Code/a.cs", "a").Commit();
        var recorded = repo.Write("src/Code/a.cs", "a, later").Commit();
        repo.Tag(Fixture.RunTag, recorded);
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/2026-10-12-windows-17/report.md", "pass"));
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: `{verified[..7]}`").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        var refusal = Assert.Single(Check(repo, commit, commit).Refusals);
        Assert.Contains($"does not name the commit {recorded[..12]}", refusal.Message);
        Assert.Equal("verification/2026-10-12-windows-17/report.md", refusal.File);
    }

    [Fact] // ADR-0208: a path that names nothing is a list that was not reviewed when its code moved
    public void A_path_that_names_nothing_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a").Write("src/Moved/b.cs", "b");
        var recorded = repo.Commit();
        repo.Tag(Fixture.RunTag, recorded);
        var row = new Row("VZ-14", "Windows", ["src/Code/", "src/Gone/", "src/Code/a.cs/", "src/Moved"], (recorded, "verification/2026-10-12-windows-17/report.md", "pass"));
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        var refusal = Assert.Single(Check(repo, commit, commit).Refusals);
        Assert.Contains("names src/Gone/, src/Code/a.cs/, src/Moved, which", refusal.Message);
    }

    [Fact] // ADR-0208: the target is what is judged — the signed-off commit, not the tag
    public void The_rows_are_judged_at_the_target()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var y = repo.Write("src/Code/a.cs", "class A { int afterX; }").Commit("A change after X");

        Assert.Empty(Check(repo, y, x).Refusals);
        Assert.Single(Check(repo, y, y).Refusals);
    }

    [Fact] // ADR-0208: the index and the table name the same rows and the same paths
    public void The_index_and_the_table_must_agree()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/Code/a.cs", "a").Write("src/Other/b.cs", "b").Commit();
        repo.Tag(Fixture.RunTag, recorded);
        var indexed = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/2026-10-12-windows-17/report.md", "pass"));
        var listed = new Row("VZ-14", "Windows", ["src/Code/", "src/Other/"]);
        var onlyListed = new Row("KB-3", "macOS", ["src/Code/"]);
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: {recorded}")
            .Write("verification/by-hand.json", Fixture.Index(indexed))
            .Write("docs/definition-of-done.md", Fixture.Table(listed, onlyListed));
        var commit = repo.Commit();

        var refusals = Check(repo, commit, commit).Refusals;

        Assert.Equal(2, refusals.Count);
        Assert.Contains("VZ-14 on Windows: the paths differ, only docs/definition-of-done.md names src/Other/", refusals[0].Message);
        Assert.Contains("KB-3 on macOS is in docs/definition-of-done.md's table and not in verification/by-hand.json", refusals[1].Message);
        Assert.Equal(12, refusals[1].Line);
    }

    [Fact] // ADR-0208: the table is read from the Definition of Done, and from its proposal until it lands there
    public void The_table_is_read_from_the_definition_of_done_or_its_proposal()
    {
        using var repo = new Repo();
        var row = new Row("VZ-14", "Windows", ["src/Code/"]);
        repo.Write("docs/definition-of-done.md", "# Definition of Done\n\nNo table yet.\n")
            .Write(ByHandTable.Proposal, "# Proposal\n\n" + Fixture.Table(row));
        var proposed = repo.Commit();
        Assert.Equal(ByHandTable.Proposal, ByHandTable.Read(new CommitTree(repo.Git, proposed), new Report())!.Source);

        repo.Write("docs/definition-of-done.md", Fixture.Table(row with { Paths = ["src/Landed/"] }));
        var landed = repo.Commit();
        var table = ByHandTable.Read(new CommitTree(repo.Git, landed), new Report())!;
        Assert.Equal(ByHandTable.DefinitionOfDone, table.Source);
        Assert.Equal(["src/Landed/"], Assert.Single(table.Rows).Paths);

        repo.Delete(ByHandTable.Proposal).Write("docs/definition-of-done.md", "# Definition of Done\n");
        var neither = repo.Commit();
        var report = new Report();
        Assert.Null(ByHandTable.Read(new CommitTree(repo.Git, neither), report));
        Assert.Contains("Neither docs/definition-of-done.md nor", Assert.Single(report.Refusals).Message);
    }

    [Theory] // ADR-0208: the table's shape is held, so a row cannot be misread
    [InlineData("| Row | Where | The code it covers |\n|---|---|---|\n| VZ-14 | Windows | `a/` |", "has the columns Row | Where | The code it covers")]
    [InlineData("| Row | Where | Checked by hand | The code it covers |\n|---|---|---|---|\n| VZ-14 | Windows | A check. |", "has 3 cells")]
    [InlineData("| Row | Where | Checked by hand | The code it covers |\n|---|---|---|---|\n| VZ-14 | Windows | A check. | a/ |", "does not name a row, where it is checked, and the paths")]
    [InlineData("No table follows.\n\n### Step 5", "has no table under")]
    [InlineData("| Row | Where | Checked by hand | The code it covers |\n|---|---|---|---|\n| VZ-14 | Windows | A check. | `a/` |\n| **VZ-14** | Windows | Again. | `b/` |", "VZ-14 on Windows is written twice")]
    public void A_table_out_of_shape_is_refused(string body, string why)
    {
        var report = new Report();
        ByHandTable.Parse("dod.md", $"#### {ByHandTable.Heading}\n\n{body}\n", report);

        Assert.Contains(why, Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0208: the table's paths are its last column's code spans, its rows bold or not
    public void The_table_reads_rows_and_code_spans()
    {
        var report = new Report();
        var table = ByHandTable.Parse("dod.md",
            $"**{ByHandTable.Heading}**\n\n| Row | Where | Checked by hand | The code it covers |\n|---|---|---|---|\n"
            + "| **§21.11** | Windows | Where focus is, as `document.activeElement` reads it | `src/A/`, `src/B.cs` |\n"
            + "| `FN-12e` | the Citrix PC | A check | `src/C/` |\n\nAfter the table.\n", report);

        Assert.Empty(report.Refusals);
        Assert.Equal(["§21.11 on Windows", "FN-12e on the Citrix PC"], table!.Rows.Select(r => r.Name));
        Assert.Equal(["src/A/", "src/B.cs"], table.Rows[0].Paths);
    }

    [Theory] // ADR-0208: the index is held to its shape
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/*.cs"], "record": null}]}""", "which is not a path from the repository's root")]
    [InlineData("""{"rows": [{"row": "WR-10", "where": "Linux", "paths": [":(glob)src/ExGrid.MudBlazor/"], "record": null}]}""", "which is not a path from the repository's root")]
    [InlineData("""{"rows": [{"row": "WR-10", "where": "Linux", "paths": [":(icase)src/*.MudBlazor/**"], "record": null}]}""", "which is not a path from the repository's root")]
    [InlineData("""{"rows": [{"row": "WR-10", "where": "Linux", "paths": [":(glob)../*.MudBlazor/**"], "record": null}]}""", "which is not a path from the repository's root")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["../src/"], "record": null}]}""", "which is not a path from the repository's root")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": [], "record": null}]}""", "names no paths")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"]}]}""", "has no 'record'")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "1655299", "tag": "verified/2026-10-01-windows-15", "path": "verification/2026-10-01-windows-15/r.md", "verdict": "pass"}}]}""", "Write the commit the record was made at in full")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "tag": "verified/2026-10-01-windows-15", "path": "docs/r.md", "verdict": "pass"}}]}""", "a record is a file in its run's directory under verification/")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "tag": "verified/2026-10-01-windows-15", "path": "verification/2026-10-01-windows-15/r.md", "verdict": "passed"}}]}""", "has the verdict 'passed'")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "path": "verification/2026-10-01-windows-15/r.md", "verdict": "pass"}}]}""", "names no 'tag'. A run tags the commit it verified")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "tag": "windows-15", "path": "verification/2026-10-01-windows-15/r.md", "verdict": "pass"}}]}""", "names the tag 'windows-15'. A record's tag is 'verified/' followed by the name of the record's directory under verification/, and nothing else: verified/2026-10-01-windows-15")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "tag": "verified/2026-10-01-Windows-15", "path": "verification/2026-10-01-windows-15/r.md", "verdict": "pass"}}]}""", "names the tag 'verified/2026-10-01-Windows-15'")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": null}, {"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": null}]}""", "is written twice")]
    [InlineData("""{"rows": [{"row": "VZ-14", "paths": ["src/"], "record": null}]}""", "does not say which row it is and where")]
    [InlineData("""{"records": []}""", "holds no 'rows' array")]
    [InlineData("""{"rows": [""", "is not JSON")]
    public void An_index_out_of_shape_is_refused(string json, string why)
    {
        using var repo = new Repo();
        var commit = repo.Write(ByHand.IndexPath, json).Commit();

        var report = new Report();
        ByHand.ReadIndex(new CommitTree(repo.Git, commit), report);

        Assert.Contains(report.Refusals, r => r.Message.Contains(why));
    }

    [Theory] // ADR-0208, ADR-0205 (2026-10-12): a path is a file, a directory, or a :(glob) pattern; no other magic, and no wildcard outside one
    [InlineData("src/ExGrid/", true)]
    [InlineData("src/ExGrid/GridMetrics.cs", true)]
    [InlineData("§21.11/notes.md", true)]
    [InlineData(":(glob)src/*.MudBlazor/**", true)]
    [InlineData(":(glob)tests/GlyphWidths/*.macos.json", true)]
    [InlineData("src/*.MudBlazor/", false)]
    [InlineData(":(glob)src/ExGrid.MudBlazor/", false)]
    [InlineData(":(literal)src/ExGrid/", false)]
    [InlineData(":(glob)/src/**", false)]
    [InlineData(":(glob)src/../**", false)]
    [InlineData("", false)]
    public void A_path_is_a_file_a_directory_or_a_glob(string path, bool valid) => Assert.Equal(valid, ByHand.IsPath(path));

    [Fact] // ADR-0208: with no index at all, nothing can be judged
    public void A_tag_without_the_index_is_refused()
    {
        using var repo = new Repo();
        var commit = repo.Write("README.md", "r").Commit();

        Assert.Contains("has no verification/by-hand.json", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }
}

/// <summary>A list of MudBlazor's published versions and their dates given to the check, as a test sets it.</summary>
public sealed class Listed(IReadOnlyList<PublishedVersion> versions) : IPublishedVersions
{
    public IReadOnlyList<PublishedVersion> Versions => versions;

    /// <summary>These versions, each published at the date given with it.</summary>
    public static Listed Dated(IEnumerable<(string Version, string Date)> listed) =>
        new(listed.Select(v => new PublishedVersion(v.Version, DateTimeOffset.Parse(v.Date, System.Globalization.CultureInfo.InvariantCulture))).ToList());

    /// <summary>These versions, each published a day before <paramref name="commit"/> was committed.</summary>
    public static Listed Before(string commit, Repo repo, params string[] listed)
    {
        var at = repo.Git.CommitTime(commit).AddDays(-1);
        return new(listed.Select(v => new PublishedVersion(v, at)).ToList());
    }

    public string Source => "the test's list";

    public bool TryRead(out IReadOnlyList<PublishedVersion> read, out string failure)
    {
        read = versions;
        failure = "";
        return true;
    }
}

/// <summary>A list that cannot be read, as nuget.org is when the network is down.</summary>
public sealed class Unreadable : IPublishedVersions
{
    public string Source => "the index";

    public bool TryRead(out IReadOnlyList<PublishedVersion> read, out string failure)
    {
        read = [];
        failure = "the network is down";
        return false;
    }
}
