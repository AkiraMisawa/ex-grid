using ExGrid.Data;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>A report source answers the shape of the data it pivots, before any report (ADR-0341).</summary>
public class ShapeAnswerTests
{
    private static Snapshot Trades()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Text("desk").Append("Rates");
        builder.Decimal("pnl").Append(1m);
        return builder.Build();
    }

    [Fact] // ADR-0341: a source over a Snapshot answers the Snapshot's shape, records' included, held as one
    public async Task A_local_source_answers_its_snapshots_shape()
    {
        var trades = Trades();
        await using var local = PivotReportSource.From(PivotSource.From(trades));
        await using var records = PivotReportSource.From(PivotSource.From<string>(["a"], PivotFields.Of<string>().Text("Name", s => s)));

        Assert.Equal(DataShape.Of(trades).Columns, (await local.ShapeAsync(TestContext.Current.CancellationToken))!.Columns);
        Assert.Equal([new DataShapeColumn("Name", SnapshotKind.Text)], (await records.ShapeAsync(TestContext.Current.CancellationToken))!.Columns);
    }

    [Fact] // ADR-0341: a fetching source says the shape its server answers, asking for no report; none when given none
    public async Task A_fetching_source_says_its_servers_shape()
    {
        var asked = 0;
        await using var local = PivotReportSource.From(PivotSource.From(Trades()));
        await using var remote = PivotReportSource.Fetch(local.Fields, local.Features, local.UpdateMode, (request, ct) =>
        {
            asked++;
            return local.WindowAsync(request, ct);
        });
        Assert.Null(await remote.ShapeAsync(TestContext.Current.CancellationToken));

        var shape = DataShape.Of(Trades());
        remote.Shape = _ => ValueTask.FromResult(shape);
        var input = new BoardInput("trades", [new("desk", SnapshotKind.Text), new("pnl", SnapshotKind.Decimal)]);

        await input.CheckAsync(remote, TestContext.Current.CancellationToken);
        Assert.Equal(0, asked);
    }
}
