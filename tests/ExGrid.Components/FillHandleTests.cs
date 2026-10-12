using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The fill handle (ADR-0050, item 5; DC-12, DC-13, DC-14): painted in the selection
/// overlay when declared, grabbed by arithmetic like every other press in the body, dragged
/// along one axis with the target outlined, and released as exactly one Fill Intent — or one
/// refusal — with nothing written. The drag in a real browser is layer 3's.
/// </summary>
public class FillHandleTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        new("Locked", ColumnType.Text, r => r.Book, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null,
        GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350);
            extra?.Invoke(ps);
        });

    private static Task DownAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool shift = false, bool ctrl = false)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, ShiftKey = shift, CtrlKey = ctrl,
        });

    private static Task MoveAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task UpAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y });

    /// <summary>Rows 0-1 of Book: the handle's corner is at (100, 40).</summary>
    private static async Task SelectBookRows0To1Async(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await DownAsync(cut, 50, 10);
        await UpAsync(cut, 50, 10);
        await DownAsync(cut, 50, 30, shift: true);
        await UpAsync(cut, 50, 30);
    }

    [Fact] // ADR-0050 / DC-1: undeclared, no handle is painted
    public async Task Without_the_declaration_no_handle_is_painted()
    {
        var cut = RenderGrid();
        await SelectBookRows0To1Async(cut);

        Assert.Empty(cut.FindAll(".ex-fill-handle"));
        Assert.Empty(cut.FindAll(".ex-fill-target"));
    }

    [Fact] // ADR-0050 item 5 / ADR-0008 / DC-12: one handle, at the last range's bottom-right, sized by the metrics
    public async Task Declared_one_handle_is_painted_at_the_bottom_right_of_the_range()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await SelectBookRows0To1Async(cut);

        var handle = Assert.Single(cut.FindAll(".ex-fill-handle"));
        // A 6px square (a little under a third of a 20px row) centred on (100, 40).
        Assert.Equal("left: 97px; top: 37px; width: 6px; height: 6px", handle.GetAttribute("style"));
        Assert.NotNull(handle.Closest(".ex-selection"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: a disjoint Selection shows no handle
    public async Task A_disjoint_selection_shows_no_handle()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await DownAsync(cut, 50, 10);
        await UpAsync(cut, 50, 10);
        await DownAsync(cut, 150, 70, ctrl: true);
        await UpAsync(cut, 150, 70);

        Assert.Empty(cut.FindAll(".ex-fill-handle"));
    }

    [Fact] // ADR-0050 item 5 / DC-13: the drag outlines the target and the release raises one intent; nothing is written
    public async Task Dragging_the_handle_down_raises_one_fill_intent_and_writes_nothing()
    {
        var fills = new List<GridFillIntent>();
        var edits = 0;
        var pastes = 0;
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i))
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => edits++)
            .Add(g => g.OnPaste, (GridPasteIntent _) => pastes++)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 99, 41);                            // on the handle
        await MoveAsync(cut, 50, 90);                            // row 4, Book
        var outline = Assert.Single(cut.FindAll(".ex-fill-target"));
        Assert.Equal("left: 0px; top: 0px; width: 100px; height: 100px", outline.GetAttribute("style"));
        Assert.Empty(fills);                                     // nothing before the release
        await UpAsync(cut, 50, 90);

        var fill = Assert.Single(fills);
        Assert.Equal(new SelectionRange(0, 0, 2, 1), fill.Source);
        Assert.Equal(new SelectionRange(2, 0, 3, 1), fill.Target);
        Assert.Equal(GridDirection.Down, fill.Direction);
        Assert.Equal(0, fill.RowSequenceVersion);
        Assert.Equal(0, edits);
        Assert.Equal(0, pastes);
        Assert.Empty(cut.FindAll(".ex-fill-target"));
        // The press on the handle selected nothing; the accepted fill then selected the
        // source and the target together (ADR-0050, item 5 refined).
        Assert.Equal([new SelectionRange(0, 0, 5, 1)], selection!.Ranges);
    }

    [Fact] // ADR-0050 item 5 refined / DC-27: after a fill the Consumer accepts, the Selection is source and target together
    public async Task An_accepted_fill_selects_the_source_and_the_target()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent _) => { })
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await SelectBookRows0To1Async(cut);                      // Focus row 0, Extent row 1

        await DownAsync(cut, 99, 41);
        await MoveAsync(cut, 50, 90);                            // row 4
        await UpAsync(cut, 50, 90);

        Assert.Equal([new SelectionRange(0, 0, 5, 1)], selection!.Ranges);
        // The Focus has not moved: it stays on the source's first cell, so the cell on
        // display is the one it was (ADR-0052).
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
        var overlay = Assert.Single(cut.FindAll(".ex-range"));
        Assert.Contains("height: 100px", overlay.GetAttribute("style"));
        // The handle now stands at the bottom-right of the filled range.
        Assert.Equal("left: 97px; top: 97px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 5 refined / DC-27: a fill up or left selects the target before the source
    public async Task An_accepted_fill_upward_selects_from_the_target()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent _) => { })
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await DownAsync(cut, 50, 90);                            // Book, row 4
        await UpAsync(cut, 50, 90);

        await DownAsync(cut, 100, 100);                          // its handle
        await MoveAsync(cut, 50, 30);                            // row 1
        await UpAsync(cut, 50, 30);

        Assert.Equal([new SelectionRange(1, 0, 4, 1)], selection!.Ranges);
        Assert.Equal(new CellPosition(4, 0), selection.Focus);
    }

    [Fact] // ADR-0050 item 5 refined / DC-27: after a fill the Consumer refuses, the Selection stays on the source
    public async Task A_refused_fill_leaves_the_selection_on_the_source()
    {
        GridSelection? selection = null;
        var changes = 0;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, async (GridFillIntent i) =>
            {
                await Task.Yield();                              // a refusal reached asynchronously still counts
                i.Refuse();
            })
            .Add(g => g.SelectionChanged, (GridSelection s) => { selection = s; changes++; }));
        await SelectBookRows0To1Async(cut);
        var before = changes;

        await DownAsync(cut, 99, 41);
        await MoveAsync(cut, 50, 90);
        await UpAsync(cut, 50, 90);

        Assert.Equal(before, changes);
        Assert.Equal([new SelectionRange(0, 0, 2, 1)], selection!.Ranges);
        Assert.Equal("left: 97px; top: 37px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 5 refined / DC-27: with nobody taking the intent, nobody accepted it
    public async Task Without_a_handler_the_selection_stays_on_the_source()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 99, 41);
        await MoveAsync(cut, 50, 90);
        await UpAsync(cut, 50, 90);

        Assert.Equal([new SelectionRange(0, 0, 2, 1)], selection!.Ranges);
    }

    [Fact] // ADR-0050 item 5 refined / ADR-0011: an order that moved while the Consumer handled the intent places nothing
    public async Task A_reorder_during_the_handler_places_nothing()
    {
        GridSelection? selection = null;
        IRenderedComponent<ExGrid<TestRow>>? grid = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent _) => grid!.Render(p => p.Add(g => g.RowSequenceVersion, 1)))
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        grid = cut;
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 99, 41);
        await MoveAsync(cut, 50, 90);
        await UpAsync(cut, 50, 90);

        // The reorder dropped the Selection (ADR-0011); the fill did not bring one back.
        Assert.True(selection!.IsEmpty);
    }

    [Fact] // ADR-0050 item 5 / DC-13: one axis only — the larger displacement decides
    public async Task A_diagonal_drag_extends_along_one_axis()
    {
        var fills = new List<GridFillIntent>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i)));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 100, 40);
        await MoveAsync(cut, 150, 150);                          // row 7, Amount: six rows down, one column right
        await UpAsync(cut, 150, 150);

        var fill = Assert.Single(fills);
        Assert.Equal(new SelectionRange(2, 0, 6, 1), fill.Target);
        Assert.Equal(GridDirection.Down, fill.Direction);
    }

    [Fact] // ADR-0050 item 5 / ADR-0035 / DC-14: a target covering a non-editable column is refused whole, and no intent is raised
    public async Task A_fill_into_a_non_editable_column_is_refused_before_the_intent()
    {
        var fills = new List<GridFillIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 100, 40);
        await MoveAsync(cut, 250, 30);                           // Locked, row 1: right, two columns
        await UpAsync(cut, 250, 30);

        Assert.Empty(fills);
        Assert.Equal(PasteRefusalReason.TargetNotEditable, refused);
    }

    [Fact] // ADR-0050 item 5: a release back inside the source raises nothing
    public async Task A_release_inside_the_source_raises_nothing()
    {
        var fills = new List<GridFillIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i))
            .Add(g => g.OnPasteRefused, (PasteRefusalReason r) => refused = r));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 100, 40);
        await MoveAsync(cut, 50, 90);
        await MoveAsync(cut, 50, 30);                            // back into the source
        Assert.Empty(cut.FindAll(".ex-fill-target"));
        await UpAsync(cut, 50, 30);

        Assert.Empty(fills);
        Assert.Null(refused);
    }

    [Fact] // ADR-0050 item 5 / ADR-0008: the button coming up away from the grid ends the drag and raises nothing
    public async Task A_drag_whose_button_came_up_elsewhere_raises_nothing()
    {
        var fills = new List<GridFillIntent>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i)));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 100, 40);
        await MoveAsync(cut, 50, 90);
        await cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs { Buttons = 0, OffsetX = 50, OffsetY = 110 });

        Assert.Empty(fills);
        Assert.Empty(cut.FindAll(".ex-fill-target"));
    }

    [Fact] // ADR-0050 item 5 / ADR-0003 / ADR-0008: the drag is overlay only — every row still skips its render
    public async Task A_fill_drag_renders_no_row()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await SelectBookRows0To1Async(cut);
        var before = cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(r => r.Instance, r => r.RenderCount);

        await DownAsync(cut, 100, 40);
        await MoveAsync(cut, 50, 90);
        await MoveAsync(cut, 50, 130);
        await UpAsync(cut, 50, 130);

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
            Assert.Equal(before[row.Instance], row.RenderCount);
    }

    [Fact] // ADR-0050 item 5: a press away from the handle keeps its meaning — it selects
    public async Task A_press_away_from_the_handle_still_selects()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 150, 90);                           // Amount, row 4
        await UpAsync(cut, 150, 90);

        Assert.Equal([new SelectionRange(4, 1, 1, 1)], selection!.Ranges);
    }

    [Fact] // ADR-0050 item 5 / ADR-0011: a drag under an order that moved raises nothing
    public async Task A_drag_across_a_reorder_raises_nothing()
    {
        var fills = new List<GridFillIntent>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i)));
        await SelectBookRows0To1Async(cut);

        await DownAsync(cut, 100, 40);
        await MoveAsync(cut, 50, 90);
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        await UpAsync(cut, 50, 90);

        Assert.Empty(fills);
    }

    // ---- The handle on a whole axis (decided with the user, 2026-10-12; the seventeenth Windows run, h3) ----

    private static GridColumn<TestRow>[] EditableColumns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        new("Notes", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
    ];

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
    {
        await DownAsync(cut, x, y);
        await UpAsync(cut, x, y);
    }

    [Fact] // ADR-0050 item 5 / DC-12: a whole column's handle stands at its first row's top-right corner, inside the row
    public async Task A_whole_column_puts_its_handle_at_the_first_rows_top_right()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 150, 70);                          // Amount, row 3
        await KeyAsync(cut, " ", ctrl: true);                    // Ctrl+Space: Amount, every row

        var handle = Assert.Single(cut.FindAll(".ex-fill-handle"));
        // Centred on the column's right edge (200), its top on row 0's top: beside the column's
        // header, wholly below it, as Excel draws it — not at row 49's bottom, out of sight.
        Assert.Equal("left: 197px; top: 0px; width: 6px; height: 6px", handle.GetAttribute("style"));
        Assert.NotNull(handle.Closest(".ex-selection"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: several whole columns put it at the first row's top-right of the last of them
    public async Task Several_whole_columns_put_the_handle_at_the_first_rows_top_right()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 70);                           // Book, row 3
        await KeyAsync(cut, " ", ctrl: true);
        await KeyAsync(cut, "ArrowRight", shift: true);          // Book and Amount, every row

        Assert.Equal("left: 197px; top: 0px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: a whole row's handle stands at its first column's bottom-left corner, inside the column
    public async Task A_whole_row_puts_its_handle_at_the_first_columns_bottom_left()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 150, 30);                          // Amount, row 1
        await KeyAsync(cut, " ", shift: true);                   // Shift+Space: row 1, every column

        // Its left on the first column's left (the grid's edge here), centred on the row's bottom (40).
        Assert.Equal("left: 0px; top: 37px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: beside the Row Headings, a whole row's handle starts at their edge, and a press on them stays theirs
    public async Task Beside_the_row_headings_a_whole_rows_handle_starts_at_their_edge()
    {
        var fills = new List<GridFillIntent>();
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.RowHeadings, row => (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Add(g => g.RowHeadingWidth, 40d)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i))
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickAsync(cut, 190, 30);                          // Amount, row 1 (the columns start at 40)
        await KeyAsync(cut, " ", shift: true);

        Assert.Equal("left: 40px; top: 37px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));

        // A press on row 2's Heading, inside the handle's grab box but on the Heading: the
        // Heading's, so it selects row 2 and starts no fill drag.
        await DownAsync(cut, 38, 41);
        Assert.Equal([new SelectionRange(2, 0, 1, 3)], selection!.Ranges);
        await UpAsync(cut, 38, 41);

        Assert.Empty(fills);
        Assert.Empty(cut.FindAll(".ex-fill-target"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: a whole column's handle goes with its first row, out of the painted rows
    public async Task A_whole_columns_handle_goes_with_its_first_row()
    {
        var cut = RenderGrid(ps => ps.Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 150, 70);
        await KeyAsync(cut, " ", ctrl: true);
        Assert.Single(cut.FindAll(".ex-fill-handle"));

        await ScrollToAsync(cut.Find(".ex-scroller"), 400);      // rows 20 onwards

        Assert.Empty(cut.FindAll(".ex-fill-handle"));
    }

    [Fact] // ADR-0050 item 5 / DC-12: every cell (Ctrl+A) keeps the bottom-right corner until Excel is asked
    public async Task Every_cell_keeps_the_bottom_right_corner()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(5))
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFillHandle, true));
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "a", ctrl: true);                    // rows 0-4, every column

        Assert.Equal("left: 297px; top: 97px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 5 / DC-13: dragged from the top-right, a whole column fills across, along one axis
    public async Task A_whole_columns_handle_drags_across_and_raises_one_intent()
    {
        var fills = new List<GridFillIntent>();
        GridSelection? selection = null;
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i))
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s), EditableColumns());
        await ClickAsync(cut, 50, 70);                           // Book, row 3
        await KeyAsync(cut, " ", ctrl: true);                    // Book, every row

        await DownAsync(cut, 100, 3);                            // on the handle, beside the header
        // Down first, inside the column: a whole column has no rows past it, so nothing is reached.
        await MoveAsync(cut, 50, 150);
        Assert.Empty(cut.FindAll(".ex-fill-target"));
        await MoveAsync(cut, 150, 150);                          // Amount, row 7
        var outline = Assert.Single(cut.FindAll(".ex-fill-target"));
        Assert.StartsWith("left: 0px; top: 0px; width: 200px;", outline.GetAttribute("style"));
        await UpAsync(cut, 150, 150);

        var fill = Assert.Single(fills);
        Assert.Equal(new SelectionRange(0, 0, 50, 1), fill.Source);
        Assert.Equal(new SelectionRange(0, 1, 50, 1), fill.Target);
        Assert.Equal(GridDirection.Right, fill.Direction);
        Assert.Equal([new SelectionRange(0, 0, 50, 2)], selection!.Ranges);
        // Both columns now, so the handle stands at the top-right of the second.
        Assert.Equal("left: 197px; top: 0px; width: 6px; height: 6px",
            Assert.Single(cut.FindAll(".ex-fill-handle")).GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 5 / DC-13: dragged from the bottom-left, a whole row fills down, along one axis
    public async Task A_whole_rows_handle_drags_down_and_raises_one_intent()
    {
        var fills = new List<GridFillIntent>();
        var cut = RenderGrid(ps => ps
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i)), EditableColumns());
        await ClickAsync(cut, 150, 30);                          // Amount, row 1
        await KeyAsync(cut, " ", shift: true);                   // row 1, every column

        await DownAsync(cut, 3, 40);                             // on the handle, at the row's left
        await MoveAsync(cut, 250, 30);                           // across, inside the row: nothing reached
        Assert.Empty(cut.FindAll(".ex-fill-target"));
        await MoveAsync(cut, 250, 90);                           // row 4
        await UpAsync(cut, 250, 90);

        var fill = Assert.Single(fills);
        Assert.Equal(new SelectionRange(1, 0, 1, 3), fill.Source);
        Assert.Equal(new SelectionRange(2, 0, 3, 3), fill.Target);
        Assert.Equal(GridDirection.Down, fill.Direction);
    }

    [Fact] // ADR-0050 item 5: a whole column's bottom-right corner is no handle any more — a press there selects
    public async Task A_whole_columns_old_corner_is_no_handle()
    {
        var fills = new List<GridFillIntent>();
        GridSelection? selection = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(5))
            .Add(g => g.Columns, EditableColumns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, (GridFillIntent i) => fills.Add(i))
            .Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickAsync(cut, 50, 30);
        await KeyAsync(cut, " ", ctrl: true);                    // Book, rows 0-4

        await DownAsync(cut, 99, 99);                            // where the bottom-right handle stood
        await MoveAsync(cut, 150, 90);
        await UpAsync(cut, 150, 90);

        Assert.Empty(fills);
        Assert.Equal([new SelectionRange(4, 0, 1, 2)], selection!.Ranges);
    }
}
