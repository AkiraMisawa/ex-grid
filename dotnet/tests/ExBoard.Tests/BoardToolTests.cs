using System.Text.Json;
using ExGrid.Data;

namespace ExBoard.Tests;

public sealed class BoardToolTests
{
    private const string Board = """
        {"version": 1, "title": "Desk P&L",
         "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "day", "type": "date"}, {"name": "pnl", "type": "decimal"}]}},
         "views": [
           {"id": "blotter", "title": "Trades", "grid": {"data": "trades", "columns": [{"column": "day", "header": "Day"}, {"column": "desk"}, {"column": "pnl", "header": "P&L", "format": "#,##0"}]}},
           {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}},
           {"id": "daily", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "trades", "x": "day", "y": "pnl"}]}}],
         "layout": [{"views": ["blotter", "byDesk"]}, {"views": ["daily"]}],
         "links": [{"from": "blotter", "to": ["byDesk", "daily"], "on": ["desk"]}],
         "outputs": {"review": {"from": "blotter", "on": ["day"], "confirm": "Send to review"}}}
        """;

    private static Snapshot Trades(bool pnlAsDouble = false)
    {
        var builder = new SnapshotColumnsBuilder();
        var desk = builder.Text("desk");
        var day = builder.Date("day");
        foreach (var (d, on) in new[] { ("Rates", 19), ("FX", 20), ("Rates", 21) })
        {
            desk.Append(d);
            day.Append(new DateOnly(2026, 10, on));
        }
        if (pnlAsDouble)
        {
            var pnl = builder.Double("pnl");
            foreach (var p in new[] { 1.0, 2.0, 3.0 })
                pnl.Append(p);
        }
        else
        {
            var pnl = builder.Decimal("pnl");
            foreach (var p in new[] { 100m, -50m, 75m })
                pnl.Append(p);
        }
        return builder.Build();
    }

    private static string Call(string data = """{"trades": "h-1"}""", string board = Board) => $$"""{"board": {{board}}, "data": {{data}}}""";

    private static Snapshot? Handles(string handle) => handle == "h-1" ? Trades() : handle == "h-2" ? Trades(pnlAsDouble: true) : null;

    [Fact] // ADR-0340, in ADR-0320's form: the tool is named, said, and its input's schema is JSON naming the Board's members
    public void The_tool_is_named_and_its_schema_names_the_boards_members()
    {
        Assert.Equal("draw_board", BoardSpec.Tool.Name);
        using var schema = JsonDocument.Parse(BoardSpec.Tool.InputSchema);
        var board = schema.RootElement.GetProperty("properties").GetProperty("board").GetProperty("properties");
        Assert.Equal(["version", "title", "inputs", "views", "layout", "links", "outputs"], board.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["text", "decimal", "double", "integer", "date", "boolean"],
            board.GetProperty("inputs").GetProperty("additionalProperties").GetProperty("properties").GetProperty("columns")
                .GetProperty("items").GetProperty("properties").GetProperty("type").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact] // ADR-0340: what the AI is told of a source is its shape without its rows, as a Board declares an input
    public void A_sources_shape_is_said_as_the_declaration_a_board_writes()
    {
        var declaration = BoardSpec.DeclarationOf(DataShape.Of(Trades()));

        Assert.Equal("""{"columns":[{"name":"desk","type":"text"},{"name":"day","type":"date"},{"name":"pnl","type":"decimal"}]}""", declaration);
        using var read = JsonDocument.Parse(declaration);
        Assert.Equal(DataShape.Of(Trades()).Columns, BoardInput.Read("trades", read.RootElement, "inputs.trades").Columns);
    }

    [Fact] // ADR-0340: an accepted Board is said in words, view by view in the layout's order, then its links and outputs
    public void An_accepted_board_is_said_in_words()
    {
        var answer = BoardSpec.Answer(Call(), Handles);

        Assert.True(answer.Accepted, answer.Text);
        Assert.StartsWith("Board \"Desk P&L\": 3 views in 2 rows. blotter (\"Trades\") is a grid of 3 rows over trades, showing Day, desk and P&L. "
            + "byDesk is a pivot of 3 rows of trades: rows desk; values Sum of pnl. daily is a chart. ", answer.Text, StringComparison.Ordinal);
        Assert.EndsWith(" A selection in blotter narrows byDesk and daily on desk. \"Send to review\" hands on day of the rows selected in blotter, as output review.",
            answer.Text, StringComparison.Ordinal);
        Assert.Equal(["trades"], answer.Board!.Inputs.Select(i => i.Name));
        Assert.Equal(3, answer.Bound!.Data["trades"].RowCount);
    }

    public static TheoryData<string, string> Refusals => new()
    {
        { Call(board: Board.Replace("\"format\": \"#,##0\"", "\"width\": 90", StringComparison.Ordinal)), "board.views[0].grid.columns[2].format: " },
        { Call("""{"trades": "h-9"}"""), "data.trades: \"h-9\" is not known: name a Data Handle a capability returned." },
        { Call("""{}"""), "data: Input 'trades' is bound to no Data Handle." },
        { Call("""{"trades": "h-1", "quotes": "h-1"}"""), "data.quotes: The call binds input 'quotes', which the Board does not declare." },
        { Call("""{"trades": "h-2"}"""), "data.trades: Input 'trades', column 'pnl': declared decimal, and its source holds double." },
        { $$"""{"board": {{Board}}}""", "data: The call binds each input to a Data Handle in 'data'." },
        { """{"board": {"version": 1}, "data": {}, "when": 1}""", "when: The call has no member 'when'." },
        { """[]""", "The call is a JSON object of 'board' and 'data'." },
    };

    [Theory, MemberData(nameof(Refusals))] // ADR-0340, in ADR-0320's form: a refusal is one sentence naming the path and the reason
    public void A_board_that_cannot_be_drawn_is_refused_by_path(string call, string text)
    {
        var answer = BoardSpec.Answer(call, Handles);

        Assert.False(answer.Accepted);
        Assert.StartsWith(text, answer.Text, StringComparison.Ordinal);
        Assert.Null(answer.Board);
    }
}
