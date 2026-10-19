using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class SeriesDrawingTests
{
    private static ChartPoint P(double x, double? y) => new(x, y is { } v ? ChartValue.Of(v) : null);
    private static readonly ChartAxis Years = ChartAxis.Numbers();

    [Fact] // ADR-0253: a missing value is a gap, never a zero; a lone point is a marker with no line
    public void A_level_breaks_its_line_at_a_missing_value()
    {
        var d = SeriesDrawing.Of(ChartSeries.Of("a", "EPE", SeriesKind.Level, [P(0, 1), P(1, 2), P(2, null), P(3, 4), P(4, null), P(5, 6), P(6, 7)]), Years);
        Assert.Equal(2, d.Lines.Count);
        Assert.Equal([new(0, 1), new(1, 2)], d.Lines[0]);
        Assert.Equal([new(5, 6), new(6, 7)], d.Lines[1]);
        Assert.Equal(5, d.Markers.Count);
        Assert.DoesNotContain(d.Markers, m => m.Y == 0);
    }

    [Fact] // ADR-0253: (tᵢ₋₁, tᵢ] — filled where the rate holds, open where it does not, no riser
    public void A_preceding_step_has_filled_right_ends_and_open_left_ends()
    {
        // A CDS hazard curve: a first row at the curve's date carrying no rate, then three levels.
        var d = SeriesDrawing.Of(ChartSeries.Of("h", "Hazard", SeriesKind.RateOverInterval, [P(0, null), P(1, 0.7), P(3, 0.9), P(5, 0.9)]), Years);
        Assert.Equal([[new(0, 0.7), new(1, 0.7)], [new(1, 0.9), new(3, 0.9)], [new(3, 0.9), new(5, 0.9)]], d.Lines);
        Assert.Contains(new DrawnMarker(1, 0.7, true), d.Markers);
        Assert.Contains(new DrawnMarker(0, 0.7, false), d.Markers);
        Assert.Contains(new DrawnMarker(1, 0.9, false), d.Markers);
        // Equal levels either side of 3: the function is continuous there, so no open end is drawn.
        Assert.DoesNotContain(new DrawnMarker(3, 0.9, false), d.Markers);
        Assert.All(d.Lines, l => Assert.Equal(l[0].Y, l[^1].Y));
    }

    [Fact] // ADR-0253: [tᵢ, tᵢ₊₁) — filled on the left, open on the right
    public void A_following_step_has_filled_left_ends()
    {
        var d = SeriesDrawing.Of(ChartSeries.Of("f", "Fwd", SeriesKind.RateOverInterval, [P(0, 1), P(1, 2), P(2, null)]) with { Step = StepSide.Following }, Years);
        Assert.Contains(new DrawnMarker(0, 1, true), d.Markers);
        Assert.Contains(new DrawnMarker(1, 1, false), d.Markers);
        Assert.Contains(new DrawnMarker(1, 2, true), d.Markers);
        Assert.Contains(new DrawnMarker(2, 2, false), d.Markers);
    }

    [Fact] // ADR-0253 (Q6): an amount per period is drawn per year, so widening buckets do not jump
    public void An_amount_is_drawn_per_year_of_its_interval()
    {
        // Monthly 100, quarterly 300, yearly 1,200: one constant risk.
        var d = SeriesDrawing.Of(ChartSeries.Of("cva", "CVA", SeriesKind.AmountPerPeriod,
            [P(0, null), P(1.0 / 12, 100), P(1.0 / 12 + 0.25, 300), P(1.0 / 12 + 1.25, 1200)]), Years);
        Assert.All(d.Lines, l => Assert.Equal(1200, l[0].Y, 6));
        // Only where the curve starts is an end open: every later level meets the next.
        Assert.Equal([new DrawnMarker(0, 1200, false)], d.Markers.Where(m => !m.Filled));
    }

    [Fact] // ADR-0253: a date axis divides an amount by its days over 365.25
    public void An_amount_on_dates_is_divided_by_its_days()
    {
        var start = new DateOnly(2026, 1, 1).DayNumber;
        var d = SeriesDrawing.Of(ChartSeries.Of("cva", "CVA", SeriesKind.AmountPerPeriod, [P(start, null), P(start + 365.25, 50)]), ChartAxis.Dates());
        Assert.Equal(50, d.Lines[0][0].Y, 9);
    }

    [Fact] // ADR-0257: a function is asked about once per pixel and at every quote, and draws no markers
    public void A_sampled_series_is_asked_per_pixel_and_at_the_quotes()
    {
        var asked = new List<double>();
        var s = ChartSeries.Sampled("z", "Zero", SeriesKind.Level, 0, 10, t => { asked.Add(t); return t / 10; });
        var d = SeriesDrawing.Of(s, Years, from: 0, to: 10, pixels: 100, quotes: [0.5, 3.3]);
        Assert.Contains(3.3, asked);
        Assert.InRange(asked.Count, 101, 104);
        Assert.Empty(d.Markers);
        Assert.Single(d.Lines);
    }

    [Fact] // ADR-0257: no line is joined across a Segment's boundary, where a forward may jump
    public void A_sampled_series_is_not_joined_across_a_boundary()
    {
        var s = ChartSeries.Sampled("f", "Fwd", SeriesKind.Level, 0, 4, t => t < 2 ? 1.0 : 3.0) with
        {
            Interpolated = true,
            Segments = [new Segment(0, 2, "Linear on zero rates"), new Segment(2, 4, "Log-linear on discount factors")],
        };
        var d = SeriesDrawing.Of(s, Years, 0, 4, pixels: 8);
        Assert.Equal(2, d.Lines.Count);
        Assert.All(d.Lines[0], p => Assert.Equal(1.0, p.Y));
        Assert.All(d.Lines[1], p => Assert.Equal(3.0, p.Y));
        Assert.Equal(2, d.Lines[1][0].X);
    }

    [Fact] // ADR-0257: handed in as points, a point on a boundary ends one line and starts the next
    public void Points_on_a_boundary_end_and_start_a_line()
    {
        var s = ChartSeries.Of("c", "Curve", SeriesKind.Level, [P(0, 1), P(1, 2), P(2, 3), P(3, 4)]) with
        {
            Segments = [new Segment(0, 1, "a"), new Segment(1, 3, "b")],
        };
        var d = SeriesDrawing.Of(s, Years);
        Assert.Equal([[new(0, 1), new(1, 2)], [new(1, 2), new(2, 3), new(3, 4)]], d.Lines);
    }
}
