using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>A Snapshot built from columns — the way in the CSV, database and Arrow readers are built on
/// (ADR-0064).</summary>
public class ColumnsBuilderTests
{
    [Fact] // ADR-0064: built from columns a value at a time, a Snapshot holds what the same records built from objects hold
    public void Columns_appended_a_value_at_a_time_hold_what_objects_hold()
    {
        var records = Trades(500);
        var tuning = new SnapshotTuning(SegmentShift: 6);
        var builder = new SnapshotColumnsBuilder { Tuning = tuning };
        var id = builder.Integer("Id");
        var desk = builder.Text("Desk");
        var notional = builder.Decimal("Notional");
        var price = builder.Double("Price");
        var when = builder.Date("When");
        var live = builder.Boolean("Live");
        builder.Key("Id");

        foreach (var trade in records)
        {
            id.Append(trade.Id);
            desk.Append(trade.Desk);
            notional.Append(trade.Notional);
            price.Append(trade.Price);
            if (trade.When is { } clock)
                when.Append(clock);
            else
                when.AppendBlank();
            live.Append(trade.Live);
        }
        var fromColumns = builder.Build();
        var fromObjects = Trades(tuning).Build(records);

        Assert.False(fromColumns.KeepsRecords);
        Assert.Null(fromColumns.RecordAt(fromColumns.Rows[0]));
        Assert.Equal("Id", fromColumns.RecordKey?.Name);
        Assert.Equal(fromObjects.SliceCount, fromColumns.SliceCount);
        foreach (var column in fromObjects.Columns)
            Assert.Equal(Values(fromObjects, column.Name), Values(fromColumns, column.Name));
        Assert.Equal(((TextColumn)fromObjects["Desk"]).Dictionary, ((TextColumn)fromColumns["Desk"]).Dictionary);
    }

    [Fact] // ADR-0064: whole spans append at once, across slices, with their Blanks given as a bit set
    public void Spans_append_across_slices_with_their_blanks()
    {
        var builder = new SnapshotColumnsBuilder { Tuning = new SnapshotTuning(SegmentShift: 3) };
        var price = builder.Double("Price");
        var quantity = builder.Integer("Quantity");
        var live = builder.Boolean("Live");
        var values = Enumerable.Range(0, 23).Select(i => i + 0.5).ToArray();

        price.Append(values.AsSpan(0, 3));
        price.Append(values.AsSpan(3), [(1UL << 3) | (1UL << 8) | (1UL << 19)]);
        quantity.Append([.. Enumerable.Range(0, 23).Select(i => (long)i)], [(1UL << 6) | (1UL << 11) | (1UL << 22)]);
        live.Append([.. Enumerable.Range(0, 23).Select(i => i % 2 == 0)], [(1UL << 6) | (1UL << 11) | (1UL << 22)]);
        var snapshot = builder.Build();

        Assert.Equal(3, snapshot.SliceCount);
        int[] blank = [6, 11, 22];
        Assert.Equal(Enumerable.Range(0, 23).Select(i => blank.Contains(i) ? null : (object)(i + 0.5)), Values(snapshot, "Price"));
        Assert.Equal(Enumerable.Range(0, 23).Select(i => blank.Contains(i) ? null : (object)(long)i), Values(snapshot, "Quantity"));
        Assert.Equal(Enumerable.Range(0, 23).Select(i => blank.Contains(i) ? null : (object)(i % 2 == 0)), Values(snapshot, "Live"));
        // A Blank's slot holds zero, so a plain sum over a span is not changed by it.
        Assert.Equal(0.0, snapshot.Slice(0).Doubles((DoubleColumn)snapshot["Price"])[6]);
        Assert.Equal(0L, snapshot.Slice(2).Integers((IntegerColumn)snapshot["Quantity"])[6]);
    }

