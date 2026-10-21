using Bunit;
using ExGrid.Data;
using BoardComponent = ExBoard.Components.ExBoard;

namespace ExBoard.Tests;

public sealed class BoardDrawingTests : BunitContext
{
    private const string Json = """
        {"version": 1, "title": "Desk P&L",
         "inputs": {"trades": {"columns": [
           {"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
         "views": [
           {"id": "blotter", "title": "Trades", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0.00"}]}},
           {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}},
           {"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}],
         "layout": [{"views": ["blotter", "byDesk"], "widths": [2, 1]}, {"views": ["curve"], "height": 300}]}
        """;

    private static readonly BoardInput Trades = new("trades",
        [new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Decimal), new("day", SnapshotKind.Date)]);

    private static Snapshot Data() => Trades.FromJson("""
        [{"desk": "Rates", "pnl": 1250.5, "day": "2026-10-19"},
         {"desk": "FX", "pnl": -300, "day": "2026-10-20"},
         {"desk": "Rates", "pnl": 99.25, "day": "2026-10-21"}]
        """);

    public BoardDrawingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("WebAssembly", isInteractive: true));
    }

    [Fact] // ADR-0340/0341: every input binds, and each view is made from its data
    public void A_board_binds_its_inputs_and_makes_each_view()
    {
        var bound = BoundBoard.Bind(BoardSpec.Read(Json), new Dictionary<string, Snapshot> { ["trades"] = Data() });

        var grid = Assert.IsType<BoundGridView>(bound["blotter"]);
        Assert.Equal(3, grid.Grid.Source.Window.Count);
        Assert.IsType<BoundPivotView>(bound["byDesk"]);
        var chart = Assert.IsType<BoundChartView>(bound["curve"]);
        Assert.Equal(3, chart.Reading.Definition.Series[0].Points.Count);
    }

    [Fact] // ADR-0341: an input with no data is refused by name
    public void An_input_bound_to_no_data_is_refused()
    {
        var refused = Assert.Throws<BoardRefusedException>(() => BoundBoard.Bind(BoardSpec.Read(Json), new Dictionary<string, Snapshot>()));
        Assert.Equal("inputs.trades", refused.Path);
        Assert.Equal("Input 'trades' is bound to no data.", refused.Message);
    }

    [Fact] // ADR-0341: a change upstream is caught on the run that meets it, not drawn
    public void A_board_whose_data_changed_shape_draws_only_the_refusal()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Text("desk").Append("Rates");
        builder.Double("pnl").Append(1.0);
        builder.Date("day").Append(new DateOnly(2026, 10, 20));

        var cut = Render<BoardComponent>(ps => ps
            .Add(b => b.Board, BoardSpec.Read(Json))
            .Add(b => b.Data, new Dictionary<string, Snapshot> { ["trades"] = builder.Build() }));

        Assert.Equal("trades: Input 'trades', column 'pnl': declared decimal, and its source holds double.",
            cut.Find(".ex-board-refusal").TextContent);
        Assert.Empty(cut.FindAll(".ex-board-view"));
    }

    [Fact] // ADR-0340: the layout is rows of views, each its share of the row, written in the Board
    public void A_board_draws_its_views_in_its_rows()
    {
        var cut = Render<BoardComponent>(ps => ps
            .Add(b => b.Board, BoardSpec.Read(Json))
            .Add(b => b.Data, new Dictionary<string, Snapshot> { ["trades"] = Data() }));

        var rows = cut.FindAll(".ex-board-row");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["blotter", "byDesk"], rows[0].QuerySelectorAll(".ex-board-view").Select(v => v.GetAttribute("data-ex-view")));
        Assert.Contains("flex: 2 1 0", rows[0].QuerySelectorAll(".ex-board-view")[0].GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("height: 300px", rows[1].GetAttribute("style"), StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[data-ex-view=blotter] .ex-grid"));
        Assert.NotNull(cut.Find("[data-ex-view=byDesk] .ex-grid"));
        Assert.NotNull(cut.Find("[data-ex-view=curve] svg"));
    }
}
