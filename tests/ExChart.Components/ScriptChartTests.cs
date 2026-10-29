using System.Globalization;
using System.Text.Json;
using Bunit;
using ExChart.Engine;
using ExChart.Script;
using ExGrid.Testing;
using Microsoft.JSInterop;
using Xunit;
using ScriptChart = ExChart.Script.ExChart;
using ScriptSurface = ExChart.Script.ExSurface;
using ScriptFunction = global::ExGrid.Data.ScriptFunction;

namespace ExChart.Components.Tests;

/// <summary>
/// ADR-0370 section 1: Blazor's ExChart and ExSurface over the Script Core. A definition crosses once for each one the
/// Consumer hands, with the texts its Formats write where they differ from the Script Core's own, and what the chart
/// would ask a .NET function of a value it does not hold is refused by name; a click hands back the Row Key the point
/// was handed in with, and one taken on a definition since replaced is not raised (spine rule 6).
/// </summary>
public class ScriptChartTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _chart;
    private readonly BunitJSModuleInterop _surface;

    public ScriptChartTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import"
            && invocation.Arguments[0] is string address && address.Contains("ex-chart-core.min.js", StringComparison.Ordinal));
        _module.Setup<string?>("stamp").SetResult(ScriptCoreAsset.Stamp);
        _chart = _module.SetupModule("attachChart", _ => true);
        _chart.SetupVoid("update", _ => true).SetVoidResult();
        _chart.SetupVoid("dispose", _ => true).SetVoidResult();
        _surface = _module.SetupModule("attachSurface", _ => true);
        _surface.SetupVoid("update", _ => true).SetVoidResult();
        _surface.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private static ChartPoint P(double x, double y, object? row = null) => new(x, ChartValue.Of(y)) { Row = row };

    private static ChartDefinition Bonds(params ChartPoint[] points) =>
        new(ChartAxis.Numbers("Years"), [ChartSeries.Of("bonds", "Bonds", SeriesKind.Observation, points)]);

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    private JsonElement Attached(BunitJSModuleInterop module, string function) => Parsed(Assert.Single(_module.Invocations[function]).Arguments[2]);

    private DotNetObjectReference<ScriptBridge> Bridge(string function)
        => Assert.IsType<DotNetObjectReference<ScriptBridge>>(Assert.Single(_module.Invocations[function]).Arguments[1]);

    [Fact] // ADR-0123 / ADR-0370 s1: the bundled module ExChart.Script imports answers the stamp the components are built with, at the version its address carries
    public void The_script_core_module_is_the_one_the_components_were_built_with()
    {
        const string module = "ex-chart-core.min.js";
        var stamp = ScriptCoreAsset.Stamp;
        var shipped = File.ReadAllText(ShippedFiles.PathOf("ExChart", module));
        Assert.StartsWith($"/*! ex-chart-core.js sha256:{stamp} ", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{stamp}\"", shipped, StringComparison.Ordinal);
        Assert.Equal(ScriptCoreAsset.Version, ShippedFiles.Version("ExChart", module));
        Assert.Contains($"sourceMappingURL={module}.map", shipped, StringComparison.Ordinal);
        // The grid core and the data come from the Shared Script ExGrid ships, at the address its stamp names, which the
        // module expects (ADR-0411).
        var shared = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(ShippedFiles.PathOf("ExGrid", "ex-grid-script.min.js")),
            "^/\\*! ex-grid-script\\.js sha256:(?<stamp>[0-9a-f]{64}) ").Groups["stamp"].Value;
        Assert.Matches("^[0-9a-f]{64}$", shared);
        Assert.Contains($"from\"../ExGrid/ex-grid-script.min.js?v={shared[..12]}\"", shipped, StringComparison.Ordinal);
        Assert.Contains($"\"{shared}\"", shipped, StringComparison.Ordinal);
    }

    [Fact] // ADR-0370 s1: the definition crosses with the attach, and again only when the Consumer hands another
    public void The_definition_crosses_once_for_each_one_handed()
    {
        var first = Bonds(P(0, 1), P(1, 2));
        var cut = Render<ScriptChart>(ps => ps.Add(c => c.Definition, first));
        var attach = Attached(_module, "attachChart");
        Assert.Equal(1, attach.GetProperty("generation").GetInt32());
        Assert.Equal("observation", attach.GetProperty("definition").GetProperty("series")[0].GetProperty("kind").GetString());

        cut.Render(ps => ps.Add(c => c.Definition, first).Add(c => c.Title, "Bonds"));
        var update = Parsed(Assert.Single(_chart.Invocations["update"]).Arguments[0]);
        Assert.False(update.TryGetProperty("definition", out _));
        Assert.Equal("Bonds", update.GetProperty("options").GetProperty("title").GetString());

        cut.Render(ps => ps.Add(c => c.Definition, Bonds(P(0, 3))).Add(c => c.Title, "Bonds"));
        var replaced = Parsed(_chart.Invocations["update"][^1].Arguments[0]);
        Assert.Equal(2, replaced.GetProperty("generation").GetInt32());

        // The same parameters again cross nothing.
        cut.Render(ps => ps.Add(c => c.Title, "Bonds"));
        Assert.Equal(2, _chart.Invocations["update"].Count);
    }

    [Fact] // ADR-0370 s1: a value a Format writes another way crosses as its text; one written as the Script Core writes it crosses as the number alone
    public void A_Format_crosses_as_the_texts_it_writes_where_they_differ()
    {
        var series = ChartSeries.Of("amount", "Amount", SeriesKind.Level, [new(0, 3.30m), new(1, 5m), new(2, 2.5)]);
        var (json, _) = ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [series]));
        var texts = Parsed(json).GetProperty("series")[0].GetProperty("texts");
        // A decimal keeps the digits it was handed in with, as C#'s own writing does.
        Assert.Equal("""[[3.3,"3.30"]]""", texts.GetRawText());

        var percent = series with { Format = v => Convert.ToDouble(v, CultureInfo.InvariantCulture).ToString("0.0", CultureInfo.InvariantCulture) + "%" };
        var (written, _) = ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [percent]));
        Assert.Equal("""[[3.3,"3.3%"],[5,"5.0%"],[2.5,"2.5%"]]""", Parsed(written).GetProperty("series")[0].GetProperty("texts").GetRawText());
    }

    [Fact] // ADR-0370 s1, spine rule 1: what the chart would ask a .NET function of a value it does not hold is refused by name
    public void What_the_chart_would_ask_a_function_of_is_refused_by_name()
    {
        var axisFormat = new ChartDefinition(ChartAxis.Numbers() with { Format = x => $"{x}y" }, [ChartSeries.Of("a", "A", SeriesKind.Level, [P(0, 1), P(1, 2)])]);
        Assert.Contains("axis has a Format", Assert.Throws<NotSupportedException>(() => ChartWire.Definition(axisFormat)).Message, StringComparison.Ordinal);

        var sampled = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Sampled("f", "Fitted", SeriesKind.Level, 0, 1, x => ChartValue.Of(x))]);
        Assert.Contains("'Fitted' is sampled from a .NET function", Assert.Throws<NotSupportedException>(() => ChartWire.Definition(sampled)).Message, StringComparison.Ordinal);

        // 3.3 and 3.30 are one number in the browser, and the Format writes them apart.
        var both = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("a", "A", SeriesKind.Level, [new(0, 3.3m), new(1, 3.30m)])]);
        Assert.Contains("writes the value 3.3 both as \"3.3\" and as \"3.30\"", Assert.Throws<NotSupportedException>(() => ChartWire.Definition(both)).Message, StringComparison.Ordinal);

        var keys = Bonds(P(0, 1, row: 1), P(1, 2, row: 1.0m));
        Assert.Contains("Row Keys 1 (Int32) and 1.0 (Decimal)", Assert.Throws<NotSupportedException>(() => ChartWire.Definition(keys)).Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0258 / ADR-0370 s1 / spine rule 6: a click hands back the Row Key the point was handed in with; one taken on a definition since replaced is not raised
    public async Task A_click_hands_back_its_Row_Key_and_one_on_a_replaced_definition_is_not_raised()
    {
        var key = new DateOnly(2026, 10, 1);
        var clicks = new List<ChartPointClick>();
        var cut = Render<ScriptChart>(ps => ps
            .Add(c => c.Definition, Bonds(P(0, 1, row: key), P(1, 2, row: "b")))
            .Add(c => c.OnPointClick, (ChartPointClick c) => clicks.Add(c)));
        var rows = Attached(_module, "attachChart").GetProperty("definition").GetProperty("series")[0].GetProperty("rows");
        // A key that is neither text, a number nor a Boolean crosses as C# writes it into a sentence, invariantly.
        Assert.Equal("""["10/01/2026","b"]""", rows.GetRawText());
        Assert.Equal("pointClick", Attached(_module, "attachChart").GetProperty("options").GetProperty("hears")[0].GetString());

        var bridge = Bridge("attachChart").Value;
        await bridge.Heard("pointClick", Parsed("""{"generation":1,"seriesId":"bonds","position":0,"row":"10/01/2026"}"""));
        Assert.Equal([new ChartPointClick("bonds", 0, key)], clicks);

        cut.Render(ps => ps.Add(c => c.Definition, Bonds(P(0, 1, row: "a"))));
        await bridge.Heard("pointClick", Parsed("""{"generation":1,"seriesId":"bonds","position":1,"row":"b"}"""));
        Assert.Single(clicks);
    }

    [Fact] // ADR-0272 / ADR-0370 s1: the range the user narrowed is raised in the data's terms, an open end absent
    public async Task A_range_is_raised_in_the_datas_terms()
    {
        var ranges = new List<ChartRange?>();
        Render<ScriptChart>(ps => ps
            .Add(c => c.Definition, Bonds(P(0, 1), P(5, 2)))
            .Add(c => c.Range, new ChartRange(1, null))
            .Add(c => c.OnRangeChanged, (ChartRange? r) => ranges.Add(r)));
        Assert.Equal("""{"from":1}""", Attached(_module, "attachChart").GetProperty("options").GetProperty("range").GetRawText());

        var bridge = Bridge("attachChart").Value;
        await bridge.Heard("rangeChanged", Parsed("""{"from":2,"to":4}"""));
        await bridge.Heard("rangeChanged", Parsed("null"));
        Assert.Equal([new ChartRange(2, 4), null], ranges);
    }

    [Fact] // ADR-0123: a module of another version is refused, and the root says so in the built-in words
    public void A_module_of_another_version_is_refused()
    {
        _module.Setup<string?>("stamp").SetResult("another");
        var cut = Render<ScriptChart>(ps => ps.Add(c => c.Definition, Bonds(P(0, 1))));
        cut.WaitForAssertion(() => Assert.Equal("alert", cut.Find(".ex-chart-script-notice").GetAttribute("role")));
        Assert.Empty(_module.Invocations["attachChart"]);
    }

    [Fact] // ADR-0370 s1: a circuit that is closing calls off the dispose it was sent, and the component lets go quietly
    public async Task A_dispose_the_closing_circuit_calls_off_is_not_raised()
    {
        _surface.SetupVoid("dispose", _ => true).SetCanceled();
        Render<ScriptSurface>(ps => ps.Add(c => c.Definition, Vol()));
        Assert.Single(_module.Invocations["attachSurface"]);
        await DisposeComponentsAsync();
        Assert.Single(_surface.Invocations["dispose"]);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    private static SurfaceDefinition Vol() => new(
        SurfaceAxis.Of("Strike", new[] { 0.50m, 1.25m, 2.00m }) with { Scale = AxisScale.Categories },
        SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
        [[10.5, 9.2, 9.9], [10.9, 9.6, 10.2]],
        "Volatility");

    [Fact] // ADR-0274 / ADR-0370 s1: a slice crosses as the value the browser holds, and comes back as the axis' own
    public async Task A_slice_crosses_as_the_browsers_value_and_comes_back_as_the_axis_own()
    {
        var slices = new List<SurfaceSlice>();
        var cut = Render<ScriptSurface>(ps => ps
            .Add(c => c.Definition, Vol())
            .Add(c => c.Slice, SurfaceSlice.AtX(1.25m))
            .Add(c => c.OnSliceChanged, (SurfaceSlice s) => slices.Add(s)));
        var attach = Attached(_module, "attachSurface");
        Assert.Equal("""{"axis":"x","value":1.25}""", attach.GetProperty("options").GetProperty("slice").GetRawText());
        // The decimals' own writing differs from the number's: every value crosses with its text.
        Assert.Equal("""[[0.5,"0.50"],[1.25,"1.25"],[2,"2.00"]]""", attach.GetProperty("definition").GetProperty("x").GetProperty("writing").GetRawText());

        var bridge = Bridge("attachSurface").Value;
        await bridge.Heard("sliceChanged", Parsed("""{"generation":1,"axis":"x","value":2}"""));
        await bridge.Heard("sliceChanged", Parsed("""{"generation":1,"axis":"y","value":"1Y"}"""));
        Assert.Equal([SurfaceSlice.AtX(2m), SurfaceSlice.AtY("1Y")], slices);
        Assert.IsType<decimal>(slices[0].Value);
        _ = cut;
    }

    [Fact] // ADR-0370 s1, spine rule 1: a Format on an axis placed by value would be asked to write its ticks, and is refused by name
    public void A_Format_the_surface_would_ask_of_a_tick_is_refused()
    {
        var byValue = new SurfaceDefinition(
            SurfaceAxis.Of("Strike", new[] { 0.5, 1.0, 2.0 }, v => v.ToString("0.00", CultureInfo.InvariantCulture)) with { Scale = AxisScale.Proportional },
            SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
            [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]);
        Assert.Contains("ticks between its values", Assert.Throws<NotSupportedException>(() => ChartWire.Surface(byValue)).Message, StringComparison.Ordinal);

        // A Format that writes every held value plainly, and a tick between them its own way.
        var agreesAtValues = new SurfaceDefinition(
            SurfaceAxis.Of("Strike", new[] { 1.5, 2.5, 3.5 }, v => v.ToString("0.0", CultureInfo.InvariantCulture)) with { Scale = AxisScale.Proportional },
            SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
            [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]);
        Assert.Contains("ticks between its values", Assert.Throws<NotSupportedException>(() => ChartWire.Surface(agreesAtValues)).Message, StringComparison.Ordinal);

        // A sampled surface's axis writes plainly at its values and its ticks alike, and crosses as such.
        var sampled = SurfaceDefinition.Sampled("f", 0, 1, 0, 1, (x, y) => ChartValue.Of(x + y), "x", "y", xScale: AxisScale.Proportional);
        var (json, _) = ChartWire.Surface(sampled);
        Assert.Equal("plain", Parsed(json).GetProperty("x").GetProperty("writing").GetString());
    }

    [Fact] // ADR-0311 / ADR-0370 s1: a mesh's slice reads values between its nodes, so a Format on its values is refused whatever it writes at them
    public void A_Format_on_a_meshs_values_is_refused()
    {
        SurfaceNode[] nodes = [new(0, 0, 1.5), new(1, 0, 2.25), new(0, 1, 3.0)];
        var mesh = SurfaceDefinition.FromTriangles(nodes, [new SurfaceTriangle(0, 1, 2)], "x", "y", "z", zFormat: v => Convert.ToDouble(v, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.InvariantCulture));
        Assert.Contains("between its nodes", Assert.Throws<NotSupportedException>(() => ChartWire.Surface(mesh)).Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0420: a surface's number codes cross as themselves, in place of the Formats a mesh refuses; a date code's axis crosses as the texts it writes
    public void A_surfaces_codes_cross_in_place_of_its_Formats()
    {
        SurfaceNode[] nodes = [new(0, 0, 0.015), new(1, 0, 0.0225), new(0, 1, 0.03)];
        var mesh = SurfaceDefinition.FromTriangles(nodes, [new SurfaceTriangle(0, 1, 2)], "Strike", "Tenor", "Rate", xFormatCode: "0.00") with { ZFormatCode = "0.00%" };
        var parsed = Parsed(ChartWire.Surface(mesh).Json);
        Assert.Equal("0.00", parsed.GetProperty("xFormatCode").GetString());
        Assert.Equal(JsonValueKind.Null, parsed.GetProperty("yFormatCode").ValueKind);
        Assert.Equal("0.00%", parsed.GetProperty("zFormatCode").GetString());
        Assert.Equal(JsonValueKind.Null, parsed.GetProperty("zTexts").ValueKind);

        var dated = new SurfaceDefinition(
            SurfaceAxis.Of("Expiry", new[] { new DateOnly(2026, 12, 18), new DateOnly(2027, 3, 19) }) with { FormatCode = "mmm yy" },
            SurfaceAxis.Of("Delta", new[] { 0.25, 0.5 }) with { FormatCode = "0%" },
            [[1.0, 2.0], [3.0, 4.0]]);
        var grid = Parsed(ChartWire.Surface(dated).Json);
        Assert.Equal("""["Dec 26","Mar 27"]""", grid.GetProperty("x").GetProperty("values").GetRawText());
        Assert.Equal(JsonValueKind.Null, grid.GetProperty("x").GetProperty("formatCode").ValueKind);
        Assert.Equal("0%", grid.GetProperty("y").GetProperty("formatCode").GetString());
        Assert.Equal(JsonValueKind.Null, grid.GetProperty("y").GetProperty("writing").ValueKind);
    }

    [Fact] // ADR-0370 s1: two axis values that would be one value in the browser are refused, as a slice at either could not say which
    public void Axis_values_that_cross_alike_are_refused()
    {
        var mixed = new SurfaceDefinition(
            new SurfaceAxis("Strike", [1, 1.0m, 2]),
            SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
            [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]);
        Assert.Contains("holds 1 (Int32) and 1.0 (Decimal)", Assert.Throws<NotSupportedException>(() => ChartWire.Surface(mixed)).Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0300 / ADR-0370 s1: a Price Range's values cross as numbers, so a candle's change, which the chart computes, is never written by a held value's text
    public void A_price_ranges_values_cross_without_texts()
    {
        var candles = ChartSeries.Candles("ix", "Index", [new PriceBar(20000, 3.30m, 3.50m, 3.10m, 3.30m, 100m), new PriceBar(20001, 3.30m, 6.70m, 3.30m, 6.60m, 120m)]);
        var (json, _) = ChartWire.Definition(new ChartDefinition(ChartAxis.Dates(), [candles]));
        Assert.Equal(JsonValueKind.Null, Parsed(json).GetProperty("series")[0].GetProperty("texts").ValueKind);
    }

    [Fact] // ADR-0420: a format code crosses as itself, for the Script Core's reader to write every value by, so a candle's change is written by it too
    public void A_format_code_crosses_as_itself_in_place_of_texts()
    {
        var candles = ChartSeries.Candles("ix", "Index", [new PriceBar(20000, 3.30m, 3.50m, 3.10m, 3.30m, 100m)]) with { FormatCode = "#,##0.00" };
        var (json, _) = ChartWire.Definition(new ChartDefinition(ChartAxis.Dates() with { FormatCode = "dd/mm/yyyy" }, [candles]));
        var parsed = Parsed(json);
        Assert.Equal("dd/mm/yyyy", parsed.GetProperty("axis").GetProperty("formatCode").GetString());
        var series = parsed.GetProperty("series")[0];
        Assert.Equal("#,##0.00", series.GetProperty("formatCode").GetString());
        Assert.Equal(JsonValueKind.Null, series.GetProperty("texts").ValueKind);

        var plain = ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("a", "A", SeriesKind.Level, [P(0, 1)])])).Json;
        Assert.Equal(JsonValueKind.Null, Parsed(plain).GetProperty("axis").GetProperty("formatCode").ValueKind);
    }

    [Fact] // ADR-0420 s4: a series sampled from the family's own curve crosses as what it was made from; one whose function was replaced is refused
    public void A_family_curve_crosses_as_what_it_was_made_from()
    {
        var spline = Engine.Interpolation.Interpolate.NaturalCubicSpline([0, 1, 2], [0, 1, 0]);
        var fitted = Engine.Fitting.Fit.LeastSquares([1, 2, 3, 4], [2, 4, 7, 15], Engine.Fitting.FitSpace.LogY);
        var (json, _) = ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [spline.Series("s", "Spline"), fitted.Series("f", "Fit")]));
        var series = Parsed(json).GetProperty("series");
        Assert.Equal("""{"from":0,"to":2,"madeFrom":{"kind":"naturalCubicSpline","xs":[0,1,2],"ys":[0,1,0]}}""", series[0].GetProperty("sampler").GetRawText());
        var fit = series[1].GetProperty("sampler").GetProperty("madeFrom");
        Assert.Equal("fit", fit.GetProperty("kind").GetString());
        Assert.Equal("logY", fit.GetProperty("space").GetString());
        Assert.Equal(fitted.Coefficients, fit.GetProperty("coefficients").EnumerateArray().Select(c => c.GetDouble()));

        // The curve's function replaced, or the sampler's: the function is the Consumer's.
        var replaced = (spline with { Evaluate = x => x }).Series("r", "Replaced");
        Assert.Contains("'Replaced' is sampled from a .NET function", Assert.Throws<NotSupportedException>(
            () => ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [replaced]))).Message, StringComparison.Ordinal);
        var linear = Engine.Interpolation.Interpolate.Linear([0, 1, 2], [0, 1, 0]);
        var straight = Parsed(ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [linear.Series("l", "Linear")])).Json).GetProperty("series")[0];
        Assert.Equal("linear", straight.GetProperty("sampler").GetProperty("madeFrom").GetProperty("kind").GetString());
        var renamed = (linear with { Method = "mine" }).Series("n", "Renamed");
        Assert.Throws<NotSupportedException>(() => ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [renamed])));
        var wrapped = fitted.Series("w", "Wrapped") with { Sampler = new SeriesSampler(1, 4, x => fitted.ValueAt(x)) };
        Assert.Throws<NotSupportedException>(() => ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [wrapped])));
        var formatted = spline.Series("p", "Formatted") with { Format = v => $"{v}" };
        Assert.Contains("write the values it samples", Assert.Throws<NotSupportedException>(
            () => ChartWire.Definition(new ChartDefinition(ChartAxis.Numbers(), [formatted]))).Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0420 s3: the Consumer's own script crosses as its module and export wherever it stands; a component drawing in .NET refuses it
    public void A_script_function_crosses_by_name_and_the_razor_component_refuses_it()
    {
        var tenor = ScriptFunction.Of("./js/chart-formats.js", "tenor");
        var smile = ScriptFunction.Of("./js/curves.js", "smile");
        var definition = new ChartDefinition(ChartAxis.Numbers() with { FormatScript = tenor },
        [
            ChartSeries.Of("a", "A", SeriesKind.Level, [P(0, 1), P(1, 2)]) with { FormatScript = smile },
            ChartSeries.Sampled("s", "Smile", SeriesKind.Level, 0, 1, smile),
        ]);
        var parsed = Parsed(ChartWire.Definition(definition).Json);
        Assert.Equal("""{"module":"./js/chart-formats.js","export":"tenor"}""", parsed.GetProperty("axis").GetProperty("formatScript").GetRawText());
        Assert.Equal(JsonValueKind.Null, parsed.GetProperty("series")[0].GetProperty("texts").ValueKind);
        Assert.Equal("""{"kind":"script","module":"./js/curves.js","export":"smile"}""",
            parsed.GetProperty("series")[1].GetProperty("sampler").GetProperty("madeFrom").GetRawText());

        var surface = new SurfaceDefinition(SurfaceAxis.Of("x", [1.0, 2.0]) with { FormatScript = tenor }, SurfaceAxis.Of("y", [1.0, 2.0]),
            [[ChartValue.Of(1.0), ChartValue.Of(2.0)], [ChartValue.Of(3.0), ChartValue.Of(4.0)]], "z") { ZFormatScript = smile };
        var wired = Parsed(ChartWire.Surface(surface).Json);
        Assert.Equal("tenor", wired.GetProperty("x").GetProperty("formatScript").GetProperty("export").GetString());
        Assert.Equal("smile", wired.GetProperty("zFormatScript").GetProperty("export").GetString());

        Assert.Contains("only a chart drawn in the browser calls", Assert.Throws<NotSupportedException>(
            () => Render<global::ExChart.Components.ExChart>(ps => ps.Add(c => c.Definition, definition))).Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => Render<global::ExChart.Components.ExSurface>(ps => ps.Add(c => c.Definition, surface)));
    }

    // ---- Read through Chart Sources (ADR-0322, ADR-0344) ----

    private const string TicksSpec = """
        { "version": 1, "axis": { "values": "numbers", "title": "Second" },
          "series": [ { "id": "p", "name": "Price", "kind": "level", "data": "ticks", "x": "t", "y": "price", "fromZero": false } ] }
        """;

    // Ten thousand ticks, one spike at 7,000 that every thinning keeps.
    private static ChartData Ticks() => ChartData.Of(
        ("t", Enumerable.Range(0, 10_000).Select(i => (object?)(double)i).ToArray()),
        ("price", Enumerable.Range(0, 10_000).Select(i => (object?)(i == 7_000 ? 500m : 100m + i % 7)).ToArray()));

    /// <summary>A source whose answers the test releases, each thinned as the reference thins.</summary>
    private sealed class Gated(ChartData data) : IChartSource
    {
        public List<(ChartSourceQuery Query, TaskCompletionSource<ChartSourceAnswer> Answer)> Asks { get; } = [];

        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default)
        {
            var answer = new TaskCompletionSource<ChartSourceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
            Asks.Add((query, answer));
            return new ValueTask<ChartSourceAnswer>(answer.Task);
        }

        public void Answer(int ask, long version = 1) => Asks[ask].Answer.SetResult(ChartSource.Thin(data, Asks[ask].Query, version));
    }

    private IRenderedComponent<ScriptChart> FromSpec(Gated source) => Render<ScriptChart>(ps => ps
        .Add(c => c.Spec, TicksSpec)
        .Add(c => c.Sources, h => h == "ticks" ? source : null)
        .Add(c => c.Width, 600));

    // The definitions that crossed after the attach, with their generations.
    private List<JsonElement> DefinitionsSent() => _chart.Invocations["update"]
        .Select(i => Parsed(i.Arguments[0])).Where(u => u.TryGetProperty("definition", out _)).ToList();

    [Fact] // ADR-0344 / ADR-0370 s1: a spec is read through its source in .NET, at the chart's pixels; nothing is drawn and the root is Busy until the answer, which crosses as a definition with what each pixel held
    public void A_spec_is_read_through_its_source_and_crosses_as_a_definition()
    {
        var source = new Gated(Ticks());
        var cut = FromSpec(source);
        var (query, _) = Assert.Single(source.Asks);
        Assert.Equal(1200, query.Pixels);
        Assert.Equal("true", cut.Find("div").GetAttribute("aria-busy"));
        Assert.Equal(0, Attached(_module, "attachChart").GetProperty("definition").GetProperty("series").GetArrayLength());

        source.Answer(0);
        cut.WaitForAssertion(() => Assert.Single(DefinitionsSent()));
        var series = DefinitionsSent()[0].GetProperty("definition").GetProperty("series")[0];
        Assert.Equal("p", series.GetProperty("id").GetString());
        Assert.NotEqual(0, series.GetProperty("thinned").GetArrayLength()); // the chart offers no CSV of points it does not hold
        Assert.Null(cut.Find("div").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0344, spine rule 6: a range narrowed by the parameter or by the user is asked again; what crossed stays until the newest answer, and an overtaken one is dropped
    public async Task A_narrowed_range_is_asked_again_and_an_overtaken_answer_is_dropped()
    {
        var source = new Gated(Ticks());
        var cut = FromSpec(source);
        source.Answer(0);
        await cut.WaitForStateAsync(() => DefinitionsSent().Count == 1);
        // Heard although the Consumer does not, so the user's range is asked for.
        Assert.Contains("rangeChanged", Attached(_module, "attachChart").GetProperty("options").GetProperty("hears").EnumerateArray().Select(e => e.GetString()));

        var updates = _chart.Invocations["update"].Count;
        cut.Render(ps => ps.Add(c => c.Range, new ChartRange(1000, 2000)));
        // The range crosses with the points read over it, never before them.
        Assert.Equal(updates, _chart.Invocations["update"].Count);
        // Handled without waiting on the source: the ask it makes is not awaited, so no later event waits on it.
        await Bridge("attachChart").Value.Heard("rangeChanged", Parsed("""{"from":3000,"to":4000}"""));
        Assert.Equal(3, source.Asks.Count);
        Assert.Equal((3000.0, 4000.0), (source.Asks[2].Query.From!.Value, source.Asks[2].Query.To!.Value));
        Assert.Single(DefinitionsSent());

        source.Answer(2);
        await cut.WaitForStateAsync(() => DefinitionsSent().Count == 2);
        source.Answer(1);
        Assert.Equal(2, DefinitionsSent().Count);
        Assert.Null(cut.Find("div").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0322, spine rule 1: an answer from data older than the chart drew is said in its place, and the drawing is hidden under it
    public void An_answer_from_older_data_is_said_in_the_charts_place()
    {
        var source = new Gated(Ticks());
        var cut = FromSpec(source);
        source.Answer(0, version: 5);
        cut.WaitForAssertion(() => Assert.Single(DefinitionsSent()));

        cut.Render(ps => ps.Add(c => c.Range, new ChartRange(6000, 8000)));
        source.Answer(1, version: 4);
        cut.WaitForAssertion(() => Assert.Contains("version 4 of 'ticks', older than version 5", cut.Find(".ex-chart-source-refusal").TextContent, StringComparison.Ordinal));
        // The drawing beside it is hidden.
        Assert.Single(cut.FindAll("div[hidden]"));
        Assert.Single(DefinitionsSent());
    }

    [Fact] // ADR-0344, spine rule 1: a spec refused as it is read is said in the chart's place, in the chart's box, from the first render
    public void A_spec_refused_at_once_is_said_in_the_charts_place()
    {
        var cut = Render<ScriptChart>(ps => ps
            .Add(c => c.Spec, TicksSpec.Replace("\"ticks\"", "\"quotes\"", StringComparison.Ordinal))
            .Add(c => c.Sources, _ => null)
            .Add(c => c.Width, 600)
            .Add(c => c.Height, 300));
        var refusal = cut.Find(".ex-chart-source-refusal");
        Assert.StartsWith("series[0].data \"quotes\" is not known", refusal.TextContent, StringComparison.Ordinal);
        Assert.Equal("width:600px;min-height:300px", refusal.ParentElement!.GetAttribute("style"));
    }

    [Fact] // ADR-0254, ADR-0344: a chart made wider asks its source again for its new pixels
    public void A_wider_chart_asks_its_source_again()
    {
        var source = new Gated(Ticks());
        var cut = FromSpec(source);
        source.Answer(0);
        cut.Render(ps => ps.Add(c => c.Width, 900));
        Assert.Equal(1800, source.Asks[^1].Query.Pixels);
    }

    [Fact] // ADR-0344, spine rule 6: a Definition handed while an ask is out is drawn, and the ask's failure is dropped
    public void An_ask_left_out_by_a_definition_is_dropped()
    {
        var source = new Gated(Ticks());
        var cut = FromSpec(source);
        cut.Render(ps => ps.Add(c => c.Spec, (string?)null).Add(c => c.Sources, null).Add(c => c.Definition, Bonds(P(0, 1))));
        Assert.Null(cut.Find("div").GetAttribute("aria-busy"));

        source.Asks[0].Answer.SetException(new TaskCanceledException("timed out"));
        Assert.Empty(cut.FindAll(".ex-chart-source-refusal"));
        Assert.Equal("bonds", DefinitionsSent()[^1].GetProperty("definition").GetProperty("series")[0].GetProperty("id").GetString());
    }
}
