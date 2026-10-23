using Bunit;
using ExChart.Engine;
using ExChart.Engine.Fitting;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

/// <summary>A scatter on the component (ADR-0290): the nearest mark, a click to its row, and the rectangle.</summary>
public class ScatterTests : BunitContext
{
    private readonly List<object?[]> _draws = [];

    public ScatterTests()
    {
        var module = JSInterop.SetupModule($"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}");
        module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        var handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("draw", i => { _draws.Add([.. i.Arguments]); return true; }).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private static ChartPoint O(double x, double y, string name, object row) => new(x, ChartValue.Of(y)) { Name = name, Row = row };

    private static readonly ChartPoint[] Names = [O(1, 100, "ABC", 1), O(2, 180, "DEF", 2), O(3, 260, "XYZ Corp", 3), O(4, 330, "GHI", 4)];

    private static readonly ChartDefinition Scatter = new(ChartAxis.Numbers("Leverage"),
    [
        ChartSeries.Of("names", "Names", SeriesKind.Observation, Names),
        Fit.LeastSquares(Names.Select(p => p.X).ToArray(), Names.Select(p => p.Y!.Value.ToDouble()).ToArray()).Series("ols", "OLS"),
    ]);

    [Fact] // ADR-0290: the pointer entry is handed the marks, and a report of one reads that point alone
    public async Task ADR0290_the_tooltip_reads_the_mark_the_pointer_is_nearest()
    {
        var cut = Render<ChartComponent>(ps => ps.Add(c => c.Definition, Scatter));
        var chart = cut.Instance;
        var marks = (double[])_draws[^1][2]!;
        Assert.Equal(Names.Length * 3, marks.Length);
        var xyz = chart.Scene.Marks.ToList().FindIndex(m => m.Point == 2);
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, xyz, 0, 0));
        Assert.Contains("XYZ Corp", cut.Find(".ex-chart-tooltip").TextContent);
        Assert.Contains("3, 260", cut.Find(".ex-chart-tooltip").TextContent);
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, -1, 0, 0));
        Assert.Empty(cut.FindAll(".ex-chart-tooltip"));
    }

    [Fact] // ADR-0321: the pointer entry is handed the edges after the marks, and one over an edge reads its two keys
    public async Task ADR0321_the_tooltip_over_an_edge_says_its_two_keys()
    {
        var nodes = new[] { O(0, 100, "Root", "a"), O(1, 90, "Down", "b"), O(1, 110, "Up", "c") };
        var lattice = new ChartDefinition(ChartAxis.Numbers("Step"),
            [ChartSeries.Of("tree", "Tree", SeriesKind.Observation, nodes) with { Edges = [new ChartEdge("a", "b"), new ChartEdge("a", "c")] }]);
        var clicks = new List<ChartPointClick>();
        var cut = Render<ChartComponent>(ps => ps.Add(c => c.Definition, lattice).Add(c => c.OnPointClick, (ChartPointClick c) => clicks.Add(c)));
        var chart = cut.Instance;
        var edges = (double[])_draws[^1][5]!;
        Assert.Equal(2 * 5, edges.Length);
        var index = chart.Scene.Marks.Count + 1;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, index, 0, 0));
        Assert.Contains("a → c", cut.Find(".ex-chart-tooltip").TextContent);
        Assert.Contains("edge from a to c", cut.Find(".ex-chart-tooltip").TextContent);

        // An edge has no row of its own: a click on it raises nothing.
        var e = chart.Scene.EdgeMarks[1].Edge;
        await cut.InvokeAsync(() => chart.OnPress(chart.Version, index, e.X1, e.Y1, 0));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, index, e.X1, e.Y1));
        Assert.Empty(clicks);
    }

    [Fact] // ADR-0290: a press and a release on one mark raise its point's Row Key
    public async Task ADR0290_a_click_on_a_mark_raises_its_row()
    {
        var clicks = new List<ChartPointClick>();
        var cut = Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, Scatter)
            .Add(c => c.OnPointClick, (ChartPointClick c) => clicks.Add(c)));
        var chart = cut.Instance;
        var i = chart.Scene.Marks.ToList().FindIndex(m => m.Point == 1);
        var m = chart.Scene.Marks[i];
        await cut.InvokeAsync(() => chart.OnPress(chart.Version, i, m.X, m.Y, 0));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, i, m.X, m.Y));
        Assert.Equal([new ChartPointClick("names", 2, 2)], clicks);
    }

    [Fact] // ADR-0290: a rectangle narrows both axes, raised as a range the Consumer hands back
    public async Task ADR0290_a_rectangle_narrows_both_axes()
    {
        var ranges = new List<ChartRange?>();
        var cut = Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, Scatter)
            .Add(c => c.OnRangeChanged, (ChartRange? r) => ranges.Add(r)));
        var chart = cut.Instance;
        var before = chart.Scene;
        var plot = before.Panels[0].Plot;
        var left = before.Panels[0].Left!;
        double x0 = plot.X + plot.Width * 0.2, x1 = plot.X + plot.Width * 0.6, yTop = plot.Y + plot.Height * 0.3, yFoot = plot.Y + plot.Height * 0.8;
        await cut.InvokeAsync(() => chart.OnPress(chart.Version, -1, x0, yFoot, 0));
        await cut.InvokeAsync(() => chart.OnDrag(chart.Version, x1, yTop));
        Assert.Single(cut.FindAll(".ex-chart-band"));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, -1, x1, yTop));
        var range = Assert.Single(ranges)!;
        Assert.Equal(before.PositionAt(x0), range.From!.Value, 9);
        Assert.Equal(left.ValueAt(yFoot, plot), range.YFrom!.Value, 9);
        Assert.Equal(left.ValueAt(yTop, plot), range.YTo!.Value, 9);
        Assert.Equal(range.YFrom.Value, chart.Scene.Panels[0].Left!.Min, 9);
    }

    [Fact] // ADR-0290, spine rule 6: a release against another drawing raises nothing
    public async Task ADR0290_a_release_against_another_drawing_is_refused()
    {
        var ranges = new List<ChartRange?>();
        var cut = Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, Scatter)
            .Add(c => c.OnRangeChanged, (ChartRange? r) => ranges.Add(r)));
        var chart = cut.Instance;
        var v = chart.Version;
        await cut.InvokeAsync(() => chart.OnPress(v, -1, 100, 100, 0));
        cut.Render(ps => ps.Add(c => c.Width, 700));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, -1, 200, 200));
        Assert.Empty(ranges);
    }
}
