using Bunit;
using ExChart.Engine;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

/// <summary>Candles, volume and intervals as rendered (ADR-0300, ADR-0302, ADR-0303).</summary>
public class CandleTests : BunitContext
{
    public CandleTests()
    {
        var module = JSInterop.SetupModule($"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}");
        module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        var handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("draw", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private static List<PriceBar> Days(int count)
    {
        var bars = new List<PriceBar>();
        var x = (double)new DateOnly(2026, 1, 5).DayNumber;
        var price = 100m;
        for (var i = 0; i < count; i++)
        {
            while (DateOnly.FromDayNumber((int)x).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) x++;
            var close = price + (i % 2 == 0 ? 3 : -2);
            bars.Add(new PriceBar(x, price, Math.Max(price, close) + 1, Math.Min(price, close) - 1, close, 1000m + i));
            price = close;
            x++;
        }
        return bars;
    }

    [Fact] // ADR-0300: rising candles hollow, falling filled, and volume in a panel of its own
    public void ADR0300_rising_and_falling_candles_and_volume_are_drawn()
    {
        var cut = Render<ChartComponent>(ps => ps.Add(c => c.Definition, new ChartDefinition(ChartAxis.Dates(), [ChartSeries.Candles("idx", "Index", Days(10))])));
        Assert.Equal(2, cut.FindAll(".ex-chart-rising").Count); // candles and volume bars
        Assert.Equal(2, cut.FindAll(".ex-chart-falling").Count);
        Assert.Equal(2, cut.Instance.Scene.Panels.Count);
    }

    [Fact] // ADR-0303: an interval is drawn as whiskers, or as a band
    public void ADR0303_an_interval_is_drawn_as_whiskers_or_a_band()
    {
        ChartDefinition Curve(IntervalStyle style) => new(ChartAxis.Numbers(),
        [
            ChartSeries.Of("c", "Curve", SeriesKind.Level, [new ChartPoint(0, 3m), new ChartPoint(1, 3.1m)]) with
            {
                Interval = new SeriesInterval("Bid–Ask", [new IntervalBound(0, 2.9m, 3.1m), new IntervalBound(1, 3.0m, 3.2m)], style),
            },
        ]);
        Assert.Single(Render<ChartComponent>(ps => ps.Add(c => c.Definition, Curve(IntervalStyle.Whiskers))).FindAll(".ex-chart-whiskers"));
        Assert.Single(Render<ChartComponent>(ps => ps.Add(c => c.Definition, Curve(IntervalStyle.Band))).FindAll(".ex-chart-interval-band"));
    }

    [Fact] // ADR-0302: a click on a merged candle narrows to its period; on a row's candle it names the row
    public async Task ADR0302_a_click_on_a_merged_candle_narrows_to_its_period()
    {
        var ranges = new List<ChartRange?>();
        var clicks = new List<ChartPointClick>();
        var cut = Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, new ChartDefinition(ChartAxis.Dates(), [ChartSeries.Candles("idx", "Index", Days(400))]))
            .Add(c => c.Width, 300)
            .Add(c => c.OnRangeChanged, (ChartRange? r) => ranges.Add(r))
            .Add(c => c.OnPointClick, (ChartPointClick c) => clicks.Add(c)));
        var chart = cut.Instance;
        Assert.NotEqual(CandlePeriod.Row, chart.Scene.Panels[0].Series[0].Period);
        var clicked = chart.Scene.Positions[10].Position;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 10, 0, 50));
        await cut.InvokeAsync(() => chart.OnPress(chart.Version, 10));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, 10));
        Assert.Empty(clicks);
        var range = Assert.Single(ranges)!;
        Assert.True(range.From <= clicked && clicked <= range.To);

        // Narrowed to one period, the candles are rows again, and a click names its row.
        Assert.Equal(CandlePeriod.Row, chart.Scene.Panels[0].Series[0].Period);
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 1, 0, 50));
        await cut.InvokeAsync(() => chart.OnPress(chart.Version, 1));
        await cut.InvokeAsync(() => chart.OnReleaseAsync(chart.Version, 1));
        Assert.Single(clicks);
    }
}
