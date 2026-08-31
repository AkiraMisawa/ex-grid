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

    [Fact] // Rather than be quietly wrong: nonsense geometry is refused by name
    public void Invalid_geometry_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(0, 100, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(20, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(double.NaN, 100, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportGeometry(20, 100, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).SliceAt(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry(10).OffsetPxOf(-1));
    }
}
