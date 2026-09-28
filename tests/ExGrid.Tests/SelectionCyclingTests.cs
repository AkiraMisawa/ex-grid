using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Transcribes the ADR-0012 Enter/Tab diagram. Its range B2:D4 is rows 1–3 × columns 1–3
/// zero-based, so B2 = (1, 1) and D4 = (3, 3).
/// </summary>
public class SelectionCyclingTests
{
    private static readonly GridExtent Grid = new(10, 10);

    /// <summary>B2:D4 selected with the Focus at B2, as in the ADR-0012 diagram: from B2,
    /// which stays the Focus while the Extent goes to D4 (ADR-0052).</summary>
    private static GridSelection B2D4WithFocusAtB2()
        => GridSelection.Empty.Click(new(1, 1), Grid).ExtendTo(new(3, 3), Grid);

    private static IReadOnlyList<CellPosition> Walk(GridSelection selection, CycleOrder order, bool backward, int steps)
    {
        var visited = new List<CellPosition>();
        for (var i = 0; i < steps; i++)
        {
            selection = selection.CycleFocus(order, backward, Grid);
            visited.Add(selection.Focus);
        }
        return visited;
    }

    [Fact] // ADR-0012: Enter runs down columns and wraps back to the start
    public void Enter_cycles_column_major_and_wraps()
    {
        Assert.Equal(
        [
            new CellPosition(2, 1), new(3, 1),
            new(1, 2), new(2, 2), new(3, 2),
            new(1, 3), new(2, 3), new(3, 3),
            new(1, 1), // back to B2
        ], Walk(B2D4WithFocusAtB2(), CycleOrder.ColumnMajor, backward: false, steps: 9));
    }

    [Fact] // ADR-0012: Tab runs across rows and wraps back to the start
    public void Tab_cycles_row_major_and_wraps()
    {
        Assert.Equal(
        [
            new CellPosition(1, 2), new(1, 3),
            new(2, 1), new(2, 2), new(2, 3),
            new(3, 1), new(3, 2), new(3, 3),
            new(1, 1), // back to B2
        ], Walk(B2D4WithFocusAtB2(), CycleOrder.RowMajor, backward: false, steps: 9));
    }

    [Fact] // ADR-0012: Shift+Enter runs the Enter sequence backwards — the first step wraps to D4
    public void Shift_enter_cycles_backwards()
    {
        Assert.Equal(
        [
            new CellPosition(3, 3), new(2, 3), new(1, 3),
            new(3, 2), new(2, 2), new(1, 2),
            new(3, 1), new(2, 1), new(1, 1),
        ], Walk(B2D4WithFocusAtB2(), CycleOrder.ColumnMajor, backward: true, steps: 9));
    }

    [Fact] // ADR-0012: Shift+Tab runs the Tab sequence backwards
    public void Shift_tab_cycles_backwards()
    {
        Assert.Equal(
        [
            new CellPosition(3, 3), new(3, 2), new(3, 1),
            new(2, 3), new(2, 2), new(2, 1),
            new(1, 3), new(1, 2), new(1, 1),
        ], Walk(B2D4WithFocusAtB2(), CycleOrder.RowMajor, backward: true, steps: 9));
    }

    [Fact] // ADR-0012/0052: the range stays selected while cycling; only the Focus moves
    public void Cycling_moves_only_the_focus()
    {
        var before = B2D4WithFocusAtB2();
        var after = before.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);

