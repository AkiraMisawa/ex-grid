using ExChart.Engine;
using Xunit;
using global::ExGrid.Data;

namespace ExChart.Components.Tests;

public class SnapshotChartDataTests
{
    private sealed record Deal(long Id, string Desk, decimal Notional);

    private static SnapshotBuilder<Deal> Builder() => new SnapshotBuilder<Deal>()
        .Integer("Id", d => d.Id)
        .Text("Desk", d => d.Desk)
        .Decimal("Notional", d => d.Notional)
        .Key("Id");

    [Fact] // ADR-0322: a Chart Spec reads a Snapshot by its columns' names, and the answer names its version
    public async Task A_spec_draws_a_snapshot_by_its_column_names()
    {
        var snapshot = await Builder().BuildAsync([new Deal(1, "EMEA", 2m), new Deal(2, "APAC", 5m)], new SnapshotLoadOptions(), TestContext.Current.CancellationToken);
        var data = new SnapshotChartData(snapshot);
        Assert.Equal(["Id", "Desk", "Notional"], data.Columns);
        Assert.Equal(snapshot.Version, data.Version);
        var reading = ChartSpec.Read("""
            {"version":1,"axis":{"values":"numbers"},"series":[{"id":"d","name":"Deals","kind":"level","data":"deals","x":"Id","y":"Notional"}]}
            """, name => name == "deals" ? data : null);
        var points = reading.Definition.Series[0].Points;
        Assert.Equal([2.0, 5.0], points.Select(p => p.Y!.Value.ToDouble()));
    }

    [Fact] // ADR-0322: a Snapshot's next version is a new one; the chart reads it by being given it
    public async Task The_next_version_is_read_from_the_snapshot_it_is_given()
    {
        var builder = Builder();
        var first = await builder.BuildAsync([new Deal(1, "EMEA", 2m)], new SnapshotLoadOptions(), TestContext.Current.CancellationToken);
        var next = first.Apply(builder.Batch(added: [new Deal(2, "APAC", 5m)], removedKeys: [])).After;
        Assert.Equal(1, new SnapshotChartData(first).RowCount);
        var data = new SnapshotChartData(next);
        Assert.True(data.Version > first.Version);
        Assert.Equal(2, data.RowCount);
    }
}
