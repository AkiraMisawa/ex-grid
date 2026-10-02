using Bunit;
using ExGrid.Components;
using ExGrid.Selection;
using ExPivot.Components;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// Where the keyboard goes when Show Details' dialog or a details tab goes away, and what Escape does
/// inside their grids (ADR-0070, PV-39), under <c>MudPivotChrome</c>: as under ExPivot's own markup,
/// because swapping the Chrome changes no behaviour (ADR-0061). The dialog's grid — dressed by the
/// grid Wrapper — closes the dialog on the Escape it has nothing left to dismiss; however the dialog
/// closes, the report's grid takes the keyboard back; the last details tab closed leaves it with the
/// report's grid; a tab closed while selected hands it to the tab MudTabs shows next; and Escape in a
/// details tab's grid closes nothing.
/// </summary>
public class MudPivotKeyboardBackTests : MudPivotTestContext
{
    //  Row 0 East, 1 Apples, 2 Pears, 3 North, 4 Pears, 5 West, 6 Apples, 7 Plums, 8 (blank),
    //  9 Plums, 10 Grand Total; column 1 the Sum of Amount.
    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static Task DoubleClickAsync(IRenderedComponent<PivotComponent> cut, int row, int column = 1)
        => cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(row, column)));

    private static IRenderedComponent<ExGrid<PivotDetailRecord>> RecordsGrid(IRenderedComponent<PivotComponent> cut)
        => cut.FindComponent<ExGrid<PivotDetailRecord>>();

    private static Task EscapeAsync(IRenderedComponent<ExGrid<PivotDetailRecord>> grid)
        => grid.InvokeAsync(() => grid.Instance.OnKeyAsync("Escape", ctrl: false, shift: false, alt: false, meta: false, metaIsPrimary: false));

    private static string[] TabTitles(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".mud-ex-pivot-tabs [role=tab]").Select(t => t.TextContent.Trim()).ToArray();

    private static Task CloseTabAsync(IRenderedComponent<PivotComponent> cut, string title)
        => cut.FindAll(".mud-ex-pivot-tab-close").Single(b => b.GetAttribute("aria-label") == $"Close {title}").ClickAsync(new MouseEventArgs());

    private int TabReleases() => JSInterop.Invocations.Count(invocation => invocation.Identifier == "releaseTab");

    private int FocusCalls() => JSInterop.Invocations.Count(invocation => invocation.Identifier == Focus);

    /// <summary>What stood on screen each time the report's grid asked for the keyboard back: whether
    /// the dialog was gone, and the report neither inert nor covered by a tab's records.</summary>
    private static List<bool> WatchReturns(BunitJSModuleInterop report, Func<IRenderedComponent<PivotComponent>> cut)
    {
        var seen = new List<bool>();
        report.SetupVoid(invocation =>
        {
            if (invocation.Identifier == "reclaimFocus")
            {
                var pivot = cut();
                seen.Add(pivot.FindAll(".ex-pivot-dialog").Count == 0
                    && !pivot.Find(".ex-pivot-report").HasAttribute("inert")
                    && pivot.FindAll(".ex-pivot-sheet-covered").Count == 0);
            }
            return false;
        });
        return seen;
    }

    [Fact] // ADR-0070/0062 (PV-39): under MudBlazor, the dialog's grid declares OnLeave, the dialog closes on the Escape that grid has nothing left to dismiss, and the report's grid takes the keyboard back
    public async Task The_dialog_closes_on_an_escape_its_grid_has_nothing_left_to_dismiss()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(RegionProduct, detailsView: PivotDetailsView.Dialog);
        await DoubleClickAsync(cut, 1);
        var records = RecordsGrid(cut);
        Assert.NotNull(cut.Find(".ex-pivot-dialog .mud-ex-pivot-dialog-records .ex-grid"));
        Assert.True(records.Instance.OnLeave.HasDelegate);
        var returns = KeyboardReturns(report);
        var releases = TabReleases();

        await EscapeAsync(records);

        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
        Assert.False(cut.Find(".ex-pivot-report").HasAttribute("inert"));
        Assert.Equal(releases, TabReleases());
        Assert.Equal(returns + 1, KeyboardReturns(report));
    }

    [Theory] // ADR-0070/0062 (PV-9, PV-39): however the dialog closes under MudBlazor — its grid's Escape, an Escape on its Close, Close, the backdrop — the report's grid takes the keyboard back, once, after the render that took the dialog away
    [InlineData("grid")]
    [InlineData("escape")]
    [InlineData("close")]
    [InlineData("backdrop")]
    public async Task However_the_dialog_closes_the_report_takes_the_keyboard_back(string how)
    {
        var report = ReportGridHandle();
        IRenderedComponent<PivotComponent>? cut = null;
        var seen = WatchReturns(report, () => cut!);
        cut = RenderPivot(RegionProduct, detailsView: PivotDetailsView.Dialog);
        await DoubleClickAsync(cut, 1);
        Assert.Empty(seen);

        switch (how)
        {
            case "grid":
                await EscapeAsync(RecordsGrid(cut));
                break;
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
        Assert.Equal([true], seen);
    }

    [Fact] // ADR-0070/0062 (PV-39, KB-8): under MudBlazor, Escape in a details tab's grid closes nothing — a tab is a sheet of its own — and releases Tab
    public async Task Escape_in_a_details_tabs_grid_closes_nothing()
    {
        ReportGridHandle();
        var cut = RenderPivot(RegionProduct);
        await DoubleClickAsync(cut, 1);
        var records = RecordsGrid(cut);
        Assert.False(records.Instance.OnLeave.HasDelegate);
        var releases = TabReleases();

        await EscapeAsync(records);

        Assert.Equal(["PivotTable", "Details: East / Apples"], TabTitles(cut));
        Assert.Single(cut.FindAll(".ex-pivot-details-panel"));
        Assert.Equal(releases + 1, TabReleases());
    }

    [Fact] // ADR-0070/0062 (PV-39): under MudBlazor, the last details tab closed takes MudTabs away, and the report's grid takes the keyboard back once its records no longer cover it
    public async Task Closing_the_last_details_tab_gives_the_report_the_keyboard_back()
    {
        var report = ReportGridHandle();
        IRenderedComponent<PivotComponent>? cut = null;
        var seen = WatchReturns(report, () => cut!);
        cut = RenderPivot(RegionProduct);
        await DoubleClickAsync(cut, 1);
        var focusCalls = FocusCalls();

        await CloseTabAsync(cut, "Details: East / Apples");

        Assert.Empty(cut.FindComponents<MudTabs>());
        Assert.Equal([true], seen);
        Assert.Equal(focusCalls, FocusCalls());
    }

    [Fact] // ADR-0070/0062 (PV-39): under MudBlazor, a details tab closed while selected hands the keyboard to the details tab MudTabs shows next; the report's grid, covered, is not asked
    public async Task Closing_the_selected_tab_hands_the_keyboard_to_the_details_tab_selected_next()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(RegionProduct);
        await DoubleClickAsync(cut, 1);
        await DoubleClickAsync(cut, 2);
        var returns = KeyboardReturns(report);
        var focusCalls = FocusCalls();

        await CloseTabAsync(cut, "Details: East / Pears");

        Assert.Equal(["PivotTable", "Details: East / Apples"], TabTitles(cut));
        Assert.Equal(focusCalls + 1, FocusCalls());
        var tabs = cut.FindComponent<MudTabs>().Instance;
        Assert.Equal(tabs.Panels[1].PanelRef.Id, LastFocus()?.Id);
        Assert.Equal(returns, KeyboardReturns(report));
    }
}
