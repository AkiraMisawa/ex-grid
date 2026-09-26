using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Reflection;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The column menu and the Context Menu under MudBlazor (WR-4, ADR-0010/0036/0039):
/// exactly the core's commands, in its order and state, as MudButton menu items with an
/// icon each — and the same keys as the built-in menu, because the table is the core's.
/// </summary>
public class MudMenuTests : MudTestContext
{
    private const string Focus = "Blazor._internal.domWrapper.focus";

    private readonly List<string> _ran = [];
    private int _closed;

    private GridCommand Command(string id, bool enabled = true)
        => new(id, enabled, () => { _ran.Add(id); return Task.CompletedTask; });

    // The column menu's seven core commands, two of them disabled, and a Consumer's own.
    private GridCommand[] Commands() =>
    [
        Command("sort-ascending"), Command("sort-descending"), Command("clear-filter", enabled: false),
        Command("hide"), Command("pin", enabled: false), Command("unpin"), Command("size-to-fit"),
        Command("open-report"),
    ];

    private IRenderedComponent<ContainerFragment> RenderMenu(MudGridChrome chrome, GridCommand[]? commands = null, int focusRequest = 1)
        => Render(chrome.ColumnMenu(
            new ColumnMenuContext("Book", ColumnType.Text, commands ?? Commands(), () => _closed++, focusRequest))!);

    private static IReadOnlyList<IElement> Items(IRenderedComponent<ContainerFragment> cut)
        => cut.FindAll("button[role=menuitem]");

    /// <summary>The reference each item's button focuses through, read from the MudButton
    /// itself: bUnit writes a reference into the markup only at the render that created
    /// the element, and a MudButton renders again after its first.</summary>
    private static string[] ItemRefs(IRenderedComponent<ContainerFragment> cut)
        => [.. cut.FindComponents<MudButton>().Select(b => ReferenceOf(b.Instance).Id)];

