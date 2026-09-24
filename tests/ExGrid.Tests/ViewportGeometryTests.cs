using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The arithmetic every visual layer reads (ADR-0013). Pinned here, in the pure layer,
/// because the selection overlay and the cell editor will take the same numbers and
/// must not re-derive them.
/// </summary>
public class ViewportGeometryTests
{
    private static ViewportGeometry Geometry(int totalRowCount, double rowHeightPx = 20, double viewportHeightPx = 100)
        => new(rowHeightPx, viewportHeightPx, totalRowCount);

    [Fact] // ADR-0013: scrollbar length is total rows × row height
    public void The_scroll_height_is_every_row_whether_or_not_it_is_in_the_window()
    {
        Assert.Equal(2_000_000, Geometry(100_000).ScrollHeightPx);
    }

    [Fact] // ADR-0004: a Viewport paints what fits plus the row straddling its bottom edge
    public void Rows_per_viewport_covers_the_partly_visible_row()
    {
        Assert.Equal(6, Geometry(1000).RowsPerViewport);
        Assert.Equal(7, Geometry(1000, rowHeightPx: 20, viewportHeightPx: 101).RowsPerViewport);
    }

    [Fact] // ADR-0013: position = row index × row height, and the offset lands on a row boundary
    public void An_exact_boundary_offset_starts_at_that_row()
    {
        Assert.Equal(new RowRange(5, 6), Geometry(1000).SliceAt(100));
        Assert.Equal(new RowRange(4, 6), Geometry(1000).SliceAt(99.9));
    }

    [Fact] // ADR-0013: fractional heights are carried, not rounded — the drift they cause is the trap
    public void A_fractional_row_height_divides_exactly()
    {
        var geometry = Geometry(1000, rowHeightPx: 28.125, viewportHeightPx: 600);

        Assert.Equal(new RowRange(35, 23), geometry.SliceAt(35 * 28.125));
        Assert.Equal(35 * 28.125, geometry.OffsetPxOf(35));
    }

    [Fact] // ADR-0001: an offset past the end lands on the last full Viewport, never on nothing
    public void An_offset_past_the_end_is_clamped_to_the_last_viewport()
    {
        var geometry = Geometry(10);

        Assert.Equal(new RowRange(4, 6), geometry.SliceAt(100_000));
    }

    [Fact] // A negative offset is a browser artefact (rubber-band scrolling), not a position
    public void A_negative_offset_reads_as_the_top()
    {
        Assert.Equal(new RowRange(0, 6), Geometry(1000).SliceAt(-50));
    }

    [Fact] // The last slice is short rather than over-running the end
    public void A_total_shorter_than_the_viewport_paints_only_what_exists()
    {
        Assert.Equal(new RowRange(0, 3), Geometry(3).SliceAt(0));
    }

    [Fact] // ADR-0001: no rows is not an empty range — RowRange refuses those
    public void No_rows_slices_to_nothing()
    {
        Assert.Null(Geometry(0).SliceAt(0));
    }

    [Fact] // ADR-0004: a move of more than one Viewport is a fling — every row changes, memoisation buys nothing
    public void A_fling_is_a_move_of_more_than_one_viewport()
    {
        var geometry = Geometry(1000);

        Assert.False(geometry.IsFling(0, 6));
        Assert.True(geometry.IsFling(0, 7));
        Assert.True(geometry.IsFling(500, 100));
        Assert.False(geometry.IsFling(100, 100));
    }

    [Fact] // ADR-0012 groundwork: the header cancels out, so top-aligning a row is row × row height
    public void Revealing_a_row_above_the_viewport_puts_it_at_the_top()
    {
        // The header occupies the content's first row height and covers the Viewport's
        // first row height, so the two subtract away and no header term appears here —
        // unlike the pinned width on the other axis, which does not cancel.
        Assert.Equal(0, Geometry(1000).ScrollTopToReveal(0, currentScrollTopPx: 500));
        Assert.Equal(200, Geometry(1000).ScrollTopToReveal(10, currentScrollTopPx: 900));
    }

    [Fact] // ADR-0012: reaching down scrolls just far enough to show the whole row
    public void Revealing_a_row_below_the_viewport_aligns_it_against_the_bottom()
    {
        // Row 10 spans 200-220 and the row area is 100 tall, so 120 brings its bottom
        // edge onto the Viewport's — the minimum move, not a jump to the top.
        Assert.Equal(120, Geometry(1000).ScrollTopToReveal(10, currentScrollTopPx: 0));
    }

    [Fact] // ADR-0012: a row already on screen does not jerk the Viewport to an edge
    public void Revealing_a_visible_row_does_not_move_the_offset()
    {
        Assert.Equal(0, Geometry(1000).ScrollTopToReveal(3, currentScrollTopPx: 0));
    }

    [Fact] // The last row is revealed at the furthest the browser will scroll, not past it
    public void Revealing_the_last_row_clamps_to_the_end()
    {
        Assert.Equal(19_900, Geometry(1000).ScrollTopToReveal(999, currentScrollTopPx: 0));
    }

    [Fact] // ADR-0013/0017: past what a browser can scroll the tail is unreachable — refused, not shown
    public void A_result_too_tall_to_scroll_is_refused()
    {
        var atCap = (int)(ViewportGeometry.MaxScrollHeightPx / 28);

        // The last row height that still maps the scrollbar to the last row.
        var geometry = new ViewportGeometry(28, 600, atCap);
        Assert.True(geometry.ScrollHeightPx <= ViewportGeometry.MaxScrollHeightPx);

        var ex = Assert.Throws<InvalidOperationException>(() => new ViewportGeometry(28, 600, atCap + 1));
        Assert.Contains("page the result", ex.Message);
    }

    [Fact] // ADR-0008: a pointer position becomes a row here, not by measuring elements
    public void A_pixel_names_the_row_it_lands_in()
    {
        var geometry = Geometry(1000);

        Assert.Equal(0, geometry.RowAt(0));
        Assert.Equal(0, geometry.RowAt(19.9));
        Assert.Equal(1, geometry.RowAt(20));
        Assert.Equal(7, geometry.RowAt(150));
    }

    [Fact] // ADR-0008: a drag past the last row keeps extending to the last row
    public void A_pixel_outside_the_content_clamps_to_the_end_row()
    {
        var geometry = Geometry(10);

        Assert.Equal(9, geometry.RowAt(10_000));
        Assert.Equal(0, geometry.RowAt(-50));
        // Past what the cast itself can hold: clamped in pixels first, or this would
        // overflow and come back as row 0 — the wrong end entirely.
        Assert.Equal(9, geometry.RowAt(1e18));
    }

    [Fact] // A result with no rows has nothing to point at — not an error, just nothing
    public void There_is_no_row_when_there_are_none()
    {
        Assert.Null(Geometry(0).RowAt(0));
    }

    [Fact] // Rather than be quietly wrong: nonsense geometry is refused by name
    public void Invalid_geometry_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).RowAt(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(0, 100, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(20, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(double.NaN, 100, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(20, 100, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).SliceAt(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).OffsetPxOf(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).ScrollTopToReveal(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).ScrollTopToReveal(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).ScrollTopToReveal(0, double.NaN));
    }
}
