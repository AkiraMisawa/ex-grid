using Bunit;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>Filter…, Field Settings…, Value Field Settings… and the report filter band (ADR-0060):
/// drafts held by ExPivot until OK, refusals said in the panel.</summary>
public class PanelTests : PivotTestContext
{
    private static Task ClickAsync(IRenderedComponent<ExPivot<Sale>> cut, string selector)
        => cut.Find(selector).ClickAsync(new MouseEventArgs());

    private static Task TickAsync(IRenderedComponent<ExPivot<Sale>> cut, string label, bool tick)
        => cut.FindAll(".ex-pivot-item").Single(i => i.TextContent.Trim() == label).QuerySelector("input")!
            .ChangeAsync(new ChangeEventArgs { Value = tick });

    [Fact] // ADR-0060: Filter… lists every Item; unticking one and OK hides it
    public async Task Filter_hides_an_item()
    {
        PivotLayout? told = null;
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] },
            ps => ps.Add(p => p.LayoutChanged, layout => told = layout));
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        Assert.Equal(["(Select All)", "East", "North", "West", "(blank)"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim()));
        await TickAsync(cut, "West", false);
        await ClickAsync(cut, ".ex-pivot-ok");

        Assert.Equal([PivotItemKey.Text("West")], told!.Rows[0].HiddenItems);
        Assert.Equal(["East | 180", "North | 10", "(blank) | 5", "Grand Total | 195"], RowTexts(cut));
        Assert.NotNull(AreaElement(cut, "Rows").QuerySelector(".ex-pivot-filtered-mark"));
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0059/0060: unticking every Item disables OK and says why
    public async Task Filter_refuses_to_hide_every_item()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        await TickAsync(cut, "(Select All)", false);

        Assert.True(cut.Find(".ex-pivot-ok").HasAttribute("disabled"));
        Assert.Equal("Select at least one item.", cut.Find(".ex-pivot-refusal").TextContent);
        await TickAsync(cut, "North", true);
        Assert.False(cut.Find(".ex-pivot-ok").HasAttribute("disabled"));
    }

    [Fact] // ADR-0060: the search narrows the list; (Select All) acts on what it leaves
    public async Task Filter_search_narrows_the_items()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");

        await cut.Find(".ex-pivot-item-filter .ex-pivot-search").InputAsync(new ChangeEventArgs { Value = "st" });
        Assert.Equal(["(Select All)", "East", "West"], cut.FindAll(".ex-pivot-item").Select(i => i.TextContent.Trim()));
        await TickAsync(cut, "(Select All)", false);
        await ClickAsync(cut, ".ex-pivot-ok");

        Assert.Equal(["North | 10", "(blank) | 5", "Grand Total | 15"], RowTexts(cut));
    }

    [Fact] // ADR-0060: Cancel drops the draft
    public async Task Cancel_drops_the_draft()
    {
        var told = new List<PivotLayout>();
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")] }, ps => ps.Add(p => p.LayoutChanged, told.Add));
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Filter…");
        await TickAsync(cut, "West", false);

        await ClickAsync(cut, ".ex-pivot-cancel");

        Assert.Empty(told);
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
    }

    [Fact] // ADR-0060: Field Settings… sets subtotals and the order
    public async Task Field_settings_apply_subtotals_and_order()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")], SubtotalsAtTop = false });
        await OpenMenuAsync(cut, "Rows", "Region");
        await RunMenuAsync(cut, "Field Settings…");

        await cut.FindAll(".ex-pivot-field-settings input[type=radio]")[1].ChangeAsync(new ChangeEventArgs { Value = "on" });
        await cut.Find(".ex-pivot-field-settings select").ChangeAsync(new ChangeEventArgs { Value = "3" });
        await ClickAsync(cut, ".ex-pivot-ok");

        var layout = cut.Instance.CurrentLayout;
        Assert.False(layout.Rows[0].Subtotals);
        Assert.Equal(new PivotSort(PivotSortDirection.Descending, 0), layout.Rows[0].Sort);
        Assert.Equal("−East |", RowTexts(cut)[0]);
        Assert.DoesNotContain(RowTexts(cut), text => text.Contains("Total", StringComparison.Ordinal) && !text.StartsWith("Grand", StringComparison.Ordinal));
    }

    [Fact] // ADR-0060: Value Field Settings… — the caption follows the Aggregation until the user writes one
    public async Task Value_field_settings_change_the_aggregation()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");

        await cut.FindAll(".ex-pivot-value-settings select")[0].ChangeAsync(new ChangeEventArgs { Value = "Average" });
        Assert.Equal("Average of Amount", cut.Find(".ex-pivot-value-settings input[type=text]").GetAttribute("value"));
        await cut.FindAll(".ex-pivot-value-settings input[type=text]")[1].InputAsync(new ChangeEventArgs { Value = "N1" });
        Assert.Equal("Sample: -1,234.6", cut.Find(".ex-pivot-sample").TextContent);
        await ClickAsync(cut, ".ex-pivot-ok");

        var value = cut.Instance.CurrentLayout.Values[0];
        Assert.Equal(PivotAggregation.Average, value.Aggregation);
        Assert.Null(value.Caption);
        Assert.Equal("N1", value.NumberFormat);
        Assert.Equal(["Row Labels", "Average of Amount"], HeaderTexts(cut));
        Assert.Equal("East | 60.0", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0059/0060: a caption another field has is refused, and the panel says so and stays
    public async Task Value_field_settings_refuse_a_taken_caption()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Sum("Quantity")] });
        await OpenMenuAsync(cut, "Values", "Sum of Quantity");
        await RunMenuAsync(cut, "Value Field Settings…");

        await cut.Find(".ex-pivot-value-settings input[type=text]").InputAsync(new ChangeEventArgs { Value = "Sum of Amount" });
        await ClickAsync(cut, ".ex-pivot-ok");

        Assert.Equal("PivotTable field name already exists.", cut.Find(".ex-pivot-refusal").TextContent);
        Assert.Single(cut.FindAll(".ex-pivot-popup"));
        Assert.Null(cut.Instance.CurrentLayout.Values[1].Caption);
    }

    [Fact] // ADR-0060: an unusable number format shows in the sample and is refused
    public async Task Value_field_settings_refuse_a_runaway_format()
    {
        var cut = RenderPivot(new PivotLayout { Values = [Sum("Amount")] });
        await OpenMenuAsync(cut, "Values", "Sum of Amount");
        await RunMenuAsync(cut, "Value Field Settings…");

        await cut.FindAll(".ex-pivot-value-settings input[type=text]")[1].InputAsync(new ChangeEventArgs { Value = "N999999999" });
        Assert.Equal("This number format cannot be used.", cut.Find(".ex-pivot-sample").TextContent);
        await ClickAsync(cut, ".ex-pivot-ok");

        Assert.Equal("This number format cannot be used.", cut.Find(".ex-pivot-refusal").TextContent);
        Assert.Null(cut.Instance.CurrentLayout.Values[0].NumberFormat);
    }

    [Fact] // ADR-0060: the report filter band shows (All), then the one Item, then (Multiple Items)
    public async Task The_report_filter_band()
    {
        var cut = RenderPivot(new PivotLayout { Filters = [P("Region")], Values = [Sum("Amount")] });
        Assert.Equal("(All)", cut.Find(".ex-pivot-filter-summary").TextContent);

        await ClickAsync(cut, ".ex-pivot-filter-button");
        Assert.Contains("ex-pivot-popup-overlay", cut.Find(".ex-pivot-popup").ClassName);
        Assert.Single(cut.FindAll(".ex-pivot-backdrop"));
        await TickAsync(cut, "(Select All)", false);
        await TickAsync(cut, "East", true);
        await ClickAsync(cut, ".ex-pivot-ok");
        Assert.Equal("East", cut.Find(".ex-pivot-filter-summary").TextContent);
        Assert.Equal(["180"], RowTexts(cut));

        await ClickAsync(cut, ".ex-pivot-filter-button");
        await TickAsync(cut, "West", true);
        await ClickAsync(cut, ".ex-pivot-ok");
        Assert.Equal("(Multiple Items)", cut.Find(".ex-pivot-filter-summary").TextContent);
        Assert.Empty(cut.FindAll(".ex-pivot-backdrop"));
    }

    [Fact] // ADR-0060: a press on the backdrop closes the band's Filter…
    public async Task The_backdrop_closes_the_bands_filter()
    {
        var cut = RenderPivot(new PivotLayout { Filters = [P("Region")], Values = [Sum("Amount")] });
        await ClickAsync(cut, ".ex-pivot-filter-button");

        await ClickAsync(cut, ".ex-pivot-backdrop");

        Assert.Empty(cut.FindAll(".ex-pivot-popup"));
        Assert.Empty(cut.FindAll(".ex-pivot-backdrop"));
    }
}
