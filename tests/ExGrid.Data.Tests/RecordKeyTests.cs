using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>The Record Key and the Change Batch (ADR-0063, DA-11): every clause a named test.</summary>
public class RecordKeyTests
{
    private static readonly SnapshotTuning Small = new(SegmentShift: 2);

    [Fact] // ADR-0063: only a Text or an Integer column can be the Record Key
    public void Only_a_text_or_an_integer_column_can_be_the_key()
    {
        var builder = new SnapshotBuilder<Trade>().Integer("Id", t => t.Id).Text("Desk", t => t.Desk).Double("Price", t => t.Price);
        var columns = new SnapshotColumnsBuilder();
        columns.Date("When");

        Assert.Contains("'Price' is Double", Assert.Throws<ArgumentException>(() => builder.Key("Price")).Message);
        Assert.Contains("'Missing'", Assert.Throws<ArgumentException>(() => builder.Key("Missing")).Message);
        Assert.Contains("'When' is Date", Assert.Throws<ArgumentException>(() => columns.Key("When")).Message);
        Assert.Equal("Desk", builder.Key("Desk").Build([Make(0)]).RecordKey?.Name);
    }

    [Fact] // ADR-0063: two records under one key are refused, naming the key
    public void Two_records_under_one_key_are_refused_naming_the_key()
    {
        var text = new SnapshotBuilder<Trade>().Text("Ref", t => $"T-{t.Id % 4}").Key("Ref");
        var integer = new SnapshotBuilder<Trade>().Integer("Id", t => t.Id % 3).Key("Id");

        var byText = Assert.Throws<SnapshotException>(() => text.Build(Trades(6)));
        var byInteger = Assert.Throws<SnapshotException>(() => integer.Build(Trades(6)));

        Assert.Equal("T-0", byText.Key);
        Assert.Equal(5, byText.Row);
        Assert.Equal("Row 5, column 'Ref': the Record Key 'T-0' is already carried by row 1.", byText.Message);
        Assert.Equal(0L, byInteger.Key);
        Assert.Equal("Row 4, column 'Id': the Record Key 0 is already carried by row 1.", byInteger.Message);
    }

    [Fact] // ADR-0063: a Blank key is refused, naming its row
    public void A_blank_key_is_refused()
    {
        var builder = new SnapshotBuilder<Trade>().Text("Desk", t => t.Desk).Key("Desk");

        var refusal = Assert.Throws<SnapshotException>(() => builder.Build(Trades(5)));

        Assert.Equal("Row 4, column 'Desk': the Record Key is Blank.", refusal.Message);
    }

