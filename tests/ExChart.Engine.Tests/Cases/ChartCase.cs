using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExChart.Engine.Fitting;
using ExChart.Engine.Interpolation;

namespace ExChart.Engine.Tests.Cases;

/// <summary>
/// One shared chart case (ADR-0370): what it builds, read from its JSON, and the checks it holds the
/// result to. A result is compared as the JSON it serialises to — public properties, camelCase, an enum
/// by its camelCase name, a tuple as the array of its elements — which is how the Script Core's runner
/// reads its plain objects. A key a case or an object does not name fails the case: a misspelt key never
/// passes on a value nobody chose.
/// </summary>
internal sealed class ChartCase(JsonElement file, JsonElement entry)
{
    private static readonly string[] Operations =
        ["scene", "drawing", "thin", "through", "tenor", "nice", "steps", "decades", "logSteps", "fit", "horizontal", "candles", "legendMembers", "format", "appearance", "mesh", "slice", "slicePath",
         "reading", "summary", "csv", "surfaceSummary", "curve", "interpolate", "sourceThin"];

    // What a scene is laid out with, which the tooltip and the summary read their scene by.
    private static readonly string[] SceneKeys = ["metrics", "view", "style", "independentScales", "legend"];

    private static readonly string[] CaseKeys = ["name", "build", "definition", "surface", "throws", "saying", "expect", .. Operations];

    // The positions each sampled series was asked at, by its identity.
    private readonly Dictionary<string, List<double>> _asked = [];

    // What each operation answered, unserialised, by its name in `build` ("" for a case's own).
    private readonly Dictionary<string, object?> _results = [];

    private string Name => entry.GetProperty("name").GetString()!;

    public async Task Run()
    {
        if (entry.TryGetProperty("sources", out _))
        {
            await RunThrough();
            return;
        }
        if (IsSpecCase)
        {
            RunSpec();
            return;
        }
        Allow(entry, "the case", CaseKeys);
        if (entry.TryGetProperty("throws", out var throws))
        {
            if (entry.TryGetProperty("expect", out _))
                throw new InvalidOperationException($"\"{Name}\" both throws and expects.");
            var e = Assert.ThrowsAny<Exception>(() => Build());
            Assert.True(Refusal(e) == throws.GetString(), $"\"{Name}\": expected {throws.GetString()}, but {e.GetType().Name}: {e.Message}");
            foreach (var text in Strings(entry, "saying"))
                Assert.True(e.Message.Contains(text, StringComparison.Ordinal), $"\"{Name}\": \"{e.Message}\" does not say \"{text}\".");
            return;
        }
        var subject = Build();
        var checks = entry.GetProperty("expect");
        Assert.True(checks.GetArrayLength() > 0, $"\"{Name}\" checks nothing.");
        foreach (var check in checks.EnumerateArray())
            Check(subject, subject, check);
    }

    private static string Refusal(Exception e) => e switch
    {
        ChartRefusedException => "chartRefused",
        ChartSpecRefusedException => "chartSpecRefused",
        ArgumentOutOfRangeException => "outOfRange",
        ArgumentException => "argument",
        InvalidOperationException => "invalidOperation",
        _ => e.GetType().Name,
    };

    private static IEnumerable<string> Strings(JsonElement e, string key) =>
        !e.TryGetProperty(key, out var v) ? []
        : v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(s => s.GetString()!) : [v.GetString()!];

    private JsonNode? Build()
    {
        if (!entry.TryGetProperty("build", out var build))
        {
            var result = Operate(entry);
            _results[""] = result;
            return Serialise(result);
        }
        var all = new JsonObject();
        foreach (var named in build.EnumerateObject())
        {
            Allow(named.Value, $"\"{named.Name}\"", ["definition", "surface", .. Operations]);
            var result = Operate(named.Value);
            _results[named.Name] = result;
            all[named.Name] = Serialise(result);
        }
        return all;
    }

    // ---- What a case builds ----

    private object? Operate(JsonElement op)
    {
        var present = Operations.Where(k => op.TryGetProperty(k, out _)).ToList();
        if (present.Count > 1)
            throw new InvalidOperationException($"\"{Name}\" names {string.Join(" and ", present)}; an operation is one.");
        if (present.Count == 0)
            return op.TryGetProperty("surface", out var surface) ? Surface(surface) : Definition(op.GetProperty("definition"));
        var key = present[0];
        var args = op.GetProperty(key);
        switch (key)
        {
            case "scene":
                Allow(args, "scene", SceneKeys);
                return Scene(Definition(op.GetProperty("definition")), args);
            case "sourceThin":
            {
                // The reference Chart Source (ADR-0344) over one of the file's tables: what it keeps, each row by its
                // position across and its height, and what each thinned pixel held.
                Allow(args, "sourceThin", "table", "query", "version");
                var handle = args.GetProperty("table").GetString()!;
                var table = SpecTables(includeCase: false).GetValueOrDefault(handle)
                    ?? throw new InvalidOperationException($"\"{Name}\" thins the table \"{handle}\", which the file does not hold.");
                var query = SourceQuery(args.GetProperty("query"));
                return AnswerRows(ChartSource.Thin(table, query, args.GetProperty("version").GetInt64()), query);
            }
            case "reading":
            {
                // The tooltip of the scene the case lays out, at a position, over a mark or over an edge.
                Allow(args, "reading", [.. SceneKeys, "at"]);
                var d = Definition(op.GetProperty("definition"));
                // A reading names the view its scene is laid out with, as the tooltip is always read of one.
                var view = View(args.GetProperty("view"));
                var scene = Scene(d, args);
                var at = args.GetProperty("at");
                if (at.TryGetProperty("mark", out var mark))
                {
                    Allow(at, "at", "mark");
                    return ChartReadings.OfMark(d, scene, mark.GetInt32());
                }
                if (at.TryGetProperty("edge", out var edge))
                {
                    Allow(at, "at", "edge");
                    return ChartReadings.OfEdge(d, scene, edge.GetInt32());
                }
                Allow(at, "at", "position", "panel", "pointerY");
                return ChartReadings.At(d, scene, Metrics(args.GetProperty("metrics")), view, Position(at.GetProperty("position")),
                    at.GetProperty("panel").GetInt32(), Number(at.GetProperty("pointerY")));
            }
            case "summary":
            {
                Allow(args, "summary", [.. SceneKeys, "title", "description"]);
                var d = Definition(op.GetProperty("definition"));
                return ChartSummary.Of(d, Scene(d, args), View(args.GetProperty("view")), Text(args, "title"), Text(args, "description"));
            }
            case "csv":
            {
                Allow(args, "csv", "form", "metrics", "view");
                var d = Definition(op.GetProperty("definition"));
                var metrics = Metrics(args.GetProperty("metrics"));
                var view = View(args.GetProperty("view"));
                return args.GetProperty("form").GetString() switch
                {
                    "long" => ChartCsv.Long(d, metrics, view),
                    "wide" => ChartCsv.Wide(d, metrics, view),
                    "table" => ChartCsv.WideTable(d, metrics, view),
                    var form => throw new InvalidOperationException($"\"{form}\" is not a form of the files; it is long, wide or table."),
                };
            }
            case "curve":
            {
                // A fit a Consumer calls (ADR-0291): the curve, or its values at the positions `at` names.
                var curve = Fitted(args, "at");
                return args.TryGetProperty("at", out var at) ? at.EnumerateArray().Select(x => curve.ValueAt(Position(x))).ToList() : curve;
            }
            case "interpolate":
            {
                // An interpolation a Consumer calls (ADR-0257): the interpolant, or its values where `at` names.
                var interpolant = Interpolant(args, "at");
                if (!args.TryGetProperty("at", out var at))
                    return interpolant;
                return interpolant switch
                {
                    Interpolant1D curve => at.EnumerateArray().Select(x => curve.ValueAt(Position(x))).ToList(),
                    Interpolant2D surface => at.EnumerateArray().Select(p => surface.ValueAt(Number(p[0]), Number(p[1]))).ToList(),
                    _ => throw new InvalidOperationException("Internal: an interpolant is a curve or a surface."),
                };
            }
            case "drawing":
            {
                Allow(args, "drawing", "axis", "series", "from", "to", "pixels", "quotes");
                return SeriesDrawing.Of(Series(args.GetProperty("series")), Axis(args.GetProperty("axis")),
                    args.TryGetProperty("from", out var from) ? Position(from) : double.NegativeInfinity,
                    args.TryGetProperty("to", out var to) ? Position(to) : double.PositiveInfinity,
                    args.TryGetProperty("pixels", out var pixels) ? pixels.GetInt32() : 0,
                    args.TryGetProperty("quotes", out var quotes) ? quotes.EnumerateArray().Select(Position).ToList() : null);
            }
            case "thin":
                Allow(args, "thin", "drawing", "toPixel", "pixels");
                return Thinning.Of(Drawing(args.GetProperty("drawing")), Linear(args.GetProperty("toPixel")), args.GetProperty("pixels").GetInt32());
            case "through":
                Allow(args, "through", "source", "drawn", "kept");
                return Thinning.Through(Summaries(args.GetProperty("source")), Summaries(args.GetProperty("drawn")),
                    args.GetProperty("kept").EnumerateArray().Select(Position).ToList());
            case "tenor":
                return TickLayout.Tenor(Number(args));
            case "nice":
                return args.GetArrayLength() == 3
                    ? TickLayout.Nice(Number(args[0]), Number(args[1]), args[2].GetInt32())
                    : TickLayout.Nice(Number(args[0]), Number(args[1]));
            case "steps":
                return TickLayout.Steps(Number(args[0]), Number(args[1]), Number(args[2]));
            case "decades":
                return TickLayout.Decades(Number(args[0]), Number(args[1]));
            case "logSteps":
                return TickLayout.LogSteps(Number(args[0]), Number(args[1]));
            case "fit":
                Allow(args, "fit", "ticks", "angle", "map", "textWidth", "lineHeight", "gap");
                return TickLayout.Fit(args.GetProperty("ticks").EnumerateArray().Select(t => (Position(t[0]), t[1].GetString()!)).ToList(),
                    args.GetProperty("angle").GetInt32(), Linear(args.GetProperty("map")), TextWidth(args.GetProperty("textWidth")),
                    Number(args.GetProperty("lineHeight")), Number(args.GetProperty("gap")));
            case "horizontal":
                Allow(args, "horizontal", "axis", "style", "from", "to", "points", "map", "textWidth", "lineHeight");
                return TickLayout.Horizontal(Axis(args.GetProperty("axis")), Style(args.GetProperty("style")), Position(args.GetProperty("from")),
                    Position(args.GetProperty("to")), args.GetProperty("points").EnumerateArray().Select(Position).ToList(), Linear(args.GetProperty("map")),
                    TextWidth(args.GetProperty("textWidth")), Number(args.GetProperty("lineHeight")));
            case "candles":
            {
                Allow(args, "candles", "series", "from", "to", "period");
                return PriceRanges.Candles(Series(args.GetProperty("series")),
                    args.TryGetProperty("from", out var from) ? Position(from) : double.NegativeInfinity,
                    args.TryGetProperty("to", out var to) ? Position(to) : double.PositiveInfinity,
                    Enum<CandlePeriod>(args.GetProperty("period")));
            }
            case "legendMembers":
                return ChartScene.LegendMembers(Definition(op.GetProperty("definition")), args.GetString()!);
            case "format":
                Allow(args, "format", "format", "value");
                return Number(args.GetProperty("value")).ToString(args.GetProperty("format").GetString(), CultureInfo.InvariantCulture);
            case "mesh":
                Allow(args, "mesh");
                return SurfaceMesh.Of(Surface(op.GetProperty("surface")));
            case "slice":
                return Surface(op.GetProperty("surface")).SliceChart(Slice(args));
            case "slicePath":
                return SurfaceMesh.SlicePath(Surface(op.GetProperty("surface")), Slice(args));
            case "surfaceSummary":
                Allow(args, "surfaceSummary", "title", "description");
                return SurfaceMesh.Summary(Surface(op.GetProperty("surface")),
                    args.TryGetProperty("title", out var surfaceTitle) ? surfaceTitle.GetString() : null,
                    args.TryGetProperty("description", out var surfaceDescription) ? surfaceDescription.GetString() : null);
            case "appearance":
                Allow(args, "appearance", "colourSlots");
                return Appearance.Of(Definition(op.GetProperty("definition")),
                    args.TryGetProperty("colourSlots", out var slots) ? slots.GetInt32() : Appearance.DefaultColourSlots);
            default:
                throw new InvalidOperationException($"Internal: {key} is listed and not run.");
        }
    }

