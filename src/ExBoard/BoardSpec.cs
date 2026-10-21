using System.Text.Json;
using ExGrid;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>A Board read (ADR-0340): its title, inputs, views and layout.</summary>
/// <param name="Title">The Board's title, or empty when it gives none.</param>
/// <param name="Inputs">The inputs, in the order the Board declares them.</param>
/// <param name="Views">The views, in the order the Board lists them.</param>
/// <param name="Layout">The rows the views are laid out in, top to bottom.</param>
public sealed record BoardReading(string Title, IReadOnlyList<BoardInput> Inputs, IReadOnlyList<BoardView> Views, IReadOnlyList<BoardRow> Layout)
{
    /// <summary>The input named <paramref name="name"/>, or null.</summary>
    public BoardInput? Input(string name) => Inputs.FirstOrDefault(i => i.Name == name);

    /// <summary>The view whose id is <paramref name="id"/>, or null.</summary>
    public BoardView? View(string id) => Views.FirstOrDefault(v => v.Id == id);
}

/// <summary>A view of a Board: a grid, a pivot or a chart over its inputs.</summary>
public abstract record BoardView
{
    private protected BoardView(string id, string title)
    {
        Id = id;
        Title = title;
    }

    /// <summary>The view's id, by which the layout places it.</summary>
    public string Id { get; }

    /// <summary>The view's title, or empty when it gives none.</summary>
    public string Title { get; }

    /// <summary>The inputs the view reads.</summary>
    public abstract IReadOnlyList<string> Data { get; }
}

/// <summary>A grid view (ADR-0342): its spec read against its input's declaration.</summary>
public sealed record BoardGridView : BoardView
{
    internal BoardGridView(string id, string title, GridViewSpecReading grid)
        : base(id, title) => Grid = grid;

    /// <summary>The grid's spec.</summary>
    public GridViewSpecReading Grid { get; }

    /// <inheritdoc />
    public override IReadOnlyList<string> Data => [Grid.Data];
}

/// <summary>A pivot view: the input it reads and its Pivot Layout (ADR-0059), every field it places
/// declared by the input.</summary>
public sealed record BoardPivotView : BoardView
{
    internal BoardPivotView(string id, string title, string data, PivotLayout layout)
        : base(id, title)
    {
        Input = data;
        Layout = layout;
    }

    /// <summary>The input the pivot reads.</summary>
    public string Input { get; }

    /// <summary>The pivot's layout.</summary>
    public PivotLayout Layout { get; }

    /// <inheritdoc />
    public override IReadOnlyList<string> Data => [Input];
}

/// <summary>A chart view: a Chart Spec whose tables are the Board's inputs (ADR-0320 as rewritten by
/// ADR-0340), kept as written and read against its inputs' data when the Board is drawn.</summary>
public sealed record BoardChartView : BoardView
{
    internal BoardChartView(string id, string title, string spec, IReadOnlyList<string> data)
        : base(id, title)
    {
        Spec = spec;
        Data = data;
    }

    /// <summary>The Chart Spec, as JSON.</summary>
    public string Spec { get; }

    /// <inheritdoc />
    public override IReadOnlyList<string> Data { get; }
}

/// <summary>A row of a Board's layout: its views, left to right, each with its share of the row's
/// width, and the height their data is drawn at.</summary>
/// <param name="Cells">The views in the row.</param>
/// <param name="Height">The height, in CSS pixels, of each view's data: a grid's or a pivot's viewport,
/// a chart's stage. A view's title and a chart's toolbar stand above it.</param>
public sealed record BoardRow(IReadOnlyList<BoardCell> Cells, double Height);

/// <summary>A view's place in a row.</summary>
/// <param name="View">The view's id.</param>
/// <param name="Width">Its share of the row's width, a whole number from 1: views of widths 2 and 1
/// take two thirds and one third.</param>
public sealed record BoardCell(string View, int Width);

