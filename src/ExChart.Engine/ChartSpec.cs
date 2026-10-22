using System.Globalization;
using System.Text.Json;

namespace ExChart.Engine;

/// <summary>
/// A Chart Spec that cannot be drawn (ADR-0320): its message names the path in the spec and the reason,
/// in one sentence a host returns to the AI that wrote it, so the AI can correct the spec.
/// </summary>
public sealed class ChartSpecRefusedException : Exception
{
    /// <summary>A refusal at <paramref name="path"/>, said by <paramref name="message"/>.</summary>
    public ChartSpecRefusedException(string path, string message) : base(message) => Path = path;

    /// <summary>Where in the spec it was refused, such as <c>series[1].kind</c>; empty for the spec as a whole.</summary>
    public string Path { get; }
}

/// <summary>A Chart Spec read: its title, and the Chart Definition or the 3D surface it describes.</summary>
public sealed record ChartSpecReading
{
    private readonly ChartDefinition? _definition;

    /// <summary>A spec of series, read.</summary>
    /// <param name="title">The chart's title, or empty when the spec gives none.</param>
    /// <param name="definition">The chart, its points read from the tables the spec names.</param>
    public ChartSpecReading(string title, ChartDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Title = title;
        _definition = definition;
    }

    /// <summary>A spec of a 3D surface, read (ADR-0274).</summary>
    /// <param name="title">The surface's title, or empty when the spec gives none.</param>
    /// <param name="surface">The surface, its points read from the tables the spec names.</param>
    public ChartSpecReading(string title, SurfaceDefinition surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Title = title;
        Surface = surface;
    }

    /// <summary>The chart's title, or empty when the spec gives none.</summary>
    public string Title { get; }

    /// <summary>The chart of series, its points read from the tables the spec names.</summary>
    /// <exception cref="InvalidOperationException">The spec is of a 3D surface, which <see cref="Surface"/> holds: a chart of series read from it would be no chart at all (spine rule 1).</exception>
    public ChartDefinition Definition => _definition
        ?? throw new InvalidOperationException("This Chart Spec is of a 3D surface: draw its Surface with ExSurface; it has no chart of series.");

    /// <summary>The 3D surface a spec of one describes (ADR-0274), drawn by <c>ExSurface</c>; null for a spec of series.</summary>
    public SurfaceDefinition? Surface { get; }
}

/// <summary>The tool an AI calls with a Chart Spec, as a host hands it to a model (ADR-0320).</summary>
/// <param name="Name">The tool's name.</param>
/// <param name="Description">What it does, for the model.</param>
/// <param name="InputSchema">The JSON Schema of the Chart Spec.</param>
public sealed record ChartSpecTool(string Name, string Description, string InputSchema);

/// <summary>What the tool answers the AI.</summary>
/// <param name="Accepted">Whether the chart can be drawn.</param>
/// <param name="Text">The refusal, or the chart said in words.</param>
/// <param name="Reading">The spec read, when it was accepted.</param>
public sealed record ChartSpecAnswer(bool Accepted, string Text, ChartSpecReading? Reading);

/// <summary>
/// The Chart Spec (ADR-0320): a chart said in JSON, as an AI writes it. It names Data Handles and their
/// columns and never holds a number; each series says its Series Kind; what cannot be drawn is refused
/// with the path and the reason.
/// </summary>
public static class ChartSpec
{
    /// <summary>The version of the Chart Spec this reader reads.</summary>
    public const int Version = 1;

    internal static readonly string[] RootKeys = ["version", "title", "axis", "series", "surface", "notes"];
    internal static readonly string[] SurfaceKeys =
        ["data", "x", "y", "z", "xTitle", "yTitle", "zTitle", "xScale", "yScale", "zScale", "centre", "method", "interpolated", "key", "triangles"];
    internal static readonly string[] TriangleKeys = ["data", "a", "b", "c"];
    internal static readonly string[] AxisKeys = ["values", "title", "scale", "packed"];
    internal static readonly string[] SeriesKeys =
        ["id", "name", "kind", "data", "x", "y", "open", "high", "low", "close", "volume", "label", "panel", "axis", "fromZero", "step", "colour", "size", "key", "edges", "tooltip", "interval", "xInterval"];
    internal static readonly string[] EdgeKeys = ["data", "from", "to"];
    internal static readonly string[] ColourKeys = ["column", "scale", "centre", "title"];
    internal static readonly string[] SizeKeys = ["column", "title"];
    internal static readonly string[] IntervalKeys = ["lower", "upper", "meaning", "style"];
    private static readonly string[] PriceColumns = ["open", "high", "low", "close", "volume"];

    private static readonly Dictionary<string, SeriesKind> Kinds = new(StringComparer.Ordinal)
    {
        ["level"] = SeriesKind.Level,
        ["rateOverInterval"] = SeriesKind.RateOverInterval,
        ["amountPerPeriod"] = SeriesKind.AmountPerPeriod,
        ["observation"] = SeriesKind.Observation,
        ["priceRange"] = SeriesKind.PriceRange,
    };

    internal static IEnumerable<string> KindNames => Kinds.Keys;

    /// <summary>The tool an AI calls to draw a chart: its name, what it does, and the JSON Schema of its input.</summary>
    public static ChartSpecTool Tool { get; } = new("draw_chart",
        "Draws a chart of data a capability already made. Name each table by the Data Handle the capability returned, and its columns; "
        + "never write the values themselves. Every series says its kind: level (a quantity at an instant), rateOverInterval "
        + "(a rate constant over an interval), amountPerPeriod (a total over its interval), observation (one thing measured "
        + "on two quantities, never joined) or priceRange (open, high, low and close); a level or an observation may carry an interval "
        + "read from lower and upper columns, with its meaning. A 3D surface is a spec with a surface in place of axis and series: "
        + "its x, y and z columns on a grid, or a mesh of the nodes' keys and a table of triangles. A spec that cannot be drawn is refused "
        + "with the path and the reason; an accepted one is answered with the chart said in words, to check against what was meant.",
        Schema);

