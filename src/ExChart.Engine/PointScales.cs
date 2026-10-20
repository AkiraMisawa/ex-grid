using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// A point's colour read from a column (ADR-0320): one value a point, in the points' order, coloured in
/// nine bands of Visual Tokens, never a colour value. A key is always drawn beside the plot.
/// </summary>
/// <param name="Title">What the values are, as the key and the tooltip say it.</param>
/// <param name="Values">One value a point; null where the point has none, drawn as an outline.</param>
public sealed record PointColour(string Title, IReadOnlyList<ChartValue?> Values)
{
    /// <summary>
    /// The value the colours diverge from, such as 0 for a P&amp;L (ADR-0310); null colours the values in
    /// one run from the lowest to the highest.
    /// </summary>
    public double? Centre { get; init; }
}

/// <summary>
/// A point's size read from a column (ADR-0320): one value a point, never negative, the marker's area in
/// proportion to it. A key is always drawn beside the plot.
/// </summary>
/// <param name="Title">What the values are, as the key and the tooltip say it.</param>
/// <param name="Values">One value a point; null where the point has none, drawn as an outline.</param>
public sealed record PointSize(string Title, IReadOnlyList<ChartValue?> Values);

/// <summary>A tick of a key: its value, its height in CSS pixels, and its label.</summary>
/// <param name="Value">The value.</param>
/// <param name="Y">Its height in CSS pixels.</param>
/// <param name="Label">Its label.</param>
public readonly record struct KeyTick(double Value, double Y, string Label);

/// <summary>
/// The colour bar a series' <see cref="PointColour"/> is read on (ADR-0320): its bands from
/// <see cref="From"/> at the foot to <see cref="To"/> at the head, in <see cref="Box"/>, and its ticks.
/// </summary>
/// <param name="SeriesId">The series it belongs to.</param>
/// <param name="Title">What the colours are.</param>
/// <param name="From">The value at the foot of the lowest band.</param>
/// <param name="To">The value at the head of the highest band.</param>
/// <param name="Ticks">Its ticks, the centre always among them when it diverges.</param>
/// <param name="Diverging">Whether the bands diverge from a centre, which the middle band holds.</param>
/// <param name="Box">The bar's box in CSS pixels; its title stands above it.</param>
public sealed record ColourKey(string SeriesId, string Title, double From, double To, EquatableList<KeyTick> Ticks, bool Diverging, Box Box)
{
    /// <summary>How many bands it is drawn in, low to high.</summary>
    public int Bands => PointScales.BandCount;
}

/// <summary>A size key's entry: a value, its label, and the radius a marker of that value is drawn at.</summary>
/// <param name="Value">The value.</param>
/// <param name="Label">Its label.</param>
/// <param name="Radius">The radius in CSS pixels.</param>
public readonly record struct SizeEntry(double Value, string Label, double Radius);

/// <summary>The size key a series' <see cref="PointSize"/> is read on (ADR-0320).</summary>
/// <param name="SeriesId">The series it belongs to.</param>
/// <param name="Title">What the sizes are.</param>
/// <param name="Entries">Its entries, smallest first.</param>
/// <param name="Box">Its box in CSS pixels, under its title.</param>
public sealed record SizeKey(string SeriesId, string Title, EquatableList<SizeEntry> Entries, Box Box);

internal static class PointScales
{
    /// <summary>How many bands a colour is drawn in; the stylesheet's heat and diverge tokens, as the surface's (ADR-0310).</summary>
    public const int BandCount = 9;

    private const double BarWidth = 12;

    /// <summary>The largest marker's radius, against a line's height.</summary>
    public static double MaxRadius(double lineHeight) => lineHeight * 0.6;

    public static bool Keyed(ChartSeries s) => s.ColourBy is not null || s.SizeBy is not null;

    /// <summary>The width of the column the keys take at the plot's right, or 0 when no visible series has one.</summary>
    public static double ColumnWidth(IEnumerable<ChartSeries> visible, ChartMetrics m)
    {
        var keyed = visible.Where(Keyed).ToList();
        if (keyed.Count == 0) return 0;
        var label = m.EffectiveTextWidth("-0,000.00");
        var titles = keyed.SelectMany(s => new[] { s.ColourBy?.Title, s.SizeBy?.Title }).OfType<string>().Max(t => m.EffectiveTextWidth(t));
        return m.Padding + Math.Max(titles, Math.Max(BarWidth, 2 * MaxRadius(m.LineHeight)) + m.Padding / 2 + label) + m.Padding;
    }

    /// <summary>The lowest and highest of a scale's values, or null when it has none.</summary>
    public static (double Lo, double Hi)? Range(IReadOnlyList<ChartValue?> values)
    {
        var present = values.OfType<ChartValue>().Select(v => v.ToDouble()).ToList();
        return present.Count == 0 ? null : (present.Min(), present.Max());
    }

    /// <summary>The run a colour's bands cover: the values' own, or alike on both sides of its centre (ADR-0310).</summary>
    public static (double From, double To) Run(PointColour c)
    {
        var (lo, hi) = Range(c.Values) ?? (c.Centre ?? 0, c.Centre ?? 0);
        if (c.Centre is { } mid)
        {
            var reach = Math.Max(Math.Abs(hi - mid), Math.Abs(lo - mid));
            if (reach == 0) reach = mid == 0 ? 1 : Math.Abs(mid) / 10;
            return (mid - reach, mid + reach);
        }
        return hi > lo ? (lo, hi) : (lo - (lo == 0 ? 1 : Math.Abs(lo) / 10), hi + (hi == 0 ? 1 : Math.Abs(hi) / 10));
    }

