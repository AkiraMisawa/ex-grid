using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExGrid;
using ExPivot.Engine;

namespace ExBoard;

public static partial class BoardSpec
{
    /// <summary>
    /// A Board as its JSON (ADR-0340), which <see cref="Read(string)"/> reads back to the same Board: what Blazor's
    /// Script adapter hands ExBoard's Script Core, whose reader is the TypeScript one (ADR-0370 section 1). Each part is
    /// written by the writer of what it holds: a grid's columns and View State by <see cref="GridViewStateJson"/>, a
    /// pivot's layout by <see cref="PivotLayoutJson"/>, and a chart's spec as it was written.
    /// </summary>
    internal static string Write(BoardReading board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("version", Version);
            if (board.Title.Length > 0)
                json.WriteString("title", board.Title);
            json.WriteStartObject("inputs");
            foreach (var input in board.Inputs)
            {
                json.WritePropertyName(input.Name);
                json.WriteRawValue(DeclarationOf(input.Shape));
            }
            json.WriteEndObject();
            json.WriteStartArray("views");
            foreach (var view in board.Views)
                WriteView(json, view);
            json.WriteEndArray();
            json.WriteStartArray("layout");
            foreach (var row in board.Layout)
            {
                json.WriteStartObject();
                json.WriteStartArray("views");
                foreach (var cell in row.Cells)
                    json.WriteStringValue(cell.View);
                json.WriteEndArray();
                json.WriteStartArray("widths");
                foreach (var cell in row.Cells)
                    json.WriteNumberValue(cell.Width);
                json.WriteEndArray();
                json.WriteNumber("height", row.Height);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartArray("links");
            foreach (var link in board.Links)
            {
                json.WriteStartObject();
                json.WriteString("from", link.From);
                WriteNames(json, "to", link.To);
                WriteNames(json, "on", link.On);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteStartObject("outputs");
            foreach (var output in board.Outputs)
            {
                json.WriteStartObject(output.Name);
                json.WriteString("from", output.From);
                WriteNames(json, "on", output.On);
                json.WriteString("confirm", output.Confirm);
                json.WriteEndObject();
            }
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteView(Utf8JsonWriter json, BoardView view)
    {
        json.WriteStartObject();
        json.WriteString("id", view.Id);
        if (view.Title.Length > 0)
            json.WriteString("title", view.Title);
        switch (view)
        {
            case BoardGridView grid:
                json.WritePropertyName("grid");
                WriteGrid(json, grid.Grid);
                break;
            case BoardPivotView pivot:
                json.WriteStartObject("pivot");
                json.WriteString("data", pivot.Input);
                json.WritePropertyName("layout");
                json.WriteRawValue(PivotLayoutJson.Write(pivot.Layout));
                json.WriteEndObject();
                break;
            case BoardChartView chart:
                json.WritePropertyName("chart");
                json.WriteRawValue(chart.Spec);
                break;
            default:
                throw new InvalidOperationException($"Internal: a view of the type {view.GetType().Name}.");
        }
        json.WriteEndObject();
    }

    // A grid as a Board writes it: each column's header and format beside the width and pinning its View State holds,
    // and the View State's sorts and filter, as its own writer writes them.
    private static void WriteGrid(Utf8JsonWriter json, GridViewSpecReading grid)
    {
        var state = JsonNode.Parse(GridViewStateJson.Write(grid.ViewState)) as JsonObject
            ?? throw new InvalidOperationException("Internal: the View State's writer wrote no object.");
        var shown = Member(state, "columns").AsArray();
        if (shown.Count != grid.Columns.Count)
            throw new InvalidOperationException($"Internal: a grid of {grid.Columns.Count} columns holds a View State of {shown.Count}.");
        json.WriteStartObject();
        json.WriteString("data", grid.Data);
        json.WriteStartArray("columns");
        for (var i = 0; i < grid.Columns.Count; i++)
        {
            var column = grid.Columns[i];
            var held = shown[i] as JsonObject ?? throw new InvalidOperationException($"Internal: the View State's writer wrote column {i} as no object.");
            if ((string?)held["column"] != column.Column)
                throw new InvalidOperationException($"Internal: a grid's column {i} is '{column.Column}', and its View State's '{held["column"]}'.");
            json.WriteStartObject();
            json.WriteString("column", column.Column);
            json.WriteString("header", column.Header);
            if (column.Format is { } format)
                json.WriteString("format", format);
            json.WritePropertyName("width");
            Member(held, "width").WriteTo(json);
            json.WritePropertyName("pinned");
            Member(held, "pinned").WriteTo(json);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WritePropertyName("sorts");
        Member(state, "sorts").WriteTo(json);
        if (state["filter"] is { } filter)
        {
            json.WritePropertyName("filter");
            filter.WriteTo(json);
        }
        json.WriteEndObject();
    }

    private static JsonNode Member(JsonObject owner, string name) =>
        owner[name] ?? throw new InvalidOperationException($"Internal: the View State's writer wrote no '{name}'.");

    private static void WriteNames(Utf8JsonWriter json, string member, IReadOnlyList<string> names)
    {
        json.WriteStartArray(member);
        foreach (var name in names)
            json.WriteStringValue(name);
        json.WriteEndArray();
    }
}