    /// <summary>
    /// Reads <paramref name="json"/> and answers as the tool answers the AI: the refusal, or the chart said
    /// in words (ADR-0275) with how many pixels were thinned and how many points are missing.
    /// </summary>
    /// <param name="json">The Chart Spec.</param>
    /// <param name="tables">The host's lookup from a Data Handle to its table, null for a handle it does not know.</param>
    /// <param name="metrics">The size the chart is said at; by default 800 by 400 CSS pixels.</param>
    public static ChartSpecAnswer Answer(string json, Func<string, IChartData?> tables, ChartMetrics? metrics = null)
    {
        ChartSpecReading reading;
        try
        {
            reading = Read(json, tables);
        }
        catch (ChartSpecRefusedException e)
        {
            return new ChartSpecAnswer(false, e.Message, null);
        }
        // A surface is said as a screen reader hears it (ADR-0275), with why a scale it asked for is not drawn.
        if (reading.Surface is { } surface)
            return new ChartSpecAnswer(true, string.Join(" ",
                new[] { SurfaceMesh.Summary(surface, reading.Title, null) }.Concat(surface.AllNotes.Except(surface.Notes).Where(n => !n.StartsWith("Interpolated by", StringComparison.Ordinal)))), reading);
        ChartScene scene;
        try
        {
            scene = ChartScene.Build(reading.Definition, metrics ?? new ChartMetrics(800, 400));
        }
        catch (ChartRefusedException e)
        {
            return new ChartSpecAnswer(false, e.Message, null);
        }
        var parts = new List<string> { ChartSummary.Of(reading.Definition, scene, ChartView.All, reading.Title) };
        var thinned = scene.Panels.SelectMany(p => p.Series).Sum(s => s.Thinned.Count);
        parts.Add(thinned == 0 ? "No pixel holds more than one point."
            : $"{thinned} {(thinned == 1 ? "pixel holds" : "pixels each hold")} more than one point, and say{(thinned == 1 ? "s" : "")} its range.");
        var missing = reading.Definition.Series.Sum(s => s.Points.Count(p => p.Y is null));
        parts.Add(missing == 0 ? "No point is missing." : $"{missing} {(missing == 1 ? "point is" : "points are")} missing, drawn as {(missing == 1 ? "a gap" : "gaps")}.");
        parts.AddRange(scene.Notes.Except(reading.Definition.Notes));
        return new ChartSpecAnswer(true, string.Join(" ", parts), reading);
    }

