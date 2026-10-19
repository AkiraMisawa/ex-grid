using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class SceneTests
{
    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);
    private static readonly ChartMetrics Metrics = new(800, 400);

    private static ChartDefinition Exposure(double epeLast = 9) => new(ChartAxis.Numbers("Years"),
    [
        ChartSeries.Of("epe-before", "EPE · Before", SeriesKind.Level, [P(0, 1), P(1, 5), P(2, epeLast)]) with { Panel = "EPE", Keys = new("Before") },
        ChartSeries.Of("epe-after", "EPE · After", SeriesKind.Level, [P(0, 2), P(1, 4), P(2, 8)]) with { Panel = "EPE", Keys = new("After") },
        ChartSeries.Of("ene-after", "ENE · After", SeriesKind.Level, [P(0, -1), P(1, -3), P(2, -2)]) with { Panel = "ENE", Keys = new("After") },
    ]);

    [Fact] // ADR-0252: a change to one series changes only that series' piece
    public void A_change_to_one_series_changes_only_its_piece()
    {
        var a = ChartScene.Build(Exposure(), Metrics);
        var b = ChartScene.Build(Exposure(), Metrics);
        Assert.Equal(a, b);
        var c = ChartScene.Build(Exposure(epeLast: 8.5), Metrics);
        Assert.NotEqual(a.Panels[0].Series[0], c.Panels[0].Series[0]);
        Assert.Equal(a.Panels[0].Series[1], c.Panels[0].Series[1]);
        Assert.Equal(a.Panels[1], c.Panels[1]);
    }

    [Fact] // ADR-0256: hiding a series keeps every other series' keys
    public void Hiding_a_series_keeps_the_others_keys()
    {
        var all = ChartScene.Build(Exposure(), Metrics);
        var hidden = ChartScene.Build(Exposure(), Metrics, new ChartView(new HashSet<string> { "epe-before" }));
        Assert.Single(hidden.Panels[0].Series);
        Assert.Equal(all.Panels[0].Series[1].Keys, hidden.Panels[0].Series[0].Keys);
        Assert.Equal(all.Panels[1].Series[0].Keys, hidden.Panels[1].Series[0].Keys);
    }

    [Fact] // ADR-0251: panels stack in the definition's order, and a Level axis reaches zero
    public void Panels_stack_and_a_level_axis_reaches_zero()
    {
        var s = ChartScene.Build(Exposure(), Metrics);
        Assert.Equal(["EPE", "ENE"], s.Panels.Select(p => p.Name));
        Assert.True(s.Panels[0].Plot.Y < s.Panels[1].Plot.Y);
        Assert.Equal(0, s.Panels[0].Left!.Min);
        Assert.False(s.Panels[0].Left!.Broken);
    }

    [Fact] // ADR-0253 (Q8): a series read away from zero leaves it, and the axis shows a break mark
    public void An_axis_away_from_zero_is_broken()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("z", "Zero", SeriesKind.Level, [P(1, 3.0), P(2, 3.6)]) with { FromZero = false }]);
        var axis = ChartScene.Build(d, Metrics).Panels[0].Left!;
        Assert.True(axis.Min > 0);
        Assert.True(axis.Broken);
    }

    [Fact] // ADR-0251 (Q7): left and right axes put zero at the same height
    public void Left_and_right_zeros_are_at_the_same_height()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("s", "Spread", SeriesKind.Level, [P(0, 10), P(1, 200)]),
            ChartSeries.Of("h", "Diff", SeriesKind.Level, [P(0, -2), P(1, 3)]) with { Axis = AxisSide.Right },
        ]);
        var p = ChartScene.Build(d, Metrics).Panels[0];
        var leftZero = p.Left!.Ticks.Single(t => t.Value == 0).Y;
        Assert.Equal(p.ZeroY!.Value, leftZero, 6);
        var r = p.Right!;
        var rightZero = p.Plot.Y + p.Plot.Height - (0 - r.Min) / (r.Max - r.Min) * p.Plot.Height;
        Assert.Equal(leftZero, rightZero, 6);
        Assert.Empty(ChartScene.Build(d, Metrics).Notes);
    }

    [Fact] // ADR-0251 (Q7): declared independent, the scales differ and the chart says so
    public void Independent_scales_are_said_under_the_chart()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("s", "Spread", SeriesKind.Level, [P(0, 10), P(1, 200)]),
            ChartSeries.Of("h", "Diff", SeriesKind.Level, [P(0, -2), P(1, 3)]) with { Axis = AxisSide.Right },
        ]);
        var s = ChartScene.Build(d, Metrics, independentScales: new HashSet<string> { "" });
        Assert.Contains(s.Notes, n => n.Contains("zero at different heights"));
    }

    [Fact] // ADR-0257: the Consumer's Notes come first, then each Segment's method
    public void Notes_carry_the_consumers_lines_and_the_segments()
    {
        var curve = ChartSeries.Sampled("c", "USD SOFR", SeriesKind.Level, 0, 40, t => 3 + t / 100) with
        {
            Interpolated = true,
            Segments = [new Segment(0, 30, "Log-linear on discount factors"), new Segment(30, 40, "Flat", Extrapolated: true)],
        };
        var s = ChartScene.Build(new ChartDefinition(ChartAxis.Numbers(), [curve], ["Zero rates, continuously compounded"]), Metrics);
        Assert.Equal("Zero rates, continuously compounded", s.Notes[0]);
        Assert.Contains("USD SOFR: 30Y–40Y Flat (extrapolated)", s.Notes);
        Assert.Single(s.Panels[0].Series[0].Extrapolated);
    }

    [Fact] // ADR-0254: daily data over decades is thinned to the plot's device pixels
    public void Daily_data_is_thinned_to_the_device_pixels()
    {
        var start = new DateOnly(2026, 1, 1).DayNumber;
        var daily = ChartSeries.Of("d", "EPE", SeriesKind.Level, Enumerable.Range(0, 10959).Select(i => P(start + i, Math.Sin(i / 50.0))));
        var s = ChartScene.Build(new ChartDefinition(ChartAxis.Dates(), [daily]), Metrics with { DevicePixelRatio = 2 });
        var piece = s.Panels[0].Series[0];
        Assert.InRange(piece.Lines.Sum(l => l.Count), 1, 4 * 2 * 800);
        Assert.NotEmpty(piece.Thinned);
        Assert.Empty(piece.Markers);
    }

    [Fact] // ADR-0251: on categories, positions are evenly spaced whatever their values
    public void Categories_are_evenly_spaced()
    {
        var axis = ChartAxis.Numbers() with { Scale = AxisScale.Categories, Named = [(0.25, "3M"), (1, "1Y"), (10, "10Y")] };
        var s = ChartScene.Build(new ChartDefinition(axis, [ChartSeries.Of("v", "ATM", SeriesKind.Level, [P(0.25, 9), P(1, 10), P(10, 12)]) with { FromZero = false }]), Metrics);
        var xs = s.Panels[0].Series[0].Lines[0].Select(p => p.X).ToList();
        Assert.Equal(xs[1] - xs[0], xs[2] - xs[1], 6);
    }
}
