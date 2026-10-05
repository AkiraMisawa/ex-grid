using System.Reflection;
using Bunit;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>The Field List through its built-in markup (ADR-0061): ticking, dragging, the menus.</summary>
public class FieldListTests : PivotTestContext
{
    [Fact] // ADR-0061: every declared field, with a checkbox, and the four Areas in Excel's order
    public void The_pane_lists_the_fields_and_the_areas()
    {
        var cut = RenderPivot();

        Assert.Equal("PivotTable Fields", cut.Find(".ex-pivot-pane-title").TextContent);
        Assert.Equal(["Region", "Product", "Amount", "Quantity", "Online"],
            cut.FindAll(".ex-pivot-field-caption").Select(e => e.TextContent));
        Assert.Equal(["Filters", "Columns", "Rows", "Values"], cut.FindAll(".ex-pivot-area-title").Select(e => e.TextContent));
        Assert.Equal("region", cut.Find(".ex-pivot-field-list").GetAttribute("role"));
    }

    [Fact] // ADR-0061: ticking places a field where Excel does, and the layout is raised
    public async Task Ticking_places_fields_and_raises_the_layout()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(parameters: ps => ps.Add(p => p.LayoutChanged, told.Add));

        await FieldItem(cut, "Region").QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = true });
        await FieldItem(cut, "Amount").QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(2, told.Count);
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.Equal(["Sum of Amount"], AreaEntries(cut, "Values"));
        Assert.Equal("East | 180", RowTexts(cut)[0]);
        Assert.True(FieldItem(cut, "Region").QuerySelector("input")!.HasAttribute("checked"));
        Assert.False(FieldItem(cut, "Product").QuerySelector("input")!.HasAttribute("checked"));
    }

    [Fact] // ADR-0061: unticking removes the field from every Area
    public async Task Unticking_removes_the_field()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), new("Region", PivotAggregation.Count)] });

        await FieldItem(cut, "Region").QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = false });

        Assert.Empty(AreaEntries(cut, "Rows"));
        Assert.Equal(["Sum of Amount"], AreaEntries(cut, "Values"));
    }

    [Fact] // ADR-0061: the search narrows the list of fields
    public async Task The_search_narrows_the_fields()
    {
        var cut = RenderPivot();

        await cut.Find(".ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "an" });

        Assert.Equal(["Quantity"], cut.FindAll(".ex-pivot-field-caption").Select(e => e.TextContent));
    }

    [Fact] // ADR-0061: a field dragged from the list and dropped on an Area stands there
    public async Task Dragging_a_field_onto_an_area()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        await FieldItem(cut, "Online").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Columns").DragEnterAsync(new DragEventArgs());
        Assert.Contains("ex-pivot-drop-target", AreaElement(cut, "Columns").ClassName);
        await AreaElement(cut, "Columns").DropAsync(new DragEventArgs());

        Assert.Equal(["Online"], AreaEntries(cut, "Columns"));
        Assert.Equal(["Row Labels", "FALSE", "TRUE", "Grand Total"], HeaderTexts(cut));
        Assert.DoesNotContain("ex-pivot-drop-target", AreaElement(cut, "Columns").ClassName);
    }

    [Fact] // ADR-0061: an entry dropped before another in its Area is a reorder
    public async Task Dropping_an_entry_before_another_reorders()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });
        var entries = AreaElement(cut, "Rows").QuerySelectorAll(".ex-pivot-entry");

        await entries[1].DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Rows").QuerySelectorAll(".ex-pivot-entry")[0].DropAsync(new DragEventArgs());

        Assert.Equal(["Product", "Region"], AreaEntries(cut, "Rows"));
    }

    [Fact] // ADR-0061: an entry dragged back to the list of fields is removed
    public async Task Dropping_an_entry_on_the_list_removes_it()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        await AreaElement(cut, "Rows").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        Assert.Contains("ex-pivot-drop-remove", cut.Find(".ex-pivot-fields").ClassName);
        await cut.Find(".ex-pivot-fields").DropAsync(new DragEventArgs());

        Assert.Empty(AreaEntries(cut, "Rows"));
    }

    [Fact] // ADR-0061: a drag that ends without a drop changes nothing
    public async Task A_drag_that_ends_changes_nothing()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")] }, ps => ps.Add(p => p.LayoutChanged, told.Add));

        await FieldItem(cut, "Product").DragStartAsync(new DragEventArgs());
        await FieldItem(cut, "Product").DragEndAsync(new DragEventArgs());
        await AreaElement(cut, "Rows").DropAsync(new DragEventArgs());

        Assert.Empty(told);
    }

    [Fact] // ADR-0061: Σ Values appears with a second Value Field and moves between Rows and Columns only
    public async Task Values_pseudo_field_moves_between_rows_and_columns()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Sum("Quantity")] });
        Assert.Equal(["Σ Values"], AreaEntries(cut, "Columns"));

        await OpenMenuAsync(cut, "Columns", "Σ Values");
        Assert.Equal(["Move to Row Labels", "Move to Column Labels"], cut.FindAll(".ex-pivot-menu-item").Select(b => b.TextContent));
        Assert.True(cut.FindAll(".ex-pivot-menu-item")[1].HasAttribute("disabled"));
        await RunMenuAsync(cut, "Move to Row Labels");

        Assert.Equal(["Region", "Σ Values"], AreaEntries(cut, "Rows"));
        Assert.Empty(AreaEntries(cut, "Columns"));
    }

    [Fact] // ADR-0061: Excel's menu on a row field, a command that would change nothing disabled
    public async Task A_row_fields_menu()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });

        await OpenMenuAsync(cut, "Rows", "Region");

        var items = cut.FindAll(".ex-pivot-menu-item");
        Assert.Equal(
        [
            "Move Up", "Move Down", "Move to Beginning", "Move to End", "Move to Report Filter", "Move to Row Labels",
            "Move to Column Labels", "Move to Values", "Remove Field", "Sort A to Z", "Sort Z to A", "Filter…",
            "Expand Entire Field", "Collapse Entire Field", "Field Settings…",
        ], items.Select(b => b.TextContent));
        Assert.Equal(
            ["Move Up", "Move to Beginning", "Move to Row Labels", "Sort A to Z", "Expand Entire Field"],
            items.Where(b => b.HasAttribute("disabled")).Select(b => b.TextContent));
        Assert.Equal("menu", cut.Find(".ex-pivot-popup").GetAttribute("role"));
    }

    [Fact] // ADR-0061: Move to Column Labels moves the field with its settings
    public async Task Moving_a_field_with_its_menu()
    {
        var cut = RenderPivot(new PivotLayout
        {
            Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }, P("Product")],
            Values = [Sum("Amount")],
        });

        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Move to Column Labels");

        Assert.Equal(["Region"], AreaEntries(cut, "Columns"));
        Assert.Equal(["Row Labels", "East", "North", "(blank)", "Grand Total"], HeaderTexts(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0061: Escape closes the menu and changes nothing
    public async Task Escape_closes_the_menu()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")] });
        await OpenMenuAsync(cut, "Rows", "Region");

        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0061: one menu or panel at a time; opening another closes the first
    public async Task One_menu_at_a_time()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Columns = [P("Online")] });

        await OpenMenuAsync(cut, "Rows", "Region");
        await OpenMenuAsync(cut, "Columns", "Online");

        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.NotNull(AreaElement(cut, "Columns").QuerySelector(".ex-pivot-popup"));
    }

    [Theory] // ADR-0061 (PV-11): an outside event painted for an old menu cannot close its replacement
    [InlineData("onmousedown", false)]
    [InlineData("onfocusin", false)]
    [InlineData("onmousedown", true)]
    [InlineData("onfocusin", true)]
    public async Task A_late_dismissal_leaves_the_replacement_surface_open(string eventName, bool panel)
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        var oldDismissal = RenderedDismissal(cut, eventName);

        if (panel)
            await RunMenuAsync(cut, "Field Settings…");
        else
            await OpenMenuAsync(cut, "Rows", "Product");

        // The browser saw the old menu when it made this gesture. Deliver that rendered event
        // only after its replacement exists, without sleeps or assumptions about a round trip.
        EventArgs args = eventName == "onmousedown" ? new MouseEventArgs() : new FocusEventArgs();
        await cut.InvokeAsync(() => oldDismissal.InvokeAsync(args));

        var popup = Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(panel ? "Field Settings…" : "Options for Product", popup.GetAttribute("aria-label"));
    }

    // As in SurfaceWriteBackTests: the renderer's event binding is the browser-facing boundary.
    // bUnit normally dispatches against the latest render; keep the older binding here so the
    // test can deliver exactly the gesture a circuit had not received before the replacement.
