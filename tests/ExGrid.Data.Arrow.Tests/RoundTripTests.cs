using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>A Snapshot written and read back through ExGrid.Data.Arrow is the Snapshot it was (DA-13).</summary>
public class RoundTripTests
{
    [Fact] // ADR-0065: a Snapshot round-trips whole, column by column and Blank by Blank, with its captions, Record Key and version
    public async Task Every_kind_round_trips_with_its_blanks_captions_key_and_version()
    {
        var deals = Make(500);
        var builder = Deals();
        var snapshot = builder.Build(deals);
        // Two batches that change nothing move the version on, so the version that travels is not 0.
        snapshot = snapshot.Apply(builder.Batch(changed: [deals[0]])).After;
        snapshot = snapshot.Apply(builder.Batch(changed: [deals[1]])).After;

        var read = await ReadEveryWayAsync(await WriteAsync(snapshot));

        Assert.Equal(2, read.Version);
        Assert.Equal("Id", read.RecordKey?.Name);
        Assert.Equal(["Id", "Trading desk", "Notional", "Price (USD)", "Quantity", "Trade date", "When", "Live"], read.Columns.Select(c => c.Caption));
        AssertSame(snapshot, read);
        // Every kind holds a Blank, and the Blank is not "" or 0.
        foreach (var column in snapshot.Columns.Where(c => c.Name != "Id"))
            Assert.Contains(true, Blanks(read, column.Name));
        Assert.Contains("''", Values(read, "Desk"));
        Assert.Equal("0", Values(read, "Quantity")[499]);
        Assert.False(Blanks(read, "Quantity")[499]);
    }

    [Fact] // ADR-0065: a fresh Snapshot round-trips with its dictionary in the same order, codes and all
    public async Task A_fresh_snapshot_keeps_its_dictionary_and_codes()
    {
        var snapshot = Deals().Build(Make(64));

        var read = await ReadAsync(await WriteAsync(snapshot));

        Assert.Equal(Dictionary(snapshot, "Desk"), Dictionary(read, "Desk"));
        var desk = (TextColumn)snapshot["Desk"];
        var readDesk = (TextColumn)read["Desk"];
        Assert.Equal(snapshot.Slice(0).Codes(desk).ToArray(), read.Slice(0).Codes(readDesk).ToArray());
    }

    [Fact] // ADR-0065: a Snapshot after Change Batches writes the rows it holds, in its order, at its version
    public async Task A_snapshot_after_change_batches_writes_what_it_holds()
    {
        var deals = Make(2_000);
        var builder = Deals();
        var before = builder.Build(deals);
        var changed = deals.Where(d => d.Id % 97 == 0).Select(d => d with { Desk = "CHANGED", Notional = 0.001m, When = null }).ToArray();
        var added = Make(25, first: 10_001);
        var removed = deals.Where(d => d.Id % 89 == 0).Select(d => (object)d.Id).ToArray();
        var after = before.Apply(builder.Batch(added: added, changed: changed, removedKeys: removed)).After;
        after = after.Apply(builder.Batch(added: Make(3, first: 20_001))).After;

        var read = await ReadEveryWayAsync(await WriteAsync(after, batchRows: 333));

        // The rows the version holds, in its order — a changed record in its place, an added one at the end.
        AssertSame(after, read, sameDictionaries: false);
        Assert.Equal(after.RowCount, read.RowCount);
        Assert.Equal("'CHANGED'", Values(read, "Desk")[Array.IndexOf(Values(read, "Id"), "97")]);
        Assert.Equal("20003", Values(read, "Id")[^1]);
        // Read back, the dictionary is the texts the rows hold, in the order they first appear in them.
        var texts = Values(after, "Desk").Where(v => v != "∅").Distinct().ToArray();
        Assert.Equal(texts, Dictionary(read, "Desk").Select(t => $"'{t}'"));
    }

    [Fact] // ADR-0065: rows split over several record batches, and batches that split the runs a batch left, read back in order
    public async Task Several_record_batches_round_trip()
    {
        var deals = Make(1_000);
        var builder = Deals();
        var snapshot = builder.Build(deals);
        snapshot = snapshot.Apply(builder.Batch(changed: deals.Where(d => d.Id % 10 == 3).Select(d => d with { Price = -1 }), removedKeys: [5L, 6L, 7L])).After;

        foreach (var batchRows in new[] { 1, 7, 64, 997, 1_000, 65_536 })
            AssertSame(snapshot, await ReadAsync(await WriteAsync(snapshot, batchRows)), sameDictionaries: false);
    }

