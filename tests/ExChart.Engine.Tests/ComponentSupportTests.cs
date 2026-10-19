using System.Globalization;
using ExChart.Engine;

namespace ExChart.Engine.Tests;

// What the Engine hands the M2 components: looks, the legend's layout, positions, readings, the
// summary and the table.
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

    [Fact] // ADR-0256: the same colour key takes the same slot in every panel, and hiding keeps every look
    public void The_same_key_takes_the_same_look_and_hiding_keeps_it()
    {
        var (looks, repeat) = Appearance.Of(Exposure());
        Assert.False(repeat);
        Assert.Equal(0, looks["epe-before"].Colour);
        Assert.Equal(1, looks["epe-after"].Colour);
        Assert.Equal(looks["epe-after"], looks["ene-after"]);

        var hidden = ChartScene.Build(Exposure(), Metrics, Hide("epe-before"));
        Assert.Equal(1, hidden.Panels[0].Series[0].Look.Colour);
    }

    [Fact] // ADR-0256, CH-6: past the last colour slot colours repeat, and the chart says so
    public void Past_the_last_slot_colours_repeat_and_a_note_says_so()
    {
        var series = Enumerable.Range(0, 12).Select(i => ChartSeries.Of($"c{i}", $"Counterparty {i}", SeriesKind.Level, [P(0, i), P(1, i)])).ToList();
        var definition = new ChartDefinition(ChartAxis.Numbers(), series);
        var (looks, repeat) = Appearance.Of(definition);
        Assert.True(repeat);
        Assert.Equal(0, looks["c10"].Colour);
        Assert.Contains(ChartScene.Build(definition, Metrics).Notes, n => n.StartsWith("Colours repeat", StringComparison.Ordinal));
        Assert.DoesNotContain(ChartScene.Build(definition, Metrics with { ColourSlots = 12 }).Notes, n => n.StartsWith("Colours repeat", StringComparison.Ordinal));
    }

    [Fact] // ADR-0256: line-style and marker keys take their slots in order; none set is solid and a circle
    public void Line_style_and_marker_keys_take_slots_in_order()
    {
        var definition = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("a", "A", SeriesKind.Level, [P(0, 1)]) with { Keys = new("x", "Before", "m1") },
            ChartSeries.Of("b", "B", SeriesKind.Level, [P(0, 1)]) with { Keys = new("x", "After", "m2") },
            ChartSeries.Of("c", "C", SeriesKind.Level, [P(0, 1)]),
        ]);
        var (looks, _) = Appearance.Of(definition);
        Assert.Equal(new SeriesLook(0, 0, 0), looks["a"]);
        Assert.Equal(new SeriesLook(0, 1, 1), looks["b"]);
        Assert.Equal(MarkerShape.Square, looks["b"].Shape);
        Assert.Equal(new SeriesLook(1, 0, 0), looks["c"]);
    }

    [Fact] // ADR-0271: each panel has its own legend, hidden entries stay, and none sits over a plot
    public void Each_panel_has_its_own_legend_off_the_plot()
    {
        var scene = ChartScene.Build(Exposure(), Metrics, Hide("epe-before"));
        Assert.Equal(["epe-before", "epe-after"], scene.Panels[0].Legend.Select(e => e.Id));
        Assert.True(scene.Panels[0].Legend[0].Hidden);
        Assert.Equal(["ene-after"], scene.Panels[1].Legend.Select(e => e.Id));
        foreach (var p in scene.Panels)
            foreach (var e in p.Legend)
                Assert.True(e.Box.Y + e.Box.Height <= p.Plot.Y, "a top legend sits above its plot");
    }

    [Fact] // ADR-0271: a panel whose series are all hidden keeps its legend, so they can be shown again
    public void A_panel_with_every_series_hidden_keeps_its_legend()
    {
        var scene = ChartScene.Build(Exposure(), Metrics, Hide("ene-after"));
        Assert.Equal(2, scene.Panels.Count);
        Assert.Empty(scene.Panels[1].Series);
        Assert.Single(scene.Panels[1].Legend);
    }

    [Theory] // ADR-0271: left and right legends take a column beside the plots; none takes nothing
    [InlineData(LegendPosition.Left)]
    [InlineData(LegendPosition.Right)]
    [InlineData(LegendPosition.Bottom)]
    [InlineData(LegendPosition.None)]
    public void A_legend_takes_its_own_space(LegendPosition position)
    {
        var top = ChartScene.Build(Exposure(), Metrics);
        var scene = ChartScene.Build(Exposure(), Metrics, legend: new LegendPlacement(position));
        var plot = scene.Panels[0].Plot;
        foreach (var p in scene.Panels)
            foreach (var e in p.Legend)
                Assert.False(e.Box.X < p.Plot.X + p.Plot.Width && e.Box.X + e.Box.Width > p.Plot.X
                    && e.Box.Y < p.Plot.Y + p.Plot.Height && e.Box.Y + e.Box.Height > p.Plot.Y, "an entry over the plot");
        switch (position)
        {
            case LegendPosition.Left:
                Assert.True(plot.X > top.Panels[0].Plot.X);
                Assert.All(scene.Panels[0].Legend, e => Assert.True(e.Box.X + e.Box.Width <= plot.X));
                break;
            case LegendPosition.Right:
                Assert.True(plot.Width < top.Panels[0].Plot.Width);
                Assert.All(scene.Panels[0].Legend, e => Assert.True(e.Box.X >= plot.X + plot.Width));
                break;
            case LegendPosition.Bottom:
                Assert.All(scene.Panels[^1].Legend, e => Assert.True(e.Box.Y >= scene.Panels[^1].Plot.Y + scene.Panels[^1].Plot.Height));
                break;
            case LegendPosition.None:
                Assert.All(scene.Panels, p => Assert.Empty(p.Legend));
                Assert.True(plot.Height > top.Panels[0].Plot.Height);
                break;
        }
    }

    [Theory] // ADR-0271: the alignment moves a row of entries along its side
    [InlineData(LegendAlign.Start)]
    [InlineData(LegendAlign.Centre)]
    [InlineData(LegendAlign.End)]
    public void The_alignment_moves_the_entries(LegendAlign align)
    {
        var scene = ChartScene.Build(Exposure(), Metrics, legend: new LegendPlacement(LegendPosition.Top, align));
        var p = scene.Panels[0];
        var left = p.Legend[0].Box.X - p.Plot.X;
        var right = p.Plot.X + p.Plot.Width - (p.Legend[^1].Box.X + p.Legend[^1].Box.Width);
        switch (align)
        {
            case LegendAlign.Start: Assert.Equal(0, left, 6); break;
            case LegendAlign.Centre: Assert.Equal(left, right, 6); break;
            case LegendAlign.End: Assert.Equal(0, right, 6); break;
        }
    }

    [Fact] // ADR-0255: the positions are every point a visible series has in range, left to right
    public void The_positions_are_the_points_in_range()
    {
        var scene = ChartScene.Build(Exposure(), Metrics, new ChartView(new HashSet<string>(), From: 1));
        Assert.Equal([1d, 2d], scene.Positions.Select(p => p.Position));
        Assert.True(scene.Positions[0].X < scene.Positions[1].X);
    }

    [Fact] // ADR-0272, CH-5: a hidden key naming no series, and a range with no point, are said
    public void A_stale_hidden_key_and_an_empty_range_are_said()
    {
        var stale = ChartScene.Build(Exposure(), Metrics, Hide("pfe-99"));
        Assert.Contains("\"pfe-99\" is hidden, but no series has that key.", stale.Notes);
        var empty = ChartScene.Build(Exposure(), Metrics, new ChartView(new HashSet<string>(), From: 0.2, To: 0.8));
        Assert.Contains(empty.Notes, n => n.StartsWith("No series has a point between", StringComparison.Ordinal));
        Assert.DoesNotContain(ChartScene.Build(Exposure(), Metrics).Notes, n => n.StartsWith("No series", StringComparison.Ordinal));
    }

    [Fact] // ADR-0251: the viewer switches a number axis to categories, spaced evenly and named as the axis writes them
    public void A_number_axis_switched_to_categories_is_spaced_evenly()
    {
        var definition = new ChartDefinition(ChartAxis.Numbers("Tenor"),
            [ChartSeries.Of("r", "Rate", SeriesKind.Level, [P(0.5, 1), P(1, 2), P(10, 3)])]);
        var scene = ChartScene.Build(definition, Metrics, new ChartView(new HashSet<string>(), Scale: AxisScale.Categories));
        var xs = scene.Positions.Select(p => p.X).ToList();
        Assert.Equal(xs[1] - xs[0], xs[2] - xs[1], 6);
        Assert.Contains(scene.XTicks, t => t.Label == "6M");
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

    [Fact] // ADR-0256, ADR-0257: a level is marked only when its marker key asks, or when it is an Interpolated series' quotes
    public void A_level_is_marked_only_when_asked()
    {
        var definition = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("plain", "Plain", SeriesKind.Level, [P(0, 1), P(1, 2)]),
            ChartSeries.Of("asked", "Asked", SeriesKind.Level, [P(0, 1), P(1, 2)]) with { Keys = new("asked", null, "dot") },
            ChartSeries.Of("quotes", "Quotes", SeriesKind.Level, [P(0, 1), P(1, 2)]) with { Keys = new("curve") },
            ChartSeries.Sampled("curve", "Curve", SeriesKind.Level, 0, 1, x => x) with { Interpolated = true, Keys = new("curve") },
        ]);
        var pieces = ChartScene.Build(definition, Metrics).Panels[0].Series.ToDictionary(p => p.Id);
        Assert.Empty(pieces["plain"].Markers);
        Assert.Equal(2, pieces["asked"].Markers.Count);
        Assert.Equal(2, pieces["quotes"].Markers.Count);
    }

    [Fact] // ADR-0251: a vertical axis never labels zero "-0"
    public void Zero_is_never_labelled_negative()
    {
        var definition = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("n", "N", SeriesKind.Level, [P(0, -9.7), P(1, -0.3)])]);
        var ticks = ChartScene.Build(definition, Metrics).Panels[0].Left!.Ticks;
        Assert.Contains(ticks, t => t.Label == "0");
        Assert.DoesNotContain(ticks, t => t.Label == "-0");
    }
}
