using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The keyboard, from the component's entry point down (ADR-0012). What happens above it
/// — the capture-phase listener, preventDefault, which grid has focus — is JavaScript and
/// is checked in a browser; everything reachable from OnKeyAsync is pinned here.
///
/// 100 columns of 100px in a 350px Viewport, 20px rows in a 100px Viewport of which the
/// header takes the first 20: five rows painted.
/// </summary>
public class GridKeyboardTests : GridTestContext
{
    private const double RowHeightPx = 20;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection>? onSelectionChanged = null,
        GridColumn<TestRow>[]? columns = null,
        Action<GridActionEventArgs<TestRow>>? onAction = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(200))
              .Add(g => g.TotalCount, 200)
              .Add(g => g.Columns, columns ?? TestRows.Wide(100))
              .Add(g => g.RowHeight, RowHeightPx)
              .Add(g => g.ViewportHeight, 100)
              .Add(g => g.ViewportWidth, 350);
            if (onSelectionChanged is not null)
                ps.Add(g => g.SelectionChanged, onSelectionChanged);
            if (onAction is not null)
                ps.Add(g => g.OnAction, onAction);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key,
        bool ctrl = false, bool shift = false, bool alt = false, bool meta = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, alt, meta));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0018: the root is the tab stop, so the browser's focus says which grid hears keys
    public void The_root_is_focusable()
    {
        var cut = RenderGrid();

        Assert.Equal("0", cut.Find(".ex-grid").GetAttribute("tabindex"));
    }

    [Fact] // ADR-0012: arrows collapse and move; Shift+arrow keeps the Anchor and grows the range
    public async Task Arrows_move_and_shift_arrows_extend()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickAsync(cut, 50, 10); // cell (0, 0)

        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "ArrowRight");
        Assert.Equal(new CellPosition(1, 1), selection!.Focus);
        Assert.Equal(1, selection.CellCount);

        await PressAsync(cut, "ArrowDown", shift: true);
        Assert.Equal(new CellPosition(2, 1), selection!.Focus);
        Assert.Equal(new CellPosition(1, 1), selection.Anchor);
        Assert.Equal(2, selection.CellCount);
    }

    [Fact] // ADR-0012: Ctrl+arrow jumps to the edge, Ctrl+Shift+arrow extends to it
    public async Task Ctrl_arrows_reach_the_edges()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "ArrowDown", ctrl: true);
        Assert.Equal(199, selection!.Focus.Row);

        await PressAsync(cut, "ArrowUp", ctrl: true);
        await PressAsync(cut, "ArrowDown", ctrl: true, shift: true);
        Assert.Equal(200, selection!.CellCount); // the whole column
    }

    [Fact] // ADR-0012: Meta counts as Ctrl — the gesture a Mac user makes
    public async Task Meta_behaves_as_ctrl()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "ArrowDown", meta: true);

        Assert.Equal(199, selection!.Focus.Row);
    }

    [Fact] // ADR-0012: Enter runs down columns and Tab across rows, and the range stays selected
    public async Task Enter_and_tab_cycle_inside_the_selection()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickAsync(cut, 50, 10);                       // (0, 0)
        await PressAsync(cut, "ArrowRight", shift: true);    // (0,0)-(0,1)
        await PressAsync(cut, "ArrowDown", shift: true);     // a 2x2 block
        var cells = selection!.CellCount;

        // The block is rows 0-1 x columns 0-1 with the Focus at its last cell, so
        // column-major cycling wraps to the first.
        Assert.Equal(new CellPosition(1, 1), selection!.Focus);
        await PressAsync(cut, "Enter");
        Assert.Equal(new CellPosition(0, 0), selection!.Focus);
        await PressAsync(cut, "Enter");
        Assert.Equal(new CellPosition(1, 0), selection!.Focus); // down the column
        await PressAsync(cut, "Tab");
        Assert.Equal(new CellPosition(1, 1), selection!.Focus); // across the row
        Assert.Equal(cells, selection.CellCount);               // the range never moved
    }

    [Fact] // ADR-0011 / ADR-0012: the whole-selection keys
    public async Task Ctrl_a_and_the_space_keys_select_wholes()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, " ", ctrl: true);
        Assert.Equal(200, selection!.CellCount);   // the whole column

        // From a fresh single cell: expanding the previous whole-column range would keep
        // its span and select every cell, which is ADR-0012's rule and a different test.
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, " ", shift: true);
        Assert.Equal(100, selection!.CellCount);   // the whole row

        await PressAsync(cut, "a", ctrl: true);
        Assert.Equal(200L * 100, selection!.CellCount);
    }

    [Fact] // ADR-0012 (refined): with focus but no selection, the first key starts at the first visible cell
    public async Task The_first_key_on_an_empty_selection_starts_where_the_eye_is()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ScrollToAsync(cut.Find(".ex-scroller"), top: 600, left: 0); // rows 30..

        // Nothing has been clicked — the state a user is in after tabbing in, or after a
        // sort dropped the selection (ADR-0011). Left as a no-op the keyboard would be
        // dead until they reached for the mouse.
        await PressAsync(cut, "ArrowDown");

        // Placed, not stepped: the first Down selects the first visible cell, the way the
        // first Down in any list selects its first item.
        Assert.NotNull(selection);
        Assert.Equal(new CellPosition(30, 0), selection!.Focus);
        Assert.Equal(1, selection.CellCount);

        // And from there it moves as usual.
        await PressAsync(cut, "ArrowDown");
        Assert.Equal(new CellPosition(31, 0), selection!.Focus);
    }

    [Fact] // ADR-0012: the Focus must always be visible — the grid scrolls to it
    public async Task The_grid_scrolls_to_keep_the_focus_visible()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        Assert.Empty(Js.ScrolledTo);

        await PressAsync(cut, "ArrowDown", ctrl: true);   // to row 199

        var scrolled = Assert.Single(Js.ScrolledTo);
        // 200 rows of 20px in a Viewport of 80 (100 less the header): the last row is
        // revealed by scrolling to 4000 - 80 = 3920.
        Assert.Equal(3920, scrolled.Top);
    }

    [Fact] // ADR-0012: arrowing within what is on screen does not move the Viewport
    public async Task A_move_inside_the_viewport_scrolls_nothing()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "ArrowDown");

        Assert.Empty(Js.ScrolledTo);
    }

    [Fact] // ADR-0008: the rows know nothing about the selection, keyboard or not
    public async Task Moving_by_keyboard_repaints_no_row()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        var counts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "ArrowRight", shift: true);

        Assert.Equal(counts, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount));
    }

    [Fact] // ADR-0020: Space fires a single action, and reports it once
    public async Task Space_fires_a_single_action()
    {
        var raised = new List<GridActionEventArgs<TestRow>>();
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(100))),
            GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")],
                width: new ColumnWidthSpec(ColumnWidth.Fixed(100))),
        ];
        var cut = RenderGrid(columns: columns, onAction: raised.Add);
        await ClickAsync(cut, 150, 10); // the action column, first row

        await PressAsync(cut, " ");

        var args = Assert.Single(raised);
        Assert.Equal("Actions", args.ColumnName);
        Assert.Equal("open", args.ActionName);
    }

    [Fact] // ADR-0020: Space on an ordinary cell waits for the editor — it does not act
    public async Task Space_on_a_value_cell_does_nothing_yet()
    {
        var raised = 0;
        GridColumn<TestRow>[] columns =
        [
            new("Book", ColumnType.Text, r => r.Book, width: new ColumnWidthSpec(ColumnWidth.Fixed(100))),
            GridColumn<TestRow>.ActionColumn("Actions", [new GridAction("open", "Open")],
                width: new ColumnWidthSpec(ColumnWidth.Fixed(100))),
        ];
        var cut = RenderGrid(columns: columns, onAction: _ => raised++);
        await ClickAsync(cut, 50, 10); // the value column

        await PressAsync(cut, " ");

        Assert.Equal(0, raised);
    }

    [Fact] // ADR-0010: a key the core has not claimed changes nothing
    public async Task An_unclaimed_key_is_ignored()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s);
        await ClickAsync(cut, 50, 10);
        var focus = selection!.Focus;

        await PressAsync(cut, "PageDown");
        await PressAsync(cut, "c", ctrl: true);
        await PressAsync(cut, "F2");
        await PressAsync(cut, "Unidentified");

        Assert.Equal(focus, selection!.Focus);
    }
}
