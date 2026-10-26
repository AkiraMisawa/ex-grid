using System.Globalization;

namespace ExChart.Engine.Fitting;

// Fit helpers a Consumer may call (ADR-0291), beside the interpolation helpers and in their shape. The
// chart never calls them: a Consumer calls one, explicitly, and hands the result in as a series, so no
// fit sits on the drawing's path. Each result names its method, its coefficients and the space it was
// fitted in, and its series carries that as a Segment, so the Notes say it. None extrapolates: the
// series has values only between the smallest and the largest x it was fitted on.

/// <summary>The space a fit is made in, when it is not the plotted one (ADR-0291).</summary>
public enum FitSpace
{
    /// <summary>y against x.</summary>
    Linear,

    /// <summary>ln y against x: an exponential curve.</summary>
    LogY,

    /// <summary>ln y against ln x: a power law, straight on logarithmic axes.</summary>
    LogLog,
}

/// <summary>
/// A curve fitted by one of <see cref="Fit"/>'s methods: its method, its coefficients in the space it was
/// fitted in, R² there, how many points it was fitted on, and the range of x they span.
/// </summary>
/// <param name="Method">The method's name, such as "OLS" or "Theil–Sen".</param>
/// <param name="Coefficients">The polynomial's coefficients from the constant up: for a line, the intercept then the slope.</param>
/// <param name="RSquared">The share of the variance it explains, in the space it was fitted in; never below zero for least squares, and below zero for Theil–Sen where it does worse than the mean.</param>
/// <param name="Count">How many points it was fitted on.</param>
/// <param name="From">The smallest x it was fitted on.</param>
/// <param name="To">The largest.</param>
/// <param name="Space">The space it was fitted in.</param>
public sealed record FittedCurve(string Method, IReadOnlyList<double> Coefficients, double RSquared, int Count, double From, double To, FitSpace Space = FitSpace.Linear)
{
    /// <summary>The intercept: the constant coefficient.</summary>
    public double Intercept => Coefficients[0];

    /// <summary>The slope: the first-degree coefficient, or zero for a constant.</summary>
    public double Slope => Coefficients.Count > 1 ? Coefficients[1] : 0;

    /// <summary>The polynomial's degree.</summary>
    public int Degree => Coefficients.Count - 1;

    /// <summary>The fitted value at <paramref name="x"/>, or null outside the range it was fitted over.</summary>
    public ChartValue? ValueAt(double x)
    {
        if (!(x >= From && x <= To)) return null;
        var t = Space == FitSpace.LogLog ? Math.Log(x) : x;
        var v = 0d;
        for (var k = Coefficients.Count - 1; k >= 0; k--) v = v * t + Coefficients[k];
        if (Space != FitSpace.Linear) v = Math.Exp(v);
        return double.IsFinite(v) ? ChartValue.Of(v) : null;
    }

    /// <summary>
    /// Its Note: one line naming the method and the space, the equation for a line or the degree for a
    /// higher polynomial, R² and n (ADR-0291), such as "OLS: y = 41.2x + 88.5, R² = 0.63, n = 212".
    /// </summary>
    public string Note
    {
        get
        {
            var (y, x) = Space switch { FitSpace.LogY => ("ln y", "x"), FitSpace.LogLog => ("ln y", "ln x"), _ => ("y", "x") };
            var on = Space == FitSpace.Linear ? "" : $" on {y} vs {x}";
            var shape = Degree switch
            {
                0 => $"{y} = {N(Intercept)}",
                1 => $"{y} = {N(Slope)}{x} {(Intercept < 0 ? "−" : "+")} {N(Math.Abs(Intercept))}",
                _ => $"degree {Degree}",
            };
            return $"{Method}{on}: {shape}, R² = {RSquared.ToString("0.00", CultureInfo.InvariantCulture)}, n = {Count}";
        }
    }

