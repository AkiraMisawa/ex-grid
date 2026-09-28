using ExGrid.Keys;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0052's "What the third run settled", transcribed key for key from Excel's sequences in
/// verification/2026-09-28-windows-excel-3/active-cell.md, items 1–4 (SL-5, KB-43). Cells and
/// ranges are written in A1 notation, as the record writes them; a Selection is compared with
/// Excel's <c>Selection.Address</c>, area by area, in its order.
/// </summary>
public class ActiveCellThirdRunTests
{
    private static readonly GridExtent Grid = new(1000, 60);

    // ---- A1 notation --------------------------------------------------------------------

    private static CellPosition Cell(string a1)
    {
        var i = 0;
        var column = 0;
        while (i < a1.Length && char.IsLetter(a1[i]))
            column = column * 26 + (a1[i++] - 'A' + 1);
        return new(int.Parse(a1[i..]) - 1, column - 1);
    }

    private static SelectionRange Range(string a1)
    {
        var parts = a1.Split(':');
        return SelectionRange.FromCorners(Cell(parts[0]), Cell(parts.Length == 2 ? parts[1] : parts[0]));
    }

    private static string Name(CellPosition cell)
    {
        var letters = "";
        for (var c = cell.Column + 1; c > 0; c = (c - 1) / 26)
            letters = (char)('A' + (c - 1) % 26) + letters;
        return letters + (cell.Row + 1);
    }

    private static string Address(GridSelection selection) => string.Join(",", selection.Ranges.Select(r =>
        r.CellCount == 1
            ? Name(new(r.TopRow, r.LeftColumn))
            : $"{Name(new(r.TopRow, r.LeftColumn))}:{Name(new(r.BottomRow, r.RightColumn))}"));

    // ---- Gestures, as a hand makes them ---------------------------------------------------

    /// <summary>A click, then a Shift+click: the first range.</summary>
    private static GridSelection Made(string a1)
    {
        var parts = a1.Split(':');
        return GridSelection.Empty.Click(Cell(parts[0]), Grid).ExtendTo(Cell(parts[^1]), Grid);
    }

    /// <summary>A Ctrl+drag from the range's first corner to its other: a further range.</summary>
    private static GridSelection CtrlDrag(GridSelection selection, string a1)
    {
        var parts = a1.Split(':');
        return selection.ToggleRange(Cell(parts[0]), Grid).ExtendTo(Cell(parts[^1]), Grid);
    }

    private static GridSelection Enter(GridSelection s) => s.CycleFocus(CycleOrder.ColumnMajor, backward: false, Grid);
    private static GridSelection ShiftEnter(GridSelection s) => s.CycleFocus(CycleOrder.ColumnMajor, backward: true, Grid);
    private static GridSelection Tab(GridSelection s) => s.CycleFocus(CycleOrder.RowMajor, backward: false, Grid);

    private static GridSelection Times(GridSelection s, int count, Func<GridSelection, GridSelection> key)
    {
        for (var i = 0; i < count; i++)
            s = key(s);
        return s;
    }

    /// <summary>The Focus after each press of <paramref name="key"/>, and the selection left.</summary>
    private static (string Visited, GridSelection After) Presses(GridSelection s, int count, Func<GridSelection, GridSelection> key)
    {
        var visited = new List<string>();
        for (var i = 0; i < count; i++)
        {
            s = key(s);
            visited.Add(Name(s.Focus));
        }
        return (string.Join(" ", visited), s);
    }

    // ---- Item 1: Shift+arrow after Enter has cycled back into the earlier range -----------

    [Fact] // ADR-0052 third run, item 1: Enter ×4 from D4 reaches A1; Shift+Down gives A1:B3, Shift+Right A1:C3; D4:E5 stands
    public void After_enter_cycles_back_the_range_holding_the_focus_extends_from_the_focus()
    {
        var made = CtrlDrag(Made("A1:B2"), "D4:E5");
        Assert.Equal("A1:B2,D4:E5", Address(made));
        Assert.Equal("D4", Name(made.Focus));

        var cycled = Times(made, 4, Enter);
        Assert.Equal("A1", Name(cycled.Focus));

        var down = cycled.Extend(GridDirection.Down, Grid);
        Assert.Equal("A1:B3,D4:E5", Address(down));
        Assert.Equal("A1", Name(down.Focus));

        var right = down.Extend(GridDirection.Right, Grid);
        Assert.Equal("A1:C3,D4:E5", Address(right));
        Assert.Equal("A1", Name(right.Focus));
    }

