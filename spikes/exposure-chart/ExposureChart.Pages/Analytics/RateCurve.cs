namespace ExposureChart.Analytics;

/// <summary>
/// An interest-rate curve as an analytics service builds it: market quotes — deposits, then par
/// swaps — bootstrapped into discount factors at each instrument's maturity, with zero rates and
/// forwards read from them. Every quote is invented; a curve is a deterministic function of its
/// name and date, so a date in the past can be compared with today's.
/// </summary>
public static class RateCurve
{
    public static readonly string[] Curves = ["USD OIS", "EUR OIS", "JPY OIS"];
    public static readonly string[] Interpolations = ["Log-linear discount factors", "Linear zero rates"];

    public enum Kind { Deposit, Swap }

    /// <summary>A quoted instrument: its type, tenor and maturity, and its rate at the bid and ask.</summary>
    public sealed record Quote(Kind Kind, string Tenor, double Years, DateOnly Maturity, double Bid, double Ask)
    {
        public double Mid => (Bid + Ask) / 2;
        public string Instrument => Kind == Kind.Deposit ? "Deposit" : "Swap";
    }

    /// <summary>A pillar of the built curve, at an instrument's maturity.</summary>
    /// <param name="ImpliedQuote">The instrument repriced off the built curve: equal to its quote when the curve fits it.</param>
    public sealed record Pillar(
        Quote Quote, double Years, double Discount, double ZeroContinuous, double ZeroAnnual, double Forward1Y, double ImpliedQuote);

    private static readonly (string Tenor, double Years, Kind Kind)[] Instruments =
    [
        ("ON", 1 / 360.0, Kind.Deposit), ("1W", 7 / 360.0, Kind.Deposit), ("1M", 1 / 12.0, Kind.Deposit),
        ("2M", 2 / 12.0, Kind.Deposit), ("3M", 0.25, Kind.Deposit), ("6M", 0.5, Kind.Deposit),
        ("1Y", 1, Kind.Swap), ("2Y", 2, Kind.Swap), ("3Y", 3, Kind.Swap), ("4Y", 4, Kind.Swap), ("5Y", 5, Kind.Swap),
        ("7Y", 7, Kind.Swap), ("10Y", 10, Kind.Swap), ("12Y", 12, Kind.Swap), ("15Y", 15, Kind.Swap),
        ("20Y", 20, Kind.Swap), ("25Y", 25, Kind.Swap), ("30Y", 30, Kind.Swap), ("40Y", 40, Kind.Swap), ("50Y", 50, Kind.Swap),
    ];

    // A short rate, a long rate and a hump: the shape of each curve, and how a date moves it.
    private static (double Short, double Long, double Hump) Shape(string curve) => curve switch
    {
        "EUR OIS" => (0.0195, 0.0255, 0.0030),
        "JPY OIS" => (0.0048, 0.0215, 0.0010),
        _ => (0.0430, 0.0395, -0.0060),
    };

    public static IReadOnlyList<Quote> Quotes(string curve, DateOnly asOf)
    {
        var (s, l, h) = Shape(curve);
        var day = asOf.DayNumber;
        var level = 0.0012 * Math.Sin(day / 11.0);
        var twist = 0.0008 * Math.Sin(day / 7.0);
        return [.. Instruments.Select(i =>
        {
            var t = i.Years;
            var rate = l + (s - l) * Math.Exp(-t / 3) + h * t / 3 * Math.Exp(-t / 3) + level + twist * (1 - Math.Exp(-t / 10));
            var half = (i.Kind == Kind.Deposit ? 0.0002 : 0.00015) / 2;
            return new Quote(i.Kind, i.Tenor, t, Maturity(asOf, i.Tenor, t), Math.Round(rate - half, 6), Math.Round(rate + half, 6));
        })];
    }

    private static DateOnly Maturity(DateOnly asOf, string tenor, double years)
    {
        var date = tenor switch
        {
            "ON" => asOf.AddDays(1),
            "1W" => asOf.AddDays(7),
            _ when years < 1 => asOf.AddMonths((int)Math.Round(years * 12)),
            _ => asOf.AddYears((int)Math.Round(years)),
        };
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        return date;
    }

