using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class CsvTests
{
    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);
    private static ChartPoint M(double x, decimal y) => new(x, ChartValue.Of(y));
    private static readonly ChartMetrics Metrics = new(800, 400);

    [Fact] // ticket 07: invariant, every digit, a byte-order mark, a missing value empty and never zero
    public void Long_form_keeps_every_digit_and_leaves_a_missing_value_empty()
    {
        var day = new DateOnly(2026, 10, 19).DayNumber;
        var d = new ChartDefinition(ChartAxis.Dates(), [ChartSeries.Of("e", "EPE, Before", SeriesKind.Level, [M(day, 1234567.89m), P(day + 1, null)])]);
        var csv = ChartCsv.Long(d, Metrics);
        Assert.StartsWith("﻿Series,Date,Value,Kind,Method\r\n", csv);
        Assert.Contains("\"EPE, Before\",2026-10-19,1234567.89,quoted,\r\n", csv);
        Assert.Contains("\"EPE, Before\",2026-10-20,,quoted,\r\n", csv);
    }

    [Fact] // ticket 07: what the chart shows — hidden series and positions outside the range are left out
    public void The_csv_follows_the_chart_filters()
    {
        var d = new ChartDefinition(ChartAxis.Numbers("Years"),
        [
            ChartSeries.Of("a", "A", SeriesKind.Level, [P(0, 1), P(5, 2), P(10, 3)]),
            ChartSeries.Of("b", "B", SeriesKind.Level, [P(0, 4)]),
        ]);
        var csv = ChartCsv.Wide(d, Metrics, new ChartView(new HashSet<string> { "b" }, To: 5));
        Assert.Equal("﻿Years,A\r\n0,1\r\n5,2\r\n", csv);
    }

    [Fact] // ADR-0254: the CSV carries every point, never the thinned ones
    public void Every_point_is_written_whatever_the_width()
    {
        var d = new ChartDefinition(ChartAxis.Numbers(), [ChartSeries.Of("a", "A", SeriesKind.Level, Enumerable.Range(0, 5000).Select(i => P(i, i)))]);
        Assert.Equal(5002, ChartCsv.Wide(d, Metrics with { Width = 200 }).Split("\r\n").Length);
    }

    [Fact] // ADR-0257 (Q5): the long form says quoted, interpolated or extrapolated, and the method
    public void Kind_and_method_are_written_for_an_interpolated_series()
    {
        var quotes = ChartSeries.Of("q", "USD", SeriesKind.Level, [P(1, 3.1), P(30, 3.5)]) with { Keys = new("USD") };
        var curve = ChartSeries.Sampled("c", "USD", SeriesKind.Level, 1, 40, t => 3 + t / 100) with
        {
            Keys = new("USD"),
            Interpolated = true,
            Segments = [new Segment(1, 30, "Log-linear"), new Segment(30, 40, "Flat", Extrapolated: true)],
        };
        var csv = ChartCsv.Long(new ChartDefinition(ChartAxis.Numbers(), [quotes, curve]), Metrics with { Width = 120 });
        Assert.Contains("USD,1,3.1,quoted,\r\n", csv);
        Assert.Contains(",interpolated,Log-linear\r\n", csv);
        Assert.Contains(",extrapolated,Flat\r\n", csv);
        var wide = ChartCsv.Wide(new ChartDefinition(ChartAxis.Numbers(), [quotes, curve]), Metrics with { Width = 120 });
        Assert.StartsWith("﻿X,USD,USD (interpolated),USD (extrapolated)\r\n", wide);
    }
}