    [Fact] // ADR-0065: a Snapshot of no rows round-trips its columns, captions, key and version
    public async Task An_empty_snapshot_round_trips()
    {
        var builder = Deals();
        var snapshot = builder.Build(Make(3));
        snapshot = snapshot.Apply(builder.Batch(removedKeys: [1L, 2L, 3L])).After;

        var read = await ReadEveryWayAsync(await WriteAsync(snapshot));

        Assert.Equal(0, read.RowCount);
        Assert.Equal(1, read.Version);
        AssertSame(snapshot, read, sameDictionaries: false);
        Assert.Empty(Dictionary(read, "Desk"));
    }

    [Fact] // ADR-0065: Decimal slices held at different scales, and as decimals, are written at the largest scale and read back exact
    public async Task Decimals_held_at_different_scales_round_trip_exactly()
    {
        var builder = Deals();
        var deals = Make(10).Select(d => d with { Notional = d.Id * 1.25m }).ToArray();
        var snapshot = builder.Build(deals);
        // A batch adds a slice at four places, and another a slice held as decimals, past a long.
        snapshot = snapshot.Apply(builder.Batch(added: [One(11) with { Notional = 0.0001m }, One(12) with { Notional = -1234.5678m }])).After;
        snapshot = snapshot.Apply(builder.Batch(added: [One(13) with { Notional = 79_228_162_514_264_337_593_543.950335m }, One(14) with { Notional = 1.5m }])).After;

        var payload = await WriteAsync(snapshot);
        var read = await ReadAsync(payload);

        AssertSame(snapshot, read);
        Assert.Equal(Shown(1.25m, 2.5m, 3.75m, 5m, 6.25m, 7.5m, 8.75m, 10m, 11.25m, 12.5m, 0.0001m, -1234.5678m, 79_228_162_514_264_337_593_543.950335m, 1.5m), Values(read, "Notional"));
        using var arrow = new Apache.Arrow.Ipc.ArrowStreamReader(payload);
        Assert.Equal("decimal128(38, 6)", ArrowTypeNames.Of(arrow.Schema.GetFieldByName("Notional").DataType));
    }

    [Fact] // ADR-0065: a double keeps its bits — NaN, both infinities, negative zero, the smallest subnormal
    public async Task Doubles_keep_their_bits()
    {
        double?[] values = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.0, 0.0, double.Epsilon, double.MaxValue, null, BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0123)];
        var snapshot = new SnapshotBuilder<double?>().Double("Value", v => v).Build(values);

        var read = await ReadAsync(await WriteAsync(snapshot));

        AssertSame(snapshot, read);
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + "d", Values(read, "Value")[3]);
    }

    [Fact] // ADR-0065: text round-trips exactly — two spellings two values, the empty string a value and not a Blank
    public async Task Text_round_trips_exactly()
    {
        string?[] values = ["amer", "AMER", "", null, " ", "Zürich", "東京", "🚀", new string('x', 70_000), "amer", "\0", "a\r\nb"];
        var snapshot = new SnapshotBuilder<string?>().Text("Value", v => v).Build(values);

        var read = await ReadEveryWayAsync(await WriteAsync(snapshot));

        AssertSame(snapshot, read);
        Assert.Equal(["amer", "AMER", "", " ", "Zürich", "東京", "🚀", new string('x', 70_000), "\0", "a\r\nb"], Dictionary(read, "Value"));
        Assert.Equal([false, false, false, true, false, false, false, false, false, false, false, false], Blanks(read, "Value"));
    }

    [Fact] // ADR-0065: the extreme integers and dates round-trip
    public async Task Extreme_integers_and_dates_round_trip()
    {
        (long? Number, DateTime? When)[] values =
        [
            (long.MinValue, DateTime.MinValue),
            (long.MaxValue, new DateTime(9999, 12, 31, 23, 59, 59, 999)),
            (0, null),
            (null, new DateTime(1969, 12, 31, 23, 59, 59, 1)),
        ];
        var snapshot = new SnapshotBuilder<(long? Number, DateTime? When)>()
            .Integer("Number", v => v.Number)
            .Date("When", v => v.When)
            .Build(values);

        var read = await ReadAsync(await WriteAsync(snapshot));

        AssertSame(snapshot, read);
    }
}
