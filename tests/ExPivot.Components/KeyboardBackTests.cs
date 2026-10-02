using Bunit;
using ExGrid.Components;
using ExGrid.Selection;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// Where the keyboard goes when Show Details' dialog or a details tab goes away, and what Escape
/// does inside their grids (ADR-0070, PV-39), under ExPivot's own markup. The dialog's grid declares
/// <c>OnLeave</c>, and the dialog closes on the Escape that grid has nothing left to dismiss. However
/// the dialog closes, the report's grid takes the keyboard back, through ExGrid's
/// <c>ReturnKeyboardAsync</c>, once the render that took the dialog away has landed. A details tab
/// that closes hands the keyboard to the tab selected then: a details tab's button, or the report's
/// grid when the report's tab is the one. Escape in a details tab's grid closes nothing.
///
/// <para>Which grid asked is read off the report grid's own handle; whether the browser grants the
/// request — DOM focus on nothing, or already inside the report — is layer 3's.</para>
/// </summary>
public class KeyboardBackTests : PivotTestContext
{
    private const string BlazorFocus = "Blazor._internal.domWrapper.focus";

    private static readonly PivotLayout ByRegionAndProduct = new() { Rows = [P("Region")], Columns = [P("Product")], Values = [Sum("Amount")] };

    private static Task DoubleClickAsync(IRenderedComponent<PivotComponent> cut, int row, int column)
        => cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(row, column)));

    private static IRenderedComponent<ExGrid<PivotDetailRecord>> RecordsGrid(IRenderedComponent<PivotComponent> cut)
        => cut.FindComponent<ExGrid<PivotDetailRecord>>();

    /// <summary>A key on the records grid as its capture-phase listener forwards it: from its root
    /// unless it came from a focusable descendant.</summary>
    private static Task PressAsync(
        IRenderedComponent<ExGrid<PivotDetailRecord>> grid, string key, bool shift = false, bool fromDescendant = false)
        => grid.InvokeAsync(() => grid.Instance.OnKeyAsync(
            key, ctrl: false, shift: shift, alt: false, meta: false, metaIsPrimary: false, fromDescendant: fromDescendant));

    private int TabReleases() => JSInterop.Invocations.Count(invocation => invocation.Identifier == "releaseTab");

    private int FocusCalls() => JSInterop.Invocations.Count(invocation => invocation.Identifier == BlazorFocus);

    private static string[] TabTitles(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".ex-pivot-tab-button").Select(tab => tab.TextContent).ToArray();

    private static IRenderedComponent<PivotFocusButton> TabButton(IRenderedComponent<PivotComponent> cut, string title)
        => cut.FindComponents<PivotFocusButton>().Single(b => b.Instance.Class == "ex-pivot-tab-button" && b.Find("button").TextContent == title);

    /// <summary>What stood on screen each time the report's grid asked for the keyboard back: whether
    /// the dialog was gone, and whether the report was neither inert nor covered by a tab's records.
    /// The request is granted only to a root that can take focus, so it is made after the render
    /// that removes what held the keyboard.</summary>
    private static List<bool> WatchReturns(Bunit.BunitJSModuleInterop report, Func<IRenderedComponent<PivotComponent>> cut)
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

    // ---- The dialog -----------------------------------------------------------------------------

    [Fact] // ADR-0070 (PV-39): Show Details' dialog declares OnLeave on its grid, closes on the Escape that grid has nothing left to dismiss, and the report's grid takes the keyboard back
    public async Task The_dialog_closes_on_an_escape_its_grid_has_nothing_left_to_dismiss()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.DetailsView, PivotDetailsView.Dialog));
        await DoubleClickAsync(cut, 0, 1);
        var records = RecordsGrid(cut);
        Assert.True(records.Instance.OnLeave.HasDelegate);
        var returns = KeyboardReturns(report);
        var releases = TabReleases();

        await PressAsync(records, "Escape");

        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
        Assert.Empty(cut.FindAll(".ex-pivot-dialog-backdrop"));
        Assert.False(cut.Find(".ex-pivot-report").HasAttribute("inert"));
        // The records grid raised OnLeave in place of releasing Tab (ADR-0070, beside ADR-0012's
        // rewrite); the report's grid takes the keyboard back.
        Assert.Equal(releases, TabReleases());
        Assert.Equal(returns + 1, KeyboardReturns(report));
    }

    [Theory] // ADR-0070 (PV-39): however the dialog closes — its grid's Escape, an Escape on its frame, Close, the backdrop — the report's grid takes the keyboard back, once, after the render that took the dialog away
    [InlineData("grid")]
    [InlineData("frame")]
    [InlineData("close")]
    [InlineData("backdrop")]
    public async Task However_the_dialog_closes_the_report_takes_the_keyboard_back(string how)
    {
        var report = ReportGridHandle();
        IRenderedComponent<PivotComponent>? cut = null;
        var seen = WatchReturns(report, () => cut!);
        cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.DetailsView, PivotDetailsView.Dialog));
        await DoubleClickAsync(cut, 0, 1);
        Assert.Empty(seen);

        switch (how)
        {
            case "grid":
                await PressAsync(RecordsGrid(cut), "Escape");
                break;
            case "frame":
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
        Assert.Equal([true], seen);
    }

    [Fact] // ADR-0070/0012 (PV-39): the dialog's grid peels its own layers first — the Escape that closes its Context Menu leaves the dialog standing, and the next one closes it
    public async Task An_escape_that_closes_the_records_menu_leaves_the_dialog_standing()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(ByRegionAndProduct, ps => ps.Add(p => p.DetailsView, PivotDetailsView.Dialog));
        await DoubleClickAsync(cut, 0, 1);
        var records = RecordsGrid(cut);
        // The records grid stretches into the dialog, and paints once the browser has said how big
        // that box is; the first key places the Focus on its first cell (ADR-0012).
        await records.InvokeAsync(() => records.Instance.OnViewportReportAsync(0, 0, 800, 300));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-pivot-dialog .ex-viewport .ex-row")));
        await PressAsync(records, "ArrowDown");
        await PressAsync(records, "F10", shift: true);
        Assert.Single(cut.FindAll(".ex-pivot-dialog .ex-popover"));
        var returns = KeyboardReturns(report);

        await PressAsync(records, "Escape", fromDescendant: true);

        Assert.Empty(cut.FindAll(".ex-pivot-dialog .ex-popover"));
        Assert.Single(cut.FindAll(".ex-pivot-dialog"));
        Assert.Equal(returns, KeyboardReturns(report));

        await PressAsync(records, "Escape");

        Assert.Empty(cut.FindAll(".ex-pivot-dialog"));
        Assert.Equal(returns + 1, KeyboardReturns(report));
    }

    // ---- The tabs -------------------------------------------------------------------------------

    [Fact] // ADR-0070 (PV-39, KB-8): Escape in a details tab's grid closes nothing — a tab is a sheet of its own — and the grid releases Tab, as any grid's Escape with nothing to dismiss does
    public async Task Escape_in_a_details_tabs_grid_closes_nothing()
    {
        var cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);
        var records = RecordsGrid(cut);
        Assert.False(records.Instance.OnLeave.HasDelegate);
        var releases = TabReleases();

        await PressAsync(records, "Escape");

        Assert.Equal(["PivotTable", "Details: East / Apples"], TabTitles(cut));
        Assert.Single(cut.FindAll(".ex-pivot-details-panel"));
        Assert.Equal(releases + 1, TabReleases());
    }

    [Fact] // ADR-0070 (PV-39): closing the last details tab, the selected one, selects the report's tab, and the report's grid takes the keyboard back once its records no longer cover it — no tab is left to hold it
    public async Task Closing_the_last_details_tab_gives_the_report_the_keyboard_back()
    {
        var report = ReportGridHandle();
        IRenderedComponent<PivotComponent>? cut = null;
        var seen = WatchReturns(report, () => cut!);
        cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);
        var focusCalls = FocusCalls();

        await cut.Find(".ex-pivot-tab-close").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
        Assert.Equal([true], seen);
        Assert.Equal(focusCalls, FocusCalls());
    }

    [Fact] // ADR-0070 (PV-39): the last details tab closed while the report's tab is selected leaves the keyboard nowhere else either: the report's grid takes it back
    public async Task Closing_the_last_details_tab_behind_the_report_gives_the_report_the_keyboard_back()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);
        await cut.FindAll(".ex-pivot-tab-button")[0].ClickAsync(new MouseEventArgs());
        var returns = KeyboardReturns(report);

        await cut.Find(".ex-pivot-tab-close").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-pivot-tabs"));
        Assert.Equal(returns + 1, KeyboardReturns(report));
    }

    [Fact] // ADR-0070 (PV-39): a details tab closed while the report's tab is selected hands the keyboard to the tab selected then — the report's, which is its grid — and not to the report tab's button
    public async Task Closing_a_tab_while_the_reports_tab_is_selected_gives_the_report_the_keyboard()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);
        await DoubleClickAsync(cut, 1, 4);
        await cut.FindAll(".ex-pivot-tab-button")[0].ClickAsync(new MouseEventArgs());
        var returns = KeyboardReturns(report);
        var focusCalls = FocusCalls();

        await cut.FindAll(".ex-pivot-tab-close")[0].ClickAsync(new MouseEventArgs());

        Assert.Equal(["PivotTable", "Details: North"], TabTitles(cut));
        Assert.Equal("true", TabButton(cut, "PivotTable").Find("button").GetAttribute("aria-selected"));
        Assert.Equal(returns + 1, KeyboardReturns(report));
        Assert.Equal(focusCalls, FocusCalls());
    }

    [Fact] // ADR-0070/0059 (PV-39): a details tab closed while selected hands the keyboard to the details tab selected next, on its button; the report's grid, covered, is not asked
    public async Task Closing_the_selected_tab_hands_the_keyboard_to_the_details_tab_selected_next()
    {
        var report = ReportGridHandle();
        var cut = RenderPivot(ByRegionAndProduct);
        await DoubleClickAsync(cut, 0, 1);
        await DoubleClickAsync(cut, 1, 4);
        var opened = TabButton(cut, "Details: North").Instance.FocusRequest;
        var returns = KeyboardReturns(report);
        var focusCalls = FocusCalls();

        await cut.FindAll(".ex-pivot-tab-close")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("true", TabButton(cut, "Details: East / Apples").Find("button").GetAttribute("aria-selected"));
        Assert.True(TabButton(cut, "Details: East / Apples").Instance.FocusRequest > opened);
        Assert.Equal(focusCalls + 1, FocusCalls());
        Assert.Equal(returns, KeyboardReturns(report));
    }
}
