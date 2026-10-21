namespace ExGrid.Docs.Examples.Board;

using ExBoard;
using ExGrid.Data;

/// <summary>A Board an AI composed once, and the JSON a tool returned for its input this run.</summary>
public static class DeskBoard
{
    public const string Json = """
        {"version": 1, "title": "Desk P&L",
         "inputs": {"trades": {"columns": [
           {"name": "desk", "type": "text"}, {"name": "trader", "type": "text"},
           {"name": "day", "type": "date"}, {"name": "pnl", "type": "decimal"}]}},
         "views": [
           {"id": "blotter", "title": "Trades", "grid": {"data": "trades",
             "columns": [{"column": "day", "header": "Day"}, {"column": "desk", "header": "Desk"},
                         {"column": "trader", "header": "Trader"}, {"column": "pnl", "header": "P&L", "format": "#,##0;(#,##0)"}],
             "sorts": [{"column": "day", "direction": "Ascending"}]}},
           {"id": "byTrader", "title": "By trader", "pivot": {"data": "trades", "layout": {"version": 1,
             "rows": [{"field": "trader"}], "values": [{"field": "pnl", "aggregation": "sum", "numberFormat": "#,##0"}]}}},
           {"id": "daily", "title": "P&L by day", "chart": {"version": 1, "axis": {"values": "dates"},
             "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "trades", "x": "day", "y": "pnl"}]}}],
         "layout": [{"views": ["blotter", "byTrader"], "widths": [2, 1], "height": 260}, {"views": ["daily"], "height": 300}],
         "links": [{"from": "blotter", "to": ["byTrader", "daily"], "on": ["desk"]},
                   {"from": "byTrader", "to": ["daily"], "on": ["trader"]}],
         "outputs": {"review": {"from": "blotter", "on": ["day", "trader"], "confirm": "Send to review"}}}
        """;

    public const string Data = """
        [{"desk": "Rates", "trader": "Aoki", "day": "2026-10-14", "pnl": 120500},
         {"desk": "FX", "trader": "Brandt", "day": "2026-10-15", "pnl": -42000},
         {"desk": "Rates", "trader": "Chen", "day": "2026-10-16", "pnl": 88250},
         {"desk": "Credit", "trader": "Dubois", "day": "2026-10-17", "pnl": 15400},
         {"desk": "FX", "trader": "Aoki", "day": "2026-10-20", "pnl": 61000}]
        """;

    /// <summary>The Board read, and its one input made a Snapshot by its declaration; or the refusal.</summary>
    public static (BoardReading? Board, IReadOnlyDictionary<string, Snapshot>? Data, string? Refusal) Read(string json)
    {
        try
        {
            var board = BoardSpec.Read(json);
            var trades = board.Input("trades") ?? throw new BoardRefusedException("inputs", "This page binds input 'trades'.");
            return (board, new Dictionary<string, Snapshot> { ["trades"] = trades.FromJson(Data) }, null);
        }
        catch (BoardRefusedException refused)
        {
            return (null, null, refused.Path.Length == 0 ? refused.Message : $"{refused.Path}: {refused.Message}");
        }
    }

    // The user's changes read back, as the host does next time. What no longer fits the Board is
    // dropped as it binds, and the Board says so above its views; a Diff this reader cannot read is
    // refused whole.
    public static (BoardDiff? Diff, string? Refusal) LayOver(string diff)
    {
        try
        {
            return (BoardDiff.Read(diff), null);
        }
        catch (BoardRefusedException refused)
        {
            return (null, $"The changes kept: {refused.Path}: {refused.Message}");
        }
    }
}
