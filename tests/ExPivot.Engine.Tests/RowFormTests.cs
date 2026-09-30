using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>How the rows are laid out (ADR-0059, "The report's shape"): the three forms,
/// subtotals, grand totals, collapse and Σ Values in rows.</summary>
public class RowFormTests
{
    private static PivotLayout RegionProduct(PivotReportForm form = PivotReportForm.Compact) => new()
    {
        Rows = [P("Region"), P("Product")],
        Values = [Sum("Amount")],
        Form = form,
    };

    [Fact] // ADR-0059: the Compact form — one indented column, a group row carrying its subtotal at the top
    public void Compact_form_with_subtotals_at_the_top()
    {
        var report = Report(RegionProduct());

        Assert.Equal(["Row Labels", "Sum of Amount"], Headers(report));
        Assert.Equal(
        [
            "g [-]East || 180",
            "i   Apples || 130",
            "i   Pears || 50",
            "g [-]North || 10",
            "i   Pears || 10",
            "g [-]West || 90",
            "i   Apples || 70",
            "i   Plums || 20",
            "g [-](blank) || 5",
            "i   Plums || 5",
            "t Grand Total || 285",
        ], Lines(report));
    }

    [Fact] // ADR-0059: subtotals at the bottom are rows of their own, "<item> Total"
    public void Compact_form_with_subtotals_at_the_bottom()
    {
        var report = Report(RegionProduct() with { SubtotalsAtTop = false });

        Assert.Equal(
        [
            "g [-]East ||",
            "i   Apples || 130",
            "i   Pears || 50",
            "s East Total || 180",
            "g [-]North ||",
            "i   Pears || 10",
            "s North Total || 10",
            "g [-]West ||",
            "i   Apples || 70",
            "i   Plums || 20",
            "s West Total || 90",
            "g [-](blank) ||",
            "i   Plums || 5",
            "s (blank) Total || 5",
            "t Grand Total || 285",
        ], Lines(report));
    }

    [Fact] // ADR-0059: a field's subtotals switched off leave its group rows empty
    public void Subtotals_switched_off()
    {
        var layout = RegionProduct() with { Rows = [P("Region") with { Subtotals = false }, P("Product")] };

        Assert.Equal(["g [-]East ||", "i   Apples || 130", "i   Pears || 50"], Lines(Report(layout))[..3]);
    }

    [Fact] // ADR-0059: the grand total row can be switched off
    public void Grand_total_row_off()
        => Assert.DoesNotContain(Lines(Report(RegionProduct() with { GrandTotalRow = false })), line => line.StartsWith('t'));

    [Fact] // ADR-0059: the Outline form — a column per field, group rows, labels in their own field's column
    public void Outline_form()
    {
        var report = Report(RegionProduct(PivotReportForm.Outline));

        Assert.Equal(["Region", "Product", "Sum of Amount"], Headers(report));
        Assert.Equal(
        [
            "g [-]East |  || 180",
            "i  | Apples || 130",
            "i  | Pears || 50",
            "g [-]North |  || 10",
            "i  | Pears || 10",
        ], Lines(report)[..5]);
        Assert.Equal("t Grand Total |  || 285", Lines(report)[^1]);
    }

    [Fact] // ADR-0059: the Tabular form — no group rows; the outer label on its block's first row; subtotals at the bottom
    public void Tabular_form()
    {
        var report = Report(RegionProduct(PivotReportForm.Tabular));

        Assert.Equal(["Region", "Product", "Sum of Amount"], Headers(report));
        Assert.Equal(
        [
            "i [-]East | Apples || 130",
            "i  | Pears || 50",
            "s East Total |  || 180",
            "i [-]North | Pears || 10",
            "s North Total |  || 10",
            "i [-]West | Apples || 70",
            "i  | Plums || 20",
            "s West Total |  || 90",
            "i [-](blank) | Plums || 5",
            "s (blank) Total |  || 5",
            "t Grand Total |  || 285",
        ], Lines(report));
    }

    [Fact] // ADR-0059: the Tabular form puts subtotals at the bottom whatever the setting
    public void Tabular_ignores_subtotals_at_the_top()
        => Assert.Equal(Lines(Report(RegionProduct(PivotReportForm.Tabular))),
            Lines(Report(RegionProduct(PivotReportForm.Tabular) with { SubtotalsAtTop = true })));

    [Fact] // ADR-0059: Repeat Item Labels fills the outer columns of every row
    public void Repeat_item_labels()
    {
        var report = Report(RegionProduct(PivotReportForm.Tabular) with { RepeatItemLabels = true });

        Assert.Equal(["i [-]East | Apples || 130", "i East | Pears || 50", "s East Total |  || 180"], Lines(report)[..3]);
        var outline = Report(RegionProduct(PivotReportForm.Outline) with { RepeatItemLabels = true });
        Assert.Equal(["g [-]East |  || 180", "i East | Apples || 130"], Lines(outline)[..2]);
    }

    [Fact] // ADR-0059: a collapsed Item's row carries its totals, and the Items under it are gone
    public void A_collapsed_item_shows_its_totals()
    {
        var layout = RegionProduct() with
        {
            Rows = [P("Region") with { ToggledItems = [PivotItemKey.Text("East")] }, P("Product")],
        };

        Assert.Equal(["g [+]East || 180", "g [-]North || 10", "i   Pears || 10"], Lines(Report(layout))[..3]);
    }

