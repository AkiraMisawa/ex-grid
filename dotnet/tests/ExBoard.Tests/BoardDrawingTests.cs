using Bunit;
using ExGrid.Data;

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
        Assert.Equal(3, grid.Grid!.Source.Window.Count);
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

    [Fact] // ADR-0341: a chart reads only what its input declares, as a grid and a pivot do
    public void A_chart_naming_a_column_its_input_does_not_declare_is_refused()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Text("desk").Append("Rates");
        builder.Decimal("pnl").Append(1m);
        builder.Date("day").Append(new DateOnly(2026, 10, 20));
        builder.Decimal("notional").Append(5m);
        var json = Json.Replace("\"y\": \"pnl\"", "\"y\": \"notional\"");

        var refused = Assert.Throws<BoardRefusedException>(() =>
            BoundBoard.Bind(BoardSpec.Read(json), new Dictionary<string, Snapshot> { ["trades"] = builder.Build() }));

        Assert.StartsWith("views[2].chart", refused.Path, StringComparison.Ordinal);
        Assert.Contains("notional", refused.Message, StringComparison.Ordinal);
    }
}
