using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Clicks on the grid's own controls in the Input Order (ADR-0240 section 2). The script numbers a click
/// on a control that claims it (data-ex-claims) as it goes on to Blazor and tells the core
/// (<c>InputTakenAt</c>); the handler claims the number and is handled in its place, after every input
/// numbered before it. A number no handler claims is void at the next telling, or at a claim of another
/// kind. What the script hears and holds is layer 3's; here the telling is made by hand. 20px rows in a
/// 120px Viewport; Book and Amount, 100px each, with column menus.
/// </summary>
public class InputOrderClicksTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(int? pageSize = null)
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        return Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Source, source)
              .Add(g => g.Columns,
              [
                  new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                  new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
              ])
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (pageSize is { } size)
                ps.Add(g => g.PageSize, size);
        });
    }

    private static Task TellAsync(IRenderedComponent<ExGrid<TestRow>> cut, string kind, int input)
        => cut.InvokeAsync(() => cut.Instance.InputTakenAt(kind, input));

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, int input)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, input: input));

    private Task<string?> AnsweredAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => ReadWhenIdleAsync(() => cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-answered"));

    private static CellPosition Focus(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Instance.ReadSelection().Selection.Focus;

    [Fact] // ADR-0240 §2: the controls whose handlers take a number say which events they claim
    public void ADR0240_the_grids_own_controls_say_what_they_claim()
    {
        var cut = RenderGrid(pageSize: 10);

        Assert.Equal("mousedown click", cut.Find(".ex-header").GetAttribute("data-ex-claims"));
        Assert.All(cut.FindAll(".ex-menu-button"), button => Assert.Equal("click", button.GetAttribute("data-ex-claims")));
        Assert.Equal("contextmenu", cut.Find(".ex-viewport").GetAttribute("data-ex-claims"));
        Assert.All(cut.FindAll(".ex-pager button"), button => Assert.Equal("click", button.GetAttribute("data-ex-claims")));
    }

    [Fact] // ADR-0240 §2: a click told ahead of its event is handled in its number, after a key numbered before it that arrives later
    public async Task ADR0240_a_told_click_waits_for_the_input_numbered_before_it()
    {
        var cut = RenderGrid();
        await TellAsync(cut, "click", input: 2);
        Task opener = Task.CompletedTask;
        await cut.InvokeAsync(() => { opener = cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs()); });
        await WhenIdleAsync();
        Assert.Empty(cut.FindAll(".ex-popover"));
        Assert.Equal("0", await AnsweredAsync(cut));

        await KeyAsync(cut, "ArrowDown", input: 1);
        await opener;

        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Equal("2", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §2: a number told and never claimed is void at the next telling of the same channel, never left owed
    public async Task ADR0240_a_click_told_and_never_heard_is_void_at_the_next_telling()
    {
        var cut = RenderGrid(pageSize: 10);
        await TellAsync(cut, "click", input: 1);
        await TellAsync(cut, "click", input: 2);
        Assert.Equal("1", await AnsweredAsync(cut));

        await cut.FindAll(".ex-pager button")[1].ClickAsync(new MouseEventArgs());
        Assert.Equal("2", await AnsweredAsync(cut));
        Assert.Contains("2 / 5", cut.Find(".ex-pager").TextContent, StringComparison.Ordinal);
    }

    [Fact] // ADR-0240 §2: a number told for another kind of event is no one's: a claim of another kind voids it, and the handler runs unnumbered
    public async Task ADR0240_a_click_told_as_another_kind_is_void_at_the_claim()
    {
        var cut = RenderGrid();
        await TellAsync(cut, "mousedown", input: 1);

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Equal("1", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §2: a heading's press and click each take the number told ahead of them, in turn behind a key
    public async Task ADR0240_a_headings_press_and_click_take_their_numbers_in_turn()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await TellAsync(cut, "mousedown", input: 2);
        Task press = Task.CompletedTask;
        await cut.InvokeAsync(() => { press = cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 130, OffsetY = 10, ClientX = 130 }); });
        await WhenIdleAsync();
        Assert.False(press.IsCompleted);

        await KeyAsync(cut, "ArrowDown", input: 1);
        await press;
        Assert.Equal("2", await AnsweredAsync(cut));
        await TellAsync(cut, "click", input: 3);
        await cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 130, OffsetY = 10, ClientX = 130 });

        Assert.Equal("3", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §2, ADR-0036: a copy from a menu item's numbered click asks for its payload as that click's, never under a number of its own it would wait for
    public async Task ADR0240_a_menu_copy_from_a_numbered_click_is_that_clicks_input()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { OffsetX = 50, OffsetY = 10 });
        var copy = cut.FindAll("[role=menu] button[role=menuitem]").First(b => b.TextContent == "Copy");
        Assert.Equal("click", copy.GetAttribute("data-ex-claims"));

        await TellAsync(cut, "click", input: 1);
        await copy.ClickAsync(new MouseEventArgs());

        var written = Assert.Single(JSInterop.Invocations["writeCopy"]);
        Assert.Equal([false, true], written.Arguments);
        Assert.Equal("1", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §2, ADR-0036: a menu copy nobody numbered keeps the script's own numbering of its payload
    public async Task ADR0240_a_menu_copy_from_an_unnumbered_click_lets_the_script_number_its_payload()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { OffsetX = 50, OffsetY = 10 });

        await cut.FindAll("[role=menu] button[role=menuitem]").First(b => b.TextContent == "Copy").ClickAsync(new MouseEventArgs());

        Assert.Equal([false, false], Assert.Single(JSInterop.Invocations["writeCopy"]).Arguments);
        Assert.Equal(new CellPosition(0, 0), Focus(cut));
    }
}
