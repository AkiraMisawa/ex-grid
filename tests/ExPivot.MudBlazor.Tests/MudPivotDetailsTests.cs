using AngleSharp.Dom;
using Bunit;
using ExGrid.Components;
using ExGrid.MudBlazor;
using ExGrid.Selection;
using ExPivot.Components;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Extensions;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// Show Details under MudBlazor (ADR-0058/0061, PV-14). The tabs at the report's foot are
/// <c>MudTabs</c> placed at the bottom, bound to the tab ExPivot selects — never holding a selection
/// of their own — each closable in ExPivot's words, the keyboard handed on as ExPivot asks. The
/// dialog is ExPivot's frame with MudBlazor's controls inside, never a <c>MudDialog</c>. The same
/// gestures leave the same tabs, selection and layout as under the built-in markup (PV-9).
/// </summary>
public class MudPivotDetailsTests : MudPivotTestContext
{
    //  Row 0 East, 1 Apples, 2 Pears, 3 North, 4 Pears, 5 West, 6 Apples, 7 Plums, 8 (blank),
    //  9 Plums, 10 Grand Total; column 1 the Sum of Amount.
    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static Task DoubleClickAsync(IRenderedComponent<PivotComponent> cut, int row, int column = 1)
        => cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(row, column)));

    private static IReadOnlyList<IElement> TabElements(IRenderedComponent<PivotComponent> cut) => cut.FindAll(".mud-ex-pivot-tabs [role=tab]");

    private static string[] TabTitles(IRenderedComponent<PivotComponent> cut) => TabElements(cut).Select(t => t.TextContent.Trim()).ToArray();

    private static string? SelectedTitle(IRenderedComponent<PivotComponent> cut)
        => TabElements(cut).SingleOrDefault(t => t.GetAttribute("aria-selected") == "true")?.TextContent.Trim();

    private static IElement Tab(IRenderedComponent<PivotComponent> cut, string title) => TabElements(cut).Single(t => t.TextContent.Trim() == title);

    private static Task CloseAsync(IRenderedComponent<PivotComponent> cut, string title)
        => cut.FindAll(".mud-ex-pivot-tab-close").Single(b => b.GetAttribute("aria-label") == $"Close {title}").ClickAsync(new MouseEventArgs());

    private static MudTabs Tabs(IRenderedComponent<PivotComponent> cut) => cut.FindComponent<MudTabs>().Instance;

    /// <summary>The id of the element MudTabs draws for the tab titled <paramref name="title"/> —
    /// what a focus call names when the tab takes the keyboard.</summary>
    private static string TabElementId(IRenderedComponent<PivotComponent> cut, string title)
        => Tabs(cut).Panels[Array.IndexOf(TabTitles(cut), title)].PanelRef.Id;

    /// <summary>The title of the tab whose records ExPivot shows over the report, read from the
    /// panel's label; null while the report shows.</summary>
    private static string? ShownRecords(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-pivot-details-panel[role=tabpanel]").SingleOrDefault() is { } panel
            ? cut.Find($"#{panel.GetAttribute("aria-labelledby")}").TextContent
            : null;

    // ---- The tabs ---------------------------------------------------------------------------

    [Fact] // ADR-0058/0061 (PV-14): the tabs are MudTabs placed at the bottom, at the report's foot — the report's tab first, a closable one per Show Details named by its cell — and the records, ExPivot's, stand over the report labelled by the tab
    public async Task The_tabs_are_mud_tabs_at_the_reports_foot()
    {
        var cut = RenderPivot(RegionProduct);

        await DoubleClickAsync(cut, 1);

        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
        Assert.Equal(Position.Bottom, Tabs(cut).Position);
        var report = cut.Find(".ex-pivot-report").Children.Select(c => c.ClassName ?? "").ToList();
        Assert.True(report.FindIndex(c => c.Contains("ex-pivot-sheet", StringComparison.Ordinal))
            < report.FindIndex(c => c.Contains("mud-ex-pivot-tabs", StringComparison.Ordinal)));
        Assert.Equal("Sheets", cut.Find(".mud-ex-pivot-tabs").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll(".mud-ex-pivot-tabs [role=tablist]"));
        Assert.Equal(["PivotTable", "Details: East / Apples"], TabTitles(cut));
        Assert.Equal("Details: East / Apples", SelectedTitle(cut));
        Assert.Equal(1, Tabs(cut).GetState(x => x.ActivePanelIndex));
        // Each Show Details tab closes, in ExPivot's words, by a button beside it, not inside it.
        var close = Assert.Single(cut.FindAll(".mud-ex-pivot-tab-close"));
        Assert.Equal("Close Details: East / Apples", close.GetAttribute("aria-label"));
        Assert.Null(close.Closest("[role=tab]"));
        Assert.NotNull(close.Closest("[role=tablist]"));
        // ExPivot's records, labelled by the tab's title; MudTabs' own panel stays hidden.
        Assert.Equal("Details: East / Apples", ShownRecords(cut));
        var label = cut.Find($"#{cut.Find(".ex-pivot-details-panel").GetAttribute("aria-labelledby")}");
        Assert.Equal(Tab(cut, "Details: East / Apples").Id, label.Closest("[role=tab]")?.Id);
        Assert.All(cut.FindAll(".mud-ex-pivot-tabs [role=tabpanel]"), panel => Assert.True(panel.HasAttribute("hidden")));
        var details = cut.FindComponent<ExGrid<PivotDetailRecord>>();
        Assert.Same(Grid(cut).Instance.Chrome, details.Instance.Chrome);
        Assert.IsType<MudGridChrome>(details.Instance.Chrome);
    }

    [Fact] // ADR-0058 (PV-14): a tab Show Details opens takes the keyboard — the report it covers keeps it no longer
    public async Task A_new_tab_takes_the_keyboard()
    {
        var cut = RenderPivot(RegionProduct);

        await DoubleClickAsync(cut, 1);
        Assert.Equal((TabElementId(cut, "Details: East / Apples"), false), LastFocus());

        await DoubleClickAsync(cut, 2);
        Assert.Equal("Details: East / Pears", SelectedTitle(cut));
        Assert.Equal((TabElementId(cut, "Details: East / Pears"), false), LastFocus());
    }

    [Fact] // ADR-0058/0060 (PV-9, PV-14): which tab is selected is ExPivot's — a tab activated by the pointer or the keyboard is selected through ExPivot, which shows its records or brings the report back
    public async Task Selecting_a_tab_goes_through_expivot()
    {
        var cut = RenderPivot(RegionProduct);
        await DoubleClickAsync(cut, 1);

        await Tab(cut, "PivotTable").ClickAsync(new MouseEventArgs());

        Assert.Null(ShownRecords(cut));
        Assert.Equal("PivotTable", SelectedTitle(cut));
        Assert.Equal(0, Tabs(cut).GetState(x => x.ActivePanelIndex));
        Assert.Empty(cut.FindAll(".ex-pivot-details-panel"));

        await Tab(cut, "Details: East / Apples").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Details: East / Apples", ShownRecords(cut));
        Assert.Equal("Details: East / Apples", SelectedTitle(cut));
        Assert.Equal(1, Tabs(cut).GetState(x => x.ActivePanelIndex));
    }

    [Fact] // ADR-0058 (PV-14): closing a tab before the selected one leaves the selection where ExPivot keeps it, though every index after the closed one moves — MudTabs holds no index of its own — and the selected tab takes the keyboard
    public async Task Closing_a_tab_before_the_selected_one_keeps_the_selection()
    {
        var cut = RenderPivot(RegionProduct);
        await DoubleClickAsync(cut, 1);
        await DoubleClickAsync(cut, 2);
        await DoubleClickAsync(cut, 4);
        Assert.Equal("Details: North / Pears", SelectedTitle(cut));

        await CloseAsync(cut, "Details: East / Apples");

        Assert.Equal(["PivotTable", "Details: East / Pears", "Details: North / Pears"], TabTitles(cut));
        Assert.Equal("Details: North / Pears", SelectedTitle(cut));
        Assert.Equal(2, Tabs(cut).GetState(x => x.ActivePanelIndex));
        Assert.Equal("Details: North / Pears", ShownRecords(cut));
        Assert.Equal((TabElementId(cut, "Details: North / Pears"), false), LastFocus());
    }

    [Fact] // ADR-0058 (PV-14): closing the selected tab selects Excel's next sheet — the one after it, else the one before, else the report — which takes the keyboard; the last one closed takes the tabs away
    public async Task Closing_the_selected_tab_selects_the_next()
    {
        var cut = RenderPivot(RegionProduct);
        await DoubleClickAsync(cut, 1);
        await DoubleClickAsync(cut, 2);
        await Tab(cut, "Details: East / Apples").ClickAsync(new MouseEventArgs());

        await CloseAsync(cut, "Details: East / Apples");

        Assert.Equal(["PivotTable", "Details: East / Pears"], TabTitles(cut));
        Assert.Equal("Details: East / Pears", SelectedTitle(cut));
        Assert.Equal("Details: East / Pears", ShownRecords(cut));
        Assert.Equal((TabElementId(cut, "Details: East / Pears"), false), LastFocus());

        await CloseAsync(cut, "Details: East / Pears");

        Assert.Empty(cut.FindAll(".mud-ex-pivot-tabs"));
        Assert.Empty(cut.FindComponents<MudTabs>());
        Assert.Null(ShownRecords(cut));
        Assert.Single(cut.FindComponents<ExGrid<PivotReportRow>>());
    }

    [Fact] // ADR-0060/0061 (PV-9, PV-14): the same Show Details and tab gestures leave the same tabs, the same selection and the same layout under the built-in markup and MudPivotChrome
    public async Task The_same_tab_gestures_leave_the_same_state_under_either_chrome()
    {
        var mud = RenderPivot(RegionProduct);
        var plain = RenderPivot(RegionProduct, chrome: BuiltIn);
        var plainTabs = () => plain.FindAll(".ex-pivot-tab-button").Select(t => t.TextContent.Trim()).ToArray();
        var plainSelected = () => plain.FindAll(".ex-pivot-tab-button").Single(t => t.GetAttribute("aria-selected") == "true").TextContent.Trim();

        foreach (var row in new[] { 1, 2, 4 })
        {
            await DoubleClickAsync(mud, row);
            await DoubleClickAsync(plain, row);
        }
        await CloseAsync(mud, "Details: East / Apples");
        await plain.FindAll(".ex-pivot-tab-close").Single(b => b.GetAttribute("aria-label") == "Close Details: East / Apples").ClickAsync(new MouseEventArgs());
        Assert.Equal(plainTabs(), TabTitles(mud));
        Assert.Equal(plainSelected(), SelectedTitle(mud));

        await Tab(mud, "PivotTable").ClickAsync(new MouseEventArgs());
        await plain.FindAll(".ex-pivot-tab-button")[0].ClickAsync(new MouseEventArgs());
        Assert.Equal(plainSelected(), SelectedTitle(mud));
        Assert.Empty(mud.FindAll(".ex-pivot-details-panel"));
        Assert.Empty(plain.FindAll(".ex-pivot-details-panel"));

        await Tab(mud, "Details: East / Pears").ClickAsync(new MouseEventArgs());
        await plain.FindAll(".ex-pivot-tab-button").Single(t => t.TextContent.Trim() == "Details: East / Pears").ClickAsync(new MouseEventArgs());
        await CloseAsync(mud, "Details: East / Pears");
        await plain.FindAll(".ex-pivot-tab-close").Single(b => b.GetAttribute("aria-label") == "Close Details: East / Pears").ClickAsync(new MouseEventArgs());

        Assert.Equal(plainTabs(), TabTitles(mud));
        Assert.Equal(plainSelected(), SelectedTitle(mud));
        Assert.Equal("Details: North / Pears", ShownRecords(mud));
        Assert.Equal(PivotLayoutJson.Write(plain.Instance.CurrentLayout), PivotLayoutJson.Write(mud.Instance.CurrentLayout));
        Assert.Same(RegionProduct, mud.Instance.CurrentLayout);
    }

    // ---- The dialog ---------------------------------------------------------------------------

    [Fact] // ADR-0058/0061 (PV-14): the dialog is ExPivot's frame — modal, named by the cell — with MudBlazor's controls inside: the title a heading, the records dressed by the grid Wrapper, and Close a MudButton that takes the keyboard; never a MudDialog
    public async Task The_dialog_is_expivots_frame_with_mudblazor_controls_inside()
    {
        var cut = RenderPivot(RegionProduct, detailsView: PivotDetailsView.Dialog);

        await DoubleClickAsync(cut, 1);

        var dialog = cut.Find(".ex-pivot > .ex-pivot-dialog[role=dialog]");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("Details: East / Apples", dialog.GetAttribute("aria-label"));
        Assert.NotNull(dialog.QuerySelector(".mud-ex-pivot-dialog-content"));
        Assert.Empty(cut.FindAll(".ex-pivot-dialog-content"));
        Assert.Empty(cut.FindComponents<MudDialog>());
        var heading = cut.FindComponents<MudText>().Single(t => t.Instance.Class == "mud-ex-pivot-dialog-title");
        Assert.Equal(Typo.h6, heading.Instance.Typo);
        Assert.Equal("Details: East / Apples", heading.Find("h6").TextContent.Trim());
        Assert.NotNull(dialog.QuerySelector(".mud-ex-pivot-dialog-records .ex-grid"));
        Assert.IsType<MudGridChrome>(cut.FindComponent<ExGrid<PivotDetailRecord>>().Instance.Chrome);
        var close = cut.FindComponents<MudPivotButton>().Single(b => b.Instance.Class == "mud-ex-pivot-dialog-close");
        Assert.Equal("Close", close.Find("button").TextContent.Trim());
        Assert.Equal((ElementIdOf(close.Instance), false), LastFocus());
        Assert.True(cut.Find(".ex-pivot-report").HasAttribute("inert"));
        Assert.Empty(cut.FindAll(".mud-ex-pivot-tabs"));
    }

    [Theory] // ADR-0058/0060 (PV-9, PV-14): Escape, Close and the backdrop close the dialog under MudBlazor, as under the built-in markup
    [InlineData("escape")]
    [InlineData("close")]
    [InlineData("backdrop")]
    public async Task The_dialog_closes(string how)
    {
        var cut = RenderPivot(RegionProduct, detailsView: PivotDetailsView.Dialog);
        await DoubleClickAsync(cut, 1);

        switch (how)
        {
            case "escape":
                await cut.Find(".mud-ex-pivot-dialog-close").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
                break;
            case "close":
                await cut.Find(".mud-ex-pivot-dialog-close").ClickAsync(new MouseEventArgs());
                break;
            default:
                await cut.Find(".ex-pivot-dialog-backdrop").ClickAsync(new MouseEventArgs());
                break;
        }

        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
        Assert.Empty(cut.FindAll(".ex-pivot-dialog-backdrop"));
        Assert.False(cut.Find(".ex-pivot-report").HasAttribute("inert"));
    }
}