    /// <summary>
    /// The curve as a Level series over the range it was fitted on, sampled by the chart as it draws, read
    /// away from zero, with one Segment named by its Note, so the Notes say it (ADR-0291).
    /// </summary>
    public ChartSeries Series(string id, string name) =>
        ChartSeries.Sampled(id, name, SeriesKind.Level, From, To, ValueAt) with { FromZero = false, Segments = [new Segment(From, To, Note)] };

    private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>General fits, for a Consumer to call explicitly (ADR-0291). A point whose x or y is not a finite number is refused.</summary>
public static class Fit
{
    /// <summary>The most points <see cref="TheilSen"/> takes: it weighs every pair, which past this is too many to run.</summary>
    public const int TheilSenLimit = 3_000;

    /// <summary>Ordinary least squares: the line that minimises the squared vertical distances.</summary>
    public static FittedCurve LeastSquares(IReadOnlyList<double> xs, IReadOnlyList<double> ys, FitSpace space = FitSpace.Linear) =>
        Fitted(xs, ys, 1, space, "OLS");

    /// <summary>Least squares of a polynomial of <paramref name="degree"/>, solved by Householder QR.</summary>
    public static FittedCurve Polynomial(IReadOnlyList<double> xs, IReadOnlyList<double> ys, int degree, FitSpace space = FitSpace.Linear) =>
        Fitted(xs, ys, degree, space, degree == 1 ? "OLS" : "Least squares");

    /// <summary>
    /// Theil–Sen: the median of the slopes between every pair of points with different x, and the median
    /// of yᵢ − slope·xᵢ as the intercept. A single point far off does not drag it.
    /// </summary>
    public static FittedCurve TheilSen(IReadOnlyList<double> xs, IReadOnlyList<double> ys, FitSpace space = FitSpace.Linear)
    {
        var (x, y) = Prepared(xs, ys, space, 2);
        if (x.Length > TheilSenLimit)
            throw new ArgumentException($"Theil–Sen weighs every pair of points; {x.Length} points are more than the {TheilSenLimit} it takes.");
        var slopes = new List<double>(x.Length * (x.Length - 1) / 2);
        for (var i = 0; i < x.Length; i++)
            for (var j = i + 1; j < x.Length; j++)
                if (x[i] != x[j])
                {
                    var run = x[j] - x[i];
                    var between = (y[j] - y[i]) / run;
                    // A slope past what a double holds would sort to an end and pass as a median or not by
                    // chance, and a run past it reads as a slope of zero, so either is refused by the pair
                    // rather than weighed (spine rule 1).
                    if (!double.IsFinite(run))
                        throw new ArgumentException($"Points {i} and {j} lie too far apart in x for the run between them to be a finite number.");
                    if (!double.IsFinite(between))
                        throw new ArgumentException($"Points {i} and {j} rise too steeply for the slope between them to be a finite number.");
                    slopes.Add(between);
                }
        if (slopes.Count == 0) throw new ArgumentException("Every point has the same x: no slope can be fitted.");
        var slope = Median(slopes);
        var intercept = Median(Enumerable.Range(0, x.Length).Select(i => y[i] - slope * x[i]).ToList());
        double[] c = [intercept, slope];
        return Curve("Theil–Sen", c, x, y, xs, space);
    }

    private static FittedCurve Fitted(IReadOnlyList<double> xs, IReadOnlyList<double> ys, int degree, FitSpace space, string method)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(degree);
        var (x, y) = Prepared(xs, ys, space, degree + 1);
        if (x.Distinct().Count() <= degree)
            throw new ArgumentException($"A polynomial of degree {degree} needs {degree + 1} different x at least.");
        // Centred and scaled x keeps the columns of the design matrix comparable, so degree 9 on
        // [0, 1] is solved as accurately as degree 1; the coefficients are mapped back afterwards.
        var mid = (x.Min() + x.Max()) / 2;
        var half = Math.Max((x.Max() - x.Min()) / 2, double.Epsilon);
        // A span past what a double holds makes every centred x zero, which would read as points that do not
        // determine the polynomial.
        if (!double.IsFinite(mid) || !double.IsFinite(half))
            throw new ArgumentException("The points lie too far apart in x for a polynomial: their span is not a finite number.");
        var a = new double[x.Length, degree + 1];
        for (var i = 0; i < x.Length; i++)
        {
            var t = (x[i] - mid) / half;
            var p = 1d;
            for (var k = 0; k <= degree; k++, p *= t) a[i, k] = p;
        }
        var scaled = SolveLeastSquares(a, y);
        var c = Unscale(scaled, mid, half);
        Kept(scaled, c, x, y, mid, half, degree, space);
        return Curve(method, c, x, y, xs, space, leastSquares: true);
    }

