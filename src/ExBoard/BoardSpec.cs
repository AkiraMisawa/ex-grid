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
/// <param name="Links">The links, in the order the Board lists them.</param>
/// <param name="Outputs">The outputs, in the order the Board declares them.</param>
public sealed record BoardReading(string Title, IReadOnlyList<BoardInput> Inputs, IReadOnlyList<BoardView> Views, IReadOnlyList<BoardRow> Layout,
    IReadOnlyList<BoardLink> Links, IReadOnlyList<BoardOutputDeclaration> Outputs)
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
///  "layout": [{"views": ["blotter", "byDesk"], "widths": [2, 1], "height": 360}, {"views": ["curve"]}],
///  "links": [{"from": "blotter", "to": ["byDesk", "curve"], "on": ["desk"]}],
///  "outputs": {"chosen": {"from": "blotter", "on": ["tradeId"], "confirm": "Book these trades"}}}
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
/// names one that is not there, or gives widths or heights that are no widths or heights; and a link
/// (<see cref="BoardLink"/>) from a view that is not a grid, to a view that is not there, to its own
/// view or one that narrows it, on a column its views' inputs do not all declare of one kind; and an
/// output (<see cref="BoardOutputDeclaration"/>) from a view that is not a grid, on a column its input
/// does not declare, or with no confirm action to raise it.</para>
/// </summary>
public static class BoardSpec
{
    /// <summary>The version of the Board this reader reads.</summary>
    public const int Version = 1;

    /// <summary>A row's height when the layout gives none, in CSS pixels.</summary>
    public const double DefaultRowHeight = 320;

    private static readonly string[] RootKeys = ["version", "title", "inputs", "views", "layout", "links", "outputs"];
    private static readonly string[] OutputKeys = ["from", "on", "confirm"];
    private static readonly string[] LinkKeys = ["from", "to", "on"];
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

