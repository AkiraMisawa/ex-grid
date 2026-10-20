using ExChart.Engine;
using ExChart.Engine.Fitting;

namespace ExChart.Engine.Tests;

public class FittingTests
{
    [Fact] // ADR-0291: least squares recovers an exact line, and its Note names method, equation, R² and n
    public void Least_squares_recovers_a_line_and_says_it()
    {
        double[] xs = [1, 2, 3, 4];
        var fit = Fit.LeastSquares(xs, xs.Select(x => 41.2 * x + 88.5).ToArray());
        Assert.Equal(41.2, fit.Slope, 9);
        Assert.Equal(88.5, fit.Intercept, 9);
        Assert.Equal(1, fit.RSquared, 12);
        Assert.Equal("OLS: y = 41.2x + 88.5, R² = 1.00, n = 4", fit.Note);
    }

    [Fact] // ADR-0291: one point far off drags least squares and not Theil–Sen
    public void Theil_sen_is_not_dragged_by_one_point()
    {
        var xs = Enumerable.Range(1, 20).Select(i => (double)i).ToArray();
        var ys = xs.Select(x => 2 * x + 1).ToArray();
        ys[19] = 400;
        var ols = Fit.LeastSquares(xs, ys);
        var ts = Fit.TheilSen(xs, ys);
        Assert.Equal(2, ts.Slope, 9);
        Assert.Equal(1, ts.Intercept, 9);
        Assert.True(ols.Slope > 5);
        Assert.StartsWith("Theil–Sen: y = 2x + 1", ts.Note);
    }

    [Fact] // ADR-0291: a fitted line is drawn over the range it was fitted on, and never beyond
    public void A_fitted_line_has_no_value_beyond_its_points()
    {
        var fit = Fit.LeastSquares([2, 3, 5], [1, 2, 4]);
        Assert.Null(fit.ValueAt(1.9));
        Assert.Null(fit.ValueAt(5.1));
        Assert.NotNull(fit.ValueAt(4));
        var s = fit.Series("fit", "OLS");
        Assert.Equal(2, s.Sampler!.From);
        Assert.Equal(5, s.Sampler.To);
        Assert.False(s.ReachesZero);
    }

    [Fact] // ADR-0291: the fit's Note reaches the chart's Notes through its series
    public void The_fits_note_reaches_the_charts_notes()
    {
        var fit = Fit.LeastSquares([1, 2, 3], [2, 4, 7]);
        var d = new ChartDefinition(ChartAxis.Numbers(),
        [
            ChartSeries.Of("o", "Names", SeriesKind.Observation, [new ChartPoint(1, ChartValue.Of(2d)), new ChartPoint(2, ChartValue.Of(4d)), new ChartPoint(3, ChartValue.Of(7d))]),
            fit.Series("fit", "OLS"),
        ]);
        var scene = ChartScene.Build(d, new ChartMetrics(800, 400));
        Assert.Contains(scene.Notes, n => n.Contains(fit.Note));
    }

    [Fact] // ADR-0291: a power law fitted on ln y vs ln x names its space and is evaluated back in y
    public void A_fit_in_log_space_names_it()
    {
        double[] xs = [1, 2, 4, 8];
        var fit = Fit.LeastSquares(xs, xs.Select(x => 3 * Math.Pow(x, 0.5)).ToArray(), FitSpace.LogLog);
        Assert.Equal(0.5, fit.Slope, 9);
        Assert.Equal(3 * Math.Sqrt(5), fit.ValueAt(5)!.Value.ToDouble(), 9);
        Assert.StartsWith("OLS on ln y vs ln x: ln y = 0.5ln x +", fit.Note);
        Assert.Throws<ArgumentException>(() => Fit.LeastSquares([1, 2], [0, 1], FitSpace.LogY));
    }

    [Fact] // ADR-0291: a polynomial of degree M by least squares; degree 9 through ten points passes through every one (PRML §1.1)
    public void A_polynomial_of_degree_nine_passes_through_ten_points()
    {
        var xs = Enumerable.Range(0, 10).Select(i => i / 9.0).ToArray();
        var rng = new Random(1);
        var ys = xs.Select(x => Math.Sin(2 * Math.PI * x) + 0.3 * (rng.NextDouble() - 0.5)).ToArray();
        var m9 = Fit.Polynomial(xs, ys, 9);
        Assert.Equal(9, m9.Degree);
        for (var i = 0; i < xs.Length; i++) Assert.Equal(ys[i], m9.ValueAt(xs[i])!.Value.ToDouble(), 6);
        Assert.Equal(1, m9.RSquared, 6);
        Assert.Matches(@"^Least squares: degree 9, R² = 1\.00, n = 10$", m9.Note);
        var m3 = Fit.Polynomial(xs, ys, 3);
        Assert.InRange(m3.RSquared, 0.8, 0.999);
        Assert.Throws<ArgumentException>(() => Fit.Polynomial(xs.Take(3).ToArray(), ys.Take(3).ToArray(), 3));
    }

    [Fact] // ADR-0291: a point that is not a finite number is refused, never dropped
    public void A_point_that_is_not_finite_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Fit.LeastSquares([1, 2, double.NaN], [1, 2, 3]));
        Assert.Throws<ArgumentException>(() => Fit.TheilSen([1, 1], [1, 2]));
    }
}
