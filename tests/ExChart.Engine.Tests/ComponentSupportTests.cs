using System.Globalization;
using ExChart.Engine;

namespace ExChart.Engine.Tests;

// What the Engine hands the M2 components: the pieces' equality, readings, the summary and the table.
// Looks, the legend's layout and positions are shared cases (tests/cases/chart/appearance.json,
// legend.json, scene.json).
public class ComponentSupportTests
{
    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);
    private static ChartPoint D(double x, decimal y) => new(x, ChartValue.Of(y));
    private static readonly ChartMetrics Metrics = new(800, 400);
    private static ChartView Hide(params string[] ids) => new(ids.ToHashSet());

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

    [Fact] // ADR-0270, ADR-0251: a level's value is the one handed in, through its format; between points there is none
    public void A_level_reads_the_value_handed_in_and_nothing_between()
    {
        var definition = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("a", "A", SeriesKind.Level, [D(0, 1234567.89m), D(2, 2m)]) with { Format = v => ((decimal)v).ToString("N2", CultureInfo.InvariantCulture) },
            ChartSeries.Of("b", "B", SeriesKind.Level, [D(0, 1m), D(1, 1m), D(2, 1m)]),
        ]);
        var scene = ChartScene.Build(definition, Metrics);
        var at0 = ChartReadings.At(definition, scene, Metrics, ChartView.All, 0, 0, 0);
        Assert.Equal("1,234,567.89", at0.Lines.Single(l => l.Id == "a").Value);
        var at1 = ChartReadings.At(definition, scene, Metrics, ChartView.All, 1, 0, 0);
        var a = at1.Lines.Single(l => l.Id == "a");
        Assert.Null(a.Value);
        Assert.Equal("no point here", a.Note);
        Assert.Equal("1", at1.Lines.Single(l => l.Id == "b").Value);
    }

    [Fact] // ADR-0270, ADR-0253: a step names its interval
    public void A_step_names_its_interval()
    {
        var definition = new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Of("h", "Hazard", SeriesKind.RateOverInterval, [P(0, 0.01), P(1, 0.02), P(3, 0.03)])]);
        var scene = ChartScene.Build(definition, Metrics);
        var line = ChartReadings.At(definition, scene, Metrics, ChartView.All, 3, 0, 0).Lines.Single();
        Assert.Equal("0.03", line.Value);
        Assert.Equal("1 – 3", line.Note);
    }

    [Fact] // ADR-0270, ADR-0254: a thinned pixel names its range and count
    public void A_thinned_pixel_names_its_range()
    {
        var points = Enumerable.Range(0, 5000).Select(i => P(i, i % 7)).ToList();
        var definition = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("n", "Noise", SeriesKind.Level, points)]);
        var scene = ChartScene.Build(definition, Metrics);
        var line = ChartReadings.At(definition, scene, Metrics, ChartView.All, 2500, 0, 0).Lines.Single();
        Assert.Contains(" – ", line.Value, StringComparison.Ordinal);
        Assert.Contains("points", line.Note, StringComparison.Ordinal);
    }

    [Fact] // ADR-0270: past what fits the plot, the nearest series are listed and the rest counted
    public void Past_what_fits_the_nearest_are_listed_and_the_rest_counted()
    {
        var series = Enumerable.Range(0, 40).Select(i => ChartSeries.Of($"s{i}", $"S{i}", SeriesKind.Level, [P(0, i), P(1, i)])).ToList();
        var definition = new ChartDefinition(ChartAxis.Numbers(), series);
        var small = Metrics with { Height = 200 };
        var scene = ChartScene.Build(definition, small, legend: new LegendPlacement(LegendPosition.None));
        var plot = scene.Panels[0].Plot;
        var reading = ChartReadings.At(definition, scene, small, ChartView.All, 0, 0, plot.Y);
        Assert.True(reading.More > 0);
        Assert.Equal(40, reading.Lines.Count + reading.More);
        Assert.True((reading.Lines.Count + 2) * small.LineHeight <= plot.Height);
        Assert.Contains(reading.Lines, l => l.Id == "s39");
        Assert.DoesNotContain(reading.Lines, l => l.Id == "s0");
    }

    [Fact] // ADR-0275, CH-8: the summary names the axis with its range and each visible series with its maximum
    public void The_summary_names_the_axes_and_each_series_maximum()
    {
        var scene = ChartScene.Build(Exposure(), Metrics, Hide("epe-before"));
        var summary = ChartSummary.Of(Exposure(), scene, Hide("epe-before"), "Exposure profile", "Netting set 12.");
        Assert.StartsWith("Exposure profile. Years, 0 to 2.", summary, StringComparison.Ordinal);
        Assert.Contains("EPE · After: maximum 8 at 2.", summary, StringComparison.Ordinal);
        Assert.Contains("ENE · After: maximum -1 at 0.", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Before", summary, StringComparison.Ordinal);
        Assert.Contains("axis,", summary, StringComparison.Ordinal);
        Assert.EndsWith("Netting set 12.", summary, StringComparison.Ordinal);
    }

    [Fact] // ADR-0275, CH-8: the table holds the wide CSV's content
    public void The_table_holds_the_csv_content()
    {
        var table = ChartCsv.WideTable(Exposure(), Metrics, Hide("ene-after"));
        var csv = ChartCsv.Wide(Exposure(), Metrics, Hide("ene-after")).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(csv[0], string.Join(',', table.Header));
        Assert.Equal(csv.Skip(1), table.Rows.Select(r => string.Join(',', r)));
    }
}
