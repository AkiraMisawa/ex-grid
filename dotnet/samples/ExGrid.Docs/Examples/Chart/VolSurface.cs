using System.Globalization;
using ExChart.Engine;
using ExChart.Engine.Interpolation;

namespace ExGrid.Docs.Examples.Chart;

/// <summary>A fixed FX volatility surface for the 3D Examples: five deltas by eight tenors, one quote missing.</summary>
public static class VolSurface
{
    /// <summary>The deltas across, as quoted.</summary>
    public static readonly string[] Deltas = ["10P", "25P", "ATM", "25C", "10C"];

    /// <summary>The tenors along, as quoted.</summary>
    public static readonly string[] Tenors = ["1W", "1M", "3M", "6M", "1Y", "2Y", "5Y", "10Y"];

    // Fixed sample quotes, in percent. 25C at 5Y has no quote, and is drawn as a hole.
    private static readonly double?[][] Quotes =
    [
        [12.89, 11.79, 11.29, 11.59, 12.39],
        [12.28, 11.17, 10.66, 10.97, 11.78],
        [11.82, 10.70, 10.18, 10.49, 11.32],
        [11.44, 10.31, 9.78, 10.10, 10.94],
        [11.22, 10.08, 9.53, 9.86, 10.73],
        [11.08, 9.92, 9.35, 9.69, 10.59],
        [11.62, 10.42, 9.80, null, 11.15],
        [12.82, 11.58, 10.92, 11.32, 12.38],
    ];

    private static string Percent(object v) => ((double)v).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    /// <summary>The quotes as a surface: tenors along, deltas across.</summary>
    public static SurfaceDefinition Quoted() =>
        new(SurfaceAxis.Of("Delta", Deltas), SurfaceAxis.Of("Tenor", Tenors),
            Quotes.Select(row => (IReadOnlyList<ChartValue?>)row.Select(v => v is { } x ? (ChartValue?)ChartValue.Of(x) : null).ToArray()).ToArray(),
            "Volatility", Percent);

    /// <summary>The quotes as a capability would return them for a Chart Spec: a row a tenor and delta, the missing quote null.</summary>
    public static IChartData Table()
    {
        var rows = Tenors.SelectMany((tenor, j) => Deltas.Select((delta, i) => (tenor, delta, Quotes[j][i]))).ToArray();
        return global::ExChart.Engine.ChartData.Of(
            ("tenor", rows.Select(r => r.tenor).ToArray()),
            ("delta", rows.Select(r => r.delta).ToArray()),
            ("vol", rows.Select(r => (object?)r.Item3).ToArray()));
    }

    private static readonly double[] DeltaValues = [10, 25, 50, 75, 90];
    private static readonly double[] TenorYears = [7 / 365.0, 1 / 12.0, 0.25, 0.5, 1, 2, 5, 10];

    /// <summary>
    /// The same quotes interpolated bilinearly by the page, with ExChart.Engine's Interpolation Helper,
    /// and handed in as a function the surface samples. The method is the page's, and is said as such.
    /// </summary>
    public static SurfaceDefinition Bilinear() =>
        SurfaceDefinition.Sampled(Interpolate.Bilinear(DeltaValues, TenorYears, Quotes), "Delta", "Years", "Volatility", samples: 13);
}
