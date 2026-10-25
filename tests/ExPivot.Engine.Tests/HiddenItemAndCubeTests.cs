using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>The cube kept across layouts and the question a layout asks, as the C# surface has them
/// (ADR-0059/0060/0066). Hidden Items, the records behind a cell, what a cube holds, the row sequence and
/// what the engine refuses are held on both engines by the shared cases (<c>tests/cases/pivot/hidden.json</c>).</summary>
public class HiddenItemAndCubeTests
{
    [Fact] // ADR-0059/0060: a cube is reused only for the records and the field list it was aggregated from, by instance
    public void A_cube_is_reused_only_for_its_own_records_and_fields()
    {
        var layout = new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount")] };
        var cube = PivotEngine.Aggregate(Sales, Fields, layout);

        Assert.True(PivotEngine.CanReuse(cube, Sales, Fields, layout with { ValuesAxis = PivotAxis.Rows }));
        Assert.False(PivotEngine.CanReuse(cube, Sales.ToArray(), Fields, layout));
        Assert.False(PivotEngine.CanReuse(cube, Sales, Fields, layout with { Values = [Sum("Quantity")] }));
    }

    [Fact] // ADR-0066 (refined): a field in Filters that hides nothing does not travel in a question
    public void A_filters_field_that_hides_nothing_does_not_travel()
    {
        var layout = new PivotLayout
        {
            Filters = [P("Date"), P("Online") with { HiddenItems = [PivotItemKey.Boolean(false)] }],
            Rows = [P("Region")],
            Values = [Sum("Amount")],
        };

        var query = PivotQuery.For(layout);

        Assert.Equal(["Online"], query.Filters.Select(f => f.Field));
        Assert.Equal([PivotItemKey.Boolean(false)], query.Filters[0].HiddenItems);
        Assert.False(query.Places("Date"));
    }

    [Fact] // ADR-0060: an Aggregation the enum does not name is refused, not read as another
    public void An_unknown_aggregation_is_refused()
        => Assert.Throws<InvalidOperationException>(() => Report(new PivotLayout { Values = [new PivotValueField("Amount", (PivotAggregation)99)] }));

    [Fact] // ADR-0060: KeepingOnly drops fields no longer declared, for a Consumer that expects old views
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

    [Fact] // ADR-0059: many records aggregate in one pass (functional only; timing never gates)
    public void Many_records_aggregate()
    {
        var many = Enumerable.Range(0, 200_000)
            .Select(i => new Sale(i % 3 == 0 ? "East" : "West", "P" + (i % 50), new DateTime(2026, 1, 1).AddDays(i % 365), i % 7, 1, i % 2 == 0))
            .ToArray();
        var report = Report(new PivotLayout { Rows = [P("Region"), P("Product")], Columns = [P("Online")], Values = [Sum("Amount"), Sum("Quantity")] }, many);

        Assert.Equal("200000", report.ValueAt(report.Rows[^1], report.ValueColumns.Count - 1)!.Text);
        Assert.Equal(1 + 50 + 1 + 50 + 1, report.Rows.Count);
    }
}
