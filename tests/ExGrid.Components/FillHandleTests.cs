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
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.Columns, Columns())
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
        // The press on the handle selected nothing: the Selection is the source.
        Assert.Equal([new SelectionRange(0, 0, 2, 1)], selection!.Ranges);
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
}
