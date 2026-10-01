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
/// What the Pivot Source contract added, under MudBlazor (ADR-0060/0061/0065): Defer Layout Update
/// at the Mud pane's foot, the Layout menu drawn as the Mud menu, the Aggregations a source does
/// not answer offered disabled with the reason — and the toolbar and Show Details' tab, built-in
/// markup for now, holding the Mud band and the details grid dressed by the grid Wrapper.
/// </summary>
public class MudPivotToolbarTests : MudPivotTestContext
{
    private static readonly PivotLayout RegionAmount = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

    private static Task DeferAsync(IRenderedComponent<PivotComponent> cut, bool defer)
        => cut.Find(".mud-ex-pivot-defer input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = defer });

    [Fact] // ADR-0060/0061 (PV-28): Defer Layout Update under MudBlazor — the pane shows the pending layout, the report waits for Update
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

    [Fact] // ADR-0060/0061 (PV-28): the same deferred gestures make the same layout under either Chrome
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

    [Fact] // ADR-0060/0061 (PV-30): the Layout menu is the Mud menu — the groups headed, each choice a radio, the current ones checked, a no-op disabled
    public async Task The_layout_menu_is_the_mud_menu()
    {
        var cut = RenderPivot(RegionAmount with { Rows = [P("Region"), P("Product")] });

        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());

        var frame = cut.Find(".ex-pivot-toolbar-anchor .ex-pivot-popup");
        Assert.Equal("menu", frame.GetAttribute("role"));
        Assert.NotNull(frame.QuerySelector(".mud-ex-pivot-menu"));
        Assert.Equal(["Subtotals", "Grand Totals", "Report Layout"],
            cut.FindAll(".mud-ex-pivot-menu-heading").Select(h => h.TextContent.Trim()));
        var items = MenuItems(cut);
        Assert.Equal(12, items.Count);
        Assert.All(items, item => Assert.Equal("menuitemradio", item.GetAttribute("role")));
        Assert.Equal(["Show all Subtotals at Top of Group", "On for Rows and Columns", "Show in Compact Form"],
            items.Where(i => i.GetAttribute("aria-checked") == "true").Select(i => i.TextContent.Trim()));
        Assert.Equal(["Show all Subtotals at Top of Group", "On for Rows and Columns", "Show in Compact Form"],
            items.Where(i => i.HasAttribute("disabled")).Select(i => i.TextContent.Trim()));
        Assert.DoesNotContain(items, i => i.TextContent.Contains("Blank", StringComparison.Ordinal));
        Assert.Single(cut.FindAll(".ex-pivot-backdrop"));

        await RunMenuAsync(cut, "Show in Tabular Form");

        Assert.Equal(PivotReportForm.Tabular, cut.Instance.CurrentLayout.Form);
        Assert.Equal(["Region", "Product", "Sum of Amount"], HeaderTexts(cut));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal("false", cut.Find(".ex-pivot-layout-button").GetAttribute("aria-expanded"));
    }

    [Fact] // ADR-0039/0060: the Layout menu's first enabled choice takes DOM focus under MudBlazor; Escape closes it
    public async Task The_layout_menu_takes_the_keyboard()
    {
        var cut = RenderPivot(RegionAmount);

        await cut.Find(".ex-pivot-layout-button").ClickAsync(new MouseEventArgs());

        var first = cut.FindComponents<MudPivotButton>().First(b => b.Instance.Class == "mud-ex-pivot-menu-item" && !b.Instance.Disabled);
        Assert.Equal("Do Not Show Subtotals", first.Find("button").TextContent.Trim());
        Assert.Equal((ElementIdOf(first.Instance), false), LastFocus());

        await cut.Find(".ex-pivot-popup").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(RegionAmount.Form, cut.Instance.CurrentLayout.Form);
    }

    [Fact] // ADR-0060/0061 (PV-30): the toolbar is ExPivot's markup holding the Mud band on its left; its toggle hides and shows the Mud pane
    public async Task The_toolbar_holds_the_mud_band_and_toggles_the_pane()
    {
        var cut = RenderPivot(RegionAmount with { Filters = [P("Online")] });

        var toolbar = cut.Find(".ex-pivot-toolbar");
        Assert.NotNull(toolbar.QuerySelector(".ex-pivot-toolbar-start .mud-ex-pivot-filter-button"));
        Assert.Equal(["Layout", "Field List"],
            toolbar.QuerySelectorAll(".ex-pivot-toolbar-end .ex-pivot-toolbar-button")
                .Select(b => (b.QuerySelector("span") ?? b).TextContent.Trim()));
        Assert.Single(cut.FindAll(".mud-ex-pivot-pane"));

        await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".mud-ex-pivot-pane"));
        Assert.Equal("false", cut.Find(".ex-pivot-field-list-toggle").GetAttribute("aria-pressed"));

        await cut.Find(".ex-pivot-field-list-toggle").ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".mud-ex-pivot-pane"));
    }

    [Fact] // ADR-0061/0065 (PV-23): an Aggregation the source does not answer is offered disabled in the Mud panel, with the reason, and never asked for
    public async Task Value_field_settings_offer_unanswered_aggregations_disabled()
    {
        var source = new LimitedSource(PivotSource.From(Sales, Fields), new PivotSourceFeatures([PivotAggregation.Sum, PivotAggregation.Count]));
        var cut = RenderPivot(RegionAmount, source: source);
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");

        var reasons = cut.FindAll(".mud-ex-pivot-not-offered").Select(r => r.TextContent.Trim()).ToArray();
        Assert.Equal(9, reasons.Length);
        Assert.Equal("The source does not answer Average.", reasons[0]);
        var asked = source.Questions;

        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<PivotAggregation>>().Instance.ValueChanged.InvokeAsync(PivotAggregation.Average));
        await cut.Find(".mud-ex-pivot-ok").ClickAsync(new MouseEventArgs());

        Assert.Equal("The source does not answer Average.", cut.Find(".mud-ex-pivot-refusal").TextContent.Trim());
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Equal(PivotAggregation.Sum, cut.Instance.CurrentLayout.Values[0].Aggregation);
        Assert.Equal(asked, source.Questions);
    }

    [Fact] // ADR-0058/0061 (PV-14): Show Details under MudBlazor opens the built-in tab at the report's foot, its grid dressed by the grid Wrapper
    public async Task Show_details_opens_a_tab_dressed_by_the_wrapper()
    {
        var cut = RenderPivot(RegionAmount with { Rows = [P("Region"), P("Product")] });

        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(1, 1)));

        var tabs = cut.Find(".ex-pivot-tabs");
        Assert.Equal(["PivotTable", "Details: East / Apples"], tabs.QuerySelectorAll("[role=tab]").Select(t => t.TextContent.Trim()));
        var details = cut.FindComponent<ExGrid<PivotDetailRecord>>();
        Assert.Same(Grid(cut).Instance.Chrome, details.Instance.Chrome);
        Assert.IsType<MudGridChrome>(details.Instance.Chrome);
    }

    /// <summary>The bundled source with fewer features, counting what it is asked.</summary>
    private sealed class LimitedSource(PivotSource inner, PivotSourceFeatures features) : PivotSource
    {
        public int Questions { get; private set; }

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

        public override ValueTask RefreshAsync(CancellationToken cancellationToken = default) => inner.RefreshAsync(cancellationToken);
    }
}
