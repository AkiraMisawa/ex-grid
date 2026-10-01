using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// Random Change Batches against a plain list of records that follows the rules by hand: whatever
/// the sequence, the Snapshot holds what the list holds, in its order, and each change folds
/// exactly (ADR-0063, ADR-0066).
/// </summary>
public class ModelTests
{
    [Theory] // ADR-0063: after any sequence of batches, a Snapshot holds what the batches say, in order, and folds exactly
    [InlineData(20260930)]
    [InlineData(7)]
    [InlineData(1234567)]
    public void Random_batches_agree_with_a_list(int seed)
    {
        var random = new Random(seed);
        var builder = Trades(new SnapshotTuning(SegmentShift: 3, MaxBatchSegments: 5, BatchRowsDivisor: 2));
        var model = Trades(37).ToList();
        var nextId = 37L;
        var snapshot = builder.Build([.. model]);
        AssertHolds(model, snapshot);
        var merges = 0;
        var compactions = 0;
        var refusals = 0;

        for (var step = 0; step < 250; step++)
        {
            var held = model.Select(t => t.Id).OrderBy(_ => random.Next()).ToList();
            var removed = held.Take(random.Next(0, Math.Min(4, held.Count) + 1)).ToList();
            var changed = held.Skip(removed.Count).Take(random.Next(0, 6))
                .Select(id => Vary(model.Single(t => t.Id == id), random)).ToList();
            var added = Enumerable.Range(0, random.Next(0, 6)).Select(_ => Vary(Make(nextId++), random)).ToList();
            if (removed.Count > 0 && random.Next(4) == 0)
                added.Add(Vary(Make(removed[0]), random));

            if (random.Next(8) == 0)
            {
                var before = Dump(snapshot);
                var refused = builder.Batch(added: added, changed: [.. changed, Make(-1)], removedKeys: removed.Cast<object>());
                Assert.Equal(-1L, Assert.Throws<SnapshotException>(() => snapshot.Apply(refused)).Key);
                Assert.Equal(before, Dump(snapshot));
                refusals++;
            }

            var dump = Dump(snapshot);
            var change = snapshot.Apply(builder.Batch(added: added, changed: changed, removedKeys: removed.Cast<object>()));

            Assert.Equal(dump, Dump(change.Before));
            model.RemoveAll(t => removed.Contains(t.Id));
            foreach (var amended in changed)
                model[model.FindIndex(t => t.Id == amended.Id)] = amended;
            model.AddRange(added);
            AssertHolds(model, change.After);
            AssertFolds(change);
            AssertCodesKept(change);
            if (!change.Compacted)
                AssertShared(change);
            else if (change.After.BaseSegmentCount == change.After.SliceCount)
                compactions++;
            else
                merges++;
            snapshot = change.After;
        }

        Assert.True(merges > 0, "No batch merged the slices batches made.");
        Assert.True(compactions > 0, "No batch compacted the whole Snapshot.");
        Assert.True(refusals > 0, "No batch was refused.");
    }

    /// <summary>A record with every field but its id drawn afresh: new desks now and then, Blanks,
    /// decimals too large for a long, NaN, and times that are not midnights.</summary>
    private static Trade Vary(Trade trade, Random random) => trade with
    {
        Desk = random.Next(10) switch
        {
            0 => null,
            1 => $"Desk {random.Next(1_000)}",
            _ => Desks[random.Next(Desks.Length)],
        },
        Notional = random.Next(12) switch
        {
            0 => null,
            1 => 123_456_789_012_345_678.901234m,
            _ => random.Next(-1_000_000, 1_000_000) / 100m,
        },
        Price = random.Next(10) switch
        {
            0 => null,
            1 => double.NaN,
            _ => random.NextDouble() * 100,
        },
        When = random.Next(6) switch
        {
            0 => null,
            1 => Epoch.AddDays(random.Next(100)),
            _ => Epoch.AddSeconds(random.Next(10_000_000)),
        },
        Live = random.Next(3) switch
        {
            0 => null,
            1 => true,
            _ => false,
        },
    };

    /// <summary>A reader folding the change: subtract what left, add what came, and land on After.</summary>
    private static void AssertFolds(SnapshotChange change)
    {
        Assert.Equal(change.Before.RowCount - change.Removed.Count + change.Added.Count, change.After.RowCount);
        Assert.Equal(
            Sum(change.After, change.After.Rows, "Id"),
            Sum(change.Before, change.Before.Rows, "Id") - Sum(change.Before, change.Removed, "Id") + Sum(change.After, change.Added, "Id"));
        Assert.Equal(
            Sum(change.After, change.After.Rows, "Notional"),
            Sum(change.Before, change.Before.Rows, "Notional") - Sum(change.Before, change.Removed, "Notional") + Sum(change.After, change.Added, "Notional"));
        Assert.All(change.Removed, r => Assert.True(change.Before.Holds(r)));
        Assert.All(change.Added, r => Assert.True(change.After.Holds(r)));
        if (!change.Compacted)
        {
            Assert.All(change.Removed, r => Assert.False(change.After.Holds(r)));
            Assert.All(change.Added, r => Assert.False(change.Before.Holds(r)));
        }
    }

    private static decimal Sum(Snapshot snapshot, IEnumerable<SnapshotRow> rows, string column)
    {
        var target = snapshot[column];
        var sum = 0m;
        foreach (var row in rows)
        {
            sum += snapshot.ValueAt(row, target) switch
            {
                long integer => integer,
                decimal number => number,
                _ => 0m,
            };
        }
        return sum;
    }

    /// <summary>A code means the same text in every version: the dictionary before is where the one after begins.</summary>
    private static void AssertCodesKept(SnapshotChange change)
    {
        var before = ((TextColumn)change.Before["Desk"]).Dictionary;
        var after = ((TextColumn)change.After["Desk"]).Dictionary;
        Assert.True(after.Count >= before.Count);
        Assert.Equal(before, after.Take(before.Count));
    }

    /// <summary>Every slice of Before is a slice of After, holding the very same memory.</summary>
    private static void AssertShared(SnapshotChange change)
    {
        var id = (IntegerColumn)change.Before["Id"];
        for (var s = 0; s < change.Before.SliceCount; s++)
        {
            var before = change.Before.Slice(s).Integers(id);
            var after = change.After.Slice(s).Integers(id);
            Assert.True(Unsafe.AreSame(ref MemoryMarshal.GetReference(before), ref MemoryMarshal.GetReference(after)));
        }
    }
}
