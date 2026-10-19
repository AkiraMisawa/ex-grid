using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class ThinningTests
{
    [Fact] // ADR-0254: past the pixels, each pixel keeps first, last, min and max, so every peak stays
    public void Each_pixel_keeps_its_first_last_minimum_and_maximum()
    {
        var line = Enumerable.Range(0, 1000).Select(i => new DrawnPoint(i, i == 437 ? 99 : i == 612 ? -99 : Math.Sin(i))).ToList();
        var t = Thinning.Of(new SeriesDrawing([line], []), x => x / 10, 100);
        var kept = t.Drawing.Lines.Single();
        Assert.InRange(kept.Count, 100, 400);
        Assert.Contains(new DrawnPoint(437, 99), kept);
        Assert.Contains(new DrawnPoint(612, -99), kept);
        Assert.Equal(line[0], kept[0]);
        Assert.Equal(line[^1], kept[^1]);
        Assert.Equal(100, t.Thinned.Count);
        Assert.Equal(10, t.Thinned[43].Count);
        Assert.Equal(99, t.Thinned[43].Max);
    }

    [Fact] // ADR-0254: a drawing within the pixels is left as it is
    public void Nothing_is_thinned_within_the_pixels()
    {
        var d = new SeriesDrawing([[new(0, 1), new(1, 2)]], [new DrawnMarker(0, 1, true)]);
        Assert.Same(d, Thinning.Of(d, x => x, 100).Drawing);
    }

    [Fact] // ADR-0254: a step is thinned by its levels, with no end at a level it does not take there
    public void A_step_is_thinned_to_its_lowest_and_highest_level_per_pixel()
    {
        var levels = Enumerable.Range(0, 100).Select(i => (IReadOnlyList<DrawnPoint>)[new(i, i % 3), new(i + 1, i % 3)]).ToList();
        var t = Thinning.Of(new SeriesDrawing(levels, []), x => x / 10, 10);
        Assert.Empty(t.Drawing.Markers);
        Assert.All(t.Drawing.Lines, l => Assert.Equal(l[0].Y, l[1].Y));
        Assert.All(t.Thinned, p => Assert.Equal((0, 2), (p.Min, p.Max)));
    }
}
