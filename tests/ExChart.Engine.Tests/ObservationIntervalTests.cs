using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class ObservationIntervalTests
{
    private static readonly ChartMetrics Metrics = new(800, 400);

    private static ChartPoint O(double x, double y, string name) => new(x, ChartValue.Of(y)) { Name = name };

    // Two names share a leverage of 3, each with its own spread and interval.
    private static readonly ChartPoint[] Points = [O(1, 150, "A"), O(3, 200, "C"), O(3, 260, "D")];

    private static SeriesInterval Spread(string meaning = "95% CI") =>
        new(meaning, [new IntervalBound(1, 140m, 160m), new IntervalBound(3, 190m, 215m), new IntervalBound(3, 240m, 290m)]);

    private static SeriesInterval Leverage(string meaning = "±1σ") =>
        new(meaning, [new IntervalBound(1, 0.5m, 1.5m), new IntervalBound(3, 2.8m, 3.4m), new IntervalBound(3, 2.5m, 3.1m)]);

    private static ChartDefinition Chart(SeriesInterval? y, SeriesInterval? x = null, ChartPoint[]? points = null) =>
        new(ChartAxis.Numbers("Leverage"), [ChartSeries.Of("names", "Names", SeriesKind.Observation, points ?? Points) with { Interval = y, XInterval = x }]);

    [Fact] // ADR-0304: an Observation's bounds are one a row, so two points at one position keep their own
    public void Bounds_follow_rows_not_positions()
    {
        var markers = ChartScene.Build(Chart(Spread()), Metrics).Panels[0].Series[0].Markers;
        Assert.Equal(3, markers.Count);
        var c = markers.Single(m => m.Point == 1);
        var d = markers.Single(m => m.Point == 2);
        Assert.NotNull(c.YBounds);
        Assert.NotEqual(c.YBounds, d.YBounds);
        Assert.True(d.YBounds!.Value.Lower < d.Y && d.Y < d.YBounds.Value.Upper);
    }

    [Fact] // ADR-0304: a count or a position that does not match its points is refused by name
    public void Bounds_that_do_not_match_the_rows_are_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => Chart(new SeriesInterval("95% CI", [new IntervalBound(1, 140m, 160m)])));
        Assert.Contains("3 points and 1 bounds", e.Message);
        var shifted = new SeriesInterval("95% CI", [new IntervalBound(3, 140m, 160m), new IntervalBound(3, 190m, 215m), new IntervalBound(3, 240m, 290m)]);
        e = Assert.Throws<ChartRefusedException>(() => Chart(shifted));
        Assert.Contains("at A", e.Message);
    }

    [Fact] // ADR-0304: an Observation's interval takes whiskers; a band has no neighbours to join
    public void A_band_on_an_observation_is_refused()
    {
        Assert.Throws<ChartRefusedException>(() => Chart(Spread() with { Style = IntervalStyle.Band }));
    }

    [Fact] // ADR-0303, ADR-0304: a horizontal interval belongs to an Observation only
    public void A_horizontal_interval_on_a_level_is_refused()
    {
        var level = ChartSeries.Of("l", "Level", SeriesKind.Level, [new ChartPoint(1, 1m)]) with { XInterval = new SeriesInterval("±1σ", [new IntervalBound(1, 0.5m, 1.5m)]) };
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(), [level]));
    }

    [Fact] // ADR-0304: both axes take the bounds, so no whisker is cut at the plot's edge
    public void The_axes_take_the_bounds()
    {
        var scene = ChartScene.Build(Chart(Spread(), Leverage()), Metrics);
        Assert.True(scene.Panels[0].Left!.Max >= 290);
        Assert.True(scene.XFrom <= 0.5);
    }

    [Fact] // ADR-0304: a merged mark carries the union of its points' intervals
    public void A_merged_mark_carries_the_union()
    {
        ChartPoint[] same = [O(3, 200, "C"), O(3, 200, "D")];
        var y = new SeriesInterval("95% CI", [new IntervalBound(3, 190m, 205m), new IntervalBound(3, 195m, 230m)]);
        var scene = ChartScene.Build(Chart(y, points: same), Metrics);
        var mark = Assert.Single(scene.Panels[0].Series[0].Markers);
        Assert.Equal(2, mark.Count);
        var axis = scene.Panels[0].Left!;
        var plot = scene.Panels[0].Plot;
        Assert.Equal(axis.YOf(230, plot), mark.YBounds!.Value.Lower, 6);
        Assert.Equal(axis.YOf(190, plot), mark.YBounds.Value.Upper, 6);
    }

    [Fact] // ADR-0304: the legend, the Notes, the tooltip and the CSV say what each interval means
    public void Each_meaning_is_said()
    {
        var d = Chart(Spread(), Leverage());
        var scene = ChartScene.Build(d, Metrics);
        Assert.Equal("Names (y: 95% CI; x: ±1σ)", scene.Panels[0].Legend.Single().Name);
        Assert.Contains("Names: the vertical interval is 95% CI.", scene.Notes);
        Assert.Contains("Names: the horizontal interval is ±1σ.", scene.Notes);
        var reading = ChartReadings.OfMark(d, scene, scene.Marks.ToList().FindIndex(m => m.Point == 2));
        Assert.Contains("y [240, 290] 95% CI", reading.Lines[0].Note);
        Assert.Contains("x [2.5, 3.1] ±1σ", reading.Lines[0].Note);
        var table = ChartCsv.WideTable(d, Metrics);
        Assert.Equal(["Series", "Point", "Leverage", "Value", "Value lower (95% CI)", "Value upper (95% CI)", "Leverage lower (±1σ)", "Leverage upper (±1σ)"], table.Header);
        Assert.Equal(["Names", "D", "3", "260", "240", "290", "2.5", "3.1"], table.Rows[2]);
        Assert.Contains(ChartCsv.Long(d, Metrics).Split("\r\n"), r => r == "Names y upper (95% CI),D,3,290,interval,");
    }

    [Fact] // ADR-0303, ADR-0304: a bound at or below zero keeps a logarithmic axis proportional, by name
    public void A_bound_at_or_below_zero_keeps_the_axis_proportional()
    {
        var y = new SeriesInterval("95% CI", [new IntervalBound(1, -5m, 160m), new IntervalBound(3, 190m, 215m), new IntervalBound(3, 240m, 290m)]);
        var scene = ChartScene.Build(Chart(y), Metrics, new ChartView(new HashSet<string>(), YScale: AxisScale.Logarithmic));
        Assert.False(scene.Panels[0].Left!.Logarithmic);
        Assert.Contains(scene.Notes, n => n.Contains("'Names' has a bound of -5 in its 95% CI"));
        var x = new SeriesInterval("±1σ", [new IntervalBound(1, 0m, 1.5m), new IntervalBound(3, 2.8m, 3.4m), new IntervalBound(3, 2.5m, 3.1m)]);
        scene = ChartScene.Build(Chart(null, x), Metrics, new ChartView(new HashSet<string>(), Scale: AxisScale.Logarithmic));
        Assert.False(scene.LogarithmicX);
        Assert.Contains(scene.Notes, n => n.Contains("'Names' has a bound of 0 in its ±1σ at 1"));
    }

    [Fact] // ADR-0304: a narrowed range cuts a whisker at the plot's edge, never past it
    public void A_whisker_stops_at_the_plots_edge()
    {
        var scene = ChartScene.Build(Chart(Spread(), Leverage()), Metrics, new ChartView(new HashSet<string>(), From: 2.9, To: 3.3, YFrom: 230, YTo: 280));
        var panel = scene.Panels[0];
        var mark = panel.Series[0].Markers.Single(m => m.Point == 2);
        Assert.True(mark.YBounds!.Value.Lower >= panel.Plot.Y - 1e-6);
        Assert.True(mark.YBounds.Value.Upper <= panel.Plot.Y + panel.Plot.Height + 1e-6);
        Assert.True(mark.XBounds!.Value.Lower >= panel.Plot.X - 1e-6);
    }
}
