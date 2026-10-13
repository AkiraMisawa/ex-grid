namespace ExposureChart;

/// <summary>
/// One pillar of a CDS curve: a quoted par spread and what the bootstrap made of it. The first row
/// is the curve's date, where nothing is quoted and no hazard holds yet — the interval the first
/// hazard rate covers starts there. Every figure is invented.
/// </summary>
public sealed record CdsRow(
    string Tenor, DateOnly Maturity,
    decimal? SpreadBp,
    decimal? HazardPct, decimal SurvivalPct, decimal DefaultPct);

public static class CdsData
{
    public static readonly DateOnly AsOf = new(2026, 10, 13);
    public const double Recovery = 0.40, Rate = 0.03;

    // An investment-grade name: an upward-sloping curve. Maturities on the standard CDS roll: after
    // the 20 September roll, a tenor runs to 20 December (20 June for 6M).
    private static readonly (string Tenor, DateOnly Maturity, double SpreadBp)[] Quotes =
    [
        ("6M", new(2027, 6, 20), 42), ("1Y", new(2027, 12, 20), 50), ("2Y", new(2028, 12, 20), 66),
        ("3Y", new(2029, 12, 20), 84), ("4Y", new(2030, 12, 20), 101), ("5Y", new(2031, 12, 20), 116),
        ("7Y", new(2033, 12, 20), 134), ("10Y", new(2036, 12, 20), 148),
    ];

    /// <param name="shiftBp">A parallel shift of every quote, for "the quotes moved".</param>
    public static IReadOnlyList<CdsRow> Curve(double shiftBp = 0)
    {
        var knots = new List<double>();
        var hazards = new List<double>();
        var rows = new List<CdsRow> { new("0", AsOf, null, null, 100m, 0m) };
        foreach (var (tenor, maturity, spreadBp) in Quotes)
        {
            var spread = (spreadBp + shiftBp) / 10_000;
            knots.Add(Years(maturity));
            hazards.Add(0);
            // The hazard over the new interval that prices this maturity's CDS at par, the
            // intervals before it already fixed: the bootstrap.
            double lo = 1e-8, hi = 2;
            for (var i = 0; i < 100; i++)
            {
                hazards[^1] = (lo + hi) / 2;
                var value = spread * RiskyAnnuity(maturity, knots, hazards) - (1 - Recovery) * Protection(Years(maturity), knots, hazards);
                if (value > 0) lo = hazards[^1]; else hi = hazards[^1];
            }
            var survival = Survival(Years(maturity), knots, hazards);
            rows.Add(new CdsRow(tenor, maturity, Round(spreadBp + shiftBp, 2),
                Round(hazards[^1] * 100, 4), Round(survival * 100, 4), Round((1 - survival) * 100, 4)));
        }
        return rows;
    }

    private static double Years(DateOnly d) => (d.DayNumber - AsOf.DayNumber) / 365.0;
    private static double Discount(double t) => Math.Exp(-Rate * t);

    // The hazard holds on (tᵢ₋₁, tᵢ]; past the last knot the last one goes on.
    private static double Survival(double t, IReadOnlyList<double> knots, IReadOnlyList<double> hazards)
    {
        double integral = 0, from = 0;
        for (var i = 0; i < knots.Count && from < t; i++)
        {
            var to = Math.Min(t, knots[i]);
            integral += hazards[i] * (to - from);
            from = to;
        }
        if (from < t) integral += hazards[^1] * (t - from);
        return Math.Exp(-integral);
    }

    // (1 − R) aside: ∫₀ᵀ DF(t) dQ(t), exact on each interval of constant hazard:
    // λ/(r+λ) · (1 − e^{−(r+λ)Δ}) · DF(a) · S(a).
    private static double Protection(double maturity, IReadOnlyList<double> knots, IReadOnlyList<double> hazards)
    {
        double total = 0, from = 0;
        for (var i = 0; i < knots.Count && from < maturity; i++)
        {
            var to = Math.Min(maturity, knots[i]);
            var l = hazards[i];
            total += l / (Rate + l) * (1 - Math.Exp(-(Rate + l) * (to - from))) * Discount(from) * Survival(from, knots, hazards);
            from = to;
        }
        return total;
    }

    // The premium leg per unit of spread: quarterly on the IMM dates, ACT/360, with half a period's
    // accrual paid on default. The first coupon accrues from the last roll, but the buyer is paid
    // back what accrued before today, so the leg's value counts from today.
    private static double RiskyAnnuity(DateOnly maturity, IReadOnlyList<double> knots, IReadOnlyList<double> hazards)
    {
        double total = 0;
        var start = new DateOnly(2026, 9, 20);
        for (var end = start.AddMonths(3); end <= maturity; start = end, end = end.AddMonths(3))
        {
            var accrual = (end.DayNumber - Math.Max(start.DayNumber, AsOf.DayNumber)) / 360.0;
            var (s0, s1) = (Survival(Math.Max(0, Years(start)), knots, hazards), Survival(Years(end), knots, hazards));
            total += accrual * Discount(Years(end)) * (s1 + (s0 - s1) / 2);
        }
        return total;
    }

    private static decimal Round(double value, int digits) => Math.Round((decimal)value, digits);
}
