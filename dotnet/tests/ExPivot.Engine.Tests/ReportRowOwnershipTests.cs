using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// A report answers the values of its own rows, and refuses another report's (ADR-0153): a row
/// names a Value Field and an axis node of the report it was laid out for, and read through
/// another report the same names stand for another cell. The rows a report shares with the
/// versions it was made from, or that were made from it, are its own.
/// </summary>
public class ReportRowOwnershipTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // ADR-0153: a row of another report is refused by name, never read as another Value Field's figure
    public void A_row_of_another_report_is_refused_by_name()
    {
        var sums = Report(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        var counts = Report(new PivotLayout { Rows = [P("Region")], Values = [Value("Quantity", PivotAggregation.Count)] });
        var east = sums.Rows[0];
        Assert.Equal("180", sums.ValueAt(east, 0)!.Text);

        var refused = Assert.Throws<ArgumentException>(() => counts.ValueAt(east, 0));

        Assert.Equal("row", refused.ParamName);
        Assert.Contains(east.Key.ToString(), refused.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => counts.ValueFieldAt(east, 0));
        Assert.Throws<ArgumentException>(() => counts.RowPath(east));
        Assert.Throws<ArgumentException>(() => counts.DetailsQuery(east, 0));
        // Its own report still answers it.
        Assert.Equal("180", sums.ValueAt(east, 0)!.Text);
    }

    [Fact] // ADR-0153: two layouts of one cube share its axis nodes, and still refuse each other's rows
    public void A_row_of_another_layout_of_the_same_cube_is_refused()
    {
        var layout = new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount"), Value("Quantity", PivotAggregation.Count)],
            ValuesAxis = PivotAxis.Rows,
        };
        var cube = PivotEngine.Aggregate(Sales, Fields, layout);
        var onRows = PivotEngine.Report(cube, layout, EnUs);
        var onColumns = PivotEngine.Report(cube, layout with { ValuesAxis = PivotAxis.Columns }, EnUs);
        // East's Count of Quantity, a row of the report with the values on rows.
        var count = onRows.Rows.First(row => row.ValueField == 1);
        Assert.Equal("3", onRows.ValueAt(count, 0)!.Text);

        Assert.Throws<ArgumentException>(() => onColumns.ValueAt(count, 0));
        Assert.Throws<ArgumentException>(() => onColumns.ValueAt(onRows.Rows[0], 1));
    }

    private sealed record Trade(long Id, string Desk, decimal Amount);

    [Fact] // ADR-0153: the rows a version shares with the one it was made from are its own, and answer its values
    public async Task Rows_a_version_shares_answer_its_values_and_a_fresh_computation_refuses_them()
    {
        var fields = PivotFields.Of<Trade>().Key("Id", t => t.Id).Text("Desk", t => t.Desk).Number("Amount", t => t.Amount);
        var data = PivotSource.From([new Trade(1, "East", 10m), new Trade(2, "West", 20m)], fields);
        var layout = new PivotLayout { Rows = [P("Desk")], Values = [Sum("Amount") with { NumberFormat = "0" }] };
        using var session = new PivotComputationSession(data);
        var before = (await session.ComputeAsync(layout, cancellationToken: Ct)).Report!;
        data.Apply(fields.Batch(changed: [new Trade(1, "East", 15m)]));
        var after = (await session.ComputeAsync(layout, cancellationToken: Ct)).Report!;
        var east = before.Rows[0];

        Assert.Same(east, after.Rows[0]);
        Assert.Equal("10", before.ValueAt(east, 0)!.Text);
        Assert.Equal("15", after.ValueAt(east, 0)!.Text);
        // The same data and layout, computed afresh: equal rows, another report's.
        var fresh = PivotEngine.Report(PivotEngine.Cube(PivotQuery.For(layout),
            await data.AggregateAsync(PivotQuery.For(layout), Ct), data.Fields), layout);
        Assert.Equal(fresh.Rows[0].Key, east.Key);
        Assert.Throws<ArgumentException>(() => fresh.ValueAt(east, 0));
    }
}
