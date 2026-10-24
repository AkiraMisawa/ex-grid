using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Headings' pure half (ADR-0050, item 1): the Row Headings are a lead band in the
/// horizontal geometry that no column index names. Five 100px columns in a 350px Viewport,
/// with a 40px lead unless a test says otherwise. What a Heading's click selects is in the
/// shared cases (tests/cases/selection/headings.json, ADR-0350).
/// </summary>
public class HeadingsSelectionTests
{
    private static readonly double[] FiveEqualColumns = [100, 100, 100, 100, 100];

    [Fact] // ADR-0050/0028: the lead band comes first, and every column's offset starts after it
    public void The_lead_band_shifts_every_offset_and_covers_the_left_edge()
    {
        var geometry = new ColumnGeometry(FiveEqualColumns, 0, 350, leadWidthPx: 40);

        Assert.Equal(40, geometry.LeadWidthPx);
        Assert.Equal(40, geometry.OffsetPxOf(0));
        Assert.Equal(140, geometry.OffsetPxOf(1));
        Assert.Equal(540, geometry.TotalWidthPx);
        Assert.Equal(40, geometry.PinnedWidthPx);
    }

    [Fact] // ADR-0050: with no lead the geometry is exactly the one before
    public void A_zero_lead_is_the_geometry_without_the_band()
    {
        var plain = new ColumnGeometry(FiveEqualColumns, 2, 350);
        var zero = new ColumnGeometry(FiveEqualColumns, 2, 350, leadWidthPx: 0);

        for (var c = 0; c <= 5; c++)
            Assert.Equal(plain.OffsetPxOf(c), zero.OffsetPxOf(c));
        Assert.Equal(plain.PinnedWidthPx, zero.PinnedWidthPx);
        Assert.False(zero.IsInLead(10, 0));
    }

    [Fact] // ADR-0050: the band stays at the Viewport's left edge whatever the scroll offset
    public void A_pixel_in_the_band_is_in_the_lead_at_any_scroll_offset()
    {
        var geometry = new ColumnGeometry(FiveEqualColumns, 0, 350, leadWidthPx: 40);

        Assert.True(geometry.IsInLead(10, 0));
        Assert.True(geometry.IsInLead(110, 100));
        Assert.False(geometry.IsInLead(45, 0));
        Assert.False(geometry.IsInLead(145, 100));
    }

    [Fact] // ADR-0050/0004: a scrolled column under the band is not what a pixel there names
    public void Column_at_clamps_the_band_to_the_first_column_clear_of_it()
    {
        var geometry = new ColumnGeometry(FiveEqualColumns, 0, 350, leadWidthPx: 40);

        // Scrolled 150: the band covers content 150-190; the column standing at its right
        // edge is column 1 (content 140-240).
        Assert.Equal(1, geometry.ColumnAt(160, 150));
        Assert.Equal(0, geometry.ColumnAt(10, 0));
        Assert.Equal(0, geometry.ColumnAt(60, 0));
    }

    [Fact] // ADR-0050/0004: a column is revealed clear of the band, as it is clear of a pinned block
    public void Reveal_left_aligns_clear_of_the_band()
    {
        var geometry = new ColumnGeometry(FiveEqualColumns, 0, 350, leadWidthPx: 40);

        // Scrolled to the end (190), content 190-230 is under the band. Column 1 starts
        // at 140; left-aligned clear of the 40px band is 100.
        Assert.Equal(100, geometry.ScrollLeftToReveal(1, 190));
        // And from 100 the readable run is content 140-450: columns 1 to 4.
        Assert.Equal(new ColumnRange(1, 4), geometry.ScrollableSliceAt(100, virtualise: true));
    }

    [Fact] // ADR-0050: a lead that is not a width is refused by name
    public void A_negative_lead_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnGeometry(FiveEqualColumns, 0, 350, leadWidthPx: -1));
    }
}
