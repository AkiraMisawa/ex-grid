namespace ExChart.Engine;

/// <summary>
/// An edge between two nodes of an Observation, each named by its point's <see cref="ChartPoint.Row"/>
/// (ADR-0321): drawn as a line between their positions, with no position or look of its own.
/// </summary>
/// <param name="From">The Row Key of the node it starts at.</param>
/// <param name="To">The Row Key of the node it ends at.</param>
public sealed record ChartEdge(object From, object To);

/// <summary>An edge in CSS pixels, cut at the plot's edges.</summary>
/// <param name="X1">Where it starts across.</param>
/// <param name="Y1">Where it starts down.</param>
/// <param name="X2">Where it ends across.</param>
/// <param name="Y2">Where it ends down.</param>
public readonly record struct DrawnEdge(double X1, double Y1, double X2, double Y2);

internal static class Edges
{
    /// <summary>What the definition refuses of a series' edges, or null.</summary>
    public static string? Refusal(ChartSeries s)
    {
        if (s.Edges.Count == 0) return null;
        if (s.Kind != SeriesKind.Observation)
            return $"'{s.Name}' has edges, which join an observation's nodes; a {s.Kind} joins its points itself.";
        var keys = new HashSet<object>();
        for (var i = 0; i < s.Points.Count; i++)
        {
            var p = s.Points[i];
            if (p.Row is null)
                return $"'{s.Name}' has edges, but its point at row {i + 1} has no key for an edge to name.";
            if (!keys.Add(p.Row))
                return $"'{s.Name}' has two nodes keyed {p.Row}; an edge could not say which it joins.";
            if (p.Y is null || !double.IsFinite(p.X))
                return $"'{s.Name}': node {p.Row} has no position, so no edge can reach it.";
        }
        foreach (var e in s.Edges)
            foreach (var end in new[] { e.From, e.To })
                if (!keys.Contains(end))
                    return $"'{s.Name}' has an edge from {e.From} to {e.To}, but no node is keyed {end}.";
        return null;
    }

    /// <summary>The series' edges in pixels, each cut to the plot; an edge wholly outside it is left out.</summary>
    public static IEnumerable<DrawnEdge> Pixels(ChartSeries s, Func<double, double> mapX, Func<double, double> mapY, Box plot)
    {
        if (s.Edges.Count == 0) yield break;
        var at = new Dictionary<object, (double X, double Y)>();
        foreach (var p in s.Points)
            at[p.Row!] = (mapX(p.X), mapY(p.Y!.Value.ToDouble()));
        foreach (var e in s.Edges)
        {
            var (a, b) = (at[e.From], at[e.To]);
            if (Clip(a.X, a.Y, b.X, b.Y, plot) is { } cut) yield return cut;
        }
    }

    // Liang–Barsky: the part of the segment inside the box, or null.
    private static DrawnEdge? Clip(double x1, double y1, double x2, double y2, Box box)
    {
        double t0 = 0, t1 = 1, dx = x2 - x1, dy = y2 - y1;
        foreach (var (p, q) in new[] { (-dx, x1 - box.X), (dx, box.X + box.Width - x1), (-dy, y1 - box.Y), (dy, box.Y + box.Height - y1) })
        {
            if (p == 0)
            {
                if (q < 0) return null;
                continue;
            }
            var r = q / p;
            if (p < 0) t0 = Math.Max(t0, r);
            else t1 = Math.Min(t1, r);
            if (t0 > t1) return null;
        }
        return new DrawnEdge(x1 + t0 * dx, y1 + t0 * dy, x1 + t1 * dx, y1 + t1 * dy);
    }
}
