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

/// <summary>A Chart Spec read: its title, and the Chart Definition it describes.</summary>
/// <param name="Title">The chart's title, or empty when the spec gives none.</param>
/// <param name="Definition">The chart, its points read from the tables the spec names.</param>
public sealed record ChartSpecReading(string Title, ChartDefinition Definition);

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

    internal static readonly string[] RootKeys = ["version", "title", "axis", "series", "notes"];
    internal static readonly string[] AxisKeys = ["values", "title", "scale", "packed"];
    internal static readonly string[] SeriesKeys =
        ["id", "name", "kind", "data", "x", "y", "open", "high", "low", "close", "volume", "label", "panel", "axis", "fromZero", "step"];
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
        + "on two quantities, never joined) or priceRange (open, high, low and close). A spec that cannot be drawn is refused "
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
        if (thinned > 0)
            parts.Add($"{thinned} {(thinned == 1 ? "pixel holds" : "pixels each hold")} more than one point, and say{(thinned == 1 ? "s" : "")} its range.");
        var missing = reading.Definition.Series.Sum(s => s.Points.Count(p => p.Y is null));
        if (missing > 0)
            parts.Add($"{missing} {(missing == 1 ? "point is" : "points are")} missing, drawn as {(missing == 1 ? "a gap" : "gaps")}.");
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

    private const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Chart Spec",
          "type": "object",
          "additionalProperties": false,
          "required": ["version", "axis", "series"],
          "properties": {
            "version": { "const": 1, "description": "The version of the Chart Spec." },
            "title": { "type": "string", "description": "The chart's title." },
            "axis": { "$ref": "#/$defs/axis" },
            "series": { "type": "array", "minItems": 1, "items": { "$ref": "#/$defs/series" } },
            "notes": { "type": "array", "items": { "type": "string" }, "description": "Lines said under the chart, such as the data's source." }
          },
          "$defs": {
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
                "step": { "enum": ["preceding", "following"], "description": "Which interval a step's point stands for." }
              }
            }
          }
        }
        """;

    private sealed class Reader(Func<string, IChartData?> tables)
    {
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
            var axis = new ChartAxis
            {
                Values = values,
                Title = String(axisElement, "title", "axis") ?? "",
                Scale = scale,
                Labels = _labels.ToArray(),
                Packed = Bool(axisElement, "packed", "axis") ?? false,
            };
            try
            {
                return new ChartSpecReading(title, new ChartDefinition(axis, series, notes));
            }
            catch (ChartRefusedException e)
            {
                // The definition names a series by its name; the spec's path says which one it was.
                var at = series.FindIndex(s => e.Message.Contains($"'{s.Name}'", StringComparison.Ordinal));
                var path = at >= 0 ? $"series[{at}]" : "axis";
                throw Refuse(path, $"{path}: {e.Message}");
            }
        }

        private ChartSeries Series(JsonElement s, string path, AxisValues values)
        {
            if (s.ValueKind != JsonValueKind.Object)
                throw Refuse(path, $"{path} is a JSON object.");
            Known(s, path, SeriesKeys);
            var id = String(s, "id", path) ?? throw Missing(path, "id", "a series' identity, unique in the chart");
            var name = String(s, "name", path) ?? throw Missing(path, "name", "its name in the legend and the tooltip");
            if (!s.TryGetProperty("kind", out _))
                throw Missing(path, "kind", "every series says what its numbers are: " + string.Join(", ", Kinds.Keys) + "; there is no default");
            var kindText = String(s, "kind", path)!;
            if (!Kinds.TryGetValue(kindText, out var kind))
                throw Refuse($"{path}.kind", $"{path}.kind \"{kindText}\" is not a Series Kind: it is one of {string.Join(", ", Kinds.Keys)}.");
            var handle = String(s, "data", path) ?? throw Missing(path, "data", "the Data Handle of the table its values are read from");
            var table = tables(handle) ?? throw Refuse($"{path}.data", $"{path}.data \"{handle}\" is not known: name a Data Handle a capability returned.");
            var x = Column(s, "x", path, handle, table, required: true)!;
            var rows = table.RowCount;
            var positions = new double[rows];
            for (var r = 0; r < rows; r++)
                positions[r] = Position(table.Value(x, r), values, $"{path}.x", handle, r);
            string? label = Column(s, "label", path, handle, table, required: false);
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
                    };
                result = result with { Points = points };
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
            };
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

        private static void Known(JsonElement o, string path, string[] keys)
        {
            foreach (var p in o.EnumerateObject())
                if (!keys.Contains(p.Name))
                {
                    var at = path.Length == 0 ? p.Name : $"{path}.{p.Name}";
                    throw Refuse(at, $"{at} is not part of a Chart Spec: a spec holds no numbers, only the columns they are read from; {(path.Length == 0 ? "a spec" : path)} takes {string.Join(", ", keys)}.");
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
