namespace ExChart.Engine.Interpolation;

// Interpolation helpers a Consumer may call (ADR-0257, rewritten 2026-10-19). The chart never calls
// them: a Consumer calls one, explicitly, and hands the result in as a function, so no interpolation
// sits on the drawing's path. Each result carries its method's name, which the chart shows as it is,
// so an interpolated curve or surface is never drawn silently as data. Only general methods live
// here; a finance-specific choice (SVI, total variance, delta or strike space) stays the Consumer's.
// None extrapolates: outside the points it was made from, a result has no value, and the chart draws
// a gap or a hole there (ADR-0274).

/// <summary>
/// A curve interpolated by one of <see cref="Interpolate"/>'s methods: its method's name, the range
/// it has values over, and its value at a position. It returns null outside that range.
/// </summary>
/// <param name="Method">The method's name, shown as it is, such as "natural cubic spline".</param>
/// <param name="From">The first position it has a value at.</param>
/// <param name="To">The last position it has a value at.</param>
/// <param name="Evaluate">Its value at a position, null outside <paramref name="From"/>–<paramref name="To"/>.</param>
public sealed record Interpolant1D(string Method, double From, double To, Func<double, double?> Evaluate)
{
    /// <summary>The value at <paramref name="x"/>, or null outside the range it was made over.</summary>
    public ChartValue? ValueAt(double x) => Evaluate(x) is { } v && double.IsFinite(v) ? ChartValue.Of(v) : null;

    /// <summary>
    /// The curve as a series declared Interpolated, sampled by the chart as it draws, with one Segment
    /// named by the method, so the tooltip and the Notes say it (ADR-0257).
    /// </summary>
    public ChartSeries Series(string id, string name) =>
        ChartSeries.Sampled(id, name, SeriesKind.Level, From, To, ValueAt) with { Interpolated = true, Segments = [new Segment(From, To, Method)] };
}

/// <summary>
/// A surface interpolated by one of <see cref="Interpolate"/>'s methods: its method's name, the
/// rectangle it is evaluated over, and its value at a point. It returns null where it has no value:
/// outside the points it was made from, or where a value it would need is missing.
/// </summary>
/// <param name="Method">The method's name, shown as it is, such as "bilinear".</param>
/// <param name="XFrom">The rectangle's left edge.</param>
/// <param name="XTo">The rectangle's right edge.</param>
/// <param name="YFrom">The rectangle's near edge.</param>
/// <param name="YTo">The rectangle's far edge.</param>
/// <param name="Evaluate">Its value at a point, or null.</param>
public sealed record Interpolant2D(string Method, double XFrom, double XTo, double YFrom, double YTo, Func<double, double, double?> Evaluate)
{
    /// <summary>The value at (<paramref name="x"/>, <paramref name="y"/>), or null where it has none.</summary>
    public ChartValue? ValueAt(double x, double y) => Evaluate(x, y) is { } v && double.IsFinite(v) ? ChartValue.Of(v) : null;
}

/// <summary>
/// General interpolation methods, for a Consumer to call explicitly (ADR-0257). None extrapolates.
/// </summary>
public static class Interpolate
{
    /// <summary>Straight lines between neighbouring points; <paramref name="xs"/> strictly increasing.</summary>
    public static Interpolant1D Linear(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        Check(xs, ys.Count, 2);
        var x = xs.ToArray();
        var y = ys.ToArray();
        return new Interpolant1D("linear", x[0], x[^1], t =>
        {
            if (!(t >= x[0] && t <= x[^1])) return null;
            var k = Interval(x, t);
            var f = (t - x[k]) / (x[k + 1] - x[k]);
            return y[k] + f * (y[k + 1] - y[k]);
        });
    }

    /// <summary>A natural cubic spline through the points (zero second derivative at both ends); <paramref name="xs"/> strictly increasing.</summary>
    public static Interpolant1D NaturalCubicSpline(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        Check(xs, ys.Count, 3);
        var x = xs.ToArray();
        var y = ys.ToArray();
        var m = SecondDerivatives(x, y);
        return new Interpolant1D("natural cubic spline", x[0], x[^1], t =>
        {
            if (!(t >= x[0] && t <= x[^1])) return null;
            var k = Interval(x, t);
            var h = x[k + 1] - x[k];
            var a = (x[k + 1] - t) / h;
            var b = (t - x[k]) / h;
            return a * y[k] + b * y[k + 1] + ((a * a * a - a) * m[k] + (b * b * b - b) * m[k + 1]) * h * h / 6;
        });
    }

