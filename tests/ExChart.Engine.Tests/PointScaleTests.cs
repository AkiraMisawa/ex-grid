using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class PointScaleTests
{
    private static ChartSeries Names(params double[] ys) =>
        ChartSeries.Of("n", "Names", SeriesKind.Observation, ys.Select((y, i) => new ChartPoint(i + 1, ChartValue.Of(y))));

    private static ChartScene Scene(ChartSeries s) =>
        ChartScene.Build(new ChartDefinition(ChartAxis.Numbers("Leverage"), [s]), new ChartMetrics(600, 400));

    private static IReadOnlyList<ChartValue?> Values(params double?[] vs) => vs.Select(v => v is { } d ? ChartValue.Of(d) : (ChartValue?)null).ToArray();

    [Fact] // ADR-0320: a point's colour is read from a column, in nine bands from its lowest value to its highest
    public void A_sequential_colour_puts_the_lowest_in_the_first_band_and_the_highest_in_the_last()
    {
        var scene = Scene(Names(1, 2, 3) with { ColourBy = new PointColour("Value", Values(0, 50, 100)) });
        var markers = scene.Panels[0].Series[0].Markers.OrderBy(m => m.Point).ToList();
        Assert.Equal([0, 4, 8], markers.Select(m => m.Band));
        var key = Assert.Single(scene.ColourKeys);
        Assert.Equal("Value", key.Title);
        Assert.False(key.Diverging);
        Assert.Equal(0, key.From);
        Assert.Equal(100, key.To);
        Assert.NotEmpty(key.Ticks);
    }

    [Fact] // ADR-0310 carried to points: diverging bands are alike on both sides of the centre, which is neutral
    public void A_diverging_colour_is_alike_on_both_sides_of_its_centre()
    {
        var scene = Scene(Names(1, 2, 3, 4) with { ColourBy = new PointColour("P&L", Values(-1, 0, 1, 10)) { Centre = 0 } });
        var bands = scene.Panels[0].Series[0].Markers.OrderBy(m => m.Point).Select(m => m.Band).ToList();
        Assert.Equal(4, bands[1]);       // the centre is the neutral band
        Assert.Equal(8, bands[3]);       // the farthest value takes the deepest band
        Assert.Equal(4 - (bands[2] - 4), bands[0]); // -1 is as deep below as +1 is above
        var key = scene.ColourKeys[0];
        Assert.True(key.Diverging);
        Assert.Equal(-10, key.From);
        Assert.Equal(10, key.To);
        Assert.Contains(key.Ticks, t => t.Value == 0);
    }

    [Fact] // ADR-0320: a size is read from a column; the marker's area is in proportion to the value
    public void A_size_makes_the_area_proportional_to_the_value()
    {
        var scene = Scene(Names(1, 2) with { SizeBy = new PointSize("Notional", Values(1, 4)) });
        var radii = scene.Panels[0].Series[0].Markers.OrderBy(m => m.Point).Select(m => m.Radius!.Value).ToList();
        Assert.Equal(2, radii[1] / radii[0], 6);
        var key = Assert.Single(scene.SizeKeys);
        Assert.Equal("Notional", key.Title);
        Assert.All(key.Entries, e => Assert.True(e.Radius > 0));
    }

    [Fact] // ADR-0320: a key takes a column at the plot's right; the plot is narrower than without one
    public void A_key_takes_a_column_beside_the_plot()
    {
        var plain = Scene(Names(1, 2));
        var keyed = Scene(Names(1, 2) with { ColourBy = new PointColour("Value", Values(1, 2)) });
        Assert.True(keyed.Panels[0].Plot.Width < plain.Panels[0].Plot.Width);
        Assert.True(keyed.ColourKeys[0].Box.X >= keyed.Panels[0].Plot.X + keyed.Panels[0].Plot.Width);
    }

    [Fact] // spine rule 1: a point with no colour value is drawn as an outline, and the Notes say how many
    public void A_point_without_a_colour_value_is_an_outline_and_said()
    {
        var scene = Scene(Names(1, 2) with { ColourBy = new PointColour("Value", Values(1, null)) });
        var m = scene.Panels[0].Series[0].Markers.Single(m => m.Point == 1);
        Assert.Null(m.Band);
        Assert.False(m.Filled);
        Assert.Contains("Names: 1 point has no Value, drawn as an outline.", scene.Notes);
    }

    [Fact] // ADR-0290: marks merge only where they look alike, so no colour is lost to a merge
    public void Marks_of_different_bands_are_not_merged()
    {
        var s = ChartSeries.Of("n", "Names", SeriesKind.Observation, [new ChartPoint(1, ChartValue.Of(1.0)), new ChartPoint(1, ChartValue.Of(1.0))])
            with { ColourBy = new PointColour("Value", Values(0, 100)) };
        Assert.Equal(2, Scene(s).Panels[0].Series[0].Markers.Count);
    }

    [Fact] // ADR-0320: only marks drawn one a row take a colour or a size
    public void A_colour_on_a_line_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Of("l", "Line", SeriesKind.Level, [new ChartPoint(1, ChartValue.Of(1.0))]) with { ColourBy = new PointColour("V", Values(1)) }]));
        Assert.Contains("'Line'", e.Message);
        Assert.Contains("observation", e.Message);
    }

    [Fact] // ADR-0320: a colour column has one value a point; a size is never negative
    public void A_scale_of_the_wrong_length_or_a_negative_size_is_refused()
    {
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(), [Names(1, 2) with { ColourBy = new PointColour("V", Values(1)) }]));
        var e = Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(), [Names(1, 2) with { SizeBy = new PointSize("N", Values(1, -2)) }]));
        Assert.Contains("negative", e.Message);
    }

    [Fact] // ADR-0275: the summary says what the colour and the size run over
    public void The_summary_says_the_colour_and_size_ranges()
    {
        var s = Names(1, 2) with { ColourBy = new PointColour("Value", Values(3, 7)), SizeBy = new PointSize("Notional", Values(10, 20)) };
        var d = new ChartDefinition(ChartAxis.Numbers(), [s]);
        var text = ChartSummary.Of(d, ChartScene.Build(d, new ChartMetrics(600, 400)), ChartView.All);
        Assert.Contains("Names: colour is Value, 3 to 7.", text);
        Assert.Contains("Names: size is Notional, 10 to 20.", text);
    }
}

public class PointScaleReadingTests
{
    [Fact] // ADR-0320: the tooltip and the file say the colour's and the size's values
    public void The_tooltip_and_the_csv_carry_the_scales()
    {
        var s = ChartSeries.Of("n", "Names", SeriesKind.Observation, [new ChartPoint(1, ChartValue.Of(2.0)) { Name = "Acme" }])
            with { ColourBy = new PointColour("P&L", [ChartValue.Of(-3m)]), SizeBy = new PointSize("Notional", [ChartValue.Of(5m)]) };
        var d = new ChartDefinition(ChartAxis.Numbers(), [s]);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        var line = ChartReadings.OfMark(d, scene, 0).Lines[0];
        Assert.Contains("P&L -3", line.Note);
        Assert.Contains("Notional 5", line.Note);
        var table = ChartCsv.WideTable(d, new ChartMetrics(600, 400));
        Assert.Equal(["P&L", "Notional"], table.Header.TakeLast(2));
        Assert.Equal(["-3", "5"], table.Rows[0].TakeLast(2));
    }
}
