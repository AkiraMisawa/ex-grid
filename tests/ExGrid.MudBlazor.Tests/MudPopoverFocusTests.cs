using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using ExGrid.Finding;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// A Wrapper's popover is handed the core's focus function and takes the keyboard through it, never
/// with its own <c>FocusAsync</c> (ADR-0204, KB-45, SRV-5): on a circuit the request lands a round
/// trip after the gesture, and the core grants it only while the keyboard is still the grid's. The
/// column menu and the Context Menu, the find panel and the filter panel, each with the element it
/// names, and whether that may scroll.
/// </summary>
public class MudPopoverFocusTests : MudTestContext
{
    private readonly List<(string? Element, bool PreventScroll)> _taken = [];

    private Task Take(ElementReference element, bool preventScroll)
    {
        _taken.Add((element.Id, preventScroll));
        return Task.CompletedTask;
    }

    /// <summary>MudBlazor's own focus calls and Blazor's: none is made while the core's function is at hand.</summary>
    private int OwnFocusCalls()
        => JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus"
            || i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase) && i.Identifier.StartsWith("mud", StringComparison.OrdinalIgnoreCase));

    private static GridCommand Command(string id) => new(id, true, () => Task.CompletedTask);

    [Fact] // ADR-0204 / SRV-5: the column menu's opening focus goes through the core's function, without scrolling, on the first enabled item
    public void The_column_menu_takes_the_keyboard_through_the_cores_function()
    {
        var cut = Render(MudGridChrome.Default.ColumnMenu(new ColumnMenuContext(
            "Book", ColumnType.Text, [Command("sort-ascending"), Command("sort-descending")], () => { }, FocusRequest: 1,
            TakeFocus: Take))!);

        var (element, preventScroll) = Assert.Single(_taken);
        Assert.NotNull(element);
        Assert.True(preventScroll);
        Assert.Equal(0, OwnFocusCalls());
        Assert.NotEmpty(cut.FindAll("button[role=menuitem]"));
    }

    [Fact] // ADR-0204 / ADR-0039: an arrow's move inside the Context Menu goes through the core's function too, and may scroll the item into view
    public async Task An_arrow_in_the_context_menu_takes_the_keyboard_through_the_cores_function()
    {
        MenuKey Resolve(string key, bool shift, bool chord) => new(MenuKeyKind.Move, 1);
        var cut = Render(MudGridChrome.Default.ContextMenu(new ContextMenuContext<object>(
            new object(), "Book", ColumnType.Text, [], 0, [Command("copy"), Command("copy-with-headers")], () => { },
            FocusRequest: 1, ResolveKey: Resolve, TakeFocus: Take))!);

        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Equal(2, _taken.Count);
        Assert.False(_taken[1].PreventScroll);
        Assert.Equal(0, OwnFocusCalls());
    }

    [Fact] // ADR-0204 / SRV-5: the find panel's field takes the keyboard through the core's function, by its input element
    public void The_find_panel_takes_the_keyboard_through_the_cores_function()
    {
        var cut = Render(MudGridChrome.Default.FindPanel(new FindContext(
            "", false, false, FindOutcome.None, _ => { }, _ => { }, _ => { },
            () => Task.CompletedTask, () => Task.CompletedTask, () => { }, FocusRequest: 1, TakeFocus: Take))!);

        var (element, preventScroll) = Assert.Single(_taken);
        Assert.Equal(cut.FindComponent<MudTextField<string>>().Instance.InputReference!.ElementReference.Id, element);
        Assert.True(preventScroll);
        Assert.Equal(0, OwnFocusCalls());
    }

    [Theory] // ADR-0204 / SRV-5: the filter panel's controls take the keyboard through the core's function — the first, the search field and the last — each by a box holding the control
    [InlineData(1, 0, 0, ".mud-ex-grid-filter-operator")]
    [InlineData(0, 1, 0, ".mud-ex-grid-filter-cancel")]
    [InlineData(0, 0, 1, ".mud-ex-grid-filter-operand")]
    public void The_filter_panel_takes_the_keyboard_through_the_cores_function(int first, int last, int search, string control)
    {
        Render<MudPopoverProvider>();
        var context = new FilterPanelContext(
            "Book", ColumnType.Text, null, FilterOperators.AllowedFor(ColumnType.Text), FilterUiMode.Condition,
            () => Task.FromResult(DistinctValues.TooMany), _ => { }, () => { }, () => { },
            FocusRequest: first, FocusLastRequest: last, SearchRequest: search, TakeFocus: Take);

        var cut = Render(MudGridChrome.Default.FilterPanel(context)!);

        var (element, preventScroll) = Assert.Single(_taken);
        Assert.False(preventScroll);
        Assert.Equal(0, OwnFocusCalls());
        // The box the core is handed holds the control the request is for.
        var box = cut.FindAll("div[style='display: contents']").Single(b => b.GetAttribute("blazor:elementreference") == element);
        Assert.NotNull(box.QuerySelector(control));
    }
}
