using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>The Record Key and the Change Batch (ADR-0064, DA-11): the clauses the shared cases
/// (<c>tests/cases/data/change-batch.json</c>) cannot write — a key built from records, any integer type, a
/// batch that keeps records.</summary>
public class RecordKeyTests
{
    private static readonly SnapshotTuning Small = new(SegmentShift: 2);

    [Fact] // ADR-0064: only a Text or an Integer column can be the Record Key
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

    [Fact] // ADR-0064: two records under one key are refused, naming the key
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

    [Fact] // ADR-0064: a Blank key is refused, naming its row
    public void A_blank_key_is_refused()
    {
        var builder = new SnapshotBuilder<Trade>().Text("Desk", t => t.Desk).Key("Desk");

        var refusal = Assert.Throws<SnapshotException>(() => builder.Build(Trades(5)));

        Assert.Equal("Row 4, column 'Desk': the Record Key is Blank.", refusal.Message);
    }

    [Fact] // ADR-0064: any integer type names an Integer key
    public void Any_integer_type_names_an_integer_key()
    {
        var builder = Trades(Small);
        var snapshot = builder.Build(Trades(10));

        var change = snapshot.Apply(ChangeBatch.Of(removedKeys: [1, (byte)2, (short)3, 4u, 5UL]));

        Assert.Equal([0L, 6L, 7L, 8L, 9L], Values(change.After, "Id"));
    }

    [Fact] // ADR-0064: a Snapshot that keeps its records takes a batch whose records are kept, of the same type
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
}