    /// <summary>
    /// The bootstrap: a deposit gives its maturity's discount factor directly, 1 / (1 + r·τ); a par
    /// swap paying a fixed rate once a year gives its final discount factor once the earlier ones
    /// are known, the coupon dates between pillars read from the curve built so far under the
    /// chosen interpolation, solved by bisection. Year fractions are ACT/360 for the instruments and
    /// years for the curve, so that a quote reprices to itself.
    /// </summary>
    public static IReadOnlyList<Pillar> Build(IReadOnlyList<Quote> quotes, string interpolation = "Log-linear discount factors")
    {
        var times = new List<double> { 0 };
        var dfs = new List<double> { 1 };
        var logLinear = interpolation.StartsWith("Log", StringComparison.Ordinal);
        double Df(double t) => Interpolate(times, dfs, t, logLinear);

        foreach (var q in quotes)
        {
            var t = q.Years;
            double df;
            if (q.Kind == Kind.Deposit) df = 1 / (1 + q.Mid * t * 365 / 360);
            else
            {
                double Value(double guess)
                {
                    times.Add(t); dfs.Add(guess);
                    var annuity = Annuity(t, Df);
                    times.RemoveAt(times.Count - 1); dfs.RemoveAt(dfs.Count - 1);
                    return q.Mid * annuity - (1 - guess);
                }
                double lo = 1e-6, hi = 1.5;
                for (var k = 0; k < 120; k++) { var mid = (lo + hi) / 2; if (Value(mid) > 0) hi = mid; else lo = mid; }
                df = (lo + hi) / 2;
            }
            times.Add(t); dfs.Add(df);
        }

        return [.. quotes.Select(q =>
        {
            var t = q.Years;
            var df = Df(t);
            var zc = -Math.Log(df) / t;
            var za = Math.Pow(df, -1 / t) - 1;
            var forward = Df(t) / Df(t + 1) - 1;
            var implied = q.Kind == Kind.Deposit ? (1 / df - 1) / (t * 365 / 360) : (1 - df) / Annuity(t, Df);
            return new Pillar(q, t, df, zc, za, forward, implied);
        })];
    }

    /// <summary>The curve at any time, read as the pillars are joined: for drawing between them.</summary>
    public static double DiscountAt(IReadOnlyList<Pillar> pillars, double t, string interpolation)
    {
        List<double> times = [0, .. pillars.Select(p => p.Years)];
        List<double> dfs = [1, .. pillars.Select(p => p.Discount)];
        return Interpolate(times, dfs, t, interpolation.StartsWith("Log", StringComparison.Ordinal));
    }

    // A fixed leg paying once a year, the last period short when the tenor is not whole.
    private static double Annuity(double maturity, Func<double, double> df)
    {
        double total = 0, previous = 0;
        for (var end = Math.Min(1, maturity); ; end = Math.Min(end + 1, maturity))
        {
            total += (end - previous) * 365 / 360 * df(end);
            previous = end;
            if (end >= maturity - 1e-9) break;
        }
        return total;
    }

    // Log-linear in the discount factor (piecewise-flat forwards), or linear in the continuous zero
    // rate; flat zero rate beyond the last pillar.
    private static double Interpolate(List<double> times, List<double> dfs, double t, bool logLinear)
    {
        if (t <= 0) return 1;
        var i = times.FindIndex(x => x >= t);
        if (i < 0)
        {
            var (tl, dl) = (times[^1], dfs[^1]);
            return Math.Exp(Math.Log(dl) / tl * t);
        }
        if (Math.Abs(times[i] - t) < 1e-12) return dfs[i];
        var (t0, t1, d0, d1) = (times[i - 1], times[i], dfs[i - 1], dfs[i]);
        var w = (t - t0) / (t1 - t0);
        if (logLinear) return Math.Exp(Math.Log(d0) + w * (Math.Log(d1) - Math.Log(d0)));
        var z0 = t0 == 0 ? -Math.Log(d1) / t1 : -Math.Log(d0) / t0;
        var z1 = -Math.Log(d1) / t1;
        return Math.Exp(-(z0 + w * (z1 - z0)) * t);
    }
}
