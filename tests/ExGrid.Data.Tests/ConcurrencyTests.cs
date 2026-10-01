using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>A Snapshot is immutable, so it is read from several threads at once — the indexes it makes
/// on first read included — and batches may be applied to one version from several threads (ADR-0063).</summary>
public class ConcurrencyTests
{
    [Fact] // ADR-0063: a Snapshot is read from several threads at once, the index it makes on first read included
    public async Task A_snapshot_is_read_from_several_threads_at_once()
    {
        var records = Trades(5_000);
        var added = Trades(100, first: 5_000);
        var expected = Dump(Changed(records, added));
        var snapshot = Changed(records, added);

        var reads = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => Dump(snapshot), TestContext.Current.CancellationToken)));

        Assert.All(reads, read => Assert.Equal(expected, read));
    }

    [Fact] // ADR-0063: batches applied to one Snapshot from several threads each make their own next version
    public async Task Batches_applied_at_once_each_make_their_own_version()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 4));
        var snapshot = builder.Build(Trades(200));
        var before = Dump(snapshot);
        var count = ((TextColumn)snapshot["Desk"]).Dictionary.Count;

        var changes = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(
            () => snapshot.Apply(builder.Batch(added: [Make(1_000 + i) with { Desk = $"Added {i}" }], changed: [Make(i * 10) with { Desk = $"Changed {i}" }])),
            TestContext.Current.CancellationToken)));

        Assert.Equal(before, Dump(snapshot));
        for (var i = 0; i < changes.Length; i++)
        {
            var after = changes[i].After;
            Assert.Equal($"Changed {i}", Values(after, "Desk")[i * 10]);
            Assert.Equal($"Added {i}", Values(after, "Desk")[^1]);
            Assert.Equal([$"Changed {i}", $"Added {i}"], ((TextColumn)after["Desk"]).Dictionary.Skip(count));
        }
    }

    /// <summary>A Snapshot of these records, built and then changed, so its order has to be indexed.</summary>
    private static Snapshot Changed(Trade[] records, Trade[] added)
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 6));
        return builder.Build(records)
            .Apply(builder.Batch(
                added: added,
                changed: [.. records.Where(t => t.Id % 7 == 0)],
                removedKeys: [.. records.Where(t => t.Id % 11 == 3 && t.Id % 7 != 0).Select(t => (object)t.Id)]))
            .After;
    }
}