/// <summary>
/// The Board's JSON (ADR-0340), as an AI writes it once and a workflow runs it again and again:
/// <code>
/// {"version": 1, "title": "Desk P&amp;L",
///  "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, …]}},
///  "views": [{"id": "blotter", "title": "Trades", "grid": {…}},
///            {"id": "byDesk", "pivot": {"data": "trades", "layout": {…}}},
///            {"id": "curve", "chart": {…}}],
///  "layout": [{"views": ["blotter", "byDesk"], "widths": [2, 1], "height": 360}, {"views": ["curve"]}]}
/// </code>
/// A grid is <see cref="GridViewSpec"/>'s spec, a pivot names its input and holds a
/// <see cref="PivotLayoutJson"/> layout, and a chart is a Chart Spec whose series name inputs in their
/// <c>data</c>. The layout is rows of views, never free placement, and every view stands in it once.
/// <para>What reading refuses, with a <see cref="BoardRefusedException"/> naming the path and the reason
/// in one sentence: text that is not JSON, or a member written twice; a version missing, or other than
/// <see cref="Version"/>; a member the Board does not have; an input's declaration
/// (<see cref="BoardInput.Read"/>); a view with no id, an id twice, or other than one of grid, pivot and
/// chart; a view naming an input the Board does not declare, or a column or field the input does not
/// declare; a pivot layout its reader refuses; and a layout that places a view twice, leaves one out,
/// names one that is not there, or gives widths or heights that are no widths or heights.</para>
/// </summary>
public static class BoardSpec
{
    /// <summary>The version of the Board this reader reads.</summary>
    public const int Version = 1;

    /// <summary>A row's height when the layout gives none, in CSS pixels.</summary>
    public const double DefaultRowHeight = 320;

    private static readonly string[] RootKeys = ["version", "title", "inputs", "views", "layout"];
    private static readonly string[] ViewKeys = ["id", "title", "grid", "pivot", "chart"];
    private static readonly string[] Kinds = ["grid", "pivot", "chart"];
    private static readonly string[] PivotKeys = ["data", "layout"];
    private static readonly string[] RowKeys = ["views", "widths", "height"];

