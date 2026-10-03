using ExGrid.Keys;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0052's rules as Excel showed them (verification/2026-09-27-windows-excel-2/active-cell.md):
/// the Focus is Excel's active cell and stays put while a range is extended; the Extent is the
/// end that moves. Each test names the case it transcribes. A1 is (0, 0).
/// </summary>
public class ActiveCellTests
{
    private static readonly GridExtent Grid = new(1000, 60);

    [Fact] // ADR-0052 case 1: Shift+Down twice and Shift+Right from B2 give B2:C4, with B2 still the Focus
    public void Shift_arrows_move_the_extent_and_leave_the_focus()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid)
            .Extend(GridDirection.Down, Grid)
            .Extend(GridDirection.Down, Grid)
            .Extend(GridDirection.Right, Grid);

        Assert.Equal([new SelectionRange(1, 1, 3, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(1, 1), selection.Focus);
        Assert.Equal(new CellPosition(3, 2), selection.Extent);
    }

    [Fact] // ADR-0052 case 5: with D5 selected, Ctrl+drag A1:B2 adds a range holding the Focus at A1, and Shift+Down grows it to A1:B3
    public void A_ctrl_drag_adds_a_range_whose_start_is_the_focus()
    {
        var selection = GridSelection.Empty.Click(new(4, 3), Grid)   // D5
            .ToggleRange(new(0, 0), Grid)                            // Ctrl+press on A1
            .ExtendTo(new(1, 1), Grid);                              // dragged to B2

        Assert.Equal([new SelectionRange(4, 3, 1, 1), new SelectionRange(0, 0, 2, 2)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);

        var extended = selection.Extend(GridDirection.Down, Grid);
        Assert.Equal([new SelectionRange(4, 3, 1, 1), new SelectionRange(0, 0, 3, 2)], extended.Ranges);
        Assert.Equal(new CellPosition(0, 0), extended.Focus);
    }

    [Fact] // ADR-0052 case 5 (the ADR's reading): after Enter cycles the Focus into an earlier range, that range extends
    public void The_range_holding_the_focus_extends_even_when_it_is_not_the_last()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid)
            .ExtendTo(new(1, 1), Grid)                                // A1:B2
            .ToggleRange(new(4, 3), Grid)                             // D5
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid); // wraps into A1:B2 at A1

        var extended = selection.Extend(GridDirection.Right, Grid);

        Assert.Equal([new SelectionRange(0, 0, 2, 3), new SelectionRange(4, 3, 1, 1)], extended.Ranges);
        Assert.Equal(new CellPosition(0, 0), extended.Focus);
    }

    [Fact] // ADR-0052 case 7: Shift+Backspace collapses the Selection to the Focus — B2 from B2:D4, D4 from D4:B2
    public void Shift_backspace_collapses_to_the_focus()
    {
        var fromB2 = GridSelection.Empty.Click(new(1, 1), Grid).ExtendTo(new(3, 3), Grid).CollapseToFocus(Grid);
        Assert.Equal([new SelectionRange(1, 1, 1, 1)], fromB2.Ranges);
        Assert.Equal(new CellPosition(1, 1), fromB2.Focus);

        var fromD4 = GridSelection.Empty.Click(new(3, 3), Grid).ExtendTo(new(1, 1), Grid).CollapseToFocus(Grid);
        Assert.Equal([new SelectionRange(3, 3, 1, 1)], fromD4.Ranges);
    }

    [Fact] // ADR-0052 case 7: Shift+Backspace after Enter collapses to where Enter put the Focus
    public void Shift_backspace_after_cycling_collapses_to_the_cycled_focus()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid).ExtendTo(new(3, 3), Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid)   // B4
            .CollapseToFocus(Grid);

        Assert.Equal([new SelectionRange(3, 1, 1, 1)], selection.Ranges);
    }

    [Fact] // ADR-0052 case 8: Ctrl+. walks A1:C3's corners clockwise from A1 — C1, C3, A3, A1, C1 — and the Selection stays
    public void Ctrl_period_walks_the_corners_clockwise()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid);
        var visited = new List<CellPosition>();
        for (var i = 0; i < 5; i++)
        {
            selection = selection.MoveFocusToNextCorner(Grid);
            visited.Add(selection.Focus);
            Assert.Equal([new SelectionRange(0, 0, 3, 3)], selection.Ranges);
        }

        Assert.Equal([new CellPosition(0, 2), new(2, 2), new(2, 0), new(0, 0), new(0, 2)], visited);
    }

    [Fact] // ADR-0052 case 8: with A1:C3,E5:F6 and E5 active, Ctrl+. walks only E5:F6's corners
    public void Ctrl_period_walks_only_the_range_holding_the_focus()
    {
        var selection = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid)
            .ToggleRange(new(4, 4), Grid).ExtendTo(new(5, 5), Grid);
        var visited = new List<CellPosition>();
        for (var i = 0; i < 5; i++)
        {
            selection = selection.MoveFocusToNextCorner(Grid);
            visited.Add(selection.Focus);
        }

        Assert.Equal([new CellPosition(4, 5), new(5, 5), new(5, 4), new(4, 4), new(4, 5)], visited);
        Assert.Equal(2, selection.Ranges.Count);
    }

    [Fact] // ADR-0052: Ctrl+. on a range one row deep skips the corner it shares, and on one cell does nothing
    public void Ctrl_period_passes_over_shared_corners_and_stays_on_a_single_cell()
    {
        var row = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(0, 2), Grid);
        var first = row.MoveFocusToNextCorner(Grid);
        Assert.Equal(new CellPosition(0, 2), first.Focus);
        Assert.Equal(new CellPosition(0, 0), first.MoveFocusToNextCorner(Grid).Focus);

        var single = GridSelection.Empty.Click(new(3, 3), Grid);
        Assert.Equal(single, single.MoveFocusToNextCorner(Grid));
    }

    [Fact] // ADR-0052 (not observed in Excel): from a cell on an edge, Ctrl+. goes to the corner ending that edge clockwise; from inside, to the top-left
    public void Ctrl_period_from_off_a_corner_goes_to_the_next_corner_clockwise()
    {
        var a1c3 = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid);
        var b1 = a1c3.CycleFocus(CycleOrder.RowMajor, backward: false, Grid);
        Assert.Equal(new CellPosition(0, 2), b1.MoveFocusToNextCorner(Grid).Focus);

        var b2 = b1.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
        Assert.Equal(new CellPosition(1, 1), b2.Focus);
        Assert.Equal(new CellPosition(0, 0), b2.MoveFocusToNextCorner(Grid).Focus);
    }

    [Fact] // ADR-0052 case 11: from B2, Ctrl+Shift+End then Ctrl+Shift+Home keep B2 the Focus; Shift+PageDown moves only the Extent
    public void Extending_to_the_corners_and_by_a_viewport_keeps_the_focus()
    {
        var b2 = GridSelection.Empty.Click(new(1, 1), Grid);

        var toEnd = b2.ExtendToEdge(GridDirection.Down, Grid).ExtendToEdge(GridDirection.Right, Grid);
        Assert.Equal(new CellPosition(1, 1), toEnd.Focus);
        Assert.Equal(new CellPosition(999, 59), toEnd.Extent);

        var toHome = toEnd.ExtendToEdge(GridDirection.Up, Grid).ExtendToEdge(GridDirection.Left, Grid);
        Assert.Equal([new SelectionRange(0, 0, 2, 2)], toHome.Ranges); // A1:B2
        Assert.Equal(new CellPosition(1, 1), toHome.Focus);

        var paged = b2.ExtendByViewport(57, Grid).ExtendByViewport(57, Grid).ExtendByViewport(-57, Grid);
        Assert.Equal([new SelectionRange(1, 1, 58, 1)], paged.Ranges); // B2:B59
        Assert.Equal(new CellPosition(1, 1), paged.Focus);
        Assert.Equal(new CellPosition(58, 1), paged.Extent);
    }

    [Theory] // ADR-0052: the three keys new to ExGrid are the core's, outside editing
    [InlineData(".", true, false, GridKeyKind.MoveFocusToNextCorner)]
    [InlineData("Backspace", true, false, GridKeyKind.RevealFocus)]
    [InlineData("Backspace", false, true, GridKeyKind.CollapseToFocus)]
    public void The_new_keys_resolve(string key, bool ctrl, bool shift, GridKeyKind kind)
    {
        Assert.Equal(kind, GridKeys.Resolve(key, ctrl, shift, alt: false, meta: false, metaIsPrimary: false).Kind);
        Assert.Contains(GridKeys.Canonical(key, ctrl, shift, alt: false, meta: false, metaIsPrimary: false), GridKeys.Taken);
    }

    [Fact] // ADR-0052/0010: a bare Backspace is not the core's — it stays the editor's and the page's
    public void A_bare_backspace_is_not_taken()
    {
        Assert.DoesNotContain("Backspace", GridKeys.Taken);
    }
}
