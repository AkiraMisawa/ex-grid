using System.Globalization;
using System.Reflection;
using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>A Snapshot is immutable (ADR-0064, DA-2).</summary>
public class ImmutabilityTests
{
    [Fact] // ADR-0064: no public member changes a Snapshot — no setter, no field, and collections that are read-only
    public void No_public_member_changes_a_snapshot()
    {
        Type[] types =
        [
            typeof(Snapshot), typeof(SnapshotColumn), typeof(TextColumn), typeof(DecimalColumn), typeof(DoubleColumn),
            typeof(IntegerColumn), typeof(DateColumn), typeof(BooleanColumn), typeof(TextDictionary), typeof(SnapshotSlice),
            typeof(DecimalValues), typeof(SnapshotChange), typeof(ChangeBatch),
        ];
        foreach (var type in types)
        {
            Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.Instance));
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => p.SetMethod is { IsPublic: true });
        }
        Assert.Equal(
            ["Apply", "Holds", "IsBlank", "RecordAt", "Slice", "TryGetColumn", "ValueAt"],
            typeof(Snapshot).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).Distinct().Order());

        var builder = Trades();
        var snapshot = builder.Build(Trades(10));
        var change = snapshot.Apply(builder.Batch(changed: [Make(1)], removedKeys: [2L]));
        Assert.IsNotType<SnapshotColumn[]>(snapshot.Columns);
        Assert.True(((ICollection<SnapshotColumn>)snapshot.Columns).IsReadOnly);
        Assert.False(snapshot.Rows is ICollection<SnapshotRow> { IsReadOnly: false });
        Assert.False(change.After.Rows is ICollection<SnapshotRow> { IsReadOnly: false });
        Assert.True(((ICollection<SnapshotRow>)change.Removed).IsReadOnly);
        Assert.True(((ICollection<SnapshotRow>)change.Added).IsReadOnly);
        Assert.True(((ICollection<object>)ChangeBatch.Of(removedKeys: [1L]).RemovedKeys).IsReadOnly);
    }

    [Fact] // ADR-0064: after batches, the Snapshot before them reads exactly as before — every row, value, code, record and slice
    public void The_snapshot_before_a_batch_reads_exactly_as_before()
    {
        var builder = Trades(new SnapshotTuning(SegmentShift: 3));
        var snapshot = builder.Build(Trades(50));
        var dump = Dump(snapshot);
        var raw = Raw(snapshot);

        var first = snapshot.Apply(builder.Batch(
            added: [Make(50) with { Desk = "New desk" }],
            changed: [Make(7) with { Notional = 1e25m, When = Fixtures.Epoch.AddSeconds(1) }],
            removedKeys: [9L, 33L]));
        var firstDump = Dump(first.After);
        var firstRaw = Raw(first.After);
        var second = first.After.Apply(builder.Batch(changed: [Make(8)], removedKeys: [7L, 50L]));
        snapshot.Apply(builder.Batch(added: [Make(51) with { Desk = "Other desk" }]));

        Assert.Equal(dump, Dump(snapshot));
        Assert.Equal(raw, Raw(snapshot));
        Assert.Equal(firstDump, Dump(first.After));
        Assert.Equal(firstRaw, Raw(first.After));
        Assert.Equal(47, second.After.RowCount);
    }

    /// <summary>Every span of every slice, as it is stored.</summary>
    private static List<string> Raw(Snapshot snapshot)
    {
        var lines = new List<string>();
        for (var s = 0; s < snapshot.SliceCount; s++)
        {
            var slice = snapshot.Slice(s);
            lines.Add($"{s} removed {Join(slice.Removed.ToArray())}");
            foreach (var column in snapshot.Columns)
            {
                var values = column switch
                {
                    TextColumn text => Join(slice.Codes(text).ToArray()),
                    DecimalColumn number => slice.Decimals(number) is var d && d.Scale >= 0 ? $"x10^{d.Scale} {Join(d.Scaled.ToArray())}" : Join(slice.Decimals(number).Exact.ToArray()),
                    DoubleColumn number => Join(slice.Doubles(number).ToArray().Select(BitConverter.DoubleToInt64Bits).ToArray()),
                    IntegerColumn number => Join(slice.Integers(number).ToArray()),
                    DateColumn date => Join(slice.Ticks(date).ToArray()),
                    BooleanColumn flag => Join(slice.Booleans(flag).ToArray()),
                    _ => "",
                };
                lines.Add($"{s} {column.Name} {values} blanks {Join(slice.Blanks(column).ToArray())}");
            }
        }
        return lines;
    }

    private static string Join<TValue>(TValue[] values)
        where TValue : IFormattable
        => string.Join(",", values.Select(v => v.ToString(null, CultureInfo.InvariantCulture)));

    private static string Join(bool[] values) => string.Join(",", values);
}
