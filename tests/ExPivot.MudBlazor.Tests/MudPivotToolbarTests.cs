using Bunit;
using ExGrid.Components;
using ExGrid.MudBlazor;
using ExGrid.Selection;
using ExPivot.Components;
using ExPivot.Engine;
using ExPivot.MudBlazor.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Extensions;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.MudBlazor.Tests;

/// <summary>
/// What the Pivot Source contract and the toolbar added, under MudBlazor (ADR-0061/0062/0066):
/// Defer Layout Update at the Mud pane's foot, the toolbar above the report drawn with MudBlazor's
/// controls in ExPivot's order and roles — the Mud band, Layout ▾ opening the Mud menu in ExPivot's
/// frame, Refresh, the pane's pressed toggle, a refusal as an error alert — making the layouts the
/// built-in markup makes, and the Aggregations a source does not answer offered disabled with the
/// reason.
/// </summary>
public class MudPivotToolbarTests : MudPivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static Task DeferAsync(IRenderedComponent<PivotComponent> cut, bool defer)
        => cut.Find(".mud-ex-pivot-defer input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = defer });

    [Fact] // ADR-0061/0062 (PV-28): Defer Layout Update under MudBlazor — the pane shows the pending layout, the report waits for Update
    public async Task Defer_holds_the_report_until_update()
    {
        var cut = RenderPivot(RegionAmount);
        Assert.True(cut.Find(".mud-ex-pivot-update").HasAttribute("disabled"));

        await DeferAsync(cut, true);
        await TickFieldAsync(cut, "Quantity", true);

        Assert.Equal(["Sum of Amount", "Sum of Quantity"], Entries(cut, "Values"));
        Assert.Equal(["Row Labels", "Sum of Amount"], HeaderTexts(cut));
        Assert.Single(cut.Instance.CurrentLayout.Values);
        Assert.False(cut.Find(".mud-ex-pivot-update").HasAttribute("disabled"));

        await cut.Find(".mud-ex-pivot-update").ClickAsync(new MouseEventArgs());

        Assert.Equal(["Amount", "Quantity"], cut.Instance.CurrentLayout.Values.Select(v => v.Field));
        Assert.Equal("East | 180 | 18", RowTexts(cut)[0]);
        Assert.True(cut.Find(".mud-ex-pivot-update").HasAttribute("disabled"));
        Assert.True(cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Defer Layout Update").Instance.GetState(x => x.Value));
    }

    [Fact] // ADR-0061/0062 (PV-28): the same deferred gestures make the same layout under either Chrome
    public async Task Defer_makes_the_built_ins_layout()
    {
        var mud = RenderPivot(RegionAmount);
        await DeferAsync(mud, true);
        await TickFieldAsync(mud, "Product", true);
        await TickFieldAsync(mud, "Quantity", true);
        await mud.Find(".mud-ex-pivot-update").ClickAsync(new MouseEventArgs());

        var plain = RenderPivot(RegionAmount, chrome: BuiltIn);
        await plain.Find(".ex-pivot-defer input").ChangeAsync(new ChangeEventArgs { Value = true });
        await Tick(plain, "Product");
        await Tick(plain, "Quantity");
        await plain.Find(".ex-pivot-update").ClickAsync(new MouseEventArgs());

        Assert.Equal(PivotLayoutJson.Write(plain.Instance.CurrentLayout), PivotLayoutJson.Write(mud.Instance.CurrentLayout));
        Assert.Equal(RowTexts(plain), RowTexts(mud));

        static Task Tick(IRenderedComponent<PivotComponent> cut, string caption)
            => cut.FindAll(".ex-pivot-field").Single(f => f.TextContent.Trim() == caption).QuerySelector("input")!
                .ChangeAsync(new ChangeEventArgs { Value = true });
    }

    private static Task OpenLayoutMenuAsync(IRenderedComponent<PivotComponent> cut)
        => cut.Find(".mud-ex-pivot-layout-button").ClickAsync(new MouseEventArgs());

    /// <summary>The toolbar's MudButtons on its right, by their words, in order.</summary>
    private static string[] ToolbarButtons(IRenderedComponent<PivotComponent> cut)
        => cut.FindAll(".mud-ex-pivot-toolbar-end > .mud-ex-pivot-toolbar-anchor > button, .mud-ex-pivot-toolbar-end > button")
            .Select(b => b.TextContent.Trim()).ToArray();

    [Fact] // ADR-0061/0062 (PV-30): the Layout menu is the Mud menu — the groups headed, each choice a radio, the current ones checked, a no-op disabled
    public async Task The_layout_menu_is_the_mud_menu()
    {
        var cut = RenderPivot(RegionAmount with { Rows = [P("Region"), P("Product")] });

        await OpenLayoutMenuAsync(cut);

        var frame = cut.Find(".mud-ex-pivot-toolbar-anchor .ex-pivot-popup");
        Assert.Contains("ex-pivot-popup-overlay", frame.ClassName);
        Assert.Contains("ex-pivot-popup-end", frame.ClassName);
        Assert.Equal("true", cut.Find(".mud-ex-pivot-layout-button").GetAttribute("aria-expanded"));
        Assert.Equal("menu", frame.GetAttribute("role"));
        Assert.NotNull(frame.QuerySelector(".mud-ex-pivot-menu"));
        Assert.Equal(["Subtotals", "Grand Totals", "Report Layout"],
            cut.FindAll(".mud-ex-pivot-menu-heading").Select(h => h.TextContent.Trim()));
        var items = MenuItems(cut);
        Assert.Equal(12, items.Count);
        Assert.All(items, item => Assert.Equal("menuitemradio", item.GetAttribute("role")));
        Assert.Equal(["Show all Subtotals at Top of Group", "On for Rows and Columns", "Show in Compact Form"],
            items.Where(i => i.GetAttribute("aria-checked") == "true").Select(i => i.TextContent.Trim()));
        // The Compact form has no outer label columns to repeat into, so both label choices would
        // change nothing (ADR-0061).
        Assert.Equal(
            ["Show all Subtotals at Top of Group", "On for Rows and Columns", "Show in Compact Form", "Repeat All Item Labels", "Do Not Repeat Item Labels"],
            items.Where(i => i.HasAttribute("disabled")).Select(i => i.TextContent.Trim()));
        Assert.DoesNotContain(items, i => i.TextContent.Contains("Blank", StringComparison.Ordinal));
        Assert.Single(cut.FindAll(".ex-pivot-backdrop"));

        await RunMenuAsync(cut, "Show in Tabular Form");

        Assert.Equal(PivotReportForm.Tabular, cut.Instance.CurrentLayout.Form);
        Assert.Equal(["Region", "Product", "Sum of Amount"], HeaderTexts(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Empty(cut.FindAll(".ex-pivot-backdrop"));
        Assert.Equal("false", cut.Find(".mud-ex-pivot-layout-button").GetAttribute("aria-expanded"));
    }

    [Fact] // ADR-0039/0061 (PV-11): the Layout menu's first enabled choice takes DOM focus under MudBlazor; Escape closes it, and the keyboard goes back to Layout ▾
    public async Task The_layout_menu_takes_the_keyboard()
    {
        var cut = RenderPivot(RegionAmount);

        await OpenLayoutMenuAsync(cut);

        var first = cut.FindComponents<MudPivotButton>().First(b => b.Instance.Class == "mud-ex-pivot-menu-item" && !b.Instance.Disabled);
        Assert.Equal("Do Not Show Subtotals", first.Find("button").TextContent.Trim());
        Assert.Equal((ElementIdOf(first.Instance), false), LastFocus());

        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(RegionAmount.Form, cut.Instance.CurrentLayout.Form);
        var layout = cut.FindComponents<MudPivotButton>().Single(b => b.Instance.Class!.Contains("mud-ex-pivot-layout-button", StringComparison.Ordinal));
        Assert.Equal((ElementIdOf(layout.Instance), false), LastFocus());
    }

    [Fact] // ADR-0061/0062 (PV-30): the toolbar is MudBlazor's controls in ExPivot's order and roles — the Mud band on its left, then Layout ▾ and the pane's toggle, and no Refresh for a source that cannot be refreshed
    public void The_toolbar_is_drawn_with_mudblazor_controls()
    {
        var cut = RenderPivot(RegionAmount with { Filters = [P("Online")] });

        Assert.Empty(cut.FindAll(".ex-pivot-toolbar"));
        var toolbar = cut.Find(".ex-pivot-report > .mud-ex-pivot-toolbar");
        Assert.NotNull(toolbar.QuerySelector(".mud-ex-pivot-toolbar-start .mud-ex-pivot-filters .mud-ex-pivot-filter-button"));
        Assert.Equal(["Layout", "Field List"], ToolbarButtons(cut));
        var layout = cut.Find(".mud-ex-pivot-layout-button");
        Assert.Equal("menu", layout.GetAttribute("aria-haspopup"));
        Assert.Equal("false", layout.GetAttribute("aria-expanded"));
        Assert.Equal("true", cut.Find(".mud-ex-pivot-field-list-toggle").GetAttribute("aria-pressed"));
        var buttons = cut.FindComponents<MudButton>().Where(b => b.Instance.Class?.Contains("mud-ex-pivot-toolbar-button", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(
            [MudPivotIcons.ForCommand(PivotCommandIds.LayoutMenu), MudPivotIcons.ForCommand(PivotCommandIds.FieldListToggle)],
            buttons.Select(b => b.Instance.StartIcon));
        Assert.Equal(Icons.Material.Filled.ArrowDropDown, buttons[0].Instance.EndIcon);
        Assert.Empty(cut.FindAll(".mud-ex-pivot-refresh-button"));
    }

    [Fact] // ADR-0061/0062 (PV-30): the toggle shows whether the pane is shown — pressed, in the primary colour — and hides and shows the Mud pane
    public async Task The_toggle_hides_and_shows_the_pane()
    {
        var cut = RenderPivot(RegionAmount);
        Assert.Single(cut.FindAll(".mud-ex-pivot-pane"));
        Assert.Contains("mud-ex-pivot-pressed", cut.Find(".mud-ex-pivot-field-list-toggle").ClassName);
        Assert.Contains("mud-button-text-primary", cut.Find(".mud-ex-pivot-field-list-toggle").ClassName);

        await cut.Find(".mud-ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".mud-ex-pivot-pane"));
        Assert.Equal("false", cut.Find(".mud-ex-pivot-field-list-toggle").GetAttribute("aria-pressed"));
        Assert.DoesNotContain("mud-ex-pivot-pressed", cut.Find(".mud-ex-pivot-field-list-toggle").ClassName);

        await cut.Find(".mud-ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".mud-ex-pivot-pane"));
        Assert.Equal("true", cut.Find(".mud-ex-pivot-field-list-toggle").GetAttribute("aria-pressed"));
    }

    [Fact] // ADR-0066/0062 (PV-30): Refresh is a MudButton between Layout ▾ and the toggle when the source can be refreshed; it refreshes the source and asks again
    public async Task Refresh_is_a_mud_button_between_layout_and_the_toggle()
    {
        var source = new LimitedSource(PivotSource.From(Sales, Fields), new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh: true));
        var cut = RenderPivot(RegionAmount, source: source);
        Assert.Equal(["Layout", "Refresh", "Field List"], ToolbarButtons(cut));
        var refresh = cut.FindComponents<MudButton>().Single(b => b.Instance.Class!.Contains("mud-ex-pivot-refresh-button", StringComparison.Ordinal));
        Assert.Equal(Icons.Material.Filled.Refresh, refresh.Instance.StartIcon);
        var asked = source.Questions;

        await cut.Find(".mud-ex-pivot-refresh-button").ClickAsync(new MouseEventArgs());

        Assert.Equal(1, source.Refreshes);
        Assert.Equal(asked + 1, source.Questions);
    }

    [Fact] // ADR-0066/0062 (PV-29, PV-24): what the report could not do with the last change is an error MudAlert under the toolbar, an alert as the built-in notice is
    public void A_refusal_is_an_error_mud_alert()
    {
        var source = new LimitedSource(PivotSource.From(Sales, Fields), new PivotSourceFeatures([PivotAggregation.Sum, PivotAggregation.Count]));

        var cut = RenderPivot(RegionAmount with { Values = [new PivotValueField("Amount", PivotAggregation.Product)] }, source: source);

        var alert = cut.FindComponents<MudAlert>().Single(a => a.Instance.Class == "mud-ex-pivot-refusal-notice");
        Assert.Equal(Severity.Error, alert.Instance.Severity);
        var notice = cut.Find(".ex-pivot-report > .mud-alert.mud-ex-pivot-refusal-notice");
        Assert.Equal("alert", notice.GetAttribute("role"));
        Assert.Equal("The source does not answer Product.", notice.QuerySelector(".mud-alert-message")!.TextContent.Trim());
        Assert.Empty(cut.FindAll(".ex-pivot-refusal-notice"));
        Assert.Equal(0, source.Questions);
    }

    [Fact] // ADR-0061/0062 (PV-9, PV-30, PV-12): the toolbar's gestures — a Layout choice, the band's Filter… and the toggle — make the built-in markup's layout under MudBlazor
    public async Task The_toolbar_makes_the_built_ins_layout()
    {
        var start = RegionAmount with { Filters = [P("Online")], Rows = [P("Region"), P("Product")] };
        var mud = RenderPivot(start);
        await OpenLayoutMenuAsync(mud);
        await RunMenuAsync(mud, "Show in Outline Form");
        await mud.Find(".mud-ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        await TickItemAsync(mud, "FALSE", false);
        await mud.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());
        await mud.Find(".mud-ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        var plain = RenderPivot(start, chrome: BuiltIn);
        await plain.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());
        await plain.FindAll(".ex-pivot-menu-item").Single(b => b.TextContent.Replace("✓", "", StringComparison.Ordinal).Trim() == "Show in Outline Form")
            .ClickAsync(new MouseEventArgs());
        await plain.Find(".ex-pivot-filter-button").ClickAsync(new MouseEventArgs());
        await plain.FindAll(".ex-pivot-item").Single(i => i.TextContent.Trim() == "FALSE").QuerySelector("input")!
            .ChangeAsync(new ChangeEventArgs { Value = false });
        await plain.Find(".ex-pivot-ok").ClickAsync(new MouseEventArgs());
        await plain.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.Equal(PivotReportForm.Outline, mud.Instance.CurrentLayout.Form);
        Assert.Equal([PivotItemKey.Boolean(false)], mud.Instance.CurrentLayout.Filters[0].HiddenItems);
        Assert.Equal(PivotLayoutJson.Write(plain.Instance.CurrentLayout), PivotLayoutJson.Write(mud.Instance.CurrentLayout));
        Assert.Equal(RowTexts(plain), RowTexts(mud));
        Assert.Equal("TRUE", mud.Find(".mud-ex-pivot-filter-summary").TextContent.Trim());
        Assert.Empty(mud.FindAll(".mud-ex-pivot-pane"));
        Assert.Empty(plain.FindAll(".ex-pivot-field-list"));
    }

    [Fact] // ADR-0062/0066 (PV-24): an Aggregation the source does not answer is offered disabled in the Mud panel, with the reason, and never asked for
    public async Task Value_field_settings_offer_unanswered_aggregations_disabled()
    {
        var source = new LimitedSource(PivotSource.From(Sales, Fields), new PivotSourceFeatures([PivotAggregation.Sum, PivotAggregation.Count]));
        var cut = RenderPivot(RegionAmount, source: source);
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");

        var reasons = cut.FindAll(".mud-ex-pivot-not-offered").Select(r => r.TextContent.Trim()).ToArray();
        Assert.Equal(9, reasons.Length);
        Assert.Equal("The source does not answer Average.", reasons[0]);
        var items = cut.FindComponents<MudSelectItem<PivotAggregation>>();
        Assert.Equal([PivotAggregation.Sum, PivotAggregation.Count], items.Where(i => !i.Instance.Disabled).Select(i => i.Instance.Value));
        var asked = source.Questions;

        // Chosen anyway — a Chrome that does not honour the disabled state — it changes nothing,
        // and OK asks nothing for it.
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<PivotAggregation>>().Instance.ValueChanged.InvokeAsync(PivotAggregation.Average));
        Assert.Equal(PivotAggregation.Sum, cut.FindComponent<MudSelect<PivotAggregation>>().Instance.GetState(x => x.Value));
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(PivotAggregation.Sum, cut.Instance.CurrentLayout.Values[0].Aggregation);
        Assert.Equal(asked, source.Questions);
    }

    /// <summary>The bundled source with fewer features, counting what it is asked.</summary>
    private sealed class LimitedSource(PivotSource inner, PivotSourceFeatures features) : PivotSource
    {
        public int Questions { get; private set; }

        public int Refreshes { get; private set; }

        public override IReadOnlyList<PivotField> Fields => inner.Fields;

        public override PivotSourceFeatures Features => features;

        public override ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
        {
            Questions++;
            return inner.AggregateAsync(query, cancellationToken);
        }

        public override ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
            => inner.ItemsAsync(query, cancellationToken);

        public override ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
            => inner.DetailsAsync(query, cancellationToken);

        public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            Refreshes++;
            return inner.RefreshAsync(cancellationToken);
        }
    }
}
