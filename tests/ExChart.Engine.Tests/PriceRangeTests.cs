using ExChart.Engine;

namespace ExChart.Engine.Tests;

// The rows' refusals, the candles and their merging are shared cases (tests/cases/chart/price-ranges.json,
// ADR-0370); the tooltip, the summary and the files stay in C# until they are ported.

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

    [Fact] // ADR-0300: a row missing any of its four values reads as a gap
    public void A_row_missing_a_value_reads_as_a_gap()
    {
        var bars = Days(3);
        bars[1] = bars[1] with { Close = null };
        var d = Chart(bars);
        var scene = ChartScene.Build(d, Wide);
        var reading = ChartReadings.At(d, scene, Wide, ChartView.All, bars[1].X, 0, 0);
        Assert.Equal("(no value)", reading.Lines[0].Note);
    }

    [Fact] // ADR-0300, ADR-0275: the volume panel's summary says the volume, not the prices again
    public void The_summary_of_the_volume_panel_says_its_volume()
    {
        var bars = Days(10, volume: true);
        var definition = Chart(bars);
        var scene = ChartScene.Build(definition, Wide);
        var summary = ChartSummary.Of(definition, scene, ChartView.All);
        Assert.Matches($@"Index volume: maximum 1,?009 at {definition.Axis.Write(bars[^1].X)}\.", summary);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(summary, @"Index: maximum"));
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

    [Fact] // ADR-0302: where candles merge, the CSV keeps every row
    public void Merged_candles_keep_every_row_in_the_csv()
    {
        var d = Chart(Days(2_450, volume: true));
        Assert.Equal(2_450 + 1, ChartCsv.Wide(d, new ChartMetrics(600, 400)).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

}
