using ExChart.Engine;
using ExGrid.Data;

namespace ExBoard.Tests;

public sealed class BoardChartSpecTests
{
    private const string Spec = """
        {"version": 1, "title": "P&L", "axis": {"values": "dates"},
         "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "h-trades", "x": "day", "y": "pnl"}]}
        """;

    private static Snapshot Trades()
    {
        var builder = new SnapshotColumnsBuilder { Version = 3 };
        var day = builder.Date("day");
        var pnl = builder.Decimal("pnl");
        var desk = builder.Text("desk");
        foreach (var (d, on, p) in new[] { ("Rates", 19, 100m), ("FX", 20, -50m), ("Rates", 21, 75m) })
        {
            desk.Append(d);
            day.Append(new DateOnly(2026, 10, on));
            pnl.Append(p);
        }
        return builder.Build();
    }

    private static Dictionary<string, DataShape> Tables(Snapshot trades) => new()
    {
        ["h-trades"] = DataShape.Of(trades),
        ["h-limits"] = new DataShape([new DataShapeColumn("limit", SnapshotKind.Decimal)]),
    };

    [Fact] // ADR-0320 as rewritten by ADR-0340 (Q4): draw_chart's spec is a Board of one view, drawn as the tool draws it
    public void A_chart_spec_is_a_board_of_one_view()
    {
        var trades = Trades();
        var board = BoardSpec.OfChart(Spec, Tables(trades));

        var view = Assert.IsType<BoardChartView>(Assert.Single(board.Views));
        Assert.Equal(BoardSpec.ChartViewId, view.Id);
        // Only the handles the spec names are inputs, declared with their tables' shapes.
        Assert.Equal(["h-trades"], board.Inputs.Select(i => i.Name));
        Assert.Equal(["day", "pnl", "desk"], board.Input("h-trades")!.Columns.Select(c => c.Name));

        var bound = (BoundChartView)BoundBoard.Bind(board, new Dictionary<string, Snapshot> { ["h-trades"] = trades })[BoardSpec.ChartViewId];
        var answered = ChartSpec.Answer(Spec, h => h == "h-trades" ? new global::ExChart.SnapshotChartData(trades) : null);
        Assert.True(answered.Accepted);
        Assert.Equal(answered.Reading!.Definition.Series[0].Points, bound.Reading.Definition.Series[0].Points);
    }

    [Theory] // ADR-0320 / ADR-0340 Q4: refused as the tool refuses, with the spec's own path
    [InlineData("\"data\": \"h-trades\"", "\"data\": \"h-quotes\"", "series[0].data", "series[0].data \"h-quotes\" is not known")]
    [InlineData("\"y\": \"pnl\"", "\"y\": \"book\"", "series[0].y", "series[0].y: \"h-trades\" has no column \"book\"")]
    [InlineData("\"version\": 1,", "\"version\": 1, \"version\": 1,", "", "")]
    public void A_chart_spec_that_cannot_be_drawn_is_refused_by_its_own_path(string from, string to, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => BoardSpec.OfChart(Spec.Replace(from, to, StringComparison.Ordinal), Tables(Trades())));

        Assert.Equal(path, refused.Path);
        Assert.StartsWith(message, refused.Message, StringComparison.Ordinal);
    }
}
