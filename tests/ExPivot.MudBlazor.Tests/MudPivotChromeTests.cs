using Bunit;
using ExGrid.MudBlazor;
using ExPivot.Components;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Extensions;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// ExPivot under MudBlazor (ADR-0062): the pane, the menus, the panels and the report filter band
/// drawn with MudBlazor's controls, inside ExPivot's frames, making the same layouts the built-in
/// markup makes (ADR-0061) — and the report's grid dressed by the grid Wrapper, in ExPivot's words.
/// </summary>
public class MudPivotChromeTests : MudPivotTestContext
{
    private static readonly PivotLayout RegionProduct = new()
    {
        Rows = [P("Region"), P("Product")],
        Values = [Sum("Amount")],
    };

    [Fact] // ADR-0062: the pane is MudBlazor's controls — a search field, a checkbox per field, the four Areas, Defer Layout Update at the foot — in ExPivot's region
    public void The_pane_is_drawn_with_MudBlazor_controls()
    {
        var cut = RenderPivot();

        Assert.Single(cut.FindAll(".ex-pivot-field-list .mud-ex-pivot-pane"));
        Assert.Empty(cut.FindAll(".ex-pivot-pane"));
        Assert.Single(cut.FindComponents<MudTextField<string>>());
        Assert.Equal(["Region", "Product", "Amount", "Quantity", "Online", "Defer Layout Update"],
            cut.FindComponents<MudCheckBox<bool>>().Select(c => c.Instance.Label));
        Assert.Equal(["Filters", "Columns", "Rows", "Values"],
            cut.FindAll(".mud-ex-pivot-area-name").Select(e => e.TextContent.Trim()));
        Assert.Equal("Update", cut.Find(".mud-ex-pivot-defer .mud-ex-pivot-update").TextContent.Trim());
    }

    [Fact] // ADR-0061/0062: a tick through MudBlazor's checkbox places the field where Excel does
    public async Task Ticking_places_fields()
    {
        var cut = RenderPivot();

        await TickFieldAsync(cut, "Region", true);
        await TickFieldAsync(cut, "Amount", true);

        Assert.Equal(["Region"], Entries(cut, "Rows"));
        Assert.Equal(["Sum of Amount"], Entries(cut, "Values"));
        Assert.Equal("East | 180", RowTexts(cut)[0]);
        Assert.True(cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Region").Instance.GetState(x => x.Value));
    }

    [Fact] // ADR-0061/0062: a field dragged from the list onto an Area stands there, the indicator on the Area meanwhile
    public async Task Dragging_a_field_onto_an_area()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        await Field(cut, "Online").DragStartAsync(new DragEventArgs());
        await Area(cut, "Columns").DragEnterAsync(new DragEventArgs());
        Assert.Contains("mud-ex-pivot-drop-target", Area(cut, "Columns").ClassName);
        await Area(cut, "Columns").DropAsync(new DragEventArgs());

        Assert.Equal(["Online"], Entries(cut, "Columns"));
        Assert.Equal(["Row Labels", "FALSE", "TRUE", "Grand Total"], HeaderTexts(cut));
        Assert.DoesNotContain("mud-ex-pivot-drop-target", Area(cut, "Columns").ClassName);
    }

    [Fact] // ADR-0061/0062: an entry dropped before another reorders; one dropped on the list of fields is removed
    public async Task Dragging_an_entry_reorders_and_removes()
    {
        var cut = RenderPivot(RegionProduct);

        await Area(cut, "Rows").QuerySelectorAll(".mud-ex-pivot-entry")[1].DragStartAsync(new DragEventArgs());
        await Area(cut, "Rows").QuerySelectorAll(".mud-ex-pivot-entry")[0].DropAsync(new DragEventArgs());
        Assert.Equal(["Product", "Region"], Entries(cut, "Rows"));

        await Area(cut, "Rows").QuerySelectorAll(".mud-ex-pivot-entry")[0].DragStartAsync(new DragEventArgs());
        Assert.Contains("mud-ex-pivot-drop-remove", cut.Find(".mud-ex-pivot-fields").ClassName);
        await cut.Find(".mud-ex-pivot-fields").DropAsync(new DragEventArgs());
        Assert.Equal(["Region"], Entries(cut, "Rows"));
    }

