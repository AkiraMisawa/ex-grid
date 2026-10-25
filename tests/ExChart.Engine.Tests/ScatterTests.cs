using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The marks' layout is shared cases (tests/cases/chart/scatter.json, ADR-0370); the tooltip and the files
// stay in C# until they are ported.

public class ScatterTests
{
    private static readonly ChartMetrics Metrics = new(800, 400);

    private static ChartPoint O(double x, double? y, string? name = null, object? row = null) =>
        new(x, y is { } v ? ChartValue.Of(v) : null) { Name = name, Row = row };

    private static ChartDefinition Names(params ChartPoint[] points) =>
        new(ChartAxis.Numbers("Leverage"), [ChartSeries.Of("names", "Names", SeriesKind.Observation, points)]);

    [Fact] // ADR-0290: a merged mark's tooltip says how many points it holds, named by the first
    public void A_merged_marks_tooltip_counts_its_points()
    {
        var points = Enumerable.Range(0, 50).Select(i => O(1 + i * 1e-7, 100, $"N{i}")).Append(O(5, 300, "Far")).ToArray();
        var scene = ChartScene.Build(Names(points), Metrics);
        var crowd = scene.Marks.Single(m => m.Count > 1);
        var reading = ChartReadings.OfMark(Names(points), scene, scene.Marks.ToList().IndexOf(crowd));
        Assert.Equal("N0", reading.Position);
        Assert.Contains("50 points here: N0 and 49 more", reading.Lines[0].Note);
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

}
