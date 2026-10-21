using System.Collections;
using ExChart.Engine;

namespace ExChart.Engine.Tests;

/// <summary>The Chart Source: thinned where the data lives, and a Chart Spec read through it (ADR-0322, ADR-0344).</summary>
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

    private static IEnumerable<(object? X, object? Y)> Rows(ChartSourceAnswer answer) =>
        Enumerable.Range(0, answer.Points.RowCount).Select(r => (answer.Points.Value("day", r), answer.Points.Value("pnl", r)));

    [Fact] // ADR-0344 / ADR-0254: each pixel keeps its first, last, lowest and highest row, so the spike and the trough stay
    public void Each_pixel_keeps_its_first_last_lowest_and_highest_row()
    {
        var answer = ChartSource.Thin(Ticks(), Query(), version: 3);

        Assert.Equal(3, answer.Version);
        var rows = Rows(answer).ToList();
        Assert.InRange(rows.Count, 100, 401);
        Assert.Contains((DateOnly.FromDayNumber(704_321), 999m), rows);
        Assert.Contains((DateOnly.FromDayNumber(706_789), -999m), rows);
        Assert.Equal(DateOnly.FromDayNumber(700_000), rows[0].X);
        Assert.Equal(DateOnly.FromDayNumber(709_999), rows[^1].X);
        var spike = answer.Thinned.Single(t => t.From <= 704_321 && 704_321 <= t.To);
        Assert.Equal(999, spike.Max);
        Assert.Equal(0, spike.Min);
        // Every row is counted once: under the pixel that thinned it, or kept on its own.
        var alone = rows.Count(r => !answer.Thinned.Any(t => t.From <= ((DateOnly)r.X!).DayNumber && ((DateOnly)r.X!).DayNumber <= t.To));
        Assert.Equal(10_000, answer.Thinned.Sum(t => t.Count) + alone);
    }

    [Fact] // ADR-0344 / spine rule 1: a row with no value is kept, so a gap stays a gap
    public void A_row_with_no_value_is_kept_where_it_is()
    {
        var rows = Rows(ChartSource.Thin(Ticks(), Query())).ToList();
        var gap = rows.FindIndex(r => r.Y is null);
        Assert.Equal(DateOnly.FromDayNumber(705_000), rows[gap].X);
        Assert.True((DateOnly)rows[gap - 1].X! < DateOnly.FromDayNumber(705_000));
        Assert.True((DateOnly)rows[gap + 1].X! > DateOnly.FromDayNumber(705_000));
    }

    [Fact] // ADR-0344: a range asked for is the rows in it, at full detail; within the pixels nothing is thinned
    public void A_narrow_range_is_answered_whole()
    {
        var answer = ChartSource.Thin(Ticks(), Query(pixels: 2048, from: 704_300, to: 704_340));

        Assert.Empty(answer.Thinned);
        Assert.Equal(41, answer.Points.RowCount);
        Assert.Equal(DateOnly.FromDayNumber(704_300), answer.Points.Value("day", 0));
    }

    [Fact] // ADR-0344: rows out of order are answered in order across
    public void Rows_are_answered_in_order_across()
    {
        var data = ChartData.Of(("x", new ArrayList { 3.0, 1.0, 2.0 }), ("y", new ArrayList { 30m, 10m, 20m }));
        var answer = ChartSource.Thin(data, new ChartSourceQuery("t", "x", "y", AxisValues.Numbers, null, null, 10));
        Assert.Equal([1.0, 2.0, 3.0], Enumerable.Range(0, 3).Select(r => (double)answer.Points.Value("x", r)!));
    }

    [Fact] // ADR-0344: a labels axis needs every category, and is not asked of a Chart Source
    public void A_labels_axis_is_not_thinned() =>
        Assert.Throws<ArgumentException>(() => ChartSource.Thin(Ticks(), Query() with { Values = AxisValues.Labels }));

    private const string Spec = """
        {"version": 1, "axis": {"values": "dates"}, "series": [
          {"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}
        """;

    [Fact] // ADR-0344: a spec read through a Chart Source draws the kept points and carries what each thinned pixel held
    public async Task A_spec_read_through_a_source_carries_its_thinned_pixels()
    {
        var asked = new List<ChartSourceQuery>();
        var source = new Recording(ChartSource.Over(Ticks(), version: 5), asked);

        var read = await ChartSpec.ReadAsync(Spec, name => name == "trades" ? source : null, 100, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new ChartSourceQuery("trades", "day", "pnl", AxisValues.Dates, null, null, 100), Assert.Single(asked));
        var series = Assert.Single(read.Reading.Definition.Series);
        Assert.NotEmpty(series.Thinned);
        Assert.InRange(series.Points.Count, 100, 401);
        Assert.Equal(5, read.Versions["trades"]);
    }

    [Theory] // ADR-0344: what a thinned point cannot stand for is refused by path, before any source is asked
    [InlineData("""{"id": "s", "name": "S", "kind": "observation", "data": "trades", "x": "day", "y": "pnl"}""", "series[0].kind")]
    [InlineData("""{"id": "s", "name": "S", "kind": "level", "data": "trades", "x": "day", "y": "pnl", "label": "day"}""", "series[0].label")]
    [InlineData("""{"id": "s", "name": "S", "kind": "priceRange", "data": "trades", "x": "day", "y": "pnl"}""", "series[0].kind")]
    [InlineData("""{"id": "s", "name": "S", "kind": "level", "data": "trades", "x": "day"}""", "series[0].y")]
    [InlineData("""{"id": "s", "name": "S", "kind": "level", "data": "trades", "x": "day", "y": "pnl", "colour": {"column": "pnl", "scale": "sequential"}}""", "series[0].colour")]
    public async Task What_a_thinned_point_cannot_stand_for_is_refused_by_path(string series, string path)
    {
        var asked = new List<ChartSourceQuery>();
        var source = new Recording(ChartSource.Over(Ticks()), asked);
        var refused = await Assert.ThrowsAsync<ChartSpecRefusedException>(() => ChartSpec.ReadAsync(
            $$"""{"version": 1, "axis": {"values": "dates"}, "series": [{{series}}]}""", _ => source, 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(path, refused.Path);
        Assert.Empty(asked);
    }

    [Fact] // ADR-0344: a labels axis is refused by path
    public async Task A_labels_axis_is_refused_by_path()
    {
        var refused = await Assert.ThrowsAsync<ChartSpecRefusedException>(() => ChartSpec.ReadAsync(
            Spec.Replace("\"dates\"", "\"labels\"", StringComparison.Ordinal), _ => ChartSource.Over(Ticks()), 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("axis.values", refused.Path);
    }

    [Fact] // ADR-0344 / spine rule 6: two series of one data answered at different versions are never drawn together
    public async Task Two_series_answered_at_different_versions_are_refused()
    {
        var versions = new Queue<long>([1, 2]);
        var source = new Versioned(Ticks(), versions);
        var spec = """
            {"version": 1, "axis": {"values": "dates"}, "series": [
              {"id": "a", "name": "A", "kind": "level", "data": "trades", "x": "day", "y": "pnl"},
              {"id": "b", "name": "B", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}
            """;
        var refused = await Assert.ThrowsAsync<ChartSpecRefusedException>(() => ChartSpec.ReadAsync(spec, _ => source, 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("series[1].data", refused.Path);
    }

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

    private sealed class Recording(IChartSource inner, List<ChartSourceQuery> asked) : IChartSource
    {
        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default)
        {
            asked.Add(query);
            return inner.ThinAsync(query, cancellationToken);
        }
    }

    private sealed class Versioned(IChartData data, Queue<long> versions) : IChartSource
    {
        public ValueTask<ChartSourceAnswer> ThinAsync(ChartSourceQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ChartSource.Thin(data, query, versions.Dequeue()));
    }
}
