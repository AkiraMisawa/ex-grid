using System.Globalization;
using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class SurfaceColourMeshAndScaleTests
{
    private static SurfaceDefinition Grid(object[] xs, double?[][] rows, AxisScale xScale = AxisScale.Categories) =>
        new(SurfaceAxis.Of("Strike", xs) with { Scale = xScale }, SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
            rows.Select(r => (IReadOnlyList<ChartValue?>)r.Select(v => v is { } d ? ChartValue.Of(d) : (ChartValue?)null).ToArray()).ToArray(), "P&L");

    private static SurfaceDefinition Pnl(params double?[] row) => Grid([1, 2, 3], [row, row]);

    // z = x + y on the unit square, cut into two triangles along its diagonal.
    private static readonly SurfaceNode[] Square = [new(0, 0, 0.0), new(1, 0, 1.0), new(0, 1, 1.0), new(1, 1, 2.0)];
    private static readonly SurfaceTriangle[] Halves = [new(0, 1, 3), new(0, 3, 2)];

    [Fact] // ADR-0310: colours diverge from the centre alike on both sides: -1 beside +10 is near neutral, not the deepest band
    public void Colours_diverge_from_the_centre_alike_on_both_sides()
    {
        var sequential = SurfaceMesh.Of(Pnl(-1, -1, 10));
        Assert.Equal(0, sequential.Faces[0].Band);
        Assert.False(sequential.Bar.Diverging);

        var diverging = SurfaceMesh.Of(Pnl(-1, -1, 10) with { Centre = 0 });
        Assert.Equal(SurfaceMesh.BandCount / 2, diverging.Faces[0].Band);
        Assert.Equal(6, diverging.Faces[1].Band);
        Assert.True(diverging.Bar.Diverging);
        Assert.Equal(-10, diverging.Bar.From);
        Assert.Equal(10, diverging.Bar.To);
        Assert.Contains(diverging.Bar.Ticks, t => t.Value == 0 && t.Label == "0");
    }

    [Fact] // ADR-0310: values as far below the centre as others are above it take bands as deep
    public void Equal_distances_from_the_centre_take_bands_equally_deep()
    {
        var mesh = SurfaceMesh.Of(Pnl(-4, 0, 4) with { Centre = 0 });
        var (below, above) = (mesh.Faces[0].Band, mesh.Faces[1].Band);
        Assert.True(below < SurfaceMesh.BandCount / 2);
        Assert.Equal(SurfaceMesh.BandCount / 2 - below, above - SurfaceMesh.BandCount / 2);
    }

    [Fact] // ADR-0310: the bar of a surface coloured low to high runs over its values, its ticks within them
    public void The_bar_runs_over_the_values()
    {
        var mesh = SurfaceMesh.Of(Pnl(9.2, 10.4, 11.4));
        Assert.Equal(9.2, mesh.Bar.From);
        Assert.Equal(11.4, mesh.Bar.To);
        Assert.All(mesh.Bar.Ticks, t => Assert.InRange(t.Value, 9.2, 11.4));
        Assert.NotEmpty(mesh.Bar.Ticks);
    }

    [Fact] // ADR-0310: a bar whose span holds no round number is labelled at its ends, so it always reads back
    public void A_bar_without_a_round_number_is_labelled_at_its_ends()
    {
        var mesh = SurfaceMesh.Of(Pnl(3.1, 3.5, 3.9) with { ZScale = AxisScale.Logarithmic });
        Assert.Equal(["3.1", "3.9"], mesh.Bar.Ticks.Select(t => t.Label));
    }

    [Fact] // ADR-0310: a centre that is not a number is refused
    public void A_centre_that_is_not_finite_is_refused() =>
        Assert.Throws<ChartRefusedException>(() => Pnl(1, 2, 3) with { Centre = double.NaN });

    [Fact] // ADR-0312: a logarithmic vertical scale places the values by their logarithm, with decade ticks
    public void A_logarithmic_vertical_scale_places_values_by_their_logarithm()
    {
        var d = Pnl(1, 10, 100) with { ZScale = AxisScale.Logarithmic };
        Assert.Equal(AxisScale.Logarithmic, d.EffectiveZScale);
        var mesh = SurfaceMesh.Of(d);
        Assert.Equal(1, mesh.Nodes[1].Z!.Value, 12);
        Assert.Equal(0, mesh.ZMin, 12);
        Assert.Equal(2, mesh.ZMax, 12);
        Assert.Contains(mesh.ZTicks, t => t.Label == "100");
        Assert.True(mesh.Faces[0].Band < mesh.Faces[1].Band);
        Assert.Contains("The vertical scale is logarithmic.", SurfaceMesh.Summary(d, null, null));
    }

    [Fact] // ADR-0312, ADR-0290: a value at or below zero keeps the vertical scale proportional, and a Note names it
    public void A_value_at_or_below_zero_keeps_the_vertical_scale_proportional()
    {
        var d = Pnl(-1, 10, 100) with { ZScale = AxisScale.Logarithmic };
        Assert.Equal(AxisScale.Proportional, d.EffectiveZScale);
        Assert.Contains(d.AllNotes, n => n.StartsWith("The vertical scale stays proportional: P&L reach -1"));
        var centred = Pnl(1, 10, 100) with { ZScale = AxisScale.Logarithmic, Centre = 0 };
        Assert.Equal(AxisScale.Proportional, centred.EffectiveZScale);
        Assert.Contains(centred.AllNotes, n => n.Contains("the colours diverge from 0"));
    }

    [Fact] // ADR-0310, ADR-0312: on a logarithmic scale the colours diverge from the centre's logarithm, as a ratio about 1
    public void On_a_logarithmic_scale_the_colours_diverge_by_ratio()
    {
        var mesh = SurfaceMesh.Of(Pnl(0.25, 1, 4) with { ZScale = AxisScale.Logarithmic, Centre = 1 });
        var (below, above) = (mesh.Faces[0].Band, mesh.Faces[1].Band);
        Assert.Equal(SurfaceMesh.BandCount / 2 - below, above - SurfaceMesh.BandCount / 2);
        Assert.Contains(mesh.Bar.Ticks, t => t.Label == "1");
    }

    [Fact] // ADR-0312: an axis places its values evenly, in proportion, or by their logarithm
    public void An_axis_places_its_values_evenly_proportionally_or_logarithmically()
    {
        double[] Across(AxisScale scale) => SurfaceMesh.Of(Grid([1, 2, 4], [[1, 2, 3], [2, 3, 4]], scale)).Nodes.Take(3).Select(n => n.X).ToArray();
        Assert.Equal([0, 0.5, 1], Across(AxisScale.Categories));
        Assert.Equal([0, 1.0 / 3, 1], Across(AxisScale.Proportional));
        var log = Across(AxisScale.Logarithmic);
        Assert.Equal(0.5, log[1], 12);
        var lines = SurfaceMesh.Of(Grid([1, 2, 4], [[1, 2, 3], [2, 3, 4]], AxisScale.Logarithmic)).XLines;
        Assert.Equal(["1", "2", "4"], lines.Select(t => t.Label));
    }

    [Fact] // ADR-0312: a grid placed by value keeps a line at each value, and labels only those that do not crowd
    public void Crowded_values_share_a_label()
    {
        var lines = SurfaceMesh.Of(Grid([0.02, 0.08, 0.25, 10.0], [[1, 2, 3, 4], [2, 3, 4, 5]], AxisScale.Proportional)).XLines;
        Assert.Equal(4, lines.Count);
        Assert.Equal(["0.02", null, null, "10"], lines.Select(t => t.Label));
    }

    [Fact] // ADR-0312: an axis placed by value is refused by name where a value is not a number, does not rise, or is at or below zero on a logarithmic one
    public void An_axis_placed_by_value_refuses_what_it_cannot_place()
    {
        Assert.Contains("1M is not a number", Assert.Throws<ChartRefusedException>(() =>
            new SurfaceDefinition(SurfaceAxis.Of("Strike", new[] { 1, 2 }), SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }) with { Scale = AxisScale.Proportional }, [[1.0, 2.0], [3.0, 4.0]])).Message);
        Assert.Contains("Strike 2 comes after 3", Assert.Throws<ChartRefusedException>(() => Grid([1, 3, 2], [[1, 2, 3], [1, 2, 3]], AxisScale.Proportional)).Message);
        Assert.Contains("Strike 0 is at or below zero", Assert.Throws<ChartRefusedException>(() => Grid([0, 1, 2], [[1, 2, 3], [1, 2, 3]], AxisScale.Logarithmic)).Message);
        Assert.Throws<ChartRefusedException>(() => Pnl(1, 2, 3) with { ZScale = AxisScale.Categories });
    }

    [Fact] // ADR-0312: a function sampled on a logarithmic axis is sampled evenly in its logarithm
    public void A_function_on_a_logarithmic_axis_is_sampled_geometrically()
    {
        var s = SurfaceDefinition.Sampled("flat", 1, 100, 0, 1, (x, y) => 1.0, "Tenor", "Delta", samples: 3, xScale: AxisScale.Logarithmic);
        Assert.Equal([1.0, 10.0, 100.0], s.X.Values.Select(v => Math.Round((double)v, 9)));
        Assert.Equal(AxisScale.Logarithmic, s.X.Scale);
        Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.Sampled("flat", 0, 100, 0, 1, (x, y) => 1.0, "Tenor", "Delta", xScale: AxisScale.Logarithmic));
    }

    [Fact] // ADR-0312: a slice along an axis placed by value is a chart of numbers on that axis' scale
    public void A_slice_along_an_axis_placed_by_value_keeps_its_scale()
    {
        var chart = Grid([1, 2, 4], [[1, 2, 3], [2, 3, 4]], AxisScale.Logarithmic).SliceChart(SurfaceSlice.AtY("1M"))!;
        Assert.Equal(AxisValues.Numbers, chart.Axis.Values);
        Assert.Equal(AxisScale.Logarithmic, chart.Axis.Scale);
        Assert.Equal([1.0, 2.0, 4.0], chart.Series[0].Points.Select(p => p.X));
    }

    [Fact] // ADR-0311: a Consumer's mesh is drawn as its own triangles, each flat through its corners, never resampled
    public void A_mesh_is_drawn_as_its_own_triangles()
    {
        var d = SurfaceDefinition.FromTriangles(Square, Halves, "x", "y", "u");
        Assert.True(d.IsMesh);
        Assert.Equal(4, d.NodeCount);
        var mesh = SurfaceMesh.Of(d);
        Assert.Equal(2, mesh.Faces.Count);
        Assert.All(mesh.Faces, f => Assert.Equal(3, f.Corners.Count));
        Assert.Equal([0, 1, 3], mesh.Faces[0].Corners);
        Assert.Equal((1.0, 0.0), (mesh.Nodes[1].X, mesh.Nodes[1].Y));
        Assert.Empty(mesh.Rows);
        Assert.All(mesh.Nodes, n => Assert.False(n.Interpolated));
        Assert.Contains("a mesh of 4 nodes and 2 triangles", SurfaceMesh.Summary(d, null, null));
    }

    [Fact] // ADR-0311: a node without a value is a hole: no triangle at it is drawn
    public void A_node_without_a_value_is_a_hole()
    {
        SurfaceNode[] nodes = [.. Square[..3], new(1, 1, null)];
        var mesh = SurfaceMesh.Of(SurfaceDefinition.FromTriangles(nodes, Halves, "x", "y"));
        Assert.Empty(mesh.Faces);
        Assert.Equal(1, mesh.Holes);
    }

    [Fact] // ADR-0311: a mesh that cannot be drawn is refused by name
    public void A_mesh_that_cannot_be_drawn_is_refused_by_name()
    {
        Assert.Contains("names a node the mesh does not hold", Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.FromTriangles(Square, [new(0, 1, 4)], "x", "y")).Message);
        Assert.Contains("has no area", Assert.Throws<ChartRefusedException>(() =>
            SurfaceDefinition.FromTriangles([new(0, 0, 1.0), new(1, 1, 1.0), new(2, 2, 1.0)], [new(0, 1, 2)], "x", "y")).Message);
        Assert.Contains("a place in the plane holds one value", Assert.Throws<ChartRefusedException>(() =>
            SurfaceDefinition.FromTriangles([.. Square, new(1, 1, 3.0)], Halves, "x", "y")).Message);
        Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.FromTriangles(Square, [], "x", "y"));
        Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.FromTriangles(Square, Halves, "x", "y", xScale: AxisScale.Categories));
        Assert.Contains("at or below zero", Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.FromTriangles(Square, Halves, "x", "y", xScale: AxisScale.Logarithmic)).Message);
    }

    [Fact] // ADR-0311, ADR-0257: a slice cuts the triangles exactly, linear on each, and says it reads between nodes on them
    public void A_slice_cuts_the_triangles_exactly()
    {
        var d = SurfaceDefinition.FromTriangles(Square, Halves, "x", "y", "u");
        var chart = d.SliceChart(SurfaceSlice.AtX(0.25))!;
        var series = chart.Series[0];
        Assert.Equal([0, 0.25, 1], series.Points.Select(p => p.X));
        Assert.All(series.Points, p => Assert.Equal(0.25 + p.X, p.Y!.Value.ToDouble(), 12));
        Assert.True(series.Interpolated);
        Assert.Equal(SurfaceDefinition.TrianglesMethod, series.Segments[0].Method);
        Assert.Equal(AxisValues.Numbers, chart.Axis.Values);
        var path = Assert.Single(SurfaceMesh.SlicePath(d, SurfaceSlice.AtX(0.25)));
        Assert.All(path, p => Assert.Equal(0.25, p.X, 12));
        Assert.Null(d.SliceChart(SurfaceSlice.AtX(1.5)));
        Assert.Null(d.SliceChart(SurfaceSlice.AtX("a")));
    }

    [Fact] // ADR-0311: where the cut passes no triangle the slice has a gap, and its line on the surface breaks
    public void A_slice_through_a_hole_in_the_mesh_has_a_gap()
    {
        // Two squares side by side along y with the middle one missing: [0,1] and [2,3].
        SurfaceNode[] nodes = [new(0, 0, 0.0), new(1, 0, 0.0), new(0, 1, 1.0), new(1, 1, 1.0), new(0, 2, 2.0), new(1, 2, 2.0), new(0, 3, 3.0), new(1, 3, 3.0)];
        SurfaceTriangle[] triangles = [new(0, 1, 3), new(0, 3, 2), new(4, 5, 7), new(4, 7, 6)];
        var d = SurfaceDefinition.FromTriangles(nodes, triangles, "x", "y");
        var points = d.SliceChart(SurfaceSlice.AtX(0.5))!.Series[0].Points;
        Assert.Single(points, p => p.Y is null);
        Assert.Equal(2, SurfaceMesh.SlicePath(d, SurfaceSlice.AtX(0.5)).Count);
    }

    [Fact] // ADR-0311: a slice through decimal values reads decimals, so the values' own format writes them
    public void A_slice_through_decimals_reads_decimals()
    {
        SurfaceNode[] nodes = [new(0, 0, 1m), new(1, 0, 2m), new(0, 1, 3m)];
        var d = SurfaceDefinition.FromTriangles(nodes, [new(0, 1, 2)], "x", "y", zFormat: v => ((decimal)v).ToString("0.00", CultureInfo.InvariantCulture));
        var points = d.SliceChart(SurfaceSlice.AtY(0.5))!.Series[0].Points;
        Assert.All(points, p => Assert.True(p.Y!.Value.IsDecimal));
    }
}
