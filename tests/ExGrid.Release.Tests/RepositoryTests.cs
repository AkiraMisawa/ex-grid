using Xunit;

namespace ExGrid.Release.Tests;

/// <summary>
/// The repository's own files, as the release check will read them at a tag: the record index of the
/// rows checked by hand held to the Definition of Done's table (or its proposal, until the table lands),
/// every path they name present, and the corpus readable. A drift here would otherwise first show on a
/// tag, after the sign-off.
/// </summary>
public class RepositoryTests
{
    private static readonly DirectoryTree Tree = new(RepositoryRoot());

    /// <summary>The rows ADR-0208 names as checked by hand.</summary>
    private static readonly string[] Adr0208Rows = ["VZ-14", "VZ-16", "FN-12e", "ED-30", "SH-42", "SH-51", "SH-52", "KB-3", "VZ-18", "§21.11"];

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

    [Fact] // ADR-0208: every row the decision names is indexed
    public void Every_row_ADR_0208_names_is_indexed()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        Assert.Equal(Adr0208Rows.Order(StringComparer.Ordinal), index.Select(r => r.Row).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0208: a path that names nothing is a list nobody reviewed when its code moved
    public void Every_path_and_record_the_index_names_is_in_the_repository()
    {
        var index = ByHand.ReadIndex(Tree, new Report())!;

        var missing = index
            .SelectMany(row => row.Paths.Select(path => (row, path)))
            .Where(p => p.path.EndsWith('/') ? !Tree.IsDirectory(p.path) : !Tree.IsFile(p.path))
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
        Assert.Contains("long: ${{ needs.admit.outputs.long == 'true' }}", release);
        Assert.Contains("if: needs.admit.outputs.site == 'true'", release);
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ExGrid.slnx"))) return dir.FullName;
        }
        throw new InvalidOperationException($"No ExGrid.slnx above {AppContext.BaseDirectory}.");
    }
}