    [Fact] // ADR-0064: text given as codes into another producer's dictionary is taken under the Snapshot's rules
    public void Codes_into_another_producers_dictionary_are_taken_under_the_snapshots_rules()
    {
        var builder = new SnapshotColumnsBuilder();
        var region = builder.Text("Region");
        string?[] producer = ["z", "a", null, "a", "AMER", "amer", "unused"];

        region.AppendCodes([4, 1, 3, -1, 2, 5, 0, 4], producer);
        var snapshot = builder.Build();

        // In the order the values first appear in the rows; one text at two codes is one entry; two
        // spellings are two; a null entry and -1 are Blanks; an entry no row uses is not taken.
        Assert.Equal(["AMER", "a", "amer", "z"], ((TextColumn)snapshot["Region"]).Dictionary);
        Assert.Equal([0, 1, 1, -1, -1, 2, 3, 0], Codes(snapshot, "Region"));
        Assert.Equal(["AMER", "a", "a", null, null, "amer", "z", "AMER"], Values(snapshot, "Region"));
    }

    [Fact] // ADR-0064: codes into a producer's dictionary meet the text already held, whichever way it came
    public void Codes_meet_text_appended_another_way()
    {
        var builder = new SnapshotColumnsBuilder { Tuning = new SnapshotTuning(SegmentShift: 1) };
        var region = builder.Text("Region");

        region.Append("a");
        region.AppendCodes([0, 1, 1], ["b", "a"]);
        region.Append("b".AsSpan());
        region.AppendUtf8("c"u8);
        var snapshot = builder.Build();

        Assert.Equal(["a", "b", "c"], ((TextColumn)snapshot["Region"]).Dictionary);
        Assert.Equal([0, 1, 0, 0, 1, 2], Codes(snapshot, "Region"));
    }

    [Fact] // ADR-0064: a code outside the producer's dictionary fails the load, naming the row and the column
    public void A_code_outside_the_dictionary_is_refused()
    {
        var builder = new SnapshotColumnsBuilder();
        var region = builder.Text("Region");
        region.Append("x");

        var refusal = Assert.Throws<SnapshotException>(() => region.AppendCodes([0, 1, 7], ["p", "q"]));

        Assert.Equal(4, refusal.Row);
        Assert.Equal("Region", refusal.Column);
        Assert.Equal("Row 4, column 'Region': the code 7 lies outside the dictionary's 2 entries.", refusal.Message);
        Assert.Contains("refused", Assert.Throws<InvalidOperationException>(() => builder.Build()).Message);
    }

    [Fact] // ADR-0064: text given as UTF-8 is read exactly, and bytes that are not UTF-8 fail the load
    public void Utf8_text_is_read_exactly_and_invalid_bytes_are_refused()
    {
        var builder = new SnapshotColumnsBuilder();
        var city = builder.Text("City");

        city.AppendUtf8("東京"u8);
        city.AppendUtf8("Zürich"u8);
        city.AppendUtf8(""u8);
        city.AppendUtf8("東京"u8);
        var refusal = Assert.Throws<SnapshotException>(() => city.AppendUtf8([0x41, 0xFF, 0xFE]));

        Assert.Equal(5, refusal.Row);
        Assert.Equal("Row 5, column 'City': the text is not valid UTF-8.", refusal.Message);
        Assert.Throws<InvalidOperationException>(() => city.Append("x"));
    }

    [Fact] // ADR-0064: a Decimal given scaled by a power of ten is held exactly
    public void Scaled_decimals_are_held_exactly()
    {
        var builder = new SnapshotColumnsBuilder();
        var notional = builder.Decimal("Notional");

        notional.AppendScaled([12345, -5, 100, long.MinValue], 2, [0b100]);
        notional.Append([1.5m, 2.25m, 0m], [0b100]);
        notional.Append(7m);
        notional.Append((decimal?)null);
        var snapshot = builder.Build();

        Assert.Equal([123.45m, -0.05m, null, -92233720368547758.08m, 1.5m, 2.25m, null, 7m, null], Values(snapshot, "Notional"));
    }