    /// <summary>A value's band: by its place in the run, or by its distance from the centre, the middle band neutral.</summary>
    public static int Band(PointColour c, double value)
    {
        var (from, to) = Run(c);
        if (c.Centre is { } mid)
        {
            var t = (value - mid) / ((to - from) / 2);
            var depth = Math.Min(BandCount / 2, (int)Math.Floor(Math.Abs(t) * BandCount / 2 + 0.5));
            return BandCount / 2 + Math.Sign(t) * depth;
        }
        return Math.Clamp((int)Math.Floor((value - from) / (to - from) * BandCount), 0, BandCount - 1);
    }

    /// <summary>A value's radius as a share of the largest: its area in proportion to the value.</summary>
    public static double Share(PointSize s, double value)
    {
        var max = Range(s.Values)?.Hi ?? 0;
        return max > 0 ? Math.Sqrt(value / max) : 1;
    }

    /// <summary>The keys of the visible series, stacked down the column at <paramref name="x"/>.</summary>
    public static (List<ColourKey> Colours, List<SizeKey> Sizes) Keys(IEnumerable<ChartSeries> visible, double x, double top, double height, ChartMetrics m)
    {
        var colours = new List<ColourKey>();
        var sizes = new List<SizeKey>();
        var lh = m.LineHeight;
        var y = top;
        foreach (var s in visible.Where(Keyed))
        {
            if (s.ColourBy is { } c)
            {
                var (from, to) = Run(c);
                var barTop = y + lh;
                var barHeight = Math.Max(4 * lh, Math.Min(10 * lh, height - (barTop - top) - lh));
                var box = new Box(x, barTop, BarWidth, barHeight);
                var (min, max, step) = TickLayout.Nice(from, to, 4);
                var values = TickLayout.Steps(min, max, step).Where(v => v >= from - 1e-9 * (to - from) && v <= to + 1e-9 * (to - from)).ToList();
                if (c.Centre is { } mid && !values.Contains(mid)) values.Add(mid);
                var ticks = values.Order().Select(v => new KeyTick(v, box.Y + box.Height * (to - v) / (to - from), Number(v))).ToEquatableList();
                colours.Add(new ColourKey(s.Id, c.Title, from, to, ticks, c.Centre is not null, box));
                y = barTop + barHeight + lh;
            }
            if (s.SizeBy is { } z && Range(z.Values) is { } r && r.Hi > 0)
            {
                var rMax = MaxRadius(lh);
                var (_, max, step) = TickLayout.Nice(0, r.Hi, 3);
                var values = TickLayout.Steps(step, max, step).Where(v => v <= r.Hi).TakeLast(3).ToList();
                if (values.Count == 0) values.Add(r.Hi);
                var entries = values.Select(v => new SizeEntry(v, Number(v), Math.Max(0.75, rMax * Share(z, v)))).ToEquatableList();
                var boxHeight = entries.Sum(e => Math.Max(2 * e.Radius, lh) + lh / 4);
                sizes.Add(new SizeKey(s.Id, z.Title, entries, new Box(x, y + lh, 2 * rMax, boxHeight)));
                y += lh + boxHeight + lh;
            }
        }
        return (colours, sizes);
    }

    /// <summary>What the Notes say of points a scale has no value for.</summary>
    public static IEnumerable<string> Notes(ChartSeries s)
    {
        foreach (var (title, values) in new[] { (s.ColourBy?.Title, s.ColourBy?.Values), (s.SizeBy?.Title, s.SizeBy?.Values) })
        {
            if (values is null) continue;
            var missing = s.Points.Where((p, i) => p.Y is not null && values[i] is null).Count();
            if (missing > 0)
                yield return $"{s.Name}: {missing} {(missing == 1 ? "point has" : "points have")} no {title}, drawn as {(missing == 1 ? "an outline" : "outlines")}.";
        }
    }

    /// <summary>What the definition refuses of a series' scales, or null.</summary>
    public static string? Refusal(ChartSeries s)
    {
        if (!Keyed(s)) return null;
        if (s.Kind != SeriesKind.Observation)
            return $"'{s.Name}' takes a colour or a size from a column, which belongs to marks drawn one a row: an observation.";
        if (s.ColourBy is { } c)
        {
            if (c.Values.Count != s.Points.Count)
                return $"'{s.Name}' has {s.Points.Count} points and {c.Values.Count} values of {c.Title}: its colour takes one a point.";
            if (c.Centre is { } mid && !double.IsFinite(mid))
                return $"'{s.Name}': the value its colours diverge from is a finite number.";
        }
        if (s.SizeBy is { } z)
        {
            if (z.Values.Count != s.Points.Count)
                return $"'{s.Name}' has {s.Points.Count} points and {z.Values.Count} values of {z.Title}: its size takes one a point.";
            for (var i = 0; i < z.Values.Count; i++)
                if (z.Values[i] is { } v && v.ToDouble() < 0)
                    return $"'{s.Name}' at {s.Points[i].Name ?? $"row {i + 1}"}: {z.Title} is {v.ToInvariantString()}, a negative size, which no marker can be drawn at.";
        }
        return null;
    }

    internal static string Number(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
