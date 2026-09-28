using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Headings' pure half (ADR-0050, item 1): a declared header click selects the whole
/// column, a Row Heading selects the whole row, and the Row Headings are a lead band in
/// the horizontal geometry that no column index names. Five 100px columns in a 350px
/// Viewport, with a 40px lead unless a test says otherwise.
/// </summary>
public class HeadingsSelectionTests
{
    private static readonly GridExtent Grid = new(100, 26);
    private static readonly double[] FiveEqualColumns = [100, 100, 100, 100, 100];

    [Fact] // ADR-0050/0052: a declared header click selects the whole column, the Focus on the row the holder says
    public void Select_column_is_the_whole_column_with_the_focus_on_the_given_row()
    {
        var selection = GridSelection.Empty.Click(new(3, 3), Grid).SelectColumn(5, Grid, focusRow: 12);

        Assert.Equal([new SelectionRange(0, 5, 100, 1)], selection.Ranges);
        Assert.Equal(new CellPosition(12, 5), selection.Focus);
    }

    [Fact] // ADR-0050: a Row Heading click selects the whole row, every column
    public void Select_row_is_the_whole_row_with_the_focus_on_the_given_column()
    {
        var selection = GridSelection.Empty.SelectRow(7, Grid, focusColumn: 4);

        Assert.Equal([new SelectionRange(7, 0, 1, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(7, 4), selection.Focus);
    }

    [Fact] // ADR-0050/0052: Shift+click on a Row Heading extends whole rows from the Focus's row, and the Focus stays
    public void Extend_to_row_selects_whole_rows_from_the_focus()
    {
        var selection = GridSelection.Empty.Click(new(10, 3), Grid).ExtendToRow(4, Grid);

        Assert.Equal([new SelectionRange(4, 0, 7, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(10, 3), selection.Focus);
    }

    [Fact] // ADR-0052 (Excel item 4): row 5's heading, then Shift+click on row 2's, gives 2:5 with the Focus still in row 5
    public void Shift_click_on_a_row_heading_above_keeps_the_focus_on_the_first_row_clicked()
    {
        var selection = GridSelection.Empty.SelectRow(4, Grid, focusColumn: 0).ExtendToRow(1, Grid);

        Assert.Equal([new SelectionRange(1, 0, 4, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(4, 0), selection.Focus);
    }

    [Fact] // ADR-0050: from nothing selected, Shift+click on a Row Heading selects that row alone
    public void Extend_to_row_from_empty_selects_that_row()
    {
        var selection = GridSelection.Empty.ExtendToRow(9, Grid, focusColumnIfEmpty: 2);

        Assert.Equal([new SelectionRange(9, 0, 1, 26)], selection.Ranges);
        Assert.Equal(new CellPosition(9, 2), selection.Focus);
    }

    [Fact] // ADR-0050/0012: whole rows stay whole under Shift+↓, as whole columns do under Shift+→
    public void A_whole_row_selection_stays_whole_when_extended_down()
    {
        var selection = GridSelection.Empty.SelectRow(5, Grid, 0).Extend(GridDirection.Down, Grid);

        Assert.Equal([new SelectionRange(5, 0, 2, 26)], selection.Ranges);
    }

    [Fact] // ADR-0050/0012 (DC-4): a row selected by its heading cycles only through real columns
    public void Tab_through_a_selected_row_never_leaves_the_column_space()
    {
        var extent = new GridExtent(10, 3);
        var selection = GridSelection.Empty.SelectRow(2, extent, 0);
        var visited = new List<CellPosition>();
        for (var i = 0; i < 4; i++)
        {
            selection = selection.CycleFocus(CycleOrder.RowMajor, backward: false, extent);
            visited.Add(selection.Focus);
        }

        Assert.Equal([new(2, 1), new(2, 2), new(2, 0), new(2, 1)], visited);
    }

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
