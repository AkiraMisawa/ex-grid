namespace ExposureChart.Analytics;

/// <summary>
/// A reference entity's CDS curve as a trader's screen shows it: one row, its tenors across, and
/// beside the quotes the hazard rates bootstrapped from them with the row's own recovery. Every
/// name and figure is invented.
/// </summary>
public sealed record CdsCurveRow(
    string Ticker, string Entity, string Ccy, string Seniority, string DocClause, decimal RecoveryPct,
    IReadOnlyList<decimal?> Spreads, IReadOnlyList<decimal?> Hazards)
{
    /// <summary>What tells two curves apart: the same entity trades senior and subordinated, in two currencies.</summary>
    public string Key => $"{Ticker} {Seniority} {Ccy} {DocClause}";
}

public static class CdsCurves
{
    public static readonly string[] TenorNames = ["6m", "1y", "2y", "3y", "4y", "5y", "7y", "10y", "15y", "20y"];
    public static readonly double[] TenorYears = [0.5, 1, 2, 3, 4, 5, 7, 10, 15, 20];
    private const double Rate = 0.03;

    private static readonly (string Ticker, string Entity, string Ccy, string Seniority, string Doc, double Recovery, double[] Bp)[] Quotes =
    [
        ("ACMEC", "Acme Chemicals Corp", "USD", "SNRFOR", "XR14", 40, [18, 24, 35, 48, 60, 72, 88, 101, 112, 118]),
        ("BOREAL", "Boreal Utilities plc", "EUR", "SNRFOR", "MM14", 40, [12, 16, 24, 33, 42, 50, 62, 74, 84, 90]),
        ("CYGNUS", "Cygnus Holdings Inc", "USD", "SNRFOR", "XR14", 40, [210, 245, 290, 325, 350, 370, 385, 395, 400, 402]),
        ("CYGNUS", "Cygnus Holdings Inc", "USD", "SUBLT2", "XR14", 20, [340, 390, 450, 495, 525, 545, 560, 570, 574, 576]),
        ("DELTAP", "Delta Petroleum SA", "EUR", "SNRFOR", "MM14", 40, [980, 900, 800, 735, 690, 660, 625, 590, 570, 560]),
        ("KAEDE", "Kaede Heavy Industries KK", "JPY", "SNRFOR", "CR14", 35, [8, 11, 17, 24, 31, 38, 49, 60, 70, 75]),
    ];

    public static IReadOnlyList<CdsCurveRow> All(double shiftBp = 0) =>
        Quotes.Select(q =>
        {
            var spreads = q.Bp.Select(b => b + shiftBp).ToArray();
            var hazards = Bootstrap(spreads, q.Recovery / 100);
            return new CdsCurveRow(q.Ticker, q.Entity, q.Ccy, q.Seniority, q.Doc, (decimal)q.Recovery,
                spreads.Select(s => (decimal?)Math.Round((decimal)s, 2)).ToArray(),
                hazards.Select(h => h is { } v ? (decimal?)Math.Round((decimal)(v * 100), 4) : null).ToArray());
        }).ToList();

    /// <summary>
    /// Piecewise-constant hazard rates, each over (tᵢ₋₁, tᵢ] from t₀ = 0, pricing each tenor's CDS
    /// at par with the earlier ones fixed: quarterly premiums (ACT/360 on a 365-day year), half a
    /// period's accrual on default, the protection leg exact on each interval. Where no positive
    /// rate prices a quote, the rate is not invented: it and every later one are null.
    /// </summary>
    public static IReadOnlyList<double?> Bootstrap(IReadOnlyList<double> spreadsBp, double recovery)
    {
        var knots = new List<double>();
        var hazards = new List<double>();
        var result = new List<double?>();
        for (var i = 0; i < spreadsBp.Count; i++)
        {
            if (result.Count > 0 && result[^1] is null) { result.Add(null); continue; }
            var spread = spreadsBp[i] / 10_000;
            knots.Add(TenorYears[i]);
            hazards.Add(0);
            double Value(double h) { hazards[^1] = h; return spread * Annuity(TenorYears[i], knots, hazards) - (1 - recovery) * Protection(TenorYears[i], knots, hazards); }
            double lo = 1e-10, hi = 5;
            if (Value(lo) <= 0 || Value(hi) >= 0) { result.Add(null); continue; }
            for (var k = 0; k < 100; k++) { var mid = (lo + hi) / 2; if (Value(mid) > 0) lo = mid; else hi = mid; }
            hazards[^1] = (lo + hi) / 2;
            result.Add(hazards[^1]);
        }
        return result;
    }

    private static double Survival(double t, List<double> knots, List<double> hazards)
    {
        double integral = 0, from = 0;
        for (var i = 0; i < knots.Count && from < t; i++)
        {
            var to = Math.Min(t, knots[i]);
            integral += hazards[i] * (to - from);
            from = to;
        }
        return Math.Exp(-integral);
    }

    private static double Protection(double maturity, List<double> knots, List<double> hazards)
    {
        double total = 0, from = 0;
        for (var i = 0; i < knots.Count && from < maturity; i++)
        {
            var to = Math.Min(maturity, knots[i]);
            var l = hazards[i];
            total += l / (Rate + l) * (1 - Math.Exp(-(Rate + l) * (to - from))) * Math.Exp(-Rate * from) * Survival(from, knots, hazards);
            from = to;
        }
        return total;
    }

    private static double Annuity(double maturity, List<double> knots, List<double> hazards)
    {
        double total = 0;
        for (var end = 0.25; end <= maturity + 1e-9; end += 0.25)
        {
            var (s0, s1) = (Survival(end - 0.25, knots, hazards), Survival(end, knots, hazards));
            total += 0.25 * 365 / 360 * Math.Exp(-Rate * end) * (s1 + (s0 - s1) / 2);
        }
        return total;
    }
}
