using System.Globalization;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Ctrl+click and Ctrl+drag on Headings and on a plain ExGrid's column header (ADR-0050, item 1,
/// and ADR-0012, both 2026-09-29; SR-2e, DC-42). Ctrl+click adds the whole column (row) as a new
/// range, or takes a wholly selected one out; Ctrl+drag adds the columns (rows) crossed as one
/// range; neither sorts; Meta counts as Ctrl only where Meta is Command. The pure rules are in
/// HeadingToggleTests.
///
/// 50 rows of 20px under a 20px header in a 350 × 200 Viewport; six 100px columns. Where Row
/// Headings are declared they are 40px wide.
/// </summary>
public class HeadingCtrlClickTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection>? onSelection = null,
        Action<IReadOnlyList<SortSpec>>? onSort = null,
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, TestRows.Wide(6))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.SelectionChanged, onSelection ?? (_ => { }))
              .Add(g => g.OnSortChanged, onSort ?? (_ => { }));
            extra?.Invoke(ps);
        });

    private static void Declared(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.HeaderClickSelects, true);

    private static void WithRowHeadings(Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps)
        => ps.Add(g => g.RowHeadings, row => (row + 1).ToString(CultureInfo.InvariantCulture))
             .Add(g => g.RowHeadingWidth, 40d);

    // Client positions as a grid at the page's top-left corner, scrolled nowhere, would see them:
    // across, the header's and the Viewport's own offsets; down, the Viewport's under the 20px
    // header band. A Heading gesture places its events by their client delta from the press.
    private const double BandPx = 20;

    private static MouseEventArgs Press(double x, double y, bool ctrl = false, bool meta = false, double clientY = double.NaN)
        => new()
        {
            Button = 0, Buttons = 1, CtrlKey = ctrl, MetaKey = meta, OffsetX = x, OffsetY = y,
            ClientX = x, ClientY = double.IsNaN(clientY) ? BandPx + y : clientY,
        };

    private static Task PressHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, bool ctrl = false, bool meta = false)
        => cut.Find(".ex-header").MouseDownAsync(Press(x, 10, ctrl, meta, clientY: 10));

    private static Task MoveOverHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x)
        => cut.Find(".ex-header").MouseMoveAsync(new MouseEventArgs { Buttons = 1, OffsetX = x, OffsetY = 10, ClientX = x, ClientY = 10 });

    /// <summary>The release over the header, and the click the browser fires after it, both with
    /// the modifiers of the press.</summary>
    private static async Task ReleaseOverHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, bool ctrl = false, bool meta = false)
    {
        await cut.Find(".ex-header").MouseUpAsync(new MouseEventArgs
            { Button = 0, CtrlKey = ctrl, MetaKey = meta, OffsetX = x, OffsetY = 10, ClientX = x, ClientY = 10 });
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs
            { Button = 0, CtrlKey = ctrl, MetaKey = meta, OffsetX = x, OffsetY = 10, ClientX = x, ClientY = 10 });
    }

    private static async Task CtrlClickHeaderAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, bool ctrl = true, bool meta = false)
    {
        await PressHeaderAsync(cut, x, ctrl, meta);
        await ReleaseOverHeaderAsync(cut, x, ctrl, meta);
    }

    private static async Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(Press(x, y));
        await cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs
            { Button = 0, OffsetX = x, OffsetY = y, ClientX = x, ClientY = BandPx + y });
    }

    private static Task PressViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, bool ctrl = false)
        => cut.Find(".ex-viewport").MouseDownAsync(Press(x, y, ctrl));

    private static Task MoveOverViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseMoveAsync(new MouseEventArgs
            { Buttons = 1, OffsetX = x, OffsetY = y, ClientX = x, ClientY = BandPx + y });

    private static Task ReleaseOverViewportAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs
            { Button = 0, OffsetX = x, OffsetY = y, ClientX = x, ClientY = BandPx + y });

    private static SelectionRange Column(int column) => new(0, column, 50, 1);

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: declared, Ctrl+press on a column adds it as a new range, the Focus on its first visible row
    public async Task Declared_ctrl_press_adds_the_column()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s, Declared);
        await ClickCellAsync(cut, 50, 45);                             // (2, 0)

        await CtrlClickHeaderAsync(cut, 250);

        Assert.Equal([new SelectionRange(2, 0, 1, 1), Column(2)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 2), selection.Focus);
        Assert.Null(sorted);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: declared, Ctrl+press on a wholly selected column takes it out, and begins no drag
    public async Task Declared_ctrl_press_on_a_wholly_selected_column_takes_it_out()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await PressHeaderAsync(cut, 50);
        await MoveOverHeaderAsync(cut, 250);
        await ReleaseOverHeaderAsync(cut, 250);                        // A:C, the Focus on A1
        Assert.Equal([new SelectionRange(0, 0, 50, 3)], selection!.Ranges);

        await PressHeaderAsync(cut, 150, ctrl: true);

        Assert.Equal([Column(2), Column(0)], selection.Ranges);
        Assert.Equal(new CellPosition(0, 0), selection.Focus);
        // A take-out begins no drag (ADR-0012): nothing listens for the moves.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => MoveOverHeaderAsync(cut, 350));
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, CtrlKey = true, OffsetX = 150, OffsetY = 10 });
        Assert.Equal([Column(2), Column(0)], selection.Ranges);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / SR-2e: declared, Ctrl+drag adds whole columns from the pressed one to the pointer's as one range
    public async Task Declared_ctrl_drag_adds_one_range_of_whole_columns()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await ClickCellAsync(cut, 50, 45);

        await PressHeaderAsync(cut, 150, ctrl: true);
        await MoveOverHeaderAsync(cut, 250);
        await MoveOverViewportAsync(cut, 320, 100);                    // over column 3's cells
        await ReleaseOverViewportAsync(cut, 320, 100);

        Assert.Equal([new SelectionRange(2, 0, 1, 1), new SelectionRange(0, 1, 50, 3)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 1), selection.Focus);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: Ctrl+press on a Row Heading adds the row; Ctrl+drag adds rows as one range
    public async Task Ctrl_press_and_drag_on_row_headings_add_whole_rows()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: WithRowHeadings);
        await ClickCellAsync(cut, 150, 5);                             // (0, 1)

        await PressViewportAsync(cut, 10, 65, ctrl: true);             // row 3's heading
        Assert.Equal([new SelectionRange(0, 1, 1, 1), new SelectionRange(3, 0, 1, 6)], selection!.Ranges);
        Assert.Equal(new CellPosition(3, 0), selection.Focus);

        await MoveOverViewportAsync(cut, 200, 105);                    // row 5, over the cells
        await ReleaseOverViewportAsync(cut, 200, 105);
        Assert.Equal([new SelectionRange(0, 1, 1, 1), new SelectionRange(3, 0, 3, 6)], selection.Ranges);
    }

    [Fact] // ADR-0050 item 1 (2026-09-29) / DC-42: Ctrl+press on a wholly selected Row Heading takes the row out, and begins no drag
    public async Task Ctrl_press_on_a_wholly_selected_row_heading_takes_it_out()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: WithRowHeadings);
        await PressViewportAsync(cut, 10, 45);                         // row 2
        await MoveOverViewportAsync(cut, 10, 85);                      // rows 2..4
        await ReleaseOverViewportAsync(cut, 10, 85);

        await PressViewportAsync(cut, 10, 65, ctrl: true);             // row 3, wholly selected

        Assert.Equal([new SelectionRange(4, 0, 1, 6), new SelectionRange(2, 0, 1, 6)], selection!.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Focus);
        await Assert.ThrowsAsync<MissingEventHandlerException>(() => MoveOverViewportAsync(cut, 10, 145));
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2e: on a plain header, Ctrl+click adds the whole column and does not sort
    public async Task Plain_ctrl_click_adds_the_column_and_does_not_sort()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);
        await ClickCellAsync(cut, 50, 45);

        await CtrlClickHeaderAsync(cut, 250);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(2, 0, 1, 1), Column(2)], selection!.Ranges);
        Assert.Equal(new CellPosition(0, 2), selection.Focus);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2e: on a plain header, Ctrl+click on a wholly selected column takes it out and does not sort
    public async Task Plain_ctrl_click_on_a_wholly_selected_column_takes_it_out()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);
        await ClickCellAsync(cut, 50, 45);
        await CtrlClickHeaderAsync(cut, 250);                          // adds C:C

        await CtrlClickHeaderAsync(cut, 250);                          // and takes it out

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(2, 0, 1, 1)], selection!.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Focus);
    }

    [Fact] // ADR-0012 (2026-09-29) / SR-2e: on a plain header, Ctrl+drag adds the columns crossed as one range, and nothing sorts
    public async Task Plain_ctrl_drag_adds_one_range_and_does_not_sort()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);
        await ClickCellAsync(cut, 50, 45);

        await PressHeaderAsync(cut, 150, ctrl: true);
        Assert.Equal([new SelectionRange(2, 0, 1, 1)], selection!.Ranges);  // the press selects nothing by itself
        await MoveOverHeaderAsync(cut, 320);
        await ReleaseOverHeaderAsync(cut, 320, ctrl: true);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(2, 0, 1, 1), new SelectionRange(0, 1, 50, 3)], selection.Ranges);
    }

    [Fact] // ADR-0012 / SR-2e: Meta is not Ctrl off an Apple platform — Meta+click on a plain header is a plain click, and sorts
    public async Task Off_an_apple_platform_meta_click_is_a_plain_click()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await CtrlClickHeaderAsync(cut, 250, ctrl: false, meta: true);

        Assert.NotNull(sorted);
        Assert.Null(selection);
    }

    [Fact] // ADR-0012 / SR-2e: where Meta is Command, Cmd+click on a header adds the column and does not sort
    public async Task Where_meta_is_command_meta_click_adds_the_column()
    {
        Js.MetaIsPrimary();
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);
        await ClickCellAsync(cut, 50, 45);

        await CtrlClickHeaderAsync(cut, 250, ctrl: false, meta: true);

        Assert.Null(sorted);
        Assert.Equal([new SelectionRange(2, 0, 1, 1), Column(2)], selection!.Ranges);
    }

    [Fact] // ADR-0012 / DC-42: off an Apple platform a declared Meta+press on a heading selects the column alone, as a plain press
    public async Task Off_an_apple_platform_a_declared_meta_press_is_a_plain_press()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, extra: Declared);
        await ClickCellAsync(cut, 50, 45);

        await CtrlClickHeaderAsync(cut, 250, ctrl: false, meta: true);

        Assert.Equal([Column(2)], selection!.Ranges);
    }
}
