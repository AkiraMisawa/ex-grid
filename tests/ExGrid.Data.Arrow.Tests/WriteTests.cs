using System.Globalization;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>Writing gives an uncompressed IPC stream, each kind its Arrow type, a Blank a null slot (DA-15).</summary>
public class WriteTests
{
    [Fact] // ADR-0064: the stream is uncompressed, and each kind has the Arrow type ADR-0064 gives it
    public async Task The_stream_is_uncompressed_and_each_kind_has_its_arrow_type()
    {
        var snapshot = Deals().Build(Make(300));

        var payload = await WriteAsync(snapshot);

        // Apache.Arrow refuses a compressed body without a codec factory: this reader has none.
        using var reader = new ArrowStreamReader(payload);
        var types = reader.Schema.FieldsList.Select(f => (f.Name, ArrowTypeNames.Of(f.DataType), f.IsNullable));
        Assert.Equal(
        [
            ("Id", "int64", true),
            ("Desk", "dictionary<values=utf8, indices=int32>", true),
            ("Notional", "decimal128(38, 3)", true),
            ("Price", "float64", true),
            ("Quantity", "int64", true),
            ("Day", "date32", true),
            ("When", "timestamp[ms]", true),
            ("Live", "bool", true),
        ], types);
        var rows = 0;
        while (reader.ReadNextRecordBatch() is { } batch)
            rows += batch.Length;
        Assert.Equal(300, rows);
    }

    [Fact] // ADR-0064: what is written reads back through Apache.Arrow's own accessors as the Snapshot's values, a Blank a null slot
    public async Task Arrows_own_accessors_read_the_snapshots_values()
    {
        var deals = Make(200);
        var snapshot = Deals().Build(deals);

        using var reader = new ArrowStreamReader(await WriteAsync(snapshot, batchRows: 64));
        var row = 0;
        while (reader.ReadNextRecordBatch() is { } batch)
        {
            var desk = (DictionaryArray)batch.Column("Desk");
            var deskEntries = (StringArray)desk.Dictionary;
            var deskIndices = (Int32Array)desk.Indices;
            for (var i = 0; i < batch.Length; i++, row++)
            {
                var deal = deals[row];
                Assert.Equal(deal.Id, ((Int64Array)batch.Column("Id")).GetValue(i));
                Assert.Equal(deal.Desk, deskIndices.GetValue(i) is { } code ? deskEntries.GetString(code) : null);
                Assert.Equal(deal.Notional, ((Decimal128Array)batch.Column("Notional")).GetValue(i));
                Assert.Equal(deal.Price is { } price ? (long?)BitConverter.DoubleToInt64Bits(price) : null, ((DoubleArray)batch.Column("Price")).GetValue(i) is { } p ? (long?)BitConverter.DoubleToInt64Bits(p) : null);
                Assert.Equal(deal.Quantity, ((Int64Array)batch.Column("Quantity")).GetValue(i));
                Assert.Equal(deal.Day, ((Date32Array)batch.Column("Day")).GetDateOnly(i));
                Assert.Equal(deal.When is { } when ? (DateTimeOffset?)new DateTimeOffset(when, TimeSpan.Zero) : null, ((TimestampArray)batch.Column("When")).GetTimestamp(i));
                Assert.Equal(deal.Live, ((BooleanArray)batch.Column("Live")).GetValue(i));
            }
        }
        Assert.Equal(200, row);
    }

    [Fact] // ADR-0064: a Blank is a null slot in every kind; a Text Blank is a null index
    public async Task A_blank_is_a_null_slot()
    {
        var snapshot = Deals().Build([new Deal(1, null, null, null, null, null, null, null), One(2)]);

        using var reader = new ArrowStreamReader(await WriteAsync(snapshot));
        var batch = reader.ReadNextRecordBatch()!;

        foreach (var name in new[] { "Desk", "Notional", "Price", "Quantity", "Day", "When", "Live" })
        {
            var array = batch.Column(name);
            Assert.Equal(1, array.NullCount);
            Assert.True(array.IsNull(0));
            Assert.False(array.IsNull(1));
        }
        Assert.True(((DictionaryArray)batch.Column("Desk")).Indices.IsNull(0));
        Assert.Equal(0, batch.Column("Id").NullCount);
    }

    [Fact] // ADR-0064: the captions, the Record Key and the version travel in the metadata, under the documented keys
    public async Task The_metadata_carries_the_captions_the_key_and_the_version()
    {
        var builder = Deals();
        var deals = Make(5);
        var snapshot = builder.Build(deals).Apply(builder.Batch(changed: [deals[0]])).After;

        using var reader = new ArrowStreamReader(await WriteAsync(snapshot));
        var schema = reader.Schema;

        Assert.Equal("1", schema.Metadata[SnapshotArrowMetadata.Version]);
        Assert.Equal("Id", schema.Metadata[SnapshotArrowMetadata.RecordKey]);
        Assert.Equal("exgrid.caption", SnapshotArrowMetadata.Caption);
        Assert.Equal("exgrid.recordKey", SnapshotArrowMetadata.RecordKey);
        Assert.Equal("exgrid.version", SnapshotArrowMetadata.Version);
        Assert.Equal("Trading desk", schema.GetFieldByName("Desk").Metadata[SnapshotArrowMetadata.Caption]);
        Assert.Equal("Price (USD)", schema.GetFieldByName("Price").Metadata[SnapshotArrowMetadata.Caption]);
        // A caption that is the name is not written.
        Assert.False(schema.GetFieldByName("Notional").HasMetadata);
    }

