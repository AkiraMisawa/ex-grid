using ExChart.Engine;

namespace ExChart.Engine.Tests;

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

    [Fact] // ADR-0321: an edge is a line between its nodes' positions
    public void An_edge_is_drawn_between_its_nodes()
    {
        var d = new ChartDefinition(ChartAxis.Numbers("Step"), [Lattice(Tree())]);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        var piece = scene.Panels[0].Series[0];
        Assert.Equal(6, piece.Edges.Count);
        var root = piece.Markers.Single(m => m.Point == 0);
        Assert.All(piece.Edges.Take(2), e => { Assert.Equal(root.X, e.X1, 6); Assert.Equal(root.Y, e.Y1, 6); });
    }

    [Fact] // ADR-0321: an edge to a node that is not there is refused by name
    public void An_edge_to_a_missing_node_is_refused()
    {
        var e = Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(), [Lattice(new ChartEdge("0,0", "3,0"))]));
        Assert.Contains("no node is keyed 3,0", e.Message);
    }

    [Fact] // ADR-0321: two nodes with one key are refused, and so is a node without a position
    public void Repeated_keys_and_nodes_without_a_position_are_refused()
    {
        var twice = ChartSeries.Of("t", "T", SeriesKind.Observation, [new ChartPoint(0, ChartValue.Of(1.0)) { Row = "a" }, new ChartPoint(1, ChartValue.Of(1.0)) { Row = "a" }])
            with { Edges = [new ChartEdge("a", "a")] };
        Assert.Contains("two nodes keyed a", Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(), [twice])).Message);
        var unplaced = ChartSeries.Of("t", "T", SeriesKind.Observation, [new ChartPoint(0, ChartValue.Of(1.0)) { Row = "a" }, new ChartPoint(1, null) { Row = "b" }])
            with { Edges = [new ChartEdge("a", "b")] };
        Assert.Contains("node b has no position", Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(), [unplaced])).Message);
    }

    [Fact] // ADR-0321: only an observation's nodes take edges
    public void Edges_on_a_line_are_refused() =>
        Assert.Throws<ChartRefusedException>(() => new ChartDefinition(ChartAxis.Numbers(),
            [ChartSeries.Of("l", "L", SeriesKind.Level, [new ChartPoint(0, ChartValue.Of(1.0)) { Row = "a" }]) with { Edges = [new ChartEdge("a", "a")] }]));

    [Fact] // ADR-0321: narrowed, an edge is cut at the plot's edge and one wholly outside is left out
    public void Edges_are_cut_to_the_range_shown()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(), [Lattice(Tree())]);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400), new ChartView(new HashSet<string>(), From: 1.5, To: 2));
        var plot = scene.Panels[0].Plot;
        var edges = scene.Panels[0].Series[0].Edges;
        Assert.Equal(4, edges.Count); // the root's two edges end at step 1, outside
        Assert.All(edges, e => Assert.True(e.X1 >= plot.X - 1e-6 && e.X2 <= plot.X + plot.Width + 1e-6));
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
