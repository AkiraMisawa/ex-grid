using Bunit;
using Microsoft.AspNetCore.Components;
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

    private string[] Focused()
        => [.. JSInterop.Invocations
            .Where(i => i.Identifier == "Blazor._internal.domWrapper.focus")
            .Select(i => ((ElementReference)i.Arguments[0]!).Id)];

    [Fact] // ADR-0040 / UX-11a: the Context Menu closes too, and the keyboard goes back to the root
    public async Task A_context_menu_with_no_room_closes_and_the_root_takes_the_keyboard()
    {
        var cut = RenderGrid();
        var root = Js.RootReferenceId;
        await Report(cut, 300);
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { OffsetX = 50, OffsetY = 30 });
        Assert.Single(cut.FindAll(".ex-popover"));
        var before = Focused().Length;

        // The Context Menu takes whichever side of the pointer has more room, header band
        // included, so it has less than a row only once the whole box does.
        await Report(cut, 19);

        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal(root, Focused().Skip(before).Last());
    }

    [Fact] // ADR-0040: only a shrink closes — a popover opened in a box already that small is the Consumer's box
    public async Task A_popover_opened_in_a_small_box_stays_through_other_renders()
    {
        var cut = RenderGrid();
        await Report(cut, 20 + 12);
        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindAll(".ex-popover"));

        await Report(cut, 20 + 12); // the same size again: a render, not a shrink

        Assert.Single(cut.FindAll(".ex-popover"));
    }
}
