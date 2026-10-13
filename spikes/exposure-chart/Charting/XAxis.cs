using System.Globalization;

namespace ExposureChart.Charting;

/// <summary>Where the horizontal axis puts its ticks.</summary>
public enum XTicks
{
    /// <summary>Calendar boundaries: years over a long span, months over a short one.</summary>
    Calendar,

    /// <summary>Every date the chart holds: the tenor grid the risk engine reported.</summary>
    DataDates,

    /// <summary>The market's tenors from the first date: 1M, 3M, 6M, 1Y, 2Y, 5Y, 10Y …</summary>
    Tenors,
}

/// <summary>How a tick is labelled.</summary>
public enum XLabel { Auto, Year, YearMonth, IsoDate, MonthNameYear, Tenor, Column }

/// <param name="Angle">0, 45 or 90 degrees, turned anticlockwise and read upwards.</param>
public sealed record XAxisStyle(XTicks Ticks, XLabel Label, int Angle)
{
    /// <summary>The Consumer's label for a date — a tenor column's "5Y" — for <see cref="XLabel.Column"/>.</summary>
    public Func<double, string?>? LabelOf { get; init; }

    public static XAxisStyle Default { get; } = new(XTicks.Calendar, XLabel.Auto, 0);
}

/// <summary>A tick; a label it could not fit is null, and the tick is still drawn.</summary>
public readonly record struct XTick(double X, string? Label);

public static class XAxis
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly int[] TenorMonths = [1, 3, 6, 12, 24, 36, 60, 84, 120, 180, 240, 300, 360, 480, 600];

    // Text is not measured (ADR-0021): an estimate of an 11px label, generous so labels never touch.
    private const double CharWidth = 6.3, LineHeight = 12;

    /// <param name="xs">The positions charted, in order.</param>
    /// <param name="domain">The axis' extent, which may start before the first position.</param>
    public static IReadOnlyList<XTick> Ticks(IReadOnlyList<double> xs, (double From, double To) domain, XScale scale,
        XAxisStyle style, Func<double, double> x)
    {
        var (positions, auto) = scale.IsDate ? DatePositions(xs, style) : NumberPositions(xs, domain, style);
        var label = style.Label == XLabel.Auto ? auto : style.Label;
        var first = scale.IsDate ? XScale.DateOf(xs[0]) : default;

        // Keep a label only where it clears the previous one along the axis. Upright, a label
        // takes its width; turned, it takes a line's height divided by the sine of the angle.
        var ticks = new List<XTick>();
        var clearUntil = double.NegativeInfinity;
        foreach (var position in positions)
        {
            var text = label == XLabel.Column && style.LabelOf?.Invoke(position) is { } own ? own
                : scale.IsDate ? Format(XScale.DateOf(position), first, label)
                : label == XLabel.Tenor || label == XLabel.Column ? XScale.Tenor(position) : scale.Format(position);
            var footprint = style.Angle == 0 ? text.Length * CharWidth + 10 : LineHeight / Math.Sin(style.Angle * Math.PI / 180) + 2;
            var at = x(position);
            if (at - footprint / 2 >= clearUntil)
            {
                ticks.Add(new XTick(position, text));
                clearUntil = at + footprint / 2;
            }
            else ticks.Add(new XTick(position, null));
        }
        return ticks;
    }

    private static (List<double>, XLabel) DatePositions(IReadOnlyList<double> xs, XAxisStyle style)
    {
        var first = XScale.DateOf(xs[0]);
        var last = XScale.DateOf(xs[^1]);
        return style.Ticks switch
        {
            XTicks.DataDates => (xs.ToList(), XLabel.IsoDate),
            XTicks.Tenors => (TenorMonths.Select(m => first.AddMonths(m)).Where(d => d <= last).Prepend(first).Select(XScale.Of).ToList(), XLabel.Tenor),
            _ => Calendar(first, last),
        };
    }

    // A number axis — a tenor's years: the data's positions, the market's tenors, or round numbers,
    // each from the axis' start.
    private static (List<double>, XLabel) NumberPositions(IReadOnlyList<double> xs, (double From, double To) domain, XAxisStyle style)
    {
        var (from, to) = domain;
        List<double> positions = style.Ticks switch
        {
            XTicks.DataDates => [.. xs],
            XTicks.Tenors => [.. TenorMonths.Select(m => m / 12.0).Where(y => y >= from && y <= to)],
            _ => Round(from, to),
        };
        if (positions.Count == 0 || positions[0] > from) positions.Insert(0, from);
        return (positions, XLabel.Tenor);
    }

    private static List<double> Round(double from, double to)
    {
        var step = new[] { 0.25, 0.5, 1, 2, 5, 10, 20, 50 }.First(s => (to - from) / s <= 10);
        var list = new List<double>();
        for (var v = Math.Ceiling(from / step) * step; v <= to + 1e-9; v += step) list.Add(v);
        return list;
    }

    /// <summary>How much room the labels need under the last panel.</summary>
    public static double Depth(IReadOnlyList<XTick> ticks, int angle)
    {
        if (angle == 0) return 26;
        var longest = ticks.Where(t => t.Label is not null).Select(t => t.Label!.Length).DefaultIfEmpty(4).Max();
        var a = angle * Math.PI / 180;
        return 14 + longest * CharWidth * Math.Sin(a) + LineHeight * Math.Cos(a);
    }

    public static string Format(DateOnly date, DateOnly first, XLabel label) => label switch
    {
        XLabel.Year => date.ToString("yyyy", Invariant),
        XLabel.YearMonth => date.ToString("yyyy-MM", Invariant),
        XLabel.MonthNameYear => date.ToString("MMM yyyy", Invariant),
        XLabel.Tenor => Tenor(first, date),
        _ => date.ToString("yyyy-MM-dd", Invariant),
    };

    /// <summary>The tenor from the first date: whole years as "5Y", whole months as "18M", else days.</summary>
    public static string Tenor(DateOnly first, DateOnly date)
    {
        if (date == first) return "0";
        var months = (date.Year - first.Year) * 12 + date.Month - first.Month;
        if (first.AddMonths(months) == date)
            return months % 12 == 0 ? $"{months / 12}Y" : $"{months}M";
        return $"{date.DayNumber - first.DayNumber}D";
    }

    private static (List<double>, XLabel) Calendar(DateOnly first, DateOnly last)
    {
        var ticks = Axis.DateTicks(first, last);
        var years = (last.DayNumber - first.DayNumber) / 365.25;
        return (ticks.Select(t => XScale.Of(t.Date)).ToList(), years >= 3 ? XLabel.Year : XLabel.YearMonth);
    }
}