    [Fact] // ADR-0052 third run, item 1: from A2 (Enter ×5), Shift+Down gives A2:B2 — the corner opposite A2 moves down
    public void From_the_second_cell_shift_down_moves_the_opposite_corner()
    {
        var cycled = Times(CtrlDrag(Made("A1:B2"), "D4:E5"), 5, Enter);
        Assert.Equal("A2", Name(cycled.Focus));

        var down = cycled.Extend(GridDirection.Down, Grid);
        Assert.Equal("A2:B2,D4:E5", Address(down));
        Assert.Equal("A2", Name(down.Focus));
    }

    // ---- Item 2: cycling through disjoint ranges in creation order ------------------------

    [Theory] // ADR-0052 third run, item 2 (KB-43): Enter ×10, Tab ×10, Shift+Enter ×3 in one sequence, both creation orders
    [InlineData("A1:B2", "D4:E5", "A1:B2,D4:E5", "D4",
        "D5 E4 E5 A1 A2 B1 B2 D4 D5 E4",
        "D5 E5 A1 B1 A2 B2 D4 E4 D5 E5",
        "E4 D5 D4")]
    [InlineData("D4:E5", "A1:B2", "D4:E5,A1:B2", "A1",
        "A2 B1 B2 D4 D5 E4 E5 A1 A2 B1",
        "A2 B2 D4 E4 D5 E5 A1 B1 A2 B2",
        "B1 A2 A1")]
    public void Enter_tab_and_shift_enter_cycle_through_the_ranges_in_the_order_they_were_made(
        string first, string second, string address, string start, string enters, string tabs, string shiftEnters)
    {
        var made = CtrlDrag(Made(first), second);
        Assert.Equal(address, Address(made));
        Assert.Equal(start, Name(made.Focus));

        var (enterVisits, afterEnter) = Presses(made, 10, Enter);
        Assert.Equal(enters, enterVisits);

        var (tabVisits, afterTab) = Presses(afterEnter, 10, Tab);
        Assert.Equal(tabs, tabVisits);

        var (shiftEnterVisits, afterShiftEnter) = Presses(afterTab, 3, ShiftEnter);
        Assert.Equal(shiftEnters, shiftEnterVisits);

        Assert.Equal(address, Address(afterShiftEnter)); // cycling never changes the Selection
    }

    [Fact] // ADR-0052 third run, item 2: Tab after Enter continues from the cell Enter reached — E4, Tab, is D5
    public void Tab_after_enter_continues_by_rows_from_the_cell_enter_reached()
    {
        var atE4 = Times(CtrlDrag(Made("A1:B2"), "D4:E5"), 2, Enter);
        Assert.Equal("E4", Name(atE4.Focus));

        Assert.Equal("D5", Name(Tab(atE4).Focus));
    }

    // ---- Item 3: Ctrl+click take-out resets the Focus -------------------------------------

    [Theory] // ADR-0052 third run, item 3 (SL-5): taking the Focus's own cell out of A1:C3 leaves A1 active, and Shift+Down extends A1's fragment
    [InlineData("B2", "Enter", 4, "A3:C3,C2,A2,A1:C1", "A3:C3,C2,A2,A1:C2")]
    [InlineData("C3", "Shift+Enter", 1, "A3:B3,A1:C2", "A3:B3,A1:C3")]
    [InlineData("C1", "Enter", 6, "A2:C3,A1:B1", "A2:C3,A1:B2")]
    [InlineData("A3", "Enter", 2, "B3:C3,A1:C2", "B3:C3,A1:C3")]
    public void Taking_out_the_moved_focus_puts_the_focus_on_the_first_remaining_cell_by_rows(
        string focusBefore, string key, int presses, string afterTakeOut, string afterShiftDown)
    {
        var moved = Times(Made("A1:C3"), presses, key == "Enter" ? Enter : ShiftEnter);
        Assert.Equal(focusBefore, Name(moved.Focus));

        var takenOut = moved.ToggleRange(Cell(focusBefore), Grid);
        Assert.Equal(afterTakeOut, Address(takenOut));
        Assert.Equal("A1", Name(takenOut.Focus));

        var down = takenOut.Extend(GridDirection.Down, Grid);
        Assert.Equal(afterShiftDown, Address(down));
        Assert.Equal("A1", Name(down.Focus));
    }

