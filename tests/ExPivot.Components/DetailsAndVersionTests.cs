using Bunit;
using ExGrid.Components;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// Show Details — to a tab at the report's foot, a dialog, or the Consumer (ADR-0058/0062) — and the
/// Source Version a field's Items and a cell's records are asked under (ADR-0065).
/// </summary>
public class DetailsAndVersionTests : PivotTestContext
{
    private static readonly PivotLayout ByRegionAndProduct = new() { Rows = [P("Region")], Columns = [P("Product")], Values = [Sum("Amount")] };

    private static Task DoubleClickAsync(IRenderedComponent<PivotComponent> cut, int row, int column)
        => cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(row, column)));

    /// <summary>The details grid's painted rows. It stretches into the box it stands in, so it
    /// paints once the browser has said how big that box is; here the test says it.</summary>
    private static string[] DetailRows(IRenderedComponent<PivotComponent> cut, string container)
    {
        foreach (var grid in cut.FindComponents<ExGrid<PivotDetailRecord>>())
            cut.InvokeAsync(() => grid.Instance.OnViewportReportAsync(0, 0, 800, 300)).GetAwaiter().GetResult();
        return RowTextsOf(cut.FindAll($"{container} .ex-grid .ex-viewport .ex-row"));
    }

    // ---- PV-14: a tab at the report's foot, by default ------------------------------------------

    [Fact] // ADR-0058 (PV-14): Show Details opens a tab at the report's foot, titled by the cell, holding an ExGrid of the source's fields
    public async Task Show_details_opens_a_tab_at_the_reports_foot()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.LayoutChanged, told.Add));
        var layout = cut.Instance.CurrentLayout;

        await DoubleClickAsync(cut, 0, 1);

        var tabs = cut.Find(".ex-pivot-tabs");
        Assert.Equal("tablist", tabs.GetAttribute("role"));
        Assert.Equal(["PivotTable", "Details: East / Apples"], cut.FindAll(".ex-pivot-tab-button").Select(t => t.TextContent));
        var tab = cut.FindAll(".ex-pivot-tab-button")[1];
        Assert.Equal("true", tab.GetAttribute("aria-selected"));
        Assert.Equal("false", cut.FindAll(".ex-pivot-tab-button")[0].GetAttribute("aria-selected"));
        var panel = cut.Find(".ex-pivot-details-panel");
        Assert.Equal("tabpanel", panel.GetAttribute("role"));
        Assert.Equal(tab.Id, panel.GetAttribute("aria-labelledby"));
        Assert.Equal(tab.Id + "-panel", panel.Id);
        cut.WaitForAssertion(() => Assert.Equal(
            ["East | Apples | 100 | 10 | TRUE", "East | Apples | 30 | 3 | TRUE"], DetailRows(cut, ".ex-pivot-details-panel")));
        Assert.Equal(["Region", "Product", "Amount", "Quantity", "Online"],
            cut.FindAll(".ex-pivot-details-panel .ex-header [role=columnheader]").Select(h => h.TextContent.Trim()));
        // Not part of the Pivot Layout.
        Assert.Same(layout, cut.Instance.CurrentLayout);
        Assert.Empty(told);
    }

    [Fact] // ADR-0058/0025 (PV-14): the tab's records are fetched in pages from the source, under the report's Source Version
    public async Task The_tabs_records_are_paged_under_the_reports_version()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(ByRegionAndProduct, source: source);

        await DoubleClickAsync(cut, 0, 1);

        cut.WaitForAssertion(() => Assert.NotEmpty(source.DetailQueries));
        var query = source.DetailQueries[0];
        Assert.Equal(cut.Instance.Report!.Cube.SourceVersion, query.SourceVersion);
        Assert.Equal(0, query.Start);
        Assert.Equal(ExGrid.FetchingGridSource<PivotDetailRecord>.DefaultPageRows, query.Count);
        Assert.Equal([new PivotFieldItem("Region", PivotItemKey.Text("East"))], query.RowItems);
        Assert.Equal([new PivotFieldItem("Product", PivotItemKey.Text("Apples"))], query.ColumnItems);
    }

    [Fact] // ADR-0058 (PV-14): a tab is closable, and the report's tab brings the report back; several tabs stand side by side
    public async Task Tabs_are_closable()
    {
        var cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);
        await DoubleClickAsync(cut, 1, 4);
        Assert.Equal(["PivotTable", "Details: East / Apples", "Details: North"],
            cut.FindAll(".ex-pivot-tab-button").Select(t => t.TextContent));

        await cut.FindAll(".ex-pivot-tab-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll(".ex-pivot-details-panel"));
        Assert.Equal("true", cut.FindAll(".ex-pivot-tab-button")[0].GetAttribute("aria-selected"));

        await cut.FindAll(".ex-pivot-tab-button")[1].ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindAll(".ex-pivot-details-panel"));
        Assert.Equal("Close Details: East / Apples", cut.FindAll(".ex-pivot-tab-close")[0].GetAttribute("aria-label"));
        await cut.FindAll(".ex-pivot-tab-close")[0].ClickAsync(new MouseEventArgs());
        Assert.Equal(["PivotTable", "Details: North"], cut.FindAll(".ex-pivot-tab-button").Select(t => t.TextContent));
        Assert.Equal("true", cut.FindAll(".ex-pivot-tab-button")[1].GetAttribute("aria-selected"));

        await cut.Find(".ex-pivot-tab-close").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
        Assert.Empty(cut.FindAll(".ex-pivot-details-panel"));
        Assert.Single(cut.FindComponents<ExGrid<PivotReportRow>>());
    }

    [Fact] // ADR-0058 (PV-14): while a details tab is selected its records cover the report, which stays with its state and is marked covered — left unpainted, so its own header cannot stand over the records' — until the report's tab brings it back
    public async Task The_records_cover_the_report()
    {
        var cut = RenderPivot(ByRegionAndProduct);
        var grid = Grid(cut).Instance;
        Assert.Equal("ex-pivot-sheet", cut.Find(".ex-pivot-sheet").ClassName);

        await DoubleClickAsync(cut, 0, 1);

        Assert.Contains("ex-pivot-sheet-covered", cut.Find(".ex-pivot-sheet").ClassList);
        Assert.NotNull(cut.Find(".ex-pivot-sheet-covered > .ex-grid"));
        Assert.Same(grid, Grid(cut).Instance);

        await cut.FindAll(".ex-pivot-tab-button")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal("ex-pivot-sheet", cut.Find(".ex-pivot-sheet").ClassName);
        Assert.Same(grid, Grid(cut).Instance);
    }

    [Fact] // ADR-0058 (PV-14): a tab Show Details opens takes the keyboard, which the report it covers keeps no longer; closing a tab gives it to the tab selected next
    public async Task The_keyboard_follows_the_tabs()
    {
        var cut = RenderPivot(ByRegionAndProduct);
        int FocusCalls() => JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");
        IRenderedComponent<PivotFocusButton> TabButton(string title)
            => cut.FindComponents<PivotFocusButton>().Single(b => b.Instance.Class == "ex-pivot-tab-button" && b.Find("button").TextContent == title);

        var before = FocusCalls();
        await DoubleClickAsync(cut, 0, 1);

        Assert.NotEqual(0, TabButton("Details: East / Apples").Instance.FocusRequest);
        Assert.Equal(0, TabButton("PivotTable").Instance.FocusRequest);
        Assert.True(FocusCalls() > before);

        await DoubleClickAsync(cut, 1, 4);
        var opened = TabButton("Details: North").Instance.FocusRequest;
        Assert.NotEqual(0, opened);
        Assert.Equal(0, TabButton("Details: East / Apples").Instance.FocusRequest);

        before = FocusCalls();
        await cut.FindAll(".ex-pivot-tab-close")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("true", TabButton("Details: East / Apples").Find("button").GetAttribute("aria-selected"));
        Assert.True(TabButton("Details: East / Apples").Instance.FocusRequest > opened);
        Assert.True(FocusCalls() > before);

        await cut.Find(".ex-pivot-tab-close").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
    }

    [Fact] // ADR-0058 (PV-14): the grand total's records, every one, under the title of the grand total
    public async Task The_grand_totals_details()
    {
        var cut = RenderPivot(ByRegionAndProduct);

        await DoubleClickAsync(cut, 4, 4);

        Assert.Equal("Details: Grand Total", cut.FindAll(".ex-pivot-tab-button")[1].TextContent);
        cut.WaitForAssertion(() => Assert.Equal(7, DetailRows(cut, ".ex-pivot-details-panel").Length));
    }

    [Fact] // ADR-0062 (PV-14): an empty cell shows nothing — no tab opens
    public async Task An_empty_cell_opens_no_tab()
    {
        var cut = RenderPivot(ByRegionAndProduct);

        await DoubleClickAsync(cut, 0, 3);

        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
    }

    [Fact] // ADR-0058/0065 (PV-14, PV-23): a tab whose Source Version the source can no longer answer under says the data has changed
    public async Task A_tab_whose_version_is_refused_says_the_data_has_changed()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true, RefusesVersions = true };
        var cut = RenderPivot(ByRegionAndProduct, source: source);

        await DoubleClickAsync(cut, 0, 1);

        cut.WaitForAssertion(() => Assert.Equal("The data has changed — refresh.", cut.Find(".ex-pivot-details-panel .ex-pivot-details-status").TextContent));
        Assert.Equal("alert", cut.Find(".ex-pivot-details-status").GetAttribute("role"));
        Assert.Empty(cut.FindAll(".ex-pivot-details-panel .ex-grid"));
    }

    [Fact] // ADR-0058/0065 (PV-14): the bundled source's tab keeps its version across a refresh, and so still adds up
    public async Task A_bundled_tab_survives_a_refresh()
    {
        var cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);

        cut.Render(ps => ps.Add(p => p.Source, Bundled(Sales.Select(s => s with { Amount = s.Amount * 2 }).ToArray())));

        Assert.Equal("Details: East / Apples", cut.FindAll(".ex-pivot-tab-button")[1].TextContent);
        cut.WaitForAssertion(() => Assert.Equal(
            ["East | Apples | 100 | 10 | TRUE", "East | Apples | 30 | 3 | TRUE"], DetailRows(cut, ".ex-pivot-details-panel")));
    }

    // ---- PV-14: a dialog, when the Consumer asks for one -------------------------------------------

    [Fact] // ADR-0058 (PV-14): with DetailsView Dialog, Show Details opens ExPivot's dialog, named by the cell, with the same grid inside, and takes the keyboard
    public async Task Show_details_opens_a_dialog_when_asked()
    {
        var cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.DetailsView, PivotDetailsView.Dialog));
        var focusCalls = JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");

        await DoubleClickAsync(cut, 0, 1);

        var dialog = cut.Find(".ex-pivot-dialog");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("Details: East / Apples", dialog.GetAttribute("aria-label"));
        Assert.Equal("Details: East / Apples", cut.Find(".ex-pivot-dialog-title").TextContent);
        Assert.Single(cut.FindAll(".ex-pivot-dialog-backdrop"));
        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
        cut.WaitForAssertion(() => Assert.Equal(2, DetailRows(cut, ".ex-pivot-dialog-records").Length));
        Assert.True(JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus") > focusCalls);
    }

    [Fact] // ADR-0058 (PV-14): the dialog is modal — what it covers, the report and the Field List, takes neither the keyboard nor the pointer while it stands
    public async Task What_the_dialog_covers_is_inert()
    {
        var cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.DetailsView, PivotDetailsView.Dialog));
        Assert.False(cut.Find(".ex-pivot-report").HasAttribute("inert"));

        await DoubleClickAsync(cut, 0, 1);

        Assert.True(cut.Find(".ex-pivot-report").HasAttribute("inert"));
        Assert.True(cut.Find(".ex-pivot-field-list").HasAttribute("inert"));
        Assert.Null(cut.Find(".ex-pivot-dialog").Closest("[inert]"));

        await cut.Find(".ex-pivot-dialog").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(cut.Find(".ex-pivot-report").HasAttribute("inert"));
        Assert.False(cut.Find(".ex-pivot-field-list").HasAttribute("inert"));
    }

    [Theory] // ADR-0058 (PV-14): Escape, Close and the backdrop close the dialog
    [InlineData("escape")]
    [InlineData("close")]
    [InlineData("backdrop")]
    public async Task The_dialog_closes(string how)
    {
        var cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.DetailsView, PivotDetailsView.Dialog));
        await DoubleClickAsync(cut, 0, 1);

        switch (how)
        {
            case "escape":
                await cut.Find(".ex-pivot-dialog").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
                break;
            case "close":
                await cut.Find(".ex-pivot-dialog .ex-pivot-close").ClickAsync(new MouseEventArgs());
                break;
            default:
                await cut.Find(".ex-pivot-dialog-backdrop").ClickAsync(new MouseEventArgs());
                break;
        }

        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
        Assert.Empty(cut.FindAll(".ex-pivot-dialog-backdrop"));
    }

    // ---- PV-14: the Consumer -----------------------------------------------------------------

    [Fact] // ADR-0058 (PV-14): when the Consumer listens to OnShowDetails it takes the records, and neither the tab nor the dialog opens
    public async Task A_consumer_that_listens_takes_the_records()
    {
        PivotDetails? taken = null;
        var cut = RenderPivot(ByRegionAndProduct, ps => ps
            .Add(p => p.DetailsView, PivotDetailsView.Dialog)
            .Add(p => p.OnShowDetails, (PivotDetails details) => taken = details));

        var showDetails = ContextCommands(cut, 0, Grid(cut).Instance.Columns[1].Name).Single(c => c.Id == PivotCommandIds.ShowDetails);
        await cut.InvokeAsync(showDetails.Invoke);

        Assert.NotNull(taken);
        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
        var page = await taken!.DetailsAsync(1, 1, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(1, page.Start);
        Assert.Equal(2, page.Total);
        Assert.Equal([Sales[2]], page.Records.Select(r => r.Record));
        Assert.Equal(["East", "Apples", 30m, 3m, true], page.Records[0].Values);
    }

    // ---- PV-23: the Source Version, the component's side -----------------------------------------

    [Fact] // ADR-0065 (PV-23): Filter… lists the Items the source gives under the report's Source Version
    public async Task Filter_lists_items_under_the_reports_version()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, source: source);

        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        var query = Assert.Single(source.ItemQueries);
        Assert.Equal("Region", query.Field);
        Assert.Equal(cut.Instance.Report!.Cube.SourceVersion, query.SourceVersion);
        Assert.Equal(PivotComponent.ItemListCap, query.Max);
        Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim()));
    }

    [Fact] // ADR-0065 (PV-23): a source that can no longer answer under the report's version refuses, and Filter… says the data has changed instead of listing
    public async Task Filter_says_the_data_has_changed_when_the_version_is_refused()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true, RefusesVersions = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, source: source);

        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        Assert.Equal("The data has changed — refresh.", cut.Find(".ex-pivot-unavailable").TextContent);
        Assert.Empty(cut.FindAll(".ex-pivot-item"));
        Assert.True(cut.Find(".ex-pivot-ok").HasAttribute("disabled"));
    }

    [Fact] // ADR-0065 (PV-23): while the Items are on their way Filter… says so, and lists them when they land
    public async Task Filter_waits_for_its_items()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true, HoldsItems = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, source: source);
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        Assert.Equal("Loading…", cut.Find(".ex-pivot-item-filter .ex-pivot-loading").TextContent);
        Assert.True(cut.Find(".ex-pivot-ok").HasAttribute("disabled"));

        var (query, completion) = Assert.Single(source.HeldItems);
        await cut.InvokeAsync(() => completion.SetResult(new PivotItemPage(query.SourceVersion, [PivotItemKey.Text("East"), PivotItemKey.Text("West")], 2)));
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "West"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim())));
    }

    [Fact] // ADR-0065 refined (PV-23): the search narrows the painted labels among the Items held, and asks the source nothing while every Item is held
    public async Task The_search_narrows_the_items_held()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, source: source);
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        await cut.Find(".ex-pivot-item-filter .ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "(BL" });

        Assert.Equal(["(Select All)", "(blank)"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim()));
        Assert.Single(source.ItemQueries);
    }

    [Fact] // ADR-0065 refined (PV-23): with more Items than Filter… lists, the typed search is asked of the source, under the report's version
    public async Task The_search_asks_the_source_beyond_the_list()
    {
        var many = Enumerable.Range(0, PivotComponent.ItemListCap + 1)
            .Select(i => new Sale($"R{i:D5}", "Apples", 1m, 1, true))
            .ToArray();
        var source = new OnDemandSource(Bundled(many)) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Filters = [P("Region")], Values = [Sum("Amount")] }, source: source);
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        Assert.Equal(PivotComponent.ItemListCap, cut.FindAll(".ex-pivot-item").Count - 1);
        Assert.Contains("More than 10,000 items.", cut.Find(".ex-pivot-item-filter .ex-pivot-note").TextContent);

        await cut.Find(".ex-pivot-item-filter .ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "r1000" });

        Assert.Equal(2, source.ItemQueries.Count);
        Assert.Equal("r1000", source.ItemQueries[1].Search);
        Assert.Equal(cut.Instance.Report!.Cube.SourceVersion, source.ItemQueries[1].SourceVersion);
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "R10000"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim())));
    }

    [Fact] // ADR-0065 (PV-23): an open Filter… lists again under the new report's Source Version when a refresh brings one, and stays open
    public async Task An_open_filter_lists_the_new_versions_items()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll(".ex-pivot-item").Count));
        var before = cut.Instance.Report!.Cube.SourceVersion;

        cut.Render(ps => ps.Add(p => p.Source, Bundled([.. Sales, new Sale("South", "Apples", 1m, 1, true)])));

        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "East", "North", "South", "West", "(blank)"],
            cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim())));
        Assert.NotEqual(before, cut.Instance.Report!.Cube.SourceVersion);
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0065 refined (PV-23): a search typed while Filter… waits for its Items is asked of the source once they land, when there are more Items than are listed
    public async Task A_search_typed_before_the_items_land_is_asked_when_they_do()
    {
        var many = Enumerable.Range(0, PivotComponent.ItemListCap + 1)
            .Select(i => new Sale($"R{i:D5}", "Apples", 1m, 1, true))
            .ToArray();
        var reference = Bundled(many);
        var source = new OnDemandSource(reference) { AnswersAtOnce = true, HoldsItems = true };
        var cut = RenderPivot(new PivotLayout { Filters = [P("Region")], Values = [Sum("Amount")] }, source: source);
        await cut.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        await cut.Find(".ex-pivot-item-filter .ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "r1000" });
        var (query, listing) = Assert.Single(source.HeldItems);

        var page = await reference.ItemsAsync(query, Xunit.TestContext.Current.CancellationToken);
        await cut.InvokeAsync(() => listing.SetResult(page));

        cut.WaitForAssertion(() => Assert.Equal(2, source.HeldItems.Count));
        var (search, found) = source.HeldItems[1];
        Assert.Equal("r1000", search.Search);
        Assert.Equal(query.SourceVersion, search.SourceVersion);
        var answer = await reference.ItemsAsync(search, Xunit.TestContext.Current.CancellationToken);
        await cut.InvokeAsync(() => found.SetResult(answer));
        cut.WaitForAssertion(() => Assert.Equal(["(Select All)", "R10000"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim())));
    }

    [Fact] // ADR-0065 (PV-23): the report filter band says the data has changed when the source refuses the version its summary needs
    public void The_band_says_the_data_has_changed()
    {
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true, RefusesVersions = true };

        var cut = RenderPivot(new PivotLayout { Filters = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }], Values = [Sum("Amount")] }, source: source);

        Assert.Equal("The data has changed — refresh.", cut.Find(".ex-pivot-filter-summary").TextContent);
    }
}