#pragma warning disable BL0006
    private EventCallback RenderedDismissal(IRenderedComponent<global::ExPivot.Components.ExPivot> cut, string eventName)
    {
        var read = typeof(Renderer).GetMethod("GetCurrentRenderTreeFrames", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var frames = (ArrayRange<RenderTreeFrame>)read.Invoke(Renderer, [cut.ComponentId])!;
        var binding = frames.Array.Take(frames.Count).First(frame =>
            frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == eventName).AttributeValue;
        return binding is EventCallback callback ? callback : new EventCallback(cut.Instance, (MulticastDelegate)binding);
    }
#pragma warning restore BL0006

    [Fact] // ADR-0061: Hide Field List from the Context Menu, and back
    public async Task The_field_list_can_be_hidden_and_shown()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        var hide = ContextCommands(cut, 0, "row-labels").Single(c => c.Id == PivotCommandIds.HideFieldList);
        await cut.InvokeAsync(hide.Invoke);
        Assert.Empty(cut.FindAll(".ex-pivot-field-list"));

        var show = ContextCommands(cut, 0, "row-labels").Single(c => c.Id == PivotCommandIds.ShowFieldList);
        await cut.InvokeAsync(show.Invoke);
        Assert.Single(cut.FindAll(".ex-pivot-field-list"));
    }

    [Fact] // ADR-0061: ShowFieldList="false" leaves the pane out
    public void The_field_list_can_be_left_out()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")] }, ps => ps.Add(p => p.ShowFieldList, false));

        Assert.Empty(cut.FindAll(".ex-pivot-field-list"));
    }
}