    // The scene a case lays out: its metrics, and its view, style, independent panels and legend where it names them.
    private static ChartScene Scene(ChartDefinition definition, JsonElement args) =>
        ChartScene.Build(definition, Metrics(args.GetProperty("metrics")),
            args.TryGetProperty("view", out var view) ? View(view) : null,
            args.TryGetProperty("style", out var style) ? Style(style) : null,
            args.TryGetProperty("independentScales", out var independent) ? independent.EnumerateArray().Select(s => s.GetString()!).ToHashSet() : null,
            args.TryGetProperty("legend", out var legend) ? Legend(legend) : null);

    private ChartDefinition Definition(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String)
            e = file.GetProperty("definitions").GetProperty(e.GetString()!);
        Allow(e, "a definition", "axis", "series", "notes");
        return new ChartDefinition(Axis(e.GetProperty("axis")), e.GetProperty("series").EnumerateArray().Select(Series).ToList(),
            e.TryGetProperty("notes", out var notes) ? notes.EnumerateArray().Select(n => n.GetString()!).ToList() : null);
    }

    // ---- A surface (ADR-0274, ADR-0311) ----

    // A grid (`z`), points on a grid, a Consumer's triangles (`nodes`) or a sampled function, each with
    // its vertical scale and centre where the case names them.
    private SurfaceDefinition Surface(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String)
            e = file.GetProperty("definitions").GetProperty(e.GetString()!);
        Allow(e, "a surface", "x", "y", "z", "zTitle", "zFormat", "zFormatCode", "method", "interpolated", "notes", "points", "xTitle", "yTitle", "nodes", "triangles",
            "xScale", "yScale", "xFormatCode", "yFormatCode", "sampled", "zScale", "centre", "interpolant", "samples");
        var forms = new[] { "z", "points", "nodes", "sampled", "interpolant" }.Where(k => e.TryGetProperty(k, out _)).ToList();
        if (forms.Count != 1)
            throw new InvalidOperationException($"\"{Name}\": a surface is one of a grid, points, nodes, a sampled function and an interpolant, not {forms.Count}.");
        // A key another form reads would be passed over here, and the case would test nothing by it.
        Allow(e, $"\"{Name}\"'s {forms[0]} surface", [.. SurfaceCommonKeys, .. SurfaceFormKeys[forms[0]]]);
        var zTitle = Text(e, "zTitle") ?? "";
        var zFormat = e.TryGetProperty("zFormat", out var zf) ? ValueFormat(zf) : null;
        var notes = e.TryGetProperty("notes", out var n) ? n.EnumerateArray().Select(s => s.GetString()!).ToList() : null;
        var surface = forms[0] switch
        {
            "z" => new SurfaceDefinition(SurfaceAxisOf(e.GetProperty("x")), SurfaceAxisOf(e.GetProperty("y")),
                e.GetProperty("z").EnumerateArray().Select(r => (IReadOnlyList<ChartValue?>)r.EnumerateArray().Select(Value).ToList()).ToList(),
                zTitle, zFormat, Text(e, "method"),
                e.TryGetProperty("interpolated", out var marks)
                    ? marks.EnumerateArray().Select(r => (IReadOnlyList<bool>)r.EnumerateArray().Select(b => b.GetBoolean()).ToList()).ToList()
                    : null,
                notes),
            "points" => SurfaceDefinition.FromPoints(
                e.GetProperty("points").EnumerateArray().Select(p => (AxisValue(p[0]), AxisValue(p[1]), ChartValue.Of(Number(p[2])))).ToList(),
                e.GetProperty("xTitle").GetString()!, e.GetProperty("yTitle").GetString()!, zTitle, zFormat: zFormat,
                xFormatCode: Text(e, "xFormatCode"), yFormatCode: Text(e, "yFormatCode")),
            "nodes" => SurfaceDefinition.FromTriangles(
                e.GetProperty("nodes").EnumerateArray().Select(p => new SurfaceNode(Number(p[0]), Number(p[1]), Value(p[2]))).ToList(),
                e.GetProperty("triangles").EnumerateArray().Select(t => new SurfaceTriangle(t[0].GetInt32(), t[1].GetInt32(), t[2].GetInt32())).ToList(),
                e.GetProperty("xTitle").GetString()!, e.GetProperty("yTitle").GetString()!, zTitle, zFormat: zFormat,
                xScale: e.TryGetProperty("xScale", out var xs) ? Enum<AxisScale>(xs) : AxisScale.Proportional,
                yScale: e.TryGetProperty("yScale", out var ys) ? Enum<AxisScale>(ys) : AxisScale.Proportional, notes: notes,
                xFormatCode: Text(e, "xFormatCode"), yFormatCode: Text(e, "yFormatCode")),
            "interpolant" => SurfaceDefinition.Sampled(
                Interpolant(e.GetProperty("interpolant")) as Interpolant2D ?? throw new InvalidOperationException($"\"{Name}\": a surface interpolates in two dimensions."),
                e.GetProperty("xTitle").GetString()!, e.GetProperty("yTitle").GetString()!, zTitle,
                samples: e.TryGetProperty("samples", out var samples) ? samples.GetInt32() : 25, zFormat: zFormat,
                xScale: e.TryGetProperty("xScale", out var ixs) ? Enum<AxisScale>(ixs) : AxisScale.Categories,
                yScale: e.TryGetProperty("yScale", out var iys) ? Enum<AxisScale>(iys) : AxisScale.Categories,
                xFormatCode: Text(e, "xFormatCode"), yFormatCode: Text(e, "yFormatCode")),
            _ => Sampled(e, zTitle, zFormat),
        };
        if (e.TryGetProperty("zScale", out var zScale)) surface = surface with { ZScale = Enum<AxisScale>(zScale) };
        if (Text(e, "zFormatCode") is { } zCode) surface = surface with { ZFormatCode = zCode };
        if (e.TryGetProperty("centre", out var centre)) surface = surface with { Centre = Number(centre) };
        return surface;
    }

    // What every form of surface may say, and what each form reads besides.
    private static readonly string[] SurfaceCommonKeys = ["zTitle", "zFormat", "zFormatCode", "zScale", "centre"];

    private static readonly Dictionary<string, string[]> SurfaceFormKeys = new()
    {
        ["z"] = ["x", "y", "z", "method", "interpolated", "notes"],
        ["points"] = ["points", "xTitle", "yTitle", "xFormatCode", "yFormatCode"],
        ["nodes"] = ["nodes", "triangles", "xTitle", "yTitle", "xScale", "yScale", "xFormatCode", "yFormatCode", "notes"],
        ["sampled"] = ["sampled", "xTitle", "yTitle", "xFormatCode", "yFormatCode"],
        ["interpolant"] = ["interpolant", "xTitle", "yTitle", "samples", "xScale", "yScale", "xFormatCode", "yFormatCode"],
    };

    // A function sampled over a rectangle: a constant, or a plane [a, b, c], a + b·x + c·y.
    private SurfaceDefinition Sampled(JsonElement e, string zTitle, Func<object, string>? zFormat)
    {
        var s = e.GetProperty("sampled");
        Allow(s, "sampled", "method", "x", "y", "z", "samples", "xScale", "yScale");
        var z = s.GetProperty("z");
        Func<double, double, ChartValue?> f;
        if (z.ValueKind == JsonValueKind.Number)
        {
            var constant = Number(z);
            f = (_, _) => ChartValue.Of(constant);
        }
        else
        {
            var plane = Only(z, "plane");
            var (a, b, c) = (Number(plane[0]), Number(plane[1]), Number(plane[2]));
            f = (x, y) => ChartValue.Of(a + b * x + c * y);
        }
        var (x, y) = (s.GetProperty("x"), s.GetProperty("y"));
        return SurfaceDefinition.Sampled(s.GetProperty("method").GetString()!, Number(x[0]), Number(x[1]), Number(y[0]), Number(y[1]), f,
            e.GetProperty("xTitle").GetString()!, e.GetProperty("yTitle").GetString()!, zTitle,
            samples: s.TryGetProperty("samples", out var samples) ? samples.GetInt32() : 25, zFormat: zFormat,
            xScale: s.TryGetProperty("xScale", out var xs) ? Enum<AxisScale>(xs) : AxisScale.Categories,
            yScale: s.TryGetProperty("yScale", out var ys) ? Enum<AxisScale>(ys) : AxisScale.Categories,
            xFormatCode: Text(e, "xFormatCode"), yFormatCode: Text(e, "yFormatCode"));
    }

    private static SurfaceAxis SurfaceAxisOf(JsonElement e)
    {
        Allow(e, "a surface axis", "title", "values", "scale", "format", "formatCode");
        var axis = new SurfaceAxis(e.GetProperty("title").GetString()!, e.GetProperty("values").EnumerateArray().Select(AxisValue).ToList(),
            e.TryGetProperty("format", out var format) ? ValueFormat(format) : null);
        if (e.TryGetProperty("scale", out var scale)) axis = axis with { Scale = Enum<AxisScale>(scale) };
        return Text(e, "formatCode") is { } code ? axis with { FormatCode = code } : axis;
    }

    // A surface axis' value: a text, or a number as a double.
    private static object AxisValue(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()! : Number(e);

    private static SurfaceSlice Slice(JsonElement e)
    {
        Allow(e, "a slice", "axis", "value");
        return new SurfaceSlice(Enum<SurfaceSliceAxis>(e.GetProperty("axis")), AxisValue(e.GetProperty("value")));
    }

    // A writer of a value: a prefix and a suffix around it, written with a fixed number of places or invariantly.
    private static Func<object, string> ValueFormat(JsonElement e)
    {
        Allow(e, "a format", "prefix", "suffix", "places");
        var prefix = Text(e, "prefix") ?? "";
        var suffix = Text(e, "suffix") ?? "";
        int? places = e.TryGetProperty("places", out var p) ? p.GetInt32() : null;
        return v => prefix + (places is { } k
            ? Convert.ToDouble(v, CultureInfo.InvariantCulture).ToString("F" + k.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : Convert.ToString(v, CultureInfo.InvariantCulture)) + suffix;
    }

    // ---- A fit (ADR-0291) ----

    // A curve fitted by the method the case names, over its points, in the space it names or, where it
    // names none, in the method's own default. Only a polynomial reads a degree.
    private static FittedCurve Fitted(JsonElement e, params string[] more)
    {
        var method = e.GetProperty("method").GetString();
        Allow(e, "a curve", [.. CurveKeys, .. method == "polynomial" ? ["degree"] : Array.Empty<string>(), .. more]);
        var xs = Run(e.GetProperty("x"));
        var ys = Run(e.GetProperty("y"));
        FitSpace? space = e.TryGetProperty("space", out var s) ? Enum<FitSpace>(s) : null;
        return method switch
        {
            "leastSquares" => space is { } sp ? Fit.LeastSquares(xs, ys, sp) : Fit.LeastSquares(xs, ys),
            "theilSen" => space is { } sp ? Fit.TheilSen(xs, ys, sp) : Fit.TheilSen(xs, ys),
            "polynomial" => space is { } sp
                ? Fit.Polynomial(xs, ys, e.GetProperty("degree").GetInt32(), sp)
                : Fit.Polynomial(xs, ys, e.GetProperty("degree").GetInt32()),
            _ => throw new InvalidOperationException($"\"{method}\" is not a fit; it is leastSquares, theilSen or polynomial."),
        };
    }

    private static readonly string[] CurveKeys = ["method", "x", "y", "space"];

    // A fit's xs or ys: numbers, or a run of `count` from `from` by `step`.
    private static double[] Run(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
            return e.EnumerateArray().Select(Number).ToArray();
        var run = Only(e, "run");
        Allow(run, "a run", "count", "from", "step");
        var (from, step) = (Number(run.GetProperty("from")), Number(run.GetProperty("step")));
        return Enumerable.Range(0, run.GetProperty("count").GetInt32()).Select(i => from + i * step).ToArray();
    }

    // ---- An interpolation (ADR-0257) ----

    // A curve through `x` and `y` (linear, naturalCubicSpline), a surface on a grid (bilinear: `x`, `y` and
    // the rows `z`), or a surface on a Consumer's triangles (`points` [x, y, z] and `triangles` [a, b, c]).
    private static object Interpolant(JsonElement e, params string[] more)
    {
        var method = e.GetProperty("method").GetString();
        string[] keys = method switch
        {
            "linear" or "naturalCubicSpline" => ["x", "y"],
            "bilinear" => ["x", "y", "z"],
            "triangles" => ["points", "triangles"],
            _ => throw new InvalidOperationException($"\"{method}\" is not an interpolation; it is linear, naturalCubicSpline, bilinear or triangles."),
        };
        Allow(e, "an interpolant", ["method", .. keys, .. more]);
        double[] Numbers(string key) => e.GetProperty(key).EnumerateArray().Select(Number).ToArray();
        return method switch
        {
            "linear" => Interpolate.Linear(Numbers("x"), Numbers("y")),
            "naturalCubicSpline" => Interpolate.NaturalCubicSpline(Numbers("x"), Numbers("y")),
            "bilinear" => Interpolate.Bilinear(Numbers("x"), Numbers("y"),
                e.GetProperty("z").EnumerateArray()
                    .Select(r => (IReadOnlyList<double?>)r.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Null ? (double?)null : Number(v)).ToList()).ToList()),
            _ => Interpolate.Triangles(
                e.GetProperty("points").EnumerateArray().Select(p => (Number(p[0]), Number(p[1]), Number(p[2]))).ToList(),
                e.GetProperty("triangles").EnumerateArray().Select(t => (t[0].GetInt32(), t[1].GetInt32(), t[2].GetInt32())).ToList()),
        };
    }

    private static ChartAxis Axis(JsonElement e)
    {
        Allow(e, "an axis", "values", "title", "scale", "labels", "named", "daysPerYear", "packed", "formatCode");
        var axis = new ChartAxis { Values = Enum<AxisValues>(e.GetProperty("values")) };
        if (e.TryGetProperty("title", out var title)) axis = axis with { Title = title.GetString()! };
        if (e.TryGetProperty("scale", out var scale)) axis = axis with { Scale = Enum<AxisScale>(scale) };
        if (e.TryGetProperty("labels", out var labels)) axis = axis with { Labels = labels.EnumerateArray().Select(l => l.GetString()!).ToList() };
        if (e.TryGetProperty("named", out var named)) axis = axis with { Named = named.EnumerateArray().Select(n => (Position(n[0]), n[1].GetString()!)).ToList() };
        if (e.TryGetProperty("daysPerYear", out var days)) axis = axis with { DaysPerYear = Number(days) };
        if (e.TryGetProperty("packed", out var packed)) axis = axis with { Packed = packed.GetBoolean() };
        if (e.TryGetProperty("formatCode", out var code)) axis = axis with { FormatCode = code.GetString()! };
        return axis;
    }

    private ChartSeries Series(JsonElement e)
    {
        // An interpolated curve's series, as a Consumer hands it in (ADR-0257).
        if (e.TryGetProperty("interpolant", out var interpolant))
        {
            Allow(e, "an interpolated series", "id", "name", "interpolant");
            return (Interpolant(interpolant) as Interpolant1D ?? throw new InvalidOperationException($"\"{Name}\": a series interpolates in one dimension."))
                .Series(e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!);
        }
        // A fit's series, as a Consumer hands it in (ADR-0291): its curve says the rest.
        if (e.TryGetProperty("curve", out var fitted))
        {
            Allow(e, "a fitted series", "id", "name", "curve");
            return Fitted(fitted).Series(e.GetProperty("id").GetString()!, e.GetProperty("name").GetString()!);
        }
        Allow(e, "a series", "id", "name", "kind", "step", "axis", "panel", "keys", "fromZero", "interpolated", "segments", "points", "colourBy", "sizeBy",
            "details", "edges", "bars", "mergeTo", "interval", "xInterval", "thinned", "sampler", "format", "formatCode");
        var id = e.GetProperty("id").GetString()!;
        // A series that leaves out its kind declares none, which the definition refuses (ADR-0253).
        var s = new ChartSeries { Id = id, Name = e.GetProperty("name").GetString()!, Kind = e.TryGetProperty("kind", out var kind) ? Enum<SeriesKind>(kind) : SeriesKind.Undeclared };
        if (e.TryGetProperty("step", out var step)) s = s with { Step = Enum<StepSide>(step) };
        if (e.TryGetProperty("axis", out var axis)) s = s with { Axis = Enum<AxisSide>(axis) };
        if (e.TryGetProperty("panel", out var panel)) s = s with { Panel = panel.GetString()! };
        if (e.TryGetProperty("keys", out var keys))
        {
            Allow(keys, "keys", "colour", "lineStyle", "marker");
            s = s with { Keys = new AppearanceKeys(keys.GetProperty("colour").GetString()!, Text(keys, "lineStyle"), Text(keys, "marker")) };
        }
        if (e.TryGetProperty("fromZero", out var fromZero)) s = s with { FromZero = fromZero.GetBoolean() };
        if (e.TryGetProperty("interpolated", out var interpolated)) s = s with { Interpolated = interpolated.GetBoolean() };
        if (e.TryGetProperty("segments", out var segments))
            s = s with
            {
                Segments = segments.EnumerateArray().Select(g =>
                {
                    Allow(g, "a segment", "from", "to", "method", "extrapolated");
                    return new Segment(Position(g.GetProperty("from")), Position(g.GetProperty("to")), g.GetProperty("method").GetString()!,
                        g.TryGetProperty("extrapolated", out var x) && x.GetBoolean());
                }).ToList(),
            };
        if (e.TryGetProperty("points", out var points)) s = s with { Points = Points(points) };
        if (e.TryGetProperty("colourBy", out var colour))
        {
            Allow(colour, "colourBy", "title", "values", "centre");
            s = s with
            {
                ColourBy = new PointColour(colour.GetProperty("title").GetString()!, Values(colour.GetProperty("values")))
                {
                    Centre = colour.TryGetProperty("centre", out var centre) ? Number(centre) : null,
                },
            };
        }
        if (e.TryGetProperty("sizeBy", out var size))
        {
            Allow(size, "sizeBy", "title", "values");
            s = s with { SizeBy = new PointSize(size.GetProperty("title").GetString()!, Values(size.GetProperty("values"))) };
        }
        if (e.TryGetProperty("details", out var details))
            s = s with
            {
                Details = details.EnumerateArray().Select(d =>
                    new PointDetail(d.GetProperty("title").GetString()!, d.GetProperty("values").EnumerateArray().Select(v => v.GetString()).ToList())).ToList(),
            };
        if (e.TryGetProperty("edges", out var edges))
            s = s with { Edges = edges.EnumerateArray().Select(g => new ChartEdge(Key(g[0]), Key(g[1]))).ToList() };
        if (e.TryGetProperty("bars", out var bars)) s = s with { Bars = Bars(bars) };
        if (e.TryGetProperty("mergeTo", out var merge)) s = s with { MergeTo = Enum<CandlePeriod>(merge) };
        if (e.TryGetProperty("interval", out var interval)) s = s with { Interval = Interval(interval) };
        if (e.TryGetProperty("xInterval", out var xInterval)) s = s with { XInterval = Interval(xInterval) };
        if (e.TryGetProperty("thinned", out var thinned)) s = s with { Thinned = Summaries(thinned) };
        if (e.TryGetProperty("sampler", out var sampler)) s = s with { Sampler = Sampler(id, sampler) };
        if (e.TryGetProperty("format", out var format))
        {
            Allow(format, "a format", "prefix", "suffix");
            var prefix = Text(format, "prefix") ?? "";
            var suffix = Text(format, "suffix") ?? "";
            s = s with { Format = v => prefix + Convert.ToString(v, CultureInfo.InvariantCulture) + suffix };
        }
        if (e.TryGetProperty("formatCode", out var code)) s = s with { FormatCode = code.GetString()! };
        return s;
    }

    private static string? Text(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.GetString() : null;

    // A Row Key: a text or a number, as a Consumer's row hands it in.
    private static object Key(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString()!,
        JsonValueKind.Number => e.GetDouble(),
        _ => throw new InvalidOperationException($"{e} is not a Row Key a case can write."),
    };

    private static List<ChartPoint> Points(JsonElement e)
    {
        var points = new List<ChartPoint>();
        foreach (var item in e.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                var p = new ChartPoint(Position(item[0]), Value(item[1]));
                points.Add(item.GetArrayLength() > 2 ? p with { Name = item[2].GetString() } : p);
            }
            else if (item.TryGetProperty("generate", out var g))
            {
                Allow(item, "a generated run", "generate");
                Allow(g, "generate", "count", "x", "y", "name", "at");
                var count = g.GetProperty("count").GetInt32();
                for (var i = 0; i < count; i++)
                {
                    var p = new ChartPoint(Across(g.GetProperty("x"), i),
                        g.TryGetProperty("at", out var at) && at.TryGetProperty(i.ToString(CultureInfo.InvariantCulture), out var over) ? Value(over) : ByIndex(g.GetProperty("y"), i));
                    points.Add(g.TryGetProperty("name", out var prefix) ? p with { Name = prefix.GetString() + i.ToString(CultureInfo.InvariantCulture) } : p);
                }
            }
            else
            {
                Allow(item, "a point", "x", "y", "name", "row");
                points.Add(new ChartPoint(Position(item.GetProperty("x")), Value(item.GetProperty("y")))
                {
                    Name = Text(item, "name"),
                    Row = item.TryGetProperty("row", out var row) ? Key(row) : null,
                });
            }
        }
        return points;
    }

    // A generated position: `from` and then a `step` a point.
    private static double Across(JsonElement e, int i)
    {
        Allow(e, "x", "from", "step");
        return Position(e.GetProperty("from")) + i * Number(e.GetProperty("step"));
    }

    // A generated value by its index: a constant, a line, a sine or a remainder.
    private static ChartValue? ByIndex(JsonElement e, int i)
    {
        if (e.ValueKind is JsonValueKind.Number or JsonValueKind.Null)
            return Value(e);
        var form = e.EnumerateObject().Single();
        var v = form.Name switch
        {
            "line" => Number(form.Value[0]) + Number(form.Value[1]) * i,
            "sin" => Math.Sin(i / Number(form.Value)),
            "mod" => i % form.Value.GetInt32(),
            _ => throw new InvalidOperationException($"{form.Name} is not a generated value."),
        };
        return ChartValue.Of(v);
    }

    private SeriesSampler Sampler(string id, JsonElement e)
    {
        Allow(e, "a sampler", "from", "to", "value");
        var asked = _asked[id] = [];
        var form = e.GetProperty("value").EnumerateObject().Single();
        Func<double, double> f = form.Name switch
        {
            "line" => x => Number(form.Value[0]) + Number(form.Value[1]) * x,
            "split" => x => x < Number(form.Value[0]) ? Number(form.Value[1]) : Number(form.Value[2]),
            _ => throw new InvalidOperationException($"{form.Name} is not a sampled function."),
        };
        return new SeriesSampler(Position(e.GetProperty("from")), Position(e.GetProperty("to")), x =>
        {
            asked.Add(x);
            return ChartValue.Of(f(x));
        });
    }

    private static List<PriceBar> Bars(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
            return e.EnumerateArray().Select(b => new PriceBar(Position(b[0]), Value(b[1]), Value(b[2]), Value(b[3]), Value(b[4]),
                b.GetArrayLength() > 5 ? Value(b[5]) : null)).ToList();
        Allow(e, "bars", "weekdays");
        var w = e.GetProperty("weekdays");
        Allow(w, "weekdays", "count", "from", "volume", "withoutOpen", "set");
        // Weekdays from a Monday, a fixed walk whose open, high, low and close always agree.
        var x = w.TryGetProperty("from", out var from) ? Position(from) : new DateOnly(2026, 1, 5).DayNumber;
        var volume = w.TryGetProperty("volume", out var v) && v.GetBoolean();
        var price = 100d;
        var bars = new List<PriceBar>();
        for (var i = 0; i < w.GetProperty("count").GetInt32(); i++)
        {
            while (DateOnly.FromDayNumber((int)x).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) x++;
            var close = price + (i % 7 - 3);
            bars.Add(new PriceBar(x, ChartValue.Of(price), ChartValue.Of(Math.Max(price, close) + 2), ChartValue.Of(Math.Min(price, close) - 2), ChartValue.Of(close), volume ? ChartValue.Of(1000d + i) : null));
            price = close;
            x++;
        }
        if (w.TryGetProperty("withoutOpen", out var noOpen) && noOpen.GetBoolean())
            bars = bars.Select(b => b with { Open = null }).ToList();
        if (w.TryGetProperty("set", out var set))
            foreach (var row in set.EnumerateObject())
            {
                Allow(row.Value, "a bar's values", "open", "high", "low", "close", "volume");
                var i = int.Parse(row.Name, CultureInfo.InvariantCulture);
                var b = bars[i];
                if (row.Value.TryGetProperty("open", out var o)) b = b with { Open = Value(o) };
                if (row.Value.TryGetProperty("high", out var h)) b = b with { High = Value(h) };
                if (row.Value.TryGetProperty("low", out var l)) b = b with { Low = Value(l) };
                if (row.Value.TryGetProperty("close", out var c)) b = b with { Close = Value(c) };
                if (row.Value.TryGetProperty("volume", out var vol)) b = b with { Volume = Value(vol) };
                bars[i] = b;
            }
        return bars;
    }

    private static SeriesInterval Interval(JsonElement e)
    {
        Allow(e, "an interval", "meaning", "bounds", "style");
        var bounds = e.GetProperty("bounds");
        List<IntervalBound> list;
        if (bounds.ValueKind == JsonValueKind.Array)
            list = bounds.EnumerateArray().Select(b => new IntervalBound(Position(b[0]), Value(b[1]), Value(b[2]))).ToList();
        else
        {
            var g = bounds.GetProperty("generate");
            Allow(g, "generated bounds", "count", "x", "lower", "upper", "missing");
            var missing = g.TryGetProperty("missing", out var m) ? m.EnumerateArray().Select(i => i.GetInt32()).ToHashSet() : [];
            list = Enumerable.Range(0, g.GetProperty("count").GetInt32())
                .Select(i => new IntervalBound(Across(g.GetProperty("x"), i), missing.Contains(i) ? null : ByIndex(g.GetProperty("lower"), i), ByIndex(g.GetProperty("upper"), i))).ToList();
        }
        return new SeriesInterval(e.GetProperty("meaning").GetString()!, list,
            e.TryGetProperty("style", out var style) ? Enum<IntervalStyle>(style) : IntervalStyle.Whiskers);
    }

    private static List<PixelSummary> Summaries(JsonElement e) =>
        e.EnumerateArray().Select(p =>
        {
            Allow(p, "a pixel summary", "from", "to", "min", "max", "count");
            return new PixelSummary(Position(p.GetProperty("from")), Position(p.GetProperty("to")), Number(p.GetProperty("min")), Number(p.GetProperty("max")),
                p.GetProperty("count").GetInt32());
        }).ToList();

    private static SeriesDrawing Drawing(JsonElement e)
    {
        Allow(e, "a drawing", "lines", "markers");
        var lines = new List<IReadOnlyList<DrawnPoint>>();
        foreach (var line in e.GetProperty("lines").EnumerateArray())
        {
            if (line.ValueKind == JsonValueKind.Object && line.TryGetProperty("levels", out var levels))
            {
                // A step's levels, each from i to i + 1.
                Allow(levels, "levels", "count", "y");
                for (var i = 0; i < levels.GetProperty("count").GetInt32(); i++)
                {
                    var y = ByIndex(levels.GetProperty("y"), i)!.Value.ToDouble();
                    lines.Add([new DrawnPoint(i, y), new DrawnPoint(i + 1, y)]);
                }
                continue;
            }
            lines.Add(Points(line).Select(p => new DrawnPoint(p.X, p.Y!.Value.ToDouble())).ToList());
        }
        var markers = e.GetProperty("markers").EnumerateArray().Select(m => new DrawnMarker(Number(m[0]), Number(m[1]), m[2].GetBoolean())).ToList();
        return new SeriesDrawing(lines, markers);
    }

    private static ChartMetrics Metrics(JsonElement e)
    {
        Allow(e, "metrics", "width", "height", "devicePixelRatio", "lineHeight", "padding", "panelGap", "textWidth", "colourSlots");
        var m = new ChartMetrics(Number(e.GetProperty("width")), Number(e.GetProperty("height")));
        if (e.TryGetProperty("devicePixelRatio", out var dpr)) m = m with { DevicePixelRatio = Number(dpr) };
        if (e.TryGetProperty("lineHeight", out var lh)) m = m with { LineHeight = Number(lh) };
        if (e.TryGetProperty("padding", out var pad)) m = m with { Padding = Number(pad) };
        if (e.TryGetProperty("panelGap", out var gap)) m = m with { PanelGap = Number(gap) };
        if (e.TryGetProperty("textWidth", out var tw)) m = m with { TextWidth = TextWidth(tw) };
        if (e.TryGetProperty("colourSlots", out var slots)) m = m with { ColourSlots = slots.GetInt32() };
        return m;
    }

    private static ChartView View(JsonElement e)
    {
        Allow(e, "a view", "hidden", "from", "to", "scale", "yFrom", "yTo", "yScale");
        return new ChartView(e.TryGetProperty("hidden", out var hidden) ? hidden.EnumerateArray().Select(h => h.GetString()!).ToHashSet() : [],
            e.TryGetProperty("from", out var from) ? Position(from) : null,
            e.TryGetProperty("to", out var to) ? Position(to) : null,
            e.TryGetProperty("scale", out var scale) ? Enum<AxisScale>(scale) : null,
            e.TryGetProperty("yFrom", out var yFrom) ? Number(yFrom) : null,
            e.TryGetProperty("yTo", out var yTo) ? Number(yTo) : null,
            e.TryGetProperty("yScale", out var yScale) ? Enum<AxisScale>(yScale) : null);
    }

    private static AxisStyle Style(JsonElement e)
    {
        Allow(e, "a style", "placement", "label", "angle");
        var style = AxisStyle.Default;
        if (e.TryGetProperty("placement", out var placement)) style = style with { Placement = Enum<TickPlacement>(placement) };
        if (e.TryGetProperty("label", out var label)) style = style with { Label = Enum<DateLabel>(label) };
        if (e.TryGetProperty("angle", out var angle)) style = style with { Angle = angle.GetInt32() };
        return style;
    }

    private static LegendPlacement Legend(JsonElement e)
    {
        Allow(e, "a legend", "position", "align");
        var legend = LegendPlacement.Default;
        if (e.TryGetProperty("position", out var position)) legend = legend with { Position = Enum<LegendPosition>(position) };
        if (e.TryGetProperty("align", out var align)) legend = legend with { Align = Enum<LegendAlign>(align) };
        return legend;
    }

    // A map from a position to a pixel: multiplied by `scale` or divided by `over`, then moved by `offset`.
    private static Func<double, double> Linear(JsonElement e)
    {
        Allow(e, "a map", "scale", "over", "offset");
        var offset = e.TryGetProperty("offset", out var o) ? Number(o) : 0;
        if (e.TryGetProperty("scale", out var scale) == e.TryGetProperty("over", out var over))
            throw new InvalidOperationException($"A map {e} names one of scale and over.");
        if (e.TryGetProperty("over", out _))
        {
            var d = Number(over);
            return x => x / d + offset;
        }
        var k = Number(scale);
        return x => x * k + offset;
    }

    private static Func<string, double> TextWidth(JsonElement e)
    {
        Allow(e, "a text width", "perCharacter");
        var each = Number(e.GetProperty("perCharacter"));
        return s => s.Length * each;
    }

    private static List<ChartValue?> Values(JsonElement e) => e.EnumerateArray().Select(Value).ToList();

    // A value: a number, null for a missing one, or a number JSON cannot hold, which ChartValue refuses.
    private static ChartValue? Value(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : ChartValue.Of(Number(e));

    // A position: a number, or a date as its day number.
    private static double Position(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object ? DateOnly.ParseExact(Only(e, "date").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture).DayNumber : Number(e);

    private static JsonElement Only(JsonElement e, string key)
    {
        Allow(e, key, key);
        return e.GetProperty(key);
    }

    private static double Number(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.String => e.GetString() switch
        {
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            "NaN" => double.NaN,
            var s => throw new InvalidOperationException($"\"{s}\" is not a number."),
        },
        _ => throw new InvalidOperationException($"{e} is not a number."),
    };

    private static T Enum<T>(JsonElement e) where T : struct, Enum
    {
        var name = e.GetString()!;
        var member = System.Enum.GetNames<T>().SingleOrDefault(n => Camel(n) == name)
            ?? throw new InvalidOperationException($"\"{name}\" is not a {typeof(T).Name}.");
        return System.Enum.Parse<T>(member);
    }

    private static void Allow(JsonElement e, string what, params string[] keys)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{what} is {e.ValueKind}, not an object.");
        foreach (var p in e.EnumerateObject())
            if (!keys.Contains(p.Name))
                throw new InvalidOperationException($"{what} has a key \"{p.Name}\", which the runner does not read.");
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    // ---- How a result is read ----

    /// <summary>A result as the JSON a case compares it as.</summary>
    internal static JsonNode? Serialise(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string s:
                return JsonValue.Create(s);
            case bool b:
                return JsonValue.Create(b);
            case double d:
                return double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(double.IsNaN(d) ? "NaN" : d > 0 ? "Infinity" : "-Infinity");
            case float or int or long or short or byte or decimal:
                return Serialise(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            case Enum e:
                return JsonValue.Create(Camel(e.ToString()));
            case ChartValue v:
                return Serialise(v.ToDouble());
            case Delegate:
                return null;
            case IDictionary dictionary:
            {
                var o = new JsonObject();
                foreach (DictionaryEntry kv in dictionary)
                    o[Convert.ToString(kv.Key, CultureInfo.InvariantCulture)!] = Serialise(kv.Value);
                return o;
            }
            case System.Runtime.CompilerServices.ITuple tuple when value.GetType().IsValueType:
            {
                var a = new JsonArray();
                for (var i = 0; i < tuple.Length; i++)
                    a.Add(Serialise(tuple[i]));
                return a;
            }
            case IEnumerable list:
            {
                var a = new JsonArray();
                foreach (var item in list)
                    a.Add(Serialise(item));
                return a;
            }
        }
        var obj = new JsonObject();
        foreach (var p in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0 || typeof(Delegate).IsAssignableFrom(p.PropertyType))
                continue;
            obj[Camel(p.Name)] = Serialise(p.GetValue(value));
        }
        return obj;
    }

    // ---- The checks ----

    private static readonly string[] CheckKeys =
    [
        "at", "yOf", "positionAt", "asked", "of", "where", "sortBy", "map", "differences", "sum", "count",
        "is", "isNot", "near", "above", "below", "atLeast", "atMost", "sameAs", "differsFrom", "has", "lacks", "mentions", "empty", "evenlySpaced", "startsWith", "each", "then",
    ];

    private static readonly string[] Assertions =
        ["is", "isNot", "above", "below", "atLeast", "atMost", "sameAs", "differsFrom", "has", "lacks", "mentions", "empty", "evenlySpaced", "startsWith", "each", "then"];

    private void Check(JsonNode? root, JsonNode? scope, JsonElement check)
    {
        Allow(check, "a check", CheckKeys);
        var where = $"\"{Name}\", check {check}";
        if (!Assertions.Any(a => check.TryGetProperty(a, out _)))
            throw new InvalidOperationException($"{where} asserts nothing.");
        var near = check.TryGetProperty("near", out var n) ? Number(n) : file.TryGetProperty("near", out var fn) ? Number(fn) : 0;
        var value = Subject(root, scope, check, where);

        if (check.TryGetProperty("where", out var filter))
            value = new JsonArray(Items(value, where).Where(item => Matches(Expected(filter), item, near)).Select(Copy).ToArray());
        if (check.TryGetProperty("sortBy", out var sortBy))
            value = new JsonArray(Items(value, where).OrderBy(item => AsNumber(Resolve(root, item, sortBy.GetString()!, where), where)).Select(Copy).ToArray());
        if (check.TryGetProperty("map", out var map))
            value = new JsonArray(Items(value, where).Select(item => Copy(Resolve(root, item, map.GetString()!, where))).ToArray());
        if (check.TryGetProperty("differences", out var differences) && differences.GetBoolean())
        {
            var xs = Items(value, where).Select(x => AsNumber(x, where)).ToList();
            value = new JsonArray(xs.Zip(xs.Skip(1), (a, b) => (JsonNode?)JsonValue.Create(b - a)).ToArray());
        }
        if (check.TryGetProperty("sum", out var sum) && sum.GetBoolean())
            value = JsonValue.Create(Items(value, where).Sum(x => AsNumber(x, where)));
        if (check.TryGetProperty("count", out var count) && count.GetBoolean())
            value = JsonValue.Create((double)Items(value, where).Count);

        foreach (var p in check.EnumerateObject())
        {
            switch (p.Name)
            {
                case "is":
                    Assert.True(Same(Expected(p.Value), value, near), $"{where}: is {Text(value)}.");
                    break;
                case "isNot":
                    Assert.False(Same(Expected(p.Value), value, near), $"{where}: is {Text(value)}.");
                    break;
                case "above":
                    Assert.True(AsNumber(value, where) > Reference(root, scope, p.Value, where), $"{where}: is {Text(value)}.");
                    break;
                case "below":
                    Assert.True(AsNumber(value, where) < Reference(root, scope, p.Value, where), $"{where}: is {Text(value)}.");
                    break;
                case "atLeast":
                    Assert.True(AsNumber(value, where) >= Reference(root, scope, p.Value, where) - near, $"{where}: is {Text(value)}.");
                    break;
                case "atMost":
                    Assert.True(AsNumber(value, where) <= Reference(root, scope, p.Value, where) + near, $"{where}: is {Text(value)}.");
                    break;
                case "sameAs":
                    Assert.True(Same(Other(root, scope, p.Value, where), value, near), $"{where}: is {Text(value)}, and the other {Text(Other(root, scope, p.Value, where))}.");
                    break;
                case "differsFrom":
                    Assert.False(Same(Other(root, scope, p.Value, where), value, near), $"{where}: both are {Text(value)}.");
                    break;
                case "has":
                    Assert.True(Has(value, Expected(p.Value), near, where), $"{where}: is {Text(value)}.");
                    break;
                case "mentions":
                    Assert.True(Items(value, where).Any(item => item is JsonValue t && t.GetValueKind() == JsonValueKind.String
                        && t.GetValue<string>().Contains(p.Value.GetString()!, StringComparison.Ordinal)), $"{where}: is {Text(value)}.");
                    break;
                case "lacks":
                    Assert.False(Has(value, Expected(p.Value), near, where), $"{where}: is {Text(value)}.");
                    break;
                case "empty":
                    Assert.True((Items(value, where).Count == 0) == p.Value.GetBoolean(), $"{where}: is {Text(value)}.");
                    break;
                case "evenlySpaced":
                {
                    var xs = Items(value, where).Select(x => AsNumber(x, where)).ToList();
                    var gaps = xs.Zip(xs.Skip(1), (a, b) => b - a).ToList();
                    Assert.True(gaps.Count > 0 && gaps.All(g => Math.Abs(g - gaps[0]) <= Math.Max(near, 1e-6)) == p.Value.GetBoolean(), $"{where}: is {Text(value)}.");
                    break;
                }
                case "startsWith":
                    Assert.True(value is JsonValue && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().StartsWith(p.Value.GetString()!, StringComparison.Ordinal),
                        $"{where}: is {Text(value)}.");
                    break;
                case "each":
                {
                    var items = Items(value, where);
                    Assert.True(items.Count > 0, $"{where}: has nothing to check each of.");
                    foreach (var item in items)
                        foreach (var inner in p.Value.EnumerateArray())
                            Check(root, item, inner);
                    break;
                }
                case "then":
                    foreach (var inner in p.Value.EnumerateArray())
                        Check(root, value, inner);
                    break;
            }
        }
    }

    // What a check reads: a path, a vertical axis' height of a value, the position at a pixel across, or
    // the positions a sampled series was asked at.
    private JsonNode? Subject(JsonNode? root, JsonNode? scope, JsonElement check, string where)
    {
        var forms = new[] { "at", "yOf", "positionAt", "asked" }.Where(k => check.TryGetProperty(k, out _)).ToList();
        if (forms.Count != 1)
            throw new InvalidOperationException($"{where} reads {forms.Count} things; a check reads one.");
        var scene = check.TryGetProperty("of", out var of) ? of.GetString()! : "";
        return forms[0] switch
        {
            "at" => Resolve(root, scope, check.GetProperty("at").GetString()!, where),
            "asked" => new JsonArray(_asked[check.GetProperty("asked").GetString()!].Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
            _ => JsonValue.Create(Computed(root, scope, check, forms[0], scene, where)),
        };
    }

    private double Computed(JsonNode? root, JsonNode? scope, JsonElement e, string form, string of, string where)
    {
        var scene = _results[of] as ChartScene ?? throw new InvalidOperationException($"{where}: {(of == "" ? "the answer" : of)} is not a scene.");
        if (form == "positionAt")
            return scene.PositionAt(Reference(root, scope, e.GetProperty("positionAt"), where));
        var args = e.GetProperty("yOf");
        var panel = scene.Panels[args[0].GetInt32()];
        var axis = (args[1].GetString() == "right" ? panel.Right : panel.Left) ?? throw new InvalidOperationException($"{where}: that panel has no {args[1]} axis.");
        return axis.YOf(Reference(root, scope, args[2], where), panel.Plot);
    }

    // A number a check compares with: a literal, or something read, scaled by `times` and moved by `plus`.
    private double Reference(JsonNode? root, JsonNode? scope, JsonElement e, string where)
    {
        if (e.ValueKind != JsonValueKind.Object)
            return Number(e);
        Allow(e, "a reference", "at", "yOf", "positionAt", "of", "times", "plus");
        var forms = new[] { "at", "yOf", "positionAt" }.Where(k => e.TryGetProperty(k, out _)).ToList();
        if (forms.Count != 1)
            throw new InvalidOperationException($"{where}: a reference reads one thing.");
        var of = e.TryGetProperty("of", out var o) ? o.GetString()! : "";
        var v = forms[0] == "at" ? AsNumber(Resolve(root, scope, e.GetProperty("at").GetString()!, where), where) : Computed(root, scope, e, forms[0], of, where);
        if (e.TryGetProperty("times", out var times)) v *= Number(times);
        if (e.TryGetProperty("plus", out var plus)) v += Reference(root, scope, plus, where);
        return v;
    }

    private static JsonNode? Other(JsonNode? root, JsonNode? scope, JsonElement e, string where) =>
        e.ValueKind == JsonValueKind.String ? Resolve(root, scope, e.GetString()!, where) : throw new InvalidOperationException($"{where}: sameAs and differsFrom name a path.");

    /// <summary>
    /// The value at a path: dot-separated, read from the scope or, starting <c>$.</c>, from the root. A
    /// segment is a property, an index (negative from the end), <c>length</c> of a list, or
    /// <c>[key=value]</c>, the one element of a list whose key holds that value.
    /// </summary>
    internal static JsonNode? Resolve(JsonNode? root, JsonNode? scope, string path, string where)
    {
        var node = scope;
        if (path.StartsWith('$'))
        {
            node = root;
            path = path.Length > 1 ? path[2..] : "";
        }
        foreach (var segment in Segments(path))
        {
            switch (node)
            {
                case JsonArray array when segment == "length":
                    node = JsonValue.Create((double)array.Count);
                    break;
                case JsonArray array when segment.StartsWith('['):
                {
                    var eq = segment.IndexOf('=');
                    var key = segment[1..eq];
                    var wanted = Literal(segment[(eq + 1)..^1]);
                    var found = array.Where(item => Same(wanted, Resolve(root, item, key, where), 0)).ToList();
                    if (found.Count != 1)
                        throw new InvalidOperationException($"{where}: {found.Count} elements match {segment}.");
                    node = found[0];
                    break;
                }
                case JsonArray array:
                {
                    var i = int.Parse(segment, CultureInfo.InvariantCulture);
                    if (i < 0) i += array.Count;
                    if (i < 0 || i >= array.Count)
                        throw new InvalidOperationException($"{where}: {segment} is outside a list of {array.Count}.");
                    node = array[i];
                    break;
                }
                case JsonObject obj:
                    if (!obj.TryGetPropertyValue(segment, out node))
                        throw new InvalidOperationException($"{where}: there is no \"{segment}\" in {Text(obj)[..Math.Min(200, Text(obj).Length)]}.");
                    break;
                default:
                    throw new InvalidOperationException($"{where}: {segment} reads into {Text(node)}.");
            }
        }
        return node;
    }

    // A value a check expects, a date written {"date": "yyyy-MM-dd"} read as its day number.
    private static JsonNode? Expected(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object when e.TryGetProperty("date", out _) => JsonValue.Create(Position(e)),
        JsonValueKind.Object => new JsonObject(e.EnumerateObject().Select(p => KeyValuePair.Create(p.Name, Expected(p.Value)))),
        JsonValueKind.Array => new JsonArray(e.EnumerateArray().Select(Expected).ToArray()),
        _ => JsonNode.Parse(e.GetRawText()),
    };

    private static IEnumerable<string> Segments(string path)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '[') depth++;
            else if (path[i] == ']') depth--;
            else if (path[i] == '.' && depth == 0)
            {
                yield return path[start..i];
                start = i + 1;
            }
        }
        if (path.Length > 0) yield return path[start..];
    }

    // A selector's value: JSON where it reads as JSON, a text otherwise.
    private static JsonNode? Literal(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);
        }
    }

    private static List<JsonNode?> Items(JsonNode? value, string where) =>
        value is JsonArray a ? [.. a] : throw new InvalidOperationException($"{where}: {Text(value)} is not a list.");

    private static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    private static double AsNumber(JsonNode? node, string where) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? v.GetValue<double>()
        : throw new InvalidOperationException($"{where}: {Text(node)} is not a number.");

    /// <summary>Whether two values are the same: numbers within <paramref name="near"/>, objects by every key, a missing key null.</summary>
    internal static bool Same(JsonNode? expected, JsonNode? actual, double near)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null;
        switch (expected)
        {
            case JsonObject e:
                if (actual is not JsonObject a) return false;
                return e.Select(p => p.Key).Union(a.Select(p => p.Key))
                    .All(k => Same(e.TryGetPropertyValue(k, out var x) ? x : null, a.TryGetPropertyValue(k, out var y) ? y : null, near));
            case JsonArray e:
                return actual is JsonArray b && e.Count == b.Count && e.Zip(b).All(t => Same(t.First, t.Second, near));
        }
        if (actual is not JsonValue av || expected.GetValueKind() != av.GetValueKind())
            return false;
        return expected.GetValueKind() switch
        {
            JsonValueKind.Number => Math.Abs(expected.GetValue<double>() - av.GetValue<double>()) <= near,
            JsonValueKind.String => expected.GetValue<string>() == av.GetValue<string>(),
            _ => expected.GetValueKind() == av.GetValueKind(),
        };
    }

    // An element matching: an object holds every key given, anything else is the same.
    private static bool Matches(JsonNode? wanted, JsonNode? item, double near) =>
        wanted is JsonObject w
            ? item is JsonObject o && w.All(p => Same(p.Value, o.TryGetPropertyValue(p.Key, out var v) ? v : throw new InvalidOperationException($"There is no \"{p.Key}\" to match."), near))
            : Same(wanted, item, near);

    private static bool Has(JsonNode? value, JsonNode? wanted, double near, string where) =>
        value is JsonValue v && v.GetValueKind() == JsonValueKind.String
            ? v.GetValue<string>().Contains(wanted!.GetValue<string>(), StringComparison.Ordinal)
            : Items(value, where).Any(item => Matches(wanted, item, near));

    private static string Text(JsonNode? node) => node is null ? "null" : node.ToJsonString();

    // ---- The Chart Spec reader (ADR-0320; ADR-0370, section 3) ----

    // A spec case reads a Chart Spec against tables, and expects the reading or a refusal at a path. The reading
    // carries what the tool answers the AI, said at the case's "metrics" or the tool's own 800 by 400.
    private static readonly string[] SpecCaseKeys = ["name", "spec", "specText", "tables", "metrics", "refused", "expect"];

    private bool IsSpecCase => entry.TryGetProperty("spec", out _) || entry.TryGetProperty("specText", out _);

    private void RunSpec()
    {
        Allow(entry, "the spec case", SpecCaseKeys);
        if (entry.TryGetProperty("spec", out _) == entry.TryGetProperty("specText", out _))
            throw new InvalidOperationException($"\"{Name}\" gives its spec as one of spec and specText.");
        // An object is read as the file writes it; specText is the spec's text as it is, for text that is not
        // JSON, or a number whose spelling the reader reads.
        var text = entry.TryGetProperty("spec", out var spec) ? spec.GetRawText() : entry.GetProperty("specText").GetString()!;
        var tables = SpecTables();
        IChartData? Lookup(string handle) => tables.TryGetValue(handle, out var table) ? table : null;
        if (entry.TryGetProperty("refused", out var refused) == entry.TryGetProperty("expect", out var checks))
            throw new InvalidOperationException($"\"{Name}\" is either refused or expects a reading.");
        if (entry.TryGetProperty("refused", out _))
        {
            Allow(refused, "refused", "path", "message", "startsWith");
            var e = Assert.Throws<ChartSpecRefusedException>(() => ChartSpec.Read(text, Lookup));
            var path = refused.GetProperty("path").GetString()!;
            Assert.True(e.Path == path, $"\"{Name}\": refused at \"{e.Path}\", not \"{path}\": {e.Message}");
            if (refused.TryGetProperty("message", out _) == refused.TryGetProperty("startsWith", out _))
                throw new InvalidOperationException($"\"{Name}\"'s refusal says one of message and startsWith.");
            if (refused.TryGetProperty("message", out var message))
                Assert.True(e.Message == message.GetString(), $"\"{Name}\": the refusal says \"{e.Message}\".");
            else
                Assert.True(e.Message.StartsWith(refused.GetProperty("startsWith").GetString()!, StringComparison.Ordinal), $"\"{Name}\": the refusal says \"{e.Message}\".");
            // The tool answers the AI with the same refusal, and no reading.
            var refusal = ChartSpec.Answer(text, Lookup, SpecMetrics());
            Assert.True(!refusal.Accepted && refusal.Reading is null && refusal.Text == e.Message, $"\"{Name}\": the tool answers \"{refusal.Text}\".");
            return;
        }
        var reading = ChartSpec.Read(text, Lookup);
        var told = ChartSpec.Answer(text, Lookup, SpecMetrics());
        JsonNode? definition;
        try
        {
            definition = Serialise(reading.Definition);
        }
        catch (InvalidOperationException e)
        {
            definition = new JsonObject { ["refused"] = Refusal(e) };
        }
        var answer = new JsonObject
        {
            ["title"] = reading.Title, ["definition"] = definition, ["surface"] = Serialise(reading.Surface),
            ["answer"] = new JsonObject { ["accepted"] = told.Accepted, ["text"] = told.Text },
        };
        Assert.True(checks.GetArrayLength() > 0, $"\"{Name}\" checks nothing.");
        foreach (var check in checks.EnumerateArray())
            Check(answer, answer, check);
    }

    // The size the tool says the chart at: the case's "metrics", or null for the tool's own.
    private ChartMetrics? SpecMetrics() => entry.TryGetProperty("metrics", out var metrics) ? Metrics(metrics) : null;

    // The file's tables by handle, a case's own taking the place of the file's of the same handle.
    private Dictionary<string, IChartData> SpecTables(bool includeCase = true)
    {
        var tables = new Dictionary<string, IChartData>(StringComparer.Ordinal);
        foreach (var source in includeCase ? new[] { file, entry } : [file])
            if (source.TryGetProperty("tables", out var named))
                foreach (var table in named.EnumerateObject())
                    tables[table.Name] = ChartData.Of(table.Value.EnumerateObject()
                        .Select(column => (column.Name, (IList)column.Value.EnumerateArray().Select(SpecCell).ToArray())).ToArray());
        return tables;
    }

    // A table's value: null, a text, a bool, a number as a double, or one of {"decimal": "1.25"},
    // {"integer": 3} (a long), {"date": "yyyy-MM-dd"} (a DateOnly), {"dateTime": "yyyy-MM-ddTHH:mm:ss"} and
    // {"double": "NaN" | "Infinity" | "-Infinity"}.
    private static object? SpecCell(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.String:
                return e.GetString();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return e.GetBoolean();
            case JsonValueKind.Number:
                return e.GetDouble();
        }
        var form = e.EnumerateObject().Single();
        switch (form.Name)
        {
            case "decimal":
            {
                var written = form.Value.GetString()!;
                // TypeScript holds a decimal by its value, so 1.50 is written as 1.5 in both.
                if (written.Contains('.', StringComparison.Ordinal) && written.EndsWith('0'))
                    throw new InvalidOperationException($"The decimal {written} is written with a trailing zero; write it as its value.");
                return decimal.Parse(written, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            }
            case "integer":
                return form.Value.GetInt64();
            case "date":
                return DateOnly.ParseExact(form.Value.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            case "dateTime":
                return DateTime.ParseExact(form.Value.GetString()!, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            case "double":
                return Number(form.Value);
            default:
                throw new InvalidOperationException($"{e} is not a value a table holds.");
        }
    }

    // ---- The Chart Source (ADR-0322, ADR-0344) ----

    private static readonly string[] ThroughCaseKeys = ["name", "spec", "tables", "sources", "refused", "expect"];

    // A Chart Source query as a case writes it, every key given.
    private static ChartSourceQuery SourceQuery(JsonElement q)
    {
        Allow(q, "query", "data", "x", "y", "values", "from", "to", "pixels");
        double? End(string key) => q.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Number(q.GetProperty(key));
        return new ChartSourceQuery(q.GetProperty("data").GetString()!, q.GetProperty("x").GetString()!, q.GetProperty("y").GetString()!,
            Enum<AxisValues>(q.GetProperty("values")), End("from"), End("to"), q.GetProperty("pixels").GetInt32());
    }

    // An answer as a case reads it: its version, each kept row as its position across and its height (null for a gap),
    // and what each thinned pixel held.
    private static Dictionary<string, object?> AnswerRows(ChartSourceAnswer answer, ChartSourceQuery query) => new()
    {
        ["version"] = answer.Version,
        ["rows"] = Enumerable.Range(0, answer.Points.RowCount).Select(r => new Dictionary<string, object?>
        {
            ["x"] = Placed(answer.Points.Value(query.X, r)),
            ["y"] = answer.Points.Value(query.Y, r) is { } y ? Placed(y) : null,
        }).ToList(),
        ["thinned"] = answer.Thinned,
    };

    // A value kept as a number: a date as its day number, a whole number, a decimal or a double as a double.
    private static double Placed(object? value) => value switch
    {
        DateOnly d => d.DayNumber,
        DateTime t => DateOnly.FromDateTime(t).DayNumber,
        double or long or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"A Chart Source kept {value}, which is no position or height."),
    };

    // A Chart Spec read through Chart Sources over the case's tables (ADR-0344), each answering the versions
    // sources.versions lists for its handle in turn: the reading, the questions asked and the versions held to the
    // checks, or the refusal to its path and to how many questions were asked before it.
    private async Task RunThrough()
    {
        Allow(entry, "the sources case", ThroughCaseKeys);
        var text = entry.GetProperty("spec").GetRawText();
        var through = entry.GetProperty("sources");
        Allow(through, "sources", "pixels", "from", "to", "versions");
        var tables = SpecTables();
        var versions = through.GetProperty("versions");
        Allow(versions, "versions", [.. tables.Keys]);
        var asked = new List<ChartSourceQuery>();
        var sources = new Dictionary<string, IChartSource>(StringComparer.Ordinal);
        foreach (var listed in versions.EnumerateObject())
            sources[listed.Name] = new Answering(tables[listed.Name], new Queue<long>(listed.Value.EnumerateArray().Select(v => v.GetInt64())), asked, Name, listed.Name);
        double? from = through.TryGetProperty("from", out var f) ? Number(f) : null;
        double? to = through.TryGetProperty("to", out var t) ? Number(t) : null;
        Task<ChartSourceReading> Read() => ChartSpec.ReadAsync(text, h => sources.GetValueOrDefault(h), through.GetProperty("pixels").GetInt32(), from, to,
            TestContext.Current.CancellationToken);
        if (entry.TryGetProperty("refused", out var refused) == entry.TryGetProperty("expect", out var checks))
            throw new InvalidOperationException($"\"{Name}\" is either refused or expects a reading.");
        if (entry.TryGetProperty("refused", out _))
        {
            Allow(refused, "refused", "path", "message", "asked");
            var e = await Assert.ThrowsAsync<ChartSpecRefusedException>(Read);
            var path = refused.GetProperty("path").GetString()!;
            Assert.True(e.Path == path, $"\"{Name}\": refused at \"{e.Path}\", not \"{path}\": {e.Message}");
            Assert.True(e.Message == refused.GetProperty("message").GetString(), $"\"{Name}\": the refusal says \"{e.Message}\".");
            Assert.True(asked.Count == refused.GetProperty("asked").GetInt32(), $"\"{Name}\": {asked.Count} questions were asked before the refusal.");
            return;
        }
        var read = await Read();
        var answer = new JsonObject
        {
            ["title"] = read.Reading.Title, ["definition"] = Serialise(read.Reading.Definition),
            ["asked"] = Serialise(asked), ["versions"] = Serialise(read.Versions),
        };
        Assert.True(checks.GetArrayLength() > 0, $"\"{Name}\" checks nothing.");
        foreach (var check in checks.EnumerateArray())
            Check(answer, answer, check);
    }

    // A Chart Source over a case's table, answering the versions listed for it in turn.
    private sealed class Answering(IChartData table, Queue<long> versions, List<ChartSourceQuery> asked, string name, string handle) : IChartSource
    {
        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default)
        {
            asked.Add(query);
            if (!versions.TryDequeue(out var version))
                throw new InvalidOperationException($"\"{name}\": \"{handle}\" was asked more often than its versions say.");
            return ValueTask.FromResult(ChartSource.Thin(table, query, version));
        }
    }
}
