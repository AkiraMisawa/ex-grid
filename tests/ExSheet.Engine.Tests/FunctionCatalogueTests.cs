using System.Text.RegularExpressions;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047: <c>docs/specs/exsheet-functions/spec.md</c> catalogues the declared set and the queue
/// after it. Its <b>Supported</b> rows are read here and must be exactly
/// <see cref="DeclaredFunction.All"/>, so the catalogue cannot drift from the engine.
/// </summary>
public class FunctionCatalogueTests
{
    private static readonly Regex Row = new(
        @"^\| `(?<name>[A-Z][A-Z0-9.]*)` \| (?<status>[A-Za-z]+) \| (?<priority>P[123]|—) \|",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly string[] Statuses = ["Supported", "Ready", "Observe", "Decide"];

    private static IReadOnlyList<(string Name, string Status, string Priority)> Catalogue()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        var file = Path.Combine(directory!.FullName, "docs", "specs", "exsheet-functions", "spec.md");
        Assert.True(File.Exists(file), $"no catalogue at {file}");
        return [.. Row.Matches(File.ReadAllText(file))
            .Select(m => (m.Groups["name"].Value, m.Groups["status"].Value, m.Groups["priority"].Value))];
    }

    [Fact] // ADR-0047: the catalogue's Supported rows are exactly the declared set
    public void The_catalogues_supported_rows_are_the_declared_set() =>
        Assert.Equal(
            DeclaredFunction.All.Select(f => f.Name).Order(StringComparer.Ordinal),
            Catalogue().Where(r => r.Status == "Supported").Select(r => r.Name).Order(StringComparer.Ordinal));

    [Fact] // ADR-0047: each function is catalogued once, with a known status, and only an unsupported one has a priority
    public void Each_catalogued_function_has_one_row_a_status_and_a_priority()
    {
        var rows = Catalogue();
        Assert.NotEmpty(rows);
        Assert.Empty(rows.GroupBy(r => r.Name).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.All(rows, r =>
        {
            Assert.Contains(r.Status, Statuses);
            Assert.Equal(r.Status == "Supported", r.Priority == "—");
        });
    }
}
