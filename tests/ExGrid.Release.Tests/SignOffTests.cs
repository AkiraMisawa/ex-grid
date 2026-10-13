using Xunit;

namespace ExGrid.Release.Tests;

public class SignOffTests
{
    private static readonly TagVersion Stable = new("v0.1.0", "0.1.0", ReleaseKind.Stable);

    private static Report Check(Repo repo, string tagCommit, out string? x)
    {
        var report = new Report();
        x = SignOff.Check(repo.Git, new CommitTree(repo.Git, tagCommit), Stable, tagCommit, report);
        return report;
    }

    [Fact] // ADR-0205: the tag is the signed-off commit plus only its records and its notes
    public void A_tag_that_is_X_plus_records_and_notes_is_admitted()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var y = Fixture.SignedOff(repo, x);

        var report = Check(repo, y, out var signedOff);

        Assert.Empty(report.Refusals);
        Assert.Equal(x, signedOff);
        Assert.Contains(report.Notices, n => n.Contains("is the signed-off commit"));
        Assert.Contains(report.Notices, n => n.Contains("is the commit v0.1.0-rc.1 was tagged on"));
    }

    [Fact] // ADR-0205 (2026-10-12): the sign-off is made against a release candidate, so X is a candidate's commit
    public void A_signed_off_commit_no_candidate_was_tagged_on_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var y = Fixture.SignedOff(repo, x, candidate: false);

        var refusal = Assert.Single(Check(repo, y, out var signedOff).Refusals);
        Assert.Equal(x, signedOff);
        Assert.Contains($"The signed-off commit {x[..12]} is not the commit of a release candidate of 0.1.0: no v0.1.0-rc.N is tagged", refusal.Message);
    }

    [Fact] // ADR-0205 (2026-10-12): a candidate of the version tagged on another commit, or a candidate of another version, is not X's
    public void A_candidate_tagged_elsewhere_is_not_X_s()
    {
        using var repo = new Repo();
        var earlier = repo.Write("src/Code/early.cs", "early").Commit("An earlier candidate's commit");
        repo.Tag("v0.1.0-rc.1", earlier);
        var x = Fixture.ReadyToSignOff(repo);
        repo.Tag("v0.2.0-rc.1", x).Tag("v0.1.0-beta.9", x).Tag("v0.1.0-rc1", x);
        var y = Fixture.SignedOff(repo, x, candidate: false);

        var refusal = Assert.Single(Check(repo, y, out _).Refusals);
        Assert.Contains("is not the commit of a release candidate of 0.1.0 (v0.1.0-rc.1 is tagged elsewhere)", refusal.Message);
    }

    [Fact] // ADR-0190, ADR-0205 (2026-10-12): the pending form is a candidate's; a version without a suffix is signed off
    public void A_stable_version_s_block_in_the_pending_form_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var y = Fixture.SignedOff(repo, x, block: "- **Pending:** the Definition of Done is being signed off against this release candidate, `v0.1.0-rc.1`.");

        var refusals = Check(repo, y, out _).Refusals;
        Assert.Contains(refusals, r => r.Message.Contains("is in its pending form, **Pending:**, which is a release candidate's"));
        Assert.Contains(refusals, r => r.Message.Contains("names no verification/<date>/results.md"));
    }

    [Fact] // ADR-0205: records filed after X, and changed again before the tag, are records still
    public void Records_changed_after_X_are_admitted()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        repo.Write(Fixture.Results, "Verified at the commit that holds this file.");
        var x = repo.Commit("The record, filed before the notes");
        repo.Tag("v0.1.0-rc.1", x);
        repo.Write(Fixture.Results, $"Verified commit: {x}\n").Write("docs/releases/0.1.0.md", Fixture.Notes("0.1.0", $"`{Fixture.Results}` at {x}"));
        var y = repo.Commit("Notes");

        Assert.Empty(Check(repo, y, out _).Refusals);
    }

    [Fact] // ADR-0205: anything else after X is a new X and a new sign-off
    public void A_change_after_X_outside_the_records_and_notes_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        repo.Write("src/Code/a.cs", "class A { int fixedAfterSignOff; }")
            .Write("docs/releases/unreleased/a-fix.md", "Product: ExGrid")
            .Write("verification/later/notes.md", "a record");
        repo.Commit("A fix after the sign-off");
        var y = Fixture.SignedOff(repo, x);

        var refusal = Assert.Single(Check(repo, y, out _).Refusals);
        Assert.Contains("differs from the signed-off commit", refusal.Message);
        Assert.Contains("src/Code/a.cs", refusal.Message);
        Assert.DoesNotContain("a-fix.md", refusal.Message);
        Assert.DoesNotContain("notes.md", refusal.Message);
    }

    [Fact] // ADR-0205: a file deleted after X is a change after X
    public void A_deletion_after_X_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        repo.Delete("README.md");
        var y = Fixture.SignedOff(repo, x);

        Assert.Contains("README.md", Assert.Single(Check(repo, y, out _).Refusals).Message);
    }

    [Fact] // ADR-0205: the notes name the sign-off, or the tag is refused
    public void Notes_without_a_signed_off_block_are_refused()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        repo.Write("docs/releases/0.1.0.md", Fixture.Notes("0.1.0", null));
        var y = repo.Commit("Notes");

        var refusal = Assert.Single(Check(repo, y, out var x).Refusals);
        Assert.Null(x);
        Assert.Contains("has no '## Signed off' block", refusal.Message);
        Assert.Equal("docs/releases/0.1.0.md", refusal.File);
    }

    [Fact] // ADR-0205: no notes at the tag, nothing names the sign-off
    public void A_tag_without_its_notes_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);

        Assert.Contains("are not at the tag", Assert.Single(Check(repo, x, out _).Refusals).Message);
    }

    [Theory] // ADR-0205: one record, and one commit written in full
    [InlineData("Against `verification/2026-11-02-windows/results.md`.", "names no commit in full")]
    [InlineData("Against `verification/2026-11-02-windows/results.md`, made at `{short}`.", "names no commit in full")]
    [InlineData("Made at `{x}`.", "names no verification/<date>/results.md")]
    [InlineData("Against `verification/2026-11-02-windows/results.md` and `verification/2026-11-03-macos/results.md`, at `{x}`.", "names 2 records")]
    [InlineData("Against `verification/2026-11-02-windows/results.md`, at `{x}` and `{other}`.", "names 2 commits")]
    public void The_block_names_one_record_and_one_commit_in_full(string block, string why)
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var other = repo.Run("rev-parse", "HEAD~1").Trim();
        var y = Fixture.SignedOff(repo, x, block: block.Replace("{x}", x).Replace("{short}", x[..12]).Replace("{other}", other));

        Assert.Contains(Check(repo, y, out _).Refusals, r => r.Message.Contains(why));
    }

    [Fact] // ADR-0190, ADR-0205: the block as the Docs Site's notes parser requires it, labelled lines and the record linked at the tag, is read as one record and one commit
    public void The_labelled_block_the_notes_parser_requires_is_read()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var block = string.Join("\n",
            $"- **Record:** [{Fixture.Results}](https://github.com/owner/ex-grid/blob/v0.1.0/{Fixture.Results})",
            $"- **Commit:** `{x}`",
            "- **Toolchain:** .NET SDK 10.0.203, Node 22.11.0, Playwright 1.56.0",
            "- **OS:** Ubuntu 24.04 (CI), Windows 11 24H2 and macOS 15.1 (by hand)",
            "- **Browsers:** Chrome 141 and Edge 141");
        var y = Fixture.SignedOff(repo, x, block: block);

        var report = Check(repo, y, out var signedOff);

        Assert.Empty(report.Refusals);
        Assert.Equal(x, signedOff);
    }

    [Fact] // ADR-0205: the record exists at the tag
    public void A_record_that_is_not_at_the_tag_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        repo.Tag("v0.1.0-rc.1", x);
        repo.Write("docs/releases/0.1.0.md", Fixture.Notes("0.1.0", $"Against `{Fixture.Results}`, at `{x}`."));
        var y = repo.Commit("Notes without the record");

        Assert.Contains("which is not at the tag", Assert.Single(Check(repo, y, out _).Refusals).Message);
    }

    [Fact] // ADR-0205: results.md names X, so notes naming another commit are caught
    public void A_record_that_does_not_name_X_is_refused()
    {
        using var repo = new Repo();
        var earlier = repo.Write("a.txt", "a").Commit();
        var x = Fixture.ReadyToSignOff(repo);
        repo.Tag("v0.1.0-rc.1", x);
        repo.Write(Fixture.Results, $"Verified commit: `{earlier[..7]}`")
            .Write("docs/releases/0.1.0.md", Fixture.Notes("0.1.0", $"Against `{Fixture.Results}`, at `{x}`."));
        var y = repo.Commit("Signs off");

        var refusal = Assert.Single(Check(repo, y, out _).Refusals);
        Assert.Contains("does not name the commit", refusal.Message);
        Assert.Equal(Fixture.Results, refusal.File);
    }

    [Fact] // ADR-0205: X is the tag or an ancestor of it
    public void A_signed_off_commit_off_the_tags_history_is_refused()
    {
        using var repo = new Repo();
        var main = Fixture.ReadyToSignOff(repo);
        repo.Branch("elsewhere", main);
        var elsewhere = repo.Write("src/Code/a.cs", "class A { }").Commit("Elsewhere");
        repo.Run("checkout", "--quiet", "main");
        var y = Fixture.SignedOff(repo, elsewhere);

        Assert.Contains("is not v0.1.0 or an ancestor of it", Assert.Single(Check(repo, y, out _).Refusals).Message);
    }

    [Fact] // ADR-0205: a commit this repository does not hold names no sign-off
    public void A_signed_off_commit_the_repository_does_not_hold_is_refused()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        var y = Fixture.SignedOff(repo, new string('b', 40));

        Assert.Contains(Check(repo, y, out var x).Refusals, r => r.Message.Contains("is not a commit of this repository"));
        Assert.Null(x);
    }

    [Fact] // ADR-0190: the block is read up to the next heading, line endings as written
    public void The_block_ends_at_the_next_heading()
    {
        var (block, line) = SignOff.Block("# 0.1.0\r\n\r\n## Signed off\r\n\r\nAgainst it.\r\n## SHOULD criteria accepted failing\r\n\r\nNone.\r\n");

        Assert.Equal(3, line);
        Assert.Equal("\nAgainst it.", block);
        Assert.Null(SignOff.Block("# 0.1.0\n\n## Signed off later\n").Block);
    }
}
