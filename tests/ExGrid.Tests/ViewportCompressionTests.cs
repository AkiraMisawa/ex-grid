using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The one mapping between the scroll offset the browser holds and the content offset
/// the rows are computed from (ADR-0053). Below the Layout Ceiling it is the identity,
/// bit for bit; above it the spacer is capped and every consumer reads the content
/// through <c>c(s) = s × k</c>.
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

    [Fact] // ADR-0053 / VZ-8: the refusal is at the scale-1 Layout Ceiling, which is 4 px short of 2^25
    public void The_refusal_is_at_the_scale_one_layout_ceiling()
    {
        Assert.Equal(33_554_428, ViewportGeometry.MaxScrollHeightPx);
        _ = new ViewportGeometry(1, Viewport, 33_554_428);
        Assert.Throws<InvalidOperationException>(() => new ViewportGeometry(1, Viewport, 33_554_429));
        // Whatever room the ceiling leaves: what is refused does not depend on the machine.
        Assert.Throws<InvalidOperationException>(() => new ViewportGeometry(1, Viewport, 33_554_429, 100_000_000));
    }

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

    [Fact] // ADR-0053: above the ceiling the spacer is the room less a margin, and k maps both ends exactly
    public void Above_the_ceiling_the_spacer_is_capped_and_both_ends_are_exact()
    {
        var geometry = Compressed();

        Assert.True(geometry.IsCompressed);
        Assert.Equal(Rows * RowHeight, geometry.ContentHeightPx);
        Assert.Equal(CeilingAt150 - ViewportGeometry.LayoutCeilingMarginPx, geometry.ScrollHeightPx);
        Assert.Equal(
            (geometry.ContentHeightPx - Viewport) / (geometry.ScrollHeightPx - Viewport - ViewportGeometry.EndSlackPx),
            geometry.Compression);
        Assert.InRange(geometry.Compression, 1.25, 1.26);
        // The browser can stop a device pixel short of the maximum; from EndSlackPx short
        // of it on, the last row is already flush.
        Assert.Equal(geometry.MaxScrollTopPx - ViewportGeometry.EndSlackPx, geometry.ScrollReachPx);
        Assert.Equal(geometry.ContentHeightPx - Viewport, geometry.ContentOffsetAt(geometry.ScrollReachPx), 6);
        Assert.Equal(Viewport, geometry.ViewportTopPxOf(Rows - 1, geometry.MaxScrollTopPx - 1) + RowHeight, 6);

        Assert.Equal(0, geometry.ContentOffsetAt(0));
        Assert.Equal(new RowRange(0, geometry.RowsPerViewport), geometry.SliceAt(0));
        Assert.Equal(geometry.ContentHeightPx - Viewport, geometry.ContentOffsetAt(geometry.MaxScrollTopPx), 6);
        var last = geometry.SliceAt(geometry.MaxScrollTopPx)!.Value;
        Assert.Equal(Rows - 1, last.Start + last.Count - 1);
        // The last row's bottom edge is the readable area's bottom edge.
        Assert.Equal(Viewport, geometry.ViewportTopPxOf(Rows - 1, geometry.MaxScrollTopPx) + RowHeight, 6);
        // Past the end, and before the start, the content offset is clamped like the slice.
        Assert.Equal(geometry.ContentOffsetAt(geometry.MaxScrollTopPx), geometry.ContentOffsetAt(geometry.MaxScrollTopPx + 5000));
        Assert.Equal(0, geometry.ContentOffsetAt(-40));
    }

    [Fact] // ADR-0053: the ceiling a browser tells at scale 1 never compresses a result VZ-8 accepts
    public void The_scale_one_ceiling_compresses_nothing_that_is_accepted()
    {
        var atCap = (int)(ViewportGeometry.MaxScrollHeightPx / RowHeight);
        var geometry = new ViewportGeometry(RowHeight, Viewport, atCap, ViewportGeometry.MaxScrollHeightPx);

        Assert.False(geometry.IsCompressed);
        Assert.Equal(atCap * RowHeight, geometry.ScrollHeightPx);
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

    [Fact] // ADR-0053: the ends are the ends — Ctrl+Home at 0 and Ctrl+End at the furthest the browser scrolls
    public void Revealing_the_first_and_last_rows_goes_to_the_ends()
    {
        var geometry = Compressed();

        Assert.Equal(0, geometry.ScrollTopToReveal(0, geometry.MaxScrollTopPx / 2));
        Assert.Equal(geometry.MaxScrollTopPx, geometry.ScrollTopToReveal(Rows - 1, 0));
    }

    [Fact] // ADR-0053: a row already visible leaves the offset where the browser put it, unrounded
    public void Revealing_a_visible_row_under_compression_moves_nothing()
    {
        var geometry = Compressed();
        const double s = 1_000_000.6667;
        var first = geometry.SliceAt(s)!.Value.Start;

        Assert.Equal(s, geometry.ScrollTopToReveal(first + 3, s));
    }

    [Fact] // ADR-0053 / ADR-0008: an edge auto-scroll or a PageDown steps the content by rows, not the scrollbar
    public void A_step_moves_the_content_by_the_rows_asked()
    {
        var geometry = Compressed();
        const double s = 5_000_000;

        var stepped = geometry.ScrollTopStepped(s, 10 * RowHeight);

        Assert.Equal(geometry.ContentOffsetAt(s) + (10 * RowHeight), geometry.ContentOffsetAt(stepped), 6);
        Assert.Equal(geometry.MaxScrollTopPx, geometry.ScrollTopStepped(geometry.MaxScrollTopPx - 1, 10 * RowHeight));
        Assert.Equal(0, geometry.ScrollTopStepped(1, -10 * RowHeight));
    }

    [Fact] // ADR-0053: the inverse is the inverse
    public void The_scroll_offset_for_a_content_offset_is_the_inverse()
    {
        var geometry = Compressed();

        Assert.Equal(9_000_000, geometry.ContentOffsetAt(geometry.ScrollTopAt(9_000_000)), 6);
    }

    [Fact] // Rather than be quietly wrong: a room that is not a number, or too small to scroll in, is refused
    public void Nonsense_room_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(RowHeight, Viewport, Rows, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(RowHeight, Viewport, Rows, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(RowHeight, Viewport, Rows, -1));
        // A room no taller than the readable area leaves nothing to scroll the content with.
        Assert.Throws<InvalidOperationException>(() => new ViewportGeometry(RowHeight, Viewport, Rows, Viewport));
    }
}
