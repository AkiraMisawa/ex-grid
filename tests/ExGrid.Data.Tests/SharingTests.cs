using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>The next Snapshot shares every part a batch did not touch (ADR-0063, DA-12).</summary>
public class SharingTests
{
    [Fact] // ADR-0063: the next Snapshot shares every segment the batch did not touch — the very same memory
    public void The_next_snapshot_shares_the_segments_before_it()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 4));
        var snapshot = builder.Build(Trades(100));

        var after = snapshot.Apply(builder.Batch(added: [Make(100)], changed: [Make(20) with { Price = 1 }])).After;

        for (var s = 0; s < snapshot.SliceCount; s++)
        {
            var old = snapshot.Slice(s);
            var next = after.Slice(s);
            Assert.True(Same(old.Integers((IntegerColumn)snapshot["Id"]), next.Integers((IntegerColumn)after["Id"])));
            Assert.True(Same(old.Codes((TextColumn)snapshot["Desk"]), next.Codes((TextColumn)after["Desk"])));
            Assert.True(Same(old.Decimals((DecimalColumn)snapshot["Notional"]).Scaled, next.Decimals((DecimalColumn)after["Notional"]).Scaled));
            Assert.True(Same(old.Doubles((DoubleColumn)snapshot["Price"]), next.Doubles((DoubleColumn)after["Price"])));
            Assert.True(Same(old.Ticks((DateColumn)snapshot["When"]), next.Ticks((DateColumn)after["When"])));
            Assert.True(Same(old.Booleans((BooleanColumn)snapshot["Live"]), next.Booleans((BooleanColumn)after["Live"])));
        }
        Assert.Equal(snapshot.SliceCount + 1, after.SliceCount);
    }

    [Fact] // ADR-0063: a column read from one version reads the same column in the next
    public void A_column_of_one_version_reads_the_next()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 4));
        var snapshot = builder.Build(Trades(40));
        var after = snapshot.Apply(builder.Batch(added: [Make(40)])).After;

        var price = (DoubleColumn)snapshot["Price"];

        Assert.Equal(after.Slice(3).Doubles((DoubleColumn)after["Price"]).ToArray(), after.Slice(3).Doubles(price).ToArray());
        Assert.Equal(60.0, after.ValueAt(after.Rows[^1], price));
    }

    [Fact] // ADR-0063: the rows a version leaves out belong to that version, not to the segment it shares
    public void The_rows_left_out_belong_to_the_version()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 4));
        var snapshot = builder.Build(Trades(100));

        var after = snapshot.Apply(builder.Batch(changed: [Make(20) with { Price = 1 }], removedKeys: [23L])).After;

        Assert.True(snapshot.Slice(1).Removed.IsEmpty);
        Assert.Equal(16, snapshot.Slice(1).HeldCount);
        Assert.Equal([(1UL << 4) | (1UL << 7)], after.Slice(1).Removed.ToArray());
        Assert.Equal(14, after.Slice(1).HeldCount);
        Assert.True(after.Slice(0).Removed.IsEmpty);
        Assert.True(snapshot.Holds(new SnapshotRow(1, 4)));
        Assert.False(after.Holds(new SnapshotRow(1, 4)));
        Assert.Throws<ArgumentException>(() => after.RecordAt(new SnapshotRow(1, 4)));
    }

    [Fact] // ADR-0063: applying 1,000 changes to 1,000,000 records allocates in proportion to the changes, not the records
    public void A_thousand_changes_to_a_million_records_allocate_for_the_changes()
    {
        Measure(10_000);

        var million = Measure(1_000_000);
        var hundredThousand = Measure(100_000);

        // The records' columns take some 45 MB at a million; the changes, a kilobyte each at most.
        Assert.True(million < 1_000 * 1_024, $"1,000 changes to 1,000,000 records allocated {million:N0} bytes.");
        // Ten times the records, and the same changes cost the same.
        Assert.True(million < hundredThousand + (64 * 1_024), $"1,000 changes allocated {million:N0} bytes at 1,000,000 records and {hundredThousand:N0} at 100,000.");
    }

    /// <summary>The bytes one thread allocates applying 1,000 changes spread over <paramref name="count"/>
    /// records: 500 changed, 250 added and 250 removed.</summary>
    private static long Measure(int count)
    {
        var builder = Trades();
        var snapshot = builder.Build(Trades(count));
        var batch = builder.Batch(
            added: Trades(250, first: count),
            changed: [.. Enumerable.Range(0, 500).Select(i => Make(1 + (i * (count / 500))) with { Price = -i })],
            removedKeys: [.. Enumerable.Range(0, 250).Select(i => (object)(3L + (i * (count / 250))))]);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var change = snapshot.Apply(batch);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.False(change.Compacted);
        Assert.Equal(count, change.After.RowCount);
        Assert.Equal(750, change.Removed.Count);
        Assert.Equal(750, change.Added.Count);
        return allocated;
    }

    private static bool Same<TValue>(ReadOnlySpan<TValue> a, ReadOnlySpan<TValue> b)
        => a.Length == b.Length && Unsafe.AreSame(ref MemoryMarshal.GetReference(a), ref MemoryMarshal.GetReference(b));
}
