using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The horizontal arithmetic every visual layer reads (ADR-0004/0013). Pinned here, in
/// the pure layer, because the selection overlay takes the same offsets and must not
/// re-derive them (ADR-0008 — the two are one implementation).
///
/// Unless a test says otherwise the geometry is five 100px columns in a 350px Viewport:
/// three fit, the fourth straddles the right edge, and the fifth is off screen.
/// </summary>
public class ColumnGeometryTests
{
    private static readonly double[] FiveEqualColumns = [100, 100, 100, 100, 100];

    private static ColumnGeometry Geometry(int pinnedCount = 0, double viewportWidthPx = 350)
        => new(FiveEqualColumns, pinnedCount, viewportWidthPx);

    [Fact] // ADR-0013: the scrollbar spans every column, painted or not
    public void The_total_width_is_every_column_and_the_pinned_width_is_the_leading_ones()
    {
        var geometry = Geometry(pinnedCount: 2);

        Assert.Equal(500, geometry.TotalWidthPx);
        Assert.Equal(200, geometry.PinnedWidthPx);
        Assert.Equal(150, geometry.MaxScrollLeftPx);
    }

    [Fact] // ADR-0013: a column's place is the prefix sum of the widths before it
    public void Offsets_are_the_running_sum_and_the_last_one_is_the_right_edge()
    {
        var geometry = new ColumnGeometry([120, 80, 200], 0, 350);

        Assert.Equal(0, geometry.OffsetPxOf(0));
        Assert.Equal(120, geometry.OffsetPxOf(1));
        Assert.Equal(200, geometry.OffsetPxOf(2));
        Assert.Equal(400, geometry.OffsetPxOf(3));
        Assert.Equal(80, geometry.WidthPxOf(1));
    }

    [Fact] // ADR-0004: render cost is decided by the Viewport, not by how many columns exist
    public void Only_the_columns_the_viewport_reaches_are_sliced()
    {
        // Columns 0-2 fit and column 3 straddles the right edge; column 4 is past it.
        Assert.Equal(new ColumnRange(0, 4), Geometry().ScrollableSliceAt(0, virtualise: true));
    }

    [Fact] // ADR-0004: a column only half on screen is painted — leaving it out would show a gap
    public void A_column_straddling_an_edge_is_included_and_one_wholly_past_it_is_not()
    {
        var geometry = Geometry();

        // At 150 the Viewport covers 150-500: column 1 ends exactly at 200 and is still
        // in, column 0 ends at 100 and is out.
        Assert.Equal(new ColumnRange(1, 4), geometry.ScrollableSliceAt(150, virtualise: true));
        // Exactly on a boundary: 100-450 starts at column 1's left edge.
        Assert.Equal(new ColumnRange(1, 4), geometry.ScrollableSliceAt(100, virtualise: true));
    }

    [Fact] // ADR-0004: Pinned Columns are painted regardless, so they are never part of the slice
    public void Pinned_columns_never_appear_in_the_scrollable_slice()
    {
        var geometry = Geometry(pinnedCount: 2);

        Assert.Equal(new ColumnRange(2, 2), geometry.ScrollableSliceAt(0, virtualise: true));
        Assert.Equal(new ColumnRange(3, 2), geometry.ScrollableSliceAt(150, virtualise: true));
    }

    [Fact] // ADR-0004: a column hidden under the pinned block is not painted — nobody could read it
    public void A_column_lying_entirely_under_the_pinned_block_is_left_out()
    {
        // Pinned 0-1 cover the Viewport's left 200px. At scrollLeft 150 the Viewport
        // shows content 150-500, but content 150-350 is under the pinned cells, so
        // column 2 (200-300) is completely covered and column 3 (300-400) is the first
        // one anyone can see.
        Assert.Equal(new ColumnRange(3, 2), Geometry(pinnedCount: 2).ScrollableSliceAt(150, virtualise: true));
    }

    [Fact] // ADR-0004: pinning costs directly — pinned wider than the Viewport leaves nothing to virtualise
    public void Pinned_columns_filling_the_viewport_leave_no_scrollable_slice()
    {
        Assert.Null(Geometry(pinnedCount: 4, viewportWidthPx: 350).ScrollableSliceAt(0, virtualise: true));
    }

    [Fact] // Every column pinned is a grid with nothing to scroll, not an error
    public void Pinning_every_column_leaves_no_slice_at_all()
    {
        Assert.Null(Geometry(pinnedCount: 5).ScrollableSliceAt(0, virtualise: true));
        Assert.Null(Geometry(pinnedCount: 5).ScrollableSliceAt(0, virtualise: false));
    }

    [Fact] // ADR-0004: everything fitting means everything is painted
    public void A_viewport_wider_than_the_columns_slices_all_of_them()
    {
        Assert.Equal(new ColumnRange(0, 5), Geometry(viewportWidthPx: 900).ScrollableSliceAt(0, virtualise: true));
    }

    [Fact] // ADR-0004: off, the slice is simply every scrollable column — one code path, two settings
    public void Virtualisation_off_returns_every_scrollable_column_wherever_the_offset_is()
    {
        var geometry = Geometry(pinnedCount: 2);

        Assert.Equal(new ColumnRange(2, 3), geometry.ScrollableSliceAt(0, virtualise: false));
        Assert.Equal(new ColumnRange(2, 3), geometry.ScrollableSliceAt(150, virtualise: false));
    }