    [Fact] // ADR-0061/0062: an entry's menu is MudButtons inside ExPivot's frame — ExPivot's commands, order and states, each with its icon
    public async Task An_entrys_menu_is_ExPivots_commands_as_MudButtons()
    {
        var cut = RenderPivot(RegionProduct);

        await OpenMenuAsync(cut, "Rows", "Region");

        var frame = cut.Find(".ex-pivot-popup");
        Assert.Equal("menu", frame.GetAttribute("role"));
        Assert.NotNull(frame.QuerySelector(".mud-ex-pivot-menu"));
        var items = MenuItems(cut);
        Assert.Equal(
        [
            "Move Up", "Move Down", "Move to Beginning", "Move to End", "Move to Report Filter", "Move to Row Labels",
            "Move to Column Labels", "Move to Values", "Remove Field", "Sort A to Z", "Sort Z to A", "Filter…",
            "Expand Entire Field", "Collapse Entire Field", "Field Settings…",
        ], items.Select(b => b.TextContent.Trim()));
        Assert.Equal(
            ["Move Up", "Move to Beginning", "Move to Row Labels", "Sort A to Z", "Expand Entire Field"],
            items.Where(b => b.HasAttribute("disabled")).Select(b => b.TextContent.Trim()));
        Assert.All(items, item => Assert.Equal("menuitem", item.GetAttribute("role")));
        Assert.All(items, item => Assert.NotNull(item.QuerySelector(".mud-button-icon-start")));
        Assert.Equal("true", EntryButton(cut, "Rows", "Region").GetAttribute("aria-expanded"));
    }

