using ExGrid.Data;

namespace PackageSmoke;

/// <summary>
/// ExGrid.Data's README examples, compiled against the packed package as a Consumer takes it
/// (ADR-0042, DA-1). It is not run: that the package restores and its API compiles is what this
/// checks; what a Snapshot holds is its own suite's.
/// </summary>
internal static class DataSmoke
{
    internal sealed record Deal(long Id, string Desk, decimal Notional, DateOnly TradeDate);

    public static async Task<long> Build(IReadOnlyList<Deal> trades, IProgress<SnapshotProgress> progress, CancellationToken token)
    {
        var builder = new SnapshotBuilder<Deal>()
            .Integer("Id", t => t.Id)
            .Text("Desk", t => t.Desk)
            .Decimal("Notional", t => t.Notional)
            .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
            .Key("Id");

        var snapshot = await builder.BuildAsync(trades, new SnapshotLoadOptions { Progress = progress }, token);

        var desk = (TextColumn)snapshot["Desk"];
        var notional = (DecimalColumn)snapshot["Notional"];
        long count = 0;
        for (var s = 0; s < snapshot.SliceCount; s++)
        {
            var slice = snapshot.Slice(s);
            ReadOnlySpan<int> codes = slice.Codes(desk);
            DecimalValues values = slice.Decimals(notional);
            ReadOnlySpan<ulong> removed = slice.Removed;
            count += codes.Length + values.Length + removed.Length;
        }

        ChangeBatch batch = builder.Batch(added: [new Deal(2, "EMEA", 1.5m, new DateOnly(2026, 9, 30))], removedKeys: [1L]);
        SnapshotChange change = snapshot.Apply(batch);
        return count + change.Removed.Count + change.Added.Count + change.After.Version;
    }

    public static async Task<Snapshot> Columns(CancellationToken token)
    {
        var columns = new SnapshotColumnsBuilder(new SnapshotLoadOptions(), token);
        var desk = columns.Text("Desk");
        var price = columns.Double("Price");
        desk.Append("EMEA".AsSpan());
        price.Append(101.5);
        await columns.CheckpointAsync();
        return await columns.BuildAsync();
    }
}
