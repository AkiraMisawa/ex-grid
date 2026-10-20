using System.Globalization;

namespace ExChart.Engine;

/// <summary>
/// The chart said in words, for a screen reader (ADR-0275): generated from the definition, so a chart
/// nobody described still says what it is.
/// </summary>
public static class ChartSummary
{
    /// <summary>
    /// The chart's name, the horizontal axis with its range, each panel's vertical axes with theirs,
    /// and each visible series with its maximum, written through its format, then the Consumer's
    /// description.
    /// </summary>
    public static string Of(ChartDefinition definition, ChartScene scene, ChartView view, string? title = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(view);
        var axis = definition.Axis;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(title)) parts.Add(title.Trim().TrimEnd('.') + ".");
        parts.Add($"{ChartCsv.Title(axis)}, {axis.Write(scene.XFrom)} to {axis.Write(scene.XTo)}.");
        var byId = definition.Series.ToDictionary(s => s.Id);
        foreach (var panel in scene.Panels)
        {
            var named = scene.Panels.Count > 1 && panel.Name.Length > 0 ? panel.Name + ": " : "";
            foreach (var v in new[] { panel.Left, panel.Right }.OfType<VerticalAxis>())
                parts.Add($"{named}{(v.Side == AxisSide.Left ? "Left" : "Right")} axis, {Number(v.Min)} to {Number(v.Max)}.");
            foreach (var piece in panel.Series)
                parts.Add(Series(byId[piece.Id], axis, scene));
        }
        if (!string.IsNullOrWhiteSpace(description)) parts.Add(description.Trim());
        return string.Join(" ", parts);
    }

    private static string Series(ChartSeries s, ChartAxis axis, ChartScene scene)
    {
        if (s.Kind == SeriesKind.Observation)
        {
            var seen = s.Points.Where(p => p.Y is not null && p.X >= scene.XFrom && p.X <= scene.XTo).ToList();
            if (seen.Count == 0) return $"{s.Name}: no point in range.";
            var low = seen.MinBy(p => p.Y!.Value.ToDouble());
            var high = seen.MaxBy(p => p.Y!.Value.ToDouble());
            return $"{s.Name}: {seen.Count} points, {axis.Write(seen.Min(p => p.X))} to {axis.Write(seen.Max(p => p.X))} across, {s.Write(low.Y!.Value)} to {s.Write(high.Y!.Value)} up.";
        }
        IEnumerable<ChartPoint> points = s.Sampler is { } f
            ? scene.Positions.Where(p => p.Position >= f.From && p.Position <= f.To).Select(p => new ChartPoint(p.Position, f.ValueAt(p.Position)))
            : s.Points.Where(p => p.X >= scene.XFrom && p.X <= scene.XTo);
        var valued = points.Where(p => p.Y is not null).ToList();
        if (valued.Count == 0) return $"{s.Name}: no value in range.";
        var max = valued.MaxBy(p => p.Y!.Value.ToDouble());
        return $"{s.Name}: maximum {s.Write(max.Y!.Value)} at {axis.Write(max.X)}.";
    }

    private static string Number(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
