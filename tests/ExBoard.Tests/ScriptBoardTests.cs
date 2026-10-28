using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bunit;
using ExChart.Engine;
using ExGrid;
using ExGrid.Testing;
using ExGrid.Data;
using ExGrid.Data.Arrow;
using ExPivot.Engine;
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

    /// <summary>A Board whose input carries every kind a link's condition writes, for the questions to its source.</summary>
    private static readonly BoardReading Held = BoardSpec.Read("""
        {"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "day", "type": "date"}, {"name": "pnl", "type": "decimal"}, {"name": "count", "type": "integer"}]}},
         "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0.00"}]}}],
         "layout": [{"views": ["blotter"]}],
         "outputs": {"chosen": {"from": "blotter", "on": ["desk", "day"], "confirm": "Use these"}}}
        """);

    private static BoardSource SourceOf(Func<BoardQuery, CancellationToken, ValueTask<ShapedPage>>? fetch = null,
        Func<BoardChartQuery, CancellationToken, ValueTask<ChartSourceAnswer>>? thin = null)
        => new(_ => ValueTask.FromResult(Held.Input("trades")!.Shape), fetch ?? ((_, _) => throw new InvalidOperationException("Not asked here."))) { Thin = thin };

    private IRenderedComponent<ScriptBoard> DrawHeld(IReadOnlyDictionary<string, BoardSource> sources, Action<ComponentParameterCollectionBuilder<ScriptBoard>>? more = null)
        => Render<ScriptBoard>(ps =>
        {
            ps.Add(p => p.Board, Held);
            ps.Add(p => p.Data, new Dictionary<string, Snapshot>());
            ps.Add(p => p.Sources, sources);
            more?.Invoke(ps);
        });

    private static string SourcesSent(object? json) => Parsed(json).GetProperty("sources").GetRawText();

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
            source = (int?)null,
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

    [Fact] // ADR-0343, ADR-0370 section 1: a source stays in .NET and crosses as a number, the same while a message names it for its input, and a new one once dropped
    public async Task ADR0343_a_source_crosses_as_a_number_and_is_let_go_when_the_browser_says()
    {
        var first = SourceOf();
        var second = SourceOf(thin: (_, _) => throw new InvalidOperationException("Not asked here."));
        var cut = DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = first });
        Assert.Equal("""{"trades":{"number":1,"thin":false,"under":false,"pivot":null}}""", SourcesSent(Assert.Single(_module.Invocations["attach"]).Arguments[2]));

        cut.Render(ps => ps.Add(p => p.Sources, new Dictionary<string, BoardSource> { ["trades"] = first }));
        cut.Render(ps => ps.Add(p => p.Sources, new Dictionary<string, BoardSource> { ["trades"] = second }));
        await cut.InvokeAsync(() => Bridge().Value.ReleaseSources([1]));
        // Dropped by the message before, it is numbered anew, so a number the browser let go is never named again.
        cut.Render(ps => ps.Add(p => p.Sources, new Dictionary<string, BoardSource> { ["trades"] = first }));

        Assert.Equal(["""{"trades":{"number":1,"thin":false,"under":false,"pivot":null}}""", """{"trades":{"number":2,"thin":true,"under":false,"pivot":null}}""", """{"trades":{"number":3,"thin":false,"under":false,"pivot":null}}"""],
            _view.Invocations["update"].Select(update => SourcesSent(update.Arguments[0])));
        // A number the browser let go is never asked again: one asked is the bridge's defect, said.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Bridge().Value.Shape(1, 1));
    }

    [Fact] // ADR-0341, ADR-0343: the browser asks the source its shape, answered as a Board declares its columns
    public async Task ADR0341_a_sources_shape_is_answered_as_a_declaration()
    {
        DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = SourceOf() });

        Assert.Equal("""{"columns":[{"name":"desk","type":"text"},{"name":"day","type":"date"},{"name":"pnl","type":"decimal"},{"name":"count","type":"integer"}]}""",
            await Bridge().Value.Shape(1, 1));
    }

    [Fact] // ADR-0343, ADR-0065: a fetch reaches the source with the grid's query and the links' conditions, each value of its column's kind, and its page crosses back as Arrow
    public async Task ADR0343_a_fetch_reaches_the_source_with_its_narrowing_and_its_page_crosses_as_Arrow()
    {
        BoardQuery? asked = null;
        var rows = Held.Input("trades")!.FromJson("""[{"desk": "FX", "day": "2026-10-16", "pnl": 12.50, "count": 3}]""");
        var source = SourceOf((query, _) =>
        {
            asked = query;
            return ValueTask.FromResult(new ShapedPage(rows, 4, 9));
        });
        DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = source });

        var question = """{"view":"blotter","grid":""" + JsonSerializer.Serialize("""{"version":1,"range":{"start":0,"count":50},"sorts":[]}""")
            + ""","narrowing":[{"on":["day","pnl","count"],"values":[["2026-10-16T09:30:00",12345678901234567890.25,9007199254740993]]}]}""";
        var page = await Bridge().Value.Fetch(1, 7, question);

        Assert.NotNull(asked);
        Assert.Equal(("blotter", 0, 50), (asked.View, asked.Grid.Range.Start, asked.Grid.Range.Count));
        var condition = Assert.Single(asked.Narrowing);
        Assert.Equal(["day", "pnl", "count"], condition.On);
        Assert.Equal([new object?[] { new DateTime(2026, 10, 16, 9, 30, 0), 12345678901234567890.25m, 9007199254740993L }], condition.Values.Select(v => v.ToArray()));
        Assert.Equal((4, 9), (page.Start, page.TotalCount));
        var crossed = await SnapshotArrow.ReadAsync(new MemoryStream(page.Rows), cancellationToken: Xunit.TestContext.Current.CancellationToken);
        Assert.Equal((1, 12.50m), (crossed.RowCount, crossed.ValueAt(crossed.Rows[0], crossed["pnl"])));
    }

    [Fact] // ADR-0005 (as amended 2026-10-28), ADR-0370 section 1: a question too long for one message of a Blazor Server circuit is taken as a stream, read whole, in its turn
    public async Task ADR0005_a_question_too_long_for_one_message_is_taken_in_its_turn()
    {
        var take = _view.Setup<IJSStreamReference>("take", _ => true);
        var asked = new List<int>();
        var rows = Held.Input("trades")!.FromJson("[]");
        var cut = DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = SourceOf((query, _) => { asked.Add(query.Narrowing.Count == 0 ? 0 : query.Narrowing[0].Values.Count); return ValueTask.FromResult(new ShapedPage(rows, 0, 0)); }) });

        var grid = JsonSerializer.Serialize("""{"version":1,"range":{"start":0,"count":50},"sorts":[]}""");
        var desks = string.Join(",", Enumerable.Range(0, 3000).Select(i => $"""["Desk {i}"]"""));
        var question = """{"view":"blotter","grid":""" + grid + ""","narrowing":[{"on":["desk"],"values":[""" + desks + "]}]}";
        var arguments = JsonSerializer.Serialize(new object[] { 1, 8, question });
        using var crossed = JsonDocument.Parse($$"""{"held":4,"bytes":{{Encoding.UTF8.GetByteCount(arguments)}}}""");

        Task large = Task.CompletedTask, after = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            large = Bridge().Value.Held("Fetch", crossed.RootElement);
            after = Bridge().Value.Fetch(1, 9, """{"view":"blotter","grid":""" + grid + ""","narrowing":[]}""");
        });
        // The question after it waits for the held one to be taken, rather than reaching the source first.
        Assert.Equal([4], Assert.Single(take.Invocations).Arguments);
        Assert.Empty(asked);

        take.SetResult(new TextStream(arguments));
        await large.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await after.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Equal([3000, 0], asked);
    }

    [Fact] // ADR-0343, spine rule 6: a new Board numbers each source anew, and a question under the old number is read as the Board that numbered it declares the input
    public async Task ADR0343_a_question_is_read_as_the_Board_that_numbered_its_source_declares_the_input()
    {
        var asked = new List<BoardQuery>();
        var rows = Held.Input("trades")!.FromJson("[]");
        var source = SourceOf((query, _) => { asked.Add(query); return ValueTask.FromResult(new ShapedPage(rows, 0, 0)); });
        var cut = DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = source });

        // The same source under a Board that declares 'count' as a decimal.
        var redeclared = BoardSpec.Read(BoardSpec.Write(Held).Replace("""{"name":"count","type":"integer"}""", """{"name":"count","type":"decimal"}""", StringComparison.Ordinal));
        cut.Render(ps => ps.Add(p => p.Board, redeclared));
        Assert.Equal("""{"trades":{"number":2,"thin":false,"under":false,"pivot":null}}""", SourcesSent(Assert.Single(_view.Invocations["update"]).Arguments[0]));

        var question = """{"view":"blotter","grid":""" + JsonSerializer.Serialize("""{"version":1,"range":{"start":0,"count":50},"sorts":[]}""")
            + ""","narrowing":[{"on":["count"],"values":[[3]]}]}""";
        await Bridge().Value.Fetch(1, 10, question);
        await Bridge().Value.Fetch(2, 11, question);

        Assert.Equal([3L, 3m], asked.Select(query => Assert.Single(Assert.Single(query.Narrowing).Values)[0]));
    }

    // A held call's arguments as the browser hands .NET them, uncompressed, as a browser without CompressionStream does.
    private sealed class TextStream(string text) : IJSStreamReference
    {
        private readonly byte[] _bytes = Encoding.UTF8.GetBytes(text);

        public long Length => _bytes.Length;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Stream>(new MemoryStream(_bytes));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact] // ADR-0344: a chart's points cross as rows of its columns, each value exact, with what each thinned pixel held
    public async Task ADR0344_a_charts_points_cross_exact()
    {
        BoardChartQuery? asked = null;
        var source = SourceOf(thin: (query, _) =>
        {
            asked = query;
            return ValueTask.FromResult(new ChartSourceAnswer(12, ChartData.Of(("day", new List<DateTime> { new(2026, 10, 16) }), ("pnl", new List<decimal> { 12345678901234567890.25m })),
                [new PixelSummary(3, 4, 1.5, 2, 6)]));
        });
        DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = source });

        var answer = await Bridge().Value.Thin(1, 1, """{"view":"daily","chart":{"data":"trades","x":"day","y":"pnl","values":"dates","from":null,"to":20000,"pixels":300},"narrowing":[]}""");

        Assert.NotNull(asked);
        Assert.Equal(new ChartSourceQuery("trades", "day", "pnl", AxisValues.Dates, null, 20000, 300), asked.Chart);
        Assert.Equal("""{"version":"12","points":"[{\u0022day\u0022:\u00222026-10-16\u0022,\u0022pnl\u0022:12345678901234567890.25}]","thinned":[[3,4,1.5,2,6]]}""", answer);
    }

    [Fact] // ADR-0370 section 1: a question the browser no longer waits for is cancelled at the source
    public async Task ADR0370_a_question_the_browser_gave_up_is_cancelled_at_the_source()
    {
        var cancelled = new TaskCompletionSource();
        var source = SourceOf(async (_, token) =>
        {
            token.Register(() => cancelled.SetResult());
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Never answered.");
        });
        DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = source });

        var fetching = Bridge().Value.Fetch(1, 9, """{"view":"blotter","grid":""" + JsonSerializer.Serialize("""{"version":1,"range":{"start":0,"count":50},"sorts":[]}""") + ""","narrowing":[]}""");
        await Bridge().Value.Cancel(9);

        await cancelled.Task;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetching);
    }

    [Fact] // ADR-0005 (as amended 2026-10-28), spine rule 6: an output too large for a circuit's message is taken in its turn, read against its Snapshot before the release the browser sent after it
    public async Task ADR0005_a_held_output_is_read_against_its_Snapshot_before_the_release_after_it()
    {
        var take = _view.Setup<IJSStreamReference>("take", _ => true);
        var trades = Trades("""[{"desk": "FX", "day": "2026-10-20"}]""");
        BoardOutput? taken = null;
        var cut = Draw(new Dictionary<string, Snapshot> { ["trades"] = trades }, ps => ps.Add(p => p.OnOutput, (BoardOutput output) => taken = output));
        // The browser still holds Snapshot 1 when it raises the output; .NET names another since.
        cut.Render(ps => ps.Add(p => p.Data, new Dictionary<string, Snapshot> { ["trades"] = Trades("[]") }));

        var arguments = JsonSerializer.Serialize(new object[]
        {
            "output", new { output = """{"output":"chosen","input":"trades","version":0,"on":["desk"],"values":[["FX"]]}""", snapshot = 1, source = (int?)null },
        });
        using var crossed = JsonDocument.Parse($$"""{"held":2,"bytes":{{Encoding.UTF8.GetByteCount(arguments)}}}""");
        Task held = Task.CompletedTask, released = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            held = Bridge().Value.Held("Heard", crossed.RootElement);
            released = Bridge().Value.Release([1]);
        });
        Assert.False(released.IsCompleted);

        take.SetResult(new TextStream(arguments));
        await held.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await released.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.NotNull(taken);
        Assert.Equal(1, taken.Take(trades).RowCount);
    }

    [Fact] // ADR-0340 Q6, ADR-0343: an output taken from rows a source held crosses with no Snapshot, is read as the input declares its columns, and vouches for its values by the version the source answered them at
    public async Task ADR0343_an_output_taken_from_a_sources_rows_is_read_as_declared_and_held_to_its_version()
    {
        BoardOutput? taken = null;
        var cut = DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = SourceOf() }, ps => ps.Add(p => p.OnOutput, (BoardOutput output) => taken = output));

        var payload = Parsed(JsonSerializer.Serialize(new
        {
            output = """{"output":"chosen","input":"trades","version":7,"on":["desk","day"],"values":[["FX","2026-10-20"]]}""",
            snapshot = (int?)null,
            source = 1,
        }));
        await cut.InvokeAsync(() => Bridge().Value.Heard("output", payload));

        Assert.NotNull(taken);
        Assert.Equal(("chosen", "trades", 7L), (taken.Name, taken.Input, taken.SourceVersion));
        Assert.Equal([new object?[] { "FX", new DateTime(2026, 10, 20) }], taken.Condition.Values.Select(v => v.ToArray()));
        Assert.Equal(1, taken.Take(HeldAt(7)).RowCount);
        // No Snapshot of .NET's vouches for it: data at another version is refused, as for an output taken from a fetched page.
        Assert.Throws<BoardRefusedException>(() => taken.Take(HeldAt(8)));
    }

    // The source's data at a version, as a server holds it.
    private static Snapshot HeldAt(long version)
    {
        var builder = new SnapshotColumnsBuilder { Version = version };
        var desk = builder.Text("desk");
        var day = builder.Date("day");
        var pnl = builder.Decimal("pnl");
        var count = builder.Integer("count");
        foreach (var (d, n) in new[] { ("Rates", 19), ("FX", 20) })
        {
            desk.Append(d);
            day.Append(new DateOnly(2026, 10, n));
            pnl.Append(1m);
            count.Append(1);
        }
        return builder.Build();
    }

    [Fact] // ADR-0343, ADR-0370 sections 1 and 3: a pivot over a source reads the Pivot Source it makes through .NET, the one over every row declaring its fields, and one made anew for each other narrowing
    public async Task ADR0370_a_pivot_over_a_source_reads_the_Pivot_Source_it_makes_through_NET()
    {
        var board = BoardSpec.Read("""
            {"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}]}},
             "views": [{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}],
             "layout": [{"views": ["byDesk"]}]}
            """);
        var rows = board.Input("trades")!.FromJson("""[{"desk": "FX", "pnl": 1}, {"desk": "Rates", "pnl": 2}]""");
        var made = new List<IReadOnlyList<LinkCondition>>();
        var pivots = new List<SnapshotPivotSource>();
        var source = new BoardSource(_ => ValueTask.FromResult(board.Input("trades")!.Shape), (_, _) => throw new InvalidOperationException("Not asked here."))
        {
            Pivot = narrowing =>
            {
                made.Add(narrowing);
                pivots.Add(PivotSource.From(Narrowing.Copy(rows, Narrowing.Positions(rows, narrowing))));
                return pivots[^1];
            },
        };
        var cut = Render<ScriptBoard>(ps =>
        {
            ps.Add(p => p.Board, board);
            ps.Add(p => p.Data, new Dictionary<string, Snapshot>());
            ps.Add(p => p.Sources, new Dictionary<string, BoardSource> { ["trades"] = source });
        });
        var sent = Parsed(SourcesSent(Assert.Single(_module.Invocations["attach"]).Arguments[2])).GetProperty("trades").GetProperty("pivot");
        Assert.Equal(["desk", "pnl"], sent.GetProperty("fields").EnumerateArray().Select(field => field.GetProperty("name").GetString()));

        await cut.InvokeAsync(() => Bridge().Value.MakePivot(1, 1, "[]"));
        await cut.InvokeAsync(() => Bridge().Value.MakePivot(1, 2, """[{"on":["desk"],"values":[["FX"]]}]"""));
        // The one over every row, made to declare the fields, is the first pivot's; the other is made for its narrowing.
        Assert.Equal([0, 1], made.Select(narrowing => narrowing.Count));

        string Items(int pivot) => PivotJson.Write(new PivotItemsQuery("desk", pivots[pivot - 1].SourceVersion));
        var whole = PivotJson.ReadItemPage(await Bridge().Value.PivotItems(1, 1, Items(1)));
        var fx = PivotJson.ReadItemPage(await Bridge().Value.PivotItems(2, 2, Items(2)));
        Assert.Equal((2, 1), (whole.Total, fx.Total));
        // A pivot the browser let go is asked nothing more.
        await cut.InvokeAsync(() => Bridge().Value.ReleasePivot(2));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Bridge().Value.PivotItems(2, 3, Items(2)));
    }

    [Fact] // ADR-0370 section 3, spine rule 1: a Pivot Source made over a narrowing with other fields than the browser was told of is refused by name, and its pivot's questions with it
    public async Task ADR0370_a_Pivot_Source_unlike_the_one_declared_is_refused_by_name()
    {
        var board = BoardSpec.Read("""
            {"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}]}},
             "views": [{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}],
             "layout": [{"views": ["byDesk"]}]}
            """);
        var rows = board.Input("trades")!.FromJson("""[{"desk": "FX", "pnl": 1}]""");
        var source = new BoardSource(_ => ValueTask.FromResult(board.Input("trades")!.Shape), (_, _) => throw new InvalidOperationException("Not asked here."))
        {
            // Over a narrowing, a source that lost its desk field.
            Pivot = narrowing => narrowing.Count == 0 ? PivotSource.From(rows) : PivotSource.From(rows, [PivotSource.From(rows).Fields[1]]),
        };
        var cut = Render<ScriptBoard>(ps =>
        {
            ps.Add(p => p.Board, board);
            ps.Add(p => p.Data, new Dictionary<string, Snapshot>());
            ps.Add(p => p.Sources, new Dictionary<string, BoardSource> { ["trades"] = source });
        });

        await cut.InvokeAsync(() => Bridge().Value.MakePivot(1, 1, """[{"on":["desk"],"values":[["FX"]]}]"""));

        var failed = await Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.StartsWith("The source of input 'trades' made a Pivot Source over a narrowing whose fields", failed.Message, StringComparison.Ordinal);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => Bridge().Value.PivotItemCounts(1, 1, null));
        Assert.StartsWith("The source made no Pivot Source for this pivot", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0345, ADR-0370 section 1: a link from a pivot over a source asks it for the values under the cells, each path and value read exactly, and the answer crosses back as the input declares its columns
    public async Task ADR0345_the_values_under_a_pivots_cells_are_asked_of_the_source_through_NET()
    {
        BoardCellsQuery? asked = null;
        var source = SourceOf();
        source = new BoardSource(_ => ValueTask.FromResult(Held.Input("trades")!.Shape), source.Fetch)
        {
            Under = (query, _) =>
            {
                asked = query;
                return ValueTask.FromResult(new LinkCondition(["desk", "pnl"], [["FX", 12345678901234567890.25m], ["Rates", null]]));
            },
        };
        DrawHeld(new Dictionary<string, BoardSource> { ["trades"] = source });
        var layout = PivotLayout.Empty with { Rows = [new PivotFieldPlacement("desk") { HiddenItems = [PivotItemKey.Text("Credit")] }] };
        var cell = PivotJson.Write(new PivotDetailsQuery("", rowItems: [new PivotFieldItem("desk", PivotItemKey.Text("FX"))], columnItems: [new PivotFieldItem("day", PivotItemKey.Date(new DateTime(2026, 10, 16)))]));
        var question = JsonSerializer.Serialize(new { view = "byDesk", layout = PivotLayoutJson.Write(layout), cells = new[] { cell }, on = new[] { "desk", "pnl" }, narrowing = Array.Empty<object>() });

        var answer = await Bridge().Value.Under(1, 4, question);

        Assert.NotNull(asked);
        Assert.Equal("byDesk", asked.View);
        Assert.Equal(["desk", "pnl"], asked.On);
        Assert.Equal([PivotItemKey.Text("Credit")], Assert.Single(asked.Layout.Rows).HiddenItems);
        var items = Assert.Single(asked.Cells);
        Assert.Equal([new PivotFieldItem("desk", PivotItemKey.Text("FX"))], items.Row);
        Assert.Equal([new PivotFieldItem("day", PivotItemKey.Date(new DateTime(2026, 10, 16)))], items.Column);
        Assert.Equal("""[{"desk":"FX","pnl":12345678901234567890.25},{"desk":"Rates","pnl":null}]""", answer);
    }
}
