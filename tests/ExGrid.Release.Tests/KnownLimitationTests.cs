using Xunit;

namespace ExGrid.Release.Tests;

/// <summary>
/// A row checked by hand put out of a version by decision (VZ-18 out of 0.1.0, decided with the user on 2026-10-12):
/// the record index marks it <c>knownLimitation</c>, and the release check reports it for that version instead of
/// refusing it, never lets it hold, and refuses the mark where it could let a row out silently. How the index marks
/// it and how the check reads it are the release tool's (2026-10-12).
/// </summary>
public class KnownLimitationTests
{
    private static Report Check(Repo repo, string commit, string? version)
    {
        var report = new Report();
        ByHand.Check(repo.Git, new CommitTree(repo.Git, commit), commit, report, fetchMissing: false, version: version);
        return report;
    }

    // A repository whose one row has no record and is marked as given; returns its commit.
    private static string Marked(Repo repo, Limited? limited, (string Commit, string Path, string Verdict)? record = null)
    {
        repo.Write("src/Code/a.cs", "a");
        var row = new Row("VZ-18", "the Citrix PC", ["src/Code/"], record, Limited: limited);
        return repo.Write("verification/by-hand.json", Fixture.Index(row)).Write("docs/definition-of-done.md", Fixture.Table(row)).Commit();
    }

    [Theory] // ADR-0208 (VZ-18, decided with the user 2026-10-12): a row put out of 0.1.0 by decision is reported for 0.1.0's candidates and 0.1.0, not refused, and never holds
    [InlineData("0.1.0-rc.1")]
    [InlineData("0.1.0-rc.2")]
    [InlineData("0.1.0")]
    public void A_known_limitation_of_the_version_is_reported_not_refused(string version)
    {
        using var repo = new Repo();
        var commit = Marked(repo, new Limited());

        var report = Check(repo, commit, version);

        Assert.Empty(report.Refusals);
        Assert.Contains(report.Notices, n => n.Contains("Not run, and not holding, as a Known limitation of 0.1.0 by decision: VZ-18 on the Citrix PC")
            && n.Contains("decision of 2026-10-12") && n.Contains("VZ-18 is a Known limitation of 0.1.0."));
        Assert.DoesNotContain(report.Notices, n => n.Contains("Holding on their records"));
        Assert.Contains(report.Notices, n => n.Contains("holds at") && n.Contains("but the Known limitations of 0.1.0 named above"));
    }

    [Theory] // ADR-0208 (2026-10-12): the mark reaches only the version it names; any other, or a check given none, refuses the row as never recorded
    [InlineData("0.1.1")]
    [InlineData("0.2.0-rc.1")]
    [InlineData(null)]
    public void A_known_limitation_does_not_reach_another_version(string? version)
    {
        using var repo = new Repo();
        var commit = Marked(repo, new Limited());

        var refusal = Assert.Single(Check(repo, commit, version).Refusals);

        Assert.Contains("VZ-18 on the Citrix PC has never been recorded", refusal.Message);
        Assert.Contains("It is a Known limitation of 0.1.0 by the decision of 2026-10-12, which does not reach", refusal.Message);
    }

    [Fact] // ADR-0208: a row with no record and no mark is still refused, whatever version is released
    public void A_row_without_a_record_or_a_mark_is_still_refused_for_the_version()
    {
        using var repo = new Repo();
        var commit = Marked(repo, null);

        var refusal = Assert.Single(Check(repo, commit, "0.1.0").Refusals);

        Assert.Contains("VZ-18 on the Citrix PC has never been recorded", refusal.Message);
        Assert.DoesNotContain("Known limitation", refusal.Message);
    }

    [Fact] // ADR-0208 (2026-10-12): a marked row that also names a record is refused, never read as either
    public void A_known_limitation_with_a_record_is_refused()
    {
        using var repo = new Repo();
        var recorded = repo.Write("src/Code/a.cs", "a").Commit();
        repo.Tag(Fixture.RunTag, recorded);
        repo.Write($"{Fixture.RunDirectory}/report.md", $"Verified commit: {recorded}");
        var commit = Marked(repo, new Limited(), (recorded, $"{Fixture.RunDirectory}/report.md", "pass"));

        var report = Check(repo, commit, "0.1.0");

        Assert.Contains(report.Refusals, r => r.Message.Contains("VZ-18 on the Citrix PC in verification/by-hand.json names a record and 'knownLimitation'"));
    }

    [Theory] // ADR-0208 (2026-10-12): a mark without its version, its day or its decision is refused, so a row never stops being asked for without saying why
    [InlineData("0.1.0-rc.1", "2026-10-12", "A decision.", "names the version '0.1.0-rc.1'")]
    [InlineData("", "2026-10-12", "A decision.", "names the version ''")]
    [InlineData("0.1.0", "", "A decision.", "gives '' as the day it was decided")]
    [InlineData("0.1.0", "12 October", "A decision.", "gives '12 October' as the day it was decided")]
    [InlineData("0.1.0", "2026-10-12", "", "gives no 'decision'")]
    [InlineData("0.1.0", "2026-10-12", "   ", "gives no 'decision'")]
    public void A_malformed_known_limitation_is_refused(string version, string decided, string decision, string expected)
    {
        using var repo = new Repo();
        var commit = Marked(repo, new Limited(version, decided, decision, InTable: false));

        var report = Check(repo, commit, "0.1.0");

        Assert.Contains(report.Refusals, r => r.Message.Contains($"VZ-18 on the Citrix PC's 'knownLimitation' in verification/by-hand.json {expected}"));
        // Malformed, it puts nothing out: the row is refused as never recorded too.
        Assert.Contains(report.Refusals, r => r.Message.Contains("has never been recorded"));
    }

