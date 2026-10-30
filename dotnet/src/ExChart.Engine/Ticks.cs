using System.Globalization;

namespace ExChart.Engine;

/// <summary>Where a horizontal axis puts its ticks.</summary>
public enum TickPlacement
{
    /// <summary>The default for the axis: calendar boundaries for dates, round numbers for numbers, every category.</summary>
    Auto,

    /// <summary>At calendar boundaries — years, quarters, months or days, the finest that fits.</summary>
    Calendar,

    /// <summary>At every position some series has a point at.</summary>
    EveryPoint,

    /// <summary>At the market's tenors counted from the first date: 1M, 3M, 6M, 1Y, 2Y, 3Y, 5Y, 7Y, 10Y …</summary>
    Tenors,

    /// <summary>At the positions the axis names (<see cref="ChartAxis.Named"/>).</summary>
    Named,
}

/// <summary>How a date tick is labelled.</summary>
public enum DateLabel
{
    /// <summary>The finest format the ticks need.</summary>
    Auto,

    /// <summary>2026.</summary>
    Year,

    /// <summary>2026-10.</summary>
    YearMonth,

    /// <summary>2026-10-19.</summary>
    Date,

    /// <summary>Oct 2026.</summary>
    MonthYear,

    /// <summary>The time from the first date as a tenor: 18M, 5Y.</summary>
    Tenor,
}

/// <summary>How the horizontal axis is ticked and labelled: three independent choices.</summary>
/// <param name="Placement">Where the ticks go.</param>
/// <param name="Label">How a date tick is labelled; ignored on numbers and categories.</param>
/// <param name="Angle">The labels' angle: 0, 45 or 90 degrees, read upwards.</param>
public sealed record AxisStyle(TickPlacement Placement = TickPlacement.Auto, DateLabel Label = DateLabel.Auto, int Angle = 0)
{
    /// <summary>The default style.</summary>
    public static AxisStyle Default { get; } = new();
}

/// <summary>A tick: where it stands on the axis, and its label, or null where the label is left out so as not to touch its neighbour.</summary>
/// <param name="Position">The position, in the axis' units.</param>
/// <param name="Label">The label, or null where it was left out.</param>
public sealed record AxisTick(double Position, string? Label);

/// <summary>The ticks of an axis, laid out: every tick is drawn; a label that would touch its neighbour is left out, never overprinted.</summary>
/// <param name="Ticks">The ticks, in order.</param>
/// <param name="Depth">How far the labels reach from the axis, in pixels, so the room under it grows with the angle and the longest label.</param>
public sealed record AxisTicks(IReadOnlyList<AxisTick> Ticks, double Depth)
{
    /// <summary>How many ticks kept their label.</summary>
    public int Labelled => Ticks.Count(t => t.Label is not null);
}

/// <summary>Ticks and labels for both axes (ticket 04). Text is never measured: widths come from the estimate the caller hands in (ADR-0021).</summary>
public static class TickLayout
{
    private static readonly int[] TenorMonths = [1, 3, 6, 12, 24, 36, 60, 84, 120, 180, 240, 300, 360, 480, 600];

    /// <summary>A tenor in years written as the market writes it: 6M, 18M, 1Y, 2.5Y.</summary>
    public static string Tenor(double years)
    {
        var months = years * 12;
        if (Math.Abs(months - Math.Round(months)) < 1e-6)
        {
            var m = (int)Math.Round(months);
            return m % 12 == 0 ? $"{m / 12}Y" : $"{m}M";
        }
        var weeks = years * 52;
        if (years < 1.0 / 12 && Math.Abs(weeks - Math.Round(weeks)) < 1e-6) return $"{(int)Math.Round(weeks)}W";
        return years.ToString("0.##", CultureInfo.InvariantCulture) + "Y";
    }

