using System.Globalization;
using System.Text;
using System.Text.Json;
using ExChart.Engine;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>The tool an AI calls with a Board, as a host hands it to a model (ADR-0340, in ADR-0320's form).</summary>
/// <param name="Name">The tool's name.</param>
/// <param name="Description">What it does, for the model.</param>
/// <param name="InputSchema">The JSON Schema of its input.</param>
public sealed record BoardSpecTool(string Name, string Description, string InputSchema);

/// <summary>What the tool answers the AI.</summary>
/// <param name="Accepted">Whether the Board can be drawn.</param>
/// <param name="Text">The Board said in words, or the refusal: one sentence naming the path and the reason.</param>
/// <param name="Board">The Board read, when accepted: what a workflow saves and runs again.</param>
/// <param name="Bound">The Board bound to the tables its Data Handles name, when accepted: what the host draws now.</param>
public sealed record BoardSpecAnswer(bool Accepted, string Text, BoardReading? Board, BoundBoard? Bound);

public static partial class BoardSpec
{
    /// <summary>
    /// The tool an AI calls to draw a Board: its name, what it does, and the JSON Schema of its input, which
    /// is the Board (<c>board</c>) and the Data Handle each of its inputs is bound to now (<c>data</c>).
    /// </summary>
    public static BoardSpecTool Tool { get; } = new("draw_board",
        "Draws a Board: grids, pivots and charts laid out in rows over data capabilities already made, with links that "
        + "narrow one view by what is selected in another, and outputs the user hands on with a confirm. Declare each input's "
        + "columns and types as the capability said its shape, bind each input to the Data Handle the capability returned in "
        + "'data', and never write the values themselves. The Board, not the handles, is what is saved and run again without "
        + "you. A Board that cannot be drawn is refused with the path and the reason; an accepted one is answered with the "
        + "Board said in words, to check against what was meant.",
        Schema);

    /// <summary>
    /// What the AI is told of a source while it composes (ADR-0340): its shape without its rows, in the form
    /// a Board declares an input, <c>{"columns": [{"name": "desk", "type": "text"}, …]}</c>.
    /// </summary>
    public static string DeclarationOf(DataShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteStartArray("columns");
            foreach (var column in shape.Columns)
            {
                json.WriteStartObject();
                json.WriteString("name", column.Name);
                json.WriteString("type", BoardInput.NameOf(column.Kind));
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Reads the tool's input and answers as the tool answers the AI: the refusal, or the Board said in words,
    /// bound to the tables its Data Handles name.
    /// </summary>
    /// <param name="json">The tool's input: <c>{"board": {…}, "data": {"trades": "h-7f3a"}}</c>.</param>
    /// <param name="tables">The host's lookup from a Data Handle to its table, null for a handle it does not know.</param>
    /// <param name="culture">The culture a grid's text is written in.</param>
    public static BoardSpecAnswer Answer(string json, Func<string, Snapshot?> tables, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(tables);
        try
        {
            var (board, data) = ReadCall(json, tables);
            BoundBoard bound;
            try
            {
                bound = BoundBoard.Bind(board, data, culture);
            }
            catch (BoardRefusedException e)
            {
                // A table whose shape is not its input's is said of the binding the AI wrote; anything
                // else the binding refuses, of the Board.
                var input = e.Path.StartsWith("inputs.", StringComparison.Ordinal) ? e.Path["inputs.".Length..] : e.Path;
                throw board.Inputs.Any(i => i.Name == input)
                    ? new BoardRefusedException($"data.{input}", e.Message)
                    : new BoardRefusedException(e.Path.Length == 0 ? "board" : $"board.{e.Path}", e.Message);
            }
            return new BoardSpecAnswer(true, Say(bound), board, bound);
        }
        catch (BoardRefusedException e)
        {
            return new BoardSpecAnswer(false, e.Path.Length == 0 ? e.Message : $"{e.Path}: {e.Message}", null, null);
        }
    }

    private static (BoardReading Board, Dictionary<string, Snapshot> Data) ReadCall(string json, Func<string, Snapshot?> tables)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            throw new BoardRefusedException("", $"The call is not JSON: {e.Message}");
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException("", "The call is a JSON object of 'board' and 'data'.");
            Members(root, ["board", "data"], "", "The call");
            if (!root.TryGetProperty("board", out var boardValue))
                throw new BoardRefusedException("board", "The call holds the Board in 'board'.");
            BoardReading board;
            try
            {
                board = Read(boardValue.GetRawText());
            }
            catch (BoardRefusedException e)
            {
                throw new BoardRefusedException(e.Path.Length == 0 ? "board" : $"board.{e.Path}", e.Message);
            }
            if (!root.TryGetProperty("data", out var dataValue) || dataValue.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException("data", "The call binds each input to a Data Handle in 'data'.");
            var data = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
            foreach (var member in dataValue.EnumerateObject())
            {
                if (board.Input(member.Name) is null)
                    throw new BoardRefusedException($"data.{member.Name}", $"The call binds input '{member.Name}', which the Board does not declare.");
                if (member.Value.ValueKind != JsonValueKind.String || member.Value.GetString() is not { Length: > 0 } handle)
                    throw new BoardRefusedException($"data.{member.Name}", $"Input '{member.Name}' is bound to a Data Handle, a string.");
                data[member.Name] = tables(handle)
                    ?? throw new BoardRefusedException($"data.{member.Name}", $"\"{handle}\" is not known: name a Data Handle a capability returned.");
            }
            foreach (var input in board.Inputs)
            {
                if (!data.ContainsKey(input.Name))
                    throw new BoardRefusedException("data", $"Input '{input.Name}' is bound to no Data Handle.");
            }
            return (board, data);
        }
    }

    /// <summary>The Board said in words, as the tool answers an accepted one: its views in the layout's
    /// order, each with what it shows, then its links and its outputs.</summary>
    public static string Say(BoundBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var reading = board.Board;
        var cells = reading.Layout.SelectMany(r => r.Cells).ToList();
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"{(reading.Title is { Length: > 0 } title ? $"Board \"{title}\"" : "The Board")}: ");
        text.Append(CultureInfo.InvariantCulture, $"{Count(cells.Count, "view")} in {Count(reading.Layout.Count, "row")}.");
        foreach (var cell in cells)
            text.Append(' ').Append(SayView(board[cell.View]));
        foreach (var link in reading.Links)
            text.Append(CultureInfo.InvariantCulture, $" A selection in {link.From} narrows {And(link.To)} on {And(link.On)}.");
        foreach (var output in reading.Outputs)
            text.Append(CultureInfo.InvariantCulture, $" \"{output.Confirm}\" hands on {And(output.On)} of the rows selected in {output.From}, as output {output.Name}.");
        if (reading.Links.Count == 0 && reading.Outputs.Count == 0)
            text.Append(" No view narrows another, and nothing is handed on.");
        return text.ToString();
    }

    private static string SayView(BoundView view)
    {
        var id = view.View.Id;
        var named = view.View.Title is { Length: > 0 } title ? $"{id} (\"{title}\")" : id;
        switch (view)
        {
            case BoundGridView grid:
                return $"{named} is a grid of {(grid.Grid is { } held ? Count(held.Snapshot.RowCount, "row") : "rows fetched")} over {grid.View.Data[0]}, showing {And(grid.Columns.Select(c => c.Header))}.";
            case BoundPivotView pivot:
                var layout = pivot.Layout;
                var parts = new List<string>();
                if (layout.Rows.Count > 0)
                    parts.Add($"rows {And(layout.Rows.Select(f => f.Field))}");
                if (layout.Columns.Count > 0)
                    parts.Add($"columns {And(layout.Columns.Select(f => f.Field))}");
                if (layout.Filters.Count > 0)
                    parts.Add($"filtered by {And(layout.Filters.Select(f => f.Field))}");
                parts.Add(layout.Values.Count == 0 ? "no values" : $"values {And(layout.Values.Select(v => $"{v.Aggregation} of {v.Field}"))}");
                return $"{named} is a pivot of {(pivot.Source is { } snapshot ? Count(snapshot.Snapshot.RowCount, "row") : "a report")} of {pivot.Input}: {string.Join("; ", parts)}.";
            case BoundChartView chart:
                ChartScene scene;
                try
                {
                    scene = ChartScene.Build(chart.Reading.Definition, new ChartMetrics(800, 400));
                }
                catch (ChartRefusedException e)
                {
                    return $"{named} is a chart that cannot be drawn: {e.Message}";
                }
                return $"{named} is a chart. {ChartSummary.Of(chart.Reading.Definition, scene, ChartView.All, chart.Reading.Title)}";
            default:
                return $"{named}.";
        }
    }

    private static string Count(int count, string noun) => string.Create(CultureInfo.InvariantCulture, $"{count:N0} {noun}{(count == 1 ? "" : "s")}");

    private static string And(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count switch
        {
            0 => "nothing",
            1 => list[0],
            _ => $"{string.Join(", ", list.Take(list.Count - 1))} and {list[^1]}",
        };
    }

    private const string Schema = """
        {
          "type": "object",
          "required": ["board", "data"],
          "additionalProperties": false,
          "properties": {
            "board": {
              "type": "object",
              "description": "The Board, as it is saved and run again.",
              "required": ["version", "inputs", "views", "layout"],
              "additionalProperties": false,
              "properties": {
                "version": { "const": 1 },
                "title": { "type": "string" },
                "inputs": {
                  "type": "object",
                  "description": "Each input by name, its columns declared as the capability said its shape.",
                  "additionalProperties": {
                    "type": "object", "required": ["columns"], "additionalProperties": false,
                    "properties": { "columns": { "type": "array", "minItems": 1, "items": {
                      "type": "object", "required": ["name", "type"], "additionalProperties": false,
                      "properties": { "name": { "type": "string" }, "type": { "enum": ["text", "decimal", "double", "integer", "date", "boolean"] } } } } }
                  }
                },
                "views": {
                  "type": "array", "minItems": 1,
                  "items": {
                    "type": "object", "required": ["id"], "additionalProperties": false,
                    "description": "One of grid, pivot and chart.",
                    "properties": {
                      "id": { "type": "string" },
                      "title": { "type": "string" },
                      "grid": {
                        "type": "object", "required": ["data", "columns"], "additionalProperties": false,
                        "properties": {
                          "data": { "type": "string", "description": "The input it shows." },
                          "columns": { "type": "array", "minItems": 1, "items": {
                            "type": "object", "required": ["column"], "additionalProperties": false,
                            "properties": {
                              "column": { "type": "string" }, "header": { "type": "string" },
                              "format": { "type": "string", "description": "An Excel format code; a decimal or double column always gives one, as its places are yours to choose." },
                              "width": { "type": "number" }, "pinned": { "type": "boolean" } } } },
                          "sorts": { "type": "array", "items": { "type": "object", "properties": { "column": { "type": "string" }, "direction": { "enum": ["Ascending", "Descending"] } } } },
                          "filter": { "type": "object", "description": "A grid filter, in the View State's JSON." }
                        }
                      },
                      "pivot": {
                        "type": "object", "required": ["data", "layout"], "additionalProperties": false,
                        "properties": {
                          "data": { "type": "string", "description": "The input it pivots." },
                          "layout": { "type": "object", "description": "A pivot layout: {\"version\": 1, \"rows\": [{\"field\": …}], \"columns\": […], \"filters\": […], \"values\": [{\"field\": …, \"aggregation\": \"sum\"}]}." }
                        }
                      },
                      "chart": { "type": "object", "description": "A Chart Spec, as draw_chart takes it, each series' data naming an input." }
                    }
                  }
                },
                "layout": {
                  "type": "array", "minItems": 1, "description": "Rows of views, each view placed once.",
                  "items": { "type": "object", "required": ["views"], "additionalProperties": false,
                    "properties": { "views": { "type": "array", "items": { "type": "string" } }, "widths": { "type": "array", "items": { "type": "integer", "minimum": 1 } }, "height": { "type": "number" } } }
                },
                "links": {
                  "type": "array", "description": "A selection in a grid's rows or a pivot's cells narrows other views to the rows whose values in 'on' it selects.",
                  "items": { "type": "object", "required": ["from", "to", "on"], "additionalProperties": false,
                    "properties": { "from": { "type": "string" }, "to": { "type": "array", "items": { "type": "string" } }, "on": { "type": "array", "items": { "type": "string" } } } }
                },
                "outputs": {
                  "type": "object", "description": "What the user hands on from a grid's selected rows, only when they press its confirm.",
                  "additionalProperties": { "type": "object", "required": ["from", "on", "confirm"], "additionalProperties": false,
                    "properties": { "from": { "type": "string" }, "on": { "type": "array", "items": { "type": "string" } }, "confirm": { "type": "string" } } }
                }
              }
            },
            "data": {
              "type": "object", "description": "The Data Handle each input is bound to now, by the input's name.",
              "additionalProperties": { "type": "string" }
            }
          }
        }
        """;
}