    /// <summary>
    /// Reads <paramref name="json"/> into a Chart Definition, taking each Data Handle's table from
    /// <paramref name="tables"/>, which returns null for a handle it does not know.
    /// </summary>
    /// <exception cref="ChartSpecRefusedException">The spec cannot be drawn; the message says where and why.</exception>
    public static ChartSpecReading Read(string json, Func<string, IChartData?> tables)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(tables);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ChartSpecRefusedException("", $"The Chart Spec is not JSON: {e.Message}");
        }
        using (document)
            return new Reader(tables).Read(document.RootElement);
    }

    /// <summary>
    /// Reads <paramref name="json"/> through Chart Sources (ADR-0344): each series asks the source of its
    /// Data Handle for its own column, from <paramref name="from"/> to <paramref name="to"/> at
    /// <paramref name="pixels"/> columns, all at once, and is read from the points the source kept.
    /// </summary>
    /// <exception cref="ChartSpecRefusedException">The spec cannot be drawn from Chart Sources, or the
    /// answers for one Data Handle name different versions of its data; the message says where and why.</exception>
    public static async Task<ChartSourceReading> ReadAsync(string json, Func<string, IChartSource?> sources, int pixels,
        double? from = null, double? to = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels, 1);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ChartSpecRefusedException("", $"The Chart Spec is not JSON: {e.Message}");
        }
        using (document)
        {
            var (values, asked) = ChartSource.Asked(document.RootElement);
            var answers = new ChartSourceAnswer[asked.Count];
            if (asked.Count > 0)
            {
                var asks = new Task<ChartSourceAnswer>[asked.Count];
                for (var i = 0; i < asked.Count; i++)
                {
                    var (data, x, y) = asked[i];
                    var source = sources(data) ?? throw new ChartSpecRefusedException($"series[{i}].data", $"series[{i}].data \"{data}\" is not known: name a Data Handle a capability returned.");
                    asks[i] = source.ThinAsync(new ChartSourceQuery(data, x, y, values, from, to, pixels), cancellationToken).AsTask();
                }
                answers = await Task.WhenAll(asks).ConfigureAwait(false);
            }
            var versions = new Dictionary<string, long>(StringComparer.Ordinal);
            for (var i = 0; i < asked.Count; i++)
            {
                if (versions.TryGetValue(asked[i].Data, out var seen) && seen != answers[i].Version)
                    throw new ChartSpecRefusedException($"series[{i}].data", $"series[{i}].data \"{asked[i].Data}\" answered version {answers[i].Version}, and an earlier series of it version {seen}: one chart is never drawn from two moments of its data.");
                versions[asked[i].Data] = answers[i].Version;
            }
            var reading = new Reader(_ => null, asked.Count == 0 ? null : answers).Read(document.RootElement);
            return new ChartSourceReading(reading, versions);
        }
    }

    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Chart Spec",
          "type": "object",
          "additionalProperties": false,
          "required": ["version"],
          "oneOf": [ { "required": ["axis", "series"] }, { "required": ["surface"] } ],
          "properties": {
            "version": { "const": 1, "description": "The version of the Chart Spec." },
            "title": { "type": "string", "description": "The chart's title." },
            "axis": { "$ref": "#/$defs/axis" },
            "series": { "type": "array", "minItems": 1, "items": { "$ref": "#/$defs/series" } },
            "surface": { "$ref": "#/$defs/surface" },
            "notes": { "type": "array", "items": { "type": "string" }, "description": "Lines said under the chart, such as the data's source." }
          },
          "$defs": {
            "surface": {
              "type": "object",
              "additionalProperties": false,
              "required": ["data", "x", "y", "z"],
              "description": "A 3D surface, in place of axis and series: a value at each x and y, on a grid the rows lie on, or on the Consumer's triangles. Nothing is interpolated.",
              "properties": {
                "data": { "type": "string", "description": "The Data Handle of the table of its points, or its nodes." },
                "x": { "type": "string", "description": "The column of each point's x." },
                "y": { "type": "string", "description": "The column of each point's y." },
                "z": { "type": "string", "description": "The column of each point's value; null is a hole." },
                "xTitle": { "type": "string", "description": "What x is; by default its column's name." },
                "yTitle": { "type": "string", "description": "What y is; by default its column's name." },
                "zTitle": { "type": "string", "description": "What the values are; by default their column's name." },
                "xScale": { "enum": ["categories", "proportional", "logarithmic"], "description": "How x values are placed: a grid's evenly by default, a mesh's proportionally." },
                "yScale": { "enum": ["categories", "proportional", "logarithmic"], "description": "How y values are placed." },
                "zScale": { "enum": ["proportional", "logarithmic"], "description": "How the values are placed up." },
                "centre": { "type": "number", "description": "The value the colours diverge from, such as 0 for a P&L; without it they run from the lowest value to the highest." },
                "method": { "type": "string", "description": "The method the capability interpolated values with, which the chart says; never one the chart applies." },
                "interpolated": { "type": "string", "description": "A grid's column of true where a value was interpolated by the method; without it every value is." },
                "key": { "type": "string", "description": "A mesh's column of node keys, which its triangles name." },
                "triangles": { "$ref": "#/$defs/triangles" }
              }
            },
            "triangles": {
              "type": "object",
              "additionalProperties": false,
              "required": ["data", "a", "b", "c"],
              "description": "A mesh's triangles, from a table of their own, each naming three nodes by key; each is drawn flat through its corners.",
              "properties": {
                "data": { "type": "string", "description": "The Data Handle of the triangles' table." },
                "a": { "type": "string", "description": "Its column of each triangle's first node." },
                "b": { "type": "string", "description": "Its column of the second." },
                "c": { "type": "string", "description": "Its column of the third." }
              }
            },
            "axis": {
              "type": "object",
              "additionalProperties": false,
              "required": ["values"],
              "properties": {
                "values": { "enum": ["dates", "numbers", "labels"], "description": "What the positions across are." },
                "title": { "type": "string" },
                "scale": { "enum": ["proportional", "categories", "logarithmic"], "description": "How positions are placed; by default labels are categories and the rest proportional." },
                "packed": { "type": "boolean", "description": "A dates axis with a priceRange: one slot a session, the days nobody traded skipped." }
              }
            },
            "series": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "name", "kind", "data", "x"],
              "properties": {
                "id": { "type": "string", "description": "Its identity, unique in the chart." },
                "name": { "type": "string", "description": "Its name in the legend and the tooltip." },
                "kind": { "enum": ["level", "rateOverInterval", "amountPerPeriod", "observation", "priceRange"] },
                "data": { "type": "string", "description": "The Data Handle of the table its values are read from." },
                "x": { "type": "string", "description": "The column of its positions across." },
                "y": { "type": "string", "description": "The column of its values; every kind but priceRange." },
                "open": { "type": "string", "description": "A priceRange's open column, optional." },
                "high": { "type": "string", "description": "A priceRange's high column." },
                "low": { "type": "string", "description": "A priceRange's low column." },
                "close": { "type": "string", "description": "A priceRange's close column." },
                "volume": { "type": "string", "description": "A priceRange's volume column, optional." },
                "label": { "type": "string", "description": "A column naming each point, for its tooltip." },
                "panel": { "type": "string", "description": "The panel it is drawn in; series naming the same panel share it." },
                "axis": { "enum": ["left", "right"], "description": "The vertical axis it reads on." },
                "fromZero": { "type": "boolean", "description": "Whether its axis reaches zero; false for a rate or a volatility." },
                "step": { "enum": ["preceding", "following"], "description": "Which interval a step's point stands for." },
                "colour": { "$ref": "#/$defs/colour" },
                "size": { "$ref": "#/$defs/size" },
                "key": { "type": "string", "description": "An observation's column of node keys, which its edges name." },
                "edges": { "$ref": "#/$defs/edges" },
                "tooltip": { "type": "array", "items": { "type": "string" }, "description": "An observation's columns its tooltip says beside its values, such as a node's probability; no other column is said." },
                "interval": { "$ref": "#/$defs/interval", "description": "A level's or an observation's vertical interval: a lower and an upper bound a row, read from columns, never computed." },
                "xInterval": { "$ref": "#/$defs/interval", "description": "An observation's horizontal interval, its bounds in the units across." }
              }
            },
            "interval": {
              "type": "object",
              "additionalProperties": false,
              "required": ["lower", "upper", "meaning"],
              "properties": {
                "lower": { "type": "string", "description": "The column of each row's lower bound." },
                "upper": { "type": "string", "description": "The column of each row's upper bound." },
                "meaning": { "type": "string", "description": "What the bounds are, such as 95% CI, ±1σ or Bid–Ask; said in the legend and the tooltip." },
                "style": { "enum": ["whiskers", "band"], "description": "Whiskers at each point, or a level's band joining neighbours; by default whiskers." }
              }
            },
            "edges": {
              "type": "object",
              "additionalProperties": false,
              "required": ["data", "from", "to"],
              "description": "An observation's edges, from a table of its own: each a line between two nodes, which stand where their points do. Nothing is laid out.",
              "properties": {
                "data": { "type": "string", "description": "The Data Handle of the edges' table." },
                "from": { "type": "string", "description": "Its column of the keys edges start at." },
                "to": { "type": "string", "description": "Its column of the keys edges end at." }
              }
            },
            "colour": {
              "type": "object",
              "additionalProperties": false,
              "required": ["column", "scale"],
              "description": "An observation's colour read from a column, in bands; its colour bar is always drawn. Never a colour value.",
              "properties": {
                "column": { "type": "string", "description": "The column of one value a point." },
                "scale": { "enum": ["sequential", "diverging"], "description": "sequential from the lowest to the highest; diverging from a centre." },
                "centre": { "type": "number", "description": "A diverging scale's value that means nothing, such as 0 for a P&L or 1 for a ratio." },
                "title": { "type": "string", "description": "What the values are; by default the column's name." }
              }
            },
            "size": {
              "type": "object",
              "additionalProperties": false,
              "required": ["column"],
              "description": "An observation's size read from a column, its area in proportion to the value; its key is always drawn.",
              "properties": {
                "column": { "type": "string", "description": "The column of one value a point, never negative." },
                "title": { "type": "string", "description": "What the values are; by default the column's name." }
              }
            }
          }
        }
        """;

    // A reader through Chart Sources is handed each series' answer, in the series' order (ReadAsync).
    private sealed class Reader(Func<string, IChartData?> tables, IReadOnlyList<ChartSourceAnswer>? answers = null)
    {
        private int _series;

        // A label axis' categories, in the order the data first shows them (ADR-0251).
        private readonly List<string> _labels = [];
        private readonly Dictionary<string, int> _labelIndex = new(StringComparer.Ordinal);

        public ChartSpecReading Read(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                throw Refuse("", "The Chart Spec is a JSON object.");
            Known(root, "", RootKeys);
            if (!root.TryGetProperty("version", out var version))
                throw Refuse("version", $"version is missing: a Chart Spec says which version it is written in; this reader reads version {Version}.");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != Version)
                throw Refuse("version", $"version {version.GetRawText()} is not known: this reader reads version {Version}, and never reads a spec as the nearest version.");
            var title = String(root, "title", "") ?? "";
            if (root.TryGetProperty("surface", out var surfaceElement))
            {
                foreach (var other in new[] { "axis", "series" })
                    if (root.TryGetProperty(other, out _))
                        throw Refuse(other, $"{other} belongs to a chart of series; a spec draws a surface or series, and its surface names its own x, y and z.");
                var surface = Surface(surfaceElement, "surface", Notes(root));
                return new ChartSpecReading(title, surface);
            }
            var axisElement = Object(root, "axis", "");
            Known(axisElement, "axis", AxisKeys);
            var values = Choice(axisElement, "values", "axis", new Dictionary<string, AxisValues>
            {
                ["dates"] = AxisValues.Dates,
                ["numbers"] = AxisValues.Numbers,
                ["labels"] = AxisValues.Labels,
            }, "what the positions across are");
            AxisScale? scale = axisElement.TryGetProperty("scale", out _)
                ? Choice(axisElement, "scale", "axis", new Dictionary<string, AxisScale>
                {
                    ["proportional"] = AxisScale.Proportional,
                    ["categories"] = AxisScale.Categories,
                    ["logarithmic"] = AxisScale.Logarithmic,
                }, "how the positions are placed")
                : null;
            if (!root.TryGetProperty("series", out var seriesArray) || seriesArray.ValueKind != JsonValueKind.Array || seriesArray.GetArrayLength() == 0)
                throw Refuse("series", "series is missing: a Chart Spec lists at least one series.");
            var series = new List<ChartSeries>();
            var i = 0;
            foreach (var s in seriesArray.EnumerateArray())
                series.Add(Series(s, $"series[{i++}]", values));
            var notes = Notes(root);
            var axis = new ChartAxis
            {
                Values = values,
                Title = String(axisElement, "title", "axis") ?? "",
                Scale = scale,
                Labels = _labels.ToArray(),
                Packed = Bool(axisElement, "packed", "axis") ?? false,
            };
            // Each series is checked on its own first, so a refusal names the series it belongs to.
            for (var n = 0; n < series.Count; n++)
            {
                var duplicate = series.FindIndex(s => s.Id == series[n].Id);
                if (duplicate < n)
                    throw Refuse($"series[{n}].id", $"series[{n}].id \"{series[n].Id}\" is series[{duplicate}]'s too: each series needs an identity of its own.");
                Defined($"series[{n}]", () => new ChartDefinition(axis, [series[n]]));
            }
            return new ChartSpecReading(title, Defined("", () => new ChartDefinition(axis, series, notes)));
        }

        private static List<string> Notes(JsonElement root)
        {
            var notes = new List<string>();
            if (root.TryGetProperty("notes", out var notesElement))
            {
                if (notesElement.ValueKind != JsonValueKind.Array)
                    throw Refuse("notes", "notes is a list of lines.");
                var n = 0;
                foreach (var line in notesElement.EnumerateArray())
                {
                    if (line.ValueKind != JsonValueKind.String)
                        throw Refuse($"notes[{n}]", $"notes[{n}] is a line of text.");
                    notes.Add(line.GetString()!);
                    n++;
                }
            }
            return notes;
        }

        // A 3D surface (ADR-0274): a grid its rows lie on, or the Consumer's triangles over keyed nodes
        // (ADR-0311); its colours' centre (ADR-0310) and its scales (ADR-0312). Nothing is interpolated:
        // rows that form no grid are refused, as SurfaceDefinition.FromPoints refuses them.
        private SurfaceDefinition Surface(JsonElement s, string path, IReadOnlyList<string> notes)
        {
            if (s.ValueKind != JsonValueKind.Object) throw Refuse(path, $"{path} is a JSON object.");
            Known(s, path, SurfaceKeys);
            var handle = String(s, "data", path) ?? throw Missing(path, "data", "the Data Handle of the table of its points");
            var table = tables(handle) ?? throw Refuse($"{path}.data", $"{path}.data \"{handle}\" is not known: name a Data Handle a capability returned.");
            var (x, y, z) = (Column(s, "x", path, handle, table, required: true)!, Column(s, "y", path, handle, table, required: true)!, Column(s, "z", path, handle, table, required: true)!);
            var (xTitle, yTitle, zTitle) = (String(s, "xTitle", path) ?? x, String(s, "yTitle", path) ?? y, String(s, "zTitle", path) ?? z);
            var scales = new Dictionary<string, AxisScale> { ["categories"] = AxisScale.Categories, ["proportional"] = AxisScale.Proportional, ["logarithmic"] = AxisScale.Logarithmic };
            AxisScale? ScaleOf(string key) => s.TryGetProperty(key, out _) ? Choice(s, key, path, scales, "how its values are placed") : null;
            var (xScale, yScale) = (ScaleOf("xScale"), ScaleOf("yScale"));
            var zScale = s.TryGetProperty("zScale", out _)
                ? Choice(s, "zScale", path, new Dictionary<string, AxisScale> { ["proportional"] = AxisScale.Proportional, ["logarithmic"] = AxisScale.Logarithmic }, "how the values are placed up")
                : AxisScale.Proportional;
            double? centre = null;
            if (s.TryGetProperty("centre", out var centreElement))
            {
                if (centreElement.ValueKind != JsonValueKind.Number || !centreElement.TryGetDouble(out var mid) || !double.IsFinite(mid))
                    throw Refuse($"{path}.centre", $"{path}.centre is a finite number.");
                centre = mid;
            }
            var method = String(s, "method", path);
            var rows = table.RowCount;
            var zs = new ChartValue?[rows];
            for (var r = 0; r < rows; r++)
                zs[r] = ValueOf(table.Value(z, r), $"{path}.z", handle, r);

            SurfaceDefinition surface;
            if (s.TryGetProperty("triangles", out var trianglesElement))
            {
                var at = $"{path}.triangles";
                foreach (var gridOnly in new[] { "interpolated", "method" })
                    if (s.TryGetProperty(gridOnly, out _))
                        throw Refuse($"{path}.{gridOnly}", $"{path}.{gridOnly} belongs to a grid; a mesh's nodes are its values, and between them it is read linearly on its triangles, which it says.");
                var key = Column(s, "key", path, handle, table, required: false)
                    ?? throw Missing(path, "key", "a triangle names its nodes by key, so the nodes say which column holds theirs");
                var nodes = new SurfaceNode[rows];
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var r = 0; r < rows; r++)
                {
                    var name = Key(table.Value(key, r), $"{path}.key", handle, r);
                    if (!index.TryAdd(name, r))
                        throw Refuse($"{path}.key", $"{path}.key: rows {index[name] + 1} and {r + 1} of \"{handle}\" are both keyed {name}; a triangle could not say which it names.");
                    nodes[r] = new SurfaceNode(MeshPosition(table.Value(x, r), $"{path}.x", handle, r), MeshPosition(table.Value(y, r), $"{path}.y", handle, r), zs[r]);
                }
                if (trianglesElement.ValueKind != JsonValueKind.Object) throw Refuse(at, $"{at} is a JSON object.");
                Known(trianglesElement, at, TriangleKeys);
                var triangleHandle = String(trianglesElement, "data", at) ?? throw Missing(at, "data", "the Data Handle of the triangles' table");
                var triangleTable = tables(triangleHandle) ?? throw Refuse($"{at}.data", $"{at}.data \"{triangleHandle}\" is not known: name a Data Handle a capability returned.");
                var corners = new[] { "a", "b", "c" }.Select(c => (Key: c, Column: Column(trianglesElement, c, at, triangleHandle, triangleTable, required: true)!)).ToArray();
                var triangles = new SurfaceTriangle[triangleTable.RowCount];
                for (var r = 0; r < triangles.Length; r++)
                {
                    var n = corners.Select(c =>
                    {
                        var name = Key(triangleTable.Value(c.Column, r), $"{at}.{c.Key}", triangleHandle, r);
                        return index.TryGetValue(name, out var i) ? i
                            : throw Refuse($"{at}.{c.Key}", $"{at}.{c.Key}: row {r + 1} of \"{triangleHandle}\" names node {name}, but no node is keyed {name}.");
                    }).ToArray();
                    triangles[r] = new SurfaceTriangle(n[0], n[1], n[2]);
                }
                foreach (var (key0, scale) in new[] { ("xScale", xScale), ("yScale", yScale) })
                    if (scale == AxisScale.Categories)
                        throw Refuse($"{path}.{key0}", $"{path}.{key0} \"categories\": a mesh's nodes are placed by their position, proportionally or logarithmically.");
                surface = Defined(path, () => SurfaceDefinition.FromTriangles(nodes, triangles, xTitle, yTitle, zTitle,
                    xScale: xScale ?? AxisScale.Proportional, yScale: yScale ?? AxisScale.Proportional, notes: notes));
            }
            else
            {
                if (s.TryGetProperty("key", out _))
                    throw Refuse($"{path}.key", $"{path}.key belongs to a mesh, whose triangles name its nodes; a grid's points stand at their x and y.");
                var xs = GridValues(table, x, $"{path}.x", $"{path}.xScale", handle, xScale);
                var ys = GridValues(table, y, $"{path}.y", $"{path}.yScale", handle, yScale);
                var xIndex = xs.Values.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);
                var yIndex = ys.Values.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i);
                var grid = ys.Values.Select(_ => new ChartValue?[xs.Values.Count]).ToArray();
                var marked = ys.Values.Select(_ => new bool[xs.Values.Count]).ToArray();
                var taken = ys.Values.Select(_ => new bool[xs.Values.Count]).ToArray();
                var interpolated = Column(s, "interpolated", path, handle, table, required: false);
                if (interpolated is not null && method is null)
                    throw Missing(path, "method", "a surface that marks values interpolated names the method they were made with, which the chart says (ADR-0257)");
                for (var r = 0; r < rows; r++)
                {
                    var (i, j) = (xIndex[xs.Of[r]], yIndex[ys.Of[r]]);
                    if (taken[j][i])
                        throw Refuse($"{path}.x", $"{path}: two rows of \"{handle}\" lie at {xTitle} {Write(xs.Of[r])} and {yTitle} {Write(ys.Of[r])}, the second row {r + 1}; a grid point holds one value.");
                    taken[j][i] = true;
                    grid[j][i] = zs[r];
                    if (interpolated is not null)
                        marked[j][i] = table.Value(interpolated, r) switch
                        {
                            bool b => b,
                            null => false,
                            var v => throw Refuse($"{path}.interpolated", $"{path}.interpolated: row {r + 1} of \"{handle}\" holds \"{Text(v)}\", which is not true or false."),
                        };
                }
                // A row that alone holds its x, or its y, joins a surface only by interpolation, which is not the chart's.
                foreach (var (axisValues, of, title, other, key0) in new[] { (xs.Values, xs.Of, xTitle, yTitle, "x"), (ys.Values, ys.Of, yTitle, xTitle, "y") })
                    foreach (var v in axisValues)
                        if (of.Count(o => Equals(o, v)) < 2)
                            throw Refuse($"{path}.{key0}", $"{path}.{key0}: {title} {Write(v)} has a value at only one {other}. Rows that form no grid join a surface only by interpolation, which the chart does not choose: have the capability interpolate them onto a grid and name its method, or hand its own triangles.");
                var xAxis = new SurfaceAxis(xTitle, xs.Values, Format(xs.Values)) { Scale = xScale ?? AxisScale.Categories };
                var yAxis = new SurfaceAxis(yTitle, ys.Values, Format(ys.Values)) { Scale = yScale ?? AxisScale.Categories };
                surface = Defined(path, () => new SurfaceDefinition(xAxis, yAxis, grid, zTitle, null, method,
                    interpolated is null ? null : marked.Select(m => (IReadOnlyList<bool>)m).ToArray(), notes));
            }
            return Defined(path, () => surface with { ZScale = zScale, Centre = centre });
        }

        // A grid axis' values, each row's value among them: numbers and dates in order, labels as the data
        // first shows them; one kind a column.
        private static (IReadOnlyList<object> Values, object[] Of) GridValues(IChartData table, string column, string path, string scalePath, string handle, AxisScale? scale)
        {
            var of = new object[table.RowCount];
            Type? kind = null;
            for (var r = 0; r < of.Length; r++)
            {
                var value = table.Value(column, r);
                object placed = value switch
                {
                    null => throw Refuse(path, $"{path}: row {r + 1} of \"{handle}\" has no position; a point needs one."),
                    DateOnly d => d,
                    DateTime t when t.TimeOfDay == TimeSpan.Zero => DateOnly.FromDateTime(t),
                    DateTime => throw Refuse(path, $"{path}: row {r + 1} of \"{handle}\" holds {Text(value)}, a time of day; a surface places days."),
                    string label => label,
                    _ => ValueOf(value, path, handle, r)!.Value.ToDouble(),
                };
                kind ??= placed.GetType();
                if (placed.GetType() != kind)
                    throw Refuse(path, $"{path}: row {r + 1} of \"{handle}\" holds \"{Text(value)}\", but the rows before it hold {(kind == typeof(double) ? "numbers" : kind == typeof(DateOnly) ? "dates" : "labels")}; an axis places one kind of value.");
                of[r] = placed;
            }
            if (scale is AxisScale.Proportional or AxisScale.Logarithmic && kind is not null && kind != typeof(double))
                throw Refuse(scalePath, $"{scalePath}: only numbers are placed by value; {path}'s {(kind == typeof(DateOnly) ? "dates" : "labels")} are placed evenly, as categories.");
            var distinct = of.Distinct().ToList();
            if (kind != typeof(string))
                distinct.Sort(Comparer<object>.Default);
            return (distinct, of);
        }

        private static Func<object, string>? Format(IReadOnlyList<object> values) =>
            values.Count > 0 && values[0] is DateOnly ? o => ((DateOnly)o).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

        private static string Write(object value) => value is DateOnly d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Text(value);

        private static double MeshPosition(object? value, string path, string handle, int row) =>
            ValueOf(value, path, handle, row) is { } v ? v.ToDouble()
            : throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" has no position; a node needs one in the plane.");

        private ChartSeries Series(JsonElement s, string path, AxisValues values)
        {
            if (s.ValueKind != JsonValueKind.Object)
                throw Refuse(path, $"{path} is a JSON object.");
            Known(s, path, SeriesKeys);
            var id = String(s, "id", path) ?? throw Missing(path, "id", "a series' identity, unique in the chart");
            var name = String(s, "name", path) ?? throw Missing(path, "name", "its name in the legend and the tooltip");
            var kindText = String(s, "kind", path)
                ?? throw Missing(path, "kind", "every series says what its numbers are: " + string.Join(", ", Kinds.Keys) + "; there is no default");
            if (!Kinds.TryGetValue(kindText, out var kind))
                throw Refuse($"{path}.kind", $"{path}.kind \"{kindText}\" is not a Series Kind: it is one of {string.Join(", ", Kinds.Keys)}.");
            var handle = String(s, "data", path) ?? throw Missing(path, "data", "the Data Handle of the table its values are read from");
            var answer = answers?[_series++];
            var table = answer?.Points ?? tables(handle) ?? throw Refuse($"{path}.data", $"{path}.data \"{handle}\" is not known: name a Data Handle a capability returned.");
            var x = Column(s, "x", path, handle, table, required: true)!;
            var rows = table.RowCount;
            var positions = new double[rows];
            for (var r = 0; r < rows; r++)
                positions[r] = Position(table.Value(x, r), values, $"{path}.x", handle, r);
            string? label = Column(s, "label", path, handle, table, required: false);
            string? key = Column(s, "key", path, handle, table, required: false);
            var result = new ChartSeries { Id = id, Name = name, Kind = kind };
            if (kind == SeriesKind.PriceRange)
            {
                if (s.TryGetProperty("y", out _))
                    throw Refuse($"{path}.y", $"{path}.y belongs to a series of one value a row; a priceRange reads open, high, low and close.");
                var columns = PriceColumns.ToDictionary(c => c, c => Column(s, c, path, handle, table, required: c is "high" or "low" or "close"));
                var bars = new PriceBar[rows];
                for (var r = 0; r < rows; r++)
                {
                    ChartValue? At(string c) => columns[c] is { } col ? ValueOf(table.Value(col, r), $"{path}.{c}", handle, r) : null;
                    bars[r] = new PriceBar(positions[r], At("open"), At("high"), At("low"), At("close"), At("volume"));
                }
                result = result with { Bars = bars };
            }
            else
            {
                foreach (var c in PriceColumns)
                    if (s.TryGetProperty(c, out _))
                        throw Refuse($"{path}.{c}", $"{path}.{c} belongs to a priceRange; a {kindText} reads one value a row, from y.");
                var y = Column(s, "y", path, handle, table, required: true)!;
                var points = new ChartPoint[rows];
                for (var r = 0; r < rows; r++)
                    points[r] = new ChartPoint(positions[r], ValueOf(table.Value(y, r), $"{path}.y", handle, r))
                    {
                        Name = label is null ? null : Text(table.Value(label, r)),
                        Row = key is null ? null : Key(table.Value(key, r), $"{path}.key", handle, r),
                    };
                result = result with { Points = points };
            }
            if (s.TryGetProperty("edges", out var edgesElement))
            {
                var at = $"{path}.edges";
                if (kind != SeriesKind.Observation)
                    throw Refuse(at, $"{at} belongs to an observation, whose points are the nodes.");
                if (key is null)
                    throw Refuse($"{path}.key", $"{path}.key is missing: an edge names its nodes by key, so the nodes say which column holds theirs.");
                if (edgesElement.ValueKind != JsonValueKind.Object) throw Refuse(at, $"{at} is a JSON object.");
                Known(edgesElement, at, EdgeKeys);
                var edgeHandle = String(edgesElement, "data", at) ?? throw Missing(at, "data", "the Data Handle of the edges' table");
                var edgeTable = tables(edgeHandle) ?? throw Refuse($"{at}.data", $"{at}.data \"{edgeHandle}\" is not known: name a Data Handle a capability returned.");
                var from = Column(edgesElement, "from", at, edgeHandle, edgeTable, required: true)!;
                var to = Column(edgesElement, "to", at, edgeHandle, edgeTable, required: true)!;
                var edges = new ChartEdge[edgeTable.RowCount];
                for (var r = 0; r < edges.Length; r++)
                    edges[r] = new ChartEdge(Key(edgeTable.Value(from, r), $"{at}.from", edgeHandle, r), Key(edgeTable.Value(to, r), $"{at}.to", edgeHandle, r));
                result = result with { Edges = edges };
            }
            if (s.TryGetProperty("interval", out var intervalElement))
            {
                if (kind is not (SeriesKind.Level or SeriesKind.Observation))
                    throw Refuse($"{path}.interval", $"{path}.interval belongs to a level or an observation, whose points the bounds stand at.");
                result = result with { Interval = ReadInterval(intervalElement, $"{path}.interval", handle, table, positions, (v, at, r) => ValueOf(v, at, handle, r)) };
            }
            if (s.TryGetProperty("xInterval", out var xIntervalElement))
            {
                var at = $"{path}.xInterval";
                if (kind != SeriesKind.Observation)
                    throw Refuse(at, $"{at} belongs to an observation, whose points each stand at a position of their own.");
                if (values == AxisValues.Labels)
                    throw Refuse(at, $"{at}: a labels axis has no units for a bound to stand in.");
                result = result with
                {
                    XInterval = ReadInterval(xIntervalElement, at, handle, table, positions,
                        (v, p, r) => v is null ? null : ChartValue.Of(Position(v, values, p, handle, r))),
                };
            }
            if (s.TryGetProperty("tooltip", out var tooltipElement))
                result = result with { Details = Details(tooltipElement, $"{path}.tooltip", kind, handle, table) };
            if (s.TryGetProperty("colour", out var colourElement))
                result = result with { ColourBy = Colour(colourElement, $"{path}.colour", handle, table) };
            if (s.TryGetProperty("size", out var sizeElement))
            {
                var at = $"{path}.size";
                if (sizeElement.ValueKind != JsonValueKind.Object) throw Refuse(at, $"{at} is a JSON object.");
                Known(sizeElement, at, SizeKeys);
                var column = Column(sizeElement, "column", at, handle, table, required: true)!;
                result = result with { SizeBy = new PointSize(String(sizeElement, "title", at) ?? column, Values(table, column, $"{at}.column", handle)) };
            }
            return result with
            {
                Panel = String(s, "panel", path) ?? "",
                Axis = s.TryGetProperty("axis", out _)
                    ? Choice(s, "axis", path, new Dictionary<string, AxisSide> { ["left"] = AxisSide.Left, ["right"] = AxisSide.Right }, "the vertical axis it reads on")
                    : AxisSide.Left,
                FromZero = Bool(s, "fromZero", path),
                Step = s.TryGetProperty("step", out _)
                    ? Choice(s, "step", path, new Dictionary<string, StepSide> { ["preceding"] = StepSide.Preceding, ["following"] = StepSide.Following }, "which interval a step's point stands for")
                    : StepSide.Preceding,
                Thinned = answer?.Thinned ?? [],
            };
        }

        private static PointColour Colour(JsonElement c, string path, string handle, IChartData table)
        {
            if (c.ValueKind != JsonValueKind.Object) throw Refuse(path, $"{path} is a JSON object.");
            Known(c, path, ColourKeys);
            var column = Column(c, "column", path, handle, table, required: true)!;
            var diverging = Choice(c, "scale", path, new Dictionary<string, bool> { ["sequential"] = false, ["diverging"] = true }, "how the colours run");
            double? centre = null;
            if (c.TryGetProperty("centre", out var centreElement))
            {
                if (!diverging)
                    throw Refuse($"{path}.centre", $"{path}.centre belongs to a diverging scale; a sequential one runs from the lowest value to the highest.");
                if (centreElement.ValueKind != JsonValueKind.Number || !centreElement.TryGetDouble(out var mid) || !double.IsFinite(mid))
                    throw Refuse($"{path}.centre", $"{path}.centre is a finite number.");
                centre = mid;
            }
            else if (diverging)
                throw Refuse($"{path}.centre", $"{path}.centre is missing: a diverging scale says the value that means nothing, such as 0 for a P&L or 1 for a ratio.");
            return new PointColour(String(c, "title", path) ?? column, Values(table, column, $"{path}.column", handle)) { Centre = centre };
        }

        // A series' interval: a lower and an upper bound a row, at the row's position (ADR-0303, ADR-0304).
        private static SeriesInterval ReadInterval(JsonElement iv, string path, string handle, IChartData table, double[] positions,
            Func<object?, string, int, ChartValue?> bound)
        {
            if (iv.ValueKind != JsonValueKind.Object) throw Refuse(path, $"{path} is a JSON object.");
            Known(iv, path, IntervalKeys);
            var lower = Column(iv, "lower", path, handle, table, required: true)!;
            var upper = Column(iv, "upper", path, handle, table, required: true)!;
            var meaning = String(iv, "meaning", path);
            if (string.IsNullOrWhiteSpace(meaning))
                throw Missing(path, "meaning", "what the bounds are, such as 95% CI, ±1σ or Bid–Ask; an interval that does not say is not drawn");
            var style = iv.TryGetProperty("style", out _)
                ? Choice(iv, "style", path, new Dictionary<string, IntervalStyle> { ["whiskers"] = IntervalStyle.Whiskers, ["band"] = IntervalStyle.Band }, "how the bounds are drawn")
                : IntervalStyle.Whiskers;
            var bounds = new IntervalBound[table.RowCount];
            for (var r = 0; r < bounds.Length; r++)
                bounds[r] = new IntervalBound(positions[r], bound(table.Value(lower, r), $"{path}.lower", r), bound(table.Value(upper, r), $"{path}.upper", r));
            return new SeriesInterval(meaning, bounds, style);
        }

        // The columns an observation's tooltip says, each one value a row written as the table holds it (ADR-0321).
        private static PointDetail[] Details(JsonElement t, string path, SeriesKind kind, string handle, IChartData table)
        {
            if (kind != SeriesKind.Observation)
                throw Refuse(path, $"{path} belongs to an observation, whose tooltip names one point; a line's tooltip reads every series at a position.");
            if (t.ValueKind != JsonValueKind.Array)
                throw Refuse(path, $"{path} is a list of the columns its tooltip says.");
            var details = new List<PointDetail>();
            var n = 0;
            foreach (var c in t.EnumerateArray())
            {
                var at = $"{path}[{n++}]";
                if (c.ValueKind != JsonValueKind.String)
                    throw Refuse(at, $"{at} names a column, as text.");
                var name = c.GetString()!;
                if (!table.Columns.Contains(name))
                    throw Refuse(at, $"{at}: \"{handle}\" has no column \"{name}\"; its columns are {string.Join(", ", table.Columns)}.");
                if (details.Any(d => d.Title == name))
                    throw Refuse(at, $"{at} names \"{name}\" twice.");
                var values = new string?[table.RowCount];
                for (var r = 0; r < values.Length; r++)
                    values[r] = table.Value(name, r) is { } v ? Text(v) : null;
                details.Add(new PointDetail(name, values));
            }
            return details.ToArray();
        }

        // A key is compared as its text, so a key read as a number in one table meets the same key in another.
        private static string Key(object? value, string path, string handle, int row) =>
            value is null ? throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" has no key.") : Text(value);

        private static ChartValue?[] Values(IChartData table, string column, string path, string handle)
        {
            var values = new ChartValue?[table.RowCount];
            for (var r = 0; r < values.Length; r++)
                values[r] = ValueOf(table.Value(column, r), path, handle, r);
            return values;
        }

        private double Position(object? value, AxisValues values, string path, string handle, int row)
        {
            switch (values)
            {
                case AxisValues.Dates:
                    if (value is DateOnly d) return d.DayNumber;
                    if (value is DateTime t)
                    {
                        if (t.TimeOfDay != TimeSpan.Zero)
                            throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" holds {Text(value)}, a time of day; a dates axis places days.");
                        return DateOnly.FromDateTime(t).DayNumber;
                    }
                    break;
                case AxisValues.Numbers:
                    if (ValueOf(value, path, handle, row) is { } n) return n.ToDouble();
                    break;
                default:
                    if (value is string s)
                    {
                        if (!_labelIndex.TryGetValue(s, out var at))
                        {
                            at = _labels.Count;
                            _labels.Add(s);
                            _labelIndex[s] = at;
                        }
                        return at;
                    }
                    break;
            }
            if (value is null)
                throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" has no position; a point needs one across.");
            var expected = values switch { AxisValues.Dates => "a date", AxisValues.Numbers => "a number", _ => "a label" };
            throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" holds \"{Text(value)}\", but the axis is {values.ToString().ToLowerInvariant()} and needs {expected}.");
        }

        private static ChartValue? ValueOf(object? value, string path, string handle, int row) => value switch
        {
            null => null,
            decimal m => ChartValue.Of(m),
            double d when !double.IsFinite(d) => throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" is {Text(d)}, which is no number; a missing value is null, drawn as a gap."),
            double d => ChartValue.Of(d),
            float f when !float.IsFinite(f) => throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" is {Text(f)}, which is no number; a missing value is null, drawn as a gap."),
            float f => ChartValue.Of((double)f),
            int n => ChartValue.Of((decimal)n),
            long n => ChartValue.Of((decimal)n),
            short n => ChartValue.Of((decimal)n),
            byte n => ChartValue.Of((decimal)n),
            _ => throw Refuse(path, $"{path}: row {row + 1} of \"{handle}\" holds \"{Text(value)}\", which is not a number."),
        };

        private static string? Column(JsonElement s, string key, string path, string handle, IChartData table, bool required)
        {
            var name = String(s, key, path);
            if (name is null)
                return required ? throw Missing(path, key, "the column it is read from") : null;
            if (!table.Columns.Contains(name))
                throw Refuse($"{path}.{key}", $"{path}.{key}: \"{handle}\" has no column \"{name}\"; its columns are {string.Join(", ", table.Columns)}.");
            return name;
        }

        private static T Defined<T>(string path, Func<T> define)
        {
            try
            {
                return define();
            }
            catch (ChartRefusedException e)
            {
                throw Refuse(path, path.Length == 0 ? e.Message : $"{path}: {e.Message}");
            }
        }

        private static void Known(JsonElement o, string path, string[] keys)
        {
            foreach (var p in o.EnumerateObject())
                if (!keys.Contains(p.Name))
                {
                    var at = path.Length == 0 ? p.Name : $"{path}.{p.Name}";
                    var numbers = p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.Array
                        ? " a spec holds no numbers, only the columns they are read from;" : "";
                    throw Refuse(at, $"{at} is not part of a Chart Spec:{numbers} {(path.Length == 0 ? "a spec" : path)} takes {string.Join(", ", keys)}.");
                }
        }

        private static JsonElement Object(JsonElement o, string key, string path)
        {
            var at = path.Length == 0 ? key : $"{path}.{key}";
            if (!o.TryGetProperty(key, out var e))
                throw Refuse(at, $"{at} is missing.");
            if (e.ValueKind != JsonValueKind.Object)
                throw Refuse(at, $"{at} is a JSON object.");
            return e;
        }

        private static string? String(JsonElement o, string key, string path)
        {
            if (!o.TryGetProperty(key, out var e) || e.ValueKind == JsonValueKind.Null) return null;
            var at = path.Length == 0 ? key : $"{path}.{key}";
            return e.ValueKind == JsonValueKind.String ? e.GetString() : throw Refuse(at, $"{at} is text, not {e.ValueKind.ToString().ToLowerInvariant()}.");
        }

        private static bool? Bool(JsonElement o, string key, string path)
        {
            if (!o.TryGetProperty(key, out var e) || e.ValueKind == JsonValueKind.Null) return null;
            return e.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw Refuse($"{path}.{key}", $"{path}.{key} is true or false."),
            };
        }

        private static T Choice<T>(JsonElement o, string key, string path, Dictionary<string, T> choices, string what)
        {
            var at = $"{path}.{key}";
            var text = String(o, key, path) ?? throw Refuse(at, $"{at} is missing: it says {what}, one of {string.Join(", ", choices.Keys)}.");
            return choices.TryGetValue(text, out var c) ? c : throw Refuse(at, $"{at} \"{text}\" is not known: it is one of {string.Join(", ", choices.Keys)}.");
        }

        private static ChartSpecRefusedException Missing(string path, string key, string what) =>
            Refuse($"{path}.{key}", $"{path}.{key} is missing: {what}.");

        private static ChartSpecRefusedException Refuse(string path, string message) => new(path, message);

        private static string Text(object? v) => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }
}