    /// <summary>
    /// The whole decades that hold <paramref name="min"/> to <paramref name="max"/>, both above zero:
    /// the bounds a logarithmic axis is drawn between (ADR-0290).
    /// </summary>
    public static (double Min, double Max) Decades(double min, double max)
    {
        if (!(min > 0) || !(max > 0)) return (1, 10);
        var lo = Math.Pow(10, Math.Floor(Math.Log10(min) + 1e-9));
        var hi = Math.Pow(10, Math.Ceiling(Math.Log10(max) - 1e-9));
        return hi > lo ? (lo, hi) : (lo, lo * 10);
    }

    /// <summary>
    /// A logarithmic axis' ticks between <paramref name="min"/> and <paramref name="max"/>, both above
    /// zero (ADR-0290): every 1, 2 and 5 times a power of ten across up to three decades, every power
    /// of ten across more, and every whole multiple within less than one.
    /// </summary>
    public static IReadOnlyList<double> LogSteps(double min, double max)
    {
        if (!(min > 0) || !(max > min)) return min > 0 ? [min] : [];
        var decades = Math.Log10(max / min);
        int[] multiples = decades < 1 ? [1, 2, 3, 4, 5, 6, 7, 8, 9] : decades <= 3 ? [1, 2, 5] : [1];
        var ticks = new List<double>();
        for (var e = (int)Math.Floor(Math.Log10(min)) - 1; e <= (int)Math.Ceiling(Math.Log10(max)); e++)
            foreach (var m in multiples)
            {
                var t = Math.Round(m * Math.Pow(10, e), 12 - Math.Min(12, Math.Max(0, e)));
                if (t >= min * (1 - 1e-9) && t <= max * (1 + 1e-9)) ticks.Add(t);
            }
        return ticks;
    }

    /// <summary>
    /// Round-number ticks covering <paramref name="min"/>–<paramref name="max"/>, about
    /// <paramref name="target"/> of them, at a step of 1, 2, 2.5 or 5 times a power of ten.
    /// </summary>
    public static (double Min, double Max, double Step) Nice(double min, double max, int target = 5)
    {
        if (min == max) { var pad = min == 0 ? 1 : Math.Abs(min) / 10; min -= pad; max += pad; }
        var raw = (max - min) / Math.Max(target, 1);
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var step = new[] { 1, 2, 2.5, 5, 10 }.Select(f => f * mag).First(s => s >= raw * (1 - 1e-9));
        return (Math.Floor(min / step + 1e-9) * step, Math.Ceiling(max / step - 1e-9) * step, step);
    }

    /// <summary>The positions of round-number ticks from <paramref name="min"/> to <paramref name="max"/> at <paramref name="step"/>.</summary>
    public static IReadOnlyList<double> Steps(double min, double max, double step)
    {
        var n = (int)Math.Round((max - min) / step);
        return Enumerable.Range(0, n + 1).Select(i => Math.Round(min + i * step, 12)).ToArray();
    }

