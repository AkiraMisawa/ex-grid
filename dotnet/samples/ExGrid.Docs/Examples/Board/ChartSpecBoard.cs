namespace ExGrid.Docs.Examples.Board;

using ExBoard;
using ExGrid.Data;

/// <summary>A spec <c>draw_chart</c> took while an AI composed, over a table a capability returned as a
/// Data Handle, drawn as a Board of one view.</summary>
public static class ChartSpecBoard
{
    public const string Spec = """
        {"version": 1, "title": "P&L by day", "axis": {"values": "dates"},
         "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "h-7f3a", "x": "day", "y": "pnl"}]}
        """;

    // The table behind the handle, as the host holds it this conversation.
    private static readonly Snapshot Trades = new BoardInput("h-7f3a",
        [new DataShapeColumn("desk", SnapshotKind.Text), new DataShapeColumn("trader", SnapshotKind.Text),
         new DataShapeColumn("day", SnapshotKind.Date), new DataShapeColumn("pnl", SnapshotKind.Decimal)]).FromJson(DeskBoard.Data);

    /// <summary>The spec as a Board, each handle it names an input declared with its table's shape; or the refusal, in the spec's own path.</summary>
    public static (BoardReading? Board, IReadOnlyDictionary<string, Snapshot>? Data, string? Refusal) Read(string spec)
    {
        try
        {
            var board = BoardSpec.OfChart(spec, new Dictionary<string, DataShape> { ["h-7f3a"] = DataShape.Of(Trades) });
            return (board, new Dictionary<string, Snapshot> { ["h-7f3a"] = Trades }, null);
        }
        catch (BoardRefusedException refused)
        {
            // A Chart Spec's refusal says its path in its sentence, as the tool answers the AI.
            return (null, null, refused.Message);
        }
    }
}