    [Fact] // ADR-0064: a Snapshot without a Record Key writes none
    public async Task A_snapshot_without_a_key_writes_none()
    {
        var snapshot = new SnapshotBuilder<Deal>().Integer("Id", d => d.Id).Build(Make(3));

        using var reader = new ArrowStreamReader(await WriteAsync(snapshot));

        Assert.False(reader.Schema.Metadata.ContainsKey(SnapshotArrowMetadata.RecordKey));
        Assert.Equal("0", reader.Schema.Metadata[SnapshotArrowMetadata.Version]);
    }

    public static TheoryData<string, string[]> DateUnits => new()
    {
        { "date32", ["2026-01-01", "1969-12-31", "0001-01-01", "9999-12-31"] },
        { "timestamp[s]", ["2026-01-01", "2026-01-01T12:00:01"] },
        { "timestamp[ms]", ["2026-01-01T12:00:01", "2026-01-01T12:00:01.5"] },
        { "timestamp[us]", ["2026-01-01T12:00:01.5", "1969-12-31T23:59:59.999999"] },
        { "timestamp[ns]", ["2026-01-01T12:00:00.0000001", "1970-01-01", "2262-04-11"] },
    };

    [Theory] // ADR-0064: Date is date32 when every value is a midnight, else a timestamp without a zone in the coarsest unit that holds every value exactly
    [MemberData(nameof(DateUnits))]
    public async Task A_date_takes_the_coarsest_unit_that_holds_every_value(string type, string[] values)
    {
        var dates = values.Select(v => (DateTime?)DateTime.Parse(v, CultureInfo.InvariantCulture)).Append(null).ToArray();
        var snapshot = new SnapshotBuilder<DateTime?>().Date("When", d => d).Build(dates);

        var payload = await WriteAsync(snapshot);

        using var reader = new ArrowStreamReader(payload);
        var field = reader.Schema.GetFieldByName("When");
        Assert.Equal(type, ArrowTypeNames.Of(field.DataType));
        Assert.True(field.DataType is not TimestampType stamp || stamp.Timezone is null);
        AssertSame(snapshot, await ReadAsync(payload));
    }

    [Fact] // ADR-0064: a Date column of Blanks alone is date32
    public async Task A_date_column_of_blanks_is_date32()
    {
        var snapshot = new SnapshotBuilder<DateTime?>().Date("When", d => d).Build([null, null]);

        using var reader = new ArrowStreamReader(await WriteAsync(snapshot));

        Assert.IsType<Date32Type>(reader.Schema.GetFieldByName("When").DataType);
    }

    [Fact] // ADR-0064: a date no unit holds exactly — it needs nanoseconds, and lies outside their reach — is refused by row and column, and nothing is written
    public async Task A_date_no_unit_holds_is_refused()
    {
        DateTime?[] dates = [new DateTime(2026, 1, 1).AddTicks(1), null, new DateTime(2300, 1, 1)];
        var snapshot = new SnapshotBuilder<DateTime?>().Date("When", d => d).Build(dates);
        using var stream = new MemoryStream();

        var refusal = await Assert.ThrowsAsync<SnapshotException>(() => SnapshotArrow.WriteAsync(snapshot, stream, TestContext.Current.CancellationToken));

        Assert.Equal(3, refusal.Row);
        Assert.Equal("When", refusal.Column);
        Assert.Equal("Row 3, column 'When': the date 2300-01-01T00:00:00.0000000 cannot be written: the column holds a time finer than a microsecond, so it is written in nanoseconds, which reach only from 1677-09-21 to 2262-04-11.", refusal.Message);
        Assert.Equal(0, stream.Length);
    }

    [Fact] // ADR-0064: Decimal is decimal128(38, scale) at the largest scale, and a value that would pass 38 digits there is refused by row and column, and nothing is written
    public async Task A_decimal_past_38_digits_at_the_columns_scale_is_refused()
    {
        var builder = new SnapshotBuilder<(long Id, decimal? Value)>().Integer("Id", v => v.Id).Decimal("Value", v => v.Value).Key("Id");
        var snapshot = builder.Build([(1, decimal.MaxValue), (2, 1m)]);
        snapshot = snapshot.Apply(builder.Batch(added: [(3, 0.0000000001m)])).After;
        using var stream = new MemoryStream();

        var refusal = await Assert.ThrowsAsync<SnapshotException>(() => SnapshotArrow.WriteAsync(snapshot, stream, TestContext.Current.CancellationToken));

        Assert.Equal(1, refusal.Row);
        Assert.Equal("Value", refusal.Column);
        Assert.Equal("Row 1, column 'Value': the value 79228162514264337593543950335 cannot be written exactly as decimal128(38, 10): at the column's 10 places it passes 38 digits.", refusal.Message);
        Assert.Equal(0, stream.Length);
    }