    /// <summary>
    /// The horizontal axis' ticks over <paramref name="from"/>–<paramref name="to"/>, placed on the plot
    /// by <paramref name="map"/>, in pixels, with labels left out where they would
    /// touch. <paramref name="points"/> are the positions series have points at; <paramref name="textWidth"/>
    /// estimates a label's width and <paramref name="lineHeight"/> is a label's height, in pixels.
    /// </summary>
    public static AxisTicks Horizontal(ChartAxis axis, AxisStyle style, double from, double to, IReadOnlyCollection<double> points,
        Func<double, double> map, Func<string, double> textWidth, double lineHeight, double gap = 4)
    {
        var placement = style.Placement == TickPlacement.Auto
            ? axis.Values switch
            {
                AxisValues.Dates => TickPlacement.Calendar,
                AxisValues.Labels => TickPlacement.EveryPoint,
                _ => axis.Named.Count > 0 ? TickPlacement.Named : TickPlacement.Calendar,
            }
            : style.Placement;

        var ticks = new List<(double X, string Label)>();
        if (axis.EffectiveScale == AxisScale.Logarithmic && from > 0)
        {
            foreach (var x in LogSteps(from, to)) ticks.Add((x, axis.Writes ? axis.Write(x) : x.ToString("0.####", CultureInfo.InvariantCulture)));
        }
        else if (axis.Values == AxisValues.Labels)
        {
            for (var i = 0; i < axis.Labels.Count; i++) ticks.Add((i, axis.Labels[i]));
        }
        else if (placement == TickPlacement.Named)
        {
            ticks.AddRange(axis.Named.Where(n => n.Position >= from && n.Position <= to).Select(n => (n.Position, n.Label)));
        }
        else if (placement == TickPlacement.EveryPoint)
        {
            ticks.AddRange(points.Where(x => x >= from && x <= to).Distinct().Order().Select(x => (x, LabelOf(axis, style, x, from, Unit.Day))));
        }
        else if (placement == TickPlacement.Tenors)
        {
            foreach (var m in TenorMonths)
            {
                var x = axis.Values == AxisValues.Dates ? DateOnly.FromDayNumber((int)from).AddMonths(m).DayNumber : from + m / 12.0;
                if (x <= to) ticks.Add((x, axis.Values == AxisValues.Dates && style.Label != DateLabel.Tenor && style.Label != DateLabel.Auto ? LabelOf(axis, style, x, from, Unit.Day) : Tenor(m / 12.0)));
            }
        }
        else if (axis.Values == AxisValues.Dates)
        {
            var unit = CalendarUnit(from, to, map, textWidth, gap, axis.CodeWritesTicks && style.Label == DateLabel.Auto ? axis : null);
            foreach (var x in CalendarTicks(from, to, unit)) ticks.Add((x, LabelOf(axis, style, x, from, unit.Kind)));
        }
        else
        {
            var (lo, hi, step) = Nice(from, to, 6);
            foreach (var x in Steps(lo, hi, step).Where(x => x >= from - 1e-9 && x <= to + 1e-9))
                ticks.Add((x, axis.CodeWritesTicks ? axis.Write(x) : x.ToString("0.####", CultureInfo.InvariantCulture)));
        }
        return Fit(ticks, style.Angle, map, textWidth, lineHeight, gap);
    }

    /// <summary>Keeps each label whose footprint along the axis clears the last kept one's; every tick stays.</summary>
    internal static AxisTicks Fit(IReadOnlyList<(double X, string Label)> ticks, int angle, Func<double, double> map, Func<string, double> textWidth, double lineHeight, double gap)
    {
        var rad = angle * Math.PI / 180;
        var result = new List<AxisTick>(ticks.Count);
        var lastEnd = double.NegativeInfinity;
        var depth = 0.0;
        foreach (var (x, label) in ticks)
        {
            var w = textWidth(label);
            // Along the axis a label at an angle takes its height's share, not its width's; read
            // upwards, it is anchored at its tick, so its footprint starts there.
            var along = w * Math.Cos(rad) + lineHeight * Math.Sin(rad);
            var px = map(x);
            var start = angle == 0 ? px - along / 2 : px - lineHeight * Math.Sin(rad) / 2;
            if (start >= lastEnd + gap)
            {
                result.Add(new AxisTick(x, label));
                lastEnd = start + along;
                depth = Math.Max(depth, w * Math.Sin(rad) + lineHeight * Math.Cos(rad));
            }
            else
            {
                result.Add(new AxisTick(x, null));
            }
        }
        return new AxisTicks(result, depth);
    }

    internal enum Unit { Day, Month, Quarter, Year }

    internal readonly record struct CalendarStep(Unit Kind, int Every);

