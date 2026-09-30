using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>Hidden Items, the records behind a cell, the cube kept across layouts, the row
/// sequence, and what the engine refuses (ADR-0059/0062).</summary>
public class HiddenItemAndCubeTests
{
    [Fact] // ADR-0059: a hidden row Item leaves every cell and every total
    public void A_hidden_row_item_leaves_the_totals()
    {
        var layout = new PivotLayout { Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }], Values = [Sum("Amount")] };

        Assert.Equal(["i East || 180", "i North || 10", "i (blank) || 5", "t Grand Total || 195"], Lines(Report(layout)));
    }

    [Fact] // ADR-0059: a report filter's Hidden Items filter the report
    public void A_report_filter_filters_the_report()
    {
        var layout = new PivotLayout
        {
            Filters = [P("Product") with { HiddenItems = [PivotItemKey.Text("apples")] }],
            Rows = [P("Region")],
            Values = [Sum("Amount")],
        };

        Assert.Equal(["i East || 50", "i North || 10", "i West || 20", "i (blank) || 5", "t Grand Total || 85"], Lines(Report(layout)));
    }

    [Fact] // ADR-0059: a column Item hidden leaves the column and the row totals
    public void A_hidden_column_item_leaves_its_column_and_the_row_totals()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Columns = [P("Online") with { HiddenItems = [PivotItemKey.Boolean(true)] }],
            Values = [Sum("Amount")],
        };
        var report = Report(layout);

        Assert.Equal(["Row Labels", "FALSE", "Grand Total"], Headers(report));
        Assert.Equal("t Grand Total || 130 | 130", Lines(report)[^1]);
    }

    [Fact] // ADR-0059: the layout holds what is hidden, so an Item a later snapshot brings is shown
    public void A_new_item_is_shown()
    {
        var layout = new PivotLayout { Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")] }], Values = [Sum("Amount")] };
        Sale[] later = [.. Sales, Sales[0] with { Region = "South", Amount = 1 }];

        Assert.Contains("i South || 1", Lines(Report(layout, later)));
    }

    [Fact] // ADR-0059/0060: Filter… lists every Item of the field in its order, hidden or not
    public void The_item_list_holds_every_item_with_its_state()
    {
        var layout = new PivotLayout
        {
            Filters = [P("Product") with { HiddenItems = [PivotItemKey.Text("Pears")] }],
            Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")], Sort = PivotSort.Descending }],
            Values = [Sum("Amount")],
        };
        var cube = PivotEngine.Aggregate(Sales, Fields, layout);

        Assert.Equal(
            ["West (hidden)", "North", "East", "(blank)"],
            PivotEngine.ItemsOf(cube, layout, "Region", EnUs).Select(i => i.Label + (i.IsHidden ? " (hidden)" : "")));
        // Items are listed over the whole snapshot: Pears is hidden, and North (only Pears) is still listed.
        Assert.Equal(["Apples", "Pears", "Plums"], PivotEngine.ItemsOf(cube, layout, "Product", EnUs).Select(i => i.Label));
        Assert.Throws<ArgumentException>(() => PivotEngine.ItemsOf(cube, layout, "Amount"));
    }

    [Fact] // ADR-0062: Show Details — a value cell's records, in their order in the snapshot
    public void The_records_behind_a_value_cell()
    {
        var layout = new PivotLayout { Rows = [P("Region")], Columns = [P("Product")], Values = [Sum("Amount")] };
        var report = Report(layout);
        var east = report.Rows[0];

        Assert.Equal([Sales[0], Sales[2]], PivotEngine.RecordsBehind(Sales, Fields, report, east, 0));
        Assert.Equal([Sales[0], Sales[1], Sales[2]], PivotEngine.RecordsBehind(Sales, Fields, report, east, -1));
        Assert.Equal([Sales[0], Sales[1], Sales[2]], PivotEngine.RecordsBehind(Sales, Fields, report, east, 3));
        Assert.Equal(Sales, PivotEngine.RecordsBehind(Sales, Fields, report, report.Rows[^1], 3));
    }

    [Fact] // ADR-0062/0059: a Hidden Item's records are never behind a cell
    public void Hidden_records_are_never_behind_a_cell()
    {
        var layout = new PivotLayout
        {
            Filters = [P("Online") with { HiddenItems = [PivotItemKey.Boolean(false)] }],
            Rows = [P("Region")],
            Values = [Sum("Amount")],
        };
        var report = Report(layout);

        Assert.Equal([Sales[0], Sales[2]], PivotEngine.RecordsBehind(Sales, Fields, report, report.Rows[0], 0));
        Assert.Equal(4, PivotEngine.RecordsBehind(Sales, Fields, report, report.Rows[^1], 0).Count);
    }

    [Fact] // ADR-0059: laying out again — collapse, order, form, totals, Aggregation, format — needs no new cube
    public void The_cube_holds_every_layout_of_the_same_fields()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount")] };
        var cube = PivotEngine.Aggregate(Sales, Fields, layout);

        Assert.True(cube.Holds(layout with { Rows = [P("Region") with { Collapsed = true, Sort = PivotSort.Descending }, P("Product")] }));
        Assert.True(cube.Holds(layout with { Form = PivotReportForm.Tabular, GrandTotalRow = false, SubtotalsAtTop = false }));
        Assert.True(cube.Holds(layout with { Values = [Value("Amount", PivotAggregation.Average) with { NumberFormat = "N2" }] }));
        Assert.True(cube.Holds(layout with { Values = [] }));
        Assert.True(PivotEngine.CanReuse(cube, Sales, Fields, layout with { ValuesAxis = PivotAxis.Rows }));

        Assert.False(cube.Holds(layout with { Rows = [P("Product"), P("Region")] }));
        Assert.False(cube.Holds(layout with { Rows = [P("Region") with { HiddenItems = [PivotItemKey.Blank] }, P("Product")] }));
        Assert.False(cube.Holds(layout with { Values = [Sum("Quantity")] }));
        Assert.False(cube.Holds(layout with { Filters = [P("Date")] }));
        Assert.False(PivotEngine.CanReuse(cube, Sales.ToArray(), Fields, layout));
        Assert.Throws<InvalidOperationException>(() => PivotEngine.Report(cube, layout with { Values = [Sum("Quantity")] }));
    }

    [Fact] // ADR-0059: a report laid out from a kept cube is the report computed from scratch
    public void A_kept_cube_lays_out_the_same_report()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount")] };
        var cube = PivotEngine.Aggregate(Sales, Fields, layout);
        var next = layout with { Rows = [P("Region") with { Collapsed = true }, P("Product")], Form = PivotReportForm.Outline };

        Assert.Equal(Lines(Report(next)), Lines(PivotEngine.Report(cube, next, EnUs)));
    }

    [Fact] // ADR-0058/0011: new values on the same rows keep the row sequence; a structural change does not
    public void The_row_sequence_survives_a_refresh_of_values_only()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Values = [Sum("Amount")] };
        var report = Report(layout);
        var refreshed = Report(layout, Sales.Select(s => s with { Amount = s.Amount * 2, Region = s.Region?.ToUpperInvariant() }).ToArray());

        Assert.True(report.HasSameRowsAs(refreshed));
        Assert.False(report.HasSameRowsAs(Report(layout with { Rows = [P("Region") with { Collapsed = true }, P("Product")] })));
        Assert.False(report.HasSameRowsAs(Report(layout, Sales[..6])));
        Assert.False(report.HasSameRowsAs(null));
    }

    [Fact] // ADR-0059: a layout naming an undeclared field, or a field in two Areas, is refused by name
    public void An_impossible_layout_is_refused_by_name()
    {
        var unknown = Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout { Rows = [P("Desk")] }));
        Assert.Contains("'Desk'", unknown.Message);
        var twice = Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout { Rows = [P("Region")], Filters = [P("Region")] }));
        Assert.Contains("'Region'", twice.Message);
        Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout
        {
            Rows = [P("Region") with { Sort = new PivotSort(ByValue: 1) }],
            Values = [Sum("Amount")],
        }));
        Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout { Values = [new PivotValueField("Amount", (PivotAggregation)99)] }));
    }

    [Fact] // ADR-0059: two fields of one name are refused — a layout could not say which it means
    public void Two_fields_of_one_name_are_refused()
    {
        PivotField<Sale>[] fields = [.. Fields, new("Region", PivotFieldType.Text, s => s.Product)];

        Assert.Throws<ArgumentException>(() => PivotEngine.Compute(Sales, fields, RowsBy("Region")));
    }

    [Fact] // ADR-0059: a field's own format is held to the same rules as a Value Field's
    public void A_fields_runaway_format_is_refused_at_declaration()
    {
        Assert.Throws<ArgumentException>(() => new PivotField<Sale>("Amount", PivotFieldType.Number, s => s.Amount, format: "N999999999"));
        Assert.Throws<ArgumentException>(() => new PivotField<Sale>("Date", PivotFieldType.Date, s => s.Date, format: "%"));
        _ = new PivotField<Sale>("Date", PivotFieldType.Date, s => s.Date, format: "yyyy-MM-dd");
    }

    [Fact] // ADR-0059: KeepingOnly drops fields no longer declared, for a Consumer that expects old views
    public void Keeping_only_declared_fields()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Desk"), P("Region")],
            Values = [Sum("Amount"), Sum("Pnl")],
            Columns = [P("Region2")],
        };

        var kept = layout.KeepingOnly(Fields.Select(f => f.Name));

        Assert.Equal(["Region"], kept.Rows.Select(p => p.Field));
        Assert.Empty(kept.Columns);
        Assert.Equal(["Amount"], kept.Values.Select(v => v.Field));
    }

    [Fact] // ADR-0058: many records aggregate in one pass (functional only; timing never gates)
    public void Many_records_aggregate()
    {
        var many = Enumerable.Range(0, 200_000)
            .Select(i => new Sale(i % 3 == 0 ? "East" : "West", "P" + (i % 50), new DateTime(2026, 1, 1).AddDays(i % 365), i % 7, 1, i % 2 == 0))
            .ToArray();
        var report = Report(new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount"), Sum("Quantity")] }, many);

        Assert.Equal("200000", report.Rows[^1].ValueAt(report.ValueColumns.Count - 1)!.Text);
        Assert.Equal(1 + 50 + 1 + 50 + 1, report.Rows.Count);
    }
}
