namespace ExposureChart;

/// <summary>
/// One date of a netting set's exposure profile, before and after a proposed trade. Every figure
/// is invented: a 10Y amortising swap's hump, an ENE mirrored below zero, and the CVA each
/// date's bucket contributes.
/// </summary>
public sealed record ExposureRow(
    DateOnly Date,
    decimal EpeBefore, decimal EpeAfter, decimal EpeDiff,
    decimal EneBefore, decimal EneAfter, decimal EneDiff,
    decimal CvaBefore, decimal CvaAfter, decimal CvaDiff,
    decimal PfeBefore, decimal PfeAfter, decimal PfeDiff);

public static class ExposureData
{
    public static readonly DateOnly AsOf = new(2026, 10, 13);

    /// <summary>
    /// The tenor grid a risk engine would report: monthly to 2Y, quarterly to 5Y, half-yearly to
    /// 10Y and yearly to 30Y. Uneven on purpose — on a category axis it would look evenly spaced
    /// and the hump would sit in the wrong place.
    /// </summary>
    public static IReadOnlyList<DateOnly> TenorGrid()
    {
        var dates = new List<DateOnly> { AsOf };
        for (var m = 1; m <= 24; m++) dates.Add(AsOf.AddMonths(m));
        for (var m = 27; m <= 60; m += 3) dates.Add(AsOf.AddMonths(m));
        for (var m = 66; m <= 120; m += 6) dates.Add(AsOf.AddMonths(m));
        for (var y = 11; y <= 30; y++) dates.Add(AsOf.AddYears(y));
        return dates;
    }

    /// <param name="afterScale">How strongly the proposed trade offsets the book: 1 is the
    /// proposal as booked, more is a larger hedge.</param>
    public static IReadOnlyList<ExposureRow> Profile(double afterScale = 1.0)
    {
        var rows = new List<ExposureRow>();
        DateOnly? previous = null;
        foreach (var date in TenorGrid())
        {
            var t = (date.DayNumber - AsOf.DayNumber) / 365.25;
            var dt = previous is { } p ? (date.DayNumber - p.DayNumber) / 365.25 : 0;
            previous = date;

            // Before: a 10Y amortising receiver plus a 30Y tail. After: a 7Y hedge cuts the hump.
            var epeBefore = Hump(t, 18_000_000, 10) + Hump(t, 4_000_000, 30);
            var eneBefore = -(Hump(t, 11_000_000, 10) + Hump(t, 2_500_000, 30));
            var hedge = afterScale * Hump(t, 7_500_000, 7);
            var epeAfter = Math.Max(0, epeBefore - hedge);
            var eneAfter = eneBefore - 0.35 * hedge;

            // CVA of the bucket ending at this date: LGD x EPE x default probability x discount.
            const double lgd = 0.6, hazard = 0.012, rate = 0.03;
            var weight = lgd * hazard * Math.Exp(-(hazard + rate) * t) * dt;
            var cvaBefore = -epeBefore * weight;
            var cvaAfter = -epeAfter * weight;

            // PFE at 97.5%: a tail well above the mean, cut less by the hedge than EPE is.
            var pfeBefore = 2.6 * epeBefore;
            var pfeAfter = Math.Max(0, pfeBefore - 1.8 * hedge);

            rows.Add(new ExposureRow(
                date,
                Money(epeBefore), Money(epeAfter), Money(epeAfter) - Money(epeBefore),
                Money(eneBefore), Money(eneAfter), Money(eneAfter) - Money(eneBefore),
                Money(cvaBefore), Money(cvaAfter), Money(cvaAfter) - Money(cvaBefore),
                Money(pfeBefore), Money(pfeAfter), Money(pfeAfter) - Money(pfeBefore)));
        }
        return rows;
    }

    // Rises like sqrt(t) and amortises to nothing at maturity.
    private static double Hump(double t, double peak, double maturity) =>
        t <= 0 || t >= maturity ? 0 : peak * 1.54 * Math.Sqrt(t / maturity) * (1 - t / maturity);

    private static decimal Money(double value) => Math.Round((decimal)value, 2);
}
