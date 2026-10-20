using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// One row of a Price Range (ADR-0300): a period's open, high, low and close at its position, and
/// optionally its volume. A row missing any of the four is a gap at its position; a series whose rows
/// all have no open is drawn as High-Low-Close bars.
/// </summary>
/// <param name="X">The period's position: on a Dates axis, its day number.</param>
/// <param name="Open">The first price of the period, or null.</param>
/// <param name="High">The highest price of the period, or null.</param>
/// <param name="Low">The lowest price of the period, or null.</param>
/// <param name="Close">The last price of the period, or null.</param>
/// <param name="Volume">The quantity traded in the period, or null.</param>
public readonly record struct PriceBar(double X, ChartValue? Open, ChartValue? High, ChartValue? Low, ChartValue? Close, ChartValue? Volume = null);

/// <summary>
/// A series' Interval (ADR-0303): a lower and an upper bound at its points, handed in by the Consumer,
/// with what they mean. ExChart computes no width.
/// </summary>
/// <param name="Meaning">What the interval is — "95% CI", "±1σ", "Bid–Ask" — shown in the legend, the
/// tooltip, the Notes and the CSV; required.</param>
/// <param name="Bounds">The bounds at the series' positions; a position may be left out, or have a
/// bound missing, and then has no interval.</param>
/// <param name="Style">Whiskers or a band.</param>
public sealed record SeriesInterval(string Meaning, IReadOnlyList<IntervalBound> Bounds, IntervalStyle Style = IntervalStyle.Whiskers);

/// <summary>An Interval's bounds at one position of its series.</summary>
/// <param name="X">The position: one of the series' points.</param>
/// <param name="Lower">The lower bound, or null.</param>
/// <param name="Upper">The upper bound, or null.</param>
public readonly record struct IntervalBound(double X, ChartValue? Lower, ChartValue? Upper);

/// <summary>A candle as drawn, in the data's units: one row, or a merged candle standing for several (ADR-0302).</summary>
/// <param name="From">The first position it stands for.</param>
/// <param name="To">The last.</param>
/// <param name="X">Where it is centred.</param>
/// <param name="Open">Its open, or null for a High-Low-Close bar.</param>
/// <param name="High">Its high.</param>
/// <param name="Low">Its low.</param>
/// <param name="Close">Its close.</param>
/// <param name="Volume">Its volume, or null.</param>
/// <param name="Rows">How many rows it stands for.</param>
public readonly record struct DrawnCandle(double From, double To, double X, double? Open, double High, double Low, double Close, double? Volume, int Rows)
{
    /// <summary>Rising: its close above its own open, drawn hollow (ADR-0300).</summary>
    public bool Rising => Open is { } o && Close > o;

    /// <summary>Unchanged: its close equal to its own open, drawn as one horizontal line.</summary>
    public bool Unchanged => Open is { } o && Close == o;
}

/// <summary>A whisker as drawn: a point's interval.</summary>
/// <param name="X">The point's position.</param>
/// <param name="Lower">Its lower bound.</param>
/// <param name="Upper">Its upper bound.</param>
public readonly record struct DrawnWhisker(double X, double Lower, double Upper);

internal static class PriceRanges
{
    // Days in a row of a Dates axis are day numbers; merged periods are the market's own.
    internal static double PeriodStart(double x, CandlePeriod period)
    {
        if (period == CandlePeriod.Row) return x;
        var d = DateOnly.FromDayNumber((int)Math.Floor(x));
        return period switch
        {
            CandlePeriod.Week => d.DayNumber - ((int)d.DayOfWeek + 6) % 7,
            CandlePeriod.Month => new DateOnly(d.Year, d.Month, 1).DayNumber,
            CandlePeriod.Quarter => new DateOnly(d.Year, (d.Month - 1) / 3 * 3 + 1, 1).DayNumber,
            _ => new DateOnly(d.Year, 1, 1).DayNumber,
        };
    }

    internal static string Name(CandlePeriod period) => period switch
    {
        CandlePeriod.Week => "Weekly",
        CandlePeriod.Month => "Monthly",
        CandlePeriod.Quarter => "Quarterly",
        CandlePeriod.Year => "Yearly",
        _ => "Rows",
    };

    /// <summary>The rows that make a candle: all four (or, with no open column, three) present.</summary>
    internal static bool Whole(PriceBar b, bool hasOpen) =>
        b.High is not null && b.Low is not null && b.Close is not null && (!hasOpen || b.Open is not null);

