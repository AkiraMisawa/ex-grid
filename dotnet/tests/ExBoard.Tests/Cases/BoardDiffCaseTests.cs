using System.Text.Json.Nodes;
using ExGrid.Data;

namespace ExBoard.Tests.Cases;

/// <summary>
/// The shared Board Diff cases (<c>tests/cases/board</c>, kind <c>boardDiff</c>; ADR-0340 Q7, ADR-0370 section 3), run
/// against ExBoard's <see cref="BoardDiff"/>: a Diff read and written again, or its refusal, path and words, and a Diff
/// laid against a Board, with what it dropped. The Script Core's runner is
/// <c>packages/board/test/board-diff-cases.test.ts</c>; <c>tests/cases/README.md</c> says what a file holds.
/// </summary>
public class BoardDiffCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "board");

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Read(path);
            if ((string?)document["kind"] != "boardDiff")
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0340 Q7: a Board Diff case holds on ExBoard's BoardDiff
    public void Board_diff(string file, string name)
    {
        var document = Read(Path.Combine(Root, file));
        var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        BoardDiff diff;
        try
        {
            diff = BoardDiff.Read((string)entry["read"]!);
        }
        catch (BoardRefusedException refused)
        {
            var expected = entry["refused"]?.AsObject() ?? throw new InvalidOperationException($"Refused unexpectedly at '{refused.Path}': {refused.Message}");
            Assert.Equal((string)expected["path"]!, refused.Path);
            if (expected["saying"] is { } saying)
                Assert.StartsWith(saying.GetValue<string>(), refused.Message, StringComparison.Ordinal);
            else
                Assert.Equal((string)expected["message"]!, refused.Message);
            return;
        }
        Assert.False(entry.ContainsKey("refused"), "Expected a refusal; the Diff was read.");
        if (entry["fitTo"] is { } board)
        {
            var fitted = diff.FitTo(BoardSpec.Read((string)document["boards"]![(string)board!]!));
            Assert.Equal(entry["dropped"]!.AsArray().Select(d => (string)d!), fitted.Dropped);
            diff = fitted.Diff;
        }
        Assert.Equal((string)entry["writes"]!, diff.ToJson());
    }

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}
