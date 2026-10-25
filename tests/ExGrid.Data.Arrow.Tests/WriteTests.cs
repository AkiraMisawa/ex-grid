using System.Globalization;
using Apache.Arrow;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// What a write does that the shared cases (<see cref="Cases.ArrowCaseTests"/>) cannot show (DA-15): Apache.Arrow's
/// own accessors reading it, a Snapshot after Change Batches, record batches of 65,536 rows, and an asynchronous,
/// cancellable write. Each kind's Arrow type, the metadata and every refusal are cases under
/// <c>tests/cases/arrow/</c>, which <c>@ex-grid/data</c> runs too.
/// </summary>
public class WriteTests
{
    [Fact] // ADR-0065: what is written reads back through Apache.Arrow's own accessors as the Snapshot's values, a Blank a null slot
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

    [Fact] // ADR-0065: an entry no row holds any more is no value, so one UTF-8 cannot carry is written with U+FFFD rather than refusing the Snapshot
    public async Task A_dictionary_entry_no_row_holds_is_written_even_when_not_unicode()
    {
        var builder = new SnapshotBuilder<(long Id, string Name)>().Integer("Id", v => v.Id).Text("Name", v => v.Name).Key("Id");
        var snapshot = builder.Build([(1, "fine"), (2, "lone \uD800 surrogate")]);
        snapshot = snapshot.Apply(builder.Batch(changed: [(2, "mended")])).After;

        var payload = await WriteAsync(snapshot);

        AssertSame(snapshot, await ReadAsync(payload), sameDictionaries: false);
        using var reader = new ArrowStreamReader(payload);
        var entries = (StringArray)((DictionaryArray)reader.ReadNextRecordBatch()!.Column("Name")).Dictionary;
        Assert.Equal(["fine", "lone � surrogate", "mended"], Enumerable.Range(0, entries.Length).Select(i => entries.GetString(i)));
    }

    [Fact] // ADR-0065: Text is written as the Snapshot's own dictionary, its codes the indices — after Change Batches, entries no row holds included
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

    [Fact] // ADR-0065: rows go out in record batches of 65,536, the last holding the rest
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

    [Fact] // ADR-0065: the write writes asynchronously, as a server's response body requires, and leaves the stream open
    public async Task The_write_is_asynchronous_and_leaves_the_stream_open()
    {
        var snapshot = Deals().Build(Make(1_000));
        using var stream = new AsynchronousOnlyStream();

        await SnapshotArrow.WriteAsync(snapshot, stream, TestContext.Current.CancellationToken);

        Assert.True(stream.CanWrite);
        AssertSame(snapshot, await ReadAsync(stream.ToArray()));
    }

    [Fact] // ADR-0065: a cancelled write throws, between record batches
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
