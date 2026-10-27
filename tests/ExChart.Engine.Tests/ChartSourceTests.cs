using System.Collections;
using ExChart.Engine;

namespace ExChart.Engine.Tests;

/// <summary>
/// The Chart Source's thinned pixels as the drawing says them (ADR-0322, ADR-0344). What the reference source keeps and what
/// a Chart Spec read through sources refuses are shared cases, <c>tests/cases/chart/source.json</c>.
/// </summary>
public class ChartSourceTests
{
    // 10,000 rows across 10,000 days, with a spike and a trough the thinning must keep, and one gap.
    private static ChartData Ticks()
    {
        var days = new ArrayList();
        var pnl = new ArrayList();
        for (var i = 0; i < 10_000; i++)
        {
            days.Add(DateOnly.FromDayNumber(700_000 + i));
            pnl.Add(i == 4_321 ? 999m : i == 6_789 ? -999m : i == 5_000 ? null : (object)(decimal)(i % 7));
        }
        return ChartData.Of(("day", days), ("pnl", pnl));
    }

    private static ChartSourceQuery Query(int pixels = 100, double? from = null, double? to = null) =>
        new("trades", "day", "pnl", AxisValues.Dates, from, to, pixels);

    [Fact] // ADR-0344: a pixel the drawing thins again counts every row under it, not the points the source kept
    public void A_pixel_thinned_again_counts_every_row_under_it()
    {
        var source = new List<PixelSummary> { new(0, 9, -1, 5, 1000), new(10, 19, 0, 7, 500) };
        var drawn = new List<PixelSummary> { new(0, 19, -1, 7, 8) };
        double[] kept = [0, 3, 5, 9, 10, 12, 15, 19];

        var said = Assert.Single(Thinning.Through(source, drawn, kept));

        Assert.Equal(new PixelSummary(0, 19, -1, 7, 1500), said);
        Assert.Equal(source, Thinning.Through(source, [], kept));
    }

    [Fact] // ADR-0344: a source pixel whose kept points fall in two drawn pixels joins them, counting no row twice
    public void A_source_pixel_across_two_drawn_pixels_joins_them()
    {
        var source = new List<PixelSummary> { new(5, 14, -3, 9, 100) };
        var drawn = new List<PixelSummary> { new(0, 9, -1, 4, 4), new(10, 19, 0, 6, 4), new(30, 39, 1, 2, 2) };
        double[] kept = [0, 3, 5, 9, 12, 14, 15, 19, 30, 39];

        var said = Thinning.Through(source, drawn, kept);

        // 100 rows under the source's pixel, and 0, 3, 15 and 19, each a row of its own.
        Assert.Equal([new PixelSummary(0, 19, -3, 9, 104), new PixelSummary(30, 39, 1, 2, 2)], said);
    }

    [Fact] // ADR-0344: the scene's tooltip says the source's count where the drawing did not thin again
    public void The_scene_says_the_source_pixels()
    {
        var answer = ChartSource.Thin(Ticks(), Query(pixels: 50));
        var series = new ChartSeries
        {
            Id = "p", Name = "P", Kind = SeriesKind.Level,
            Points = [.. Enumerable.Range(0, answer.Points.RowCount).Select(r => new ChartPoint(((DateOnly)answer.Points.Value("day", r)!).DayNumber,
                answer.Points.Value("pnl", r) is decimal m ? ChartValue.Of(m) : null))],
            Thinned = answer.Thinned,
        };
        var scene = ChartScene.Build(new ChartDefinition(new ChartAxis { Values = AxisValues.Dates }, [series]), new ChartMetrics(800, 400));
        var piece = scene.Panels.SelectMany(p => p.Series).Single();
        Assert.Equal(answer.Thinned.Sum(t => t.Count), piece.Thinned.Sum(t => t.Count));
    }
}