    // Coefficients in powers of x far from zero cancel one another, and past a degree they no longer say the
    // curve that was fitted in centred x: the fitted values at the points are compared, and a curve that has
    // lost them is refused, never handed back as plausible coefficients (spine rule 1). A millionth of the
    // values' spread is a loss a reader could see; a few dozen units in the last place of the largest value
    // is rounding the values themselves carry, so a line through large values close together still fits.
    private static void Kept(double[] scaled, double[] c, double[] x, double[] y, double mid, double half, int degree, FitSpace space)
    {
        var tolerance = Math.Max(1e-6 * (y.Max() - y.Min()), 64 * Math.ScaleB(1d, -52) * y.Max(v => Math.Abs(v)));
        if (!double.IsFinite(tolerance)) throw Overflow();
        for (var i = 0; i < x.Length; i++)
        {
            double inT = 0, inX = 0;
            var t = (x[i] - mid) / half;
            for (var k = c.Length - 1; k >= 0; k--)
            {
                inT = inT * t + scaled[k];
                inX = inX * x[i] + c[k];
            }
            if (!double.IsFinite(inT)) throw Overflow();
            // Powers of x past what a double holds, where the centred curve is finite, are the same loss.
            if (!double.IsFinite(inX) || !(Math.Abs(inT - inX) <= tolerance))
                throw new ArgumentException(space == FitSpace.LogLog
                    // On ln x, dividing x by a constant is what moves the centre to zero.
                    ? $"A polynomial of degree {degree} on ln x near {R(mid)} cannot be written in powers of ln x without losing its fit; fit it on x ÷ {R(Math.Exp(mid))} instead."
                    : $"A polynomial of degree {degree} on x near {R(mid)} cannot be written in powers of x without losing its fit; fit it on x − {R(mid)} instead.");
        }
    }

    private static FittedCurve Curve(string method, double[] c, double[] x, double[] y, IReadOnlyList<double> xs, FitSpace space, bool leastSquares = false)
    {
        var mean = y.Average();
        double res = 0, tot = 0;
        for (var i = 0; i < x.Length; i++)
        {
            var f = 0d;
            for (var k = c.Length - 1; k >= 0; k--) f = f * x[i] + c[k];
            res += (y[i] - f) * (y[i] - f);
            tot += (y[i] - mean) * (y[i] - mean);
        }
        // Sums past what a double holds would read as an R² of 1 or, clamped, of 0.
        if (c.Any(v => !double.IsFinite(v)) || !double.IsFinite(res) || !double.IsFinite(tot)) throw Overflow();
        var r2 = tot == 0 ? (res == 0 ? 1 : 0) : 1 - res / tot;
        // Least squares with a constant does no worse than the mean, so its R² is never below zero: a value a
        // hair below is rounding, which would read "R² = -0.00", and one further below is a solve that failed,
        // which is refused rather than shown as 0.00 (Theil–Sen's may truly be below).
        if (leastSquares && r2 < 0)
        {
            if (r2 < -1e-9)
                throw new ArgumentException("The least-squares solve failed at these values: the fit does worse than their mean.");
            r2 = 0;
        }
        return new FittedCurve(method, c, r2, x.Length, xs.Min(), xs.Max(), space);
    }

