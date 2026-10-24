using Bunit;
using ExPivot.Chrome;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// The Wrapper's pivot surfaces in the pivot's Input Order (ADR-0240 section 6, tickets 05 and 06):
/// its buttons claim their click and their keys and press at Enter's keydown, its boxes claim their
/// click, its drafts claim nothing, and its controls take the keyboard through the context's seam, as
/// ExPivot's built-in markup does — swapping the Chrome changes no behaviour (ADR-0061).
/// </summary>
public class MudPivotInputOrderTests : MudPivotTestContext
{
    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount")] };

    private int FocusCalls() => JSInterop.Invocations.Count(invocation => invocation.Identifier == Focus);

    [Fact] // ADR-0240 §6: the Mud buttons claim their click and keys, the boxes their click, and the drafts nothing, on the element that takes the keyboard
    public async Task The_mud_controls_claim_their_input_on_the_element_that_takes_the_keyboard()
    {
        var cut = RenderPivot(RegionProduct with { Filters = [P("Online")], Columns = [] });

        Assert.Equal("click keydown", EntryButton(cut, "Rows", "Region").GetAttribute("data-ex-claims"));
        Assert.Equal("click keydown", cut.Find(".mud-ex-pivot-pane-close").GetAttribute("data-ex-claims"));
        Assert.Equal("click keydown", cut.Find(".mud-ex-pivot-update").GetAttribute("data-ex-claims"));
        Assert.Equal("click keydown", cut.Find(".mud-ex-pivot-filter-button").GetAttribute("data-ex-claims"));
        Assert.Equal("click keydown", cut.Find(".mud-ex-pivot-layout-button").GetAttribute("data-ex-claims"));
        Assert.Equal("click keydown", cut.Find(".mud-ex-pivot-field-list-toggle").GetAttribute("data-ex-claims"));
        Assert.Equal("click", Field(cut, "Region").QuerySelector("input[type=checkbox]")!.GetAttribute("data-ex-claims"));
        Assert.Equal("click", cut.Find(".mud-ex-pivot-defer input[type=checkbox]").GetAttribute("data-ex-claims"));
        Assert.Equal("", cut.Find(".mud-ex-pivot-pane input.mud-input-root").GetAttribute("data-ex-claims"));

        await cut.InvokeAsync(() => cut.Find(".mud-ex-pivot-filter-button").ClickAsync(new MouseEventArgs { Detail = 1 }));
        var filter = cut.FindComponent<MudPivotItemFilter>();
        Assert.Equal("", filter.Find("input.mud-input-root").GetAttribute("data-ex-claims"));
        Assert.All(filter.FindAll("input[type=checkbox]"), box => Assert.Equal("", box.GetAttribute("data-ex-claims")));
        Assert.Equal("click keydown", filter.Find(".mud-ex-pivot-ok").GetAttribute("data-ex-claims"));
        Assert.Equal("click keydown", filter.Find(".mud-ex-pivot-cancel").GetAttribute("data-ex-claims"));
    }

