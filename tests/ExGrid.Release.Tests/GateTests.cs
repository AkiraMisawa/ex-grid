using Xunit;

namespace ExGrid.Release.Tests;

public class GateTests
{
    private static Report Run(Repo repo, string tag, string commit)
    {
        var report = new Report();
        Gate.Run(repo.Git, TagVersion.Read(tag, report)!, commit, report);
        return report;
    }

    [Fact] // ADR-0042: a beta is gated by CI alone — no sign-off, no corpus, no rows checked by hand
    public void A_prerelease_runs_no_stable_check()
    {
        using var repo = new Repo();
        var commit = repo.Write("README.md", "Nothing a stable version needs.").Commit();

        var report = Run(repo, "v0.1.0-beta.3", commit);

        Assert.Empty(report.Refusals);
        Assert.Contains("is a prerelease", Assert.Single(report.Notices));
    }

    [Fact] // ADR-0205, ADR-0208: a stable version signed off, its corpus answered and its rows holding, is admitted
    public void A_signed_off_stable_version_is_admitted()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var y = Fixture.SignedOff(repo, x);

        var report = Run(repo, "v0.1.0", y);

        Assert.Empty(report.Refusals);
    }

    [Fact] // ADR-0205 (2026-10-12): a candidate rehearses the stable path without a sign-off of its own; its notes, read by form by the Docs Site's build, are not read here
    public void A_candidate_runs_every_check_but_the_sign_offs()
    {
        using var repo = new Repo();
        var commit = Fixture.ReadyToSignOff(repo);

        var report = Run(repo, "v0.1.0-rc.1", commit);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("is a release candidate") && n.Contains("pending form"));
        Assert.Contains(report.Notices, n => n.Contains("Every case under"));
        Assert.Contains(report.Notices, n => n.Contains("Every row checked by hand holds"));
    }

    [Fact] // ADR-0205, ADR-0208: a candidate is refused on an open question or a row to run, as a stable version is
    public void A_candidate_is_refused_where_a_stable_version_would_be()
    {
        using var repo = new Repo();
        Fixture.ReadyToSignOff(repo);
        var commit = repo.Write($"{Fixture.Corpus}/round.json", Fixture.Cases(("ROUND-001", "uncertain", null, null)))
            .Write("src/Code/a.cs", "class A { int changed; }")
            .Commit("An open case and a change the row covers");

        var report = Run(repo, "v0.1.0-rc.2", commit);

        Assert.Contains(report.Refusals, r => r.Message.Contains("ROUND-001"));
        Assert.Contains(report.Refusals, r => r.Message.Contains("VZ-14 on Windows: the code it covers changed"));
        Assert.DoesNotContain(report.Refusals, r => r.Message.Contains("Signed off"));
    }

    [Fact] // ADR-0205, ADR-0208 (decided with the user, 2026-10-12): a candidate is refused while WR-10's record misses a MudBlazor version published within the Wrappers' range
    public void A_candidate_is_refused_on_a_MudBlazor_version_its_floor_record_did_not_run()
    {
        using var repo = new Repo();
        repo.Write("src/ExGrid.MudBlazor/ExGrid.MudBlazor.csproj", Fixture.WrapperProject("[9.6.0,10.0.0)"));
        var commit = Fixture.ReadyToSignOff(repo, new Row("WR-10", "Windows or Linux", [":(glob)src/*.MudBlazor/**"], Versions: ["9.6.0"]));
        var version = TagVersion.Read("v0.1.0-rc.1", new Report())!;

        var covered = new Report();
        Gate.Run(repo.Git, version, commit, covered, mudBlazor: new Listed(["9.5.0", "9.6.0", "9.7.0-preview.1"]));
        Assert.Empty(covered.Refusals);

        var later = new Report();
        Gate.Run(repo.Git, version, commit, later, mudBlazor: new Listed(["9.5.0", "9.6.0", "9.7.0"]));
        Assert.Contains("WR-10 on Windows or Linux: MudBlazor 9.7.0, published within the range the Wrappers declare", Assert.Single(later.Refusals).Message);
    }

    [Fact] // ADR-0205, ADR-0208: a stable version says every refusal at once
    public void A_stable_version_says_every_refusal()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        repo.Write($"{Fixture.Corpus}/round.json", Fixture.Cases(("ROUND-001", "documented", null, null))).Commit("An open case after X");
        var y = Fixture.SignedOff(repo, x);

        var report = Run(repo, "v0.1.0", y);

        Assert.Contains(report.Refusals, r => r.Message.Contains("differs from the signed-off commit") && r.Message.Contains("round.json"));
        Assert.Contains(report.Refusals, r => r.Message.Contains("ROUND-001"));
    }

    [Fact] // ADR-0208: a stable version's rows are judged at X, so records filed after X do not move the target
    public void A_stable_versions_rows_are_judged_at_the_signed_off_commit()
    {
        using var repo = new Repo();
        var coded = repo.Write("src/Code/a.cs", "a")
            .Write($"{Fixture.Corpus}/abs.json", Fixture.Cases(("ABS-001", "observed", null, null)))
            .Commit("Code");
        var row = new Row("VZ-14", "Windows", ["src/Code/"]);
        repo.Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row));
        var x = repo.Commit("The row, never recorded");
        // The sign-off's own run records the row at X, after X, and tags X.
        repo.Tag("verified/2026-11-02-windows-18", x);
        repo.Write("verification/2026-11-02-windows-18/report.md", $"Verified commit: {x}")
            .Write("verification/by-hand.json", Fixture.Index(row with { Record = (x, "verification/2026-11-02-windows-18/report.md", "pass"), Tag = "verified/2026-11-02-windows-18" }));
        repo.Commit("Records the row at X");
        var y = Fixture.SignedOff(repo, x);

        var report = Run(repo, "v0.1.0", y);

        Assert.Empty(report.Refusals);
        Assert.NotEqual(coded, x);
    }

    [Fact] // ADR-0205 (2026-10-12): the stable tag is the candidate's commit X plus records and notes, so a sign-off made at no candidate's commit is refused
    public void A_stable_version_signed_off_at_no_candidate_s_commit_is_refused()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var y = Fixture.SignedOff(repo, x, candidate: false);

        var report = Run(repo, "v0.1.0", y);

        Assert.Contains("is not the commit of a release candidate of 0.1.0: no v0.1.0-rc.N is tagged", Assert.Single(report.Refusals).Message);
    }
}
