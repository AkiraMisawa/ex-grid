using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using ExGrid.Testing;
using ExGrid.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ScriptBoard = ExBoard.Script.ExBoard;

namespace ExBoard.Tests;

/// <summary>
/// ADR-0370 section 1, ADR-0065: Blazor's ExBoard over the Script Core. A Snapshot crosses to the browser once, as its
/// Arrow stream, and is named by its number after; an output the browser raises is read back against the very Snapshot
/// it was taken from; and a Diff the Board raised and the host handed back binds nothing again, while one of the host's
/// own does.
/// </summary>
public sealed class ScriptBoardTests : BunitContext
{
    private static readonly BoardReading Board = BoardSpec.Read("""
        {"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "day", "type": "date"}]}},
         "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "day"}]}}],
         "layout": [{"views": ["blotter"]}],
         "outputs": {"chosen": {"from": "blotter", "on": ["desk", "day"], "confirm": "Use these"}}}
        """);

    private static Snapshot Trades(string rows) => Board.Input("trades")!.FromJson(rows);

    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _view;

    public ScriptBoardTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExBoard.Script.ScriptCoreAsset.Stamp);
        _view = _module.SetupModule("attach", _ => true);
        _view.SetupVoid("update", _ => true).SetVoidResult();
    }

    private IRenderedComponent<ScriptBoard> Draw(IReadOnlyDictionary<string, Snapshot> data, Action<ComponentParameterCollectionBuilder<ScriptBoard>>? more = null)
        => Render<ScriptBoard>(ps =>
        {
            ps.Add(p => p.Board, Board);
            ps.Add(p => p.Data, data);
            more?.Invoke(ps);
        });

    private DotNetObjectReference<global::ExBoard.Script.ScriptBridge> Bridge()
        => Assert.IsType<DotNetObjectReference<global::ExBoard.Script.ScriptBridge>>(Assert.Single(_module.Invocations["attach"]).Arguments[1]);

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    private static (string Data, string Sent, int Streams) Crossed(IReadOnlyList<object?> arguments, int at)
    {
        var message = Parsed(arguments[at]);
        var data = message.TryGetProperty("data", out var numbers) ? numbers.GetRawText() : "";
        return (data, string.Join(",", message.GetProperty("sent").EnumerateArray().Select(n => n.GetInt32())), Assert.IsType<DotNetStreamReference[]>(arguments[at + 1]).Length);
    }

    [Fact] // ADR-0065, ADR-0370 section 1: a Snapshot crosses once, is named by its number after, and crosses again under a new number once a message dropped it
    public void ADR0065_a_Snapshot_crosses_once_and_is_named_by_its_number_after()
    {
        var first = Trades("""[{"desk": "Rates", "day": "2026-10-19"}]""");
        var second = Trades("""[{"desk": "FX", "day": "2026-10-20"}]""");
        var cut = Draw(new Dictionary<string, Snapshot> { ["trades"] = first });

        var attach = Assert.Single(_module.Invocations["attach"]).Arguments;
        Assert.Equal(("""{"trades":1}""", "1", 1), Crossed(attach, 2));
        var options = Parsed(attach[2]).GetProperty("options");
        Assert.Equal("invariant", options.GetProperty("locale").GetString());
        Assert.Empty(options.GetProperty("hears").EnumerateArray());

        // The same Snapshot in a new dictionary: named by its number, not sent again.
        cut.Render(ps => ps.Add(p => p.Data, new Dictionary<string, Snapshot> { ["trades"] = first }));
        cut.Render(ps => ps.Add(p => p.Data, new Dictionary<string, Snapshot> { ["trades"] = second }));
        // Dropped by the message before, it crosses again, under a number the browser never let go.
        cut.Render(ps => ps.Add(p => p.Data, new Dictionary<string, Snapshot> { ["trades"] = first }));

        var updates = _view.Invocations["update"].Select(update => Crossed(update.Arguments, 0)).ToArray();
        Assert.Equal([("""{"trades":1}""", "", 0), ("""{"trades":2}""", "2", 1), ("""{"trades":3}""", "3", 1)], updates);
    }

    [Fact] // ADR-0340 Q6, ADR-0370 section 1: an output the browser raises reaches OnOutput read against the Snapshot it was taken from, each value of its column's kind
    public async Task ADR0340_an_output_is_read_against_the_Snapshot_it_was_taken_from()
    {
        var trades = Trades("""[{"desk": "Rates", "day": "2026-10-19"}, {"desk": "FX", "day": "2026-10-20T09:30:00"}]""");
        BoardOutput? taken = null;
        var cut = Draw(new Dictionary<string, Snapshot> { ["trades"] = trades }, ps => ps.Add(p => p.OnOutput, (BoardOutput output) => taken = output));
        Assert.Equal(["output"], Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options").GetProperty("hears").EnumerateArray().Select(h => h.GetString()));

        var payload = Parsed(JsonSerializer.Serialize(new
        {
            output = """{"output":"chosen","input":"trades","version":0,"on":["desk","day"],"values":[["FX","2026-10-20T09:30:00"]]}""",
            snapshot = 1,
        }));
        await cut.InvokeAsync(() => Bridge().Value.Heard("output", payload));

        Assert.NotNull(taken);
        Assert.Equal(("chosen", "trades", 0L), (taken.Name, taken.Input, taken.SourceVersion));
        Assert.Equal([new object?[] { "FX", new DateTime(2026, 10, 20, 9, 30, 0) }], taken.Condition.Values.Select(v => v.ToArray()));
        Assert.Equal(1, taken.Take(trades).RowCount);
        // Taken against the very Snapshot it came from: at version 0, no other data vouches for it.
        Assert.Throws<BoardRefusedException>(() => taken.Take(Trades("""[{"desk": "FX", "day": "2026-10-20T09:30:00"}]""")));
    }

    [Fact] // ADR-0340 Q7: a Diff the Board raised and the host handed back binds nothing again; one of the host's own is sent
    public async Task ADR0340_a_Diff_handed_back_binds_nothing_again()
    {
        BoardDiff? raised = null;
        var data = new Dictionary<string, Snapshot> { ["trades"] = Trades("""[{"desk": "Rates", "day": "2026-10-19"}]""") };
        var cut = Draw(data, ps => ps.Add(p => p.OnDiff, (BoardDiff diff) => raised = diff));
        Assert.Equal(JsonValueKind.Null, Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("diff").ValueKind);

        var payload = Parsed(JsonSerializer.Serialize(new { diff = BoardDiff.Empty.ToJson(), diffsSent = 1 }));
        await cut.InvokeAsync(() => Bridge().Value.Heard("diff", payload));
        Assert.NotNull(raised);
        cut.Render(ps => ps.Add(p => p.Diff, raised));
        Assert.Empty(_view.Invocations["update"]);

        var own = BoardDiff.Read(BoardDiff.Empty.ToJson());
        cut.Render(ps => ps.Add(p => p.Diff, own));
        var update = Parsed(Assert.Single(_view.Invocations["update"]).Arguments[0]);
        Assert.Equal((BoardDiff.Empty.ToJson(), 2), (update.GetProperty("diff").GetString(), update.GetProperty("diffsSent").GetInt32()));
    }

    [Fact] // ADR-0123, ADR-0370 section 1, ADR-0411: the bundled module ExBoard.Script.ExBoard imports answers the stamp the component is built with, over the Shared Script ExGrid ships
    public void ADR0370_the_script_core_module_is_the_one_the_component_was_built_with()
    {
        const string module = "ex-board.min.js";
        var stamp = global::ExBoard.Script.ScriptCoreAsset.Stamp;
        var shipped = File.ReadAllText(ShippedFiles.PathOf("ExBoard", module));
        Assert.StartsWith($"/*! ex-board.js sha256:{stamp} ", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{stamp}\"", shipped, StringComparison.Ordinal);
        Assert.Equal(global::ExBoard.Script.ScriptCoreAsset.Version, ShippedFiles.Version("ExBoard", module));
        Assert.Contains($"sourceMappingURL={module}.map", shipped, StringComparison.Ordinal);
        var shared = Regex.Match(File.ReadAllText(ShippedFiles.PathOf("ExGrid", "ex-grid-script.min.js")), "^/\\*! ex-grid-script\\.js sha256:(?<stamp>[0-9a-f]{64}) ").Groups["stamp"].Value;
        Assert.Matches("^[0-9a-f]{64}$", shared);
        Assert.Contains($"from\"../ExGrid/ex-grid-script.min.js?v={shared[..12]}\"", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{shared}\"", shipped, StringComparison.Ordinal);
    }
}