        Assert.Equal(before.Ranges, after.Ranges);
        Assert.Equal(new CellPosition(2, 1), after.Focus);
    }

    [Fact] // ADR-0052 case 4: C3:A1 selected from C3 makes C3 the Focus, and Enter from the last cell wraps to A1
    public void Enter_from_a_range_selected_backwards_wraps_to_its_first_cell()
    {
        var selection = GridSelection.Empty.Click(new(2, 2), Grid).ExtendTo(new(0, 0), Grid);
        Assert.Equal(new CellPosition(2, 2), selection.Focus);

        Assert.Equal(
            [new CellPosition(0, 0), new(1, 0), new(2, 0), new(0, 1)],
            Walk(selection, CycleOrder.ColumnMajor, backward: false, steps: 4));
    }

    [Fact] // ADR-0012: with disjoint ranges, cycling visits them in creation order
    public void Cycling_crosses_disjoint_ranges_in_creation_order()
    {
        // Range 1: (0,0)-(1,1); range 2: (5,5)-(6,6); Focus at range 2's last cell.
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(1, 1), Grid)
            .ToggleRange(new(6, 6), Grid)
            .ExtendTo(new(5, 5), Grid);

        // Past the last range's last cell, wrap to the first range's first cell,
        // walk range 1 column-major, then enter range 2 at its top-left.
        Assert.Equal(
        [
            new CellPosition(0, 0),
            new(1, 0), new(0, 1), new(1, 1),
            new(5, 5),
        ], Walk(selection, CycleOrder.ColumnMajor, backward: false, steps: 5));
    }

    [Fact] // ADR-0012: the backward mirror — before a range's first cell, go to the previous range's last
    public void Cycling_backward_enters_the_previous_range_at_its_last_cell()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid); // Focus at range 2's only (= first) cell

        var stepped = selection.CycleFocus(CycleOrder.ColumnMajor, backward: true, Grid);

        Assert.Equal(new CellPosition(1, 1), stepped.Focus); // range 1's bottom-right
    }

    [Fact] // ADR-0012: with no range, Enter moves down, Tab moves right, and the selection follows
    public void Single_cell_enter_moves_down_and_tab_moves_right()
    {
        var start = GridSelection.Empty.Click(new(2, 2), Grid);

        var entered = start.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
        Assert.Equal([new SelectionRange(3, 2, 1, 1)], entered.Ranges);
        Assert.Equal(new CellPosition(3, 2), entered.Focus);

        var tabbed = start.CycleFocus(CycleOrder.RowMajor, backward: false, Grid);
        Assert.Equal([new SelectionRange(2, 3, 1, 1)], tabbed.Ranges);

        var reversed = start.CycleFocus(CycleOrder.ColumnMajor, backward: true, Grid);
        Assert.Equal(new CellPosition(1, 2), reversed.Focus);
    }

    [Fact] // ADR-0012: a single cell at the grid edge clamps — no wrap target is invented
    public void Single_cell_enter_and_tab_clamp_at_the_edge()
    {
        var small = new GridExtent(3, 3);

        var atBottom = GridSelection.Empty.Click(new(2, 1), small);
        Assert.Equal(new CellPosition(2, 1), atBottom.CycleFocus(CycleOrder.ColumnMajor, backward: false, small).Focus);

        var atRight = GridSelection.Empty.Click(new(1, 2), small);
        Assert.Equal(new CellPosition(1, 2), atRight.CycleFocus(CycleOrder.RowMajor, backward: false, small).Focus);
    }

    [Fact] // ADR-0012: cycling keeps advancing when the Focus sits where two ranges overlap
    public void Cycling_advances_when_the_focus_sits_in_an_overlap()
    {
        // Range 1 is (0,0)-(2,2); range 2 is (2,2)-(5,5), created last and holding the Focus,
        // which Ctrl+. walks from (5,5) to the corner the two ranges share (ADR-0052).
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(2, 2), Grid)
            .ToggleRange(new(5, 5), Grid)
            .ExtendTo(new(2, 2), Grid)
            .MoveFocusToNextCorner(Grid)   // (5,2)
            .MoveFocusToNextCorner(Grid);  // (2,2)
        Assert.Equal(new CellPosition(2, 2), selection.Focus);

        var stepped = selection.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);

        Assert.Equal(new CellPosition(3, 2), stepped.Focus); // walks range 2 — no fixpoint on the shared cell
    }

    [Fact] // ADR-0052 (withdrawing ADR-0012's re-anchoring): Shift+arrow after cycling into another range extends that range, from the edge opposite the Focus
    public void Shift_arrow_after_cycling_into_another_range_extends_the_range_holding_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(0, 0), Grid)
            .ExtendTo(new(1, 1), Grid)
            .ToggleRange(new(5, 5), Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: true, Grid); // Focus (1,1), range 1's bottom-right

        var extended = selection.Extend(GridDirection.Down, Grid);

        Assert.Equal(
        [
            new SelectionRange(1, 0, 1, 2), // the top edge, opposite the Focus, moved down
            new SelectionRange(5, 5, 1, 1), // the other range is untouched — not bridged into a block
        ], extended.Ranges);
        Assert.Equal(new CellPosition(1, 1), extended.Focus);
    }

    [Fact] // ADR-0052 case 3: A1:C3, Enter twice (A3), then Shift+Right moves the right edge and Shift+Down the top edge
    public void Shift_arrow_after_enter_moves_the_edge_opposite_the_focus()
    {
        var a1c3 = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid);
        var a3 = a1c3
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
        Assert.Equal(new CellPosition(2, 0), a3.Focus);

        var right = a3.Extend(GridDirection.Right, Grid);
        Assert.Equal([new SelectionRange(0, 0, 3, 4)], right.Ranges); // A1:D3
        var down = right.Extend(GridDirection.Down, Grid);
        Assert.Equal([new SelectionRange(1, 0, 2, 4)], down.Ranges);  // A2:D3
        Assert.Equal(new CellPosition(2, 0), down.Focus);
    }

    [Fact] // ADR-0052 case 3: A1:C3, Enter three times (B1), then Shift+Up moves the bottom edge
    public void Shift_up_with_the_focus_on_the_top_edge_moves_the_bottom_edge()
    {
        var b1 = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid)
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
        Assert.Equal(new CellPosition(0, 1), b1.Focus);

        var up = b1.Extend(GridDirection.Up, Grid);

        Assert.Equal([new SelectionRange(0, 0, 2, 3)], up.Ranges); // A1:C2
        Assert.Equal(new CellPosition(0, 1), up.Focus);
    }

    [Fact] // ADR-0052 case 3: with the Focus on neither edge of an axis (B2 in A1:C3), Shift+arrow along it changes nothing
    public void Shift_arrow_along_an_axis_where_the_focus_is_interior_changes_nothing()
    {
        var b2 = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(2, 2), Grid)
            .CycleFocus(CycleOrder.RowMajor, backward: false, Grid)       // Tab: B1
            .CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);   // Enter: B2
        Assert.Equal(new CellPosition(1, 1), b2.Focus);
        Assert.Equal(new CellPosition(1, 1), b2.Extent);

        Assert.Equal(b2, b2.Extend(GridDirection.Left, Grid));
        Assert.Equal(b2, b2.Extend(GridDirection.Down, Grid));
        Assert.Equal(b2, b2.ExtendToEdge(GridDirection.Right, Grid));
        Assert.Equal(b2, b2.ExtendByViewport(20, Grid));
    }

    [Fact] // ADR-0052: the Extent is recomputed from the Focus when cycling moves it — the corner opposite, or the Focus's own line inside
    public void The_extent_follows_the_focus_through_cycling()
    {
        var b2d4 = B2D4WithFocusAtB2();
        Assert.Equal(new CellPosition(3, 3), b2d4.Extent);

        var d4 = b2d4.CycleFocus(CycleOrder.ColumnMajor, backward: true, Grid);
        Assert.Equal(new CellPosition(3, 3), d4.Focus);
        Assert.Equal(new CellPosition(1, 1), d4.Extent);

        var b3 = b2d4.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
        Assert.Equal(new CellPosition(2, 3), b3.Extent); // row: the Focus's own; column: the right edge
    }

    [Fact] // ADR-0012: an out-of-range CycleOrder is refused, never silently row-major
    public void An_unknown_cycle_order_is_refused()
    {
        var selection = GridSelection.Empty.Click(new(1, 1), Grid);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => selection.CycleFocus((CycleOrder)2, backward: false, Grid));
    }

    [Fact] // ADR-0052 (withdrawing ADR-0012's detached state): after the Focus's own cell is taken out, cycling goes on from where the Focus now stands
    public void After_a_toggle_off_cycling_goes_on_from_the_focus()
    {
        var selection = GridSelection.Empty
            .Click(new(1, 1), Grid)
            .ExtendTo(new(2, 2), Grid)
            .ToggleRange(new(1, 1), Grid); // fragments: (2,1,1,2) and (1,2,1,1); the Focus goes on to (1,2)
        Assert.Equal(new CellPosition(1, 2), selection.Focus);

        var forward = selection.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
        Assert.Equal(new CellPosition(2, 1), forward.Focus);

        var backward = selection.CycleFocus(CycleOrder.ColumnMajor, backward: true, Grid);
        Assert.Equal(new CellPosition(2, 2), backward.Focus);
    }
}
