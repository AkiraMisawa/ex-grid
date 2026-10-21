using Bunit;
using ExChart;
using ExChart.Engine;
using ExGrid;
using ExGrid.Data;
using ExGrid.Selection;
using ExPivot.Engine;
using BoardComponent = ExBoard.Components.ExBoard;

namespace ExBoard.Tests;

/// <summary>A Board over sources held where the data lives (ADR-0343).</summary>
public sealed class BoardSourceTests : BunitContext
{
    private const string Inputs = """
        "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}}
        """;

    private const string Blotter = """{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0"}]}}""";
    private const string Other = """{"id": "other", "grid": {"data": "trades", "columns": [{"column": "desk"}]}}""";
    private const string Pivot = """{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}""";
    private const string Chart = """{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}""";

    private static BoardReading Board(string views, string links = "", string outputs = "")
    {
        var ids = System.Text.RegularExpressions.Regex.Matches(views, "\"id\": (\"[A-Za-z]+\"), \"(grid|pivot|chart)\"").Select(m => m.Groups[1].Value);
        return BoardSpec.Read($$"""
            {"version": 1, {{Inputs}}, "views": [{{views}}], "layout": [{"views": [{{string.Join(", ", ids)}}]}],
             "links": [{{links}}]{{(outputs.Length == 0 ? "" : $", \"outputs\": {{{outputs}}}")}}}
            """);
    }

