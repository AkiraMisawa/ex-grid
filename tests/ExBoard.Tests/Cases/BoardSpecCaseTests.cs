using System.Text.Json.Nodes;
using ExGrid.Data;

namespace ExBoard.Tests.Cases;

/// <summary>
/// The shared Board cases (<c>tests/cases/board</c>, kind <c>boardSpec</c>; ADR-0340, ADR-0370 section 3), run against
/// ExBoard's reader: a Board read, held to what the case says it holds, or its refusal, path and words. The Script
/// Core's runner is <c>src/npm/board/test/board-spec-cases.test.ts</c>; <c>tests/cases/README.md</c> says what a file
/// holds.
/// </summary>
public class BoardSpecCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "board");

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Read(path);
            if ((string?)document["kind"] != "boardSpec")
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0340: a Board case holds on ExBoard's reader
    public void Board_spec(string file, string name)
    {
        var entry = Read(Path.Combine(Root, file))["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        BoardReading board;
        try
        {
            board = BoardSpec.Read((string)entry["read"]!);
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
        Assert.False(entry.ContainsKey("refused"), "Expected a refusal; the Board was read.");
        var held = Summary(board);
        var keys = entry.ContainsKey("expect") ? null : new[] { "links", "outputs" }.Where(entry.ContainsKey).ToArray();
        Assert.True(keys is null || keys.Length > 0, "The case says neither what it expects nor what it refuses.");
        foreach (var key in keys ?? [])
            Assert.True(JsonNode.DeepEquals(entry[key], held[key]), $"{key}: expected {entry[key]!.ToJsonString()}, read {held[key]!.ToJsonString()}.");
        if (keys is null)
            Assert.True(JsonNode.DeepEquals(entry["expect"], held), $"Expected {entry["expect"]!.ToJsonString()}, read {held.ToJsonString()}.");
    }

    private static JsonArray Names(IEnumerable<string> names) => new([.. names.Select(n => (JsonNode)n)]);

    /// <summary>What a Board holds, as tests/cases/README.md writes it under boardSpec.</summary>
    private static JsonObject Summary(BoardReading board) => new()
    {
        ["title"] = board.Title,
        ["inputs"] = new JsonArray([.. board.Inputs.Select(i => (JsonNode)new JsonObject
        {
            ["name"] = i.Name,
            ["columns"] = new JsonArray([.. i.Columns.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["type"] = BoardInput.NameOf(c.Kind) })]),
        })]),
        ["views"] = new JsonArray([.. board.Views.Select(View)]),
        ["layout"] = new JsonArray([.. board.Layout.Select(r => (JsonNode)new JsonObject
        {
            ["cells"] = new JsonArray([.. r.Cells.Select(c => (JsonNode)new JsonArray(c.View, c.Width))]),
            ["height"] = r.Height,
        })]),
        ["links"] = new JsonArray([.. board.Links.Select(l => (JsonNode)new JsonObject { ["from"] = l.From, ["to"] = Names(l.To), ["on"] = Names(l.On) })]),
        ["outputs"] = new JsonArray([.. board.Outputs.Select(o => (JsonNode)new JsonObject
        {
            ["name"] = o.Name, ["from"] = o.From, ["on"] = Names(o.On), ["confirm"] = o.Confirm,
        })]),
    };

    private static JsonNode View(BoardView view)
    {
        var shown = new JsonObject { ["id"] = view.Id, ["kind"] = BoardSpec.KindOf(view), ["title"] = view.Title, ["data"] = Names(view.Data) };
        switch (view)
        {
            case BoardGridView grid:
                shown["columns"] = new JsonArray([.. grid.Grid.Columns.Select(c => (JsonNode)new JsonObject
                {
                    ["column"] = c.Column, ["kind"] = BoardInput.NameOf(c.Kind), ["header"] = c.Header, ["format"] = c.Format,
                })]);
                break;
            case BoardPivotView pivot:
                shown["rows"] = Names(pivot.Layout.Rows.Select(f => f.Field));
                shown["columns"] = Names(pivot.Layout.Columns.Select(f => f.Field));
                shown["values"] = Names(pivot.Layout.Values.Select(f => f.Field));
                break;
        }
        return shown;
    }

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}
