namespace ExGrid.Docs.Examples.Board;

using ExBoard;
using ExGrid.Data;

/// <summary>A <c>draw_board</c> call as a model makes it: the Board, and the Data Handle a capability
/// returned for its input this conversation.</summary>
public static class BoardToolCall
{
    public static readonly string Call = "{\"board\": " + DeskBoard.Json + ", \"data\": {\"trades\": \"h-7f3a\"}}";

    // The table behind the handle; what the model was told of it is its declaration alone, never its rows.
    private static readonly Snapshot Trades = BoardSpec.Read(DeskBoard.Json).Input("trades")!.FromJson(DeskBoard.Data);

    /// <summary>What the model was told of the table: its shape, as a Board declares an input.</summary>
    public static readonly string Told = BoardSpec.DeclarationOf(DataShape.Of(Trades));

    /// <summary>What the tool answers the model, with the Board bound to draw when it is accepted.</summary>
    public static BoardSpecAnswer Answer(string call) => BoardSpec.Answer(call, handle => handle == "h-7f3a" ? Trades : null);
}