    [Theory] // ADR-0052 third run, item 3 (SL-5): a cell taken out of the earlier range leaves the Focus on D4, and Shift+Down extends D4:E5
    [InlineData("A1", "A2:B2,B1,D4:E5", "A2:B2,B1,D4:E6")]
    [InlineData("B2", "A2,A1:B1,D4:E5", "A2,A1:B1,D4:E6")]
    public void Taking_out_a_cell_of_the_earlier_range_leaves_the_focus_in_the_range_made_last(
        string clicked, string afterTakeOut, string afterShiftDown)
    {
        var made = CtrlDrag(Made("A1:B2"), "D4:E5");

        var takenOut = made.ToggleRange(Cell(clicked), Grid);
        Assert.Equal(afterTakeOut, Address(takenOut));
        Assert.Equal("D4", Name(takenOut.Focus));

        var down = takenOut.Extend(GridDirection.Down, Grid);
        Assert.Equal(afterShiftDown, Address(down));
        Assert.Equal("D4", Name(down.Focus));
    }

    [Fact] // ADR-0052 third run, item 3 (SL-5): Enter cycles the Focus to A1, then Ctrl+click on E5 leaves D4 active — not A1
    public void A_take_out_moves_the_focus_to_the_range_made_last_wherever_cycling_had_moved_it()
    {
        var cycled = Times(CtrlDrag(Made("A1:B2"), "D4:E5"), 4, Enter);
        Assert.Equal("A1", Name(cycled.Focus));

        var takenOut = cycled.ToggleRange(Cell("E5"), Grid);
        Assert.Equal("A1:B2,D5,D4:E4", Address(takenOut));
        Assert.Equal("D4", Name(takenOut.Focus));

        var down = takenOut.Extend(GridDirection.Down, Grid);
        Assert.Equal("A1:B2,D5,D4:E5", Address(down));
        Assert.Equal("D4", Name(down.Focus));
    }

    // ---- Item 4: fragment order, and the order Enter visits them ---------------------------

    [Theory] // ADR-0052 third run, item 4 (SL-5, KB-43): the fragments are listed bottom to top, and Enter visits the Focus's fragment, then the others in that order
    [InlineData("B2", "A3:C3,C2,A2,A1:C1", "A1", "B1 C1 A3 B3 C3 C2 A2 A1 B1 C1")]
    [InlineData("C3", "A3:B3,A1:C2", "A1", "A2 B1 B2 C1 C2 A3 B3 A1 A2 B1")]
    [InlineData("A1", "A2:C3,B1:C1", "B1", "C1 A2 A3 B2 B3 C2 C3 B1 C1 A2")]
    public void Fragments_are_listed_bottom_to_top_and_enter_visits_them_from_the_focus(
        string takenOut, string address, string focus, string enters)
    {
        var selection = Made("A1:C3").ToggleRange(Cell(takenOut), Grid);
        Assert.Equal(address, Address(selection));
        Assert.Equal(focus, Name(selection.Focus));

        var (visited, after) = Presses(selection, 10, Enter);
        Assert.Equal(enters, visited);
        Assert.Equal(address, Address(after));
    }
    [Theory] // ADR-0052 third run, item 4: Ctrl+. from A2 (Enter ×1) or B2 (Enter ×4) in A1:C3 goes to A1 first, then clockwise
    [InlineData(1, "A2")]
    [InlineData(4, "B2")]
    public void Ctrl_period_from_a_cell_on_no_corner_goes_to_the_top_left_then_clockwise(int enters, string start)
    {
        var selection = Times(Made("A1:C3"), enters, Enter);
        Assert.Equal(start, Name(selection.Focus));

        var (visited, after) = Presses(selection, 5, s => s.MoveFocusToNextCorner(Grid));
        Assert.Equal("A1 C1 C3 A3 A1", visited);
        Assert.Equal("A1:C3", Address(after));
    }

    [Fact] // ADR-0052 third run, item 4: C3, Ctrl+Space selects C:C; Shift+Down changes nothing; Shift+Right gives C:D, C3 active
    public void After_ctrl_space_shift_down_changes_nothing_and_shift_right_widens()
    {
        var column = GridSelection.Empty.Click(Cell("C3"), Grid).SelectWholeColumns(Grid);
        var wholeC = new SelectionRange(0, 2, Grid.RowCount, 1);
        Assert.Equal([wholeC], column.Ranges);

        var down = column.Extend(GridDirection.Down, Grid);
        Assert.Equal([wholeC], down.Ranges);
        Assert.Equal("C3", Name(down.Focus));

        var right = down.Extend(GridDirection.Right, Grid);
        Assert.Equal([new SelectionRange(0, 2, Grid.RowCount, 2)], right.Ranges);
        Assert.Equal("C3", Name(right.Focus));

        Assert.Equal(right.Ranges, column.Extend(GridDirection.Right, Grid).Ranges); // Shift+Right alone gives C:D as well
    }
}