    private static ElementReference ReferenceOf(MudButton button)
    {
        for (var type = button.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType == typeof(ElementReference));
            if (field is not null)
                return (ElementReference)field.GetValue(button)!;
        }
        throw new InvalidOperationException("MudButton holds no element reference");
    }

    private string? LastFocused()
        => JSInterop.Invocations.Where(i => i.Identifier == Focus)
            .Select(i => ((ElementReference)i.Arguments[0]!).Id).LastOrDefault();

    [Fact] // WR-4 / ADR-0010: exactly the core's commands, in its order, with their state, as menu items
    public void The_items_are_the_cores_commands_in_order_and_state()
    {
        var cut = RenderMenu(MudGridChrome.Default);

        var items = Items(cut);
        Assert.Equal(8, items.Count);
        Assert.All(items, i => Assert.Contains("mud-button-root", i.ClassName));
        Assert.Equal(
            ["Sort ascending", "Sort descending", "Clear filter", "Hide", "Pin up to this column", "Unpin all columns", "Size to fit", "open-report"],
            items.Select(i => i.TextContent.Trim()));
        Assert.Equal(
            [false, false, true, false, true, false, false, false],
            items.Select(i => i.HasAttribute("disabled")));
    }

    [Fact] // WR-4 / ADR-0010/0036: every core command has an icon; a Consumer's has none unless the Chrome supplies one
    public void Every_core_command_has_an_icon_and_a_consumers_only_when_supplied()
    {
        static bool HasIcon(IElement item) => item.QuerySelector(".mud-button-icon-start") is not null;

        var plain = Items(RenderMenu(MudGridChrome.Default));
        Assert.All(plain.Take(7), i => Assert.True(HasIcon(i), i.TextContent));
        Assert.False(HasIcon(plain[7]));

        var supplied = Items(RenderMenu(new MudGridChrome
        {
            Icon = id => id == "open-report" ? Icons.Material.Filled.OpenInNew : null,
        }));
        Assert.True(HasIcon(supplied[7]));
    }

    [Fact] // WR-3 / ADR-0030: MudBlazor's words where it has a key, the Chrome's label function for the rest
    public void The_wording_is_mudblazors_where_keyed_and_the_chromes_elsewhere()
    {
        Services.AddSingleton<ILocalizationInterceptor>(new FrenchInterceptor());
        var chrome = new MudGridChrome
        {
            // Asked for what MudBlazor has no key for; never for Clear filter or Hide.
            Label = id => id switch
            {
                "sort-ascending" => "Tri croissant",
                "clear-filter" => "never asked",
                _ => null,
            },
        };

        var items = Items(RenderMenu(chrome));

        Assert.Equal("Tri croissant", items[0].TextContent.Trim());
        Assert.Equal("Sort descending", items[1].TextContent.Trim());
        Assert.Equal("Effacer le filtre", items[2].TextContent.Trim());
        Assert.Equal("Masquer", items[3].TextContent.Trim());
    }

    private sealed class FrenchInterceptor : ILocalizationInterceptor
    {
        public LocalizedString Handle(string key, params object[] arguments) => key switch
        {
            "MudDataGrid_ClearFilter" => new(key, "Effacer le filtre"),
            "MudDataGrid_Hide" => new(key, "Masquer"),
            _ => new(key, key, resourceNotFound: true),
        };
    }

    [Fact] // KB-29 / ADR-0039: each opening puts DOM focus on the first enabled item
    public void The_menu_focuses_its_first_enabled_item()
    {
        GridCommand[] fenced = [Command("sort-ascending", enabled: false), Command("sort-descending"), Command("clear-filter")];
        var cut = RenderMenu(MudGridChrome.Default, fenced);

        Assert.Equal(ItemRefs(cut)[1], LastFocused());
    }

    [Fact] // ADR-0039 / SRV-5: the opening focus does not scroll the menu, which the user may
           // already have scrolled by the time it lands on a Server circuit; an arrow's does,
           // so the item it moves to is shown
    public async Task The_opening_focus_keeps_the_scroll_and_an_arrows_does_not()
    {
        var cut = RenderMenu(MudGridChrome.Default);
        var opening = JSInterop.Invocations.Last(i => i.Identifier == Focus);
        Assert.Equal(true, opening.Arguments[1]);

        await Items(cut)[1].FocusAsync(new FocusEventArgs());
        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        var moved = JSInterop.Invocations.Last(i => i.Identifier == Focus);
        Assert.Equal(false, moved.Arguments[1]);
    }

    [Fact] // KB-30 / ADR-0039: the arrows follow MenuKeys — over the disabled items, wrapping
    public async Task The_arrows_move_among_the_enabled_items()
    {
        var cut = RenderMenu(MudGridChrome.Default);
        var refs = ItemRefs(cut);
        var menu = cut.Find(".mud-ex-grid-menu");

        await Items(cut)[1].FocusAsync(new FocusEventArgs());
        await menu.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal(refs[3], LastFocused()); // over the disabled Filter

        await Items(cut)[7].FocusAsync(new FocusEventArgs());
        await menu.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal(refs[0], LastFocused()); // wrapped

        await menu.KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Assert.Equal(refs[7], LastFocused());
    }

    [Theory] // KB-30 / ADR-0039: Enter and Space run the item that has focus
    [InlineData("Enter")]
    [InlineData(" ")]
    public async Task Enter_and_space_run_the_focused_item(string key)
    {
        var cut = RenderMenu(MudGridChrome.Default);

        await Items(cut)[3].FocusAsync(new FocusEventArgs());
        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new KeyboardEventArgs { Key = key });

        Assert.Equal(["hide"], _ran);
        Assert.Equal(0, _closed);
    }

    [Fact] // KB-30 / ADR-0039: Tab closes the menu as a Cancel, running nothing
    public async Task Tab_closes_as_a_cancel()
    {
        var cut = RenderMenu(MudGridChrome.Default);

        await Items(cut)[0].FocusAsync(new FocusEventArgs());
        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new KeyboardEventArgs { Key = "Tab", ShiftKey = true });

        Assert.Empty(_ran);
        Assert.Equal(1, _closed);
    }

    [Fact] // WR-4 / A11Y-19 / ADR-0039: in the grid, the Mud items sit in the core's named menu, and a command run closes it
    public async Task In_the_grid_a_command_run_from_the_mud_menu_closes_it()
    {
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.OnSortChanged, s => sorted = s));

        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        var menu = cut.Find(".ex-popover [role=menu]");
        Assert.Equal("Amount", menu.GetAttribute("aria-label"));
        // A11Y-19 / ADR-0044: with no filter to stand below, the menu stands alone.
        Assert.Equal("none", cut.Find(".ex-popover").GetAttribute("role"));
        Assert.Null(cut.Find(".ex-popover").GetAttribute("aria-label"));
        Assert.NotEmpty(menu.QuerySelectorAll(".mud-ex-grid-menu button.mud-button-root[role=menuitem]"));

        await cut.FindAll(".mud-ex-grid-menu button[role=menuitem]")
            .Single(b => b.TextContent.Trim() == "Sort descending").ClickAsync(new MouseEventArgs());

        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], sorted);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // WR-4 / KB-34 / ADR-0039: in the grid the Mud menu asks the core, so keys typed together run the item they chose
    public async Task In_the_grid_keys_typed_together_run_the_item_they_chose()
    {
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.OnSortChanged, s => sorted = s));
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());

        // No focus event in between: on a circuit DOM focus follows the ↓ a round trip
        // later, so the Enter typed with it lands while the first item still has it.
        var menu = cut.Find(".mud-ex-grid-menu");
        await menu.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], sorted);
    }

    // The Mud panel's selects draw their popups into the page's provider — every MudBlazor
    // app has one — so a grid with a filter below its commands renders one too.
    private IRenderedComponent<ExGrid<Trade>> RenderFilterableGrid(Action<IReadOnlyList<SortSpec>> sorted, Action<GridFilter?> filtered)
    {
        Render<MudPopoverProvider>();
        return Render<ExGrid<Trade>>(ps => ps
            .Add(g => g.Window, Rows(5))
            .Add(g => g.TotalCount, 5)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 400)
            .Add(g => g.Chrome, MudGridChrome.Default)
            .Add(g => g.OnSortChanged, s => sorted(s))
            .Add(g => g.OnFilterChanged, f => filtered(f)));
    }

    [Fact] // WR-4 / ADR-0044 / FL-12: in the grid the Mud menu stands above the Mud panel, in one popover
    public async Task In_the_grid_the_mud_menu_stands_above_the_mud_panel()
    {
        var cut = RenderFilterableGrid(_ => { }, _ => { });

        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());

        var popover = Assert.Single(cut.FindAll(".ex-popover"));
        // A11Y-19: a dialog holding the menu, both named by the column's header.
        Assert.Equal("dialog", popover.GetAttribute("role"));
        Assert.Equal("Amount", popover.GetAttribute("aria-label"));
        Assert.Equal("Amount", popover.QuerySelector("[role=menu]")!.GetAttribute("aria-label"));
        var menu = popover.QuerySelector(".mud-ex-grid-menu");
        var panel = popover.QuerySelector("form.mud-ex-grid-filter");
        Assert.NotNull(menu);
        Assert.NotNull(panel);
        Assert.True(menu!.CompareDocumentPosition(panel!).HasFlag(AngleSharp.Dom.DocumentPositions.Following));
        Assert.Contains(cut.FindAll(".mud-ex-grid-menu button[role=menuitem]"), b => b.TextContent.Trim() == "Clear filter");
    }

    [Fact] // WR-4 / ADR-0044 / FL-15: in the grid the Mud menu answers Excel's letters through the core
    public async Task In_the_grid_the_mud_menu_answers_the_letters()
    {
        IReadOnlyList<SortSpec>? sorted = null;
        var cut = RenderFilterableGrid(s => sorted = s, _ => { });
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());

        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new KeyboardEventArgs { Key = "o" });

        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], sorted);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // WR-4 / ADR-0044 / KB-31: Tab on a Mud command moves the keyboard to the Mud panel, and nothing closes
    public async Task In_the_grid_tab_moves_from_the_mud_menu_to_the_mud_panel()
    {
        var cut = RenderFilterableGrid(_ => { }, _ => { });
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        var before = JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

        await cut.Find(".mud-ex-grid-menu").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });

        cut.WaitForAssertion(() => Assert.True(
            JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)) > before));
        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Equal(0, _closed);
    }

    [Fact] // WR-4 / ADR-0044 / FL-16: in the grid the Mud menu's Clear filter is disabled with no filter, and clears and closes with one
    public async Task In_the_grid_clear_filter_clears_and_closes()
    {
        var filtered = new List<GridFilter?>();
        var cut = RenderFilterableGrid(_ => { }, f => filtered.Add(f));
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        IElement ClearFilter() => cut.FindAll(".mud-ex-grid-menu button[role=menuitem]").Single(b => b.TextContent.Trim() == "Clear filter");
        Assert.True(ClearFilter().HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".mud-ex-grid-filter-clear"));
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());

        cut.Render(ps => ps.Add(g => g.Filters, new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Amount"] = new([new FilterClause(FilterOperator.GreaterThan, 100m)]),
        })));
        await cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs());
        Assert.False(ClearFilter().HasAttribute("disabled"));
        await ClearFilter().ClickAsync(new MouseEventArgs());

        Assert.Null(Assert.Single(filtered));
        Assert.Empty(cut.FindAll(".ex-popover"));
    }
}
