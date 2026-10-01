using System.Buffers.Binary;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// Reading streams written the way other producers write them, with Apache.Arrow's own builders: the
/// type table, and other producers' dictionaries taken under the Snapshot's rules (DA-14).
/// </summary>
public class ReadTests
{
    [Fact] // ADR-0064: utf8 is Text, exactly as written; a null is a Blank and the empty string a value
    public async Task Utf8_is_text()
    {
        var payload = Stream(Batch(("Region", Utf8("amer", "AMER", null, "", "amer", "Zürich"))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(SnapshotKind.Text, snapshot["Region"].Kind);
        Assert.Equal(Shown("amer", "AMER", null, "", "amer", "Zürich"), Values(snapshot, "Region"));
        Assert.Equal(["amer", "AMER", "", "Zürich"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: large_utf8 is Text
    public async Task Large_utf8_is_text()
    {
        var payload = Stream(Batch(("Region", LargeUtf8("b", null, "a", "b"))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown("b", null, "a", "b"), Values(snapshot, "Region"));
        Assert.Equal(["b", "a"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: utf8_view is Text exactly as written — twelve bytes or fewer held in the view, longer text in a data buffer; a null is a Blank and the empty string a value
    public async Task Utf8_view_is_text()
    {
        string?[] values = ["amer", "AMER", null, "", "twelve bytes", "thirteen byte", "Zürich 東京 🚀, at length", "amer", "thirteen byte"];
        var payload = Stream(Batch(("Region", Utf8View(values))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(SnapshotKind.Text, snapshot["Region"].Kind);
        Assert.Equal(Shown(values), Values(snapshot, "Region"));
        Assert.Equal(["amer", "AMER", "", "twelve bytes", "thirteen byte", "Zürich 東京 🚀, at length"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: utf8_view's longer text is read from the data buffer its view names, wherever in it the text starts — as Polars spreads its text over several buffers
    public async Task Utf8_view_text_is_read_from_the_data_buffer_its_view_names()
    {
        byte[][] buffers = [[.. "Emerging Markets Rates"u8], [.. "--Global Credit Trading--"u8]];
        var desk = Utf8Views(buffers,
            BufferView(buffers, 1, 2, 21),
            InlineView([.. "FX"u8]),
            null,
            BufferView(buffers, 0, 0, 22),
            BufferView(buffers, 0, 9, 13),
            BufferView(buffers, 1, 2, 21));

        var snapshot = await ReadEveryWayAsync(Stream(Batch(("Desk", desk))));

        Assert.Equal(Shown("Global Credit Trading", "FX", null, "Emerging Markets Rates", "Markets Rates", "Global Credit Trading"), Values(snapshot, "Desk"));
        Assert.Equal(["Global Credit Trading", "FX", "Emerging Markets Rates", "Markets Rates"], Dictionary(snapshot, "Desk"));
    }

    [Fact] // ADR-0064: a dictionary of utf8_view — as Polars writes a Categorical by default — is Text under the Snapshot's rules: out of order, amer beside AMER, a null entry, null indices, an entry longer than its view
    public async Task A_dictionary_of_utf8_view_is_text()
    {
        var entries = Utf8View("emea", "AMER", null, "amer", "Asia Pacific, excluding Japan", "unused");
        var indices = Raw<uint>(UInt32Type.Default, 3u, 1u, null, 0u, 2u, 4u, 1u, 4u);
        var region = new DictionaryArray(new DictionaryType(UInt32Type.Default, StringViewType.Default, ordered: false), indices, entries);

        var snapshot = await ReadEveryWayAsync(Stream(Batch(("Region", region))));

        Assert.Equal(Shown("amer", "AMER", null, "emea", null, "Asia Pacific, excluding Japan", "AMER", "Asia Pacific, excluding Japan"), Values(snapshot, "Region"));
        Assert.Equal(["amer", "AMER", "emea", "Asia Pacific, excluding Japan"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: another producer's dictionary — out of order, amer beside AMER, a null entry, null indices, an entry no row uses — is taken under the Snapshot's rules
    public async Task Another_producers_dictionary_is_taken_under_the_snapshots_rules()
    {
        var region = Dictionary(["emea", "AMER", null, "amer", "APAC", "unused"], [3, 1, null, 0, 2, 4, 1, 3]);
        var payload = Stream(Batch(("Region", region)));

        var snapshot = await ReadEveryWayAsync(payload);

        // In the order values first appear in the rows; amer and AMER are two values, as written; the
        // null index and the null entry are Blanks; the entry no row uses is not taken.
        Assert.Equal(["amer", "AMER", "emea", "APAC"], Dictionary(snapshot, "Region"));
        Assert.Equal(Shown("amer", "AMER", null, "emea", null, "APAC", "AMER", "amer"), Values(snapshot, "Region"));
        var column = (TextColumn)snapshot["Region"];
        Assert.Equal([0, 1, -1, 2, -1, 3, 1, 0], snapshot.Slice(0).Codes(column).ToArray());
    }

    [Fact] // ADR-0064: a dictionary of large_utf8 is Text
    public async Task A_dictionary_of_large_utf8_is_text()
    {
        var payload = Stream(Batch(("Region", Dictionary(["x", "y"], [1, null, 0], large: true))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown("y", null, "x"), Values(snapshot, "Region"));
    }

    public static TheoryData<string> IndexTypes => ["int8", "int16", "int32", "int64", "uint8", "uint16", "uint32", "uint64"];

    [Theory] // ADR-0064: a dictionary's indices may be any integer type
    [MemberData(nameof(IndexTypes))]
    public async Task A_dictionarys_indices_may_be_any_integer(string name)
    {
        var values = new long?[] { 2, 0, null, 1, 2 };
        IArrowArray indices = name switch
        {
            "int8" => Raw<sbyte>(Int8Type.Default, values.Select(v => (sbyte?)v).ToArray()),
            "int16" => Raw<short>(Int16Type.Default, values.Select(v => (short?)v).ToArray()),
            "int32" => Raw<int>(Int32Type.Default, values.Select(v => (int?)v).ToArray()),
            "int64" => Raw<long>(Int64Type.Default, values),
            "uint8" => Raw<byte>(UInt8Type.Default, values.Select(v => (byte?)v).ToArray()),
            "uint16" => Raw<ushort>(UInt16Type.Default, values.Select(v => (ushort?)v).ToArray()),
            "uint32" => Raw<uint>(UInt32Type.Default, values.Select(v => (uint?)v).ToArray()),
            _ => Raw<ulong>(UInt64Type.Default, values.Select(v => (ulong?)v).ToArray()),
        };
        var entries = Utf8("z", "y", "x");
        var region = new DictionaryArray(new DictionaryType(indices.Data.DataType, StringType.Default, ordered: false), indices, entries);

        var snapshot = await ReadEveryWayAsync(Stream(Batch(("Region", region))));

        Assert.Equal(Shown("x", "z", null, "y", "x"), Values(snapshot, "Region"));
        Assert.Equal(["x", "z", "y"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: several record batches are read in order, a dictionary shared by them taken once
    public async Task Several_record_batches_are_read_in_order()
    {
        var entries = new string?[] { "c", "b", "a" };
        RecordBatch Part(int?[] codes, long?[] numbers) => Batch(("Region", Dictionary(entries, codes)), ("N", Raw<long>(Int64Type.Default, numbers)));
        var payload = Stream(Part([0, 1], [1, 2]), Part([], []), Part([2, null, 0], [3, null, 5]), Part([1], [6]));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown("c", "b", "a", null, "c", "b"), Values(snapshot, "Region"));
        Assert.Equal(Shown(1L, 2L, 3L, null, 5L, 6L), Values(snapshot, "N"));
        Assert.Equal(["c", "b", "a"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: a dictionary replaced between record batches is read by the batches that follow it
    public async Task A_dictionary_replaced_between_record_batches_is_read_by_the_batches_after_it()
    {
        // Apache.Arrow's writer sends one dictionary; a second stream's dictionary and batch are
        // spliced in after the first's batch, as a producer replacing its dictionary sends them.
        var first = Stream(Batch(("Region", Dictionary(["b", "a"], [0, 1, 1]))));
        var second = Stream(Batch(("Region", Dictionary(["a", "c", "b"], [1, 0, 2, null]))));
        var a = IpcFrames.Messages(first);
        var b = IpcFrames.Messages(second);
        byte[] payload = [.. first.AsSpan(0, a[2].End), .. second.AsSpan(b[1].Start, b[2].End - b[1].Start), .. first.AsSpan(a[2].End)];

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown("b", "a", "a", "c", "a", "b", null), Values(snapshot, "Region"));
        Assert.Equal(["b", "a", "c"], Dictionary(snapshot, "Region"));
    }

    [Fact] // ADR-0064: decimal128 is Decimal, exact: as scaled integers where they fit, as decimals where they do not
    public async Task Decimal128_is_decimal()
    {
        var type = new Decimal128Type(38, 4);
        var payload = Stream(Batch(("Small", Decimals(type, Words(15_000), Words(-1), null, Words(0))), ("Large", Decimals(type, Words(Int128.Parse("792281625142643375935439503350000")), Words(Int128.Parse("-1234567890123456789012345678")), null, Words(1)))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown(1.5m, -0.0001m, null, 0m), Values(snapshot, "Small"));
        Assert.Equal(Shown(decimal.MaxValue, -123_456_789_012_345_678_901_234.5678m, null, 0.0001m), Values(snapshot, "Large"));
        Assert.Equal(4, snapshot.Slice(0).Decimals((DecimalColumn)snapshot["Small"]).Scale);
        Assert.Equal(-1, snapshot.Slice(0).Decimals((DecimalColumn)snapshot["Large"]).Scale);
    }

    [Fact] // ADR-0064: decimal256 within decimal's range is Decimal
    public async Task Decimal256_within_range_is_decimal()
    {
        // 10^40 at 40 places is 1; trailing zeros past 28 places are no places at all.
        var one = System.Numerics.BigInteger.Pow(10, 40);
        var words = new long[4];
        one.TryWriteBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan()), out _, isUnsigned: false, isBigEndian: false);
        var payload = Stream(
            Batch(("Wide", Decimals(new Decimal256Type(76, 2), Words(-250, 4), null, Words(Int128.Parse("7922816251426433759354395033500"), 4)))),
            Batch(("Wide", Decimals(new Decimal256Type(76, 2), Words(1, 4), Words(2, 4), Words(3, 4)))));
        var tens = Stream(Batch(("Tens", Decimals(new Decimal256Type(76, 40), words, Words(0, 4)))));

        Assert.Equal(Shown(-2.5m, null, decimal.MaxValue, 0.01m, 0.02m, 0.03m), Values(await ReadEveryWayAsync(payload), "Wide"));
        Assert.Equal(Shown(1m, 0m), Values(await ReadEveryWayAsync(tens), "Tens"));
    }

    [Fact] // ADR-0064: decimal32 and decimal64 are Decimal, exact, with Blanks — to the last digit of their precision
    public async Task Decimal32_and_decimal64_are_decimal()
    {
        var payload = Stream(Batch(
            ("D32", new Decimal32Array.Builder(new Decimal32Type(9, 2)).Append(1.5m).AppendNull().Append(-2.25m).Append(9_999_999.99m).Append(0m).Build()),
            ("D32Whole", new Decimal32Array.Builder(new Decimal32Type(9, 9)).Append(0.999_999_999m).Append(-0.999_999_999m).AppendNull().Append(0.000_000_001m).Append(0m).Build()),
            ("D64", new Decimal64Array.Builder(new Decimal64Type(18, 4)).Append(1.5m).Append(-0.0001m).AppendNull().Append(99_999_999_999_999.9999m).Append(0m).Build()),
            ("D64Whole", new Decimal64Array.Builder(new Decimal64Type(18, 18)).Append(0.999_999_999_999_999_999m).AppendNull().Append(-0.999_999_999_999_999_999m).Append(0.000_000_000_000_000_001m).Append(0m).Build())));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.All(snapshot.Columns, c => Assert.Equal(SnapshotKind.Decimal, c.Kind));
        Assert.Equal(Shown(1.5m, null, -2.25m, 9_999_999.99m, 0m), Values(snapshot, "D32"));
        Assert.Equal(Shown(0.999_999_999m, -0.999_999_999m, null, 0.000_000_001m, 0m), Values(snapshot, "D32Whole"));
        Assert.Equal(Shown(1.5m, -0.0001m, null, 99_999_999_999_999.9999m, 0m), Values(snapshot, "D64"));
        Assert.Equal(Shown(0.999_999_999_999_999_999m, null, -0.999_999_999_999_999_999m, 0.000_000_000_000_000_001m, 0m), Values(snapshot, "D64Whole"));
        // In as scaled integers, at the column's scale.
        Assert.Equal(2, snapshot.Slice(0).Decimals((DecimalColumn)snapshot["D32"]).Scale);
        Assert.Equal(4, snapshot.Slice(0).Decimals((DecimalColumn)snapshot["D64"]).Scale);
    }

    [Fact] // ADR-0064: a decimal32's or decimal64's negative scale multiplies, and a scale past 28 whose places are trailing zeros is read exactly
    public async Task A_decimal32_or_decimal64_at_a_scale_outside_0_to_28_is_read_exactly()
    {
        var payload = Stream(Batch(
            ("Thousands", Raw<int>(new Decimal32Type(9, -3), 7, -12, null, int.MaxValue)),
            ("Tiny", Raw<long>(new Decimal64Type(18, 30), 1_500_000_000_000L, null, -250_000_000_000_000_000L, 0L))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown(7_000m, -12_000m, null, 2_147_483_647_000m), Values(snapshot, "Thousands"));
        Assert.Equal(Shown(0.000_000_000_000_000_0015m, null, -0.000_000_000_000_25m, 0m), Values(snapshot, "Tiny"));
    }

    [Fact] // ADR-0064: a decimal's negative scale multiplies, and places past 28 that are trailing zeros are dropped
    public async Task A_negative_scale_multiplies_and_trailing_places_are_dropped()
    {
        var payload = Stream(
            Batch(
                ("Hundreds", Decimals(new Decimal128Type(10, -2), Words(5), Words(-7))),
                ("Fine", Decimals(new Decimal128Type(38, 30), Words(Int128.Parse("1500000000000000000000000000000")), Words(Int128.Parse("-25000000000000000000000000000"))))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown(500m, -700m), Values(snapshot, "Hundreds"));
        Assert.Equal(Shown(1.5m, -0.025m), Values(snapshot, "Fine"));
    }

    [Fact] // ADR-0064: float64 is Double as it came, non-finite values kept; float32 is widened exactly
    public async Task Floats_are_double()
    {
        var payload = Stream(Batch(
            ("F64", Raw<double>(DoubleType.Default, double.NaN, double.NegativeInfinity, -0.0, null, 0.1)),
            ("F32", Raw<float>(FloatType.Default, float.NaN, float.PositiveInfinity, 0.1f, null, float.Epsilon))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown(double.NaN, double.NegativeInfinity, -0.0, null, 0.1), Values(snapshot, "F64"));
        Assert.Equal(Shown((double)float.NaN, double.PositiveInfinity, (double)0.1f, null, (double)float.Epsilon), Values(snapshot, "F32"));
        Assert.Equal(SnapshotKind.Double, snapshot["F32"].Kind);
    }

    [Fact] // ADR-0064: int8 to int64, uint8 to uint32, and uint64 within long's range are Integer
    public async Task Integers_of_every_width_are_integer()
    {
        var payload = Stream(Batch(
            ("I8", Raw<sbyte>(Int8Type.Default, sbyte.MinValue, sbyte.MaxValue, null)),
            ("I16", Raw<short>(Int16Type.Default, short.MinValue, short.MaxValue, null)),
            ("I32", Raw<int>(Int32Type.Default, int.MinValue, int.MaxValue, null)),
            ("I64", Raw<long>(Int64Type.Default, long.MinValue, long.MaxValue, null)),
            ("U8", Raw<byte>(UInt8Type.Default, byte.MinValue, byte.MaxValue, null)),
            ("U16", Raw<ushort>(UInt16Type.Default, ushort.MinValue, ushort.MaxValue, null)),
            ("U32", Raw<uint>(UInt32Type.Default, uint.MinValue, uint.MaxValue, null)),
            ("U64", Raw<ulong>(UInt64Type.Default, ulong.MinValue, (ulong)long.MaxValue, null))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.All(snapshot.Columns, c => Assert.Equal(SnapshotKind.Integer, c.Kind));
        Assert.Equal(Shown(-128L, 127L, null), Values(snapshot, "I8"));
        Assert.Equal(Shown((long)short.MinValue, (long)short.MaxValue, null), Values(snapshot, "I16"));
        Assert.Equal(Shown((long)int.MinValue, (long)int.MaxValue, null), Values(snapshot, "I32"));
        Assert.Equal(Shown(long.MinValue, long.MaxValue, null), Values(snapshot, "I64"));
        Assert.Equal(Shown(0L, 255L, null), Values(snapshot, "U8"));
        Assert.Equal(Shown(0L, 65_535L, null), Values(snapshot, "U16"));
        Assert.Equal(Shown(0L, (long)uint.MaxValue, null), Values(snapshot, "U32"));
        Assert.Equal(Shown(0L, long.MaxValue, null), Values(snapshot, "U64"));
    }

    [Fact] // ADR-0064: date32, date64 and a timestamp without a time zone are Date, as the clock value written
    public async Task Dates_and_naive_timestamps_are_the_clock_value_written()
    {
        var noon = new DateTime(2026, 9, 30, 12, 0, 0);
        var fromEpoch = noon - DateTime.UnixEpoch;
        var payload = Stream(Batch(
            ("D32", Raw<int>(Date32Type.Default, (int)fromEpoch.TotalDays, -719_162, 2_932_896, null)),
            ("D64", Raw<long>(Date64Type.Default, (long)fromEpoch.TotalMilliseconds, -1, 0, null)),
            ("S", Raw<long>(new TimestampType(TimeUnit.Second, (string?)null), (long)fromEpoch.TotalSeconds, -62_135_596_800, 253_402_300_799, null)),
            ("Ms", Raw<long>(new TimestampType(TimeUnit.Millisecond, (string?)null), (long)fromEpoch.TotalMilliseconds + 1, -1, 0, null)),
            ("Us", Raw<long>(new TimestampType(TimeUnit.Microsecond, ""), (long)(fromEpoch.Ticks / 10) + 1, -1, 0, null)),
            ("Ns", Raw<long>(new TimestampType(TimeUnit.Nanosecond, (string?)null), (fromEpoch.Ticks * 100) + 100, long.MinValue + 8, long.MaxValue - 7, null))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.All(snapshot.Columns, c => Assert.Equal(SnapshotKind.Date, c.Kind));
        Assert.Equal(Shown(noon.Date, DateTime.MinValue, new DateTime(9999, 12, 31), null), Values(snapshot, "D32"));
        Assert.Equal(Shown(noon, DateTime.UnixEpoch.AddMilliseconds(-1), DateTime.UnixEpoch, null), Values(snapshot, "D64"));
        Assert.Equal(Shown(noon, DateTime.MinValue, new DateTime(9999, 12, 31, 23, 59, 59), null), Values(snapshot, "S"));
        Assert.Equal(Shown(noon.AddMilliseconds(1), DateTime.UnixEpoch.AddMilliseconds(-1), DateTime.UnixEpoch, null), Values(snapshot, "Ms"));
        Assert.Equal(Shown(noon.AddMicroseconds(1), DateTime.UnixEpoch.AddMicroseconds(-1), DateTime.UnixEpoch, null), Values(snapshot, "Us"));
        Assert.Equal(Shown(noon.AddTicks(1), DateTime.UnixEpoch.AddTicks((long.MinValue + 8) / 100), DateTime.UnixEpoch.AddTicks((long.MaxValue - 7) / 100), null), Values(snapshot, "Ns"));
    }

    [Fact] // ADR-0064: time32 and time64 are Date on the first day — the clock time on 0001-01-01, as a database's TimeOnly is read — with Blanks, from midnight to the day's last instant
    public async Task Times_are_a_date_on_the_first_day()
    {
        var time = new TimeOnly(12, 30, 15);
        var payload = Stream(Batch(
            ("S", new Time32Array.Builder(new Time32Type(TimeUnit.Second)).Append(0).Append((int)(time.Ticks / TimeSpan.TicksPerSecond)).AppendNull().Append(86_399).Build()),
            ("Ms", new Time32Array.Builder(new Time32Type(TimeUnit.Millisecond)).Append(0).Append((int)(time.Ticks / TimeSpan.TicksPerMillisecond) + 1).AppendNull().Append(86_399_999).Build()),
            ("Us", new Time64Array.Builder(new Time64Type(TimeUnit.Microsecond)).Append(0L).Append((time.Ticks / TimeSpan.TicksPerMicrosecond) + 1).AppendNull().Append(86_399_999_999L).Build()),
            ("Ns", new Time64Array.Builder(new Time64Type(TimeUnit.Nanosecond)).Append(0L).Append((time.Ticks * 100) + 100).AppendNull().Append(86_399_999_999_900L).Build())));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.All(snapshot.Columns, c => Assert.Equal(SnapshotKind.Date, c.Kind));
        Assert.Equal(Shown(OnTheFirstDay(TimeOnly.MinValue), OnTheFirstDay(time), null, OnTheFirstDay(new TimeOnly(23, 59, 59))), Values(snapshot, "S"));
        Assert.Equal(Shown(OnTheFirstDay(TimeOnly.MinValue), OnTheFirstDay(time.Add(TimeSpan.FromMilliseconds(1))), null, OnTheFirstDay(new TimeOnly(23, 59, 59, 999))), Values(snapshot, "Ms"));
        Assert.Equal(Shown(OnTheFirstDay(TimeOnly.MinValue), OnTheFirstDay(time.Add(TimeSpan.FromMicroseconds(1))), null, OnTheFirstDay(new TimeOnly(23, 59, 59, 999, 999))), Values(snapshot, "Us"));
        Assert.Equal(Shown(OnTheFirstDay(TimeOnly.MinValue), OnTheFirstDay(time.Add(TimeSpan.FromTicks(1))), null, OnTheFirstDay(TimeOnly.MaxValue)), Values(snapshot, "Ns"));
    }

    public static TheoryData<string> UtcZones =>
    [
        "UTC", "Etc/UTC", "GMT", "Etc/GMT", "UCT", "Etc/UCT", "Universal", "Etc/Universal", "Zulu", "Etc/Zulu",
        "+00:00", "-00:00", "Z", "utc", "gmt", "etc/zulu", "UNIVERSAL",
    ];

    [Theory] // ADR-0064: a timestamp in UTC — under any of its IANA names, each also under Etc/, as +00:00, -00:00 or Z, in any case — is Date, as the UTC clock value
    [MemberData(nameof(UtcZones))]
    public async Task A_timestamp_in_utc_is_the_utc_clock_value(string zone)
    {
        var instant = new DateTime(2026, 9, 30, 21, 15, 0);
        var payload = Stream(Batch(("When", Raw<long>(new TimestampType(TimeUnit.Microsecond, zone), (instant - DateTime.UnixEpoch).Ticks / 10, null))));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown(instant, null), Values(snapshot, "When"));
    }

    [Fact] // ADR-0064: bool is Boolean, across a byte and with a null
    public async Task Bool_is_boolean()
    {
        var flags = new BooleanArray.Builder();
        for (var i = 0; i < 20; i++)
        {
            if (i == 9)
                flags.AppendNull();
            else
                flags.Append(i % 3 == 0);
        }
        var snapshot = await ReadEveryWayAsync(Stream(Batch(("Live", flags.Build()))));

        Assert.Equal(Enumerable.Range(0, 20).Select(i => i == 9 ? "∅" : Show(i % 3 == 0)), Values(snapshot, "Live"));
    }

    [Fact] // ADR-0064: Arrow's file format is read, from memory, from a stream that seeks, from one that does not, and from one that starts further on
    public async Task The_file_format_is_read()
    {
        var entries = new string?[] { "Rates", "FX" };
        var file = File(new IpcOptions(),
            Batch(("Desk", Dictionary(entries, [1, 0, null])), ("Pnl", Raw<double>(DoubleType.Default, 1.5, null, -2))),
            Batch(("Desk", Dictionary(entries, [0])), ("Pnl", Raw<double>(DoubleType.Default, 3.0))));

        var snapshot = await ReadEveryWayAsync(file);
        using var further = new MemoryStream();
        further.Write([1, 2, 3]);
        further.Write(file);
        further.Position = 3;
        var fromFurther = await SnapshotArrow.ReadAsync(further, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(Shown("FX", "Rates", null, "Rates"), Values(snapshot, "Desk"));
        Assert.Equal(Shown(1.5, null, -2.0, 3.0), Values(snapshot, "Pnl"));
        AssertSame(snapshot, fromFurther);
    }

    [Fact] // ADR-0064: the captions, Record Key and version another producer writes under ExGrid.Data.Arrow's keys are honoured
    public async Task Metadata_another_producer_wrote_is_honoured()
    {
        var id = new Field("Id", Int64Type.Default, nullable: false);
        var desk = new Field("Desk", StringType.Default, nullable: true, [new(SnapshotArrowMetadata.Caption, "Trading desk")]);
        var schema = new Schema([id, desk], [new(SnapshotArrowMetadata.RecordKey, "Id"), new(SnapshotArrowMetadata.Version, "41")]);
        var payload = Stream(new RecordBatch(schema, [Raw<long>(Int64Type.Default, 7L, 8L), Utf8("FX", null)], 2));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(41, snapshot.Version);
        Assert.Equal("Id", snapshot.RecordKey?.Name);
        Assert.Equal("Trading desk", snapshot["Desk"].Caption);
        Assert.Equal("Id", snapshot["Id"].Caption);
        // The key works as a key: a batch finds a record by it.
        var change = SnapshotColumns(("Id", 8L), ("Desk", "Rates"));
        var after = snapshot.Apply(ChangeBatch.Of(changed: change)).After;
        Assert.Equal(Shown("FX", "Rates"), Values(after, "Desk"));
    }

    [Fact] // ADR-0064: a stream without metadata reads with each column's name its caption, version 0 and no Record Key
    public async Task A_stream_without_metadata_takes_the_defaults()
    {
        var snapshot = await ReadEveryWayAsync(Stream(Batch(("Desk", Utf8("FX")))));

        Assert.Equal(0, snapshot.Version);
        Assert.Null(snapshot.RecordKey);
        Assert.Equal("Desk", snapshot["Desk"].Caption);
        Assert.False(snapshot.KeepsRecords);
    }

    [Fact] // ADR-0064: a stream with a schema and no record batch is a Snapshot of no rows
    public async Task A_stream_of_no_batches_is_a_snapshot_of_no_rows()
    {
        var schema = new Schema([new Field("Desk", StringType.Default, nullable: true)], null);
        using var stream = new MemoryStream();
        using (var writer = new ArrowStreamWriter(stream, schema, leaveOpen: true))
        {
            writer.WriteStart();
            writer.WriteEnd();
        }

        var snapshot = await ReadEveryWayAsync(stream.ToArray());

        Assert.Equal(0, snapshot.RowCount);
        Assert.Equal(SnapshotKind.Text, snapshot["Desk"].Kind);
    }

    [Fact] // ADR-0064: a stream written in the legacy framing, without continuation markers, is read
    public async Task The_legacy_framing_is_read()
    {
        var payload = Stream(new IpcOptions { WriteLegacyIpcFormat = true }, Batch(("N", Raw<long>(Int64Type.Default, 1L, null, 3L))));
        Assert.NotEqual(-1, BinaryPrimitives.ReadInt32LittleEndian(payload));

        var snapshot = await ReadEveryWayAsync(payload);

        Assert.Equal(Shown(1L, null, 3L), Values(snapshot, "N"));
    }

    /// <summary>A time of day as a Date holds it: the clock time on the first day, 0001-01-01.</summary>
    private static DateTime OnTheFirstDay(TimeOnly time) => DateTime.MinValue + time.ToTimeSpan();

    /// <summary>A one-row Snapshot of the named Integer and Text values, keyed by its first column.</summary>
    private static Snapshot SnapshotColumns((string Name, long Value) key, (string Name, string Value) text)
    {
        var builder = new SnapshotColumnsBuilder();
        builder.Integer(key.Name).Append(key.Value);
        builder.Text(text.Name).Append(text.Value);
        return builder.Key(key.Name).Build();
    }
}