        var layout = ReadLayout(root, views);
        var links = ReadLinks(root, views, Shape);
        return new BoardReading(title, inputs, views, layout, links, ReadOutputs(root, views, Shape));
    }

    private static List<BoardOutputDeclaration> ReadOutputs(JsonElement root, List<BoardView> views, Func<string, DataShape?> shape)
    {
        var outputs = new List<BoardOutputDeclaration>();
        if (!root.TryGetProperty("outputs", out var list))
            return outputs;
        if (list.ValueKind != JsonValueKind.Object)
            throw new BoardRefusedException("outputs", "A Board declares its outputs, by name, in 'outputs'.");
        foreach (var member in list.EnumerateObject())
        {
            var path = $"outputs.{member.Name}";
            var output = member.Value;
            if (member.Name.Length == 0)
                throw new BoardRefusedException("outputs", "An output has a name.");
            if (output.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException(path, "An output is an object.");
            Members(output, OutputKeys, path, "An output");
            if (!output.TryGetProperty("from", out var fromValue) || fromValue.ValueKind != JsonValueKind.String || fromValue.GetString() is not { Length: > 0 } fromId)
                throw new BoardRefusedException($"{path}.from", "An output names the view whose selection it hands on, in 'from'.");
            var from = views.FirstOrDefault(v => v.Id == fromId)
                ?? throw new BoardRefusedException($"{path}.from", $"Output '{member.Name}' is taken from view '{fromId}', which the Board does not have.");
            if (from is not BoardGridView grid)
                throw new BoardRefusedException($"{path}.from", $"Output '{member.Name}' is taken from view '{fromId}', a {KindOf(from)}; an output is taken from a grid's selected rows.");
            var on = Names(output, "on", $"{path}.on", "An output names the columns whose values it hands on, in 'on'.");
            var declared = shape(grid.Grid.Data)!;
            for (var c = 0; c < on.Count; c++)
            {
                if (!declared.TryGetColumn(on[c], out _))
                    throw new BoardRefusedException($"{path}.on[{c}]", $"Output '{member.Name}' hands on column '{on[c]}', which input '{grid.Grid.Data}' does not declare.");
            }
            // Raised only by a confirm the Board declares, never by a selection on the way (Q6).
            if (!output.TryGetProperty("confirm", out var confirm) || confirm.ValueKind != JsonValueKind.String || confirm.GetString() is not { Length: > 0 } label)
                throw new BoardRefusedException($"{path}.confirm", $"Output '{member.Name}' is raised by a confirm action, whose label is its 'confirm'.");
            outputs.Add(new BoardOutputDeclaration(member.Name, fromId, on, label));
        }
        return outputs;
    }

    private static List<BoardLink> ReadLinks(JsonElement root, List<BoardView> views, Func<string, DataShape?> shape)
    {
        var links = new List<BoardLink>();
        if (!root.TryGetProperty("links", out var list))
            return links;
        if (list.ValueKind != JsonValueKind.Array)
            throw new BoardRefusedException("links", "A Board lists its links in 'links'.");
        var l = 0;
        foreach (var link in list.EnumerateArray())
        {
            var path = $"links[{l++}]";
            if (link.ValueKind != JsonValueKind.Object)
                throw new BoardRefusedException(path, "A link is an object.");
            Members(link, LinkKeys, path, "A link");
            if (!link.TryGetProperty("from", out var fromValue) || fromValue.ValueKind != JsonValueKind.String || fromValue.GetString() is not { Length: > 0 } fromId)
                throw new BoardRefusedException($"{path}.from", "A link names the view whose selection narrows in 'from'.");
            var from = views.FirstOrDefault(v => v.Id == fromId)
                ?? throw new BoardRefusedException($"{path}.from", $"The link starts at view '{fromId}', which the Board does not have.");
            if (from is not BoardGridView fromGrid)
                throw new BoardRefusedException($"{path}.from", $"The link starts at view '{fromId}', a {KindOf(from)}; a link starts at a grid, whose selected rows narrow.");

            var to = Names(link, "to", $"{path}.to", "A link names the views it narrows, in 'to'.");
            for (var t = 0; t < to.Count; t++)
            {
                if (to[t] == fromId)
                    throw new BoardRefusedException($"{path}.to[{t}]", $"The link narrows its own view '{fromId}'; a view's selection narrows other views.");
                if (!views.Any(v => v.Id == to[t]))
                    throw new BoardRefusedException($"{path}.to[{t}]", $"The link narrows view '{to[t]}', which the Board does not have.");
            }

            var on = Names(link, "on", $"{path}.on", "A link names the columns it compares, in 'on'.");
            var fromShape = shape(fromGrid.Grid.Data)!;
            for (var c = 0; c < on.Count; c++)
            {
                if (!fromShape.TryGetColumn(on[c], out var kind))
                    throw new BoardRefusedException($"{path}.on[{c}]", $"The link compares column '{on[c]}', which input '{fromGrid.Grid.Data}' of view '{fromId}' does not declare.");
                foreach (var target in to)
                {
                    foreach (var data in views.First(v => v.Id == target).Data)
                    {
                        if (!shape(data)!.TryGetColumn(on[c], out var other))
                            throw new BoardRefusedException($"{path}.on[{c}]", $"The link compares column '{on[c]}', which input '{data}' of view '{target}' does not declare.");
                        if (other.Kind != kind.Kind)
                        {
                            throw new BoardRefusedException($"{path}.on[{c}]", $"The link compares column '{on[c]}', a {BoardInput.NameOf(kind.Kind)} in input "
                                + $"'{fromGrid.Grid.Data}' and a {BoardInput.NameOf(other.Kind)} in input '{data}'; a value is compared only with one of its kind.");
                        }
                    }
                }
            }
            links.Add(new BoardLink(fromId, to, on));

            // Links run one way: a view narrowed, directly or along other links, by the view it would
            // narrow would drop its own selection each time it narrowed, and never settle.
            foreach (var target in to)
            {
                if (Reaches(links, target, fromId))
                    throw new BoardRefusedException($"{path}.to", $"The link narrows view '{target}', which already narrows view '{fromId}'; links run one way.");
            }
        }
        return links;
    }

    private static bool Reaches(List<BoardLink> links, string from, string to)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var next = new Stack<string>([from]);
        while (next.TryPop(out var at))
        {
            if (at == to)
                return true;
            if (!seen.Add(at))
                continue;
            foreach (var link in links.Where(k => k.From == at))
            {
                foreach (var target in link.To)
                    next.Push(target);
            }
        }
        return false;
    }

    private static List<string> Names(JsonElement owner, string member, string path, string missing)
    {
        if (!owner.TryGetProperty(member, out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            throw new BoardRefusedException(path, missing);
        var names = new List<string>();
        var i = 0;
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { Length: > 0 } name)
                throw new BoardRefusedException($"{path}[{i}]", "A name is text.");
            if (names.Contains(name))
                throw new BoardRefusedException($"{path}[{i}]", $"'{name}' is named twice.");
            names.Add(name);
            i++;
        }
        return names;
    }

    private static string KindOf(BoardView view) => view switch
    {
        BoardGridView => "grid",
        BoardPivotView => "pivot",
        _ => "chart",
    };

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
