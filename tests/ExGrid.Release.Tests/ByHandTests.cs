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

    /// <summary>MudBlazor 9.0.0 to 9.11.0 and the prereleases around them, as nuget.org listed them on 2026-10-12.</summary>
    private static readonly string[] Published =
        ["8.15.0", "9.0.0-preview.1", "9.0.0-preview.2", "9.0.0-rc.1", "9.0.0", "9.1.0", "9.2.0", "9.3.0", "9.4.0", "9.5.0", "9.6.0", "9.7.0", "9.8.0", "9.9.0", "9.10.0", "9.11.0"];

    /// <summary>The versions a floor record of 2026-10-12 would name: every published one of [9.6.0,10.0.0).</summary>
    private static readonly string[] Covered = ["9.6.0", "9.7.0", "9.8.0", "9.9.0", "9.10.0", "9.11.0"];

    private const string FloorTag = "verified/2026-11-02-linux-mudblazor-floor";

    /// <summary>An index of WR-10 alone, its record written as given.</summary>
    private static string Wr10Index(string record) =>
        "{\"rows\": [{\"row\": \"WR-10\", \"where\": \"Windows or Linux\", \"paths\": [\"src/\"], \"record\": " + record + "}]}";

    private const string FloorRecord = "verification/2026-11-02-linux-mudblazor-floor/results.md";

    /// <summary>
    /// A repository whose three Wrappers declare <paramref name="ranges"/> (one for all, or one each), and
    /// whose WR-10 row is recorded at the commit their code was last changed, naming <paramref name="versions"/>.
    /// Returns the commit that indexes it.
    /// </summary>
    private static string FloorRecorded(Repo repo, string[] versions, params string[] ranges)
    {
        var wrappers = new[] { "ExGrid.MudBlazor", "ExSheet.MudBlazor", "ExPivot.MudBlazor" };
        for (var i = 0; i < wrappers.Length; i++)
            repo.Write($"src/{wrappers[i]}/{wrappers[i]}.csproj", Fixture.WrapperProject(ranges[Math.Min(i, ranges.Length - 1)]));
        var recorded = repo.Write("samples/ExGrid.DemoPages/Pages/SheetPage.razor", "page").Commit("The Wrappers");
        repo.Tag(FloorTag, recorded);
        var row = new Row("WR-10", "Windows or Linux", [":(glob)src/*.MudBlazor/**", "samples/ExGrid.DemoPages/"],
            (recorded, FloorRecord, "pass"), FloorTag, versions);
        return repo.Write(FloorRecord, $"Verified commit: `{recorded}`")
            .Write("verification/by-hand.json", Fixture.Index(row))
            .Write("docs/definition-of-done.md", Fixture.Table(row))
            .Commit("The floor record");
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
        var published = new Listed(Published);
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

    [Fact] // ADR-0205, ADR-0208 (decided with the user, 2026-10-12): WR-10 holds while its record names every stable MudBlazor published within the Wrappers' range
    public void WR_10_holds_on_a_record_that_names_every_published_version_of_the_range()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");

        // Below the floor, above the range, and the prereleases are not asked for.
        var report = Check(repo, commit, commit, new Listed([.. Published, "9.12.0-preview.1", "10.0.0", "10.1.0"]));

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("Holding on their records, their code unchanged since: WR-10 on Windows or Linux"));
    }

    [Fact] // ADR-0205, ADR-0208 (decided with the user, 2026-10-12): a 9.x published after the floor record asks for the layer-2 runs at it, and the tag is refused naming it
    public void WR_10_is_refused_naming_each_version_published_after_its_record()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)");

        var report = Check(repo, commit, commit, new Listed([.. Published, "9.12.0", "9.13.0-rc.1", "9.13.0", "10.0.0"]));

        var refusal = Assert.Single(report.Refusals).Message;
        Assert.Contains("WR-10 on Windows or Linux: MudBlazor 9.12.0, 9.13.0, published within the range the Wrappers declare ([9.6.0,10.0.0)), are not among the versions its record's layer-2 runs covered (9.6.0, 9.7.0, 9.8.0, 9.9.0, 9.10.0, 9.11.0)", refusal);
        Assert.Contains("asks for each Wrapper's layer-2 suite at that version only", refusal);
        Assert.Contains("the floor run holds while its paths are unchanged", refusal);
        Assert.DoesNotContain("10.0.0,", refusal);
        Assert.Contains(report.Notices, n => n.Contains("To run by hand at") && n.Contains("WR-10 on Windows or Linux's layer-2 runs at MudBlazor 9.12.0, 9.13.0"));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
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

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): WR-10's record names the versions its layer-2 runs covered, each a stable version
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

        var refusal = Assert.Single(Check(repo, commit, commit, new Listed([.. Published, "10.0.0"])).Refusals).Message;

        Assert.Contains("MudBlazor 9.0.0, 9.1.0, 9.2.0, 9.3.0, 9.4.0, 9.5.0, 10.0.0, published within the range the Wrappers declare (9.0.0, [9.6.0,10.0.0))", refusal);
    }

    [Fact] // ADR-0205 (2026-10-12): a Wrapper whose MudBlazor range cannot be read refuses the tag, never lets WR-10 hold on a guess
    public void A_Wrapper_whose_range_cannot_be_read_is_refused()
    {
        using var repo = new Repo();
        var commit = FloorRecorded(repo, Covered, "[9.6.0,10.0.0)", "[9.6.0,10.0.0)", "9.*");

        var refusal = Assert.Single(Check(repo, commit, commit, new Listed(Published)).Refusals);

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

    [Fact] // ADR-0205 (2026-10-12): nuget.org's flat-container index is read as it is written; a copy that is not one is refused, never read as empty
    public void A_flat_container_index_is_read_and_one_that_is_not_is_refused()
    {
        var directory = Path.Combine(Path.GetTempPath(), "exgrid-release-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var saved = Path.Combine(directory, "index.json");
            File.WriteAllText(saved, """{"versions": ["9.0.0-rc.1", "9.0.0", "9.11.0"]}""");
            Assert.True(new NuGetFlatContainer(saved).TryRead(out var versions, out _));
            Assert.Equal(["9.0.0-rc.1", "9.0.0", "9.11.0"], versions);

            var wrong = Path.Combine(directory, "wrong.json");
            File.WriteAllText(wrong, """{"items": []}""");
            Assert.False(new NuGetFlatContainer(wrong).TryRead(out _, out var why));
            Assert.Contains("not a flat-container index", why);

            Assert.False(new NuGetFlatContainer(Path.Combine(directory, "absent.json")).TryRead(out _, out why));
            Assert.NotEmpty(why);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

        var refusals = Check(repo, commit, commit, new Listed(Published)).Refusals;
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

/// <summary>A list of MudBlazor's published versions given to the check, as a test sets it.</summary>
public sealed class Listed(IReadOnlyList<string> versions) : IPublishedVersions
{
    public string Source => "the test's list";

    public bool TryRead(out IReadOnlyList<string> read, out string failure)
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

    public bool TryRead(out IReadOnlyList<string> read, out string failure)
    {
        read = [];
        failure = "the network is down";
        return false;
    }
}