    [Fact] // ADR-0064: dates given as ticks are clock values, and ticks outside a date's range are refused
    public void Ticks_are_clock_values_and_ticks_outside_the_range_are_refused()
    {
        var builder = new SnapshotColumnsBuilder();
        var when = builder.Date("When");
        var noon = new DateTime(2026, 9, 30, 12, 0, 0);

        when.AppendTicks([noon.Ticks, 0, DateTime.MaxValue.Ticks], [0b10]);
        when.Append(new DateOnly(2026, 9, 30));
        when.Append(new DateTimeOffset(noon, TimeSpan.FromHours(-5)));
        var refusal = Assert.Throws<SnapshotException>(() => when.AppendTicks(-1));

        Assert.Equal(6, refusal.Row);
        Assert.Equal("When", refusal.Column);
    }

    [Fact] // ADR-0064: the version a Snapshot carries is the reader's to restore, and 0 otherwise
    public void A_reader_restores_the_version()
    {
        var restored = new SnapshotColumnsBuilder { Version = 42 };
        restored.Integer("Id").Append(1);
        var fresh = new SnapshotColumnsBuilder();
        fresh.Integer("Id").Append(1);

        Assert.Equal(42, restored.Build().Version);
        Assert.Equal(0, fresh.Build().Version);
    }

    [Fact] // ADR-0064: every column holds the same number of rows when the Snapshot is built
    public void Columns_of_different_lengths_are_not_built()
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Integer("Id").Append([1L, 2L, 3L]);
        builder.Text("Desk").Append("EMEA");

        var refusal = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("'Id' 3, 'Desk' 1", refusal.Message);
        Assert.Equal(1, builder.RowCount);
    }

    [Fact] // ADR-0064: a builder builds once
    public void A_builder_builds_once()
    {
        var builder = new SnapshotColumnsBuilder();
        var id = builder.Integer("Id");
        id.Append(1);
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Throws<InvalidOperationException>(() => id.Append(2));
    }

    [Fact] // ADR-0064: a slice's values and Blanks append back as they are, so a Snapshot copies column by column
    public void A_slices_values_and_blanks_append_back_as_they_are()
    {
        var source = Trades(new SnapshotTuning(SegmentShift: 5)).Build(Trades(100));
        var builder = new SnapshotColumnsBuilder();
        var id = builder.Integer("Id");
        var price = builder.Double("Price");
        var live = builder.Boolean("Live");
        var when = builder.Date("When");

        for (var s = 0; s < source.SliceCount; s++)
        {
            var slice = source.Slice(s);
            id.Append(slice.Integers((IntegerColumn)source["Id"]), slice.Blanks(source["Id"]));
            price.Append(slice.Doubles((DoubleColumn)source["Price"]), slice.Blanks(source["Price"]));
            live.Append(slice.Booleans((BooleanColumn)source["Live"]), slice.Blanks(source["Live"]));
            when.AppendTicks(slice.Ticks((DateColumn)source["When"]), slice.Blanks(source["When"]));
        }
        var copy = builder.Build();

        foreach (var name in new[] { "Id", "Price", "Live", "When" })
            Assert.Equal(Values(source, name), Values(copy, name));
    }

    [Fact] // ADR-0064: the Record Key of a reader's load refuses a key carried twice, naming it
    public void A_readers_record_key_refuses_a_key_carried_twice()
    {
        var builder = new SnapshotColumnsBuilder();
        var reference = builder.Text("Ref");
        builder.Key("Ref");
        reference.AppendCodes([0, 1, 2, 1], ["T-1", "T-2", "T-3"]);

        var refusal = Assert.Throws<SnapshotException>(() => builder.Build());

        Assert.Equal("T-2", refusal.Key);
        Assert.Equal(4, refusal.Row);
        Assert.Equal("Row 4, column 'Ref': the Record Key 'T-2' is already carried by row 2.", refusal.Message);
    }

    [Fact] // ADR-0064: a reader's bulk Blanks hold one bit for each value
    public void Bulk_blanks_hold_a_bit_for_each_value()
    {
        var builder = new SnapshotColumnsBuilder();
        var price = builder.Double("Price");

        Assert.Throws<ArgumentException>(() => price.Append(new double[65], [0UL]));
    }
}