    [Fact] // ADR-0240 §6: Enter presses a Mud menu's command at its keydown, and the click the browser makes of it runs nothing more
    public async Task Enter_presses_a_mud_command_at_its_keydown()
    {
        var cut = RenderPivot(RegionProduct);
        await OpenMenuAsync(cut, "Rows", "Region");
        var moveDown = MenuItems(cut).Single(b => b.TextContent.Trim() == "Move Down");

        await cut.InvokeAsync(() => moveDown.KeyDownAsync(new KeyboardEventArgs { Key = "Enter", Type = "keydown" }));

        Assert.Equal(["Product", "Region"], Entries(cut, "Rows"));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0240 §6: Enter on a Mud entry opens its menu at the keydown, the entry's click the browser makes of it does not close it again, and a press by the pointer toggles it
    public async Task Enter_on_a_mud_entry_opens_its_menu_once()
    {
        var cut = RenderPivot(RegionProduct);

        await cut.InvokeAsync(() => EntryButton(cut, "Rows", "Region").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", Type = "keydown" }));
        await cut.InvokeAsync(() => EntryButton(cut, "Rows", "Region").ClickAsync(new MouseEventArgs { Detail = 0 }));
        Assert.Single(cut.FindAll(".ex-pivot-popup"));

        await cut.InvokeAsync(() => EntryButton(cut, "Rows", "Region").ClickAsync(new MouseEventArgs { Detail = 1 }));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0240 §6: a Mud menu opened takes the keyboard in the opening's turn, on its first enabled command, through the report grid's guard and not by the button's own focus
    public async Task A_mud_menu_takes_the_keyboard_through_the_report_grids_guard()
    {
        var handle = ReportGridHandle();
        var cut = RenderPivot(RegionProduct);
        var focusCalls = FocusCalls();

        await cut.InvokeAsync(() => OpenMenuAsync(cut, "Rows", "Region"));

        var first = cut.FindComponents<MudPivotButton>().First(b => b.Instance.Class == "mud-ex-pivot-menu-item" && !b.Instance.Disabled);
        Assert.Equal([ElementIdOf(first.Instance)], KeyboardHandOffs(handle));
        Assert.Equal(focusCalls, FocusCalls());
    }

    [Fact] // ADR-0240 §6: Field Settings' select takes the keyboard through the report grid's guard, handed the box around it, in the opening's turn
    public async Task Mud_field_settings_take_the_keyboard_through_the_report_grids_guard()
    {
        var handle = ReportGridHandle();
        var cut = RenderPivot(RegionProduct);
        await cut.InvokeAsync(() => OpenMenuAsync(cut, "Rows", "Region"));
        var before = KeyboardHandOffs(handle).Length;

        await cut.InvokeAsync(() => RunMenuAsync(cut, "Field Settings…"));

        Assert.NotNull(cut.FindComponent<MudPivotFieldSettings>());
        Assert.Equal("", cut.Find(".mud-ex-pivot-sort input").GetAttribute("data-ex-claims"));
        Assert.Single(KeyboardHandOffs(handle).Skip(before));
    }

    [Fact] // ADR-0360: while no report stands — the first one still on its way — there is no grid to hand the keyboard on, and Field Settings' select takes it from the grid's script, handed the box around it, not by the box's own focus
    public async Task ADR0360_with_no_report_mud_field_settings_take_the_keyboard_from_the_grids_script()
    {
        var module = JSInterop.SetupModule(global::ExGrid.Testing.ShippedScript.Address);
        module.Setup<string>("stamp").SetResult(global::ExGrid.Testing.ShippedScript.Stamp);
        var focusBox = module.Setup<bool>("focusBox", _ => true);
        focusBox.SetResult(true);
        // The source's first answer never comes, so the pane shows the layout and no report is drawn.
        var data = PivotSource.From(Sales, Fields);
        var never = new TaskCompletionSource();
        var source = PivotSource.Fetch(Fields, PivotSourceFeatures.All,
            async (query, ct) => { await never.Task; return await data.AggregateAsync(query, ct); },
            (query, ct) => data.ItemsAsync(query, ct),
            (query, ct) => data.DetailsAsync(query, ct));
        var cut = RenderPivot(RegionProduct, source: source);
        Assert.Empty(cut.FindComponents<global::ExGrid.Components.ExGrid<PivotDisplayRow>>());
        await cut.InvokeAsync(() => OpenMenuAsync(cut, "Rows", "Region"));
        var focusCalls = FocusCalls();
        var before = focusBox.Invocations.Count;

        await cut.InvokeAsync(() => RunMenuAsync(cut, "Field Settings…"));

        Assert.NotNull(cut.FindComponent<MudPivotFieldSettings>());
        var box = cut.Find(".mud-ex-pivot-field-settings > div[style='display: contents']");
        var asked = Assert.Single(focusBox.Invocations.Skip(before));
        Assert.Equal(box.GetAttribute("blazor:elementreference"), ((ElementReference)asked.Arguments[0]!).Id);
        Assert.Equal(focusCalls, FocusCalls());
    }

    [Fact] // ADR-0240 §6: a Mud menu's request to take the keyboard stays with the command it was given to, when a press disables that command
    public void A_mud_menus_request_stays_with_its_command_when_a_press_disables_it()
    {
        var asked = new List<ElementReference>();
        Func<ElementReference, Task> take = control => { asked.Add(control); return Task.CompletedTask; };
        PivotCommand[] commands = [new("first", "First", true, () => Task.CompletedTask), new("second", "Second", true, () => Task.CompletedTask)];
        var menu = Render<MudPivotMenu>(ps => ps
            .Add(m => m.Chrome, MudPivotChrome.Default)
            .Add(m => m.Context, new PivotMenuContext("Menu", commands, () => { }, 1) { TakeKeyboard = take }));
        Assert.Single(asked);

        menu.Render(ps => ps.Add(m => m.Context, new PivotMenuContext("Menu", [commands[0] with { Enabled = false }, commands[1]], () => { }, 1) { TakeKeyboard = take }));

        Assert.Single(asked);
    }
}