    [Fact] // ADR-0059: collapsed whatever the subtotal setting, in every form
    public void A_collapsed_item_shows_its_totals_in_every_form()
    {
        var collapsed = P("Region") with { Collapsed = true, Subtotals = false };

        Assert.Equal("g [+]East || 180", Lines(Report(RegionProduct() with { Rows = [collapsed, P("Product")], SubtotalsAtTop = false }))[0]);
        Assert.Equal("g [+]East |  || 180", Lines(Report(RegionProduct(PivotReportForm.Outline) with { Rows = [collapsed, P("Product")] }))[0]);
        Assert.Equal("g [+]East |  || 180", Lines(Report(RegionProduct(PivotReportForm.Tabular) with { Rows = [collapsed, P("Product")] }))[0]);
    }

    [Fact] // ADR-0059: Collapse Entire Field, with one Item expanded again
    public void Collapse_entire_field_with_an_exception()
    {
        var layout = RegionProduct() with
        {
            Rows = [P("Region") with { Collapsed = true, ToggledItems = [PivotItemKey.Text("west")] }, P("Product")],
        };

        Assert.Equal(
        [
            "g [+]East || 180",
            "g [+]North || 10",
            "g [-]West || 90",
            "i   Apples || 70",
            "i   Plums || 20",
            "g [+](blank) || 5",
            "t Grand Total || 285",
        ], Lines(Report(layout)));
    }

    [Fact] // ADR-0059: the innermost field cannot be collapsed and has no button
    public void The_innermost_field_has_no_button()
    {
        var layout = RegionProduct() with { Rows = [P("Region"), P("Product") with { Collapsed = true }] };

        Assert.Equal("i   Apples || 130", Lines(Report(layout))[1]);
        Assert.Null(Report(layout).Rows[1].Labels[0].Toggle);
    }

    [Fact] // ADR-0059: a button names its field and Item, for the edit it makes
    public void A_button_names_its_field_and_item()
    {
        var toggle = Report(RegionProduct()).Rows[0].Labels[0].Toggle!;

        Assert.Equal(new PivotToggle("Region", PivotItemKey.Text("East"), "East", IsCollapsed: false), toggle);
    }

    [Fact] // ADR-0059: Σ Values in Rows — one row per Value Field under each Item; totals per Value Field
    public void Values_in_rows()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount"), Sum("Quantity")],
            ValuesAxis = PivotAxis.Rows,
        };
        var report = Report(layout);

        Assert.Equal(["Row Labels", "Values"], Headers(report));
        Assert.Equal(
        [
            "g East ||",
            "i   Sum of Amount || 180",
            "i   Sum of Quantity || 18",
            "g North ||",
            "i   Sum of Amount || 10",
            "i   Sum of Quantity || 1",
            "g West ||",
            "i   Sum of Amount || 90",
            "i   Sum of Quantity || 9",
            "g (blank) ||",
            "i   Sum of Amount || 5",
            "i   Sum of Quantity || 1",
            "t Total Sum of Amount || 285",
            "t Total Sum of Quantity || 29",
        ], Lines(report));
    }

    [Fact] // ADR-0059: Σ Values in Rows under an outer field: subtotals at the bottom, per Value Field
    public void Values_in_rows_with_an_outer_field()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region"), P("Product")],
            Values = [Sum("Amount"), Sum("Quantity")],
            ValuesAxis = PivotAxis.Rows,
        };
        var lines = Lines(Report(layout));

        Assert.Equal(
        [
            "g [-]East ||",
            "g   Apples ||",
            "i     Sum of Amount || 130",
            "i     Sum of Quantity || 13",
            "g   Pears ||",
            "i     Sum of Amount || 50",
            "i     Sum of Quantity || 5",
            "s East Sum of Amount || 180",
            "s East Sum of Quantity || 18",
        ], lines[..9]);
    }

    [Fact] // ADR-0059: Σ Values in Rows in the Tabular form — a Values column
    public void Values_in_rows_tabular()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount"), Sum("Quantity")],
            ValuesAxis = PivotAxis.Rows,
            Form = PivotReportForm.Tabular,
        };
        var report = Report(layout);

        Assert.Equal(["Region", "Values", "Values"], Headers(report));
        Assert.Equal(
        [
            "i East | Sum of Amount || 180",
            "i  | Sum of Quantity || 18",
        ], Lines(report)[..2]);
        Assert.Equal(["t Grand Total | Sum of Amount || 285", "t Grand Total | Sum of Quantity || 29"], Lines(report)[^2..]);
    }

    [Fact] // ADR-0059: no row field — no label column and one row of totals
    public void No_row_field_is_one_row_of_totals()
    {
        var report = Report(new PivotLayout { Columns = [P("Online")], Values = [Sum("Amount")] });

        Assert.Empty(report.LabelColumns);
        Assert.Equal(["FALSE", "TRUE", "Grand Total"], Headers(report));
        Assert.Equal(["t  || 130 | 155 | 285"], Lines(report));
    }

    [Fact] // ADR-0059: no Value Field — the Items, and no totals
    public void Rows_without_values()
    {
        var report = Report(new PivotLayout { Rows = [P("Region"), P("Product")] });

        Assert.Empty(report.ValueColumns);
        Assert.Equal(["g [-]East ||", "i   Apples ||", "i   Pears ||"], Lines(report)[..3]);
        Assert.DoesNotContain(Lines(report), line => line.StartsWith('t') || line.StartsWith('s'));
    }

    [Fact] // ADR-0059: no field at all is the empty report, which asks for fields
    public void An_empty_layout_is_the_empty_report()
    {
        var report = Report(PivotLayout.Empty);

        Assert.True(report.IsEmpty);
        Assert.Empty(report.Rows);
        Assert.Empty(report.LabelColumns);
        Assert.Empty(report.ValueColumns);
    }

    [Fact] // ADR-0059: a report filter field alone is still the empty report
    public void Filters_alone_are_the_empty_report()
        => Assert.True(Report(new PivotLayout { Filters = [P("Region")] }).IsEmpty);
}
