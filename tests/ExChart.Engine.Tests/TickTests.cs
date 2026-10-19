using ExChart.Engine;

namespace ExChart.Engine.Tests;

public class TickTests
{
    private static double W(string s) => s.Length * 7;
    private static double Day(int y, int m, int d) => new DateOnly(y, m, d).DayNumber;

    [Theory] // ticket 04: tenors are written as the market writes them
    [InlineData(0.5, "6M")]
    [InlineData(1, "1Y")]
    [InlineData(1.5, "18M")]
    [InlineData(10, "10Y")]
    [InlineData(1.0 / 52, "1W")]
    public void A_tenor_is_written_in_months_or_years(double years, string expected) =>
        Assert.Equal(expected, TickLayout.Tenor(years));

    [Fact] // ticket 04: round-number steps of 1, 2, 2.5 or 5 cover the range
    public void Nice_ticks_cover_the_range_at_a_round_step()
    {
        var (lo, hi, step) = TickLayout.Nice(0.3, 9.7);
        Assert.Equal((0, 10, 2), (lo, hi, step));
        Assert.Equal([0, 2, 4, 6, 8, 10], TickLayout.Steps(lo, hi, step));
    }

    [Fact] // ticket 04: a label that would touch its neighbour is left out, never overprinted; every tick stays
    public void Labels_that_would_touch_are_left_out_and_every_tick_is_kept()
    {
        var ticks = TickLayout.Fit([(0, "2026-01-01"), (1, "2026-01-02"), (2, "2026-01-03"), (3, "2026-01-04")], 0, x => x * 40, W, 14, 4);
        Assert.Equal(4, ticks.Ticks.Count);
        Assert.Equal(2, ticks.Labelled);
        Assert.Null(ticks.Ticks[1].Label);
    }

    [Fact] // ticket 04: at 90° a label takes its height along the axis, and the room under it grows
    public void At_ninety_degrees_every_label_fits_and_the_depth_grows()
    {
        var flat = TickLayout.Fit([(0, "2026-01-01"), (1, "2026-01-02")], 0, x => x * 20, W, 14, 4);
        var up = TickLayout.Fit([(0, "2026-01-01"), (1, "2026-01-02")], 90, x => x * 20, W, 14, 4);
        Assert.Equal(1, flat.Labelled);
        Assert.Equal(2, up.Labelled);
        Assert.True(up.Depth > flat.Depth);
    }

    [Fact] // ticket 04: tenor ticks are counted from the first date
    public void Tenor_ticks_count_from_the_first_date()
    {
        var axis = ChartAxis.Dates();
        var t = TickLayout.Horizontal(axis, new AxisStyle(TickPlacement.Tenors), Day(2026, 1, 15), Day(2028, 1, 15), [], x => x, W, 14);
        Assert.Equal(Day(2026, 2, 15), t.Ticks[0].Position);
        Assert.Contains(t.Ticks, k => k.Position == Day(2027, 1, 15) && k.Label == "1Y");
        Assert.Contains(t.Ticks, k => k.Position == Day(2028, 1, 15) && k.Label == "2Y");
    }

    [Fact] // ticket 04: calendar ticks fall on boundaries, the finest that fit
    public void Calendar_ticks_fall_on_year_boundaries_over_decades()
    {
        var t = TickLayout.Horizontal(ChartAxis.Dates(), AxisStyle.Default, Day(2026, 3, 1), Day(2046, 3, 1), [], x => (x - Day(2026, 3, 1)) / 10, W, 14);
        Assert.All(t.Ticks, k => Assert.Equal((1, 1), (DateOnly.FromDayNumber((int)k.Position).Month, DateOnly.FromDayNumber((int)k.Position).Day)));
        Assert.All(t.Ticks.Where(k => k.Label is not null), k => Assert.Equal(4, k.Label!.Length));
    }

    [Fact] // ADR-0251: a number axis with declared positions is ticked and labelled by them
    public void Named_positions_label_a_number_axis()
    {
        var axis = ChartAxis.Numbers() with { Named = [(0.5, "6m"), (1, "1y"), (5, "5y")] };
        var t = TickLayout.Horizontal(axis, AxisStyle.Default, 0.5, 5, [], x => x * 100, W, 14);
        Assert.Equal(["6m", "1y", "5y"], t.Ticks.Select(k => k.Label));
    }
}
