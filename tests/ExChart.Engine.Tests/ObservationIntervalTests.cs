using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The intervals' refusals and layout are shared cases (tests/cases/chart/observation-intervals.json,
// ADR-0370); the tooltip and the files stay in C# until they are ported.

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

    [Fact] // ADR-0304: the tooltip and the CSV say what each interval means
    public void The_tooltip_and_the_csv_say_each_meaning()
    {
        var d = Chart(Spread(), Leverage());
        var scene = ChartScene.Build(d, Metrics);
        var reading = ChartReadings.OfMark(d, scene, scene.Marks.ToList().FindIndex(m => m.Point == 2));
        Assert.Contains("y [240, 290] 95% CI", reading.Lines[0].Note);
        Assert.Contains("x [2.5, 3.1] ±1σ", reading.Lines[0].Note);
        var table = ChartCsv.WideTable(d, Metrics);
        Assert.Equal(["Series", "Point", "Leverage", "Value", "Value lower (95% CI)", "Value upper (95% CI)", "Leverage lower (±1σ)", "Leverage upper (±1σ)"], table.Header);
        Assert.Equal(["Names", "D", "3", "260", "240", "290", "2.5", "3.1"], table.Rows[2]);
        Assert.Contains(ChartCsv.Long(d, Metrics).Split("\r\n"), r => r == "Names y upper (95% CI),D,3,290,interval,");
    }

}
