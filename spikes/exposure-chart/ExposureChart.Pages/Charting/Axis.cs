using System.Globalization;

namespace ExposureChart.Charting;

/// <summary>A value axis: its ends and its tick step, both multiples of the step.</summary>
public readonly record struct ValueAxis(decimal Min, decimal Max, decimal Step)
{
    public IEnumerable<decimal> Ticks()
    {
        for (var v = Min; v <= Max + Step / 1000; v += Step) yield return v;
    }

    public double Fraction(decimal value) => Max == Min ? 0 : (double)((value - Min) / (Max - Min));
}

public static class Axis
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// The "nice" axis over the values, always including zero: an exposure read against an axis
    /// that starts at 9M looks like it went to nothing when it halved.
    /// </summary>
    public static ValueAxis Nice(IEnumerable<decimal> values, int target = 5)
    {
        decimal lo = 0, hi = 0;
        foreach (var v in values)
        {
            if (v < lo) lo = v;
            if (v > hi) hi = v;
        }
        if (lo == hi) hi = lo + 1;
        var raw = (double)(hi - lo) / target;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normal = raw / magnitude;
        var nice = normal <= 1 ? 1 : normal <= 2 ? 2 : normal <= 2.5 ? 2.5 : normal <= 5 ? 5 : 10;
        var step = (decimal)(nice * magnitude);
        return new ValueAxis(Math.Floor(lo / step) * step, Math.Ceiling(hi / step) * step, step);
    }

    /// <summary>
    /// Two axes stretched to the same count of steps below and above zero, so that their zeros
    /// and their grid lines fall on the same heights — a right-hand zero drawn at another height
    /// than the left-hand one reads as a level that is not there.
    /// </summary>
    public static (ValueAxis Left, ValueAxis Right) AlignZeros(ValueAxis left, ValueAxis right)
    {
        var below = Math.Max(-left.Min / left.Step, -right.Min / right.Step);
        var above = Math.Max(left.Max / left.Step, right.Max / right.Step);
        return (new ValueAxis(-below * left.Step, above * left.Step, left.Step),
                new ValueAxis(-below * right.Step, above * right.Step, right.Step));
    }

    /// <summary>A tick label short enough for the axis: 12.5M, -250K, 0 — one unit for the
    /// whole axis, chosen by its largest end, so -2.5M is never written -2500K.</summary>
    public static string Compact(decimal value, ValueAxis axis)
    {
        var abs = Math.Max(Math.Abs(axis.Min), Math.Abs(axis.Max));
        // Thousands only from ten thousand: an axis of basis points to 1,000 reads "1,000", not "1K".
        var (unit, suffix) = abs >= 1_000_000_000m ? (1_000_000_000m, "B")
            : abs >= 1_000_000m ? (1_000_000m, "M")
            : abs >= 10_000m ? (1_000m, "K")
            : (1m, "");
        if (value == 0) return "0";
        return (value / unit).ToString("#,##0.###", Invariant) + suffix;
    }

    /// <summary>
    /// Date ticks on calendar boundaries: years when the span is long, otherwise months — at
    /// most about ten, so the labels never collide at the chart's width.
    /// </summary>
    public static IReadOnlyList<(DateOnly Date, string Label)> DateTicks(DateOnly first, DateOnly last)
    {
        var ticks = new List<(DateOnly, string)>();
        var years = (last.DayNumber - first.DayNumber) / 365.25;
        if (years >= 3)
        {
            var step = new[] { 1, 2, 5, 10 }.First(s => years / s <= 10);
            for (var y = (first.Year / step + 1) * step; y <= last.Year; y += step)
                ticks.Add((new DateOnly(y, 1, 1), y.ToString(Invariant)));
        }
        else
        {
            var months = years * 12;
            var step = new[] { 1, 3, 6 }.First(s => months / s <= 10);
            var d = new DateOnly(first.Year, first.Month, 1).AddMonths(1);
            while ((d.Month - 1) % step != 0) d = d.AddMonths(1);
            for (; d <= last; d = d.AddMonths(step))
                ticks.Add((d, d.ToString("yyyy-MM", Invariant)));
        }
        return ticks;
    }
}