    private static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

    private static ArgumentException Overflow() =>
        new("The fit overflows at these values: its coefficients or its R² are not finite numbers.");

    // The points in the space the fit is made in: ln y, and ln x for a power law. A value a logarithm
    // cannot take is refused by name, never dropped (ADR-0290).
    private static (double[] X, double[] Y) Prepared(IReadOnlyList<double> xs, IReadOnlyList<double> ys, FitSpace space, int least)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);
        if (xs.Count != ys.Count) throw new ArgumentException("Each x needs one y.");
        if (xs.Count < least) throw new ArgumentException($"The method needs {least} points at least.");
        var x = new double[xs.Count];
        var y = new double[ys.Count];
        for (var i = 0; i < xs.Count; i++)
        {
            if (!double.IsFinite(xs[i]) || !double.IsFinite(ys[i])) throw new ArgumentException($"Point {i} is not a finite number.");
            if (space != FitSpace.Linear && !(ys[i] > 0)) throw new ArgumentException($"Point {i} has y = {R(ys[i])}, whose logarithm a fit on ln y cannot take.");
            if (space == FitSpace.LogLog && !(xs[i] > 0)) throw new ArgumentException($"Point {i} has x = {R(xs[i])}, whose logarithm a fit on ln x cannot take.");
            x[i] = space == FitSpace.LogLog ? Math.Log(xs[i]) : xs[i];
            y[i] = space == FitSpace.Linear ? ys[i] : Math.Log(ys[i]);
        }
        return (x, y);
    }

    // min ‖Ac − b‖ by Householder QR, then back substitution.
    private static double[] SolveLeastSquares(double[,] a, double[] b)
    {
        int m = a.GetLength(0), n = a.GetLength(1);
        var r = (double[,])a.Clone();
        var q = (double[])b.Clone();
        for (var k = 0; k < n; k++)
        {
            var norm = 0d;
            for (var i = k; i < m; i++) norm += r[i, k] * r[i, k];
            norm = Math.Sqrt(norm);
            if (norm == 0) throw new ArgumentException("The points do not determine the polynomial.");
            var alpha = r[k, k] > 0 ? -norm : norm;
            var v = new double[m];
            v[k] = r[k, k] - alpha;
            for (var i = k + 1; i < m; i++) v[i] = r[i, k];
            var vv = 0d;
            for (var i = k; i < m; i++) vv += v[i] * v[i];
            if (vv == 0) continue;
            for (var j = k; j < n; j++)
            {
                var d = 0d;
                for (var i = k; i < m; i++) d += v[i] * r[i, j];
                d = 2 * d / vv;
                for (var i = k; i < m; i++) r[i, j] -= d * v[i];
            }
            var e = 0d;
            for (var i = k; i < m; i++) e += v[i] * q[i];
            e = 2 * e / vv;
            for (var i = k; i < m; i++) q[i] -= e * v[i];
        }
        var c = new double[n];
        for (var k = n - 1; k >= 0; k--)
        {
            var s = q[k];
            for (var j = k + 1; j < n; j++) s -= r[k, j] * c[j];
            c[k] = s / r[k, k];
        }
        return c;
    }

    // Coefficients in t = (x − mid) / half mapped back to coefficients in x.
    private static double[] Unscale(double[] scaled, double mid, double half)
    {
        var n = scaled.Length;
        var c = new double[n];
        // Σ sₖ ((x − mid)/half)^k, expanded by the binomial theorem.
        for (var k = 0; k < n; k++)
        {
            var coeff = scaled[k] / Math.Pow(half, k);
            var binom = 1d;
            for (var j = 0; j <= k; j++)
            {
                c[j] += coeff * binom * Math.Pow(-mid, k - j);
                binom = binom * (k - j) / (j + 1);
            }
        }
        return c;
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var n = values.Count;
        return n % 2 == 1 ? values[n / 2] : (values[n / 2 - 1] + values[n / 2]) / 2;
    }
}