    /// <summary>Reads <paramref name="json"/>.</summary>
    /// <exception cref="BoardRefusedException">The Board cannot be drawn; the message says where and why.</exception>
    public static BoardReading Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException e)
        {
            throw new BoardRefusedException("", $"The Board is not JSON: {e.Message}");
        }
        using (document)
            return Read(document.RootElement);
    }

    private static BoardReading Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException("", "A Board is a JSON object.");
        // The version first: a Board of another version is told so, not about a member it may have.
        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number)
            throw new BoardRefusedException("version", $"A Board says its version; this reader reads version {Version}.");
        if (!version.TryGetInt32(out var number) || number != Version)
            throw new BoardRefusedException("version", $"The Board is version {version.GetRawText()}, and this reader reads version {Version} only.");
        Members(root, RootKeys, "", "The Board");
        var title = OptionalText(root, "title", "title", "The Board's title");

        if (!root.TryGetProperty("inputs", out var inputsValue) || inputsValue.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException("inputs", "A Board declares its inputs, by name, in 'inputs'.");
        var inputs = new List<BoardInput>();
        foreach (var input in inputsValue.EnumerateObject())
            inputs.Add(BoardInput.Read(input.Name, input.Value, $"inputs.{input.Name}"));
        if (inputs.Count == 0)
            throw new BoardRefusedException("inputs", "A Board declares at least one input.");
        DataShape? Shape(string name) => inputs.FirstOrDefault(i => i.Name == name)?.Shape;

        if (!root.TryGetProperty("views", out var viewsValue) || viewsValue.ValueKind != JsonValueKind.Array || viewsValue.GetArrayLength() == 0)
            throw new BoardRefusedException("views", "A Board lists its views in 'views'.");
        var views = new List<BoardView>();
        var i = 0;
        foreach (var view in viewsValue.EnumerateArray())
        {
            var read = ReadView(view, $"views[{i++}]", Shape);
            if (views.Any(v => v.Id == read.Id))
                throw new BoardRefusedException($"views[{i - 1}].id", $"Two views have the id '{read.Id}'.");
            views.Add(read);
        }

        return new BoardReading(title, inputs, views, ReadLayout(root, views));
    }

    private static BoardView ReadView(JsonElement view, string path, Func<string, DataShape?> shape)
    {
        if (view.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException(path, "A view is an object.");
        Members(view, ViewKeys, path, "A view");
        if (!view.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String || idValue.GetString() is not { Length: > 0 } id)
            throw new BoardRefusedException($"{path}.id", "A view has an id, by which the layout places it.");
        var title = OptionalText(view, "title", $"{path}.title", $"View '{id}''s title");
        var kinds = Kinds.Where(k => view.TryGetProperty(k, out _)).ToArray();
        if (kinds.Length != 1)
            throw new BoardRefusedException(path, $"View '{id}' is one of grid, pivot and chart{(kinds.Length == 0 ? "" : $", not {string.Join(" and ", kinds)}")}.");
        var at = $"{path}.{kinds[0]}";
        var spec = view.GetProperty(kinds[0]);
        return kinds[0] switch
        {
            "grid" => new BoardGridView(id, title, GridViewSpec.Read(spec, shape, at)),
            "pivot" => ReadPivot(id, title, spec, at, shape),
            _ => ReadChart(id, title, spec, at, shape),
        };
    }

    private static BoardPivotView ReadPivot(string id, string title, JsonElement spec, string path, Func<string, DataShape?> shape)
    {
        if (spec.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException(path, "A pivot's spec is an object.");
        Members(spec, PivotKeys, path, "A pivot's spec");
        var data = Input(spec, path, shape, out var declared);
        if (!spec.TryGetProperty("layout", out var layoutValue) || layoutValue.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException($"{path}.layout", "A pivot holds its Pivot Layout in 'layout'.");
        PivotLayout layout;
        try
        {
            layout = PivotLayoutJson.Read(layoutValue.GetRawText());
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        {
            throw new BoardRefusedException($"{path}.layout", $"The pivot's layout cannot be read: {e.Message}");
        }
        var placed = layout.Filters.Select(f => f.Field).Concat(layout.Rows.Select(f => f.Field))
            .Concat(layout.Columns.Select(f => f.Field)).Concat(layout.Values.Select(v => v.Field));
        foreach (var field in placed)
        {
            if (!declared.TryGetColumn(field, out _))
                throw new BoardRefusedException($"{path}.layout", $"The pivot places field '{field}', which input '{data}' does not declare.");
        }
        return new BoardPivotView(id, title, data, layout);
    }

    private static BoardChartView ReadChart(string id, string title, JsonElement spec, string path, Func<string, DataShape?> shape)
    {
        if (spec.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException(path, "A chart is a Chart Spec, an object.");
        // The spec is the Chart Spec's to read; here only the inputs its series name are checked, so
        // that a chart reading an input the Board does not declare is refused with the rest.
        var data = new List<string>();
        if (spec.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var one in series.EnumerateArray())
            {
                var at = $"{path}.series[{i++}]";
                if (one.ValueKind != JsonValueKind.Object)
                    continue;
                CheckChartData(one, at, shape, data);
                if (one.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Object)
                    CheckChartData(edges, $"{at}.edges", shape, data);
            }
        }
        return new BoardChartView(id, title, spec.GetRawText(), data);
    }

    private static void CheckChartData(JsonElement owner, string path, Func<string, DataShape?> shape, List<string> data)
    {
        if (!owner.TryGetProperty("data", out var value) || value.ValueKind != JsonValueKind.String)
            return;
        var name = value.GetString()!;
        if (shape(name) is null)
            throw new BoardRefusedException($"{path}.data", $"The chart reads input '{name}', which the Board does not declare.");
        if (!data.Contains(name))
            data.Add(name);
    }

    private static string Input(JsonElement spec, string path, Func<string, DataShape?> shape, out DataShape declared)
    {
        if (!spec.TryGetProperty("data", out var value) || value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: > 0 } data)
            throw new BoardRefusedException($"{path}.data", "A view names the input it reads in 'data'.");
        declared = shape(data) ?? throw new BoardRefusedException($"{path}.data", $"The view reads input '{data}', which the Board does not declare.");
        return data;
    }

    private static List<BoardRow> ReadLayout(JsonElement root, List<BoardView> views)
    {
        if (!root.TryGetProperty("layout", out var layout) || layout.ValueKind != JsonValueKind.Array || layout.GetArrayLength() == 0)
            throw new BoardRefusedException("layout", "A Board lays its views out in rows, in 'layout'.");
        var rows = new List<BoardRow>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var r = 0;
        foreach (var row in layout.EnumerateArray())
        {
            var path = $"layout[{r++}]";
            if (row.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException(path, "A row of the layout is an object.");
            Members(row, RowKeys, path, "A row of the layout");
            if (!row.TryGetProperty("views", out var ids) || ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() == 0)
                throw new BoardRefusedException($"{path}.views", "A row lists its views, left to right, in 'views'.");
            var names = new List<string>();
            var k = 0;
            foreach (var id in ids.EnumerateArray())
            {
                var at = $"{path}.views[{k++}]";
                if (id.ValueKind != JsonValueKind.String || id.GetString() is not { } name)
                    throw new BoardRefusedException(at, "A row names a view by its id.");
                if (!views.Any(v => v.Id == name))
                    throw new BoardRefusedException(at, $"The layout places view '{name}', which the Board does not have.");
                if (!placed.Add(name))
                    throw new BoardRefusedException(at, $"The layout places view '{name}' twice; a view stands in one place.");
                names.Add(name);
            }
            var widths = Enumerable.Repeat(1, names.Count).ToArray();
            if (row.TryGetProperty("widths", out var widthsValue))
            {
                if (widthsValue.ValueKind != JsonValueKind.Array || widthsValue.GetArrayLength() != names.Count)
                    throw new BoardRefusedException($"{path}.widths", $"A row gives one width per view; this row has {names.Count}.");
                var w = 0;
                foreach (var width in widthsValue.EnumerateArray())
                {
                    if (width.ValueKind != JsonValueKind.Number || !width.TryGetInt32(out var share) || share is < 1 or > 12)
                        throw new BoardRefusedException($"{path}.widths[{w}]", "A width is a whole number from 1 to 12, the view's share of the row.");
                    widths[w++] = share;
                }
            }
            var height = DefaultRowHeight;
            if (row.TryGetProperty("height", out var heightValue))
            {
                if (heightValue.ValueKind != JsonValueKind.Number || !heightValue.TryGetDouble(out height) || !double.IsFinite(height) || height is < 80 or > 4000)
                    throw new BoardRefusedException($"{path}.height", "A row's height is a number of CSS pixels from 80 to 4000.");
            }
            rows.Add(new BoardRow([.. names.Select((n, i) => new BoardCell(n, widths[i]))], height));
        }
        foreach (var view in views)
        {
            if (!placed.Contains(view.Id))
                throw new BoardRefusedException("layout", $"The layout leaves out view '{view.Id}'; every view stands in it once.");
        }
        return rows;
    }

    private static void Members(JsonElement element, string[] keys, string path, string what)
    {
        foreach (var member in element.EnumerateObject())
        {
            if (Array.IndexOf(keys, member.Name) < 0)
                throw new BoardRefusedException(path.Length == 0 ? member.Name : $"{path}.{member.Name}", $"{what} has no member '{member.Name}'.");
        }
    }

    private static string OptionalText(JsonElement element, string member, string path, string what)
    {
        if (!element.TryGetProperty(member, out var value))
            return "";
        if (value.ValueKind != JsonValueKind.String)
            throw new BoardRefusedException(path, $"{what} is text.");
        return value.GetString()!;
    }
}
