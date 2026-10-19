namespace ExChart.Engine;

/// <summary>What one thinned device pixel holds, for the tooltip to say (ADR-0254): never one point as if it were the only one.</summary>
/// <param name="From">The first position in the pixel.</param>
/// <param name="To">The last.</param>
/// <param name="Min">The lowest height.</param>
/// <param name="Max">The highest.</param>
/// <param name="Count">How many points the pixel holds.</param>
public sealed record PixelSummary(double From, double To, double Min, double Max, int Count);

/// <summary>A series' drawing after Thinning, and what each thinned pixel holds.</summary>
/// <param name="Drawing">What is drawn.</param>
/// <param name="Thinned">One entry per pixel that held more than one point; empty when nothing was thinned.</param>
public sealed record ThinnedDrawing(SeriesDrawing Drawing, IReadOnlyList<PixelSummary> Thinned);

/// <summary>
/// Thinning (ADR-0254): past the plot's device pixels, each pixel column keeps a line's first, last,
/// lowest and highest point, so the drawn shape is the full series' at that width, every peak and
/// trough kept. It runs on the drawing, never on the data: the CSV and the files keep every point.
/// </summary>
public static class Thinning
{
    /// <summary>
    /// Thins <paramref name="drawing"/> for a plot where <paramref name="toPixel"/> gives a position's
    /// device pixel column. A drawing with no more points than pixels is returned as it is.
    /// </summary>
    public static ThinnedDrawing Of(SeriesDrawing drawing, Func<double, double> toPixel, int pixels)
    {
        var points = drawing.Lines.Sum(l => l.Count);
        if (points <= pixels) return new ThinnedDrawing(drawing, []);
        var summaries = new List<PixelSummary>();
        var steps = drawing.Lines.Count > 0 && drawing.Lines.All(l => l.Count == 2 && l[0].Y == l[1].Y);
        var lines = steps ? StepLevels(drawing.Lines, toPixel, summaries) : drawing.Lines.Select(l => Line(l, toPixel, summaries)).ToList();
        // Past one point a pixel, a marker stands for no one point: markers go, the shape stays.
        return new ThinnedDrawing(new SeriesDrawing(lines, []), summaries);
    }

    private static IReadOnlyList<DrawnPoint> Line(IReadOnlyList<DrawnPoint> line, Func<double, double> toPixel, List<PixelSummary> summaries)
    {
        var kept = new List<DrawnPoint>();
        var i = 0;
        while (i < line.Count)
        {
            var column = Math.Floor(toPixel(line[i].X));
            var j = i;
            while (j + 1 < line.Count && Math.Floor(toPixel(line[j + 1].X)) == column) j++;
            if (j == i) { kept.Add(line[i]); i++; continue; }
            var lo = i; var hi = i;
            for (var k = i; k <= j; k++)
            {
                if (line[k].Y < line[lo].Y) lo = k;
                if (line[k].Y > line[hi].Y) hi = k;
            }
            foreach (var k in new SortedSet<int> { i, lo, hi, j }) kept.Add(line[k]);
            summaries.Add(new PixelSummary(line[i].X, line[j].X, line[lo].Y, line[hi].Y, j - i + 1));
            i = j + 1;
        }
        return kept;
    }

    // A step thinned by its levels: each pixel column draws its lowest and its highest level across
    // the column, so no end is drawn at a level the function does not take there.
    private static List<IReadOnlyList<DrawnPoint>> StepLevels(IReadOnlyList<IReadOnlyList<DrawnPoint>> levels, Func<double, double> toPixel, List<PixelSummary> summaries)
    {
        var result = new List<IReadOnlyList<DrawnPoint>>();
        foreach (var group in levels.GroupBy(l => Math.Floor(toPixel((l[0].X + l[1].X) / 2))))
        {
            var g = group.ToList();
            if (g.Count == 1) { result.Add(g[0]); continue; }
            var a = g.Min(l => l[0].X);
            var b = g.Max(l => l[1].X);
            var min = g.Min(l => l[0].Y);
            var max = g.Max(l => l[0].Y);
            result.Add([new DrawnPoint(a, min), new DrawnPoint(b, min)]);
            if (max != min) result.Add([new DrawnPoint(a, max), new DrawnPoint(b, max)]);
            summaries.Add(new PixelSummary(a, b, min, max, g.Count));
        }
        return result;
    }
}
