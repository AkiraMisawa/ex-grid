using Bunit;
using ExGrid.Selection;
using ExPivot.Chrome;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// The new surfaces through a substituted Chrome (ADR-0061, PV-9): the Pivot Toolbar, the Layout
/// menu, Defer Layout Update, and Show Details' tabs and dialog are handed to the Chrome as
/// contexts, and its calls back make the layouts the built-in markup makes — swapping the Chrome
/// changes no behaviour.
/// </summary>
public class SubstitutedChromeTests : PivotTestContext
{
    private static readonly PivotLayout RegionProduct = new()
    {
        Filters = [P("Online")],
        Rows = [P("Region"), P("Product")],
        Values = [Sum("Amount")],
    };

    [Fact] // ADR-0061 (PV-9/PV-30): the Pivot Toolbar is the Chrome's to draw — handed the band drawn, Layout ▾, no Refresh, and the toggle
    public void The_toolbar_is_handed_to_the_chrome()
    {
        var chrome = new StubChrome();

        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.PivotChrome, chrome));

        Assert.Single(cut.FindAll(".stub-toolbar"));
        Assert.Empty(cut.FindAll(".ex-pivot-toolbar"));
        var toolbar = chrome.Toolbar!;
        Assert.NotNull(toolbar.ReportFilters);
        Assert.Equal(PivotCommandIds.LayoutMenu, toolbar.LayoutMenu.Id);
        Assert.Equal("Layout", toolbar.LayoutMenu.Label);
        Assert.False(toolbar.LayoutMenu.IsOpen);
        Assert.Null(toolbar.Refresh);
        Assert.Equal(PivotCommandIds.FieldListToggle, toolbar.FieldList.Id);
        Assert.True(toolbar.FieldList.Checked);
        Assert.Null(toolbar.Refusal);
    }

    [Fact] // ADR-0061 (PV-9/PV-30): the Layout menu reaches the Chrome's menu surface, grouped and marked, and a choice makes the built-in's layout
    public async Task The_layout_menu_through_the_chrome()
    {
        var chrome = new StubChrome();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.PivotChrome, chrome));

        await cut.InvokeAsync(chrome.Toolbar!.LayoutMenu.Open);

        Assert.True(chrome.Toolbar!.LayoutMenu.IsOpen);
        Assert.NotNull(chrome.Toolbar.LayoutMenu.Popup);
        var menu = chrome.Menu!;
        Assert.Equal("Layout", menu.Title);
        Assert.Equal(["Subtotals", "Grand Totals", "Report Layout"], menu.Commands.Select(c => c.GroupHeading).OfType<string>());
        Assert.All(menu.Commands, c => Assert.NotNull(c.Checked));
        Assert.Equal(
            [PivotCommandIds.LayoutChoice(PivotLayoutChoice.ShowSubtotalsAtTop), PivotCommandIds.LayoutChoice(PivotLayoutChoice.GrandTotalsOn),
             PivotCommandIds.LayoutChoice(PivotLayoutChoice.CompactForm)],
            menu.Commands.Where(c => c.Checked == true).Select(c => c.Id));

        await cut.InvokeAsync(menu.Commands.Single(c => c.Id == PivotCommandIds.LayoutChoice(PivotLayoutChoice.OutlineForm)).Invoke);

        Assert.Equal(PivotReportForm.Outline, cut.Instance.CurrentLayout.Form);
        Assert.False(chrome.Toolbar!.LayoutMenu.IsOpen);
        Assert.NotEqual(0, chrome.Toolbar.LayoutMenu.FocusRequest);
    }

    [Fact] // ADR-0061 (PV-9/PV-28): Defer Layout Update through the Chrome's pane makes the built-in's layout
    public async Task Defer_through_the_chrome()
    {
        var told = new List<PivotLayout>();
        var chrome = new StubChrome();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.PivotChrome, chrome).Add(p => p.LayoutChanged, told.Add));
        Assert.False(chrome.FieldList!.DeferLayoutUpdate);
        Assert.False(chrome.FieldList.CanUpdate);

        await cut.InvokeAsync(() => chrome.FieldList!.DeferLayoutUpdateChanged(true));
        await cut.InvokeAsync(chrome.FieldList!.Fields.Single(f => f.Name == "Quantity").Toggle);

        Assert.True(chrome.FieldList!.DeferLayoutUpdate);
        Assert.True(chrome.FieldList.CanUpdate);
        Assert.Equal(["Sum of Amount", "Sum of Quantity"], chrome.FieldList.Areas[3].Entries.Select(e => e.Caption));
        Assert.Empty(told);

        await cut.InvokeAsync(chrome.FieldList!.Update);

        Assert.Single(told);
        Assert.Equal(2, cut.Instance.CurrentLayout.Values.Count);
        Assert.False(chrome.FieldList!.CanUpdate);
    }

    [Fact] // ADR-0059 (PV-9/PV-14): the details tabs are the Chrome's to draw — the report's tab first, a closable tab per Show Details
    public async Task The_details_tabs_through_the_chrome()
    {
        var chrome = new StubChrome();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.PivotChrome, chrome));

        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(1, 1)));

        var tabs = chrome.Tabs!;
        Assert.Equal("Sheets", tabs.Title);
        Assert.Equal("PivotTable", tabs.Report.Title);
        Assert.Null(tabs.Report.Close);
        var tab = Assert.Single(tabs.Tabs);
        Assert.Equal("Details: East / Apples", tab.Title);
        Assert.True(tab.IsSelected);
        Assert.Equal("Close Details: East / Apples", tab.CloseLabel);
        Assert.Single(cut.FindAll(".stub-tabs"));
        Assert.Single(cut.FindAll(".ex-pivot-details-panel"));

        await cut.InvokeAsync(tabs.Report.Select);
        Assert.Empty(cut.FindAll(".ex-pivot-details-panel"));
        await cut.InvokeAsync(chrome.Tabs!.Tabs[0].Close!);
        Assert.Empty(cut.FindAll(".stub-tabs"));
    }

    [Fact] // ADR-0059 (PV-9/PV-14): the dialog's content is the Chrome's, inside ExPivot's frame, handed the title, the records and Close
    public async Task The_details_dialog_through_the_chrome()
    {
        var chrome = new StubChrome();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.PivotChrome, chrome).Add(p => p.DetailsView, PivotDetailsView.Dialog));

        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(1, 1)));

        var dialog = chrome.Dialog!;
        Assert.Equal("Details: East / Apples", dialog.Title);
        Assert.NotEqual(0, dialog.FocusRequest);
        Assert.Equal("Details: East / Apples", cut.Find(".ex-pivot-dialog").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll(".ex-pivot-dialog .stub-dialog"));

        await cut.InvokeAsync(dialog.Close);
        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
    }

    /// <summary>A Chrome that draws a stub for each new surface, places the popups and records,
    /// and keeps the contexts it was last handed.</summary>
    private sealed class StubChrome : IPivotChrome
    {
        public PivotToolbarContext? Toolbar { get; private set; }

        public PivotMenuContext? Menu { get; private set; }

        public PivotFieldListContext? FieldList { get; private set; }

        public PivotDetailsTabsContext? Tabs { get; private set; }

        public PivotDetailsDialogContext? Dialog { get; private set; }

        RenderFragment? IPivotChrome.Toolbar(PivotToolbarContext context)
        {
            Toolbar = context;
            return builder =>
            {
                builder.AddMarkupContent(0, "<div class='stub-toolbar'></div>");
                builder.AddContent(1, context.ReportFilters);
                builder.AddContent(2, context.LayoutMenu.Popup);
            };
        }

        RenderFragment? IPivotChrome.Menu(PivotMenuContext context)
        {
            Menu = context;
            return builder => builder.AddMarkupContent(0, "<div class='stub-menu'></div>");
        }

        RenderFragment? IPivotChrome.FieldList(PivotFieldListContext context)
        {
            FieldList = context;
            return builder => builder.AddMarkupContent(0, "<div class='stub-field-list'></div>");
        }

        RenderFragment? IPivotChrome.DetailsTabs(PivotDetailsTabsContext context)
        {
            Tabs = context;
            return builder => builder.AddMarkupContent(0, "<div class='stub-tabs'></div>");
        }

        RenderFragment? IPivotChrome.DetailsDialog(PivotDetailsDialogContext context)
        {
            Dialog = context;
            return builder =>
            {
                builder.AddMarkupContent(0, "<div class='stub-dialog'></div>");
                builder.AddContent(1, context.Records);
            };
        }
    }
}
