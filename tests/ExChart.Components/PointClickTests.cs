using Bunit;
using ExChart.Engine;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

/// <summary>A click on a point (ADR-0258, M3-Q6): a press and a release at one position.</summary>
public class PointClickTests : BunitContext
{
    public PointClickTests()
    {
        var module = JSInterop.SetupModule($"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}");
        module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        var handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("draw", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private static ChartPoint P(double x, double y) => new(x, ChartValue.Of(y));

    private static readonly ChartDefinition Two = new(ChartAxis.Numbers("Years"),
    [
        ChartSeries.Of("high", "High", SeriesKind.Level, [P(0, 10), P(1, 10), P(2, 10)]),
        ChartSeries.Of("low", "Low", SeriesKind.Level, [P(0, 0), P(1, 0), P(2, 0)]),
    ]);

    [Fact] // ADR-0258: a click raises the series nearest the pointer at the position clicked; a drag raises none
    public async Task ADR0258_a_click_names_the_nearest_series_and_its_position()
    {
        var clicks = new List<ChartPointClick>();
        var cut = Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, Two)
            .Add(c => c.OnPointClick, (ChartPointClick c) => clicks.Add(c)));
        var chart = cut.Instance;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 1, 0, 1000));
        await cut.InvokeAsync(() => chart.OnPress(chart.Version, 1));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, 1));
        Assert.Equal([new ChartPointClick("low", 1)], clicks);

        await cut.InvokeAsync(() => chart.OnPress(chart.Version, 0));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, 0));
        Assert.Single(clicks);
    }
}
