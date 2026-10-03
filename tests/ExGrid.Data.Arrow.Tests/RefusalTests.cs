using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// Every refusal of a read, by name (DA-14): a type outside ADR-0065's table names its column and
/// the type; a value a kind cannot hold names its row and its column; bytes that are not a whole
/// Arrow stream or file say so. A refused read yields no Snapshot.
/// </summary>
public class RefusalTests
{
    // ---- types outside the table -------------------------------------------------------------

    public static TheoryData<string> Outside =>
    [
        "list<int64>", "large_list<int64>", "struct<a: int64>", "map<utf8, int64>", "binary", "large_binary",
        "binary_view", "fixed_size_binary[16]", "duration[ms]", "month_interval", "float16", "null",
        "dictionary<values=int64, indices=int32>",
    ];

    [Theory] // ADR-0065: a type outside the table — lists, structs, maps, binary, durations, intervals and the rest — is refused, naming the column and the type
    [MemberData(nameof(Outside))]
    public async Task A_type_outside_the_table_is_refused_by_name(string type)
    {
        var payload = Stream(Batch(("Fine", Utf8("a", "b")), ("Value", ArrayOf(type))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Value", refusal.Column);
        Assert.Null(refusal.Row);
        Assert.Equal($"Column 'Value': the Arrow type {type} is not one a Snapshot reads.", refusal.Message);
    }

    public static TheoryData<string> OtherZones => ["Asia/Tokyo", "+09:00", "Europe/London", "America/New_York", "-05:00", "Etc/GMT+5"];

    [Theory] // ADR-0065: a timestamp in a time zone other than UTC — one that is UTC's clock only in winter, or an offset of its own under Etc/ — is refused, naming the column and the zone
    [MemberData(nameof(OtherZones))]
    public async Task A_timestamp_in_another_zone_is_refused(string zone)
    {
        var payload = Stream(Batch(("When", Raw<long>(new TimestampType(TimeUnit.Microsecond, zone), 0L, null))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("When", refusal.Column);
        Assert.Equal(
            $"Column 'When': the Arrow type timestamp[us, tz={zone}] is a timestamp in the time zone '{zone}', which a Snapshot does not read: "
            + "converting it would need a time zone database. Write it in UTC, or as clock values without a time zone.",
            refusal.Message);
    }

    // ---- values a kind cannot hold, by row and column -----------------------------------------

    [Fact] // ADR-0065: a decimal128 beyond decimal's range is refused, naming its row and column — the row counted across record batches
    public async Task A_decimal128_beyond_decimals_range_is_refused_by_row_and_column()
    {
        var type = new Decimal128Type(38, 0);
        var beyond = Int128.One << 96;
        var payload = Stream(
            Batch(("Notional", Decimals(type, Words(1), Words(2), null))),
            Batch(("Notional", Decimals(type, Words(3), Words(-beyond)))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal(5, refusal.Row);
        Assert.Equal("Notional", refusal.Column);
        Assert.Equal("Row 5, column 'Notional': the decimal128(38, 0) value -79228162514264337593543950336 lies beyond the range of a decimal.", refusal.Message);
    }

    [Fact] // ADR-0065: a decimal with more significant places than a decimal's 28 is refused, not rounded
    public async Task A_decimal_with_more_than_28_places_is_refused_not_rounded()
    {
        var payload = Stream(Batch(("Rate", Decimals(new Decimal128Type(38, 30), Words(Int128.Parse("1500000000000000000000000000000")), Words(1)))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 2, column 'Rate': the decimal128(38, 30) value 0.000000000000000000000000000001 has more decimal places than the 28 a decimal holds.", refusal.Message);
    }

    [Fact] // ADR-0065: a decimal256 beyond decimal's range is refused by row and column
    public async Task A_decimal256_beyond_decimals_range_is_refused()
    {
        var huge = System.Numerics.BigInteger.Pow(10, 40);
        var words = new long[4];
        huge.TryWriteBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan()), out _, isUnsigned: false, isBigEndian: false);
        var payload = Stream(Batch(("Wide", Decimals(new Decimal256Type(76, 2), Words(5, 4), words))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 2, column 'Wide': the decimal256(76, 2) value 100000000000000000000000000000000000000.00 lies beyond the range of a decimal.", refusal.Message);
    }

    public static TheoryData<string, string> NarrowDecimalsNoDecimalHolds => new()
    {
        { "decimal32(9, -25)", "the decimal32(9, -25) value 9999999990000000000000000000000000 lies beyond the range of a decimal." },
        { "decimal64(18, -15)", "the decimal64(18, -15) value -100000000000000000000000000000000 lies beyond the range of a decimal." },
        { "decimal32(9, 30)", "the decimal32(9, 30) value 0.000000000000000000000000000015 has more decimal places than the 28 a decimal holds." },
        { "decimal64(18, 29)", "the decimal64(18, 29) value -0.00000000000000000000000000123 has more decimal places than the 28 a decimal holds." },
    };

    [Theory] // ADR-0065: a decimal32 or decimal64 beyond decimal's range, or with more places than its 28, is refused by row and column, not rounded
    [MemberData(nameof(NarrowDecimalsNoDecimalHolds))]
    public async Task A_decimal32_or_decimal64_no_decimal_holds_is_refused_by_row_and_column(string type, string reason)
    {
        IArrowArray array = type switch
        {
            "decimal32(9, -25)" => Raw<int>(new Decimal32Type(9, -25), 1, null, 999_999_999),
            "decimal64(18, -15)" => Raw<long>(new Decimal64Type(18, -15), 1L, null, -100_000_000_000_000_000L),
            "decimal32(9, 30)" => Raw<int>(new Decimal32Type(9, 30), 1_000, null, 15),
            _ => Raw<long>(new Decimal64Type(18, 29), 10L, null, -123L),
        };

        var refusal = await RefusalAsync(Stream(Batch(("Notional", array))));

        Assert.Equal(3, refusal.Row);
        Assert.Equal("Notional", refusal.Column);
        Assert.Equal($"Row 3, column 'Notional': {reason}", refusal.Message);
    }

    [Fact] // ADR-0065: a uint64 above long's range is refused by row and column
    public async Task A_uint64_beyond_longs_range_is_refused()
    {
        var payload = Stream(Batch(("Count", Raw<ulong>(UInt64Type.Default, 1UL, null, (ulong)long.MaxValue + 1))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 3, column 'Count': the uint64 value 9223372036854775808 lies beyond the range of a 64-bit integer.", refusal.Message);
    }

    public static TheoryData<string, string> DatesOutOfRange => new()
    {
        { "date32", "the date32 value -719163 (days since 1970-01-01) lies outside the range of a date, 0001-01-01 to 9999-12-31." },
        { "date64", "the date64 value 253402300800000 (milliseconds since 1970-01-01) lies outside the range of a date, 0001-01-01 to 9999-12-31." },
        { "timestamp[s]", "the timestamp[s] value -62135596801 (seconds since 1970-01-01) lies outside the range of a date, 0001-01-01 to 9999-12-31." },
        { "timestamp[us, tz=UTC]", "the timestamp[us, tz=UTC] value 253402300800000000 (microseconds since 1970-01-01) lies outside the range of a date, 0001-01-01 to 9999-12-31." },
    };

    [Theory] // ADR-0065: a date outside a date's range is refused by row and column, not moved
    [MemberData(nameof(DatesOutOfRange))]
    public async Task A_date_outside_a_dates_range_is_refused(string type, string reason)
    {
        IArrowArray array = type switch
        {
            "date32" => Raw<int>(Date32Type.Default, 0, -719_163),
            "date64" => Raw<long>(Date64Type.Default, 0L, 253_402_300_800_000L),
            "timestamp[s]" => Raw<long>(new TimestampType(TimeUnit.Second, (string?)null), 0L, -62_135_596_801L),
            _ => Raw<long>(new TimestampType(TimeUnit.Microsecond, "UTC"), 0L, 253_402_300_800_000_000L),
        };

        var refusal = await RefusalAsync(Stream(Batch(("When", array))));

        Assert.Equal($"Row 2, column 'When': {reason}", refusal.Message);
    }

    [Fact] // ADR-0065: a nanosecond timestamp finer than a date's 100 nanoseconds is refused, not truncated
    public async Task A_timestamp_finer_than_100_nanoseconds_is_refused()
    {
        var payload = Stream(Batch(("When", Raw<long>(new TimestampType(TimeUnit.Nanosecond, (string?)null), 100L, 150L))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 2, column 'When': the timestamp[ns] value 150 is finer than the 100 nanoseconds a date holds.", refusal.Message);
    }

    public static TheoryData<string, string> TimesOutsideADay => new()
    {
        { "time32[s]", "the time32[s] value 86400 (seconds since midnight) lies outside a day, 00:00:00 to 23:59:59.9999999." },
        { "time32[ms]", "the time32[ms] value -1 (milliseconds since midnight) lies outside a day, 00:00:00 to 23:59:59.9999999." },
        { "time64[us]", "the time64[us] value 86400000000 (microseconds since midnight) lies outside a day, 00:00:00 to 23:59:59.9999999." },
        { "time64[ns]", "the time64[ns] value -100 (nanoseconds since midnight) lies outside a day, 00:00:00 to 23:59:59.9999999." },
    };

    [Theory] // ADR-0065: a time outside a day — a whole day, a leap second, or before midnight — is refused by row and column, not moved to another day
    [MemberData(nameof(TimesOutsideADay))]
    public async Task A_time_outside_a_day_is_refused_by_row_and_column(string type, string reason)
    {
        IArrowArray array = type switch
        {
            "time32[s]" => new Time32Array.Builder(new Time32Type(TimeUnit.Second)).Append(86_399).AppendNull().Append(86_400).Build(),
            "time32[ms]" => new Time32Array.Builder(new Time32Type(TimeUnit.Millisecond)).Append(0).AppendNull().Append(-1).Build(),
            "time64[us]" => new Time64Array.Builder(new Time64Type(TimeUnit.Microsecond)).Append(86_399_999_999L).AppendNull().Append(86_400_000_000L).Build(),
            _ => new Time64Array.Builder(new Time64Type(TimeUnit.Nanosecond)).Append(0L).AppendNull().Append(-100L).Build(),
        };

        var refusal = await RefusalAsync(Stream(Batch(("At", array))));

        Assert.Equal(3, refusal.Row);
        Assert.Equal("At", refusal.Column);
        Assert.Equal($"Row 3, column 'At': {reason}", refusal.Message);
    }

    [Fact] // ADR-0065: a time64[ns] finer than a date's 100 nanoseconds is refused, not truncated
    public async Task A_time_finer_than_100_nanoseconds_is_refused()
    {
        var payload = Stream(Batch(("At", new Time64Array.Builder(new Time64Type(TimeUnit.Nanosecond)).Append(100L).Append(45_015_000_000_150L).Build())));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 2, column 'At': the time64[ns] value 45015000000150 is finer than the 100 nanoseconds a date holds.", refusal.Message);
    }

    [Theory] // ADR-0065: a dictionary index outside its dictionary — past its end, or negative — is refused by row and column
    [InlineData(3)]
    [InlineData(-1)]
    public async Task A_dictionary_index_outside_its_dictionary_is_refused(int index)
    {
        var payload = Stream(Batch(("Region", Dictionary(["a", "b", "c"], [0, null, index]))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal($"Row 3, column 'Region': the dictionary index {index} lies outside the dictionary's 3 entries.", refusal.Message);
    }

    [Fact] // ADR-0065: utf8 that is not valid UTF-8 is refused by row and column
    public async Task Utf8_that_is_not_utf8_is_refused()
    {
        var payload = Stream(Batch(("Name", Utf8Bytes([.. "fine"u8], [0xC3, 0x28]))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 2, column 'Name': the text is not valid UTF-8.", refusal.Message);
    }

    [Fact] // ADR-0065: a dictionary entry that is not valid UTF-8 is refused by the first row that uses it
    public async Task A_dictionary_entry_that_is_not_utf8_is_refused()
    {
        var entries = Utf8Bytes([.. "fine"u8], [0xFF]);
        var region = new DictionaryArray(new DictionaryType(Int32Type.Default, StringType.Default, ordered: false), Raw<int>(Int32Type.Default, 0, 0, 1), entries);

        var refusal = await RefusalAsync(Stream(Batch(("Region", region))));

        Assert.Equal("Row 3, column 'Region': the dictionary's entry 1 is not valid UTF-8.", refusal.Message);
    }

    public static TheoryData<string, string> ViewsThatAreNotText => new()
    {
        { "not UTF-8, in its view", "the text is not valid UTF-8." },
        { "not UTF-8, in a data buffer", "the text is not valid UTF-8." },
        { "pointing past its data buffer's end", "the Arrow stream is malformed: the value's view points outside its data." },
        { "naming a data buffer the array does not have", "the Arrow stream is malformed: the value's view points outside its data." },
        { "of a negative length", "the Arrow stream is malformed: the value's view points outside its data." },
    };

    [Theory] // ADR-0065: utf8_view that is not valid UTF-8, or whose view points outside its data, is refused by row and column
    [MemberData(nameof(ViewsThatAreNotText))]
    public async Task A_utf8_view_that_is_not_text_is_refused_by_row_and_column(string what, string reason)
    {
        byte[][] buffers = [[.. "a text longer than a view"u8, 0xC3, 0x28, .. " and more after it"u8]];
        var bad = what switch
        {
            "not UTF-8, in its view" => InlineView([0xC3, 0x28]),
            "not UTF-8, in a data buffer" => BufferView(buffers, 0, 2, 30),
            "pointing past its data buffer's end" => BufferView(buffers, 0, 2, buffers[0].Length),
            "naming a data buffer the array does not have" => BufferView(buffers, 1, 0, 13),
            _ => BufferView(buffers, 0, 0, -1),
        };

        var refusal = await RefusalAsync(Stream(Batch(("Name", Utf8Views(buffers, InlineView([.. "fine"u8]), BufferView(buffers, 0, 0, 13), bad)))));

        Assert.Equal($"Row 3, column 'Name': {reason}", refusal.Message);
    }

    [Fact] // ADR-0065: a dictionary of utf8_view whose entry's view points outside its data is refused by the first row that uses the entry
    public async Task A_dictionary_entry_whose_view_points_outside_its_data_is_refused()
    {
        byte[][] buffers = [[.. "a text longer than a view"u8]];
        var entries = Utf8Views(buffers, InlineView([.. "fine"u8]), BufferView(buffers, 0, 5, 40));
        var region = new DictionaryArray(new DictionaryType(Int32Type.Default, StringViewType.Default, ordered: false), Raw<int>(Int32Type.Default, 0, 0, 1), entries);

        var refusal = await RefusalAsync(Stream(Batch(("Region", region))));

        Assert.Equal("Row 3, column 'Region': the Arrow stream is malformed: the dictionary's entry 1's view points outside its data.", refusal.Message);
    }

    // ---- the schema and its metadata -----------------------------------------------------------

    [Fact] // ADR-0064: two columns of one name are refused, naming it, as a Snapshot's names are unique
    public async Task Two_columns_of_one_name_are_refused()
    {
        var schema = new Schema([new Field("A", Int64Type.Default, true), new Field("A", StringType.Default, true)], null);
        var payload = Stream(new RecordBatch(schema, [Raw<long>(Int64Type.Default, 1L), Utf8("x")], 1));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Column 'A': the Arrow stream has two columns of this name, and a column's name is unique within a Snapshot.", refusal.Message);
    }

    [Fact] // ADR-0064: a column without a name is refused, as every column of a Snapshot is named
    public async Task A_column_without_a_name_is_refused()
    {
        var schema = new Schema([new Field("A", Int64Type.Default, true), new Field("", Int64Type.Default, true)], null);
        var payload = Stream(new RecordBatch(schema, [Raw<long>(Int64Type.Default, 1L), Raw<long>(Int64Type.Default, 2L)], 1));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("The Arrow stream's column 2 has no name; every column of a Snapshot is named.", refusal.Message);
    }

    [Theory] // ADR-0065: a version in the metadata that is not a whole number, 0 or more, is refused
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public async Task A_version_that_is_not_one_is_refused(string version)
    {
        var payload = Stream(Batch([new(SnapshotArrowMetadata.Version, version)], ("N", Raw<long>(Int64Type.Default, 1L))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal($"The Arrow stream's exgrid.version metadata '{version}' is not a version: a version is a whole number, 0 or more.", refusal.Message);
    }

    [Fact] // ADR-0065: a Record Key the metadata names and the stream does not hold is refused, naming it
    public async Task A_record_key_the_stream_does_not_hold_is_refused()
    {
        var payload = Stream(Batch([new(SnapshotArrowMetadata.RecordKey, "Id")], ("N", Raw<long>(Int64Type.Default, 1L))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("The Arrow stream names 'Id' its Record Key (exgrid.recordKey), and holds no column of that name.", refusal.Message);
    }

    [Fact] // ADR-0064: a Record Key that is neither Text nor Integer is refused, naming its column
    public async Task A_record_key_of_another_kind_is_refused()
    {
        var payload = Stream(Batch([new(SnapshotArrowMetadata.RecordKey, "Price")], ("Price", Raw<double>(DoubleType.Default, 1.5))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Column 'Price': the Arrow stream names it the Record Key, and a Record Key is a Text or an Integer column; this one is Double.", refusal.Message);
    }

    [Fact] // ADR-0064: a Blank Record Key is refused, naming its row and the key's column
    public async Task A_blank_record_key_is_refused()
    {
        var payload = Stream(Batch([new(SnapshotArrowMetadata.RecordKey, "Id")], ("Id", Raw<long>(Int64Type.Default, 7L, null))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 2, column 'Id': the Record Key is Blank.", refusal.Message);
    }

    [Fact] // ADR-0064: a Record Key carried twice is refused, naming the key
    public async Task A_record_key_carried_twice_is_refused()
    {
        var payload = Stream(
            Batch([new(SnapshotArrowMetadata.RecordKey, "Id")], ("Id", Utf8("T1", "T2"))),
            Batch([new(SnapshotArrowMetadata.RecordKey, "Id")], ("Id", Utf8("T3", "T1"))));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("Row 4, column 'Id': the Record Key 'T1' is already carried by row 1.", refusal.Message);
        Assert.Equal("T1", refusal.Key);
    }

    // ---- bytes that are not a whole stream or file ------------------------------------------

    public static TheoryData<string> NotArrow => ["Region,Pnl\nEMEA,1.5\n", "[{\"Region\":\"EMEA\"}]", "<!DOCTYPE html><html></html>", "PAR1\0\0\0\0PAR1", "\u0010\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0"];

    [Theory] // ADR-0065: bytes that are not Arrow — a CSV, JSON, an error page, Parquet, zeros — are refused as not Arrow
    [MemberData(nameof(NotArrow))]
    public async Task Bytes_that_are_not_arrow_are_refused(string text)
    {
        var refusal = await RefusalAsync(Encoding.UTF8.GetBytes(text));

        Assert.Equal("The bytes are not an Arrow IPC stream or file: they begin with neither an IPC message nor ARROW1.", refusal.Message);
    }

    [Fact] // ADR-0065: a stream that begins with a record batch rather than its schema is refused as not Arrow
    public async Task A_stream_that_begins_without_its_schema_is_refused()
    {
        var whole = Stream(Batch(("N", Raw<long>(Int64Type.Default, 1L))));
        var schema = IpcFrames.Messages(whole)[0];

        var refusal = await RefusalAsync(whole[schema.End..]);

        Assert.Equal("The bytes are not an Arrow IPC stream or file: they begin with neither an IPC message nor ARROW1.", refusal.Message);
    }

    [Fact] // ADR-0065: an Arrow stream compressed with gzip, as an HTTP response is before it is undone, is refused, saying so
    public async Task A_gzipped_stream_is_refused_saying_so()
    {
        var whole = Stream(Batch(("N", Raw<long>(Int64Type.Default, 1L))));
        using var zipped = new MemoryStream();
        using (var gzip = new GZipStream(zipped, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(whole);

        var refusal = await RefusalAsync(zipped.ToArray());

        Assert.Equal(
            "The bytes are compressed with gzip, not an Arrow IPC stream or file: undo the compression first. An HttpClient does when its handler's AutomaticDecompression includes GZip.",
            refusal.Message);
    }

    [Fact] // ADR-0065: no bytes at all, or an end-of-stream marker alone, is an empty stream, refused
    public async Task An_empty_stream_is_refused()
    {
        var none = await RefusalAsync([]);
        var markerAlone = await RefusalAsync([0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0]);

        Assert.Equal("The Arrow stream is empty: it holds no schema.", none.Message);
        Assert.Equal(none.Message, markerAlone.Message);
    }

    [Fact] // ADR-0065: bytes that end inside their first message are refused as not Arrow or cut short
    public async Task Bytes_that_end_inside_their_first_message_are_refused()
    {
        var whole = Stream(Batch(("N", Raw<long>(Int64Type.Default, 1L))));

        var refusal = await RefusalAsync(whole[..20]);

        Assert.Equal("The bytes end inside their first message: they are not an Arrow IPC stream, or they are one cut short; a stream is read only whole.", refusal.Message);
    }

    public static TheoryData<string> Cuts => ["before the end-of-stream marker", "inside the end-of-stream marker", "inside a record batch's body", "inside a record batch's metadata", "between two record batches"];

    [Theory] // ADR-0064: a stream cut short — even between two record batches, where it would read as whole — is refused, never read as fewer rows
    [MemberData(nameof(Cuts))]
    public async Task A_stream_cut_short_is_refused(string where)
    {
        var whole = Stream(Batch(("N", Raw<long>(Int64Type.Default, 1L, 2L))), Batch(("N", Raw<long>(Int64Type.Default, 3L))));
        var messages = IpcFrames.Messages(whole);
        var second = messages[^1];
        var cut = where switch
        {
            "before the end-of-stream marker" => whole.Length - 8,
            "inside the end-of-stream marker" => whole.Length - 4,
            "inside a record batch's body" => second.End - 4,
            "inside a record batch's metadata" => second.Start + 12,
            _ => second.Start,
        };

        var refusal = await RefusalAsync(whole[..cut]);

        Assert.Equal("The Arrow stream ends before its end-of-stream marker, so it may have been cut short; a stream is read only whole.", refusal.Message);
    }

    [Theory] // ADR-0064: an Arrow file cut short, its footer gone, is refused
    [InlineData(10)]
    [InlineData(8)]
    public async Task A_file_cut_short_is_refused(int kept)
    {
        var file = File(new IpcOptions(), Batch(("N", Raw<long>(Int64Type.Default, 1L, 2L))));

        var refusal = await RefusalAsync(kept == 8 ? file[..8] : file[..^kept]);

        Assert.Equal("The Arrow file ends without its footer, so it may have been cut short; a file is read only whole.", refusal.Message);
    }

    [Fact] // ADR-0064: rows with no column are refused, as a Snapshot holds rows only in its columns and would read them as none
    public async Task Rows_without_a_column_are_refused()
    {
        var payload = Stream(new RecordBatch(new Schema([], null), [], 3));

        var refusal = await RefusalAsync(payload);

        Assert.Equal("The Arrow stream holds 3 rows and no column, and a Snapshot holds rows only in its columns.", refusal.Message);
    }

    [Fact] // ADR-0064: a refused read throws, and yields no Snapshot
    public async Task A_refused_read_yields_no_snapshot()
    {
        var payload = Stream(Batch(("Count", Raw<ulong>(UInt64Type.Default, ulong.MaxValue))));
        Snapshot? read = null;

        await Assert.ThrowsAsync<SnapshotException>(async () => read = await SnapshotArrow.ReadAsync(payload, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(read);
    }

    // ---- making what other producers may write ----------------------------------------------

    /// <summary>A two-row array of a type outside the table, named as <see cref="ArrowTypeNames"/> names it.</summary>
    private static IArrowArray ArrayOf(string type)
    {
        switch (type)
        {
            case "list<int64>":
            {
                var list = new ListArray.Builder(Int64Type.Default);
                list.Append();
                ((Int64Array.Builder)list.ValueBuilder).Append(1);
                list.AppendNull();
                return list.Build();
            }
            case "large_list<int64>":
            {
                var list = new LargeListArray.Builder(Int64Type.Default);
                list.Append();
                ((Int64Array.Builder)list.ValueBuilder).Append(1);
                list.AppendNull();
                return list.Build();
            }
            case "struct<a: int64>":
                return new StructArray(new StructType([new Field("a", Int64Type.Default, true)]), 2, [Raw<long>(Int64Type.Default, 1L, 2L)], ArrowBuffer.Empty, 0);
            case "map<utf8, int64>":
            {
                var map = new MapArray.Builder(new MapType(StringType.Default, Int64Type.Default));
                map.Append();
                ((StringArray.Builder)map.KeyBuilder).Append("k");
                ((Int64Array.Builder)map.ValueBuilder).Append(1);
                map.AppendNull();
                return map.Build();
            }
            case "binary":
                return new BinaryArray.Builder().Append([1, 2]).AppendNull().Build();
            case "large_binary":
                return new LargeBinaryArray.Builder().Append([1, 2]).AppendNull().Build();
            case "binary_view":
                return new BinaryViewArray.Builder().Append([1, 2]).AppendNull().Build();
            case "fixed_size_binary[16]":
                return new Apache.Arrow.Arrays.FixedSizeBinaryArray(new ArrayData(new FixedSizeBinaryType(16), 2, 0, 0, [ArrowBuffer.Empty, new ArrowBuffer(new byte[32])]));
            case "duration[ms]":
                return Raw<long>(DurationType.Millisecond, 1L, null);
            case "month_interval":
                return Raw<int>(IntervalType.YearMonth, 1, null);
            case "float16":
                return Raw<Half>(HalfFloatType.Default, (Half)1.5f, null);
            case "null":
                return new NullArray(2);
            case "dictionary<values=int64, indices=int32>":
                return new DictionaryArray(new DictionaryType(Int32Type.Default, Int64Type.Default, ordered: false), Raw<int>(Int32Type.Default, 0, null), Raw<long>(Int64Type.Default, 5L));
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "No such array here.");
        }
    }

    /// <summary>A utf8 array of raw bytes, valid UTF-8 or not, one value per byte array.</summary>
    private static StringArray Utf8Bytes(params byte[][] values)
    {
        var offsets = new int[values.Length + 1];
        for (var i = 0; i < values.Length; i++)
            offsets[i + 1] = offsets[i] + values[i].Length;
        var data = values.SelectMany(v => v).ToArray();
        var offsetBytes = new byte[offsets.Length * 4];
        for (var i = 0; i < offsets.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(offsetBytes.AsSpan(i * 4), offsets[i]);
        return new StringArray(values.Length, new ArrowBuffer(offsetBytes), new ArrowBuffer(data), ArrowBuffer.Empty, 0);
    }
}
