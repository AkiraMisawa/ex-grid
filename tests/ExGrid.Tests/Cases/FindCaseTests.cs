using System.Text.Json;
using System.Text.RegularExpressions;
using ExGrid.Finding;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The reference Find step and Replace All's shared cases (<c>tests/cases/finding</c>, kinds <c>find</c> and
/// <c>replace</c>; ADR-0350 section 5), run against the C# core. A row is the texts of its cells, in the
/// file's columns: what a Find matches, the displayed text, or what a Replace matches, the entry. The Script
/// Core's runner is <c>src/npm/core/test/find.test.ts</c>.
/// </summary>
public partial class FindCaseTests
{
    private const string Area = "finding";

    public static TheoryData<string, string> FindCases() => SharedCases.Of(Area, "find");

    public static TheoryData<string, string> ReplaceCases() => SharedCases.Of(Area, "replace");

    [Theory, MemberData(nameof(FindCases))] // ADR-0350: a Find case holds on the C# core
    public void Find(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var columns = Strings(document.GetProperty("columns"));
        var rows = Rows(document.GetProperty("rows"));
        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            var found = GridFind.Step(rows, FindRequest(step.GetProperty("find"), columns), TextsOf(columns));
            var expected = step.GetProperty("is");
            var label = step.GetProperty("find").GetRawText();
            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.False(found.IsFound, $"{label}: expected nothing, got [{found.Row}, {found.Column}]");
                continue;
            }
            Assert.True(found.IsFound, $"{label}: expected {expected}, got nothing");
            Assert.Equal((expected[0].GetInt32(), expected[1].GetString()), (found.Row, found.Column));
        }
    }

    [Theory, MemberData(nameof(ReplaceCases))] // ADR-0350: a Replace All case holds on the C# core
    public void Replace(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var columns = Strings(document.GetProperty("columns"));
        var numberColumns = Strings(document.GetProperty("numberColumns"));
        var written = Rows(entry.TryGetProperty("rows", out var own) ? own : document.GetProperty("rows"));
        var rows = entry.TryGetProperty("repeat", out var repeat)
            ? [.. Enumerable.Range(0, repeat.GetInt32()).SelectMany(_ => written.Select(row => row.ToArray()))]
            : written;
        var readIn = entry.TryGetProperty("readIn", out var version) ? version.GetInt32() : 0;
        var request = ReplaceRequest(entry.GetProperty("replaceAll"), columns, 0);
        var inPlace = entry.TryGetProperty("writeInPlace", out var place) && place.GetBoolean();
        GridReplaceWrite<string[]> Write(string[] row, string column, string text)
        {
            if (inPlace)
                return GridReplaceWrite<string[]>.To(row);
            if (numberColumns.Contains(column) && !NumberText().IsMatch(text))
                return GridReplaceWrite<string[]>.Refuse("not a number");
            var at = Array.IndexOf(columns, column);
            if (at < 0)
                return GridReplaceWrite<string[]>.Refuse("no such column");
            var after = row.ToArray();
            after[at] = text;
            return GridReplaceWrite<string[]>.To(after);
        }
        GridReplaceAnswer<string[]> Plan() => GridReplace.Plan(rows, readIn, request, TextsOf(columns), Write);

        if (entry.TryGetProperty("throws", out var refusal))
        {
            SharedCases.Refused(() => Plan(), refusal.GetString()!, entry.TryGetProperty("saying", out var saying) ? saying.GetString() : null);
            return;
        }
        var expected = entry.TryGetProperty("is", out var @is)
            ? @is
            : throw new InvalidOperationException($"\"{name}\" says neither what it is nor what it throws.");
        var answer = Plan();
        var result = answer.Result;
        if (expected.TryGetProperty("version", out var answered))
            Assert.Equal(answered.GetInt32(), result.RowSequenceVersion);
        if (expected.TryGetProperty("refused", out var refused))
        {
            Assert.False(result.IsReplaced, "expected a refusal");
            Assert.Equal(
                [.. refused.EnumerateArray().Select(cell => (cell[0].GetInt32(), cell[1].GetString()!, cell[2].ValueKind == JsonValueKind.Null ? null : cell[2].GetString()))],
                result.RefusedCells.Select(cell => (cell.Row, cell.Column, cell.Message)).ToArray());
            Assert.Empty(answer.Changes);
        }
        if (expected.TryGetProperty("count", out var count))
        {
            Assert.True(result.IsReplaced, "expected cells replaced, got a refusal");
            Assert.Equal(count.GetInt32(), result.Count);
        }
        if (expected.TryGetProperty("changeCount", out var changeCount))
            Assert.Equal(changeCount.GetInt32(), answer.Changes.Count);
        if (expected.TryGetProperty("changes", out var changes))
        {
            Assert.Equal(
                [.. changes.EnumerateArray().Select(change => $"{change[0].GetInt32()}: {string.Join(" | ", Strings(change[1]))}")],
                answer.Changes.Select(change => $"{IndexOf(rows, change.Before)}: {string.Join(" | ", change.After)}").ToArray());
        }
    }

    /// <summary>A Find request as a case writes it: a key left out is off, the file's columns, or none.</summary>
    internal static GridFindRequest FindRequest(JsonElement written, IReadOnlyList<string> columns, int version = 0) => new()
    {
        Text = written.GetProperty("text").GetString()!,
        Backward = Flag(written, "backward"),
        MatchCase = Flag(written, "matchCase"),
        WholeCell = Flag(written, "wholeCell"),
        From = written.TryGetProperty("from", out var from) ? SharedCases.Cell(from) : null,
        Scope = written.TryGetProperty("scope", out var scope) ? SharedCases.Ranges(scope) : null,
        Columns = written.TryGetProperty("columns", out var own) ? Strings(own) : columns,
        RowSequenceVersion = version,
    };

    /// <summary>A Replace All as a case writes it: a key left out is off, the file's columns, every one of
    /// them Editable, none, or <c>version</c> as given.</summary>
    internal static GridReplaceRequest ReplaceRequest(JsonElement written, IReadOnlyList<string> columns, int version)
    {
        var named = written.TryGetProperty("columns", out var own) ? Strings(own) : columns;
        return new()
        {
            Text = written.GetProperty("text").GetString()!,
            Replacement = written.GetProperty("replacement").GetString()!,
            MatchCase = Flag(written, "matchCase"),
            WholeCell = Flag(written, "wholeCell"),
            Scope = written.TryGetProperty("scope", out var scope) ? SharedCases.Ranges(scope) : null,
            Columns = named,
            EditableColumns = written.TryGetProperty("editable", out var editable) ? Strings(editable) : columns,
            RowSequenceVersion = written.TryGetProperty("version", out var given) ? given.GetInt32() : version,
        };
    }

    /// <summary>What a case takes for a number: the pattern both runners read. It ends at <c>\z</c>, since
    /// .NET's <c>$</c> also matches before a final line feed and ECMAScript's does not.</summary>
    [GeneratedRegex("^-?[0-9]+(\\.[0-9]+)?\\z")]
    internal static partial Regex NumberText();

    private static bool Flag(JsonElement written, string name) =>
        written.TryGetProperty(name, out var flag) && flag.GetBoolean();

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];

    private static string[][] Rows(JsonElement array) => [.. array.EnumerateArray().Select(Strings)];

    private static Func<string, Func<string[], string>?> TextsOf(string[] columns) => name =>
    {
        var at = Array.IndexOf(columns, name);
        return at < 0 ? null : row => row[at];
    };

    private static int IndexOf(string[][] rows, string[] row) => Array.IndexOf(rows, row);
}
