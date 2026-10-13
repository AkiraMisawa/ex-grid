namespace ExposureChart.Analytics;

/// <summary>
/// An FX volatility surface as a market data service would hand it over: by expiry and delta, bid
/// and ask, in both quoting forms — at-the-money with risk reversals and butterflies, and the
/// put and call vols they imply — with the deposit rates and forwards beside it. Every figure is
/// invented; a surface is a deterministic function of the pair and the date, so that a date in
/// the past can be compared with today's.
/// </summary>
public static class FxVol
{
    public static readonly string[] Pairs = ["AUDUSD", "EURUSD", "USDJPY"];

    public static readonly (string Name, double Years)[] Expiries =
    [
        ("1D", 1 / 365.0), ("1W", 7 / 365.0), ("2W", 14 / 365.0), ("3W", 21 / 365.0),
        ("1M", 1 / 12.0), ("2M", 2 / 12.0), ("3M", 3 / 12.0), ("4M", 4 / 12.0), ("6M", 0.5), ("9M", 0.75),
        ("1Y", 1), ("18M", 1.5), ("2Y", 2), ("3Y", 3), ("4Y", 4), ("5Y", 5), ("7Y", 7), ("10Y", 10),
    ];

    /// <summary>The deltas quoted either side of at-the-money, in percent.</summary>
    public static readonly int[] Deltas = [45, 35, 25, 15, 10, 5];

    /// <summary>The surface's delta axis, puts on the left: 5P … 45P, ATM, 45C … 5C.</summary>
    public static IReadOnlyList<string> SmileAxis { get; } =
        [.. Deltas.Reverse().Select(d => $"{d}P"), "ATM", .. Deltas.Select(d => $"{d}C")];

    public const string Convention = "Call/put forward delta, without premium adjustment";

    public sealed record Pair(string Name, string Foreign, string Domestic, double Spot,
        double AtmShort, double AtmLong, double Rr25, double Bf25, double ForeignRate, double DomesticRate);

    public static Pair Of(string name) => name switch
    {
        "EURUSD" => new("EURUSD", "EUR", "USD", 1.0850, 7.4, 8.6, -0.55, 0.20, 0.0255, 0.0430),
        "USDJPY" => new("USDJPY", "USD", "JPY", 149.20, 10.6, 11.4, -1.20, 0.34, 0.0430, 0.0050),
        _ => new("AUDUSD", "AUD", "USD", 0.6650, 17.4, 11.0, -2.30, 0.30, 0.0410, 0.0430),
    };

    /// <summary>A quote: bid and ask, in vol points.</summary>
    public readonly record struct Quote(double Bid, double Ask)
    {
        public double Mid => (Bid + Ask) / 2;
        public double Spread => Ask - Bid;
    }

    /// <summary>One expiry of the surface, in both forms.</summary>
    public sealed record Row(
        string Expiry, double Years, DateOnly ExpiryDate, Quote Atm,
        IReadOnlyDictionary<int, Quote> Calls, IReadOnlyDictionary<int, Quote> Puts,
        IReadOnlyDictionary<int, Quote> RiskReversals, IReadOnlyDictionary<int, Quote> Butterflies,
        double DepositForeign, double DepositDomestic, double Forward);

    // How a risk reversal and a butterfly grow away from the money, by delta.
    private static readonly Dictionary<int, (double Rr, double Bf)> Wings = new()
    {
        [45] = (0.18, 0.08), [35] = (0.55, 0.35), [25] = (1, 1), [15] = (1.55, 2.0), [10] = (1.9, 2.9), [5] = (2.5, 4.3),
    };

    /// <summary>The sources a surface can be read from: a composite and two dealers, each its own
    /// level and width.</summary>
    public static readonly string[] Sources = ["Composite", "Dealer A", "Dealer B"];

    public static IReadOnlyList<Row> Surface(string pairName, DateOnly asOf, string source = "Composite")
    {
        var s = Array.IndexOf(Sources, source) is var k && k > 0 ? k : 0;
        var bias = s == 0 ? 0 : s == 1 ? 0.08 : -0.06;
        var wide = 1 + 0.3 * s;
        var pair = Of(pairName);
        // The market moves with the date: a slow swell in the level and in the skew.
        var day = asOf.DayNumber;
        var level = 0.9 * Math.Sin(day / 9.0) + 0.4 * Math.Sin(day / 3.1);
        var skew = 1 + 0.25 * Math.Sin(day / 6.3);
        var rows = new List<Row>();
        foreach (var (name, t) in Expiries)
        {
            var atm = pair.AtmLong + (pair.AtmShort - pair.AtmLong) * Math.Exp(-t / 0.35) + level * Math.Exp(-t / 0.8) + bias;
            var rr25 = pair.Rr25 * skew * (0.6 + 0.4 * Math.Exp(-t / 0.5));
            var bf25 = pair.Bf25 * (0.5 + 0.5 * Math.Sqrt(Math.Min(t, 2) / 2));
            // Wider for short expiries and far wings: less traded.
            var width = (t < 7 / 365.0 ? 1.2 : t < 1 / 12.0 ? 0.6 : 0.4) * wide;
            Quote Around(double mid, double w) => new(Math.Round(mid - w / 2, 3), Math.Round(mid + w / 2, 3));

            var calls = new Dictionary<int, Quote>();
            var puts = new Dictionary<int, Quote>();
            var rrs = new Dictionary<int, Quote>();
            var bfs = new Dictionary<int, Quote>();
            foreach (var d in Deltas)
            {
                var (kRr, kBf) = Wings[d];
                var rr = rr25 * kRr;
                var bf = bf25 * kBf;
                var w = width * (1 + (50 - d) / 50.0 * 1.2);
                calls[d] = Around(atm + bf + rr / 2, w);
                puts[d] = Around(atm + bf - rr / 2, w);
                rrs[d] = Around(rr, 0.28 * kRr);
                bfs[d] = Around(bf, 0.2 * Math.Sqrt(kBf));
            }
            var foreign = pair.ForeignRate + 0.003 * (1 - Math.Exp(-t / 2));
            var domestic = pair.DomesticRate - 0.004 * (1 - Math.Exp(-t / 1.5));
            var forward = pair.Spot * Math.Exp((domestic - foreign) * t);
            rows.Add(new Row(name, t, ExpiryDate(asOf, t), Around(atm, width), calls, puts, rrs, bfs, foreign, domestic, forward));
        }
        return rows;
    }

