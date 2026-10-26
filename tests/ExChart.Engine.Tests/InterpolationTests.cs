using ExChart.Engine;
using ExChart.Engine.Interpolation;

namespace ExChart.Engine.Tests;

// The interpolations' facts are shared cases (tests/cases/chart/interpolation.json); what stays here is
// what only the C# engine has: the surface's spoken summary, which the Script Core does not write yet.
public class InterpolationTests
{
    [Fact] // ADR-0257, ADR-0274: a surface from an interpolant says its method in its summary
    public void A_surface_from_a_helper_says_its_method_in_its_summary()
    {
        (double, double, double)[] points = [(0, 0, 9), (1, 0, 10), (0, 1, 11)];
        var s = SurfaceDefinition.Sampled(Interpolate.Triangles(points, [(0, 1, 2)]), "Strike", "Tenor", "Volatility", samples: 5);
        Assert.EndsWith("Interpolated by the Consumer: linear on triangles (P1).", SurfaceMesh.Summary(s, null, null));
    }
}
