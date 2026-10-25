using System.Globalization;
using ExChart.Engine;

namespace ExChart.Engine.Tests;

// What only the C# engine says of a surface: its summary, and decimals and a Consumer's format carried
// through a slice. The surface, its refusals, its slices and its mesh are shared cases
// (tests/cases/chart/surface.json and surface-mesh.json, ADR-0370), run against both engines.
public class SurfaceTests
{
    private static readonly string[] Deltas = ["10P", "25P", "ATM", "25C"];
    private static readonly string[] Tenors = ["1M", "3M", "1Y"];

    private static SurfaceDefinition Vol(params double?[][] rows) =>
        new(SurfaceAxis.Of("Delta", Deltas), SurfaceAxis.Of("Tenor", Tenors),
            rows.Select(r => (IReadOnlyList<ChartValue?>)r.Select(v => v is { } d ? ChartValue.Of(d) : (ChartValue?)null).ToArray()).ToArray(),
            "Volatility", v => ((double)v).ToString("0.00", CultureInfo.InvariantCulture) + "%");

    private static SurfaceDefinition WithHole() => Vol(
        [10.5, 9.8, 9.2, 9.9],
        [10.9, null, 9.6, 10.2],
        [11.4, 10.7, 10.3, 10.8]);

    private static SurfaceDefinition Pnl(params double[] row)
    {
        var values = row.Select(v => (ChartValue?)ChartValue.Of(v)).ToArray();
        return new(SurfaceAxis.Of("Strike", new object[] { 1, 2, 3 }), SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }), [values, values], "P&L");
    }

    // z = x + y on the unit square, cut into two triangles along its diagonal.
    private static readonly SurfaceNode[] Square = [new(0, 0, 0.0), new(1, 0, 1.0), new(0, 1, 1.0), new(1, 1, 2.0)];
    private static readonly SurfaceTriangle[] Halves = [new(0, 1, 3), new(0, 3, 2)];

    [Fact] // ADR-0275: the surface is said in words, holes included
    public void The_summary_names_the_grid_the_range_and_the_holes() =>
        Assert.Equal("EURUSD, a 3D surface of Volatility over Delta from 10P to 25C and Tenor from 1M to 1Y; 4 by 3 points, from 9.20% to 11.40%, with 1 hole where the data has no value.",
            SurfaceMesh.Summary(WithHole(), "EURUSD", null));

    [Fact] // ADR-0275, ADR-0312: the summary says a logarithmic vertical scale
    public void The_summary_says_a_logarithmic_vertical_scale() =>
        Assert.Contains("The vertical scale is logarithmic.", SurfaceMesh.Summary(Pnl(1, 10, 100) with { ZScale = AxisScale.Logarithmic }, null, null));

    [Fact] // ADR-0275, ADR-0311: the summary of a Consumer's mesh counts its nodes and triangles
    public void The_summary_of_a_mesh_counts_its_nodes_and_triangles() =>
        Assert.Contains("a mesh of 4 nodes and 2 triangles", SurfaceMesh.Summary(SurfaceDefinition.FromTriangles(Square, Halves, "x", "y", "u"), null, null));

    [Fact] // ADR-0274: a slice writes its values through the surface's own format
    public void A_slice_writes_its_values_through_the_surface_format()
    {
        var smile = WithHole().SliceChart(SurfaceSlice.AtY("3M"))!;
        Assert.Equal("10.90%", smile.Series[0].Write(smile.Series[0].Points[0].Y!.Value));
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