    [Fact] // ADR-0208 (2026-10-12): a mark that is not an object is refused
    public void A_known_limitation_that_is_not_an_object_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a")
            .Write("verification/by-hand.json", "{\"rows\": [{\"row\": \"VZ-18\", \"where\": \"the Citrix PC\", \"paths\": [\"src/Code/\"], \"record\": null, \"knownLimitation\": \"0.1.0\"}]}")
            .Write("docs/definition-of-done.md", Fixture.Table(new Row("VZ-18", "the Citrix PC", ["src/Code/"])));
        var commit = repo.Commit();

        var report = Check(repo, commit, "0.1.0");

        Assert.Contains(report.Refusals, r => r.Message.Contains("'knownLimitation' in verification/by-hand.json is not an object"));
    }

    [Fact] // ADR-0208 (2026-10-12): the index alone cannot put a row out; the Definition of Done's table says the same, or the tag is refused
    public void A_known_limitation_the_table_does_not_say_is_refused()
    {
        using var repo = new Repo();
        var commit = Marked(repo, new Limited(InTable: false));

        var refusal = Assert.Single(Check(repo, commit, "0.1.0").Refusals);

        Assert.Contains("VZ-18 on the Citrix PC: verification/by-hand.json marks it a Known limitation of 0.1.0, and docs/definition-of-done.md's table does not say so", refusal.Message);
    }

    [Fact] // ADR-0208 (2026-10-12): a table that says a row is a Known limitation the index does not mark is refused, and the row is not put out
    public void A_known_limitation_only_the_table_says_is_refused()
    {
        using var repo = new Repo();
        repo.Write("src/Code/a.cs", "a")
            .Write("verification/by-hand.json", Fixture.Index(new Row("VZ-18", "the Citrix PC", ["src/Code/"])))
            .Write("docs/definition-of-done.md", Fixture.Table(new Row("VZ-18", "the Citrix PC", ["src/Code/"], Limited: new Limited())));
        var commit = repo.Commit();

        var refusals = Check(repo, commit, "0.1.0").Refusals;

        Assert.Contains(refusals, r => r.Message.Contains("docs/definition-of-done.md's table says it is a Known limitation of 0.1.0, and verification/by-hand.json does not mark it 'knownLimitation'"));
        Assert.Contains(refusals, r => r.Message.Contains("has never been recorded"));
    }

    [Fact] // ADR-0208 (2026-10-12): the table's mark is read from its "Checked by hand" cell, one version a row
    public void The_table_reads_the_version_a_row_is_put_out_of()
    {
        var report = new Report();
        var table = ByHandTable.Parse("dod.md",
            "**Rows checked by hand, and the code each covers**\n\n| Row | Where | Checked by hand | The code it covers |\n|---|---|---|---|\n"
            + "| **VZ-18** | the Citrix PC | A check. *(2026-10-12, decided with the user: a Known limitation of `0.1.0`.)* | `src/A/` |\n"
            + "| **VZ-14** | Windows | A check. | `src/B/` |\n"
            + "| **KB-3** | macOS | A Known limitation of `0.1.0` and a Known limitation of `0.2.0`. | `src/C/` |\n",
            report)!;

        Assert.Equal(["0.1.0", null, null], table.Rows.Select(r => r.KnownLimitation));
        Assert.Contains("KB-3 on macOS is a Known limitation of 0.1.0 and 0.2.0", Assert.Single(report.Refusals).Message);
    }

    [Fact] // ADR-0205, ADR-0208 (2026-10-12): the release workflow's gate passes the tag's version, so a candidate of 0.1.0 is not refused on a row put out of 0.1.0
    public void The_gate_reads_a_known_limitation_against_the_tags_version()
    {
        using var repo = new Repo();
        var x = Fixture.ReadyToSignOff(repo);
        var row = new Row("VZ-18", "the Citrix PC", ["src/Code/"], Limited: new Limited());
        var held = new Row("VZ-14", "Windows", ["src/Code/"], (repo.Git.Commit($"{x}~1")!, $"{Fixture.RunDirectory}/report.md", "pass"));
        var commit = repo.Write("verification/by-hand.json", Fixture.Index(held, row)).Write("docs/definition-of-done.md", Fixture.Table(held, row)).Commit();

        var candidate = new Report();
        Gate.Run(repo.Git, TagVersion.Read("v0.1.0-rc.1", candidate)!, commit, candidate);
        var next = new Report();
        Gate.Run(repo.Git, TagVersion.Read("v0.2.0-rc.1", next)!, commit, next);

        Assert.Empty(candidate.Refusals);
        Assert.Contains(candidate.Notices, n => n.Contains("as a Known limitation of 0.1.0 by decision"));
        Assert.Contains("VZ-18 on the Citrix PC has never been recorded", Assert.Single(next.Refusals).Message);
    }
}
