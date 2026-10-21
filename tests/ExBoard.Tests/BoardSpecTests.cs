using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard.Tests;

public sealed class BoardSpecTests
{
    private const string Inputs = """
        "inputs": {"trades": {"columns": [
          {"name": "desk", "type": "text"}, {"name": "month", "type": "text"},
          {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}}
        """;

    private static string Board(string views, string layout, string inputs = Inputs) =>
        $$"""{"version": 1, "title": "Desk P&L", {{inputs}}, "views": [{{views}}], "layout": [{{layout}}]}""";

    private const string Grid = """{"id": "blotter", "title": "Trades", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0"}]}}""";
    private const string Pivot = """{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}""";
    private const string Chart = """{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}""";

    [Fact] // ADR-0340: a Board lays out views of the family in rows, over its declared inputs
    public void A_board_reads_its_inputs_views_and_layout()
    {
        var board = BoardSpec.Read(Board($"{Grid}, {Pivot}, {Chart}",
            """{"views": ["blotter", "byDesk"], "widths": [2, 1], "height": 360}, {"views": ["curve"]}"""));

        Assert.Equal("Desk P&L", board.Title);
        Assert.Equal(["trades"], board.Inputs.Select(i => i.Name));
        var grid = Assert.IsType<BoardGridView>(board.View("blotter"));
        Assert.Equal("Trades", grid.Title);
        Assert.Equal(["desk", "pnl"], grid.Grid.Columns.Select(c => c.Column));
        var pivot = Assert.IsType<BoardPivotView>(board.View("byDesk"));
        Assert.Equal("desk", pivot.Layout.Rows[0].Field);
        Assert.Equal(["trades"], Assert.IsType<BoardChartView>(board.View("curve")).Data);
        Assert.Equal([new BoardCell("blotter", 2), new BoardCell("byDesk", 1)], board.Layout[0].Cells);
        Assert.Equal(360, board.Layout[0].Height);
        Assert.Equal(BoardSpec.DefaultRowHeight, board.Layout[1].Height);
    }

    [Theory] // ADR-0340: refusal speaks as the Chart Spec's does, one sentence naming the path and the reason
    [InlineData("""{"title": "x"}""", "version", "A Board says its version; this reader reads version 1.")]
    [InlineData("""{"version": 2}""", "version", "The Board is version 2, and this reader reads version 1 only.")]
    [InlineData("""{"version": 1, "theme": "dark"}""", "theme", "The Board has no member 'theme'.")]
    [InlineData("""{"version": 2, "links": []}""", "version", "The Board is version 2, and this reader reads version 1 only.")]
    public void A_board_without_a_version_this_reader_reads_is_refused(string json, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => BoardSpec.Read(json));
        Assert.Equal(path, refused.Path);
        Assert.Equal(message, refused.Message);
    }

    public static TheoryData<string, string, string> Refusals => new()
    {
        { Board(Grid.Replace(", \"format\": \"#,##0\"", ""), """{"views": ["blotter"]}"""), "views[0].grid.columns[1].format", "Column 'pnl' is a decimal and needs a format" },
        { Board(Grid.Replace("\"data\": \"trades\"", "\"data\": \"orders\""), """{"views": ["blotter"]}"""), "views[0].grid.data", "The grid shows input 'orders', which the Board does not declare." },
        { Board(Pivot.Replace("\"field\": \"desk\"", "\"field\": \"book\""), """{"views": ["byDesk"]}"""), "views[0].pivot.layout", "The pivot places field 'book', which input 'trades' does not declare." },
        { Board(Pivot.Replace("\"version\": 1, ", ""), """{"views": ["byDesk"]}"""), "views[0].pivot.layout", "The pivot's layout cannot be read: A Pivot Layout names its version." },
        { Board(Chart.Replace("\"data\": \"trades\"", "\"data\": \"quotes\""), """{"views": ["curve"]}"""), "views[0].chart.series[0].data", "The chart reads input 'quotes', which the Board does not declare." },
        { Board("""{"id": "x"}""", """{"views": ["x"]}"""), "views[0]", "View 'x' is one of grid, pivot and chart." },
        { Board($"{Grid}, {Grid}", """{"views": ["blotter"]}"""), "views[1].id", "Two views have the id 'blotter'." },
        { Board($"{Grid}, {Pivot}", """{"views": ["blotter"]}"""), "layout", "The layout leaves out view 'byDesk'; every view stands in it once." },
        { Board(Grid, """{"views": ["blotter", "blotter"]}"""), "layout[0].views[1]", "The layout places view 'blotter' twice; a view stands in one place." },
        { Board(Grid, """{"views": ["chart"]}"""), "layout[0].views[0]", "The layout places view 'chart', which the Board does not have." },
        { Board(Grid, """{"views": ["blotter"], "widths": [1, 1]}"""), "layout[0].widths", "A row gives one width per view; this row has 1." },
        { Board(Grid, """{"views": ["blotter"], "x": 0, "y": 0}"""), "layout[0].x", "A row of the layout has no member 'x'." },
        { Board(Grid, """{"views": ["blotter"]}""", "\"inputs\": {\"trades\": {\"columns\": [{\"name\": \"desk\"}]}}"), "inputs.trades.columns[0].type", "Column 'desk' of input 'trades' declares no type" },
    };

    [Theory, MemberData(nameof(Refusals))] // ADR-0340/0341/0342: what cannot be drawn is refused by path
    public void A_board_that_cannot_be_drawn_is_refused_by_path(string json, string path, string message)
    {
        var refused = Assert.Throws<BoardRefusedException>(() => BoardSpec.Read(json));
        Assert.Equal(path, refused.Path);
        Assert.StartsWith(message, refused.Message, StringComparison.Ordinal);
    }
}
