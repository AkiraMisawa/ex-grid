using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>How the value columns and the rectangles over them are laid out (ADR-0059/0032).</summary>
public class ColumnTests
{
    [Fact] // ADR-0059: one column per Item of the column field, and the grand total column
    public void One_column_field()
    {
        var report = Report(new PivotLayout { Rows = [P("Region")], Columns = [P("Product")], Values = [Sum("Amount")] });

        Assert.Equal(["Row Labels", "Apples", "Pears", "Plums", "Grand Total"], Headers(report));
        Assert.Equal(0, report.HeaderTierCount);
        Assert.Empty(report.HeaderSpans);
        Assert.Equal(
        [
            "i East || 130 | 50 |  | 180",
            "i North ||  | 10 |  | 10",
            "i West || 70 |  | 20 | 90",
            "i (blank) ||  |  | 5 | 5",
            "t Grand Total || 200 | 60 | 25 | 285",
        ], Lines(report));
        Assert.Equal(
            [PivotColumnRole.Item, PivotColumnRole.Item, PivotColumnRole.Item, PivotColumnRole.GrandTotal],
            report.ValueColumns.Select(c => c.Role));
    }

    [Fact] // ADR-0059/0032: an outer column Item is a rectangle over its Items; its subtotal follows outside it
    public void Two_column_fields_with_subtotals()
    {
        var report = Report(new PivotLayout { Rows = [P("Region")], Columns = [P("Online"), P("Product")], Values = [Sum("Amount")] });

        Assert.Equal(
            ["Row Labels", "Apples", "Pears", "FALSE Total", "Apples", "Plums", "TRUE Total", "Grand Total"],
            Headers(report));
        Assert.Equal(1, report.HeaderTierCount);
        Assert.Equal(["FALSE@0+2 t1x1", "TRUE@3+2 t1x1"], Spans(report));
        Assert.Equal("t Grand Total || 70 | 60 | 130 | 130 | 25 | 155 | 285", Lines(report)[^1]);
    }

    [Fact] // ADR-0059: a column field's subtotals switched off, and the grand total column off
    public void Column_subtotals_and_grand_total_off()
    {
        var layout = new PivotLayout
        {
            Columns = [P("Online") with { Subtotals = false }, P("Product")],
            Values = [Sum("Amount")],
            GrandTotalColumn = false,
        };

        Assert.Equal(["Apples", "Pears", "Apples", "Plums"], Headers(Report(layout)));
    }

    [Fact] // ADR-0059: a collapsed column Item is one column, its header standing tall under its parent
    public void A_collapsed_column_item_is_one_column()
    {
        var layout = new PivotLayout
        {
            Columns = [P("Online") with { ToggledItems = [PivotItemKey.Boolean(false)] }, P("Product")],
            Values = [Sum("Amount")],
        };
        var report = Report(layout);

        Assert.Equal(["FALSE", "Apples", "Plums", "TRUE Total", "Grand Total"], Headers(report));
        Assert.Equal(["TRUE@1+2 t1x1"], Spans(report));
        Assert.Equal("t  || 130 | 130 | 25 | 155 | 285", Lines(report)[0]);
    }

    [Fact] // ADR-0059: Σ Values in Columns — each column splits per Value Field under its Item's rectangle
    public void Values_in_columns()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Online")],
            Values = [Sum("Amount"), Sum("Quantity")],
        };
        var report = Report(layout);

        Assert.Equal(
        [
            "Row Labels",
            "Sum of Amount", "Sum of Quantity",
            "Sum of Amount", "Sum of Quantity",
            "Sum of Amount", "Sum of Quantity",
        ], Headers(report));
        Assert.Equal(1, report.HeaderTierCount);
        Assert.Equal(["FALSE@0+2 t1x1", "TRUE@2+2 t1x1", "Grand Total@4+2 t1x1"], Spans(report));
        Assert.Equal("i East || 50 | 5 | 130 | 13 | 180 | 18", Lines(report)[0]);
        Assert.Equal([0, 1, 0, 1, 0, 1], report.ValueColumns.Select(c => c.ValueField));
    }

    [Fact] // ADR-0059: Σ Values in Columns under two fields — subtotals as "<item> Total" over the captions
    public void Values_in_columns_under_two_fields()
    {
        var layout = new PivotLayout
        {
            Columns = [P("Online"), P("Product")],
            Values = [Sum("Amount"), Sum("Quantity")],
        };
        var report = Report(layout);

        Assert.Equal(2, report.HeaderTierCount);
        Assert.Equal(
        [
            "FALSE@0+4 t2x1",
            "FALSE Total@4+2 t2x2",
            "TRUE@6+4 t2x1",
            "TRUE Total@10+2 t2x2",
            "Grand Total@12+2 t2x2",
            "Apples@0+2 t1x1",
            "Pears@2+2 t1x1",
            "Apples@6+2 t1x1",
            "Plums@8+2 t1x1",
        ], Spans(report));
    }

    [Fact] // ADR-0059: no column field — one column per Value Field, headed by its caption
    public void No_column_field()
    {
        var report = Report(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount"), Value("Amount", PivotAggregation.Count)] });

        Assert.Equal(["Row Labels", "Sum of Amount", "Count of Amount"], Headers(report));
        Assert.Equal("i East || 180 | 3", Lines(report)[0]);
    }

    [Fact] // ADR-0059: column names are unique and stable for the same column across reports
    public void Column_names_are_unique_and_stable()
    {
        var layout = new PivotLayout { Columns = [P("Online"), P("Product")], Values = [Sum("Amount"), Sum("Quantity")] };
        var first = Report(layout).ValueColumns.Select(c => c.Name).ToArray();
        var refreshed = Report(layout, Sales.Select(s => s with { Amount = s.Amount + 1 }).ToArray()).ValueColumns.Select(c => c.Name);

        Assert.Equal(first.Length, first.Distinct().Count());
        Assert.Equal(first, refreshed);
        Assert.All(first, name => Assert.StartsWith("v:", name));
    }

    [Fact] // ADR-0059: the label columns and the value columns never share a name
    public void Label_and_value_column_names_differ()
    {
        var report = Report(new PivotLayout
        {
            Rows = [P("Region"), P("Product")],
            Columns = [P("Online")],
            Values = [Sum("Amount")],
            Form = PivotReportForm.Tabular,
        });

        var names = report.LabelColumns.Select(c => c.Name).Concat(report.ValueColumns.Select(c => c.Name)).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Equal(["row:Region", "row:Product"], report.LabelColumns.Select(c => c.Name));
    }
}
