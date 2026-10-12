using Xunit;

namespace ExGrid.Release.Tests;

public class ByHandTests
{
    private static Report Check(Repo repo, string tag, string target)
    {
        var report = new Report();
        ByHand.Check(repo.Git, new CommitTree(repo.Git, tag), target, report, fetchMissing: false);
        return report;
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
        var row = new Row("VZ-14", "Windows", ["src/Code/", "src/one.cs"], (recorded, "verification/r/report.md", "pass"));
        repo.Write("verification/r/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
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
        var row = new Row("ED-30", "Windows", ["src/Code/"], (recorded, "verification/r/report.md", verdict));
        repo.Write("verification/r/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        Assert.Contains($"reads '{verdict}', and a row holds only on a pass", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }

    [Fact] // ADR-0208: a record whose commit cannot be read cannot be compared, and is refused
    public void A_record_made_at_a_commit_the_repository_lacks_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a");
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (new string('c', 40), "verification/r/report.md", "pass"));
        repo.Write("verification/r/report.md", $"Verified commit: {new string('c', 40)}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        Assert.Contains("which this repository does not have", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }

    [Fact] // ADR-0208: the record the index names is at the tag
    public void A_record_file_that_is_not_at_the_tag_is_refused()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/Code/a.cs", "a").Commit();
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/r/report.md", "pass"));
        repo.Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        Assert.Contains("verification/r/report.md, is not at the tag", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }

    [Fact] // ADR-0208: a record names the commit it verified, so a slip in the index cannot move a row's baseline
    public void A_record_that_does_not_name_its_commit_is_refused()
    {
        using var repo = new Repo();
        var verified = repo.Write("src/Code/a.cs", "a").Commit();
        var recorded = repo.Write("src/Code/a.cs", "a, later").Commit();
        var row = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/r/report.md", "pass"));
        repo.Write("verification/r/report.md", $"Verified commit: `{verified[..7]}`").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var commit = repo.Commit();

        var refusal = Assert.Single(Check(repo, commit, commit).Refusals);
        Assert.Contains($"does not name the commit {recorded[..12]}", refusal.Message);
        Assert.Equal("verification/r/report.md", refusal.File);
    }

    [Fact] // ADR-0208: a path that names nothing is a list that was not reviewed when its code moved
    public void A_path_that_names_nothing_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a").Write("src/Moved/b.cs", "b");
        var recorded = repo.Commit();
        var row = new Row("VZ-14", "Windows", ["src/Code/", "src/Gone/", "src/Code/a.cs/", "src/Moved"], (recorded, "verification/r/report.md", "pass"));
        repo.Write("verification/r/report.md", $"Verified commit: {recorded}").Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
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
        var indexed = new Row("VZ-14", "Windows", ["src/Code/"], (recorded, "verification/r/report.md", "pass"));
        var listed = new Row("VZ-14", "Windows", ["src/Code/", "src/Other/"]);
        var onlyListed = new Row("KB-3", "macOS", ["src/Code/"]);
        repo.Write("verification/r/report.md", $"Verified commit: {recorded}")
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
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["../src/"], "record": null}]}""", "which is not a path from the repository's root")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": [], "record": null}]}""", "names no paths")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"]}]}""", "has no 'record'")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "1655299", "path": "verification/a/r.md", "verdict": "pass"}}]}""", "Write the commit the record was made at in full")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "path": "docs/r.md", "verdict": "pass"}}]}""", "a record is a file under verification/")]
    [InlineData("""{"rows": [{"row": "VZ-14", "where": "Windows", "paths": ["src/"], "record": {"commit": "16552992f8fa6838114b7934c81190b788b3f32c", "path": "verification/a/r.md", "verdict": "passed"}}]}""", "has the verdict 'passed'")]
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

    [Fact] // ADR-0208: with no index at all, nothing can be judged
    public void A_tag_without_the_index_is_refused()
    {
        using var repo = new Repo();
        var commit = repo.Write("README.md", "r").Commit();

        Assert.Contains("has no verification/by-hand.json", Assert.Single(Check(repo, commit, commit).Refusals).Message);
    }
}