    internal static bool HasOpen(ChartSeries s) => HasOpen(s.Bars);

    internal static bool HasOpen(IEnumerable<PriceBar> bars) => bars.Any(b => b.Open is not null);

    internal static List<DrawnCandle> Candles(ChartSeries s, double from, double to, CandlePeriod period)
    {
        var hasOpen = HasOpen(s);
        var rows = s.Bars.Where(b => b.X >= from && b.X <= to && Whole(b, hasOpen)).ToList();
        var candles = new List<DrawnCandle>();
        foreach (var g in rows.GroupBy(b => PeriodStart(b.X, period)))
        {
            var bars = g.ToList();
            var first = bars[0];
            var last = bars[^1];
            var volumes = bars.Where(b => b.Volume is not null).ToList();
            candles.Add(new DrawnCandle(first.X, last.X, period == CandlePeriod.Row ? first.X : (first.X + last.X) / 2,
                hasOpen ? first.Open!.Value.ToDouble() : null,
                bars.Max(b => b.High!.Value.ToDouble()), bars.Min(b => b.Low!.Value.ToDouble()), last.Close!.Value.ToDouble(),
                volumes.Count > 0 ? volumes.Sum(b => b.Volume!.Value.ToDouble()) : null, bars.Count));
        }
        return candles;
    }

    // The refusal of a row whose values disagree (ADR-0300): named by its row and its values.
    internal static string? Disagreement(ChartSeries s, ChartAxis axis, PriceBar b)
    {
        if (b.High is not { } hv || b.Low is not { } lv || b.Close is not { } cv) return null;
        double h = hv.ToDouble(), l = lv.ToDouble(), c = cv.ToDouble();
        string Row() => $"'{s.Name}', the row of {axis.Write(b.X)}";
        string W(ChartValue v) => s.Write(v);
        if (b.Open is { } ov)
        {
            var o = ov.ToDouble();
            if (h < o) return $"{Row()}: high {W(hv)} is below open {W(ov)}.";
            if (l > o) return $"{Row()}: low {W(lv)} is above open {W(ov)}.";
        }
        if (h < c) return $"{Row()}: high {W(hv)} is below close {W(cv)}.";
        if (l > c) return $"{Row()}: low {W(lv)} is above close {W(cv)}.";
        if (h < l) return $"{Row()}: high {W(hv)} is below low {W(lv)}.";
        return null;
    }

    internal static string Percent(double change, double previous) =>
        previous == 0 ? "" : $" ({(change >= 0 ? "+" : "")}{(change / previous).ToString("0.00%", CultureInfo.InvariantCulture)})";
}

/// <summary>A candle in pixels (ADR-0300).</summary>
/// <param name="X">Its centre.</param>
/// <param name="HalfWidth">Half its body's width.</param>
/// <param name="OpenY">The height of its open, or null for a High-Low-Close bar.</param>
/// <param name="HighY">The height of its high.</param>
/// <param name="LowY">The height of its low.</param>
/// <param name="CloseY">The height of its close.</param>
/// <param name="Rising">Its close above its own open: drawn hollow.</param>
/// <param name="Unchanged">Its close equal to its own open: one horizontal line.</param>
/// <param name="Rows">How many rows it stands for.</param>
/// <param name="From">The first position it stands for.</param>
/// <param name="To">The last.</param>
public readonly record struct PlacedCandle(double X, double HalfWidth, double? OpenY, double HighY, double LowY, double CloseY, bool Rising, bool Unchanged, int Rows, double From, double To);

/// <summary>A volume bar in pixels, following its candle (ADR-0300).</summary>
/// <param name="X">Its centre.</param>
/// <param name="HalfWidth">Half its width.</param>
/// <param name="TopY">The height of its volume.</param>
/// <param name="BaseY">The height of zero.</param>
/// <param name="Rising">Its candle rising: drawn hollow.</param>
/// <param name="Unchanged">Its candle unchanged.</param>
/// <param name="Rows">How many rows it stands for.</param>
/// <param name="From">The first position it stands for.</param>
/// <param name="To">The last.</param>
public readonly record struct PlacedVolume(double X, double HalfWidth, double TopY, double BaseY, bool Rising, bool Unchanged, int Rows, double From, double To);