    // The finest step whose labels, at their usual width, fit side by side across the plot. Where the axis' code
    // writes the ticks, no step finer than the code tells apart, so no two ticks read alike, and its labels are
    // as wide as the code writes a long date (ADR-0420).
    private static CalendarStep CalendarUnit(double from, double to, Func<double, double> map, Func<string, double> textWidth, double gap, ChartAxis? coded)
    {
        var plot = Math.Abs(map(to) - map(from));
        var finest = coded is null ? Unit.Day : CodeResolution(coded.Write) ?? throw new InvalidOperationException("Internal: a code that tells no dates apart was not refused.");
        var codedWidth = coded is null ? 0 : textWidth(coded.Write(WidestDay));
        CalendarStep[] steps =
        [
            new(Unit.Day, 1), new(Unit.Day, 7), new(Unit.Month, 1), new(Unit.Quarter, 1), new(Unit.Month, 6),
            new(Unit.Year, 1), new(Unit.Year, 2), new(Unit.Year, 5), new(Unit.Year, 10), new(Unit.Year, 25), new(Unit.Year, 50),
        ];
        foreach (var s in steps.Where(s => s.Kind >= finest))
        {
            var count = Math.Max(1, CalendarTicks(from, to, s).Count);
            var label = coded is not null ? codedWidth : textWidth(s.Kind == Unit.Year ? "2026" : s.Kind == Unit.Day ? "2026-10-19" : "2026-10");
            if (plot / count >= label + gap) return s;
        }
        return steps[^1];
    }

    // A day whose names are the longest a code writes: a Wednesday in September.
    private static readonly double WidestDay = new DateOnly(2026, 9, 30).DayNumber;

    /// <summary>
    /// The finest calendar step a date code tells apart, found by writing days a day, a month and a year apart
    /// with it; null for a code that writes none of them apart, such as one of the time alone.
    /// </summary>
    internal static Unit? CodeResolution(Func<double, string> write)
    {
        var day = new DateOnly(2001, 2, 3);
        var text = write(day.DayNumber);
        if (write(day.AddDays(1).DayNumber) != text) return Unit.Day;
        if (write(day.AddMonths(1).DayNumber) != text) return Unit.Month;
        if (write(day.AddYears(1).DayNumber) != text) return Unit.Year;
        return null;
    }

    internal static List<double> CalendarTicks(double from, double to, CalendarStep step)
    {
        var start = DateOnly.FromDayNumber((int)Math.Ceiling(from));
        var end = DateOnly.FromDayNumber((int)Math.Floor(to));
        var d = step.Kind switch
        {
            Unit.Day => start,
            Unit.Year => new DateOnly(start.Year, 1, 1),
            _ => new DateOnly(start.Year, 1, 1),
        };
        var result = new List<double>();
        while (d <= end)
        {
            var on = step.Kind switch
            {
                Unit.Day => true,
                Unit.Month => d.Day == 1 && (d.Month - 1) % step.Every == 0,
                Unit.Quarter => d.Day == 1 && (d.Month - 1) % 3 == 0,
                _ => d.Day == 1 && d.Month == 1 && d.Year % step.Every == 0,
            };
            if (on && d >= start) result.Add(d.DayNumber);
            d = step.Kind switch
            {
                Unit.Day => d.AddDays(step.Every),
                Unit.Year => d.AddYears(1),
                _ => d.AddMonths(1),
            };
        }
        return result;
    }

    private static string LabelOf(ChartAxis axis, AxisStyle style, double x, double first, Unit unit)
    {
        if (axis.Values == AxisValues.Numbers)
            return axis.Named.FirstOrDefault(n => n.Position == x).Label ?? (axis.CodeWritesTicks ? axis.Write(x) : x.ToString("0.####", CultureInfo.InvariantCulture));
        // A code writes a date tick, unless the axis' style names how date ticks are labelled (ADR-0420).
        if (axis.CodeWritesTicks && style.Label == DateLabel.Auto)
            return axis.Write(x);
        var d = DateOnly.FromDayNumber((int)x);
        var label = style.Label == DateLabel.Auto
            ? unit switch { Unit.Year => DateLabel.Year, Unit.Day => DateLabel.Date, _ => DateLabel.YearMonth }
            : style.Label;
        return label switch
        {
            DateLabel.Year => d.ToString("yyyy", CultureInfo.InvariantCulture),
            DateLabel.YearMonth => d.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            DateLabel.MonthYear => d.ToString("MMM yyyy", CultureInfo.InvariantCulture),
            DateLabel.Tenor => Tenor(MonthsBetween(DateOnly.FromDayNumber((int)first), d) / 12.0),
            _ => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    private static int MonthsBetween(DateOnly a, DateOnly b) => (b.Year - a.Year) * 12 + b.Month - a.Month;
}
