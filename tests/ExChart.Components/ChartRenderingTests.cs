using Bunit;
using ExChart.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

public class ChartRenderingTests : BunitContext
{
    private static string ModuleAddress => $"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}";

    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _handle;

    public ChartRenderingTests()
    {
        _module = JSInterop.SetupModule(ModuleAddress);
        _module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        _handle = _module.SetupModule("attach", _ => true);
        _handle.SetupVoid("draw", _ => true).SetVoidResult();
        _handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);

    private static ChartDefinition Exposure(double epeLast = 9) => new(ChartAxis.Numbers("Years"),
    [
        ChartSeries.Of("epe-before", "EPE · Before", SeriesKind.Level, [P(0, 1), P(1, 5), P(2, epeLast)]) with { Panel = "EPE", Keys = new("Before") },
        ChartSeries.Of("epe-after", "EPE · After", SeriesKind.Level, [P(0, 2), P(1, 4), P(2, 8)]) with { Panel = "EPE", Keys = new("After") },
        ChartSeries.Of("ene-after", "ENE · After", SeriesKind.Level, [P(0, -1), P(1, -3), P(2, -2)]) with { Panel = "ENE", Keys = new("After") },
    ]);

    private IRenderedComponent<ChartComponent> Render(ChartDefinition definition, Action<ComponentParameterCollectionBuilder<ChartComponent>>? more = null) =>
        Render<ChartComponent>(ps =>
        {
            ps.Add(c => c.Definition, definition);
            more?.Invoke(ps);
        });

    private static Dictionary<string, int> Counts(IRenderedComponent<ChartComponent> cut) => new()
    {
        ["axes"] = cut.FindComponents<ChartAxesView>().Sum(c => c.RenderCount),
        ["series"] = cut.FindComponents<ChartSeriesView>().Sum(c => c.RenderCount),
        ["legend"] = cut.FindComponents<ChartLegendView>().Sum(c => c.RenderCount),
        ["guide"] = cut.FindComponents<ChartGuideView>().Sum(c => c.RenderCount),
        ["tooltip"] = cut.FindComponents<ChartTooltipView>().Sum(c => c.RenderCount),
    };

    [Fact] // ADR-0252: the scene is SVG elements through the render tree, one component per part
    public void The_scene_renders_as_svg_elements()
    {
        var cut = Render(Exposure());
        Assert.Equal(2, cut.FindComponents<ChartAxesView>().Count);
        Assert.Equal(3, cut.FindComponents<ChartSeriesView>().Count);
        Assert.Equal(2, cut.FindComponents<ChartLegendView>().Count);
        Assert.Equal(3, cut.FindAll("g.ex-chart-series path.ex-chart-path").Count);
        Assert.Equal(3, cut.FindAll("g.ex-chart-legend-entry").Count);
    }

