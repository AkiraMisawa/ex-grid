using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A popover with no room closes (ADR-0040, decided 2026-09-25): shrink the box far
/// enough and the max-height falls to nothing, leaving an invisible popover that still
/// holds the keyboard. Below one row of room it closes as a Cancel. 20px rows under a
/// 20px header, in a Fill box whose size the browser reports.
/// </summary>
public class PopoverRoomTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Wide(6))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, ViewportSize.Fill)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => { }));

    private static Task Report(IRenderedComponent<ExGrid<TestRow>> cut, double heightPx)
        => cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 0, 350, heightPx));

    [Fact] // ADR-0040 / UX-11a: less than one row of room closes the column menu
    public async Task A_column_menu_with_less_than_a_row_of_room_closes()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindAll(".ex-popover"));

        await Report(cut, 20 + 19); // the header band and 19px

        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0040 / UX-11a: exactly one row of room still shows an item, and stays
    public async Task A_column_menu_with_a_row_of_room_stays()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        await Report(cut, 20 + 20);

        Assert.Single(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0040 / UX-11a: a box shrinking with room to spare leaves the popover open, only shorter
    public async Task A_shrink_that_leaves_room_keeps_the_popover()
    {
        var cut = RenderGrid();
        await Report(cut, 300);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        await Report(cut, 120);

        Assert.Contains("max-height: 100px", cut.Find(".ex-popover").GetAttribute("style"));
    }
}
