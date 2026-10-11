using Xunit;

namespace ExGrid.Release.Tests;

public class AdmissionTests
{
    [Theory] // ADR-0205: a stable version and a candidate run the long run; a beta does not
    [InlineData("v0.1.0-beta.3", "prerelease", "false")]
    [InlineData("v0.1.0-rc.1", "candidate", "true")]
    [InlineData("v0.1.0", "stable", "true")]
    public void The_long_run_gates_a_stable_version_and_a_candidate(string tag, string kind, string longRun)
    {
        using var repo = new Repo();
        var commit = repo.Write("a.txt", "a").Commit();
        repo.Remote("main", commit);

        var report = new Report();
        var admission = Admission.Admit(repo.Git, tag, commit, report);

        Assert.Empty(report.Refusals);
        Assert.Equal(
            [$"version={tag[1..]}", $"kind={kind}", $"long={longRun}", "site=true"],
            admission!.Outputs());
    }

    [Fact] // ADR-0205: a tag may be on main or on a release branch
    public void A_tag_on_a_release_branch_is_admitted()
    {
        using var repo = new Repo();
        var main = repo.Write("a.txt", "a").Commit();
        repo.Remote("main", main);
        repo.Branch("release/0.1", main);
        var fix = repo.Write("a.txt", "fixed").Commit("A fix the branch takes");
        repo.Remote("release/0.1", fix);

        var report = new Report();
        var admission = Admission.Admit(repo.Git, "v0.1.0-rc.2", fix, report);

        Assert.Empty(report.Refusals);
        Assert.Equal(fix, admission!.Commit);
    }

    [Fact] // ADR-0042, ADR-0205: a commit on neither main nor a release branch is refused
    public void A_tag_on_another_branch_is_refused()
    {
        using var repo = new Repo();
        var main = repo.Write("a.txt", "a").Commit();
        repo.Remote("main", main);
        repo.Branch("claude/a-feature", main);
        var feature = repo.Write("a.txt", "b").Commit();
        repo.Remote("claude/a-feature", feature);
        repo.Remote("lab/release/0.1", feature);

        var report = new Report();
        Admission.Admit(repo.Git, "v0.1.0-beta.3", feature, report);

        var refusal = Assert.Single(report.Refusals);
        Assert.Contains("on neither main nor a release/* branch", refusal.Message);
        Assert.Contains("the branches fetched are main", refusal.Message);
    }

    [Fact] // ADR-0042: every refusal is said, the version's and the branch's together
    public void A_malformed_tag_off_main_is_refused_twice()
    {
        using var repo = new Repo();
        var commit = repo.Write("a.txt", "a").Commit();

        var report = new Report();
        Assert.Null(Admission.Admit(repo.Git, "v0.1.0-rc1", commit, report));

        Assert.Equal(2, report.Refusals.Count);
    }

    [Fact] // ADR-0205: once a stable version exists, a prerelease publishes no Docs Site
    public void A_prerelease_after_a_stable_version_publishes_no_site()
    {
        using var repo = new Repo();
        var stable = repo.Write("a.txt", "a").Commit();
        repo.Tag("v0.1.0", stable).Tag("v0.1.0-rc.1", stable);
        var next = repo.Write("a.txt", "b").Commit();
        repo.Remote("main", next);

        var beta = new Report();
        Assert.False(Admission.Admit(repo.Git, "v0.2.0-beta.1", next, beta)!.PublishesSite);
        Assert.Empty(beta.Refusals);
        Assert.Contains("v0.1.0 is tagged, so v0.2.0-beta.1, a prerelease, publishes no Docs Site", Assert.Single(beta.Notices));

        var candidate = new Report();
        Assert.False(Admission.Admit(repo.Git, "v0.1.1-rc.1", next, candidate)!.PublishesSite);

        var patch = new Report();
        Assert.True(Admission.Admit(repo.Git, "v0.1.1", next, patch)!.PublishesSite);
        Assert.Empty(patch.Notices);
    }

    [Fact] // ADR-0110, ADR-0205: before any stable version, a prerelease still publishes the site
    public void A_prerelease_before_any_stable_version_publishes_the_site()
    {
        using var repo = new Repo();
        var commit = repo.Write("a.txt", "a").Commit();
        repo.Remote("main", commit).Tag("v0.1.0-beta.2", commit).Tag("v0.1.0-rc.1", commit);

        var report = new Report();
        Assert.True(Admission.Admit(repo.Git, "v0.1.0-rc.2", commit, report)!.PublishesSite);
    }

    [Fact] // The tagged commit is named in full, whatever named it
    public void The_commit_is_resolved_in_full()
    {
        using var repo = new Repo();
        var commit = repo.Write("a.txt", "a").Commit();
        repo.Remote("main", commit).Tag("v0.1.0-beta.9", commit);

        var report = new Report();
        Assert.Equal(commit, Admission.Admit(repo.Git, "v0.1.0-beta.9", "v0.1.0-beta.9", report)!.Commit);

        var missing = new Report();
        Assert.Null(Admission.Admit(repo.Git, "v0.1.0-beta.9", new string('a', 40), missing));
        Assert.Contains("is not a commit of this repository", Assert.Single(missing.Refusals).Message);
    }
}
