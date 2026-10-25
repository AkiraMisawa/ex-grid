using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The layout of edges is shared cases (tests/cases/chart/edges.json, ADR-0370); the tooltip, the summary
// and the table stay in C# until they are ported.

public class EdgeTests
{
    // A two-step binomial lattice: node (i, j) at step i with j up-moves.
    private static ChartSeries Lattice(params ChartEdge[] edges)
    {
        var nodes = new List<ChartPoint>();
        for (var i = 0; i <= 2; i++)
            for (var j = 0; j <= i; j++)
                nodes.Add(new ChartPoint(i, ChartValue.Of(100.0 * Math.Pow(1.1, j) * Math.Pow(0.9, i - j))) { Row = $"{i},{j}" });
        return ChartSeries.Of("tree", "Tree", SeriesKind.Observation, nodes) with { Edges = edges };
    }

    private static ChartEdge[] Tree() =>
    [
        new("0,0", "1,0"), new("0,0", "1,1"),
        new("1,0", "2,0"), new("1,0", "2,1"), new("1,1", "2,1"), new("1,1", "2,2"),
    ];

    [Fact] // ADR-0321: the pointer can be over an edge, and its tooltip says its two keys
    public void An_edges_tooltip_says_its_two_keys()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(), [Lattice(Tree())]);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        var edge = scene.EdgeMarks.ToList().FindIndex(e => e.Edge.Index == 4);
        var reading = ChartReadings.OfEdge(d, scene, edge);
        Assert.Equal("1,1 → 2,1", reading.Position);
        var line = Assert.Single(reading.Lines);
        Assert.Equal("Tree", line.Name);
        Assert.Equal("edge from 1,1 to 2,1", line.Value);
    }

    [Fact] // ADR-0321, ADR-0275: the summary says the nodes and the edges, and the table lists the edges after the nodes
    public void The_summary_and_the_table_say_the_edges()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(), [Lattice(Tree())]);
        var metrics = new ChartMetrics(600, 400);
        Assert.Contains("Tree: 6 nodes and 6 edges", ChartSummary.Of(d, ChartScene.Build(d, metrics), ChartView.All));
        var table = ChartCsv.WideTable(d, metrics);
        Assert.Equal(12, table.Rows.Count);
        Assert.Equal("edge 0,0 → 1,0", table.Rows[6][1]);
    }
}
