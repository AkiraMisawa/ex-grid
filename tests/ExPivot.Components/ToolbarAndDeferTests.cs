using Bunit;
using ExPivot.Chrome;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>
/// Defer Layout Update at the pane's foot, and the Pivot Toolbar above the report (ADR-0061): the
/// report filter band on its left; Layout ▾, Refresh and the Field List's toggle on its right.
/// </summary>
public class ToolbarAndDeferTests : PivotTestContext
{
    private static readonly PivotLayout RegionProduct = new() { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };

    private static Task DeferAsync(IRenderedComponent<PivotComponent> cut, bool defer)
        => cut.Find(".ex-pivot-defer input").ChangeAsync(new ChangeEventArgs { Value = defer });

    private static Task OpenLayoutMenuAsync(IRenderedComponent<PivotComponent> cut)
        => cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());

    // ---- PV-28: Defer Layout Update -----------------------------------------------------------

    [Fact] // ADR-0061 (PV-28): while Defer Layout Update is ticked, the pane's changes build a pending layout, and the report and the source are left alone
    public async Task Deferred_changes_build_a_pending_layout()
    {
        var told = new List<PivotLayout>();
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, ps => ps.Add(p => p.LayoutChanged, told.Add), source: source);
        var before = RowTexts(cut);
        Assert.True(cut.Find(".ex-pivot-update").HasAttribute("disabled"));

        await DeferAsync(cut, true);
        await TickFieldAsync(cut, "Product", true);
        await FieldItem(cut, "Online").DragStartAsync(new DragEventArgs());
        await AreaElement(cut, "Columns").DropAsync(new DragEventArgs());

        Assert.Equal(["Region", "Product"], AreaEntries(cut, "Rows"));
        Assert.Equal(["Online"], AreaEntries(cut, "Columns"));
        Assert.Equal(before, RowTexts(cut));
        Assert.Single(source.Questions);
        Assert.Empty(told);
        Assert.True(cut.Find(".ex-pivot-defer input").HasAttribute("checked"));
        Assert.False(cut.Find(".ex-pivot-update").HasAttribute("disabled"));
    }

    [Fact] // ADR-0061 (PV-28): Update applies the pending layout in one change — one question, one LayoutChanged — and Defer stays ticked
    public async Task Update_applies_the_pending_layout_in_one_change()
    {
        var told = new List<PivotLayout>();
        var source = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, ps => ps.Add(p => p.LayoutChanged, told.Add), source: source);
        await DeferAsync(cut, true);
        await TickFieldAsync(cut, "Product", true);
        await TickFieldAsync(cut, "Quantity", true);

        await cut.Find(".ex-pivot-update").ClickAsync(new MouseEventArgs());

        Assert.Equal(2, source.Questions.Count);
        Assert.Single(told);
        Assert.Equal(["Region", "Product"], told[0].Rows.Select(p => p.Field));
        Assert.Equal(["Amount", "Quantity"], told[0].Values.Select(v => v.Field));
        Assert.Equal("−East | 180 | 18", RowTexts(cut)[0]);
        Assert.True(cut.Find(".ex-pivot-defer input").HasAttribute("checked"));
        Assert.True(cut.Find(".ex-pivot-update").HasAttribute("disabled"));
    }

    [Fact] // ADR-0061 (PV-28): unticking Defer Layout Update applies the pending layout, as Excel does
    public async Task Unticking_applies_the_pending_layout()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, ps => ps.Add(p => p.LayoutChanged, told.Add));
        await DeferAsync(cut, true);
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Move to Column Labels");
        Assert.Empty(told);

        await DeferAsync(cut, false);

        Assert.Single(told);
        Assert.Equal(["Region"], cut.Instance.CurrentLayout.Columns.Select(p => p.Field));
        Assert.Equal(["East", "North", "West", "(blank)", "Grand Total"], HeaderTexts(cut));
        Assert.False(cut.Find(".ex-pivot-defer input").HasAttribute("checked"));
    }

    [Fact] // ADR-0061 (PV-28): LayoutChanged is raised only for the layout the report shows — a gesture on the report while deferring is shown, and reaches the pending layout too
    public async Task A_report_gesture_while_deferring_is_applied_and_kept()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.LayoutChanged, told.Add));
        await DeferAsync(cut, true);
        await TickFieldAsync(cut, "Quantity", true);

        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());

        Assert.Single(told);
        Assert.Single(told[0].Values);
        Assert.True(told[0].Rows[0].IsCollapsed(PivotItemKey.Text("East")));
        Assert.Equal("+East | 180", RowTexts(cut)[0]);

        await cut.Find(".ex-pivot-update").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, told.Count);
        Assert.True(told[1].Rows[0].IsCollapsed(PivotItemKey.Text("East")));
        Assert.Equal(2, told[1].Values.Count);
    }

    // ---- PV-30: the Pivot Toolbar ---------------------------------------------------------------

    [Fact] // ADR-0061 (PV-30): the Pivot Toolbar holds the band on its left, and Layout ▾ then the Field List's toggle on its right — no Refresh for a source that cannot be refreshed
    public void The_toolbar_holds_its_controls_in_order()
    {
        var cut = RenderPivot(RegionProduct with { Filters = [P("Online")] });

        var toolbar = cut.Find(".ex-pivot-toolbar");
        Assert.NotNull(toolbar.QuerySelector(".ex-pivot-toolbar-start .ex-pivot-filters"));
        var buttons = toolbar.QuerySelectorAll(".ex-pivot-toolbar-end button");
        Assert.Equal(["layout-button", "field-list-toggle"], buttons.Select(b => b.ClassList.Last().Replace("ex-pivot-", "")));
        Assert.Equal("Layout", buttons[0].QuerySelector("span")!.TextContent);
        Assert.Equal("Field List", buttons[1].TextContent);
        Assert.Equal("menu", toolbar.QuerySelector(".ex-pivot-layout-button")!.GetAttribute("aria-haspopup"));
        Assert.Equal("true", toolbar.QuerySelector(".ex-pivot-field-list-toggle")!.GetAttribute("aria-pressed"));
        Assert.Empty(cut.FindAll(".ex-pivot-refresh-button"));
    }

    [Fact] // ADR-0066 (PV-30): Refresh stands between Layout ▾ and the toggle when the source can be refreshed; it refreshes the source and asks again
    public async Task Refresh_refreshes_the_source_and_asks_again()
    {
        var source = new OnDemandSource(Bundled(), new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh: true)) { AnswersAtOnce = true };
        var cut = RenderPivot(RegionProduct, source: source);
        Assert.Equal(["layout-button", "refresh-button", "field-list-toggle"],
            cut.FindAll(".ex-pivot-toolbar-end button").Select(b => b.ClassList.Last().Replace("ex-pivot-", "")));

        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.Equal("Refresh", cut.Find(".ex-pivot-refresh-button").TextContent);
        Assert.Equal(1, source.Refreshes);
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(source.Questions[0].Query, source.Questions[1].Query);
    }

    /// <summary>The time a Stale Report's notice writes for an instant on the test's clock: today's,
    /// in the report's culture.</summary>
    private string TimeOf(DateTimeOffset at)
        => TimeZoneInfo.ConvertTime(at, Clock.LocalTimeZone).ToString("T", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    private static OnDemandSource Refreshable(Exception? refreshFails = null)
        => new(Bundled(), new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh: true))
        {
            AnswersAtOnce = true,
            RefreshFails = refreshFails,
        };

    [Fact] // ADR-0067 refined (PV-37, PV-30): a failed Refresh is a Stale Report — the report stays on the version shown, and the notice says the source could not answer, as of when, with Retry; nothing is said on the Pivot Toolbar, and nothing is thrown
    public async Task A_failed_refresh_is_a_stale_report()
    {
        var source = Refreshable(new InvalidOperationException("The server cannot be reached."));
        var cut = RenderPivot(RegionProduct, source: source);
        var shownAt = Clock.GetUtcNow();
        var before = RowTexts(cut);
        var layout = cut.Instance.CurrentLayout;
        Clock.Advance(TimeSpan.FromSeconds(30));

        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.True(cut.Instance.IsStale);
        Assert.Equal($"Showing the data as of {TimeOf(shownAt)}: the source could not answer: The server cannot be reached.",
            cut.Find(".ex-pivot-stale[role=status] .ex-pivot-stale-message").TextContent);
        Assert.Equal("Retry", cut.Find(".ex-pivot-stale .ex-pivot-retry").TextContent);
        Assert.False(cut.Find(".ex-pivot-retry").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
        Assert.Equal(before, RowTexts(cut));
        Assert.Same(layout, cut.Instance.CurrentLayout);
        Assert.Equal(1, source.Refreshes);
        Assert.Single(source.Questions);
        Assert.IsType<InvalidOperationException>(cut.Instance.LastError);
    }

    [Fact] // ADR-0067 refined (PV-37): Retry after a failed Refresh refreshes again — what failed was the refresh — and the notice goes when the answer is laid out
    public async Task Retry_after_a_failed_refresh_refreshes_again()
    {
        var source = Refreshable(new InvalidOperationException("The server cannot be reached."));
        var cut = RenderPivot(RegionProduct, source: source);
        var shownAt = Clock.GetUtcNow();
        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());
        Clock.Advance(TimeSpan.FromSeconds(30));

        // Still failing: still stale, as of the same time.
        await cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, source.Refreshes);
        Assert.Single(source.Questions);
        Assert.StartsWith($"Showing the data as of {TimeOf(shownAt)}: ", cut.Find(".ex-pivot-stale-message").TextContent);

        source.RefreshFails = null;
        await cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal(3, source.Refreshes);
        Assert.Equal(2, source.Questions.Count);
        Assert.Equal(source.Questions[0].Query, source.Questions[1].Query);
        Assert.False(cut.Instance.IsStale);
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
        Assert.Null(cut.Instance.LastError);
    }

    [Fact] // ADR-0067 refined (PV-37): a Refresh that succeeds but whose answer fails is a Stale Report too, and its Retry asks the report again without refreshing
    public async Task A_refresh_whose_answer_fails_is_a_stale_report_retried_by_asking_again()
    {
        var source = Refreshable();
        var cut = RenderPivot(RegionProduct, source: source);
        source.AnswersAtOnce = false;

        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => source.Questions[1].Fail(new InvalidOperationException("The server is unreachable.")));

        cut.WaitForAssertion(() => Assert.True(cut.Instance.IsStale));
        Assert.EndsWith(": the source could not answer: The server is unreachable.", cut.Find(".ex-pivot-stale-message").TextContent);
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));

        source.AnswersAtOnce = true;
        await cut.Find(".ex-pivot-retry").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, source.Refreshes);
        Assert.Equal(3, source.Questions.Count);
        Assert.False(cut.Instance.IsStale);
    }

    [Fact] // ADR-0067 refined (PV-37): before the first report there is nothing to be stale, so a failed Refresh is said on the Pivot Toolbar
    public async Task A_failed_refresh_before_the_first_report_is_said_on_the_toolbar()
    {
        var source = Refreshable(new InvalidOperationException("The server cannot be reached."));
        source.AnswersAtOnce = false;
        var cut = RenderPivot(RegionProduct, source: source);

        await cut.Find(".ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.False(cut.Instance.IsStale);
        Assert.Equal("The source could not answer: The server cannot be reached.", cut.Find(".ex-pivot-refusal-notice").TextContent);
        Assert.Equal("alert", cut.Find(".ex-pivot-refusal-notice").GetAttribute("role"));
        Assert.Empty(cut.FindAll(".ex-pivot-stale-notice"));
    }

    [Fact] // ADR-0061 (PV-30): the Layout menu offers Excel's Design tab choices under its headings, the current choice marked and a choice that would change nothing disabled
    public async Task The_layout_menu_offers_excels_choices()
    {
        var cut = RenderPivot(RegionProduct);

        await OpenLayoutMenuAsync(cut);

        var frame = cut.Find(".ex-pivot-toolbar .ex-pivot-popup");
        Assert.Equal("menu", frame.GetAttribute("role"));
        Assert.Equal("Layout", frame.GetAttribute("aria-label"));
        Assert.Contains("ex-pivot-popup-overlay", frame.ClassName);
        Assert.Single(cut.FindAll(".ex-pivot-backdrop"));
        Assert.Equal(["Subtotals", "Grand Totals", "Report Layout"], cut.FindAll(".ex-pivot-menu-heading").Select(h => h.TextContent));
        var items = cut.FindAll(".ex-pivot-menu-item");
        Assert.Equal(
        [
            "Do Not Show Subtotals", "Show all Subtotals at Bottom of Group", "Show all Subtotals at Top of Group",
            "Off for Rows and Columns", "On for Rows and Columns", "On for Rows Only", "On for Columns Only",
            "Show in Compact Form", "Show in Outline Form", "Show in Tabular Form", "Repeat All Item Labels", "Do Not Repeat Item Labels",
        ], items.Select(MenuLabel));
        Assert.All(items, item => Assert.Equal("menuitemradio", item.GetAttribute("role")));
        Assert.Equal(["Show all Subtotals at Top of Group", "On for Rows and Columns", "Show in Compact Form"],
            items.Where(i => i.GetAttribute("aria-checked") == "true").Select(MenuLabel));
        Assert.Equal(
            ["Show all Subtotals at Top of Group", "On for Rows and Columns", "Show in Compact Form", "Repeat All Item Labels", "Do Not Repeat Item Labels"],
            items.Where(i => i.HasAttribute("disabled")).Select(MenuLabel));
        Assert.Equal("true", cut.Find(".ex-pivot-layout-button").GetAttribute("aria-expanded"));
    }

    [Fact] // ADR-0061 (PV-30): a Layout menu choice lays the report out again, closes the menu, and gives the keyboard back to Layout ▾
    public async Task A_layout_choice_changes_the_report()
    {
        var cut = RenderPivot(RegionProduct);
        await OpenLayoutMenuAsync(cut);
        var focusCalls = JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");

        await RunMenuAsync(cut, "Show in Tabular Form");

        Assert.Equal(PivotReportForm.Tabular, cut.Instance.CurrentLayout.Form);
        Assert.Equal(["Region", "Product", "Sum of Amount"], HeaderTexts(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-toolbar .ex-pivot-popup"));
        Assert.Empty(cut.FindAll(".ex-pivot-backdrop"));
        Assert.Equal("false", cut.Find(".ex-pivot-layout-button").GetAttribute("aria-expanded"));
        Assert.True(JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus") > focusCalls);

        await OpenLayoutMenuAsync(cut);
        Assert.Equal(["Show all Subtotals at Bottom of Group", "On for Rows and Columns", "Show in Tabular Form", "Do Not Repeat Item Labels"],
            cut.FindAll(".ex-pivot-menu-item").Where(i => i.GetAttribute("aria-checked") == "true").Select(MenuLabel));
        Assert.True(MenuItem(cut, "Show all Subtotals at Top of Group").HasAttribute("disabled"));
        await RunMenuAsync(cut, "Repeat All Item Labels");
        Assert.True(cut.Instance.CurrentLayout.RepeatItemLabels);
        Assert.Equal("East | Pears | 50", RowTexts(cut)[1]);
    }

    [Theory] // ADR-0061 (PV-30): Escape and the backdrop close the Layout menu, and change nothing
    [InlineData("escape")]
    [InlineData("backdrop")]
    public async Task Escape_and_the_backdrop_close_the_layout_menu(string how)
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.LayoutChanged, told.Add));
        await OpenLayoutMenuAsync(cut);

        if (how == "escape")
            await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        else
            await cut.Find(".ex-pivot-backdrop").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Empty(told);
    }

    [Fact] // ADR-0061 (PV-30): the Field List's toggle shows and hides the pane, and a Consumer binds it with @bind-ShowFieldList
    public async Task The_field_list_toggle_binds()
    {
        var page = RenderPage<BoundToggle>();
        var cut = page.FindComponent<PivotComponent>();

        await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.False(page.Instance.Shown);
        Assert.Empty(cut.FindAll(".ex-pivot-field-list"));
        Assert.Equal("false", cut.Find(".ex-pivot-field-list-toggle").GetAttribute("aria-pressed"));

        await cut.InvokeAsync(ContextCommands(cut, 0, "row-labels").Single(c => c.Id == PivotCommandIds.ShowFieldList).Invoke);
        Assert.True(page.Instance.Shown);
        Assert.Single(cut.FindAll(".ex-pivot-field-list"));

        await page.InvokeAsync(() => page.Instance.Show(false));
        Assert.Empty(cut.FindAll(".ex-pivot-field-list"));
    }

    [Fact] // ADR-0061: the heading's close button binds visibility and preserves deferred edits
    public async Task The_heading_closes_the_pane_and_keeps_its_pending_layout()
    {
        var page = RenderPage<BoundToggle>();
        var cut = page.FindComponent<PivotComponent>();
        var reportBefore = RowTexts(cut);
        await DeferAsync(cut, true);
        await TickFieldAsync(cut, "Quantity", true);
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Field Settings…");

        await cut.Find("button[aria-label='Hide Field List']").ClickAsync(new MouseEventArgs());

        Assert.False(page.Instance.Shown);
        Assert.Empty(cut.FindAll(".ex-pivot-field-list"));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(reportBefore, RowTexts(cut));
        await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());
        Assert.True(page.Instance.Shown);
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Contains("Sum of Quantity", AreaEntries(cut, "Values"));
        await cut.Find(".ex-pivot-update").ClickAsync(new MouseEventArgs());
        Assert.Contains(cut.Instance.CurrentLayout.Values, v => v.Field == "Quantity");
    }

    [Fact] // ADR-0061: a pane hidden by the user stays hidden while the Consumer keeps passing the value it always passed
    public async Task An_unbound_pane_keeps_the_users_choice()
    {
        var cut = RenderPivot(RegionProduct);

        await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());
        cut.Render(ps => ps.Add(p => p.ShowFieldList, true));

        Assert.Empty(cut.FindAll(".ex-pivot-field-list"));
    }

    // ---- PV-12: the report filter band, on the Pivot Toolbar's left -----------------------------

    [Fact] // ADR-0061 (PV-12): the band's Filter… opens under the Pivot Toolbar, over the report, with a backdrop; OK filters the report and the keyboard goes back to its button
    public async Task The_bands_filter_opens_under_the_toolbar()
    {
        var cut = RenderPivot(new PivotLayout { Filters = [P("Region")], Rows = [P("Product")], Values = [Sum("Amount")] });

        await cut.Find(".ex-pivot-toolbar .ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        var frame = cut.Find(".ex-pivot-toolbar-start .ex-pivot-popup");
        Assert.Equal("dialog", frame.GetAttribute("role"));
        Assert.Equal("Filter Region", frame.GetAttribute("aria-label"));
        Assert.Contains("ex-pivot-popup-overlay", frame.ClassName);
        Assert.DoesNotContain("ex-pivot-popup-end", frame.ClassName);
        Assert.Single(cut.FindAll(".ex-pivot-backdrop"));

        await cut.FindAll(".ex-pivot-item").Single(i => i.TextContent.Trim() == "East").QuerySelector("input")!
            .ChangeAsync(new ChangeEventArgs { Value = false });
        await cut.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal("(Multiple Items)", cut.Find(".ex-pivot-filter-summary").TextContent);
        Assert.Equal("Grand Total | 105", RowTexts(cut)[^1]);
        Assert.Empty(cut.FindAll(".ex-pivot-backdrop"));
    }

    /// <summary>A Consumer's page binding the pane's visibility.</summary>
    private sealed class BoundToggle : ComponentBase
    {
        private readonly PivotSource _source = Bundled();

        public bool Shown { get; private set; } = true;

        public void Show(bool shown)
        {
            Shown = shown;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<PivotComponent>(0);
            builder.AddComponentParameter(1, nameof(PivotComponent.DataSource), _source);
            builder.AddComponentParameter(2, nameof(PivotComponent.Layout), RegionProduct);
            builder.AddComponentParameter(3, nameof(PivotComponent.ShowFieldList), Shown);
            builder.AddComponentParameter(4, nameof(PivotComponent.ShowFieldListChanged),
                EventCallback.Factory.Create<bool>(this, shown => Shown = shown));
            builder.AddComponentParameter(5, nameof(PivotComponent.ViewportHeight), (ExGrid.ViewportSize)300);
            builder.AddComponentParameter(6, nameof(PivotComponent.ViewportWidth), (ExGrid.ViewportSize)600);
            builder.CloseComponent();
        }
    }
}
