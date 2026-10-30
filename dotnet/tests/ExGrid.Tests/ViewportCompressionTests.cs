using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The one mapping between the scroll offset the browser holds and the content offset
/// the rows are computed from (ADR-0053). Below the Layout Ceiling it is the identity,
/// bit for bit; above it the spacer is capped and every consumer reads the content
/// through <c>c(s) = s × k</c>.
///
/// <para>Its single answers are shared cases (<c>tests/cases/viewport/compression.json</c>); what stays here
/// are the properties over many offsets, which <c>packages/core/test/viewport-compression.test.ts</c> holds
/// the Script Core to as well.</para>
/// </summary>
public class ViewportCompressionTests
{
    // /wide at 150%: a million rows of 28 px under the ceiling Chrome lays out at that scale.
    private const double RowHeight = 28;
    private const double Viewport = 572;
    private const int Rows = 1_000_000;
    private const double CeilingAt150 = 22_369_618;

    private static ViewportGeometry Compressed(int rows = Rows, double roomPx = CeilingAt150)
        => new(RowHeight, Viewport, rows, roomPx);

    [Theory] // ADR-0053 / VZ-15: under the ceiling k = 1 and every answer is ADR-0013's, bit for bit
    [InlineData(1000, 20, 100, double.PositiveInfinity)]
    [InlineData(1000, 20, 100, 20_000)] // exactly at the room: still not compressed
    [InlineData(1_000_000, 28, 572, 33_554_428)]
    [InlineData(1_000_000, 28.125, 571.5, 28_125_000.5)]
    public void Under_the_ceiling_the_geometry_is_unchanged(int rows, double rowHeight, double viewport, double room)
    {
        var plain = new ViewportGeometry(rowHeight, viewport, rows);
        var told = new ViewportGeometry(rowHeight, viewport, rows, room);

        Assert.False(told.IsCompressed);
        Assert.Equal(1, told.Compression);
        Assert.Equal(plain.ScrollHeightPx, told.ScrollHeightPx);
        Assert.Equal(told.ContentHeightPx, told.ScrollHeightPx);
        Assert.Equal(Math.Max(0, plain.ScrollHeightPx - viewport), told.MaxScrollTopPx);
        Assert.Equal(told.MaxScrollTopPx, told.ScrollReachPx);
        foreach (var s in new[] { -50, 0, 0.5, 99.9, 1234.5678, told.MaxScrollTopPx, told.MaxScrollTopPx + 10 })
        {
            Assert.Equal(plain.SliceAt(s), told.SliceAt(s));
            Assert.Equal(s, told.ContentOffsetAt(s));
            Assert.Equal(s, told.ScrollTopAt(s));
            Assert.Equal(Math.Clamp(s + (3 * rowHeight), 0, told.MaxScrollTopPx), told.ScrollTopStepped(s, 3 * rowHeight));
            foreach (var row in new[] { 0, 1, 7, rows / 2, rows - 1 })
            {
                Assert.Equal(plain.OffsetPxOf(row), told.PaintedTopPxOf(row, s));
                Assert.Equal(plain.OffsetPxOf(row) - s, told.ViewportTopPxOf(row, s));
                Assert.Equal(plain.ScrollTopToReveal(row, s), told.ScrollTopToReveal(row, s));
            }
        }
    }

    [Theory] // ADR-0053: the slice is floor(c(s) / RowHeight), and the painted rows stand where the content says
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(123_456.6667)]
    [InlineData(11_000_000)]
    [InlineData(22_368_000.3333)]
    public void The_slice_and_its_placement_follow_the_content_offset(double s)
    {
        var geometry = Compressed();
        s = Math.Min(s, geometry.MaxScrollTopPx);
        var slice = geometry.SliceAt(s)!.Value;
        var c = geometry.ContentOffsetAt(s);

        Assert.Equal(Math.Min((int)Math.Floor(c / RowHeight), Rows - geometry.RowsPerViewport), slice.Start);
        // The first painted row starts at or above the readable top, by less than a row.
        var top = geometry.ViewportTopPxOf(slice.Start, s);
        Assert.InRange(top, -RowHeight, 0);
        // Placed inside the scroll content, it lands on that same spot once scrolled by s.
        Assert.Equal(top, geometry.PaintedTopPxOf(slice.Start, s) - s, 6);
        // And the painted rows reach the readable bottom.
        Assert.True(top + (slice.Count * RowHeight) >= Viewport - 1e-6);
    }

    [Fact] // ADR-0053 / ADR-0012: a reveal asks for s = c* / k, rounded towards the whole Focus being visible
    public void A_reveal_shows_the_whole_row_through_the_mapping()
    {
        var geometry = Compressed();
        var random = new Random(53);
        for (var i = 0; i < 2000; i++)
        {
            var row = i switch { 0 => 0, 1 => Rows - 1, _ => random.Next(Rows) };
            var current = random.NextDouble() * geometry.MaxScrollTopPx;
            var s = geometry.ScrollTopToReveal(row, current);

            Assert.InRange(s, 0, geometry.MaxScrollTopPx);
            var top = geometry.ViewportTopPxOf(row, s);
            Assert.True(top >= -1e-6, $"row {row} from {current}: its top is {-top}px above the readable area");
            Assert.True(top + RowHeight <= Viewport + 1e-6, $"row {row} from {current}: its bottom is {top + RowHeight - Viewport}px below it");
            // A reveal is asked in whole CSS pixels, or at an end, so the device-pixel
            // quantisation cannot land the other side of the rounding by more than a pixel.
            Assert.True(s == Math.Floor(s) || s == geometry.MaxScrollTopPx, $"{s} is not a whole pixel");
            // Revealing it again from there moves nothing: no oscillation between keys.
            Assert.Equal(s, geometry.ScrollTopToReveal(row, s));
        }
    }
}
