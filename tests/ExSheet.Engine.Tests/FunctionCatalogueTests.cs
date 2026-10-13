using System.Text.RegularExpressions;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0047: <c>docs/specs/exsheet-functions/spec.md</c> catalogues the declared set and the queue
/// after it. Its <b>Supported</b> rows are read here and must be exactly
/// <see cref="DeclaredFunction.All"/>, so the catalogue cannot drift from the engine. Every name it
/// lists, its rows and its list of Excel's functions not in the queue, is one of Excel's functions,
/// each of which <c>LetTests</c> binds as a <c>LET</c> name where its shape is a name's (ADR-0181,
/// run 17).
/// </summary>
public class FunctionCatalogueTests
{
    private static readonly Regex Row = new(
        @"^\| `(?<name>[A-Z][A-Z0-9.]*)` \| (?<status>[A-Za-z]+) \| (?<priority>P[123]|—) \|",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly string[] Statuses = ["Supported", "Ready", "Observe", "Decide", "Blocked"];

    /// <summary>The heading of the catalogue's list of Excel's functions that no row queues.</summary>
    private const string NotQueuedHeading = "## Excel's functions not in the queue";

    /// <summary>One letter's line of that list, which may wrap onto lines indented by two spaces: <c>- **A**: `ACCRINT`, `ACCRINTM`</c>.</summary>
    private static readonly Regex Letter = new(@"^- \*\*(?<letter>[A-Z])\*\*: (?<names>.+)$", RegexOptions.CultureInvariant);

    private static readonly Regex ListedName = new(@"^`(?<name>[A-Z][A-Z0-9.]*)`$", RegexOptions.CultureInvariant);

    private static string CatalogueText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.True(directory is not null, $"no ExGrid.slnx above {AppContext.BaseDirectory}");
        var file = Path.Combine(directory!.FullName, "docs", "specs", "exsheet-functions", "spec.md");
        Assert.True(File.Exists(file), $"no catalogue at {file}");
        return File.ReadAllText(file);
    }

    internal static IReadOnlyList<(string Name, string Status, string Priority)> Catalogue() =>
        [.. Row.Matches(CatalogueText())
            .Select(m => (m.Groups["name"].Value, m.Groups["status"].Value, m.Groups["priority"].Value))];

    /// <summary>
    /// The catalogue's list of Excel's functions not in the queue, in its order, each with the letter
    /// of the line it stands on. Any text on the list's lines that is not a name in backticks fails.
    /// </summary>
    internal static IReadOnlyList<(char Letter, string Name)> NotQueued()
    {
        var lines = CatalogueText().ReplaceLineEndings("\n").Split('\n');
        var start = Array.IndexOf(lines, NotQueuedHeading);
        Assert.True(start >= 0, $"the catalogue has no '{NotQueuedHeading}' heading");
        var listed = new List<(char, string)>();
        var letter = '\0';
        foreach (var line in lines.Skip(start + 1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal)))
        {
            string names;
            if (Letter.Match(line) is { Success: true } item)
            {
                letter = item.Groups["letter"].Value[0];
                names = item.Groups["names"].Value;
            }
            else if (letter != '\0' && line.StartsWith("  `", StringComparison.Ordinal))
            {
                names = line.Trim();
            }
            else
            {
                letter = '\0';
                continue;
            }
            foreach (var part in names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var name = ListedName.Match(part);
                Assert.True(name.Success, $"'{part}' on the line '{line}' is not a function's name in backticks");
                listed.Add((letter, name.Groups["name"].Value));
            }
        }
        Assert.NotEmpty(listed);
        return listed;
    }

    /// <summary>Every name the catalogue lists: its rows, then its list of Excel's functions not in the queue.</summary>
    internal static IReadOnlyList<string> ExcelFunctions() =>
        [.. Catalogue().Select(r => r.Name), .. NotQueued().Select(n => n.Name)];

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

    [Fact] // ADR-0047, ADR-0181: a function not in the queue is listed once, under its own letter, in order, and has no row
    public void Excels_functions_not_in_the_queue_are_listed_once_in_order_and_have_no_row()
    {
        var listed = NotQueued();
        var rows = Catalogue().Select(r => r.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(listed, n => Assert.Equal(n.Letter, n.Name[0]));
        Assert.Equal(listed.Select(n => n.Name).Order(StringComparer.Ordinal), listed.Select(n => n.Name));
        Assert.Empty(listed.GroupBy(n => n.Name).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.Empty(listed.Where(n => rows.Contains(n.Name)).Select(n => n.Name));
    }

    [Fact] // ADR-0047 (2026-10-13, decided with the user): the functions Excel has and ExSheet does not declare, whose call is a Refused Formula, are exactly the catalogue's rows that are not Supported and its list of the rest
    public void The_engines_list_of_excels_undeclared_functions_is_the_catalogues()
    {
        var catalogued = Catalogue().Where(r => r.Status != "Supported").Select(r => r.Name).Concat(NotQueued().Select(n => n.Name));

        Assert.Equal(catalogued.Order(StringComparer.Ordinal), Formulas.ExcelFunctionNames.Names.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(DeclaredFunction.All, f => Formulas.ExcelFunctionNames.Undeclared(f.Name));
    }
}
