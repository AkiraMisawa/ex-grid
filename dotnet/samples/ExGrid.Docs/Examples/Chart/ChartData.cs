using System.Globalization;
using ExChart.Engine;

namespace ExGrid.Docs.Examples.Chart;

/// <summary>Fixed sample values the chart Examples draw. Nothing here is computed from a model: an application hands its own.</summary>
public static class ChartData
{
    private static readonly DateOnly Start = new(2026, 10, 30);

    private static string Money(object v) => Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("N2", CultureInfo.InvariantCulture);

    private static string Percent(object v) => Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("P2", CultureInfo.InvariantCulture);

    /// <summary>An exposure profile: a hump of expected exposure over ten years, monthly, before and after a trade.</summary>
    public static IReadOnlyList<ChartSeries> Exposure()
    {
        var dates = Enumerable.Range(0, 121).Select(m => Start.AddMonths(m)).ToList();
        decimal Hump(int m, double scale) =>
            (decimal)Math.Round(scale * Math.Sin(Math.PI * m / 120.0) * (1 + 0.6 * Math.Exp(-Math.Pow((m - 36) / 18.0, 2))), 2);
        ChartSeries Series(string id, string name, string key, double scale, bool negative = false) =>
            ChartSeries.Of(id, name, SeriesKind.Level,
                dates.Select((d, m) => new ChartPoint(d.DayNumber, ChartValue.Of(negative ? -Hump(m, scale) : Hump(m, scale)))))
            with { Keys = new AppearanceKeys(key), Format = Money };
        return
        [
            Series("epe-before", "EPE · Before", "Before", 10),
            Series("epe-after", "EPE · After", "After", 7),
            Series("ene-before", "ENE · Before", "Before", 6, negative: true) with { Panel = "ENE" },
            Series("ene-after", "ENE · After", "After", 4, negative: true) with { Panel = "ENE" },
        ];
    }

    /// <summary>The tenors of a curve, in years, and their names on the axis.</summary>
    public static readonly (double Years, string Name)[] Tenors =
        [(0.5, "6M"), (1, "1Y"), (2, "2Y"), (3, "3Y"), (5, "5Y"), (7, "7Y"), (10, "10Y")];

    /// <summary>An axis of tenors in years, labelled with their names.</summary>
    public static ChartAxis TenorAxis() => ChartAxis.Numbers("Tenor") with { Named = [.. Tenors] };

    /// <summary>A hazard rate, which holds over each interval up to its tenor: drawn as steps.</summary>
    public static ChartSeries Hazard() =>
        ChartSeries.Of("hazard", "Hazard rate", SeriesKind.RateOverInterval,
            [new(0, null), new(0.5, 0.0110m), new(1, 0.0125m), new(2, 0.0150m), new(3, 0.0170m), new(5, 0.0190m), new(7, 0.0205m), new(10, 0.0210m)])
        with { Format = Percent };

    /// <summary>Zero rates quoted at the tenors, a 3Y quote missing.</summary>
    public static ChartSeries Quotes() =>
        ChartSeries.Of("zero-quoted", "Zero rate", SeriesKind.Level,
            [new(0.5, 0.0301m), new(1, 0.0312m), new(2, 0.0325m), new(3, null), new(5, 0.0342m), new(7, 0.0351m), new(10, 0.0360m)])
        with { Keys = new AppearanceKeys("Zero", null, "circle"), Format = Percent, FromZero = false };

    /// <summary>
    /// The same curve as the application's analytics evaluated it, every month: declared Interpolated, with
    /// the method of each Segment. The values are the analytics'; the chart only draws them.
    /// </summary>
    public static ChartSeries Curve() =>
        ChartSeries.Of("zero-curve", "Zero rate", SeriesKind.Level,
            Enumerable.Range(6, 115).Select(m => m / 12.0).Select(t => new ChartPoint(t, ChartValue.Of(Math.Round(0.0290 + 0.0021 * Math.Log(1 + t * 3), 5)))))
        with
        {
            Keys = new AppearanceKeys("Zero"),
            Format = Percent,
            FromZero = false,
            Interpolated = true,
            Segments = [new Segment(0.5, 2, "linear on zero rates"), new Segment(2, 10, "log-linear on discount factors")],
        };

    /// <summary>CVA by bucket, the buckets widening from quarters to years: an amount per period, drawn per year.</summary>
    public static ChartSeries CvaByBucket() =>
        ChartSeries.Of("cva", "CVA", SeriesKind.AmountPerPeriod,
            [new(0, null), new(0.25, 300m), new(0.5, 290m), new(1, 560m), new(2, 1_050m), new(3, 980m), new(5, 1_800m), new(10, 3_900m)])
        with { Format = Money };
}
