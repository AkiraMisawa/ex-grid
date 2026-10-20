using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class ScatterTests
{
    private static readonly ChartMetrics Metrics = new(800, 400);

    private static ChartPoint O(double x, double? y, string? name = null, object? row = null) =>
        new(x, y is { } v ? ChartValue.Of(v) : null) { Name = name, Row = row };

    private static ChartDefinition Names(params ChartPoint[] points) =>
        new(ChartAxis.Numbers("Leverage"), [ChartSeries.Of("names", "Names", SeriesKind.Observation, points)]);

    [Fact] // ADR-0290: an Observation's points are neither ordered nor unique in x, and never joined
    public void An_observation_takes_unordered_and_repeated_positions_and_is_never_joined()
    {
        var d = Names(O(3, 200, "C"), O(1, 150, "A"), O(3, 260, "D"), O(2, 180, "B"));
        var piece = ChartScene.Build(d, Metrics).Panels[0].Series[0];
        Assert.Empty(piece.Lines);
        Assert.Equal(4, piece.Markers.Count);
    }

    [Fact] // ADR-0290: a missing value is no point, never a point at zero
    public void A_missing_value_is_no_point()
    {
        var scene = ChartScene.Build(Names(O(1, 150), O(2, null), O(3, 200)), Metrics);
        Assert.Equal(2, scene.Marks.Count);
        Assert.DoesNotContain(scene.Marks, m => m.Point == 1);
    }

    [Fact] // ADR-0290: an Observation needs a value axis across
    public void An_observation_on_categories_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Categories(["a", "b"]),
            [ChartSeries.Of("o", "Names", SeriesKind.Observation, [O(0, 1)])]));
        Assert.Contains("Observation", e.Message);
    }

    [Fact] // ADR-0290: an Observation's axes do not include zero unless the series says so
    public void Observation_axes_do_not_include_zero_by_default()
    {
        var d = Names(O(2.1, 180), O(3.5, 320));
        var axis = ChartScene.Build(d, Metrics).Panels[0].Left!;
        Assert.True(axis.Min > 0);
        Assert.True(axis.Broken);
        var fromZero = new ChartDefinition(d.Axis, [d.Series[0] with { FromZero = true }]);
        Assert.Equal(0, ChartScene.Build(fromZero, Metrics).Panels[0].Left!.Min);
    }

    [Fact] // ADR-0290: a scatter's bounds across are rounded out, so no mark stands on the plot's edge
    public void A_scatters_bounds_across_are_rounded_out()
    {
        var scene = ChartScene.Build(Names(O(2.1, 180), O(3.7, 320)), Metrics);
        Assert.True(scene.XFrom < 2.1 && scene.XTo > 3.7);
        var plot = scene.Panels[0].Plot;
        Assert.All(scene.Marks, m => Assert.True(m.X > plot.X && m.X < plot.X + plot.Width));
    }

    [Fact] // ADR-0290: markers centred on one device pixel are merged, and the mark says how many it holds
    public void Marks_on_one_device_pixel_are_merged_and_counted()
    {
        var points = Enumerable.Range(0, 50).Select(i => O(1 + i * 1e-7, 100, $"N{i}")).Append(O(5, 300, "Far")).ToArray();
        var scene = ChartScene.Build(Names(points), Metrics);
        Assert.Equal(2, scene.Marks.Count);
        var crowd = scene.Marks.Single(m => m.Count > 1);
        Assert.Equal(50, crowd.Count);
        var reading = ChartReadings.OfMark(Names(points), scene, scene.Marks.ToList().IndexOf(crowd));
        Assert.Equal("N0", reading.Position);
        Assert.Contains("50 points here: N0 and 49 more", reading.Lines[0].Note);
        Assert.Single(scene.Marks, m => m.Count == 1);
    }

    [Fact] // ADR-0290: the CSV and the table carry every point, merged or not, with its name
    public void The_csv_carries_every_point()
    {
        var points = Enumerable.Range(0, 50).Select(i => O(1 + i * 1e-7, 100, $"N{i}")).Append(O(5, 300, "Far")).ToArray();
        var table = ChartCsv.WideTable(Names(points), Metrics);
        Assert.Equal(["Series", "Point", "Leverage", "Value"], table.Header);
        Assert.Equal(51, table.Rows.Count);
        Assert.Contains("N49,", ChartCsv.Long(Names(points), Metrics));
    }

    [Fact] // ADR-0290: the reading over a mark names the point and gives its x and y
    public void The_reading_over_a_mark_names_the_point()
    {
        var d = new ChartDefinition(ChartAxis.Numbers("Leverage"),
            [ChartSeries.Of("names", "Names", SeriesKind.Observation, [O(3.2, 245, "XYZ Corp", row: 7), O(1, 100, "ABC")]) with { Format = v => $"{v}bp" }]);
        var scene = ChartScene.Build(d, Metrics);
        var reading = ChartReadings.OfMark(d, scene, scene.Marks.ToList().FindIndex(m => m.Point == 0));
        Assert.Equal("XYZ Corp", reading.Position);
        Assert.Equal("3.2, 245bp", reading.Lines[0].Value);
        Assert.Null(reading.Lines[0].Note);
    }

    [Fact] // ADR-0290: the left axis narrowed by a rectangle drops the marks outside it and cuts lines at its edge
    public void A_narrowed_left_axis_drops_marks_and_cuts_lines()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("o", "Names", SeriesKind.Observation, [O(1, 100), O(2, 200), O(3, 300)]),
            ChartSeries.Of("fit", "Fit", SeriesKind.Level, [new ChartPoint(1, ChartValue.Of(100d)), new ChartPoint(3, ChartValue.Of(300d))]) with { FromZero = false },
        ]);
        var scene = ChartScene.Build(d, Metrics, new ChartView(new HashSet<string>(), YFrom: 150, YTo: 250));
        var panel = scene.Panels[0];
        Assert.Equal(150, panel.Left!.Min);
        Assert.Equal(250, panel.Left.Max);
        Assert.Single(scene.Marks);
        var line = Assert.Single(panel.Series[1].Lines);
        Assert.All(line, p => Assert.InRange(p.Y, panel.Plot.Y - 1e-6, panel.Plot.Y + panel.Plot.Height + 1e-6));
    }

    [Fact] // ADR-0290: a logarithmic axis across places positions by their logarithm, with decade ticks
    public void A_logarithmic_axis_across_places_by_logarithm()
    {
        var d = new ChartDefinition(ChartAxis.Numbers() with { Scale = AxisScale.Logarithmic },
            [ChartSeries.Of("o", "Names", SeriesKind.Observation, [O(1, 1), O(10, 2), O(100, 3)])]);
        var scene = ChartScene.Build(d, Metrics);
        Assert.Equal(1, scene.XFrom);
        Assert.Equal(100, scene.XTo);
        var xs = scene.Marks.OrderBy(m => m.X).Select(m => m.X).ToList();
        Assert.Equal(xs[1] - xs[0], xs[2] - xs[1], 6);
        Assert.Contains(scene.XTicks, t => t.Position == 10);
        Assert.Equal(10, scene.PositionAt(xs[1]), 6);
    }

    [Fact] // ADR-0290: a logarithmic axis across refuses a position at or below zero by name
    public void A_logarithmic_axis_refuses_a_position_at_or_below_zero()
    {
        var e = Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers() with { Scale = AxisScale.Logarithmic },
            [ChartSeries.Of("o", "Names", SeriesKind.Observation, [O(0, 1), O(1, 2)])]));
        Assert.Contains("'Names'", e.Message);
    }

    [Fact] // ADR-0290: a logarithmic vertical axis, and a value at or below zero keeps it proportional with a Note naming it
    public void A_logarithmic_vertical_axis_and_its_refusal()
    {
        var view = new ChartView(new HashSet<string>(), YScale: AxisScale.Logarithmic);
        var scene = ChartScene.Build(Names(O(1, 20), O(2, 300), O(3, 4000)), Metrics, view);
        var axis = scene.Panels[0].Left!;
        Assert.True(axis.Logarithmic);
        Assert.Equal(10, axis.Min);
        Assert.Equal(10000, axis.Max);
        Assert.Contains(axis.Ticks, t => t.Value == 100);
        Assert.Empty(scene.Notes);

        var refused = ChartScene.Build(Names(O(1, 0, "Zero Co"), O(2, 300)), Metrics, view);
        Assert.False(refused.Panels[0].Left!.Logarithmic);
        Assert.Contains(refused.Notes, n => n.Contains("'Names'") && n.Contains("logarithmic"));
        Assert.Equal(2, refused.Marks.Count);
    }

    [Fact] // ADR-0290: a click finds the point's Row Key through its mark
    public void A_mark_leads_to_its_points_row()
    {
        var d = Names(O(1, 100, "A", row: "row-a"), O(2, 200, "B", row: "row-b"));
        var scene = ChartScene.Build(d, Metrics);
        var mark = scene.Marks.Single(m => m.Point == 1);
        Assert.Equal("row-b", d.Series[0].Points[mark.Point].Row);
    }
}
