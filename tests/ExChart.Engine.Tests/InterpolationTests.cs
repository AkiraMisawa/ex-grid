using ExChart.Engine;
using ExChart.Engine.Interpolation;

namespace ExChart.Engine.Tests;

public class InterpolationTests
{
    [Fact] // ADR-0257: a helper's result names its method, and has no value outside its points
    public void Linear_names_its_method_and_does_not_extrapolate()
    {
        var f = Interpolate.Linear([1, 2, 4], [10, 20, 0]);
        Assert.Equal("linear", f.Method);
        Assert.Equal(15, f.ValueAt(1.5)!.Value.ToDouble(), 12);
        Assert.Equal(10, f.ValueAt(3)!.Value.ToDouble(), 12);
        Assert.Null(f.ValueAt(0.999));
        Assert.Null(f.ValueAt(4.001));
    }

    [Fact] // ADR-0257: a natural cubic spline passes through its points, is straight past none of them, and stops at the ends
    public void A_natural_cubic_spline_passes_through_its_points()
    {
        double[] xs = [0, 1, 2, 3];
        double[] ys = [0, 1, 8, 27];
        var f = Interpolate.NaturalCubicSpline(xs, ys);
        Assert.Equal("natural cubic spline", f.Method);
        for (var k = 0; k < xs.Length; k++) Assert.Equal(ys[k], f.ValueAt(xs[k])!.Value.ToDouble(), 10);
        // On points of a straight line, the natural spline is that line.
        Assert.Equal(2.5, Interpolate.NaturalCubicSpline([0, 1, 2, 3], [1, 2, 3, 4]).ValueAt(1.5)!.Value.ToDouble(), 12);
        Assert.Null(f.ValueAt(3.5));
    }

    [Fact] // ADR-0257: the curve as a series is declared Interpolated, with a Segment named by its method
    public void A_curve_becomes_an_interpolated_series_that_names_its_method()
    {
        var series = Interpolate.Linear([0, 1], [1, 2]).Series("zero", "Zero rate");
        Assert.True(series.Interpolated);
        Assert.Equal("linear", Assert.Single(series.Segments).Method);
        Assert.NotNull(series.Sampler);
    }

    [Fact] // ADR-0257, ADR-0274: bilinear inside a cell; a cell with a missing corner stays a hole
    public void Bilinear_keeps_a_missing_corner_a_hole()
    {
        var f = Interpolate.Bilinear([0, 1, 2], [0, 1], [[0, 1, 2], [10, null, 12]]);
        Assert.Equal("bilinear", f.Method);
        Assert.Null(f.ValueAt(0.5, 0.5));
        Assert.Equal(6, Interpolate.Bilinear([0, 1], [0, 1], [[0, 2], [10, 12]]).ValueAt(0.5, 0.5)!.Value.ToDouble(), 12);
        Assert.Null(f.ValueAt(1.5, 0.5));
        Assert.Null(f.ValueAt(2.5, 0.5));
    }

    [Fact] // ADR-0257: linear on triangles (P1) is the plane of the triangle the point lies in, and nothing outside the mesh
    public void Triangles_are_linear_inside_and_have_no_value_outside()
    {
        (double, double, double)[] points = [(0, 0, 0), (1, 0, 1), (0, 1, 2)];
        var f = Interpolate.Triangles(points, [(0, 1, 2)]);
        Assert.Equal("linear on triangles (P1)", f.Method);
        Assert.Equal(0.25 + 0.5, f.ValueAt(0.25, 0.25)!.Value.ToDouble(), 12);
        Assert.Null(f.ValueAt(0.8, 0.8));
    }

    [Fact] // ADR-0257, ADR-0274: a surface handed in as a function is sampled, named by its method, and a hole where it has no value
    public void A_surface_from_a_helper_is_sampled_and_names_its_method()
    {
        (double, double, double)[] points = [(0, 0, 9), (1, 0, 10), (0, 1, 11)];
        var s = SurfaceDefinition.Sampled(Interpolate.Triangles(points, [(0, 1, 2)]), "Strike", "Tenor", "Volatility", samples: 5);
        Assert.Equal("linear on triangles (P1)", s.Method);
        Assert.Equal(5, s.X.Values.Count);
        Assert.True(s.IsInterpolated(0, 0));
        Assert.Null(s.At(4, 4));
        Assert.Equal(9, s.At(0, 0)!.Value.ToDouble(), 12);
        Assert.Contains("Interpolated by the Consumer: linear on triangles (P1).", s.AllNotes);
        Assert.EndsWith("Interpolated by the Consumer: linear on triangles (P1).", SurfaceMesh.Summary(s, null, null));
        Assert.All(SurfaceMesh.Of(s).Nodes, n => Assert.True(n.Interpolated));
    }

    [Fact] // ADR-0257: a surface handed in as a function names its method
    public void A_function_without_a_method_is_refused() =>
        Assert.Throws<ChartRefusedException>(() => SurfaceDefinition.Sampled(" ", 0, 1, 0, 1, (x, y) => x + y, "x", "y"));

    [Fact] // ADR-0257: values marked interpolated name their method, and the slice through them says it
    public void Marked_values_name_their_method_and_the_slice_says_it()
    {
        IReadOnlyList<IReadOnlyList<ChartValue?>> z = [[9.0, 9.5, 10.0], [10.0, 10.5, 11.0]];
        IReadOnlyList<IReadOnlyList<bool>> marks = [[false, true, false], [false, true, false]];
        var x = SurfaceAxis.Of("Strike", new[] { 1.0, 1.1, 1.2 });
        var y = SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" });
        Assert.Throws<ChartRefusedException>(() => new SurfaceDefinition(x, y, z, interpolated: marks));
        var s = new SurfaceDefinition(x, y, z, "Volatility", method: "natural cubic spline in strike", interpolated: marks);
        Assert.False(s.IsInterpolated(0, 0));
        Assert.True(s.IsInterpolated(1, 1));
        var smile = s.SliceChart(SurfaceSlice.AtY("1Y"))!.Series[0];
        Assert.True(smile.Interpolated);
        Assert.Equal("natural cubic spline in strike", Assert.Single(smile.Segments).Method);
        Assert.False(s.SliceChart(SurfaceSlice.AtX(1.0))!.Series[0].Interpolated);
    }
}