    private static readonly BoardInput Trades = new("trades", [new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Decimal), new("day", SnapshotKind.Date)]);

    /// <summary>A server holding the trades: it answers its shape, and each query under the narrowing it is handed.</summary>
    private sealed class Server
    {
        public Snapshot Held { get; } = Held7();

        private static Snapshot Held7()
        {
            var builder = new SnapshotColumnsBuilder { Version = 7 };
            var desk = builder.Text("desk");
            var pnl = builder.Decimal("pnl");
            var day = builder.Date("day");
            foreach (var (d, p, n) in new[] { ("Rates", 100m, 19), ("FX", 200m, 20), ("Rates", 300m, 21), ("Credit", 400m, 22) })
            {
                desk.Append(d);
                pnl.Append(p);
                day.Append(new DateOnly(2026, 10, n));
            }
            return builder.Build();
        }

        public DataShape Shape { get; set; } = DataShape.Of(Trades.FromJson("[]"));

        public List<BoardQuery> Asked { get; } = [];

        public List<IReadOnlyList<LinkCondition>> Reports { get; } = [];

        public int ShapeAsked { get; private set; }

        public List<BoardChartQuery> Charted { get; } = [];

        // Each chart query answered by a task the test may hold, in the order asked; null answers at once.
        public Func<BoardChartQuery, Task>? HoldChart { get; set; }

        public List<BoardCellsQuery> Cells { get; } = [];

        // Each cells query answered by a task the test may hold; null answers at once.
        public Func<BoardCellsQuery, Task>? HoldCells { get; set; }

        public BoardSource Source(bool reports = true, bool thins = true, bool under = true) => new(_ => { ShapeAsked++; return ValueTask.FromResult(Shape); }, (query, _) =>
        {
            Asked.Add(query);
            var through = Narrowing.Positions(Held, query.Narrowing);
            var start = Math.Min(query.Grid.Range.Start, through.Count);
            var count = Math.Min(query.Grid.Range.Count, through.Count - start);
            return ValueTask.FromResult(new ShapedPage(Narrowing.Copy(Held, through.GetRange(start, count)), start, through.Count));
        })
        {
            Report = reports ? narrowing =>
            {
                Reports.Add(narrowing);
                return PivotReportSource.From(PivotSource.From(Narrowing.Copy(Held, Narrowing.Positions(Held, narrowing))));
            } : null,
            Thin = thins ? async (query, _) =>
            {
                Charted.Add(query);
                if (HoldChart is { } hold)
                    await hold(query);
                var through = Narrowing.Copy(Held, Narrowing.Positions(Held, query.Narrowing));
                return ChartSource.Thin(new SnapshotChartData(through), query.Chart, Held.Version);
            } : null,
            Under = under ? async (query, _) =>
            {
                Cells.Add(query);
                if (HoldCells is { } hold)
                    await hold(query);
                return BoardSource.ValuesUnder(Held, query);
            } : null,
        };
    }

    private static readonly Dictionary<string, Snapshot> NoData = [];

    private static Task<BoundBoard> Bind(BoardReading board, Server server, bool reports = true, bool thins = true, bool under = true) =>
        BoundBoard.BindAsync(board, NoData, new Dictionary<string, BoardSource> { ["trades"] = server.Source(reports, thins, under) }, cancellationToken: Xunit.TestContext.Current.CancellationToken);

    private static GridSelection Rows(int top, int bottom) =>
        GridSelection.Empty.Place(new SelectionRange(top, 0, bottom - top + 1, 1), new CellPosition(top, 0), new GridExtent(1_000_000, 2));

    [Fact] // ADR-0343 with ADR-0341: the source's shape is asked and checked before any row is fetched
    public async Task A_source_of_another_shape_is_refused_before_any_row_is_fetched()
    {
        var server = new Server { Shape = new DataShape([new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Double), new("day", SnapshotKind.Date)]) };

        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => Bind(Board(Blotter), server));

        Assert.Equal("inputs.trades", refused.Path);
        Assert.Contains("'pnl'", refused.Message, StringComparison.Ordinal);
        Assert.Equal(1, server.ShapeAsked);
        Assert.Empty(server.Asked);
    }

    [Fact] // ADR-0343: an input bound both to a Snapshot and to a source is refused, never one chosen
    public async Task An_input_bound_twice_is_refused()
    {
        var server = new Server();
        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => BoundBoard.BindAsync(Board(Blotter),
            new Dictionary<string, Snapshot> { ["trades"] = server.Held }, new Dictionary<string, BoardSource> { ["trades"] = server.Source() },
            cancellationToken: Xunit.TestContext.Current.CancellationToken));
        Assert.Equal("inputs.trades", refused.Path);
    }

    [Fact] // ADR-0343: a grid's rows are fetched, and a link hands its condition to the source to execute
    public async Task A_link_hands_the_source_its_condition_and_the_view_is_asked_again()
    {
        var server = new Server();
        var board = await Bind(Board($"{Blotter}, {Other}, {Pivot}", """{"from": "blotter", "to": ["other", "byDesk"], "on": ["desk"]}"""), server);
        var blotter = (BoundGridView)board["blotter"];
        var other = (BoundGridView)board["other"];
        Assert.Null(blotter.Grid);
        Assert.Equal(["Rates", "FX", "Rates", "Credit"], blotter.Source.Window.Select(r => (string)blotter.Columns[0].Value(r)!));
        Assert.All(server.Asked, q => Assert.Empty(q.Narrowing));
        Assert.Empty(Assert.Single(server.Reports));

        Assert.True(board.Select("blotter", Rows(0, 0)));

        var asked = server.Asked[^1];
        Assert.Equal("other", asked.View);
        Assert.Equal([["Rates"]], Assert.Single(asked.Narrowing).Values);
        Assert.Equal(["Rates", "Rates"], other.Source.Window.Select(r => (string)other.Columns[0].Value(r)!));
        var pivot = (BoundPivotView)board["byDesk"];
        Assert.Null(pivot.Source);
        Assert.Equal([["Rates"]], Assert.Single(server.Reports[^1]).Values);
        Assert.Same(pivot.ReportSource, pivot.ReportSource);

        // The selection let go, the narrowing goes with it, and the views are asked for the whole again.
        Assert.True(board.Select("blotter", GridSelection.Empty));
        Assert.Empty(server.Asked[^1].Narrowing);
        Assert.Empty(server.Reports[^1]);
        Assert.Equal(4, other.Source.Window.Count);
    }

    [Fact] // ADR-0343 / spine rule 1: a selection reaching rows not fetched narrows nothing, hands nothing on, and says so
    public async Task A_selection_past_the_rows_fetched_narrows_nothing_and_says_so()
    {
        var server = new Server();
        var board = await Bind(Board($"{Blotter}, {Other}", """{"from": "blotter", "to": ["other"], "on": ["desk"]}""",
            """ "review": {"from": "blotter", "on": ["desk"], "confirm": "Send"} """), server);
        var asked = server.Asked.Count;

        Assert.False(board.Select("blotter", Rows(2, 900)));

        Assert.Equal(asked, server.Asked.Count);
        Assert.False(board.CanConfirm("review"));
        Assert.Null(board.Confirm("review"));
        Assert.Equal("The selection reaches rows not fetched from the source, so it narrows nothing and hands nothing on.", ((BoundGridView)board["blotter"]).Unread);

        // A selection of rows fetched is read again, and what was said of the last one goes.
        Assert.True(board.Select("blotter", Rows(1, 1)));
        Assert.Null(((BoundGridView)board["blotter"]).Unread);
    }

    [Fact] // ADR-0343 with ADR-0340 Q6: an output from a fetched grid carries the version its rows were read at
    public async Task An_output_from_a_fetched_grid_carries_the_version_of_its_page()
    {
        var server = new Server();
        var board = await Bind(Board(Blotter, outputs: """ "review": {"from": "blotter", "on": ["desk"], "confirm": "Send"} """), server);

        board.Select("blotter", Rows(1, 2));
        var output = board.Confirm("review")!;

        Assert.Equal(7, output.SourceVersion);
        Assert.Equal([["FX"], ["Rates"]], output.Condition.Values);
        Assert.Equal(3, output.Take(server.Held).RowCount);
    }

    [Fact] // ADR-0345: a link from a pivot over a source that answers no values under cells is refused by path
    public async Task A_link_from_a_pivot_whose_source_answers_nothing_under_cells_is_refused()
    {
        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => Bind(Board($"{Pivot}, {Blotter}", """{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}"""), new Server(), under: false));
        Assert.Equal("links[0].from", refused.Path);
        Assert.Equal("Pivot 'byDesk' is over input 'trades', held on a server, where the rows under a cell are, and its source answers no values under cells (BoardSource.Under).", refused.Message);
    }

    private static PivotCellItems Desk(string desk) => new([new PivotFieldItem("desk", PivotItemKey.Text(desk))], []);

    [Fact] // ADR-0345: cells selected in a pivot over a server ask the source for the values under them, and narrow by its answer
    public async Task Cells_selected_in_a_server_pivot_ask_the_source_for_the_values_under_them()
    {
        var server = new Server();
        var board = await Bind(Board($"{Pivot}, {Blotter}", """{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}"""), server);
        var blotter = (BoundGridView)board["blotter"];

        Assert.True(await board.SelectCellsAsync("byDesk", [Desk("Rates")], Xunit.TestContext.Current.CancellationToken));

        var asked = Assert.Single(server.Cells);
        Assert.Equal("byDesk", asked.View);
        Assert.Equal(["desk"], asked.On);
        Assert.Same(((BoundPivotView)board["byDesk"]).Layout, asked.Layout);
        Assert.Empty(asked.Narrowing);
        Assert.Equal([["Rates"]], board.ConditionOf(board.Board.Links[0])!.Values);
        Assert.Equal([["Rates"]], Assert.Single(server.Asked[^1].Narrowing).Values);
        Assert.Equal(["Rates", "Rates"], blotter.Source.Window.Select(r => (string)blotter.Columns[0].Value(r)!));

        // A selection let go asks nothing, and takes the narrowing away.
        Assert.True(await board.SelectCellsAsync("byDesk", [], Xunit.TestContext.Current.CancellationToken));
        Assert.Single(server.Cells);
        Assert.Null(board.ConditionOf(board.Board.Links[0]));
    }

    [Fact] // ADR-0345: a pivot over a server cannot be read at once, so the synchronous selection refuses it
    public async Task The_synchronous_selection_refuses_a_server_pivot()
    {
        var board = await Bind(Board($"{Pivot}, {Blotter}", """{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}"""), new Server());
        Assert.Throws<InvalidOperationException>(() => board.SelectCells("byDesk", [Desk("FX")]));
    }

    [Fact] // ADR-0345 / spine rule 6: the answer for a selection a later one replaced narrows nothing, whichever comes first
    public async Task An_answer_for_a_replaced_selection_narrows_nothing()
    {
        var server = new Server();
        var board = await Bind(Board($"{Pivot}, {Blotter}", """{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}"""), server);
        var held = new List<TaskCompletionSource>();
        server.HoldCells = _ =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            held.Add(gate);
            return gate.Task;
        };

        var first = board.SelectCellsAsync("byDesk", [Desk("Rates")], Xunit.TestContext.Current.CancellationToken);
        var second = board.SelectCellsAsync("byDesk", [Desk("FX")], Xunit.TestContext.Current.CancellationToken);
        held[1].SetResult();
        Assert.True(await second);
        held[0].SetResult();
        Assert.False(await first);

        Assert.Equal([["FX"]], board.ConditionOf(board.Board.Links[0])!.Values);
    }

    [Fact] // ADR-0345: the reference implementation reads a subtotal's rows and leaves out the Items the layout hides
    public void The_reference_reads_a_total_and_leaves_hidden_items_out()
    {
        var server = new Server();
        var layout = PivotLayoutJson.Read("""{"version": 1, "rows": [{"field": "desk", "hiddenItems": [{"kind": "text", "value": "Credit"}]}], "values": [{"field": "pnl", "aggregation": "sum"}]}""");
        var total = new PivotCellItems([], []);

        var under = BoardSource.ValuesUnder(server.Held, new BoardCellsQuery("byDesk", layout, [total], ["desk"], []));

        Assert.Equal([["Rates"], ["FX"]], under.Values);
    }

    [Fact] // ADR-0344: a chart over a server input is read from the points its source thinned, asked at the Board's pixels
    public async Task A_chart_over_a_server_input_is_read_from_its_thinned_points()
    {
        var server = new Server();
        var board = await Bind(Board(Chart), server);

        var chart = (BoundChartView)board["curve"];
        Assert.True(chart.Thinned);
        Assert.Null(chart.Refusal);
        var query = Assert.Single(server.Charted);
        Assert.Equal(new ChartSourceQuery("trades", "day", "pnl", AxisValues.Dates, null, null, BoundBoard.ChartPixels), query.Chart);
        Assert.Equal("curve", query.View);
        Assert.Equal([100.0, 200.0, 300.0, 400.0], chart.Reading.Definition.Series.Single().Points.Select(p => p.Y!.Value.ToDouble()));
    }

    [Fact] // ADR-0344: a chart over a server input whose source thins nothing is refused by path
    public async Task A_chart_over_a_source_that_thins_nothing_is_refused()
    {
        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => Bind(Board(Chart), new Server(), thins: false));
        Assert.Equal("views[0].chart", refused.Path);
    }

    [Fact] // ADR-0344: what a thinned point cannot stand for is refused at binding, by the view's path
    public async Task A_chart_spec_a_source_cannot_draw_is_refused_by_path()
    {
        var observation = Chart.Replace("\"kind\": \"level\"", "\"kind\": \"observation\"", StringComparison.Ordinal);
        var server = new Server();
        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => Bind(Board(observation), server));
        Assert.Equal("views[0].chart.series[0].kind", refused.Path);
        Assert.Empty(server.Charted);
    }

    [Fact] // ADR-0344: a link's narrowing is handed to the chart's source, and the chart drawn from its answer
    public async Task A_link_narrows_a_server_chart_through_its_source()
    {
        var server = new Server();
        var board = await Bind(Board($"{Blotter}, {Chart}", """{"from": "blotter", "to": ["curve"], "on": ["desk"]}"""), server);
        var chart = (BoundChartView)board["curve"];
        var answered = 0;
        chart.Answered += () => answered++;

        Assert.True(board.Select("blotter", Rows(0, 0)));

        await WaitFor(() => answered == 1);
        Assert.Equal([["Rates"]], Assert.Single(server.Charted[^1].Narrowing).Values);
        Assert.Equal([100.0, 300.0], chart.Reading.Definition.Series.Single().Points.Select(p => p.Y!.Value.ToDouble()));
    }

    [Fact] // ADR-0344: a range the user narrows the chart to is asked of the source, so it is drawn at full detail
    public async Task A_range_narrowed_is_asked_of_the_source()
    {
        var server = new Server();
        var board = await Bind(Board(Chart), server);
        var chart = (BoundChartView)board["curve"];
        var answered = 0;
        chart.Answered += () => answered++;
        var from = new DateOnly(2026, 10, 20).DayNumber;

        chart.ChangeRange(new ChartRange(from, null));

        await WaitFor(() => answered == 1);
        Assert.Equal((double?)from, server.Charted[^1].Chart.From);
        Assert.Equal(3, chart.Reading.Definition.Series.Single().Points.Count);
        // The same range again asks nothing.
        chart.ChangeRange(new ChartRange(from, null));
        Assert.Equal(2, server.Charted.Count);
    }

    [Fact] // ADR-0344 / spine rule 6: an answer to an ask a later one replaced is never drawn, whichever comes first
    public async Task An_answer_overtaken_by_a_later_ask_is_dropped()
    {
        var server = new Server();
        var board = await Bind(Board(Chart), server);
        var chart = (BoundChartView)board["curve"];
        var held = new List<TaskCompletionSource>();
        server.HoldChart = _ =>
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            held.Add(gate);
            return gate.Task;
        };
        var answered = 0;
        chart.Answered += () => answered++;
        var day = new DateOnly(2026, 10, 21).DayNumber;   // the rows run from the 19th to the 22nd

        // The first ask is from the 20th, the second from the 21st; the second is answered first.
        chart.LayOver(new BoardChartChange(new SortedSet<string>(), new ChartRange(day - 1, null), null));
        var first = board.AskAsync(chart, Xunit.TestContext.Current.CancellationToken);
        chart.LayOver(new BoardChartChange(new SortedSet<string>(), new ChartRange(day, null), null));
        var second = board.AskAsync(chart, Xunit.TestContext.Current.CancellationToken);
        held[1].SetResult();
        await second;
        held[0].SetResult();
        await first;

        Assert.Equal(1, answered);
        Assert.Equal([300.0, 400.0], chart.Reading.Definition.Series.Single().Points.Select(p => p.Y!.Value.ToDouble()));
    }

    [Fact] // ADR-0344: a source that fails says so in the chart's place
    public async Task A_source_that_fails_says_so_in_the_charts_place()
    {
        var server = new Server();
        var board = await Bind(Board($"{Blotter}, {Chart}", """{"from": "blotter", "to": ["curve"], "on": ["desk"]}"""), server);
        var chart = (BoundChartView)board["curve"];
        server.HoldChart = _ => Task.FromException(new InvalidOperationException("the server is down"));
        var answered = 0;
        chart.Answered += () => answered++;

        board.Select("blotter", Rows(0, 0));

        await WaitFor(() => answered == 1);
        Assert.Equal("The source did not answer the chart: the server is down", chart.Refusal);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        // An answer is taken on the thread pool; the test waits for what it reads, never for a time.
        for (var i = 0; i < 500 && !condition(); i++)
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        Assert.True(condition());
    }

    [Fact] // ADR-0343: a pivot over a source that makes no report source is refused by path
    public async Task A_pivot_over_a_source_making_no_report_is_refused()
    {
        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => Bind(Board(Pivot), new Server(), reports: false));
        Assert.Equal("views[0].pivot", refused.Path);
    }

    [Fact] // ADR-0343: the component asks the sources first, then draws the views over them
    public async Task The_component_draws_a_board_over_a_source()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("WebAssembly", isInteractive: true));
        var server = new Server();
        var sources = new Dictionary<string, BoardSource> { ["trades"] = server.Source() };

        var cut = Render<BoardComponent>(p => p.Add(b => b.Board, Board($"{Blotter}, {Pivot}, {Chart}")).Add(b => b.Data, NoData).Add(b => b.Sources, sources));

        await cut.WaitForStateAsync(() => cut.FindAll(".ex-board-view").Count == 3, TimeSpan.FromSeconds(10));
        // The chart over the server input is drawn from its source's answer (ADR-0344).
        Assert.Single(cut.FindAll("[data-ex-view=curve] .ex-chart"));
        Assert.Single(server.Charted);
        Assert.Empty(cut.FindAll(".ex-board-binding"));
        Assert.Equal(1, server.ShapeAsked);
        Assert.NotEmpty(server.Asked);
        Assert.Single(server.Reports);
    }
}
