using System.Text.Json;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0410 sections 1 and 3: Blazor's ExGrid over the Script Core names the slots its Chrome fills, renders each
/// slot the script mounts beside its root in a hidden holder, drawn by the Chrome's view overload from the context
/// the script sent as data, hands the script the element once drawn, and removes the holder when the script says
/// the slot is gone. Moving the element and wiring its controls are the script's, tested there and in layer 3.
/// </summary>
public class ScriptGridChromeTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _grid;

    public ScriptGridChromeTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        _grid = _module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
        _grid.SetupVoid("slotRendered", _ => true).SetVoidResult();
    }

    private static readonly GridColumn<TestRow>[] Columns = [new("Book", ColumnType.Text, r => r.Book)];

    /// <summary>A Chrome of the Consumer's own that fills the column menu and the loading indicator over the Script Core.</summary>
    private sealed class RazorChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => ScriptSlots.ColumnMenu | ScriptSlots.LoadingIndicator;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? ColumnMenu(GridMenuView view) => builder =>
        {
            for (var i = 0; i < view.Items.Count; i++)
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, ScriptSlotAttributes.MenuItem, i);
                builder.AddContent(2, $"{view.Items[i].Label} ({view.Column}, {view.Type}, {(view.Items[i].Enabled ? "enabled" : "disabled")})");
                builder.CloseElement();
            }
        };

        public RenderFragment? LoadingIndicator(GridLoadingView view) => builder => builder.AddContent(0, view.IsLoading ? "Loading" : "");
    }

    private global::ExGrid.Script.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Script.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Grid(IGridChrome? chrome) => Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
        .Add(g => g.Rows, TestRows.Window())
        .Add(g => g.Columns, Columns)
        .Add(g => g.Chrome, chrome));

    [Fact] // ADR-0410 section 1: the script is told which slots the Chrome fills, and no slot where it fills none
    public void ADR0410_the_slots_the_Chrome_fills_cross_with_the_options()
    {
        Grid(new RazorChrome());
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        Assert.Equal("""["columnMenu","loadingIndicator"]""", document.RootElement.GetProperty("options").GetProperty("razorSlots").GetRawText());
    }

    [Fact] // ADR-0410 sections 1 and 3: a mounted slot is drawn in a hidden holder beside the root from the data sent, handed to the script, and goes when unmounted
    public async Task ADR0410_a_mounted_slot_is_drawn_beside_the_root_and_goes_when_unmounted()
    {
        var cut = Grid(new RazorChrome());
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "columnMenu",
            """{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"Sort A to Z","enabled":true}]}"""));

        var holder = cut.Find("div[hidden] > .ex-razor-slot");
        Assert.Equal("Sort A to Z (Book, Text, enabled)", holder.QuerySelector("button[data-ex-menu-item='0']")?.TextContent);
        // Beside the root, never inside it: the component's markup is the root, then the holder.
        Assert.Equal(2, cut.Nodes.OfType<AngleSharp.Dom.IElement>().Count());
        Assert.Same(holder.ParentElement, cut.Nodes.OfType<AngleSharp.Dom.IElement>().Last());
        var handed = Assert.Single(_grid.Invocations["slotRendered"]);
        Assert.Equal([1L, 1], handed.Arguments.Take(2));

        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2,
            """{"column":"Book","columnType":"text","commands":[{"id":"sort-ascending","label":"Sort A to Z","enabled":false}]}"""));
        Assert.Equal("Sort A to Z (Book, Text, disabled)", cut.Find(".ex-razor-slot button").TextContent);
        // Told back with the number of the data it was drawn for, so the script holds the grid unsettled until the last stands.
        Assert.Equal([1L, 2], _grid.Invocations["slotRendered"][^1].Arguments.Take(2));

        await cut.InvokeAsync(() => Bridge().UnmountSlot(1));
        Assert.Empty(cut.FindAll(".ex-razor-slot"));
    }

    [Fact] // ADR-0410: a Chrome that names a slot this adapter does not fill is refused by name, never left out quietly
    public void ADR0410_a_slot_the_adapter_does_not_fill_is_refused()
    {
        var refused = Assert.ThrowsAny<InvalidOperationException>(() => Grid(new UnknownSlotChrome()));
        Assert.Contains("does not fill", refused.Message, StringComparison.Ordinal);
    }

    private sealed class UnknownSlotChrome : IGridChrome
    {
        public ScriptSlots ScriptSlots => (ScriptSlots)64;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }
}