    // An expiry's date: today plus the tenor, moved off a weekend.
    private static DateOnly ExpiryDate(DateOnly asOf, double years)
    {
        var date = years < 1 / 12.0 - 1e-9 ? asOf.AddDays((int)Math.Round(years * 365)) : asOf.AddMonths((int)Math.Round(years * 12));
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
        return date;
    }

    /// <summary>A vol by the surface's delta axis name: "25P", "ATM", "10C".</summary>
    public static Quote At(Row row, string delta) => delta switch
    {
        "ATM" => row.Atm,
        _ when delta.EndsWith('C') => row.Calls[int.Parse(delta[..^1])],
        _ => row.Puts[int.Parse(delta[..^1])],
    };

    /// <summary>
    /// What a quick pricer shows for one quote: the strike its delta stands for (forward delta,
    /// no premium adjustment) and the option's price at the bid and the ask vol, as a percentage
    /// of the foreign notional at spot (Garman–Kohlhagen).
    /// </summary>
    public sealed record Priced(double Strike, double PriceBid, double PriceAsk, bool IsCall);

    public static Priced Price(Pair pair, Row row, string delta)
    {
        var quote = At(row, delta);
        var isCall = delta != "ATM" ? delta.EndsWith('C') : true;
        var d = delta == "ATM" ? 50 : int.Parse(delta[..^1]);
        var t = row.Years;
        // Forward delta N(d1) for a call, N(d1) − 1 for a put: d1 from the delta, the strike from d1.
        var d1 = InverseNormal(isCall ? d / 100.0 : 1 - d / 100.0);
        double StrikeAt(double vol) => row.Forward * Math.Exp(-d1 * vol * Math.Sqrt(t) + 0.5 * vol * vol * t);
        var strike = StrikeAt(quote.Mid / 100);
        double PriceAt(double volPoints)
        {
            var vol = volPoints / 100;
            var sd = vol * Math.Sqrt(t);
            var dd1 = (Math.Log(row.Forward / strike) + 0.5 * sd * sd) / sd;
            var dd2 = dd1 - sd;
            var df = Math.Exp(-row.DepositDomestic * t);
            var value = isCall
                ? df * (row.Forward * Normal(dd1) - strike * Normal(dd2))
                : df * (strike * Normal(-dd2) - row.Forward * Normal(-dd1));
            return value / pair.Spot * 100;
        }
        return new Priced(strike, PriceAt(quote.Bid), PriceAt(quote.Ask), isCall);
    }

    private static double Normal(double x) => 0.5 * (1 + Erf(x / Math.Sqrt(2)));

    private static double Erf(double x)
    {
        // Abramowitz and Stegun 7.1.26, enough for a quick pricer's four decimals.
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        var t = 1 / (1 + 0.3275911 * x);
        var y = 1 - ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }

    // Acklam's rational approximation of the normal quantile.
    private static double InverseNormal(double p)
    {
        double[] a = [-39.69683028665376, 220.9460984245205, -275.9285104469687, 138.3577518672690, -30.66479806614716, 2.506628277459239];
        double[] b = [-54.47609879822406, 161.5858368580409, -155.6989798598866, 66.80131188771972, -13.28068155288572];
        double[] c = [-0.007784894002430293, -0.3223964580411365, -2.400758277161838, -2.549732539343734, 4.374664141464968, 2.938163982698783];
        double[] d = [0.007784695709041462, 0.3224671290700398, 2.445134137142996, 3.754408661907416];
        if (p < 0.02425)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        if (p > 1 - 0.02425)
        {
            var q = Math.Sqrt(-2 * Math.Log(1 - p));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        var r = p - 0.5;
        var s = r * r;
        return (((((a[0] * s + a[1]) * s + a[2]) * s + a[3]) * s + a[4]) * s + a[5]) * r / (((((b[0] * s + b[1]) * s + b[2]) * s + b[3]) * s + b[4]) * s + 1);
    }

    /// <summary>The weekday before or after a date: the surface's dates are trading days.</summary>
    public static DateOnly StepWeekday(DateOnly date, int step)
    {
        do date = date.AddDays(step); while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        return date;
    }
}