    [Fact] // ADR-0001: an offset past the end lands on the last full Viewport, never on nothing
    public void An_offset_past_the_end_is_clamped_to_the_last_viewport()
    {
        Assert.Equal(new ColumnRange(1, 4), Geometry().ScrollableSliceAt(100_000, virtualise: true));
    }

    [Fact] // A negative offset is a browser artefact (rubber-band scrolling), not a position
    public void A_negative_offset_reads_as_the_left_edge()
    {
        Assert.Equal(new ColumnRange(0, 4), Geometry().ScrollableSliceAt(-80, virtualise: true));
    }

    [Fact] // ADR-0012 groundwork: revealing a column must clear the Pinned Columns, not hide under them
    public void Revealing_a_column_subtracts_the_pinned_width()
    {
        // One pinned column of 100px in a 250px Viewport, scrolled to the right-hand
        // end, asked to come back to column 2 (content 200-300).
        var geometry = new ColumnGeometry(FiveEqualColumns, pinnedCount: 1, viewportWidthPx: 250);

        var reveal = geometry.ScrollLeftToReveal(2, currentScrollLeftPx: 250);

        // Scrolling to the column's own offset would slide it under the pinned block:
        // in the DOM, on screen, and completely unreadable.
        Assert.Equal(geometry.OffsetPxOf(2) - geometry.PinnedWidthPx, reveal);
        Assert.Equal(100, reveal);
    }

    [Fact] // ADR-0012 groundwork: near the left edge there is nothing to scroll away
    public void Revealing_a_column_clamps_at_the_left_edge()
    {
        var geometry = Geometry(pinnedCount: 2);

        Assert.Equal(0, geometry.ScrollLeftToReveal(2, currentScrollLeftPx: 0));
        Assert.Equal(0, geometry.ScrollLeftToReveal(2, currentScrollLeftPx: 40));
    }

    [Fact] // ADR-0012: a Pinned Column is always visible, so revealing it moves nothing
    public void Revealing_a_pinned_column_leaves_the_offset_where_it_is()
    {
        Assert.Equal(120, Geometry(pinnedCount: 2).ScrollLeftToReveal(0, currentScrollLeftPx: 120));
    }

    [Fact] // ADR-0012: a column already on screen does not jerk the Viewport to an edge
    public void Revealing_a_visible_column_does_not_move_the_offset()
    {
        Assert.Equal(0, Geometry().ScrollLeftToReveal(2, currentScrollLeftPx: 0));
    }

    [Fact] // ADR-0012: reaching right scrolls just far enough to show the whole column
    public void Revealing_a_column_to_the_right_aligns_it_against_the_right_edge()
    {
        // Column 4 spans 400-500 and the Viewport is 350 wide, so 150 puts its right
        // edge on the Viewport's — the minimum move, not a jump to the top of the column.
        Assert.Equal(150, Geometry().ScrollLeftToReveal(4, currentScrollLeftPx: 0));
    }

    [Fact] // ADR-0004: either axis crossing a whole Viewport is a fling; the pixels decide, not a column count
    public void A_fling_is_a_move_of_more_than_one_viewport()
    {
        var geometry = Geometry();

        Assert.False(geometry.IsFling(0, 350));
        Assert.True(geometry.IsFling(0, 351));
        Assert.True(geometry.IsFling(400, 0));
        Assert.False(geometry.IsFling(100, 100));
    }

    [Fact] // Widths differ per column, so the slice cannot assume a common one
    public void Uneven_widths_slice_from_the_running_sum()
    {
        // 0-30, 30-330, 330-380, 380-580 in a 350px Viewport.
        var geometry = new ColumnGeometry([30, 300, 50, 200], 0, 350);

        Assert.Equal(new ColumnRange(0, 3), geometry.ScrollableSliceAt(0, virtualise: true));
        Assert.Equal(new ColumnRange(1, 3), geometry.ScrollableSliceAt(100, virtualise: true));
    }

    [Fact] // ADR-0016: a zero-width column is degenerate but not a lie — it simply shows nothing
    public void A_zero_width_column_is_carried_without_breaking_the_slice()
    {
        var geometry = new ColumnGeometry([100, 0, 100, 100], 0, 350);

        Assert.Equal(300, geometry.TotalWidthPx);
        Assert.Equal(new ColumnRange(0, 4), geometry.ScrollableSliceAt(0, virtualise: true));
    }

    [Fact] // ADR-0013/0017: past what a browser can scroll the far columns are unreachable — refused, not shown
    public void A_result_too_wide_to_scroll_is_refused()
    {
        var half = ColumnGeometry.MaxScrollWidthPx / 2;

        // The last width that still maps the scrollbar to the last column.
        Assert.Equal(ColumnGeometry.MaxScrollWidthPx, new ColumnGeometry([half, half], 0, 800).TotalWidthPx);

        var ex = Assert.Throws<InvalidOperationException>(() => new ColumnGeometry([half, half, 1], 0, 800));
        Assert.Contains("could never be reached", ex.Message);
    }

    [Fact] // Rather than be quietly wrong: nonsense geometry is refused by name
    public void Invalid_geometry_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry([100, -1], 0, 350));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry([100, double.NaN], 0, 350));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry(FiveEqualColumns, 6, 350));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry(FiveEqualColumns, -1, 350));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry(FiveEqualColumns, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry(FiveEqualColumns, 0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry().ScrollableSliceAt(double.NaN, virtualise: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry().OffsetPxOf(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry().OffsetPxOf(6));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry().WidthPxOf(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry().ScrollLeftToReveal(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geometry().ScrollLeftToReveal(0, double.NaN));
    }
}
