using System.Reflection;
using Bunit;
using ExPivot.Chrome;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

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

    [Fact] // ADR-0061: dropping on the report removes only the dragged placement
    public async Task Dropping_on_the_report_removes_only_the_dragged_placement()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount"), new("Region", PivotAggregation.Count)],
        }, ps => ps.Add(p => p.LayoutChanged, told.Add));

        await AreaElement(cut, "Rows").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());

        Assert.Empty(AreaEntries(cut, "Rows"));
        Assert.Equal(["Sum of Amount", "Count of Region"], AreaEntries(cut, "Values"));
        Assert.Empty(Assert.Single(told).Rows);
    }

    [Theory] // ADR-0061: a drag's entry index is meaningful only in the layout it began against
    [InlineData(".ex-pivot-sheet")]
    [InlineData(".ex-pivot-fields")]
    [InlineData(".ex-pivot-area")]
    public async Task A_drop_after_the_layout_changes_does_not_act_on_a_different_entry(string target)
    {
        var told = new List<PivotLayout>();
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };
        var cut = RenderPivot(layout, ps => ps.Add(p => p.LayoutChanged, told.Add));
        await AreaElement(cut, "Rows").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());

        cut.Render(ps => ps.Add(p => p.Layout, layout with { Rows = [P("Product"), P("Region")] }));
        await cut.Find(target).DropAsync(new DragEventArgs());

        Assert.Equal(["Product", "Region"], AreaEntries(cut, "Rows"));
        Assert.Empty(told);
    }

    [Theory] // ADR-0061: neither a late dragstart nor an old target can reinterpret entry indices
    [InlineData(true)]
    [InlineData(false)]
    public async Task Old_rendered_drag_callbacks_do_not_change_a_replacement_layout(bool lateStart)
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };
        var cut = RenderPivot(layout);
        var old = cut.FindComponent<PivotFieldListView>().Instance.Context;
        cut.Render(ps => ps.Add(p => p.Layout, layout with { Rows = [P("Product"), P("Region")] }));
        var current = cut.FindComponent<PivotFieldListView>().Instance.Context;

        await cut.InvokeAsync(() => (lateStart ? old : current).StartDrag(PivotDragSubject.FromArea(new(PivotArea.Rows, 0))));
        await cut.InvokeAsync(() => (lateStart ? current : old).Drop(PivotArea.Columns, 0));

        Assert.Equal(["Product", "Region"], AreaEntries(cut, "Rows"));
        Assert.Empty(AreaEntries(cut, "Columns"));
    }

    [Fact] // ADR-0061: a replacement source invalidates a drag even if the layout is unchanged
    public async Task A_drop_after_the_source_changes_does_nothing()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")] });
        await AreaElement(cut, "Rows").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        cut.Render(ps => ps.Add(p => p.Source, Bundled()));
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
    }

    [Theory] // ADR-0061: removal accepts a placed entry, never Σ Values, a list field or an external drag
    [InlineData("pseudo")]
    [InlineData("list")]
    [InlineData("external")]
    [InlineData("cancelled")]
    [InlineData("hidden")]
    public async Task Report_removal_ignores_drags_it_cannot_remove(string gesture)
    {
        var layout = new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Sum("Quantity")] };
        var cut = RenderPivot(layout);
        var context = cut.FindComponent<PivotFieldListView>().Instance.Context;
        if (gesture != "external")
        {
            var subject = gesture switch
            {
                "pseudo" => PivotDragSubject.FromArea(PivotEntry.ValuesPseudoField(PivotAxis.Columns)),
                "list" => PivotDragSubject.FromList("Product"),
                _ => PivotDragSubject.FromArea(new(PivotArea.Rows, 0)),
            };
            await cut.InvokeAsync(() => context.StartDrag(subject));
        }
        if (gesture == "cancelled")
            await cut.InvokeAsync(context.EndDrag);
        if (gesture == "hidden")
        {
            await cut.InvokeAsync(context.Close);
            await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());
        }

        Assert.Empty(cut.FindAll(".ex-pivot-report-drop-remove"));
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());
        Assert.Same(layout, cut.Instance.CurrentLayout);
    }

    [Fact] // ADR-0061: the empty report is also a removal target, including for a Value Field
    public async Task Removing_values_rows_and_filters_reaches_the_empty_report()
    {
        var cut = RenderPivot(new PivotLayout { Filters = [P("Online")], Rows = [P("Region")], Values = [Sum("Amount")] });
        await AreaElement(cut, "Values").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());
        await AreaElement(cut, "Rows").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());
        Assert.Single(cut.FindAll(".ex-pivot-empty"));
        await AreaElement(cut, "Filters").QuerySelector(".ex-pivot-entry")!.DragStartAsync(new DragEventArgs());
        await cut.Find(".ex-pivot-sheet").DropAsync(new DragEventArgs());
        Assert.True(cut.Instance.CurrentLayout.IsEmpty);
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

    [Fact] // ADR-0061 (decided with the user, 2026-10-13: keys typed as the keyboard goes back are held and handed on in order, ExPivot's menus and panels too): Escape in a menu that holds the keyboard leaves it standing, its commands doing nothing, until the keyboard has left it for its button; the keys typed in it meanwhile are the button's — Enter opens the menu again — and none runs a command
    public async Task Keys_typed_while_a_menu_closes_are_its_buttons()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Columns = [P("Online")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        var frame = cut.Find(".ex-pivot-popup");
        // The menu holds the keyboard: its first command took it.
        await frame.FocusInAsync(new FocusEventArgs());

        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        // The keyboard is on its way to the button, and the menu stands until it has left.
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        // Enter typed on the menu's command as the keyboard goes: held, and the command it presses does nothing.
        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        var command = cut.FindAll(".ex-pivot-popup .ex-pivot-menu-item").First(b => !b.HasAttribute("disabled"));
        var label = command.TextContent;
        await command.ClickAsync(new MouseEventArgs());
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
        Assert.Equal(["Online"], AreaEntries(cut, "Columns"));

        // The button has the keyboard: the menu goes, and the Enter is the button's: the menu opens again.
        await cut.Find(".ex-pivot-popup").FocusOutAsync(new FocusEventArgs());

        var reopened = cut.FindAll(".ex-pivot-popup");
        Assert.Single(reopened);
        Assert.Contains(label, reopened[0].TextContent);
        Assert.Equal(["Region"], AreaEntries(cut, "Rows"));
    }

    // The entry's button, found and pressed in the renderer's turn.
    private static Task OnEntryAsync(IRenderedComponent<PivotComponent> cut, string area, string caption, Func<AngleSharp.Dom.IElement, Task> act)
        => cut.InvokeAsync(() => act(AreaElement(cut, area).QuerySelectorAll(".ex-pivot-entry-button")
            .Single(b => b.QuerySelector(".ex-pivot-entry-caption")!.TextContent.Trim() == caption)));

    private static Task TypeOnEntryAsync(IRenderedComponent<PivotComponent> cut, string area, string caption, string key)
        => OnEntryAsync(cut, area, caption, b => b.KeyDownAsync(new KeyboardEventArgs { Key = key, Type = "keydown" }));

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): Enter typed on an entry while its menu opens is the menu's — it presses the menu's first command, and the entry's press it also makes is none
    public async Task Keys_typed_on_an_entry_while_its_menu_opens_are_the_menus()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")] });
        await OpenMenuAsync(cut, "Rows", "Region");

        // On a circuit the menu takes the keyboard a round trip after the press. Enter typed on the entry
        // meanwhile: held for the menu, and the browser's press of the entry it makes is not one.
        await TypeOnEntryAsync(cut, "Rows", "Region", "Enter");
        await OpenMenuAsync(cut, "Rows", "Region");
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(["Region", "Product"], AreaEntries(cut, "Rows"));

        // The menu's first command takes the keyboard: the Enter is its own, and presses Move Down.
        await cut.Find(".ex-pivot-popup").FocusInAsync(new FocusEventArgs());

        Assert.Equal(["Product", "Region"], AreaEntries(cut, "Rows"));
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): Escape then Enter typed on an entry while its menu opens close the menu once it holds the keyboard, and the Enter, the entry's again, opens it anew
    public async Task Escape_and_enter_typed_while_a_menu_opens_close_it_and_open_it_again()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await TypeOnEntryAsync(cut, "Rows", "Region", "Escape");
        await TypeOnEntryAsync(cut, "Rows", "Region", "Enter");
        await OpenMenuAsync(cut, "Rows", "Region");

        // The menu holds the keyboard: Escape closes it, and the Enter is typed as it closes.
        await cut.Find(".ex-pivot-popup").FocusInAsync(new FocusEventArgs());
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        // The keyboard leaves it for the entry: the menu goes, and the Enter presses the entry.
        await cut.Find(".ex-pivot-popup").FocusOutAsync(new FocusEventArgs());

        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(["Region", "Product"], AreaEntries(cut, "Rows"));
        // Opened again, the menu holds the keyboard and nothing is left for it.
        await cut.Find(".ex-pivot-popup").FocusInAsync(new FocusEventArgs());
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(["Region", "Product"], AreaEntries(cut, "Rows"));
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): Tab typed on an entry while its menu opens is the browser's, and is dropped with every key after it
    public async Task A_tab_typed_while_a_menu_opens_drops_the_keys_after_it()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await TypeOnEntryAsync(cut, "Rows", "Region", "Tab");
        await TypeOnEntryAsync(cut, "Rows", "Region", "Enter");
        await OpenMenuAsync(cut, "Rows", "Region");

        await cut.Find(".ex-pivot-popup").FocusInAsync(new FocusEventArgs());

        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(["Region", "Product"], AreaEntries(cut, "Rows"));
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): Enter typed on Layout ▾ while its menu opens presses the menu's first enabled choice
    public async Task Enter_typed_on_layout_while_its_menu_opens_is_the_menus()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] });
        var before = cut.Instance.CurrentLayout;
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-layout-button").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", Type = "keydown" }));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs()));
        Assert.Single(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
        Assert.Same(before, cut.Instance.CurrentLayout);

        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusInAsync(new FocusEventArgs());

        // Do Not Show Subtotals, the first choice that changes anything, was made.
        Assert.NotSame(before, cut.Instance.CurrentLayout);
        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusOutAsync(new FocusEventArgs());
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs()));
        Assert.Equal("true", MenuItem(cut, "Do Not Show Subtotals").GetAttribute("aria-checked"));
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): keys typed on a report filter's button while its Filter… opens are carried through the report's grid, which hands them to the search as it gives it the keyboard
    public async Task Keys_typed_on_a_report_filter_while_filter_opens_go_through_the_grid_to_its_search()
    {
        var handle = ReportGridHandle();
        var cut = RenderPivot(new PivotLayout { Filters = [P("Online")], Rows = [P("Region")], Values = [Sum("Amount")] });

        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs()));

        // The grid is told the keyboard is going to a frame of ExPivot's, and takes it from the button.
        Assert.Contains(handle.Invocations, i => i.Identifier == "handOff" && (string?)i.Arguments[0] == "frame");
        var takeBack = handle.Invocations.Single(i => i.Identifier == "takeKeyboardBack");
        // Enter and E typed on the button before the grid has the keyboard: held, and Enter's press is none.
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", Type = "keydown" }));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").KeyDownAsync(new KeyboardEventArgs { Key = "E", Type = "keydown" }));
        Assert.Single(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
        // The grid has the keyboard, and asks for them; it holds them for the search.
        var grid = cut.FindComponent<global::ExGrid.Components.ExGrid<PivotDisplayRow>>();
        IReadOnlyList<KeyboardEventArgs> typed = [];
        await cut.InvokeAsync(() => typed = grid.Instance.KeysTypedOnTheWay((int)takeBack.Arguments[1]!));
        Assert.Equal(["Enter", "E"], typed.Select(k => k.Key));

        // The search takes the keyboard from the grid, which types them into it in turn.
        var search = ElementIdOf(cut.FindComponent<PivotItemFilterView>().Instance);
        Assert.Contains(search, KeyboardHandOffs(handle));
        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusInAsync(new FocusEventArgs());
        Assert.Single(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): with no report grid to carry them, a field's key typed while Filter… opens is dropped with every key after it; Escape before it closes the panel
    public async Task With_no_report_a_report_filters_panel_answers_escape_and_drops_what_only_its_field_can_type()
    {
        var cut = RenderPivot(new PivotLayout { Filters = [P("Online")] });
        Assert.Empty(cut.FindComponents<global::ExGrid.Components.ExGrid<PivotDisplayRow>>());

        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").KeyDownAsync(new KeyboardEventArgs { Key = "x", Type = "keydown" }));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").KeyDownAsync(new KeyboardEventArgs { Key = "Escape", Type = "keydown" }));
        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusInAsync(new FocusEventArgs());
        Assert.Single(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
        Assert.Equal("", cut.FindComponent<PivotItemFilterView>().Instance.Context.Search);

        // A press on the button takes the keyboard out of the panel first, and then closes it at once.
        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusOutAsync(new FocusEventArgs());
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs()));
        Assert.Empty(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => cut.Find(".ex-pivot-filter-button").KeyDownAsync(new KeyboardEventArgs { Key = "Escape", Type = "keydown" }));
        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusInAsync(new FocusEventArgs());
        // Escape closed it: it stands until the keyboard has left it for the button.
        await cut.Find(".ex-pivot-toolbar .ex-pivot-popup").FocusOutAsync(new FocusEventArgs());
        Assert.Empty(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
    }

    [Fact] // ADR-0061 (decided with the user, 2026-10-13): a menu closed while the keyboard is not in it — its own button pressed again — goes at once, holding nothing
    public async Task A_menu_closed_from_its_button_goes_at_once()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")] });
        await OpenMenuAsync(cut, "Rows", "Region");

        await OpenMenuAsync(cut, "Rows", "Region");

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
