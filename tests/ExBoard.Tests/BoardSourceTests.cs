using Bunit;
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

        public BoardSource Source(bool reports = true) => new(_ => { ShapeAsked++; return ValueTask.FromResult(Shape); }, (query, _) =>
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
        };
    }

    private static readonly Dictionary<string, Snapshot> NoData = [];

    private static Task<BoundBoard> Bind(BoardReading board, Server server, bool reports = true) =>
        BoundBoard.BindAsync(board, NoData, new Dictionary<string, BoardSource> { ["trades"] = server.Source(reports) }, cancellationToken: Xunit.TestContext.Current.CancellationToken);

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

    [Theory] // ADR-0343: what cannot be drawn over a source held on a server yet is refused by path
    [InlineData(Chart, "", "views[0].chart", "Input 'trades' is held on a server, and a chart over it waits for the Chart Source that thins where the data lives (ADR-0322).")]
    [InlineData(Pivot + ", " + Blotter, """{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}""", "links[0].from",
        "Pivot 'byDesk' is over input 'trades', held on a server, where the rows under a cell are; a link from it cannot be read in the client yet (ADR-0343).")]
    public async Task What_waits_on_a_later_decision_is_refused_by_path(string views, string links, string path, string message)
    {
        var refused = await Assert.ThrowsAsync<BoardRefusedException>(() => Bind(Board(views, links), new Server()));
        Assert.Equal(path, refused.Path);
        Assert.Equal(message, refused.Message);
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

        var cut = Render<BoardComponent>(p => p.Add(b => b.Board, Board($"{Blotter}, {Pivot}")).Add(b => b.Data, NoData).Add(b => b.Sources, sources));

        await cut.WaitForStateAsync(() => cut.FindAll(".ex-board-view").Count == 2, TimeSpan.FromSeconds(10));
        Assert.Empty(cut.FindAll(".ex-board-binding"));
        Assert.Equal(1, server.ShapeAsked);
        Assert.NotEmpty(server.Asked);
        Assert.Single(server.Reports);
    }
}
