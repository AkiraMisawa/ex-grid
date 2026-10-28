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
        Assert.Contains("'Fitted' is a function", Assert.Throws<NotSupportedException>(() => ChartWire.Definition(sampled)).Message, StringComparison.Ordinal);

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
}
