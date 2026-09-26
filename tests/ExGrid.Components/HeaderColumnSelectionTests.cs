using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Shift+click on a column header selects whole columns (ADR-0012, decided 2026-09-25):
/// Excel's gesture, and the mouse route to several whole columns. A plain click still
/// sorts. 6 columns of 100px, 50 rows.
/// </summary>
public class HeaderColumnSelectionTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridSelection> onSelection, Action<IReadOnlyList<SortSpec>> onSort)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.SelectionChanged, onSelection)
            .Add(g => g.OnSortChanged, onSort));

    [Fact] // ADR-0012 / SR-2a: Shift+click on a header selects whole columns from the Anchor's, and does not sort
    public async Task Shift_click_on_a_header_selects_whole_columns_and_does_not_sort()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);
        await cut.FindAll(".ex-cell")[0].MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 45 });

        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs
            { Button = 0, ShiftKey = true, OffsetX = 320, OffsetY = 10 });

        Assert.Null(sorted);
        Assert.NotNull(selection);
        Assert.Equal([new SelectionRange(0, 1, 50, 3)], selection.Ranges);
        Assert.Equal(new CellPosition(2, 1), selection.Anchor);
    }

    [Fact] // ADR-0012 / SR-1: the plain click still sorts and leaves the selection alone
    public async Task A_plain_click_on_a_header_still_sorts()
    {
        GridSelection? selection = null;
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderGrid(s => selection = s, s => sorted = s);

        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 320, OffsetY = 10 });

        Assert.NotNull(sorted);
        Assert.Null(selection);
    }

    [Fact] // ADR-0012 / SR-2a: from nothing selected, the clicked column alone
    public async Task Shift_click_with_nothing_selected_selects_that_column()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(s => selection = s, _ => { });

        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs
            { Button = 0, ShiftKey = true, OffsetX = 120, OffsetY = 10 });

        Assert.Equal([new SelectionRange(0, 1, 50, 1)], selection!.Ranges);
    }
}
