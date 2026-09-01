using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>The edge band's rate curve (ADR-0008): one row per tick at the inner lip,
/// the ceiling at the very edge, monotone between, zero outside.</summary>
public class EdgeBandTests
{
    private const double Band = 200;
    private const int RowsPerViewport = 11;

    [Fact] // ADR-0008: outside the band nothing scrolls
    public void Outside_the_band_the_rate_is_zero()
    {
        Assert.Equal(0, EdgeBand.RowsPerTick(100, Band, RowsPerViewport));
        Assert.Equal(0, EdgeBand.RowsPerTick(20, Band, RowsPerViewport));
        Assert.Equal(0, EdgeBand.RowsPerTick(180, Band, RowsPerViewport));
    }

    [Fact] // ADR-0008: the rate rises monotonically with depth, from one to eight
    public void The_rate_rises_monotonically_with_depth()
    {
        var previous = 0;
        for (var distance = 19.5; distance >= 0; distance -= 0.5)
        {
            var rate = EdgeBand.RowsPerTick(Band - distance, Band, RowsPerViewport);
            Assert.True(rate >= previous, $"rate fell from {previous} to {rate} at distance {distance}");
            previous = rate;
        }
        Assert.Equal(1, EdgeBand.RowsPerTick(Band - 19.5, Band, RowsPerViewport));
        Assert.Equal(8, EdgeBand.RowsPerTick(Band, Band, RowsPerViewport));
    }

    [Fact] // ADR-0008: the leading edge mirrors, negatively
    public void The_leading_edge_is_the_same_curve_negated()
    {
        Assert.Equal(-1, EdgeBand.RowsPerTick(19.5, Band, RowsPerViewport));
        Assert.Equal(-8, EdgeBand.RowsPerTick(0, Band, RowsPerViewport));
    }

    [Fact] // ADR-0008 / SL-13: the ceiling stays one row short of the fling threshold
    public void The_ceiling_never_reaches_the_fling_threshold()
    {
        // A Viewport of five rows: the fling threshold is five, so the fastest tick is four.
        Assert.Equal(4, EdgeBand.RowsPerTick(Band, Band, rowsPerViewport: 5));
        // And never below one — a two-row Viewport still scrolls.
        Assert.Equal(1, EdgeBand.RowsPerTick(Band, Band, rowsPerViewport: 2));
    }

    [Fact] // A band shorter than its two strips would be "at the edge" everywhere — it scrolls nothing
    public void A_degenerate_band_scrolls_nothing()
    {
        Assert.Equal(0, EdgeBand.RowsPerTick(10, 30, RowsPerViewport));
        Assert.Equal(0, EdgeBand.RowsPerTick(0, 0, RowsPerViewport));
    }
}
