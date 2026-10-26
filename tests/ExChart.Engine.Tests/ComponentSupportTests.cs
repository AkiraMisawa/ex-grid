using ExChart.Engine;

namespace ExChart.Engine.Tests;

// What the Engine hands the M2 components: the pieces' equality. Readings, the summary and the table are
// shared cases (tests/cases/chart/reading.json, summary.json, csv.json), as are looks, the legend's layout
// and positions (appearance.json, legend.json, scene.json).
public class ComponentSupportTests
{
    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);
    private static readonly ChartMetrics Metrics = new(800, 400);

    private static ChartDefinition Exposure() => new(ChartAxis.Numbers("Years"),
    [
        ChartSeries.Of("epe-before", "EPE · Before", SeriesKind.Level, [P(0, 1), P(1, 5), P(2, 9)]) with { Panel = "EPE", Keys = new("Before") },
        ChartSeries.Of("epe-after", "EPE · After", SeriesKind.Level, [P(0, 2), P(1, 4), P(2, 8)]) with { Panel = "EPE", Keys = new("After") },
        ChartSeries.Of("ene-after", "ENE · After", SeriesKind.Level, [P(0, -1), P(1, -3), P(2, -2)]) with { Panel = "ENE", Keys = new("After") },
    ]);

    // The shared cases compare scenes as the JSON they serialise to; the components skip a render by the
    // records' own Equals (ChartParts.ShouldRender), which only these facts hold.
    [Fact] // ADR-0252: a change to one series changes only that series' piece, by the records' equality
    public void A_change_to_one_series_changes_only_its_piece_by_equality()
    {
        var a = ChartScene.Build(Exposure(), Metrics);
        Assert.Equal(a, ChartScene.Build(Exposure(), Metrics));
        var changed = Exposure();
        var first = changed.Series[0];
        var c = ChartScene.Build(new ChartDefinition(changed.Axis,
            [first with { Points = [.. first.Points.Take(2), P(2, 8.5)] }, .. changed.Series.Skip(1)]), Metrics);
        Assert.NotEqual(a.Panels[0].Series[0], c.Panels[0].Series[0]);
        Assert.Equal(a.Panels[0].Series[1], c.Panels[0].Series[1]);
        Assert.Equal(a.Panels[1], c.Panels[1]);
    }

    [Fact] // ADR-0252, ADR-0290: an unchanged Observation series builds an equal piece, so its component skips its render
    public void An_unchanged_observation_builds_an_equal_piece()
    {
        ChartDefinition Names() => new(ChartAxis.Numbers("Leverage"),
            [ChartSeries.Of("names", "Names", SeriesKind.Observation, [P(1, 100) with { Name = "A" }, P(2, 200) with { Name = "B" }])]);
        Assert.Equal(ChartScene.Build(Names(), Metrics).Panels[0].Series[0], ChartScene.Build(Names(), Metrics).Panels[0].Series[0]);
    }
}
