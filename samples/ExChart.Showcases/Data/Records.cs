namespace ExChart.Showcases.Data;

/// <summary>
/// One date of a netting set's exposure profile, before and after a proposed trade. The CVA is the
/// amount of the bucket ending at the date, so the as-of date, which ends no bucket, has none.
/// </summary>
public sealed record ExposureRow(
    DateOnly Date,
    decimal EpeBefore, decimal EpeAfter, decimal EpeDiff,
    decimal EneBefore, decimal EneAfter, decimal EneDiff,
    decimal? CvaBefore, decimal? CvaAfter, decimal? CvaDiff,
    decimal PfeBefore, decimal PfeAfter, decimal PfeDiff);

/// <summary>The exposure profiles, as a risk system reported them.</summary>
public static partial class ExposureData
{
    /// <summary>The date the profiles were computed on.</summary>
    public static readonly DateOnly AsOf = new(2026, 10, 13);
}

/// <summary>
/// One pillar of a CDS term structure: the quote, and what the analytics bootstrapped from it. The
/// first row is the curve's date, which has no quote and no hazard rate.
/// </summary>
public sealed record CdsRow(string Tenor, DateOnly Maturity, decimal? SpreadBp, decimal? HazardPct, decimal SurvivalPct, decimal DefaultPct);

/// <summary>One reference entity's CDS term structure.</summary>
public static partial class CdsData
{
    /// <summary>The curve's date.</summary>
    public static readonly DateOnly AsOf = new(2026, 10, 13);

    /// <summary>The recovery rate the hazard rates were bootstrapped at, in percent.</summary>
    public const int RecoveryPct = 40;

    /// <summary>The flat interest rate they were bootstrapped at, in percent.</summary>
    public const int RatePct = 3;
}

/// <summary>One CDS curve, its tenors across: par spreads in basis points and hazard rates in percent a year.</summary>
public sealed record CdsCurveRow(
    string Ticker, string Entity, string Ccy, string Seniority, string DocClause, decimal RecoveryPct,
    IReadOnlyList<decimal?> Spreads, IReadOnlyList<decimal?> Hazards)
{
    /// <summary>The curve's identity: ticker, seniority, currency and documentation clause.</summary>
    public string Key => $"{Ticker} {Seniority} {Ccy} {DocClause}";
}

/// <summary>CDS curves by reference entity.</summary>
public static partial class CdsCurveData
{
    /// <summary>The tenors' names, as the market writes them.</summary>
    public static readonly string[] TenorNames = ["6m", "1y", "2y", "3y", "4y", "5y", "7y", "10y", "15y", "20y"];

    /// <summary>Each tenor's position in years, declared rather than read off its name.</summary>
    public static readonly double[] TenorYears = [0.5, 1, 2, 3, 4, 5, 7, 10, 15, 20];
}

/// <summary>A curve's market quotes on a date, in percent, in the order of <see cref="RateData.Tenors"/>.</summary>
public sealed record RateQuotes(string Curve, DateOnly Date, DateOnly[] Maturities, double[] Bid, double[] Ask)
{
    /// <summary>The mid quote of instrument <paramref name="i"/>.</summary>
    public double Mid(int i) => (Bid[i] + Ask[i]) / 2;
}

/// <summary>
/// A curve as the analytics built it from the mid quotes under one interpolation: its figures at each
/// pillar (rates in percent), the curve between the pillars as the interpolation reads it, and its
/// instantaneous forwards.
/// </summary>
public sealed record BuiltCurve(
    string Curve, DateOnly Date, string Interpolation,
    double[] ZeroContinuous, double[] ZeroAnnual, double[] Discount, double[] Forward1Y, double[] Implied,
    double[] CurveYears, double[] CurveDiscount, double[] CurveZeroContinuous, double[] CurveZeroAnnual,
    double[] ForwardYears, double[] Forward);

/// <summary>Interest-rate curves: three currencies' OIS curves on two dates.</summary>
public static partial class RateData
{
    /// <summary>The curves.</summary>
    public static readonly string[] Curves = ["USD OIS", "EUR OIS", "JPY OIS"];

    /// <summary>The interpolations each curve was built under.</summary>
    public static readonly string[] Interpolations = ["Log-linear discount factors", "Linear zero rates"];

    /// <summary>The curve date, and the earlier date it can be compared with.</summary>
    public static readonly DateOnly Today = new(2026, 10, 13), Earlier = new(2026, 9, 14);

    /// <summary>The quotes of <paramref name="curve"/> on <paramref name="date"/>.</summary>
    public static RateQuotes QuotesOf(string curve, DateOnly date) => Quotes.First(q => q.Curve == curve && q.Date == date);

    /// <summary>The curve <paramref name="curve"/> on <paramref name="date"/> built under <paramref name="interpolation"/>.</summary>
    public static BuiltCurve BuiltOf(string curve, DateOnly date, string interpolation) =>
        Built.First(b => b.Curve == curve && b.Date == date && b.Interpolation == interpolation);
}