    [Fact] // ADR-0064: the extreme decimals that fit 38 digits at the column's scale are written exactly
    public async Task Decimals_up_to_38_digits_are_written_exactly()
    {
        var builder = new SnapshotBuilder<(long Id, decimal? Value)>().Integer("Id", v => v.Id).Decimal("Value", v => v.Value).Key("Id");
        var snapshot = builder.Build([(1, decimal.MaxValue), (2, decimal.MinValue), (3, long.MinValue), (4, 0m)]);
        snapshot = snapshot.Apply(builder.Batch(added: [(5, 0.000000001m), (6, -0.000000001m)])).After;

        var payload = await WriteAsync(snapshot);

        using var reader = new ArrowStreamReader(payload);
        Assert.Equal("decimal128(38, 9)", ArrowTypeNames.Of(reader.Schema.GetFieldByName("Value").DataType));
        AssertSame(snapshot, await ReadAsync(payload));
    }

    [Fact] // ADR-0064: text that is not valid Unicode cannot be UTF-8, and is refused by row and column
    public async Task Text_that_is_not_unicode_is_refused()
    {
        var snapshot = new SnapshotBuilder<string?>().Text("Name", v => v).Build(["fine", null, "lone \uD800 surrogate"]);
        using var stream = new MemoryStream();

        var refusal = await Assert.ThrowsAsync<SnapshotException>(() => SnapshotArrow.WriteAsync(snapshot, stream, TestContext.Current.CancellationToken));

        Assert.Equal("Row 3, column 'Name': the text is not valid Unicode — it holds a lone surrogate — so UTF-8 cannot carry it.", refusal.Message);
        Assert.Equal(0, stream.Length);
    }

    [Fact] // ADR-0064: Text is written as the Snapshot's own dictionary, its codes the indices — after Change Batches, entries no row holds included
    public async Task Text_is_written_as_the_snapshots_own_dictionary()
    {
        var builder = new SnapshotBuilder<(long Id, string Desk)>().Integer("Id", v => v.Id).Text("Desk", v => v.Desk).Key("Id");
        var snapshot = builder.Build([(1, "EMEA"), (2, "AMER"), (3, "APAC")]);
        snapshot = snapshot.Apply(builder.Batch(changed: [(2, "LATAM")], removedKeys: [3L])).After;

        using var reader = new ArrowStreamReader(await WriteAsync(snapshot));
        var desk = (DictionaryArray)reader.ReadNextRecordBatch()!.Column("Desk");
        var entries = (StringArray)desk.Dictionary;

        Assert.Equal(["EMEA", "AMER", "APAC", "LATAM"], Enumerable.Range(0, entries.Length).Select(i => entries.GetString(i)));
        Assert.Equal([0, 3], ((Int32Array)desk.Indices).Values.ToArray());
    }

    [Fact] // ADR-0064: rows go out in record batches of 65,536, the last holding the rest
    public async Task Rows_go_out_in_record_batches_of_65536()
    {
        var snapshot = new SnapshotBuilder<int>().Integer("N", n => n).Build([.. Enumerable.Range(0, 150_000)]);

        using var stream = new MemoryStream();
        await SnapshotArrow.WriteAsync(snapshot, stream, TestContext.Current.CancellationToken);
        using var reader = new ArrowStreamReader(stream.ToArray());
        var lengths = new List<int>();
        while (reader.ReadNextRecordBatch() is { } batch)
            lengths.Add(batch.Length);

        Assert.Equal([65_536, 65_536, 18_928], lengths);
    }

    [Fact] // ADR-0064: the write writes asynchronously, as a server's response body requires, and leaves the stream open
    public async Task The_write_is_asynchronous_and_leaves_the_stream_open()
    {
        var snapshot = Deals().Build(Make(1_000));
        using var stream = new AsynchronousOnlyStream();

        await SnapshotArrow.WriteAsync(snapshot, stream, TestContext.Current.CancellationToken);

        Assert.True(stream.CanWrite);
        AssertSame(snapshot, await ReadAsync(stream.ToArray()));
    }

    [Fact] // ADR-0064: a cancelled write throws, between record batches
    public async Task A_cancelled_write_throws()
    {
        var snapshot = Deals().Build(Make(10));
        using var stream = new MemoryStream();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotArrow.WriteAsync(snapshot, stream, cancelled.Token));
    }

    /// <summary>A stream that refuses synchronous writes, as ASP.NET Core's response body does.</summary>
    private sealed class AsynchronousOnlyStream : Stream
    {
        private readonly MemoryStream written = new();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] ToArray() => written.ToArray();

        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override void Flush() => throw new InvalidOperationException("Synchronous operations are disallowed.");

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => written.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => written.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
