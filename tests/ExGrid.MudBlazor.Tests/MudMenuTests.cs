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
        Command("sort-ascending"), Command("sort-descending"), Command("filter", enabled: false),
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
            ["Sort ascending", "Sort descending", "Filter", "Hide", "Pin up to this column", "Unpin all columns", "Size to fit", "open-report"],
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
            // Asked for what MudBlazor has no key for; never for Filter or Hide.
            Label = id => id switch
            {
                "sort-ascending" => "Tri croissant",
                "filter" => "never asked",
                _ => null,
            },
        };

        var items = Items(RenderMenu(chrome));

        Assert.Equal("Tri croissant", items[0].TextContent.Trim());
        Assert.Equal("Sort descending", items[1].TextContent.Trim());
        Assert.Equal("Filtrer", items[2].TextContent.Trim());
        Assert.Equal("Masquer", items[3].TextContent.Trim());
    }

    private sealed class FrenchInterceptor : ILocalizationInterceptor
    {
        public LocalizedString Handle(string key, params object[] arguments) => key switch
        {
            "MudDataGrid_Filter" => new(key, "Filtrer"),
            "MudDataGrid_Hide" => new(key, "Masquer"),
            _ => new(key, key, resourceNotFound: true),
        };
    }

    [Fact] // KB-29 / ADR-0039: each opening puts DOM focus on the first enabled item
    public void The_menu_focuses_its_first_enabled_item()
    {
        GridCommand[] fenced = [Command("sort-ascending", enabled: false), Command("sort-descending"), Command("filter")];
        var cut = RenderMenu(MudGridChrome.Default, fenced);

        Assert.Equal(ItemRefs(cut)[1], LastFocused());
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
        var menu = cut.Find(".ex-popover[role=menu]");
        Assert.Equal("Amount", menu.GetAttribute("aria-label"));
        Assert.NotEmpty(menu.QuerySelectorAll(".mud-ex-grid-menu button.mud-button-root[role=menuitem]"));

        await cut.FindAll(".mud-ex-grid-menu button[role=menuitem]")
            .Single(b => b.TextContent.Trim() == "Sort descending").ClickAsync(new MouseEventArgs());

        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], sorted);
        Assert.Empty(cut.FindAll(".ex-popover"));
    }
}
