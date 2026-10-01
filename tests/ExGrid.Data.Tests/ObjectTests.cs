using System.Collections.ObjectModel;
using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>A Snapshot built from the Consumer's objects (ADR-0063, DA-4).</summary>
public class ObjectTests
{
    [Fact] // ADR-0063: built from objects, a Snapshot keeps the objects themselves, by reference and in order
    public void Records_are_kept_by_reference_in_order()
    {
        var records = Trades(1_000);

        var snapshot = Trades(new SnapshotTuning(SegmentShift: 6)).Build(records);

        Assert.True(snapshot.KeepsRecords);
        Assert.Equal(16, snapshot.SliceCount);
        AssertHolds(records, snapshot);
    }

    [Fact] // ADR-0063: the records may come as an array, a list, or any read-only list, and read the same
    public void Records_read_the_same_whatever_list_holds_them()
    {
        var records = Trades(3_000);
        var builder = Trades(new SnapshotTuning(SegmentShift: 10));

        AssertHolds(records, builder.Build(records));
        AssertHolds(records, builder.Build(records.ToList()));
        AssertHolds(records, builder.Build(new ReadOnlyCollection<Trade>(records)));
    }

    [Fact] // ADR-0063: an array of a derived type is read as it is
    public void An_array_of_a_derived_type_is_read()
    {
        IReadOnlyList<object> records = new[] { "a", "b" };

        var snapshot = new SnapshotBuilder<object>().Text("V", o => (string)o).Build(records);

        Assert.Equal(["a", "b"], Values(snapshot, "V"));
        Assert.Same(records[1], snapshot.RecordAt(snapshot.Rows[1]));
    }

    [Fact] // ADR-0063: records of a value type are kept as they are, and handed back boxed
    public void Records_of_a_value_type_are_kept()
    {
        var snapshot = new SnapshotBuilder<(int Id, string Name)>()
            .Integer("Id", r => r.Id)
            .Text("Name", r => r.Name)
            .Build([(1, "one"), (2, "two")]);

        Assert.Equal((2, "two"), snapshot.RecordAt(snapshot.Rows[1]));
    }

    [Fact] // ADR-0063: typed accessors box no value — a build allocates little beyond the columns' own arrays
    public void Typed_accessors_box_no_value()
    {
        const int count = 100_000;
        var records = Trades(count);
        var builder = Trades();
        builder.Build(Trades(5_000));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var snapshot = builder.Build(records);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // What the Snapshot holds for each row: the Id (8 bytes), the Desk's code (4), the Notional
        // (8), the Price (8), the When (8), the Live flag (1) and the record's reference (8); beside
        // them, the Record Key's index at 4 bytes a slot, two slots a row rounded up to a power of
        // two, and a Blank bit per row for each column that has Blanks.
        const long columns = count * (8L + 4 + 8 + 8 + 8 + 1 + 8);
        const long keyIndex = 4L << 18;
        const long blanks = 5L * count / 8;
        const long held = columns + keyIndex + blanks;
        // A box for each value would be 24 bytes for each of six values a row: 14.4 MB.
        Assert.True(allocated < held + (256 * 1024), $"The build allocated {allocated:N0} bytes for {held:N0} bytes of columns.");
        Assert.Equal(count, snapshot.RowCount);
    }

    [Fact] // ADR-0063: the declaration is reused, and every build is a Snapshot of its own
    public void Each_build_is_a_snapshot_of_its_own()
    {
        var builder = Trades();

        var first = builder.Build(Trades(3));
        var second = builder.Build(Trades(5));

        Assert.Equal(3, first.RowCount);
        Assert.Equal(5, second.RowCount);
        var refusal = Assert.Throws<ArgumentException>(() => second.ValueAt(second.Rows[0], first["Id"]));
        Assert.Contains("'Id'", refusal.Message);
    }

    [Fact] // ADR-0063: a column's name is unique within the Snapshot
    public void A_name_is_declared_once()
    {
        var builder = new SnapshotBuilder<Trade>().Integer("Id", t => t.Id);

        var refusal = Assert.Throws<ArgumentException>(() => builder.Text("Id", t => t.Desk));
        Assert.Contains("'Id'", refusal.Message);
        Assert.Throws<ArgumentException>(() => builder.Text("", t => t.Desk));
    }

    [Fact] // ADR-0063: no records, no rows — and still the declared columns
    public void An_empty_build_has_its_columns_and_no_rows()
    {
        var snapshot = Trades().Build([]);

        Assert.Equal(0, snapshot.RowCount);
        Assert.Equal(0, snapshot.SliceCount);
        Assert.Empty(snapshot.Rows);
        Assert.Equal(6, snapshot.Columns.Count);
        Assert.Equal("Id", snapshot.RecordKey?.Name);
    }
}