    [Theory] // ADR-0252, ADR-0254, CH-3: a series' SVG node count does not grow with its points
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void A_series_node_count_does_not_grow_with_its_points(int count)
    {
        var points = Enumerable.Range(0, count).Select(i => P(i * 1000.0 / count, Math.Sin(i * 0.01))).ToList();
        var cut = Render(new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("s", "S", SeriesKind.Level, points)]));
        var series = cut.Find("g.ex-chart-series");
        Assert.Equal(3, series.QuerySelectorAll("*").Length);
    }

    [Fact] // ADR-0252, ADR-0270, CH-1: a move to another position renders the tooltip and the guide, and nothing else
    public async Task A_pointer_move_renders_only_the_tooltip_and_the_guide()
    {
        var cut = Render(Exposure());
        var before = Counts(cut);
        var chart = cut.Instance;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 1, 0, 50));
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 2, 0, 50));
        var after = Counts(cut);
        Assert.Equal(before["axes"], after["axes"]);
        Assert.Equal(before["series"], after["series"]);
        Assert.Equal(before["legend"], after["legend"]);
        Assert.True(after["guide"] > before["guide"]);
        Assert.True(after["tooltip"] > before["tooltip"]);
    }

    [Fact] // ADR-0270, CH-4: the tooltip lists every visible series of the pointer's panel, and the guide crosses every panel
    public async Task The_tooltip_lists_the_panels_series_and_the_guide_crosses_every_panel()
    {
        var cut = Render(Exposure());
        var chart = cut.Instance;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 1, 0, 50));
        Assert.Equal("1", cut.Find(".ex-chart-tooltip-position").TextContent);
        Assert.Equal(["epe-before", "epe-after"], cut.FindAll(".ex-chart-tooltip-line").Select(l => l.GetAttribute("data-series")).Order(StringComparer.Ordinal).Reverse());
        var guide = cut.Find(".ex-chart-guide-line");
        var plots = cut.FindAll("rect.ex-chart-plot");
        Assert.Equal(plots[0].GetAttribute("y"), guide.GetAttribute("y1"));
        var lastBottom = double.Parse(plots[^1].GetAttribute("y")!, System.Globalization.CultureInfo.InvariantCulture)
            + double.Parse(plots[^1].GetAttribute("height")!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(lastBottom, double.Parse(guide.GetAttribute("y2")!, System.Globalization.CultureInfo.InvariantCulture), 2);

        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, -1, -1, 0));
        Assert.Empty(cut.FindAll(".ex-chart-tooltip"));
        Assert.Empty(cut.FindAll(".ex-chart-guide-line"));
    }

    [Fact] // ADR-0270, CH-4: past what fits, the nearest N and "and M more series"
    public async Task Past_what_fits_the_tooltip_says_how_many_more()
    {
        var series = Enumerable.Range(0, 40).Select(i => ChartSeries.Of($"s{i}", $"S{i}", SeriesKind.Level, [P(0, i), P(1, i)])).ToList();
        var cut = Render(new ChartDefinition(ChartAxis.Numbers(), series), ps => ps.Add(c => c.Height, 240).Add(c => c.Legend, new LegendPlacement(LegendPosition.None)));
        var chart = cut.Instance;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 0, 0, 30));
        var more = cut.Find(".ex-chart-tooltip-more").TextContent;
        Assert.Matches(@"^and \d+ more series$", more);
        Assert.Equal(40, cut.FindAll(".ex-chart-tooltip-line").Count + int.Parse(more.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact] // ADR-0255: a report taken against an earlier drawing is dropped
    public async Task A_report_against_an_earlier_drawing_is_dropped()
    {
        var cut = Render(Exposure());
        var stale = cut.Instance.Version;
        cut.Render(ps => ps.Add(c => c.Definition, Exposure(epeLast: 7)));
        await cut.InvokeAsync(() => cut.Instance.OnPointer(stale, 1, 0, 50));
        Assert.Empty(cut.FindAll(".ex-chart-tooltip"));
    }

    [Fact] // ADR-0271, ADR-0252, CH-2: hiding one series renders no other series, and raises the key
    public async Task Hiding_one_series_renders_no_other_series()
    {
        SeriesVisibility? told = null;
        // Before stays inside After's range, so hiding it rescales no axis; a series on an axis that
        // is rescaled moves, and renders, as the axis does.
        var cut = Render(Exposure(epeLast: 7), ps => ps.Add(c => c.OnSeriesVisibilityChanged, (SeriesVisibility v) => told = v));
        var others = cut.FindComponents<ChartSeriesView>().Where(c => c.Instance.Piece.Id != "epe-before").ToDictionary(c => c.Instance.Piece.Id, c => c.RenderCount);
        await cut.Find("g.ex-chart-legend-entry[data-series='epe-before']").ClickAsync(new MouseEventArgs());
        Assert.Equal(new SeriesVisibility("epe-before", true), told);
        Assert.Empty(cut.FindAll("g.ex-chart-series[data-series='epe-before']"));
        Assert.Contains("ex-chart-hidden", cut.Find("g.ex-chart-legend-entry[data-series='epe-before']").ClassName);
        foreach (var c in cut.FindComponents<ChartSeriesView>())
            Assert.Equal(others[c.Instance.Piece.Id], c.RenderCount);

        await cut.Find("g.ex-chart-legend-entry[data-series='epe-before']").ClickAsync(new MouseEventArgs());
        Assert.Equal(new SeriesVisibility("epe-before", false), told);
        Assert.Single(cut.FindAll("g.ex-chart-series[data-series='epe-before']"));
    }

    [Fact] // ADR-0257: quotes and their Interpolated curve, sharing a colour key, are one legend entry that hides both
    public async Task Quotes_and_their_interpolated_curve_are_one_legend_entry()
    {
        SeriesVisibility? told = null;
        var definition = new ChartDefinition(ChartAxis.Numbers("Years"),
        [
            ChartSeries.Of("zero-quoted", "Zero rate", SeriesKind.Level, [P(1, 3.0), P(5, 3.2), P(10, 3.4)]) with { Keys = new("Zero", null, "circle") },
            ChartSeries.Of("zero-curve", "Zero rate", SeriesKind.Level, [P(1, 3.0), P(3, 3.1), P(5, 3.2), P(10, 3.4)]) with { Keys = new("Zero"), Interpolated = true },
            ChartSeries.Of("hazard", "Hazard", SeriesKind.Level, [P(1, 1), P(10, 2)]),
        ]);
        var cut = Render(definition, ps => ps.Add(c => c.OnSeriesVisibilityChanged, (SeriesVisibility v) => told = v));
        Assert.Equal(["Zero", "hazard"], cut.FindAll("g.ex-chart-legend-entry").Select(e => e.GetAttribute("data-series")));

        await cut.Find("g.ex-chart-legend-entry[data-series='Zero']").ClickAsync(new MouseEventArgs());
        Assert.Equal(new SeriesVisibility("Zero", true), told);
        Assert.Empty(cut.FindAll("g.ex-chart-series[data-series='zero-quoted']"));
        Assert.Empty(cut.FindAll("g.ex-chart-series[data-series='zero-curve']"));
        Assert.Single(cut.FindAll("g.ex-chart-series[data-series='hazard']"));
        Assert.Contains("ex-chart-hidden", cut.Find("g.ex-chart-legend-entry[data-series='Zero']").ClassName);
        Assert.DoesNotContain(cut.FindAll(".ex-chart-notes li"), n => n.TextContent.Contains("no series has that key"));

        await cut.Find("g.ex-chart-legend-entry[data-series='Zero']").ClickAsync(new MouseEventArgs());
        Assert.Equal(new SeriesVisibility("Zero", false), told);
        Assert.Single(cut.FindAll("g.ex-chart-series[data-series='zero-quoted']"));
        Assert.Single(cut.FindAll("g.ex-chart-series[data-series='zero-curve']"));
    }

    [Fact] // ADR-0272: the hidden keys are taken back as a parameter, and kept across a data replacement
    public void Hidden_keys_are_taken_back_and_kept_across_new_data()
    {
        var hidden = new HashSet<string> { "epe-after" };
        var cut = Render(Exposure(), ps => ps.Add(c => c.HiddenSeries, hidden));
        Assert.Empty(cut.FindAll("g.ex-chart-series[data-series='epe-after']"));
        cut.Render(ps => ps.Add(c => c.Definition, Exposure(epeLast: 7)));
        Assert.Empty(cut.FindAll("g.ex-chart-series[data-series='epe-after']"));
    }

    [Fact] // ADR-0272, CH-5: a hidden key that names no series is written on the chart
    public void A_stale_hidden_key_is_written_on_the_chart()
    {
        var cut = Render(Exposure(), ps => ps.Add(c => c.HiddenSeries, new HashSet<string> { "pfe-99" }));
        Assert.Contains(cut.FindAll(".ex-chart-notes li"), li => li.TextContent == "\"pfe-99\" is hidden, but no series has that key.");
    }

    [Fact] // ADR-0273, ADR-0272: a drag narrows to the positions it began and ended on, raised in the axis' values
    public async Task A_drag_narrows_the_range()
    {
        ChartRange? told = null;
        var raised = false;
        var cut = Render(Exposure(), ps => ps.Add(c => c.OnRangeChanged, (ChartRange? r) => { told = r; raised = true; }));
        var chart = cut.Instance;
        var v = chart.Version;
        chart.OnPress(v, 0);
        await cut.InvokeAsync(() => chart.OnPointer(v, 1, 0, 50));
        Assert.Single(cut.FindAll("rect.ex-chart-band"));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(v, 1));
        Assert.True(raised);
        Assert.Equal(new ChartRange(0, 1), told);
        Assert.Empty(cut.FindAll("rect.ex-chart-band"));
        Assert.False(cut.Find(".ex-chart-reset").HasAttribute("disabled"));

        await cut.Find(".ex-chart-hit").DoubleClickAsync(new MouseEventArgs());
        Assert.Null(told);
    }

    [Fact] // ADR-0272, CH-5: a drag begun before the data was replaced is refused when it ends
    public async Task A_drag_begun_before_new_data_is_refused()
    {
        var raised = false;
        var cut = Render(Exposure(), ps => ps.Add(c => c.OnRangeChanged, (ChartRange? _) => raised = true));
        var v = cut.Instance.Version;
        cut.Instance.OnPress(v, 0);
        cut.Render(ps => ps.Add(c => c.Definition, Exposure(epeLast: 7)));
        await cut.InvokeAsync(() => cut.Instance.OnReleaseAsync(v, 2));
        Assert.False(raised);
        Assert.True(cut.Find(".ex-chart-reset").HasAttribute("disabled"));
    }

    [Fact] // ADR-0272, CH-5: a range in which no series has a point is written on the chart
    public void An_empty_range_is_written_on_the_chart()
    {
        var cut = Render(Exposure(), ps => ps.Add(c => c.Range, new ChartRange(0.2, 0.8)));
        Assert.Contains(cut.FindAll(".ex-chart-notes li"), li => li.TextContent.StartsWith("No series has a point between", StringComparison.Ordinal));
    }

    [Fact] // ADR-0256, CH-6: past the last colour slot, colours repeat and the chart says so
    public void Repeated_colours_are_noted()
    {
        var series = Enumerable.Range(0, 11).Select(i => ChartSeries.Of($"c{i}", $"C{i}", SeriesKind.Level, [P(0, i), P(1, i)])).ToList();
        var cut = Render(new ChartDefinition(ChartAxis.Numbers(), series));
        Assert.Contains(cut.FindAll(".ex-chart-notes li"), li => li.TextContent.StartsWith("Colours repeat", StringComparison.Ordinal));
        Assert.Contains("ex-chart-colour-0", cut.Find("g.ex-chart-series[data-series='c10']").ClassName);
    }

    [Fact] // ADR-0275, CH-8: the accessible summary names the axis and each visible series' maximum; View as table holds the CSV's content
    public async Task The_summary_and_the_table_say_what_the_chart_shows()
    {
        var cut = Render(Exposure(), ps => ps.Add(c => c.Title, "Exposure profile"));
        var svg = cut.Find("svg.ex-chart-svg");
        Assert.Equal("img", svg.GetAttribute("role"));
        var title = cut.Find("svg.ex-chart-svg > title");
        Assert.Equal(title.Id, svg.GetAttribute("aria-labelledby"));
        Assert.StartsWith("Exposure profile. Years, 0 to 2.", title.TextContent, StringComparison.Ordinal);
        Assert.Contains("EPE · After: maximum 8 at 2.", title.TextContent, StringComparison.Ordinal);

        await cut.Find(".ex-chart-table-toggle").ClickAsync(new MouseEventArgs());
        var expected = ChartCsv.WideTable(Exposure(), new ChartMetrics(800, 400));
        Assert.Equal(expected.Header, cut.FindAll(".ex-chart-table thead th").Select(th => th.TextContent));
        Assert.Equal(expected.Rows.Count, cut.FindAll(".ex-chart-table tbody tr").Count);
        Assert.Equal(expected.Rows[1], cut.FindAll(".ex-chart-table tbody tr")[1].Children.Select(c => c.TextContent));
    }

    [Fact] // ADR-0273, ADR-0272: a range typed under Chart Filters narrows the chart; a value that does not read is said
    public async Task A_typed_range_narrows_and_a_bad_value_is_said()
    {
        ChartRange? told = null;
        var cut = Render(Exposure(), ps => ps.Add(c => c.OnRangeChanged, (ChartRange? r) => told = r));
        await cut.Find(".ex-chart-filters-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-chart-from").ChangeAsync(new ChangeEventArgs { Value = "one" });
        await cut.Find(".ex-chart-apply").ClickAsync(new MouseEventArgs());
        Assert.Equal("“one” is not a number on this axis.", cut.Find(".ex-chart-filter-error").TextContent);
        Assert.Null(told);

        await cut.Find(".ex-chart-from").ChangeAsync(new ChangeEventArgs { Value = "1" });
        await cut.Find(".ex-chart-apply").ClickAsync(new MouseEventArgs());
        Assert.Equal(new ChartRange(1, null), told);
        Assert.Empty(cut.FindAll(".ex-chart-filter-error"));
    }

    [Fact] // ADR-0251, ADR-0272: the axis switched under Chart Filters is raised and drawn
    public async Task The_axis_switch_is_raised()
    {
        AxisScale? told = null;
        var cut = Render(Exposure(), ps => ps.Add(c => c.OnScaleChanged, (AxisScale s) => told = s));
        await cut.Find(".ex-chart-filters-toggle").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-chart-scale").ChangeAsync(new ChangeEventArgs { Value = "Categories" });
        Assert.Equal(AxisScale.Categories, told);
        Assert.Equal("Categories", cut.Find(".ex-chart-scale").GetAttribute("value"));
    }

    [Fact] // ADR-0123: a script of another version is refused, and the chart says so and still draws
    public void A_script_of_another_version_is_refused()
    {
        _module.Setup<string>("stamp").SetResult("another");
        var cut = Render(Exposure());
        Assert.Single(cut.FindAll(".ex-chart-script-notice"));
        Assert.Equal(3, cut.FindAll("g.ex-chart-series").Count);
        Assert.DoesNotContain(_module.Invocations, i => i.Identifier == "attach");
        Assert.False(cut.Find(".ex-chart").HasAttribute("aria-busy"));
    }

    [Fact] // ADR-0255, A11Y-20: the script is attached once and told each drawing's positions with its version; the chart is busy only until the first is told
    public void The_script_is_told_each_drawing()
    {
        var cut = Render(Exposure());
        Assert.Single(_module.Invocations, i => i.Identifier == "attach");
        var draw = Assert.Single(_handle.Invocations, i => i.Identifier == "draw");
        Assert.Equal(cut.Instance.Version, draw.Arguments[0]);
        Assert.Equal(3, ((double[])draw.Arguments[1]!).Length);
        Assert.Empty(cut.FindAll(".ex-chart-script-notice"));
        Assert.False(cut.Find(".ex-chart").HasAttribute("aria-busy"));
    }
}