    [Fact] // ADR-0063: a batch that changes a key the Snapshot does not hold is refused whole, by name, and nothing of it is applied
    public void A_batch_changing_a_missing_key_is_refused_whole()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(10));
        var before = Dump(snapshot);
        var batch = builder.Batch(added: [Make(100) with { Desk = "NEW" }], changed: [Make(3) with { Price = 9 }, Make(42)], removedKeys: [5L]);

        var refusal = Assert.Throws<SnapshotException>(() => snapshot.Apply(batch));

        Assert.Equal(42L, refusal.Key);
        Assert.Equal("Id", refusal.Column);
        Assert.Equal("Column 'Id': the batch changes the key 42, which the Snapshot does not hold.", refusal.Message);
        Assert.Equal(before, Dump(snapshot));
        Assert.False(((TextColumn)snapshot["Desk"]).Dictionary.TryGetCode("NEW", out _));
    }

    [Fact] // ADR-0063: a batch that removes a key the Snapshot does not hold is refused whole, by name
    public void A_batch_removing_a_missing_key_is_refused_whole()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(10));
        var before = Dump(snapshot);

        var refusal = Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(added: [Make(10)], removedKeys: [1L, 77L])));

        Assert.Equal(77L, refusal.Key);
        Assert.Equal("Column 'Id': the batch removes the key 77, which the Snapshot does not hold.", refusal.Message);
        Assert.Equal(before, Dump(snapshot));
    }

    [Fact] // ADR-0063: a batch that adds a key the Snapshot already holds is refused whole, by name
    public void A_batch_adding_a_held_key_is_refused_whole()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(10));
        var before = Dump(snapshot);

        var refusal = Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(added: [Make(10), Make(4)])));

        Assert.Equal(4L, refusal.Key);
        Assert.Equal("Column 'Id': the batch adds the key 4, which the Snapshot already holds.", refusal.Message);
        Assert.Equal(before, Dump(snapshot));
    }

    [Fact] // ADR-0063: a key removed earlier is not held, so a later batch that changes it is refused
    public void A_key_removed_earlier_is_not_held()
    {
        var builder = Trades(Small);
        var removed = builder.Build(Trades(10)).Apply(builder.Batch(removedKeys: [3L])).After;

        Assert.Equal(3L, Assert.Throws<SnapshotException>(() => removed.Apply(builder.Batch(changed: [Make(3)]))).Key);
        Assert.Equal(3L, Assert.Throws<SnapshotException>(() => removed.Apply(builder.Batch(removedKeys: [3L]))).Key);
        Assert.Equal(10, removed.Apply(builder.Batch(added: [Make(3)])).After.RowCount);
    }

    [Fact] // ADR-0063: a batch names each key once in each role, and never both changes and removes one
    public void A_batch_names_each_key_once()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(10));

        Assert.Contains("adds the key 20 twice", Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(added: Unkeyed([Make(20), Make(20)])))).Message);
        Assert.Contains("changes the key 2 twice", Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(changed: Unkeyed([Make(2), Make(2)])))).Message);
        Assert.Contains("removes the key 2 twice", Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(removedKeys: [2L, 2]))).Message);
        Assert.Contains("both changes and removes the key 2", Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(changed: [Make(2)], removedKeys: [2L]))).Message);
        Assert.Contains("adds the key 2, which the Snapshot already holds", Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(added: [Make(2)], changed: [Make(2)]))).Message);
    }

    [Fact] // ADR-0063: a batch's key is never Blank; a changed or added record without one is refused, naming its row
    public void A_blank_key_in_a_batch_is_refused()
    {
        var snapshot = Trades(Small).Build(Trades(4));
        var changed = new SnapshotBuilder<Trade>()
            .Integer("Id", t => t.Id == 2 ? null : t.Id).Text("Desk", t => t.Desk).Decimal("Notional", t => t.Notional)
            .Double("Price", t => t.Price).Date("When", t => t.When).Boolean("Live", t => t.Live)
            .Build([Make(1), Make(2)]);

        var refusal = Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(changed: changed)));

        Assert.Equal("Row 2, column 'Id': the Record Key of this changed record is Blank.", refusal.Message);
    }

    [Fact] // ADR-0063: a removed key is of the Record Key's kind
    public void A_removed_key_of_the_other_kind_is_refused()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(4));

        var refusal = Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(removedKeys: ["1"])));

        Assert.Equal("1", refusal.Key);
        Assert.Equal("Column 'Id': the batch removes the key '1', which is not of the Record Key's kind, Integer.", refusal.Message);
        Assert.Throws<ArgumentException>(() => ChangeBatch.Of(removedKeys: [1.5]));
        Assert.Throws<ArgumentException>(() => ChangeBatch.Of(removedKeys: [null!]));
    }

    [Fact] // ADR-0063: any integer type names an Integer key
    public void Any_integer_type_names_an_integer_key()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(10));

        var change = snapshot.Apply(ChangeBatch.Of(removedKeys: [1, (byte)2, (short)3, 4u, 5UL]));

        Assert.Equal([0L, 6L, 7L, 8L, 9L], Values(change.After, "Id"));
    }

    [Fact] // ADR-0063: a changed record keeps its place in the order, and an added record goes at the end
    public void A_changed_record_keeps_its_place_and_an_added_one_goes_at_the_end()
    {
        var builder = Trades(Small);
        var records = Trades(10);
        var snapshot = builder.Build(records);
        var amended = records[6] with { Price = 1234.5, Desk = "Rates" };
        var fresh = Make(10);

        var change = snapshot.Apply(builder.Batch(added: [fresh], changed: [amended], removedKeys: [2L]));

        var model = records.ToList();
        model[6] = amended;
        model.RemoveAt(2);
        model.Add(fresh);
        AssertHolds(model, change.After);
        AssertHolds(records, change.Before);
    }

    [Fact] // ADR-0063: a key removed and added in one batch is a new record, which goes at the end
    public void A_key_removed_and_added_in_one_batch_goes_at_the_end()
    {
        var builder = Trades(Small);
        var records = Trades(6);
        var snapshot = builder.Build(records);
        var again = records[1] with { Price = 2 };

        var change = snapshot.Apply(builder.Batch(added: [again], removedKeys: [1L]));

        AssertHolds([records[0], records[2], records[3], records[4], records[5], again], change.After);
    }

    [Fact] // ADR-0063: a dictionary only grows: a code means the same text in every version, and text no record carries stays
    public void A_dictionary_only_grows()
    {
        var builder = new SnapshotBuilder<Trade>().Integer("Id", t => t.Id).Text("Desk", t => t.Desk).Key("Id");
        var snapshot = builder.Build([Make(1) with { Desk = "EMEA" }, Make(2) with { Desk = "AMER" }]);

        var first = snapshot.Apply(builder.Batch(changed: [Make(1) with { Desk = "APAC" }], removedKeys: [2L]));
        var second = first.After.Apply(builder.Batch(added: [Make(3) with { Desk = "AMER" }, Make(4) with { Desk = "LATAM" }]));

        Assert.Equal(["EMEA", "AMER"], ((TextColumn)first.Before["Desk"]).Dictionary);
        Assert.Equal(["EMEA", "AMER", "APAC"], ((TextColumn)first.After["Desk"]).Dictionary);
        Assert.Equal(["EMEA", "AMER", "APAC", "LATAM"], ((TextColumn)second.After["Desk"]).Dictionary);
        Assert.Equal([0, 1], Codes(first.Before, "Desk"));
        Assert.Equal([2], Codes(first.After, "Desk"));
        Assert.Equal([2, 1, 3], Codes(second.After, "Desk"));
    }

    [Fact] // ADR-0063: a Snapshot without a Record Key takes only batches that add
    public void A_snapshot_without_a_key_takes_only_batches_that_add()
    {
        var builder = UnkeyedTrades(Small);
        var snapshot = builder.Build(Trades(3));

        var changing = Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(changed: [Make(1)])));
        var removing = Assert.Throws<SnapshotException>(() => snapshot.Apply(builder.Batch(removedKeys: [1L])));
        var change = snapshot.Apply(builder.Batch(added: [Make(1), Make(7)]));

        Assert.Null(snapshot.RecordKey);
        Assert.Equal("The Snapshot has no Record Key, so it takes only a batch that adds, and this batch changes or removes records.", changing.Message);
        Assert.Equal(changing.Message, removing.Message);
        Assert.Equal([0L, 1L, 2L, 1L, 7L], Values(change.After, "Id"));
    }

    [Fact] // ADR-0063: a batch's records have the Snapshot's columns, matched by name and kind
    public void A_batch_with_other_columns_is_refused_naming_the_column()
    {
        var snapshot = Trades(Small).Build(Trades(4));
        var fewer = new SnapshotBuilder<Trade>().Integer("Id", t => t.Id).Build([Make(9)]);
        var sameColumns = Trades().Build([Make(9)]);
        var other = new SnapshotBuilder<Trade>()
            .Integer("Id", t => t.Id).Text("Desk", t => t.Desk).Decimal("Notional", t => t.Notional)
            .Decimal("Price", t => (decimal?)t.Price).Date("When", t => t.When).Boolean("Live", t => t.Live)
            .Build([Make(9)]);

        Assert.Equal("Desk", Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(added: fewer))).Column);
        Assert.Equal("Column 'Price': the column is Double in the Snapshot and Decimal in the batch's added records.",
            Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(added: other))).Message);
        Assert.Equal(5, snapshot.Apply(ChangeBatch.Of(added: sameColumns)).After.RowCount);
    }

    [Fact] // ADR-0063: a Snapshot that keeps its records takes a batch whose records are kept, of the same type
    public void A_snapshot_that_keeps_records_takes_a_batch_that_keeps_them()
    {
        var snapshot = Trades(Small).Build(Trades(4));
        var columns = new SnapshotColumnsBuilder();
        columns.Integer("Id").Append(9);
        columns.Text("Desk").Append("x");
        columns.Decimal("Notional").AppendBlank();
        columns.Double("Price").AppendBlank();
        columns.Date("When").AppendBlank();
        columns.Boolean("Live").AppendBlank();
        var withoutRecords = columns.Build();

        var refusal = Assert.Throws<SnapshotException>(() => snapshot.Apply(ChangeBatch.Of(added: withoutRecords)));

        Assert.Contains("keeps its records", refusal.Message);
    }

    /// <summary>Trades built with no key declared, so a batch may carry a key twice for Apply to refuse.</summary>
    private static Snapshot Unkeyed(Trade[] trades)
        => new SnapshotBuilder<Trade>()
            .Integer("Id", t => t.Id).Text("Desk", t => t.Desk).Decimal("Notional", t => t.Notional)
            .Double("Price", t => t.Price).Date("When", t => t.When).Boolean("Live", t => t.Live)
            .Build(trades);
}
