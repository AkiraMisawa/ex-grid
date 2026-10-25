using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The scales' refusals and layout are shared cases (tests/cases/chart/point-scales.json, ADR-0370); the
// tooltip, the summary and the files stay in C# until they are ported.

public class PointScaleTests
{
    private static ChartSeries Names(params double[] ys) =>
        ChartSeries.Of("n", "Names", SeriesKind.Observation, ys.Select((y, i) => new ChartPoint(i + 1, ChartValue.Of(y))));

    private static ChartScene Scene(ChartSeries s) =>
        ChartScene.Build(new ChartDefinition(ChartAxis.Numbers("Leverage"), [s]), new ChartMetrics(600, 400));

    private static IReadOnlyList<ChartValue?> Values(params double?[] vs) => vs.Select(v => v is { } d ? ChartValue.Of(d) : (ChartValue?)null).ToArray();

    [Fact] // ADR-0321: a node with no label is named by its key in the tooltip
    public void A_node_without_a_label_is_read_by_its_key()
    {
        var s = ChartSeries.Of("n", "Nodes", SeriesKind.Observation, [new ChartPoint(1, ChartValue.Of(1.0)) { Row = "0:0" }, new ChartPoint(2, ChartValue.Of(2.0)) { Row = "1:1" }])
            with { Edges = [new ChartEdge("0:0", "1:1")] };
        var d = new ChartDefinition(ChartAxis.Numbers(), [s]);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        Assert.Equal("0:0", ChartReadings.OfMark(d, scene, scene.Marks.ToList().FindIndex(m => m.Point == 0)).Position);
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
