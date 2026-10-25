using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The interval's refusals and layout are shared cases (tests/cases/chart/intervals.json, ADR-0370); the
// tooltip and the files stay in C# until they are ported.

public class IntervalTests
{
    private static readonly ChartMetrics Wide = new(1200, 400);

    private static ChartSeries Curve(SeriesInterval? interval, int count = 5) =>
        ChartSeries.Of("2y", "2Y", SeriesKind.Level, Enumerable.Range(0, count).Select(i => new ChartPoint(i, 3.0m + i * 0.01m))) with { Interval = interval, FromZero = false };

    private static SeriesInterval Bounds(string meaning, IntervalStyle style = IntervalStyle.Whiskers, int count = 5, int? missing = null) =>
        new(meaning, Enumerable.Range(0, count).Select(i => new IntervalBound(i, i == missing ? null : 2.9m + i * 0.01m, 3.1m + i * 0.01m)).ToList(), style);

    private static ChartDefinition Chart(ChartSeries s) => new(ChartAxis.Numbers(), [s]);

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

}
