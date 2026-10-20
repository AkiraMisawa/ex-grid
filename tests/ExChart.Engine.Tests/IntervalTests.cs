using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class IntervalTests
{
    private static readonly ChartMetrics Wide = new(1200, 400);

    private static ChartSeries Curve(SeriesInterval? interval, int count = 5) =>
        ChartSeries.Of("2y", "2Y", SeriesKind.Level, Enumerable.Range(0, count).Select(i => new ChartPoint(i, 3.0m + i * 0.01m))) with { Interval = interval, FromZero = false };

    private static SeriesInterval Bounds(string meaning, IntervalStyle style = IntervalStyle.Whiskers, int count = 5, int? missing = null) =>
        new(meaning, Enumerable.Range(0, count).Select(i => new IntervalBound(i, i == missing ? null : 2.9m + i * 0.01m, 3.1m + i * 0.01m)).ToList(), style);

    private static ChartDefinition Chart(ChartSeries s) => new(ChartAxis.Numbers(), [s]);

    [Fact] // ADR-0303: an interval says what it means, or it is refused
    public void An_interval_without_a_meaning_is_refused() =>
        Assert.Throws<ChartRefusedException>(() => Chart(Curve(Bounds(" "))));

    [Fact] // ADR-0303: a lower bound above its upper is refused, naming the position
    public void A_lower_bound_above_its_upper_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => Chart(Curve(new SeriesInterval("95% CI", [new IntervalBound(2, 3.2m, 3.1m)]))));
        Assert.Contains("95% CI", e.Message);
    }

    [Fact] // ADR-0303: a value outside its own interval is drawn (a trade outside the bid-ask)
    public void A_value_outside_its_interval_is_drawn() =>
        Chart(Curve(new SeriesInterval("Bid–Ask", [new IntervalBound(1, 3.5m, 3.6m)])));

    [Fact] // ADR-0303: an interval belongs to a Level's points
    public void An_interval_off_the_points_or_off_a_level_is_refused()
    {
        Assert.Throws<ChartRefusedException>(() => Chart(Curve(new SeriesInterval("±1σ", [new IntervalBound(9, 1m, 2m)]))));
        var step = ChartSeries.Of("h", "Hazard", SeriesKind.RateOverInterval, [new ChartPoint(0, 1m), new ChartPoint(1, 1m)]) with
        {
            Interval = new SeriesInterval("±1σ", [new IntervalBound(1, 0.5m, 1.5m)]),
        };
        Assert.Throws<ChartRefusedException>(() => Chart(step));
    }

    [Fact] // ADR-0303: whiskers at each point, and the axis takes the bounds
    public void Whiskers_are_drawn_and_the_axis_holds_the_bounds()
    {
        var scene = ChartScene.Build(Chart(Curve(Bounds("95% CI"))), Wide);
        var piece = scene.Panels[0].Series[0];
        Assert.Equal(5, piece.Whiskers.Count);
        Assert.Equal("95% CI", piece.IntervalMeaning);
        Assert.True(scene.Panels[0].Left!.Min <= 2.9 + 1e-9);
        Assert.All(piece.Whiskers, w => Assert.True(w.Lower > w.Upper)); // pixels: the lower bound is lower on screen
    }

    [Fact] // ADR-0303: a band joins only neighbours, and breaks where a point has no interval
    public void A_band_breaks_at_a_point_without_an_interval()
    {
        var scene = ChartScene.Build(Chart(Curve(Bounds("Bid–Ask", IntervalStyle.Band, missing: 2))), Wide);
        var bands = scene.Panels[0].Series[0].Bands;
        Assert.Equal([2, 2], bands.Select(b => b.Count));
    }

    [Fact] // ADR-0303: past one point a pixel, whiskers become the band, thinned to the extremes per pixel
    public void Past_the_pixels_whiskers_become_a_thinned_band()
    {
        var scene = ChartScene.Build(Chart(Curve(Bounds("±1σ", count: 5000), 5000)), new ChartMetrics(300, 200));
        var piece = scene.Panels[0].Series[0];
        Assert.Empty(piece.Whiskers);
        var band = Assert.Single(piece.Bands);
        Assert.True(band.Count < 5000);
    }

    [Fact] // ADR-0303: the tooltip carries the bounds and the meaning; a missing bound says so
    public void The_tooltip_names_the_interval()
    {
        var d = Chart(Curve(Bounds("95% CI", missing: 3)));
        var scene = ChartScene.Build(d, Wide);
        Assert.Contains("[2.91, 3.11] 95% CI", ChartReadings.At(d, scene, Wide, ChartView.All, 1, 0, 0).Lines[0].Note);
        Assert.Equal("(no interval)", ChartReadings.At(d, scene, Wide, ChartView.All, 3, 0, 0).Lines[0].Note);
    }

    [Fact] // ADR-0303: the CSV names the bounds by their meaning
    public void The_csv_names_the_bounds_by_their_meaning()
    {
        var table = ChartCsv.WideTable(Chart(Curve(Bounds("95% CI"))), Wide);
        Assert.Equal(["X", "2Y", "2Y lower (95% CI)", "2Y upper (95% CI)"], table.Header);
    }

    [Fact] // ADR-0303: the long CSV marks the bound rows as an interval's
    public void The_long_csv_marks_the_bounds_as_an_interval()
    {
        var rows = ChartCsv.Long(Chart(Curve(Bounds("95% CI"))), Wide).Split("\r\n");
        Assert.Contains(rows, r => r.StartsWith("2Y lower (95% CI),") && r.Contains(",interval,"));
        Assert.DoesNotContain(rows, r => r.StartsWith("2Y lower") && r.Contains(",quoted,"));
    }

    [Fact] // ADR-0303: the legend and the Notes carry the interval's meaning
    public void The_legend_and_the_notes_carry_the_meaning()
    {
        var scene = ChartScene.Build(Chart(Curve(Bounds("95% CI"))), Wide);
        Assert.Equal("2Y (95% CI)", scene.Panels[0].Legend.Single().Name);
        Assert.Contains("2Y: the interval is 95% CI.", scene.Notes);
    }
}