    /// <summary>
    /// Bilinear interpolation on a grid: <paramref name="z"/> holds one row per y value and one value
    /// per x value, both axes strictly increasing. A cell with a missing corner has no value inside.
    /// </summary>
    public static Interpolant2D Bilinear(IReadOnlyList<double> xs, IReadOnlyList<double> ys, IReadOnlyList<IReadOnlyList<double?>> z)
    {
        Check(xs, xs.Count, 2);
        Check(ys, ys.Count, 2);
        if (z.Count != ys.Count || z.Any(r => r.Count != xs.Count))
            throw new ArgumentException("The table needs one row per y value, each with one value per x value.", nameof(z));
        var x = xs.ToArray();
        var y = ys.ToArray();
        return new Interpolant2D("bilinear", x[0], x[^1], y[0], y[^1], (u, v) =>
        {
            if (!(u >= x[0] && u <= x[^1] && v >= y[0] && v <= y[^1])) return null;
            var i = Interval(x, u);
            var j = Interval(y, v);
            if (z[j][i] is not { } z00 || z[j][i + 1] is not { } z10 || z[j + 1][i] is not { } z01 || z[j + 1][i + 1] is not { } z11) return null;
            var fx = (u - x[i]) / (x[i + 1] - x[i]);
            var fy = (v - y[j]) / (y[j + 1] - y[j]);
            return (1 - fy) * ((1 - fx) * z00 + fx * z10) + fy * ((1 - fx) * z01 + fx * z11);
        });
    }

    /// <summary>
    /// Linear interpolation on a triangle mesh (P1): within each triangle the plane through its three
    /// corners. <paramref name="triangles"/> index into <paramref name="points"/>; outside every
    /// triangle there is no value. The mesh is the Consumer's, from its own solver or triangulation.
    /// </summary>
    public static Interpolant2D Triangles(IReadOnlyList<(double X, double Y, double Z)> points, IReadOnlyList<(int A, int B, int C)> triangles)
    {
        if (triangles.Count == 0) throw new ArgumentException("A mesh needs a triangle.", nameof(triangles));
        foreach (var (a, b, c) in triangles)
            if (new[] { a, b, c }.Any(k => k < 0 || k >= points.Count))
                throw new ArgumentException("A triangle names a point the mesh does not hold.", nameof(triangles));
        var p = points.ToArray();
        var t = triangles.ToArray();
        return new Interpolant2D("linear on triangles (P1)", p.Min(q => q.X), p.Max(q => q.X), p.Min(q => q.Y), p.Max(q => q.Y), (u, v) =>
        {
            foreach (var (a, b, c) in t)
            {
                var (pa, pb, pc) = (p[a], p[b], p[c]);
                var det = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
                if (det == 0) continue;
                var l1 = ((pb.Y - pc.Y) * (u - pc.X) + (pc.X - pb.X) * (v - pc.Y)) / det;
                var l2 = ((pc.Y - pa.Y) * (u - pc.X) + (pa.X - pc.X) * (v - pc.Y)) / det;
                var l3 = 1 - l1 - l2;
                const double edge = -1e-12;
                if (l1 >= edge && l2 >= edge && l3 >= edge) return l1 * pa.Z + l2 * pb.Z + l3 * pc.Z;
            }
            return null;
        });
    }

    private static void Check(IReadOnlyList<double> xs, int count, int least)
    {
        if (xs.Count != count) throw new ArgumentException("Each position needs one value.");
        if (xs.Count < least) throw new ArgumentException($"The method needs {least} points at least.");
        for (var k = 1; k < xs.Count; k++)
            if (!(xs[k - 1] < xs[k])) throw new ArgumentException("The positions must be strictly increasing.");
    }

    // The interval [x[k], x[k+1]] that holds t, which lies within x[0]..x[^1].
    private static int Interval(double[] x, double t)
    {
        var k = Array.BinarySearch(x, t);
        if (k < 0) k = ~k - 1;
        return Math.Clamp(k, 0, x.Length - 2);
    }

    private static double[] SecondDerivatives(double[] x, double[] y)
    {
        var n = x.Length;
        var m = new double[n];
        var c = new double[n];
        var d = new double[n];
        for (var k = 1; k < n - 1; k++)
        {
            var h0 = x[k] - x[k - 1];
            var h1 = x[k + 1] - x[k];
            var rhs = 6 * ((y[k + 1] - y[k]) / h1 - (y[k] - y[k - 1]) / h0);
            var diag = 2 * (h0 + h1) - h0 * c[k - 1];
            c[k] = h1 / diag;
            d[k] = (rhs - h0 * d[k - 1]) / diag;
        }
        for (var k = n - 2; k >= 1; k--) m[k] = d[k] - c[k] * m[k + 1];
        return m;
    }
}
