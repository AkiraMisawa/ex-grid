using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>What applying a Change Batch hands a reader, and what it does to the next version
/// (ADR-0063, ADR-0066).</summary>
public class ChangeBatchTests
{
    private static readonly SnapshotTuning Small = new(SegmentShift: 2);

    [Fact] // ADR-0063: each batch applied moves the version on by one
    public void Each_batch_moves_the_version_on()
    {
        var builder = Trades(Small);
        var first = builder.Build(Trades(5));

        var second = first.Apply(builder.Batch(added: [Make(5)])).After;
        var third = second.Apply(ChangeBatch.Of()).After;

        Assert.Equal([0L, 1L, 2L], new[] { first, second, third }.Select(s => s.Version));
        Assert.Equal(Values(second, "Id"), Values(third, "Id"));
    }

    [Fact] // ADR-0066: a reader folds a batch in — the sum over After is the sum over Before, less Removed, plus Added
    public void Folding_removed_and_added_gives_the_sum_over_after()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(40));

        var change = snapshot.Apply(builder.Batch(
            added: [Make(40), Make(41) with { Notional = 1e20m }],
            changed: [Make(3) with { Notional = 99.99m }, Make(17) with { Notional = null }, Make(33) with { Notional = 0.001m }],
            removedKeys: [5L, 22L]));

        Assert.Equal(
            Sum(change.After, change.After.Rows),
            Sum(change.Before, change.Before.Rows) - Sum(change.Before, change.Removed) + Sum(change.After, change.Added));
        Assert.Equal(change.Before.RowCount - change.Removed.Count + change.Added.Count, change.After.RowCount);

        static decimal Sum(Snapshot snapshot, IEnumerable<SnapshotRow> rows)
            => rows.Sum(r => (decimal?)snapshot.ValueAt(r, snapshot["Notional"]) ?? 0m);
    }

    [Fact] // ADR-0063: Removed are rows Before holds and After does not; Added are rows After holds and Before did not
    public void Removed_and_added_are_rows_that_left_and_came()
    {
        var builder = Trades(Small);
        var records = Trades(12);
        var snapshot = builder.Build(records);
        var amended = records[9] with { Price = -1 };

        var change = snapshot.Apply(builder.Batch(added: [Make(12)], changed: [amended], removedKeys: [4L]));

        Assert.False(change.Compacted);
        Assert.Equal([new SnapshotRow(1, 0), new SnapshotRow(2, 1)], change.Removed);
        Assert.All(change.Removed, r => Assert.True(change.Before.Holds(r) && !change.After.Holds(r)));
        Assert.All(change.Added, r => Assert.True(change.After.Holds(r) && !change.Before.Holds(r)));
        Assert.Equal([new SnapshotRow(3, 0), new SnapshotRow(3, 1)], change.Added);
        Assert.Equal([amended, Make(12)], change.Added.Select(r => (Trade)change.After.RecordAt(r)!));
        Assert.Same(amended, change.After.RecordAt(change.Added[0]));
        Assert.Same(records[4], change.Before.RecordAt(change.Removed[0]));
    }

    [Fact] // ADR-0063: a batch adds its records in segments of their own, and every segment before is shared
    public void A_batch_adds_slices_and_shares_the_rest()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(40));

        var change = snapshot.Apply(builder.Batch(added: Trades(9, first: 40)));

        Assert.False(change.Compacted);
        Assert.Equal(10, snapshot.SliceCount);
        Assert.Equal(13, change.After.SliceCount);
        Assert.Equal([4, 4, 1], Enumerable.Range(10, 3).Select(s => change.After.Slice(s).Length));
        Assert.Equal(Enumerable.Range(0, 49).Select(i => (object)(long)i), Values(change.After, "Id"));
    }

    [Fact] // ADR-0063: once the slices batches made pile up, their rows are merged, and the base is shared as it is
    public void Slices_that_pile_up_are_merged_and_the_base_is_kept()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 2, MaxBatchSegments: 2));
        var records = Trades(16).ToList();
        var snapshot = builder.Build([.. records]);
        var changes = new List<SnapshotChange>();

        for (var round = 0; round < 3; round++)
        {
            var amended = records[round * 5] with { Desk = $"Desk {round}" };
            records[round * 5] = amended;
            var change = snapshot.Apply(builder.Batch(changed: [amended]));
            changes.Add(change);
            snapshot = change.After;
        }

        Assert.Equal([false, false, true], changes.Select(c => c.Compacted));
        var merged = changes[2];
        AssertHolds(records, merged.After);
        Assert.Equal(5, merged.After.SliceCount);
        Assert.Equal(3, merged.After.Slice(4).Length);
        Assert.True(merged.After.Slice(4).Removed.IsEmpty);
        // The base is kept as it is: its slices still leave out the old versions of the changed records.
        Assert.Equal([[1UL], [2UL], [4UL], []], Enumerable.Range(0, 4).Select(s => merged.After.Slice(s).Removed.ToArray()));
        var id = (IntegerColumn)merged.After["Id"];
        Assert.Equal(changes[0].Before.Slice(3).Integers(id).ToArray(), merged.After.Slice(3).Integers(id).ToArray());
        Assert.Equal([new SnapshotRow(4, 2)], merged.Added);
        Assert.Same(records[10], merged.After.RecordAt(merged.Added[0]));
        Assert.Equal(((TextColumn)merged.Before["Desk"]).Dictionary.Append("Desk 2"), ((TextColumn)merged.After["Desk"]).Dictionary);
        // The merged slice's Record Keys are indexed: the next batch finds them.
        var next = merged.After.Apply(builder.Batch(changed: [records[5] with { Live = null }], removedKeys: [0L]));
        Assert.Equal(15, next.After.RowCount);
    }

    [Fact] // ADR-0063: once the rows batches made grow large beside the rest, every row is copied, in order, into a new base; codes are kept, and the change says so
    public void A_compaction_keeps_order_and_codes_and_says_so()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 2));
        var records = Trades(16).ToList();
        var snapshot = builder.Build([.. records]);

        var first = snapshot.Apply(builder.Batch(changed: [records[1] with { Desk = "One" }, records[2] with { Desk = "Two" }]));
        records[1] = (Trade)first.After.RecordAt(first.Added[0])!;
        records[2] = (Trade)first.After.RecordAt(first.Added[1])!;
        var amended = new[] { records[6] with { Desk = "Six" }, records[9] with { Price = 0 }, records[14] with { Live = true } };
        var sixteen = Make(16);
        var compacted = first.After.Apply(builder.Batch(changed: amended, added: [sixteen], removedKeys: [3L]));
        records[6] = amended[0];
        records[9] = amended[1];
        records[14] = amended[2];
        records.RemoveAt(3);
        records.Add(sixteen);

        Assert.False(first.Compacted);
        Assert.True(compacted.Compacted);
        AssertHolds(records, compacted.After);
        Assert.Equal(4, compacted.After.SliceCount);
        Assert.All(Enumerable.Range(0, 4), s => Assert.True(compacted.After.Slice(s).Removed.IsEmpty));
        Assert.Equal(((TextColumn)compacted.Before["Desk"]).Dictionary.Append("Six"), ((TextColumn)compacted.After["Desk"]).Dictionary);
        Assert.Equal(compacted.Before.Version + 1, compacted.After.Version);
        Assert.Equal([new SnapshotRow(1, 1), new SnapshotRow(2, 0), new SnapshotRow(3, 1), new SnapshotRow(3, 3)], compacted.Added);
        Assert.Equal([records[5], records[8], records[13], records[15]], compacted.Added.Select(r => compacted.After.RecordAt(r)));
        // Its Record Keys are indexed anew: the next batch finds them.
        var next = compacted.After.Apply(builder.Batch(changed: [records[14] with { Live = null }], removedKeys: [0L, 16L]));
        Assert.Equal(14, next.After.RowCount);
    }

    [Fact] // ADR-0063: two versions made from one Snapshot each keep the text they brought, and the one before keeps its own
    public void Two_versions_made_from_one_keep_their_own_text()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(4));
        var count = ((TextColumn)snapshot["Desk"]).Dictionary.Count;

        var left = snapshot.Apply(builder.Batch(added: [Make(10) with { Desk = "Left" }])).After;
        var right = snapshot.Apply(builder.Batch(added: [Make(10) with { Desk = "Right" }, Make(11) with { Desk = "Left" }])).After;
        var further = left.Apply(builder.Batch(added: [Make(12) with { Desk = "Further" }])).After;

        Assert.Equal(count, ((TextColumn)snapshot["Desk"]).Dictionary.Count);
        Assert.Equal("Left", ((TextColumn)left["Desk"]).Dictionary[count]);
        Assert.Equal(["Right", "Left"], ((TextColumn)right["Desk"]).Dictionary.Skip(count));
        Assert.Equal(["Left", "Further"], ((TextColumn)further["Desk"]).Dictionary.Skip(count));
        Assert.Equal("Left", Values(left, "Desk")[^1]);
        Assert.Equal(["Right", "Left"], Values(right, "Desk")[^2..]);
        Assert.Equal(["Left", "Further"], Values(further, "Desk")[^2..]);
        Assert.False(((TextColumn)snapshot["Desk"]).Dictionary.TryGetCode("Left", out _));
        Assert.True(((TextColumn)right["Desk"]).Dictionary.TryGetCode("Left", out var code));
        Assert.Equal(count + 1, code);
    }

    [Fact] // ADR-0063: a batch may come from any way in, and its text is taken under the Snapshot's own codes
    public void A_batch_built_from_columns_meets_the_snapshots_codes()
    {
        var columns = new SnapshotColumnsBuilder { Tuning = Small };
        columns.Integer("Id").Append([1L, 2L]);
        columns.Text("Desk").AppendCodes([0, 1], ["EMEA", "AMER"]);
        columns.Decimal("Notional").AppendBlanks(2);
        var snapshot = columns.Build();
        var batch = new SnapshotColumnsBuilder();
        batch.Integer("Id").Append([3L, 4L, 5L]);
        batch.Text("Desk").AppendCodes([0, 1, 0], ["AMER", "APAC"]);
        batch.Decimal("Notional").Append([1m, 2m, 3m]);

        var change = snapshot.Apply(ChangeBatch.Of(added: batch.Build()));

        Assert.Equal(["EMEA", "AMER", "APAC"], ((TextColumn)change.After["Desk"]).Dictionary);
        Assert.Equal([0, 1, 1, 2, 1], Codes(change.After, "Desk"));
        Assert.Equal([null, null, 1m, 2m, 3m], Values(change.After, "Notional"));
    }
}