    [Fact] // ADR-0061/0062: a command from the Mud menu makes the layout the built-in's makes, and closes the menu
    public async Task A_command_moves_the_field()
    {
        var cut = RenderPivot(new PivotLayout
        {
            Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }, P("Product")],
            Values = [Sum("Amount")],
        });

        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Move to Column Labels");

        Assert.Equal(["Region"], Entries(cut, "Columns"));
        Assert.Equal(["Row Labels", "East", "North", "(blank)", "Grand Total"], HeaderTexts(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0039/0061: the menu's first enabled command takes DOM focus, scrolled into view; closed, the entry takes it back
    public async Task The_keyboard_goes_to_the_menu_and_back()
    {
        var cut = RenderPivot(RegionProduct);

        await OpenMenuAsync(cut, "Rows", "Region");
        var first = cut.FindComponents<MudPivotButton>().First(b => b.Instance.Class == "mud-ex-pivot-menu-item" && !b.Instance.Disabled);
        Assert.Equal((ElementIdOf(first.Instance), false), LastFocus());

        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        var entry = cut.FindComponents<MudPivotButton>().First(b => b.Instance.Class == "mud-ex-pivot-entry-button");
        Assert.Equal((ElementIdOf(entry.Instance), false), LastFocus());
    }

    [Fact] // ADR-0061/0062: Filter… under MudBlazor — every Item, (Select All) tri-state; unticking one and OK hides it
    public async Task Filter_hides_an_item()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"],
            cut.FindAll(".mud-ex-pivot-item-filter .mud-checkbox").Select(c => c.TextContent.Trim()));
        await TickItemAsync(cut, "West", false);
        Assert.Null(cut.FindComponent<MudCheckBox<bool?>>().Instance.GetState(x => x.Value));
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal([PivotItemKey.Text("West")], cut.Instance.CurrentLayout.Rows[0].HiddenItems);
        Assert.Equal(["East | 180", "North | 10", "(blank) | 5", "Grand Total | 195"], RowTexts(cut));
        Assert.NotNull(Area(cut, "Rows").QuerySelector(".mud-ex-pivot-filtered-mark"));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0060/0061: unticking every Item disables OK and says why, in ExPivot's words
    public async Task Filter_refuses_to_hide_every_item()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        await TickItemAsync(cut, "(Select All)", false);

        Assert.True(cut.Find(".mud-ex-pivot-ok").HasAttribute("disabled"));
        Assert.Equal("Select at least one item.", cut.Find(".mud-ex-pivot-refusal").TextContent.Trim());
        Assert.Equal("alert", cut.Find(".mud-ex-pivot-refusal").GetAttribute("role"));
    }

    [Fact] // ADR-0061: Cancel drops the draft, under MudBlazor too
    public async Task Cancel_drops_the_draft()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");
        await TickItemAsync(cut, "West", false);

        await cut.Find(".mud-ex-pivot-cancel").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.Instance.CurrentLayout.Rows[0].HiddenItems);
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0061/0062: Field Settings… — the order from a MudSelect, the subtotals from a MudRadioGroup
    public async Task Field_settings_apply_subtotals_and_order()
    {
        var cut = RenderPivot(RegionProduct with { SubtotalsAtTop = false });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Field Settings…");

        var sort = cut.FindComponent<MudSelect<int>>();
        Assert.Equal("Ascending (A to Z) by label", sort.Instance.ToStringFunc!(sort.Instance.GetState(x => x.Value)));
        await cut.InvokeAsync(() => sort.Instance.ValueChanged.InvokeAsync(3));
        await cut.InvokeAsync(() => cut.FindComponent<MudRadioGroup<bool>>().Instance.ValueChanged.InvokeAsync(false));
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        var layout = cut.Instance.CurrentLayout;
        Assert.False(layout.Rows[0].Subtotals);
        Assert.Equal(new PivotSort(PivotSortDirection.Descending, 0), layout.Rows[0].Sort);
        Assert.Equal("−East |", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0061/0062: Value Field Settings… — the caption follows the Aggregation chosen in a MudSelect until the user writes one
    public async Task Value_field_settings_change_the_aggregation()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");

        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<PivotAggregation>>().Instance.ValueChanged.InvokeAsync(PivotAggregation.Average));
        Assert.Equal("Average of Amount", cut.Find(".mud-ex-pivot-caption input").GetAttribute("value"));
        await cut.Find(".mud-ex-pivot-number-format input").InputAsync(new ChangeEventArgs { Value = "N1" });
        Assert.Equal("Sample: -1,234.6", cut.Find(".mud-ex-pivot-sample").TextContent.Trim());
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        var value = cut.Instance.CurrentLayout.Values[0];
        Assert.Equal(PivotAggregation.Average, value.Aggregation);
        Assert.Null(value.Caption);
        Assert.Equal("N1", value.NumberFormat);
        Assert.Equal(["Row Labels", "Average of Amount"], HeaderTexts(cut));
    }

    [Fact] // ADR-0060/0061: a taken caption is refused, the sentence shown under MudBlazor, and the panel stays
    public async Task Value_field_settings_refuse_a_taken_caption()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Sum("Quantity")] });
        await OpenMenuAsync(cut, "Values", "Sum of Quantity");
        await RunMenuAsync(cut, "Value Field Settings…");

        await cut.Find(".mud-ex-pivot-caption input").InputAsync(new ChangeEventArgs { Value = "Sum of Amount" });
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal("PivotTable field name already exists.", cut.Find(".mud-ex-pivot-refusal").TextContent.Trim());
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Null(cut.Instance.CurrentLayout.Values[1].Caption);
    }

    [Fact] // ADR-0039/0062: while a MudSelect's list is open, Escape is the list's; closed, Escape closes the panel
    public async Task An_open_select_keeps_escape_for_itself()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");
        var select = cut.FindComponent<MudSelect<PivotAggregation>>();

        await cut.InvokeAsync(() => select.Instance.OpenMenu());
        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Single(cut.FindAll(".ex-pivot-popup"));

        await cut.InvokeAsync(() => select.Instance.CloseMenu());
        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0061/0062: the report filter band — an outlined MudButton showing (All), then the one Item, in the primary colour once filtered
    public async Task The_report_filter_band()
    {
        var cut = RenderPivot(new PivotLayout { Filters = [P("Region")], Values = [Sum("Amount")] });
        Assert.Equal("(All)", cut.Find(".mud-ex-pivot-filter-summary").TextContent.Trim());

        await cut.Find(".mud-ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        var frame = cut.Find(".mud-ex-pivot-filter-anchor .ex-pivot-popup");
        Assert.Contains("ex-pivot-popup-overlay", frame.ClassName);
        Assert.Single(cut.FindAll(".ex-pivot-backdrop"));
        await TickItemAsync(cut, "(Select All)", false);
        await TickItemAsync(cut, "East", true);
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal("East", cut.Find(".mud-ex-pivot-filter-summary").TextContent.Trim());
        Assert.Contains("mud-button-outlined-primary", cut.Find(".mud-ex-pivot-filter-button").ClassName);
        Assert.Equal(["180"], RowTexts(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-backdrop"));
    }

    [Fact] // ADR-0061/0062: the report grid is dressed by MudGridChrome, which words ExPivot's commands in ExPivot's words, never their ids
    public void The_grid_menu_speaks_ExPivots_words()
    {
        var cut = RenderPivot(RegionProduct, showDetails: EventCallback.Factory.Create<PivotDetails>(this, () => { }));

        var chrome = Assert.IsType<MudGridChrome>(Grid(cut).Instance.Chrome);
        var menu = Render(chrome.ContextMenu(ContextAt(cut, 1, Grid(cut).Instance.Columns[1].Name))!);

        var labels = menu.FindAll(".mud-ex-grid-menu-item").Select(b => b.TextContent.Trim()).ToArray();
        Assert.Contains("Remove \"Product\"", labels);
        Assert.Contains("Show Details", labels);
        Assert.Contains("Collapse", labels);
        Assert.DoesNotContain(labels, label => label.Contains("remove-named-field", StringComparison.Ordinal));
        Assert.Equal(Icons.Material.Filled.ManageSearch, chrome.Icon!(PivotCommandIds.ShowDetails));
        Assert.Null(chrome.Label!("copy"));
    }

    [Fact] // ADR-0061: the Consumer's Label written on ExPivot reaches the grid Wrapper's menu too — one Label words the whole pivot
    public void The_consumers_words_reach_the_grid_menu()
    {
        Func<string, string?> label = id => id switch
        {
            PivotCommandIds.ShowDetails => "Details anzeigen",
            PivotCommandIds.RemoveNamedField => "\"{0}\" entfernen",
            "field-list" => "PivotTable-Felder",
            _ => null,
        };
        var cut = RenderPivot(RegionProduct, label: label, showDetails: EventCallback.Factory.Create<PivotDetails>(this, () => { }));

        var chrome = Assert.IsType<MudGridChrome>(Grid(cut).Instance.Chrome);
        Assert.Equal("Details anzeigen", chrome.Label!(PivotCommandIds.ShowDetails));
        Assert.Equal("\"Product\" entfernen", chrome.Label!(PivotCommandIds.RemoveNamedField + ":Product"));
        Assert.Equal("PivotTable-Felder", cut.Find(".mud-ex-pivot-pane-title").TextContent.Trim());
    }

    [Fact] // ADR-0003/0061: the grid's Chrome is asked once and held — a new one per render would re-render the grid every time
    public async Task The_grid_chrome_is_held_across_renders()
    {
        var cut = RenderPivot(RegionProduct);
        var before = Grid(cut).Instance.Chrome;

        await TickFieldAsync(cut, "Online", true);

        Assert.NotNull(before);
        Assert.Same(before, Grid(cut).Instance.Chrome);
    }

    [Fact] // ADR-0062/0003: the ± button in a label cell stays ExPivot's plain markup — no MudBlazor component in a painted row
    public void The_toggle_stays_plain_markup()
    {
        var cut = RenderPivot(RegionProduct);

        var toggle = cut.FindAll(".ex-viewport .ex-pivot-toggle")[0];
        Assert.Equal("BUTTON", toggle.TagName);
        Assert.DoesNotContain("mud-", toggle.ClassName);
        Assert.Empty(cut.FindAll(".ex-viewport .mud-button-root"));
        Assert.Empty(cut.FindAll(".ex-viewport .mud-icon-root"));
    }

    [Fact] // ADR-0061: swapping the Chrome changes no behaviour — the same gestures make the same layout under both
    public async Task The_same_gestures_make_the_same_layout_under_either_chrome()
    {
        var mud = RenderPivot();
        await TickFieldAsync(mud, "Region", true);
        await TickFieldAsync(mud, "Amount", true);
        await Field(mud, "Online").DragStartAsync(new DragEventArgs());
        await Area(mud, "Columns").DropAsync(new DragEventArgs());
        await OpenMenuAsync(mud, "Rows", "Region");
        await RunMenuAsync(mud, "Sort Z to A");

        var plain = RenderPivot(chrome: BuiltIn);
        await Tick(plain, "Region");
        await Tick(plain, "Amount");
        await plain.FindAll(".ex-pivot-field").Single(f => f.TextContent.Trim() == "Online").DragStartAsync(new DragEventArgs());
        await plain.FindAll(".ex-pivot-area").Single(a => a.QuerySelector(".ex-pivot-area-title")!.TextContent.Trim() == "Columns")
            .DropAsync(new DragEventArgs());
        await plain.FindAll(".ex-pivot-area").Single(a => a.QuerySelector(".ex-pivot-area-title")!.TextContent.Trim() == "Rows")
            .QuerySelector(".ex-pivot-entry-button")!.ClickAsync(new MouseEventArgs());
        await plain.FindAll(".ex-pivot-menu-item").Single(b => b.TextContent.Trim() == "Sort Z to A").ClickAsync(new MouseEventArgs());

        Assert.Equal(PivotLayoutJson.Write(plain.Instance.CurrentLayout), PivotLayoutJson.Write(mud.Instance.CurrentLayout));
        Assert.Equal(RowTexts(plain), RowTexts(mud));

        static Task Tick(IRenderedComponent<PivotComponent> cut, string caption)
            => cut.FindAll(".ex-pivot-field").Single(f => f.TextContent.Trim() == caption).QuerySelector("input")!
                .ChangeAsync(new ChangeEventArgs { Value = true });
    }
}
