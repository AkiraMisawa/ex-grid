using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0052's wiring: the Focus is Excel's active cell and stays put while a range is
/// extended, and the grid keeps the moving end — the Extent — in view instead. The rules
/// themselves are pinned in the pure suite (ActiveCellTests); what is pinned here is what the
/// component does with them: which cell it reveals, what <c>aria-activedescendant</c> names,
/// the three keys new to ExGrid, and the Name Box during a drag.
///
/// 200 rows of 20px under a 20px header in a 120px Viewport, so rows 0-4 are on screen; 100
/// columns of 100px in 350px. Cell (r, c) is pressed at (c × 100 + 50, r × 20 + 10) while
/// the grid is scrolled to the top.
/// </summary>
public class ActiveCellWiringTests : GridTestContext
{
    private const double RowHeightPx = 20;
    private const int FullyVisibleRows = 5;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection>? onSelection = null,
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, TestRows.Wide(100))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, onSelection ?? (_ => { }));
            extra?.Invoke(ps);
        });

    private static Task PressCellAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, int row, int column, bool shift = false, bool ctrl = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            OffsetX = (column * 100) + 50,
            OffsetY = (row * RowHeightPx) + 10,
            Button = 0,
            Buttons = 1,
            ShiftKey = shift,
            CtrlKey = ctrl,
        });

    private static Task DragToCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs
            { OffsetX = (column * 100) + 50, OffsetY = (row * RowHeightPx) + 10, Buttons = 1 });

    private static Task ReleaseAsync(IRenderedComponent<ExGrid<TestRow>> cut, int row, int column)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs
            { OffsetX = (column * 100) + 50, OffsetY = (row * RowHeightPx) + 10, Button = 0, Buttons = 0 });

    private static Task KeyAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    /// <summary>Feeds the browser's answer to the last scroll write back, as the browser's
    /// scroll event would.</summary>
    private async Task AnswerLastScrollAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var (top, left) = Js.ScrolledTo[^1];
        await ScrollToAsync(cut.Find(".ex-scroller"), top, left);
    }

    private static string? ActiveDescendant(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

    [Fact] // ADR-0052 case 1: Shift+Down past the Viewport's edge scrolls to keep the Extent in view; the Focus stays
    public async Task Extending_reveals_the_extent_not_the_focus()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 4, 0);                           // the last row on screen

        await KeyAsync(cut, "ArrowDown", shift: true);

        Assert.Equal(new CellPosition(4, 0), selection!.Focus);
        Assert.Equal(new CellPosition(5, 0), selection.Extent);
        // Row 5 brought to the bottom edge: one row down.
        Assert.Equal(RowHeightPx, Js.ScrolledTo[^1].Top);
    }

    [Fact] // ADR-0052 case 1: a move of the Extent back inside the view scrolls nothing
    public async Task Shrinking_back_inside_the_view_scrolls_nothing()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 4, 0);
        await KeyAsync(cut, "ArrowDown", shift: true);
        await KeyAsync(cut, "ArrowDown", shift: true);
        await AnswerLastScrollAsync(cut);
        var writes = Js.ScrolledTo.Count;

        await KeyAsync(cut, "ArrowUp", shift: true);

        Assert.Equal(writes, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0052 case 1: sideways too, the Extent's column is revealed while the Focus's is left where it is
    public async Task Extending_right_reveals_the_extents_column()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 0, 2);                           // column 2 ends at 300 of 350

        await KeyAsync(cut, "ArrowRight", shift: true);            // column 3 ends at 400

        Assert.True(Js.ScrolledTo[^1].Left > 0);
    }

    [Fact] // ADR-0052 case 11: Shift+PageDown moves the Viewport a page with the Extent, leaving the Focus off screen
    public async Task Shift_pagedown_moves_the_view_with_the_extent()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 1, 1);

        await KeyAsync(cut, "PageDown", shift: true);
        await AnswerLastScrollAsync(cut);
        await KeyAsync(cut, "PageDown", shift: true);

        Assert.Equal(new CellPosition(1, 1), selection!.Focus);
        Assert.Equal(new CellPosition(1 + (2 * FullyVisibleRows), 1), selection.Extent);
        Assert.Equal(2 * FullyVisibleRows * RowHeightPx, Js.ScrolledTo[^1].Top);
    }

    [Fact] // ADR-0052/0033: aria-activedescendant names the Focus, which extension does not move
    public async Task Activedescendant_stays_on_the_focus_while_extending()
    {
        var cut = RenderGrid();
        await PressCellAsync(cut, 1, 1);

        await KeyAsync(cut, "ArrowDown", shift: true);
        await KeyAsync(cut, "ArrowRight", shift: true);

        Assert.EndsWith("r1c1", ActiveDescendant(cut));
    }

    [Fact] // ADR-0052 case 7: Ctrl+Backspace scrolls the Focus back into view and changes nothing else
    public async Task Ctrl_backspace_reveals_the_focus_and_changes_nothing()
    {
        var reported = new List<GridSelection>();
        var cut = RenderGrid(reported.Add);
        await PressCellAsync(cut, 1, 1);
        await KeyAsync(cut, "PageDown", shift: true);
        await AnswerLastScrollAsync(cut);
        await KeyAsync(cut, "PageDown", shift: true);
        await AnswerLastScrollAsync(cut);
        var before = reported[^1];
        var changes = reported.Count;

        await KeyAsync(cut, "Backspace", ctrl: true);

        Assert.Equal(changes, reported.Count);                     // the Selection did not change
        Assert.Equal(RowHeightPx, Js.ScrolledTo[^1].Top);          // row 1 brought back to the top edge
        Assert.Equal(before.Focus, new CellPosition(1, 1));
    }

    [Fact] // ADR-0052 case 7: Shift+Backspace collapses the Selection to the Focus
    public async Task Shift_backspace_collapses_to_the_focus()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 3, 2);
        await PressCellAsync(cut, 1, 0, shift: true);

        await KeyAsync(cut, "Backspace", shift: true);

        Assert.Equal([new SelectionRange(3, 2, 1, 1)], selection!.Ranges);
        Assert.EndsWith("r3c2", ActiveDescendant(cut));
    }

    [Fact] // ADR-0052 case 8: Ctrl+. walks the Focus round the range's corners, and aria-activedescendant follows it
    public async Task Ctrl_period_moves_the_focus_to_the_next_corner()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 2, 2, shift: true);

        await KeyAsync(cut, ".", ctrl: true);

        Assert.Equal(new CellPosition(0, 2), selection!.Focus);
        Assert.Equal([new SelectionRange(0, 0, 3, 3)], selection.Ranges);
        Assert.EndsWith("r0c2", ActiveDescendant(cut));
    }

    [Fact] // ADR-0052 case 6 / third run: Ctrl+click on the Focus's own cell moves the Focus to the first remaining cell by rows, and begins no drag
    public async Task Ctrl_click_on_the_focus_cell_moves_the_focus_on_and_begins_no_drag()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await PressCellAsync(cut, 0, 0);
        await PressCellAsync(cut, 2, 2, shift: true);

        await PressCellAsync(cut, 0, 0, ctrl: true);

        Assert.Equal(new CellPosition(0, 1), selection!.Focus);
        Assert.False(selection.Contains(new CellPosition(0, 0)));
        Assert.EndsWith("r0c1", ActiveDescendant(cut));
        // No drag began: nothing listens for the moves that would extend from the Focus.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => DragToCellAsync(cut, 4, 4));
    }

    [Fact] // ADR-0052 case 2: while a drag's button is down, the Name Box offers the range's size; on release it names the Focus
    public async Task The_name_box_shows_the_size_while_the_button_is_down()
    {
        var cut = RenderGrid(extra: ps => ps
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"))
            .Add(g => g.NameBoxSizeLabel, range => FormattableString.Invariant($"{range.RowCount}R x {range.ColumnCount}C")));
        string NameBox() => cut.Find("input.ex-name-box").GetAttribute("value") ?? "";

        await PressCellAsync(cut, 3, 2);                           // the drag starts on C4
        Assert.Equal("R4C3", NameBox());                           // one cell: its name, not 1R x 1C
        await DragToCellAsync(cut, 0, 0);                          // up and left, to A1
        Assert.Equal("4R x 3C", NameBox());

        await ReleaseAsync(cut, 0, 0);
        Assert.Equal("R4C3", NameBox());                           // the Focus: where the button went down
    }

    [Fact] // ADR-0052/0021: keyboard extension is not offered as a size — the grid cannot tell when Shift is released
    public async Task Keyboard_extension_keeps_the_focus_label()
    {
        var cut = RenderGrid(extra: ps => ps
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"))
            .Add(g => g.NameBoxSizeLabel, range => FormattableString.Invariant($"{range.RowCount}R x {range.ColumnCount}C")));
        await PressCellAsync(cut, 1, 1);
        await ReleaseAsync(cut, 1, 1);

        await KeyAsync(cut, "ArrowDown", shift: true);

        Assert.Equal("R2C2", cut.Find("input.ex-name-box").GetAttribute("value"));
    }

    [Fact] // ADR-0052: undeclared, a drag leaves the Name Box naming the Focus throughout
    public async Task Without_a_size_label_the_name_box_names_the_focus_during_a_drag()
    {
        var cut = RenderGrid(extra: ps => ps
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}")));

        await PressCellAsync(cut, 3, 2);
        await DragToCellAsync(cut, 0, 0);

        Assert.Equal("R4C3", cut.Find("input.ex-name-box").GetAttribute("value"));
    }
}
