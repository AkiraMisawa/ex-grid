using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The page follows what a move reveals (ADR-0204, KB-45): after a move the grid makes, the
/// browser is asked — from the render that paints the move — to bring the cell the move revealed
/// into the window, naming the Focus it was taken for by the id that render writes on the root
/// (<c>data-ex-follow-focus</c>), painted or not. What the browser then does, and whether the
/// keyboard is still the grid's, is the script's and layer 3's; what is asked, and when, is pinned
/// here.
///
/// 100 columns of 100px in a 350px Viewport, 20px rows in a 100px Viewport of which the header
/// takes the first 20: four rows painted.
/// </summary>
public class PageFollowTests : GridTestContext
{
    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(int windowRows = 200, int total = 200, bool editable = false)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(windowRows))
            .Add(g => g.TotalCount, total)
            .Add(g => g.Columns, editable ? EditableWide() : TestRows.Wide(100))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> _) => { }));

    private static GridColumn<TestRow>[] EditableWide()
        => [.. TestRows.Wide(10).Select(c => new GridColumn<TestRow>(c.Name, ColumnType.Text, r => r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), editable: true))];

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    /// <summary>The <c>aria-activedescendant</c> the grid's keyboard holder carries now: its Keyboard
    /// Field on a grid that edits, its root otherwise (ADR-0080).</summary>
    private static string? ActiveDescendant(IRenderedComponent<ExGrid<TestRow>> cut)
        => (cut.FindAll(".ex-key-field").FirstOrDefault() ?? cut.Find(".ex-grid")).GetAttribute("aria-activedescendant");

    /// <summary>The Focus the root names for the script (ADR-0204), painted or not.</summary>
    private static string? FollowFocus(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-grid").GetAttribute("data-ex-follow-focus");

    [Fact] // ADR-0204 / KB-45: an arrow asks the page to follow the Focus it moved to, named as the root names it (data-ex-follow-focus) — the cell aria-activedescendant names — and the render by data-ex-follow
    public async Task An_arrow_asks_the_page_to_follow_the_focus()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);                           // (0, 0): a press is not followed
        Assert.Empty(Js.Followed);

        await PressAsync(cut, "ArrowDown");

        var (focus, target) = Assert.Single(Js.Followed);
        Assert.EndsWith("r1c0", focus);
        Assert.Equal(focus, target);
        Assert.Equal(ActiveDescendant(cut), focus);
        Assert.Equal(FollowFocus(cut), focus);
        var token = (long)Js.PageFollowed.Invocations.Single().Arguments[2]!;
        Assert.Equal(token.ToString(System.Globalization.CultureInfo.InvariantCulture), cut.Find(".ex-grid").GetAttribute("data-ex-follow"));
    }

    [Theory] // ADR-0204 / KB-45: every move the grid makes is followed — Ctrl+arrows, Home and End, PageUp and PageDown, Tab and Enter cycling, Ctrl+Backspace
    [InlineData("ArrowDown", true, false)]
    [InlineData("End", false, false)]
    [InlineData("PageDown", false, false)]
    [InlineData("Tab", false, false)]
    [InlineData("Enter", false, false)]
    [InlineData("Backspace", true, false)]
    public async Task Every_move_the_grid_makes_is_followed(string key, bool ctrl, bool shift)
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, key, ctrl, shift);

        var (focus, _) = Assert.Single(Js.Followed);
        Assert.Equal(ActiveDescendant(cut), focus);
    }

    [Fact] // ADR-0204 / ADR-0052: while extending, the page follows the Extent the reveal brought in, for the Focus that stays
    public async Task While_extending_the_page_follows_the_extent()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "ArrowDown", shift: true);

        var (focus, target) = Assert.Single(Js.Followed);
        Assert.EndsWith("r0c0", focus);
        Assert.EndsWith("r1c0", target);
    }

    [Fact] // ADR-0204 / ADR-0052, KB-45: extending past the grid's own Viewport carries the Focus off the painted rows, and the Extent is still followed at every press — the request names the Focus by the root's attribute, not by aria-activedescendant, which names nothing then
    public async Task Extending_past_the_viewport_still_follows_the_extent()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        for (var press = 1; press <= 6; press++)
        {
            await PressAsync(cut, "ArrowDown", shift: true);

            Assert.Equal(press, Js.Followed.Count);
            var (focus, target) = Js.Followed[^1];
            Assert.EndsWith("r0c0", focus);
            Assert.EndsWith($"r{press}c0", target);
            Assert.Equal(FollowFocus(cut), focus);
        }
        // Rows 3 to 6 are painted: the Focus, on row 0, is not, and nothing names it as active.
        Assert.Null(ActiveDescendant(cut));
        Assert.Empty(cut.FindAll("[id$='-r0c0']"));
        Assert.Single(cut.FindAll("[id$='-r6c0']"));
    }

    [Fact] // ADR-0204: a key that moves nothing asks nothing — the Focus already at the first row
    public async Task A_key_that_moves_nothing_asks_nothing()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "ArrowUp");

        Assert.Empty(Js.Followed);
    }

    [Fact] // ADR-0204 / ADR-0012 (2026-09-25): the box changing is not followed, though the grid reveals the Focus it covers
    public async Task The_box_changing_is_not_followed()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "ArrowDown", ctrl: true);
        var asked = Js.Followed.Count;

        // A horizontal scrollbar appears and covers the last row, where the Focus stands.
        await cut.InvokeAsync(() => cut.Instance.OnViewportReportAsync(0, 15, 350, 100));

        Assert.Equal(asked, Js.Followed.Count);
    }

    [Fact] // ADR-0204 / ED-15: Enter that commits an edit and moves is followed to the cell it moved to
    public async Task A_commit_that_moves_is_followed()
    {
        var cut = RenderGrid(editable: true);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "x");

        await PressAsync(cut, "Enter");

        var (focus, _) = Assert.Single(Js.Followed);
        Assert.EndsWith("r1c0", focus);
        Assert.Equal(ActiveDescendant(cut), focus);
    }

    [Fact] // ADR-0204 / ADR-0001: a jump onto rows not yet in hand waits for the render that paints the cell, and is followed then
    public async Task A_jump_onto_rows_not_in_hand_is_followed_once_they_are_painted()
    {
        var cut = RenderGrid(windowRows: 50, total: 1000);
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "ArrowDown", ctrl: true);         // row 999, a Placeholder
        Assert.Empty(Js.Followed);

        cut.Render(ps => ps.Add(g => g.WindowStart, 950).Add(g => g.Window, TestRows.Many(50)));

        var (focus, _) = Assert.Single(Js.Followed);
        Assert.EndsWith("r999c0", focus);
        Assert.Equal(ActiveDescendant(cut), focus);
    }

    [Fact] // ADR-0204: a follow still waiting for its rows is dropped when the user scrolls the rows away — never carried out when they come back
    public async Task A_follow_still_waiting_is_dropped_by_a_scroll()
    {
        var cut = RenderGrid(windowRows: 50, total: 1000);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "ArrowDown", ctrl: true);
        Assert.Empty(Js.Followed);

        await ScrollToAsync(cut.Find(".ex-scroller"), 0);
        cut.Render(ps => ps.Add(g => g.WindowStart, 950).Add(g => g.Window, TestRows.Many(50)));

        Assert.Empty(Js.Followed);
    }

    [Fact] // ADR-0204: a follow still waiting is dropped when something other than a move of the grid's moves the Focus on — here the Consumer's placement, which is not followed itself
    public async Task A_follow_still_waiting_is_dropped_when_the_focus_moves_on()
    {
        var cut = RenderGrid(windowRows: 50, total: 1000);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "ArrowDown", ctrl: true);

        await cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(
            new Selection.SelectionRange(998, 1, 1, 1), new Selection.CellPosition(998, 1), 0));
        cut.Render(ps => ps.Add(g => g.WindowStart, 950).Add(g => g.Window, TestRows.Many(50)));

        Assert.Empty(Js.Followed);
    }
}
