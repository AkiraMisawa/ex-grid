using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class PriceRangeTests
{
    private static readonly ChartMetrics Wide = new(1200, 400);

    private static double Day(int y, int m, int d) => new DateOnly(y, m, d).DayNumber;

    private static PriceBar Bar(double x, decimal o, decimal h, decimal l, decimal c, decimal? v = null) => new(x, o, h, l, c, v);

    // Weekdays from a Monday, a fixed walk: open, high, low, close always agree.
    private static List<PriceBar> Days(int count, double start = 0, bool volume = false)
    {
        var bars = new List<PriceBar>();
        var x = start == 0 ? Day(2026, 1, 5) : start;
        var price = 100m;
        for (var i = 0; i < count; i++)
        {
            while (DateOnly.FromDayNumber((int)x).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) x++;
            var close = price + (i % 7 - 3);
            bars.Add(Bar(x, price, Math.Max(price, close) + 2, Math.Min(price, close) - 2, close, volume ? 1000 + i : null));
            price = close;
            x++;
        }
        return bars;
    }

    private static ChartDefinition Chart(IEnumerable<PriceBar> bars, ChartAxis? axis = null, params ChartSeries[] more) =>
        new(axis ?? ChartAxis.Dates(), [ChartSeries.Candles("idx", "Index", bars), .. more]);

    [Fact] // ADR-0300: a row whose high is below its open is refused, naming the row and the values
    public void A_high_below_the_open_is_refused_by_its_row()
    {
        var e = Assert.Throws<ChartRefusedException>(() => Chart([Bar(Day(2026, 10, 14), 101, 99, 98, 100)]));
        Assert.Contains("2026-10-14", e.Message);
        Assert.Contains("high 99 is below open 101", e.Message);
    }

    [Theory] // ADR-0300: low <= min(open, close) and max(open, close) <= high, never repaired
    [InlineData(100, 105, 101, 103)] // low above open
    [InlineData(100, 105, 95, 106)] // close above high
    [InlineData(100, 105, 95, 94)] // close below low
    public void Values_that_disagree_are_refused(int o, int h, int l, int c) =>
        Assert.Throws<ChartRefusedException>(() => Chart([Bar(1, o, h, l, c)]));

    [Fact] // ADR-0300: a row missing any of its four values is a gap, never a partial candle
    public void A_row_missing_a_value_is_a_gap()
    {
        var bars = Days(3);
        bars[1] = bars[1] with { Close = null };
        var d = Chart(bars);
        var scene = ChartScene.Build(d, Wide);
        var candles = scene.Panels[0].Series[0].Candles;
        Assert.Equal(2, candles.Count);
        Assert.Null(d.Series[0].Points[1].Y);
        var reading = ChartReadings.At(d, scene, Wide, ChartView.All, bars[1].X, 0, 0);
        Assert.Equal("(no value)", reading.Lines[0].Note);
    }

    [Fact] // ADR-0300: a series with no open at all is High-Low-Close, without a body
    public void Without_an_open_column_a_bar_is_high_low_close()
    {
        var bars = Days(3).Select(b => b with { Open = null });
        var scene = ChartScene.Build(Chart(bars), Wide);
        Assert.All(scene.Panels[0].Series[0].Candles, c => Assert.Null(c.OpenY));
    }

    [Fact] // ADR-0300: negative prices are drawn, not refused (WTI, April 2020)
    public void Negative_prices_are_drawn() =>
        Assert.Single(ChartScene.Build(Chart([Bar(Day(2020, 4, 20), 17.73m, 17.85m, -40.32m, -37.63m)]), Wide).Panels[0].Series[0].Candles);

    [Fact] // ADR-0300: rising is the close above its own open, never against the previous close
    public void Rising_is_judged_against_its_own_open()
    {
        // The second closes above the first's close, but below its own open: falling.
        var scene = ChartScene.Build(Chart([Bar(Day(2026, 1, 5), 100, 106, 99, 105), Bar(Day(2026, 1, 6), 110, 111, 106, 107), Bar(Day(2026, 1, 7), 107, 108, 106, 107)]), Wide);
        var c = scene.Panels[0].Series[0].Candles;
        Assert.True(c[0].Rising);
        Assert.False(c[1].Rising);
        Assert.False(c[1].Unchanged);
        Assert.True(c[2].Unchanged);
    }

    [Fact] // ADR-0300: a price axis does not reach zero, and says so with the break mark
    public void The_value_axis_leaves_out_zero()
    {
        var scene = ChartScene.Build(Chart([Bar(1, 39_120, 39_480, 38_950, 39_310), Bar(2, 39_310, 40_000, 39_000, 39_900)], ChartAxis.Dates()), Wide);
        var axis = scene.Panels[0].Left!;
        Assert.True(axis.Min > 30_000);
        Assert.True(axis.Broken);
    }

    [Fact] // ADR-0300: a Price Range stands on a Dates axis
    public void A_price_range_is_refused_off_a_dates_axis() =>
        Assert.Throws<ChartRefusedException>(() => Chart(Days(2), ChartAxis.Numbers()));

    [Fact] // ADR-0300: volume gets a panel of its own below, undivided and from zero
    public void Volume_is_drawn_in_a_panel_below_from_zero()
    {
        var bars = Days(10, volume: true);
        var scene = ChartScene.Build(Chart(bars), Wide);
        Assert.Equal(2, scene.Panels.Count);
        var volume = scene.Panels[1];
        Assert.Equal("idx", volume.VolumeOf);
        Assert.Equal(0, volume.Left!.Min);
        Assert.True(volume.Plot.Height < scene.Panels[0].Plot.Height);
        Assert.Equal(10, volume.Series[0].Volumes.Count);
        // Monday's volume is not spread over the weekend: the tallest bar is the largest volume.
        var tallest = volume.Series[0].Volumes.MinBy(v => v.TopY);
        Assert.Equal(volume.Series[0].Volumes[^1], tallest);
        Assert.Equal(scene.Panels[0].Series[0].Candles.Select(c => c.Rising), volume.Series[0].Volumes.Select(v => v.Rising));
    }

    [Fact] // ADR-0300: the tooltip gives the four values, the change from the previous close and the volume
    public void The_tooltip_reads_a_candle()
    {
        var d = Chart([Bar(Day(2026, 10, 13), 39_000, 39_200, 38_900, 39_100, 5), Bar(Day(2026, 10, 14), 39_120, 39_480, 38_950, 39_310, 7)]);
        var scene = ChartScene.Build(d, Wide);
        var line = ChartReadings.At(d, scene, Wide, ChartView.All, Day(2026, 10, 14), 0, 0).Lines[0];
        Assert.Equal("O 39120 H 39480 L 38950 C 39310", line.Value);
        Assert.Contains("Chg +210 (+0.54%)", line.Note);
        Assert.Contains("Vol 7", line.Note);
        var first = ChartReadings.At(d, scene, Wide, ChartView.All, Day(2026, 10, 13), 0, 0).Lines[0];
        Assert.Contains("Chg —", first.Note);
    }

    [Fact] // ADR-0302: past three device pixels a candle, candles merge into weeks, months, ...
    public void Too_many_candles_merge_into_the_markets_periods()
    {
        var bars = Days(2_450, volume: true);
        var d = Chart(bars);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        var piece = scene.Panels[0].Series[0];
        Assert.NotEqual(CandlePeriod.Row, piece.Period);
        Assert.True(piece.Candles.Count * 3 <= 600);
        Assert.Contains("merged from rows to fit", scene.XTitle);
        // A merged candle is the period's first open, highest high, lowest low and last close.
        var months = PriceRangesProbe.Months(bars);
        var first = piece.Candles[0];
        Assert.Equal(months[0].Count, first.Rows);
        // The CSV keeps every row.
        Assert.Equal(2_450 + 1, ChartCsv.Wide(d, new ChartMetrics(600, 400)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact] // ADR-0302: a merged candle's values are the period's first open, highs and lows, last close, summed volume
    public void A_weekly_candle_is_the_weeks_bar()
    {
        var bars = Days(5, volume: true); // Monday to Friday
        var c = PriceRangesProbe.Merge(bars, CandlePeriod.Week).Single();
        Assert.Equal(bars[0].Open!.Value.ToDouble(), c.Open);
        Assert.Equal(bars.Max(b => b.High!.Value.ToDouble()), c.High);
        Assert.Equal(bars.Min(b => b.Low!.Value.ToDouble()), c.Low);
        Assert.Equal(bars[^1].Close!.Value.ToDouble(), c.Close);
        Assert.Equal(bars.Sum(b => b.Volume!.Value.ToDouble()), c.Volume);
        Assert.Equal(5, c.Rows);
    }

    [Fact] // ADR-0302: narrowing the range brings the rows back
    public void Narrowing_returns_the_rows()
    {
        var d = Chart(Days(2_450));
        var view = new ChartView(new HashSet<string>(), Day(2026, 1, 5), Day(2026, 3, 31));
        Assert.Equal(CandlePeriod.Row, ChartScene.Build(d, new ChartMetrics(600, 400), view).Panels[0].Series[0].Period);
    }

    [Fact] // ADR-0302: a fixed period that does not fit draws nothing and says so
    public void A_fixed_period_that_does_not_fit_says_so()
    {
        var d = new ChartDefinition(ChartAxis.Dates(), [ChartSeries.Candles("idx", "Index", Days(2_450)) with { MergeTo = CandlePeriod.Row }]);
        var scene = ChartScene.Build(d, new ChartMetrics(600, 400));
        Assert.Empty(scene.Panels[0].Series[0].Candles);
        Assert.Contains(scene.Notes, n => n.Contains("2,450 rows do not fit; narrow the range"));
    }

    [Fact] // ADR-0301: a packed axis gives each candle an even slot and says so in its title
    public void A_packed_axis_spaces_the_sessions_evenly()
    {
        var d = Chart(Days(10), ChartAxis.Dates("Date") with { Packed = true });
        var scene = ChartScene.Build(d, Wide);
        var xs = scene.Panels[0].Series[0].Candles.Select(c => c.X).ToList();
        var gaps = xs.Zip(xs.Skip(1), (a, b) => Math.Round(b - a, 6)).Distinct().ToList();
        Assert.Single(gaps); // Friday to Monday is one slot, like any other
        Assert.Contains("closed days packed", scene.XTitle);
        Assert.All(scene.XTicks, t => Assert.True(double.IsFinite(t.X)));
        Assert.NotEmpty(scene.XTicks);
    }

    [Fact] // ADR-0302: a period the Consumer fixed is not said to be merged to fit
    public void A_fixed_period_is_not_said_to_fit()
    {
        var d = new ChartDefinition(ChartAxis.Dates(), [ChartSeries.Candles("idx", "Index", Days(30)) with { MergeTo = CandlePeriod.Week }]);
        var scene = ChartScene.Build(d, Wide);
        Assert.Contains("Weekly (merged from rows)", scene.XTitle);
        Assert.Contains("Index: weekly candles, merged from rows.", scene.Notes);
    }

    [Fact] // ADR-0302: when not even years fit, nothing is drawn in slivers
    public void When_years_do_not_fit_nothing_is_drawn()
    {
        var d = Chart(Days(2_450));
        var scene = ChartScene.Build(d, new ChartMetrics(80, 400));
        Assert.Empty(scene.Panels[0].Series[0].Candles);
        Assert.Contains(scene.Notes, n => n.Contains("yearly candles do not fit"));
    }

    [Fact] // ADR-0301: a packed axis gives a slot only to a row with a value
    public void A_packed_axis_skips_rows_without_a_value()
    {
        var bars = Days(5);
        bars[2] = new PriceBar(bars[2].X, null, null, null, null);
        var scene = ChartScene.Build(Chart(bars, ChartAxis.Dates() with { Packed = true }), Wide);
        var xs = scene.Panels[0].Series[0].Candles.Select(c => c.X).ToList();
        Assert.Equal(4, xs.Count);
        Assert.Single(xs.Zip(xs.Skip(1), (a, b) => Math.Round(b - a, 6)).Distinct());
        Assert.Contains("Trading days (closed days packed)", scene.XTitle);
    }

    [Fact] // ADR-0301: by default candles stand on calendar time, so a weekend is a gap
    public void By_default_a_weekend_is_a_gap()
    {
        var scene = ChartScene.Build(Chart(Days(10)), Wide);
        var xs = scene.Panels[0].Series[0].Candles.Select(c => c.X).ToList();
        Assert.True(xs[5] - xs[4] > 2.5 * (xs[1] - xs[0]));
    }

    [Fact] // ADR-0301: on a packed axis a point where no candle stands is refused by name
    public void A_packed_axis_refuses_a_point_off_the_sessions()
    {
        var saturday = Day(2026, 1, 10);
        var ma = ChartSeries.Of("ma", "MA", SeriesKind.Level, [new ChartPoint(saturday, 100m)]);
        var e = Assert.Throws<ChartRefusedException>(() => Chart(Days(10), ChartAxis.Dates() with { Packed = true }, ma));
        Assert.Contains("2026-01-10", e.Message);
    }
}

internal static class PriceRangesProbe
{
    public static List<DrawnCandle> Merge(IEnumerable<PriceBar> bars, CandlePeriod period) =>
        PriceRanges.Candles(ChartSeries.Candles("p", "P", bars), double.NegativeInfinity, double.PositiveInfinity, period);

    public static List<List<PriceBar>> Months(IEnumerable<PriceBar> bars) =>
        bars.GroupBy(b => DateOnly.FromDayNumber((int)b.X) is var d ? (d.Year, d.Month) : default).Select(g => g.ToList()).ToList();
}