/// <summary>
/// One expiry of an FX volatility surface: its tenor, its expiry date and the implied volatilities quoted
/// at each delta of <see cref="FxVolData.Deltas"/>, in percent; null where no quote came in.
/// </summary>
public sealed record FxVolRow(string Tenor, DateOnly Expiry, IReadOnlyList<double?> Vols);

/// <summary>
/// One tenor of the deposits and forwards behind the surface: the value date, the days from spot, the
/// two currencies' deposit rates in percent (ACT/360), and the forward as points and as an outright.
/// </summary>
public sealed record FxForwardRow(string Tenor, DateOnly ValueDate, int Days, double EurPct, double UsdPct, double Points, double Outright);

/// <summary>EURUSD implied volatilities by delta and tenor, with the deposits and forwards beside them.</summary>
public static partial class FxVolData
{
    /// <summary>The pair.</summary>
    public const string Pair = "EURUSD";

    /// <summary>The date the quotes were taken on.</summary>
    public static readonly DateOnly AsOf = new(2026, 10, 13);

    /// <summary>The spot date, two business days on.</summary>
    public static readonly DateOnly SpotDate = new(2026, 10, 15);

    /// <summary>The spot rate, dollars per euro.</summary>
    public const double Spot = 1.1650;

    /// <summary>The deltas across, as the market quotes them: puts, at the money, calls.</summary>
    public static readonly string[] Deltas = ["10P", "25P", "ATM", "25C", "10C"];

    /// <summary>
    /// Each delta as a number for the page's interpolation across the smile: a put's delta from the call
    /// side (10P is a 90-delta call), at the money as 50.
    /// </summary>
    public static readonly double[] DeltaValues = [10, 25, 50, 75, 90];
}

/// <summary>One reference name: its leverage (debt over EBITDA, times) and its five-year CDS spread in basis points.</summary>
public sealed record CreditName(string Ticker, double Leverage, double SpreadBp);

/// <summary>Reference names' leverage and spreads, as a credit desk's screen shows them.</summary>
public static partial class CreditData
{
    /// <summary>The date the spreads were taken on.</summary>
    public static readonly DateOnly AsOf = new(2026, 10, 13);
}

/// <summary>One session of an equity index: its open, high, low and close, and the volume traded.</summary>
public sealed record IndexBar(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

/// <summary>Ten years of an equity index's daily bars.</summary>
public static partial class IndexData
{
    /// <summary>The index's name.</summary>
    public const string Name = "Northgate 225";
}

/// <summary>One tenor of a government curve, quoted bid and ask, in percent.</summary>
public sealed record CurveQuote(string Tenor, double Years, decimal Bid, decimal Ask);

/// <summary>One quarter of a survey of policy-rate forecasts: the median, and the 95% interval of the answers, in percent.</summary>
public sealed record RateForecast(string Quarter, decimal Median, decimal Low, decimal High);

/// <summary>
/// One strategy's annualised volatility and return, in percent, each estimated with a bootstrap 95%
/// confidence interval.
/// </summary>
public sealed record StrategyEstimate(string Name, decimal Vol, decimal VolLow, decimal VolHigh, decimal Return, decimal ReturnLow, decimal ReturnHigh);

/// <summary>The figures that carry intervals, beside the index: each interval as its source quoted it.</summary>
public static class IntervalData
{
    /// <summary>A government curve's bid and ask by tenor.</summary>
    public static readonly CurveQuote[] Curve =
    [
        new("3M", 0.25, 3.71m, 3.75m), new("6M", 0.5, 3.62m, 3.65m), new("1Y", 1, 3.48m, 3.51m), new("2Y", 2, 3.30m, 3.34m),
        new("3Y", 3, 3.27m, 3.30m), new("5Y", 5, 3.31m, 3.35m), new("7Y", 7, 3.42m, 3.47m), new("10Y", 10, 3.55m, 3.61m),
        new("20Y", 20, 3.81m, 3.90m), new("30Y", 30, 3.86m, 3.98m),
    ];

    /// <summary>Forecasts of the policy rate, now and for the next five quarters.</summary>
    public static readonly RateForecast[] Forecasts =
    [
        new("Now", 4.25m, 4.25m, 4.25m), new("Q1", 4.00m, 3.75m, 4.25m), new("Q2", 3.75m, 3.25m, 4.25m),
        new("Q3", 3.50m, 2.90m, 4.10m), new("Q4", 3.50m, 2.60m, 4.20m), new("Q5", 3.25m, 2.30m, 4.20m),
    ];

    /// <summary>Six strategies' estimated risk and return.</summary>
    public static readonly StrategyEstimate[] Strategies =
    [
        new("Carry", 6.1m, 5.4m, 6.9m, 4.2m, 1.1m, 7.3m), new("Momentum", 11.8m, 10.5m, 13.4m, 6.0m, 0.2m, 11.9m),
        new("Value", 9.4m, 8.3m, 10.6m, 2.9m, -2.0m, 7.8m), new("Low vol", 5.2m, 4.7m, 5.8m, 3.8m, 1.3m, 6.4m),
        new("Trend", 14.6m, 12.9m, 16.5m, 7.1m, -0.1m, 14.3m), new("Short vol", 8.7m, 6.2m, 12.9m, 5.5m, 0.9m, 10.0m),
    ];
}
