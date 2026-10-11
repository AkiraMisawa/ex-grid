using Xunit;

namespace ExGrid.Release.Tests;

public class CorpusTests
{
    private static (IReadOnlyList<Corpus.OpenCase> Open, Report Report) Read(Repo repo, string commit)
    {
        var report = new Report();
        return (Corpus.Open(new CommitTree(repo.Git, commit), report), report);
    }

    [Fact] // ADR-0208: a case is open while it is uncertain or documented without a written reason
    public void Uncertain_and_documented_cases_without_a_reason_are_open()
    {
        using var repo = new Repo();
        repo.Write($"{Fixture.Corpus}/abs.json", Fixture.Cases(
                ("ABS-001", "observed", null, null),
                ("ABS-002", "uncertain", null, null),
                ("ABS-003", "documented", null, null),
                ("ABS-004", "uncertain", "Excel's undo does not reach changes made through COM; ask by hand", null),
                ("ABS-005", "documented", null, "ADR-0048: renaming the Sheet is an undoable step"),
                ("ABS-006", "uncertain", "   ", null),
                ("ABS-007", null, null, null),
                ("ABS-008", "guessed", null, null)))
            .Write($"{Fixture.Corpus}/fixtures.json", "{\"functionData\": {}}");
        var commit = repo.Commit();

        var (open, report) = Read(repo, commit);

        Assert.Empty(report.Refusals);
        Assert.Equal(["ABS-002", "ABS-003", "ABS-006", "ABS-007", "ABS-008"], open.Select(c => c.Id));
        Assert.All(open, c => Assert.Equal($"{Fixture.Corpus}/abs.json", c.File));
        Assert.Equal("(no source)", open.Single(c => c.Id == "ABS-007").Source);
    }

    [Fact] // ADR-0208: the refusal names each file's open cases and the run of oracle.ps1 that answers them
    public void An_open_case_refuses_the_tag_and_says_how_to_ask()
    {
        using var repo = new Repo();
        repo.Write($"{Fixture.Corpus}/abs.json", Fixture.Cases(("ABS-001", "uncertain", null, null), ("ABS-002", "documented", null, null)))
            .Write($"{Fixture.Corpus}/round.json", Fixture.Cases(("ROUND-001", "uncertain", null, null)))
            .Write($"{Fixture.Corpus}/sum.json", Fixture.Cases(("SUM-001", "observed", null, null)));
        var commit = repo.Commit();

        var report = new Report();
        Corpus.Check(new CommitTree(repo.Git, commit), report);

        Assert.Equal(3, report.Refusals.Count);
        Assert.Equal($"{Fixture.Corpus}/abs.json", report.Refusals[0].File);
        Assert.Contains("2 cases in", report.Refusals[0].Message);
        Assert.Contains("uncertain: ABS-001; documented: ABS-002", report.Refusals[0].Message);
        Assert.Contains("1 case in", report.Refusals[1].Message);
        Assert.Contains("3 of ExSheet's Excel cases, in 2 files", report.Refusals[2].Message);
        Assert.Contains("oracle.ps1 -Area abs,round -Update", report.Refusals[2].Message);
    }

    [Fact] // ADR-0208: no case open, nothing refused
    public void A_corpus_with_every_question_answered_is_admitted()
    {
        using var repo = new Repo();
        repo.Write($"{Fixture.Corpus}/abs.json", Fixture.Cases(("ABS-001", "observed", null, null), ("ABS-002", "uncertain", "nothing to ask", null)));
        var commit = repo.Commit();

        var report = new Report();
        Corpus.Check(new CommitTree(repo.Git, commit), report);

        Assert.Empty(report.Refusals);
        Assert.Contains("Every case", Assert.Single(report.Notices));
    }

    [Fact] // ADR-0208: a file that cannot be read is refused, never passed over
    public void A_file_that_cannot_be_read_is_refused()
    {
        using var repo = new Repo();
        repo.Write($"{Fixture.Corpus}/broken.json", "{ \"cases\": [ { \"id\": ")
            .Write($"{Fixture.Corpus}/shapeless.json", "{ \"area\": \"x\" }")
            .Write($"{Fixture.Corpus}/notes.md", "not a case file");
        var commit = repo.Commit();

        var (open, report) = Read(repo, commit);

        Assert.Empty(open);
        Assert.Equal(2, report.Refusals.Count);
        Assert.Contains("is not JSON", report.Refusals[0].Message);
        Assert.Contains("holds no 'cases' array", report.Refusals[1].Message);
    }

    [Fact] // ADR-0208: "could not check" is not a pass — a commit without the corpus is refused
    public void A_commit_without_the_corpus_is_refused()
    {
        using var repo = new Repo();
        var commit = repo.Write("README.md", "No corpus here.").Commit();

        var (_, report) = Read(repo, commit);

        Assert.Contains("has no case files under", Assert.Single(report.Refusals).Message);
    }

    [Fact] // The tag's tree is read, not the working tree
    public void The_commit_is_read_not_the_working_tree()
    {
        using var repo = new Repo();
        repo.Write($"{Fixture.Corpus}/abs.json", Fixture.Cases(("ABS-001", "uncertain", null, null)));
        var commit = repo.Commit();
        repo.Write($"{Fixture.Corpus}/abs.json", Fixture.Cases(("ABS-001", "observed", null, null)));

        Assert.Single(Read(repo, commit).Open);
        Assert.Empty(Corpus.Open(new DirectoryTree(repo.Root), new Report()));
    }
}
