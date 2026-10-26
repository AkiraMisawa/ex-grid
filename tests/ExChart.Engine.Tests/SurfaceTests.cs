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
