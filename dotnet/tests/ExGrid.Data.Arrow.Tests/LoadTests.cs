using Apache.Arrow;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests;

/// <summary>
/// A read paces itself as every way into a Snapshot does (ADR-0064, DA-5): it reports its progress,
/// yields between slices, and stops when cancelled, yielding nothing.
/// </summary>
public class LoadTests
{
    private const int BatchRows = 10_000;

    [Theory] // ADR-0064: a read reports its rows as it goes — and its bytes, against the total when it is known — and the whole at the end
    [InlineData("Memory")]
    [InlineData("Seekable")]
    [InlineData("Trickle")]
    public async Task A_read_reports_its_progress(string from)
    {
        var source = Enum.Parse<Source>(from);
        var payload = Payload();
        var progress = new Collected();
        var options = new SnapshotLoadOptions { Progress = progress, SliceBudget = TimeSpan.Zero, Yield = () => ValueTask.CompletedTask };

        var snapshot = await ReadAsync(payload, source, options: options);

        Assert.Equal(3 * BatchRows, snapshot.RowCount);
        var reports = progress.Reports;
        // A report after every chunk of every record batch, each further on than the one before.
        Assert.True(reports.Count >= 6, $"{reports.Count} reports");
        Assert.Equal(reports.Select(r => r.Rows).Order(), reports.Select(r => r.Rows));
        Assert.Equal(new SnapshotProgress(3 * BatchRows, 3 * BatchRows), reports[^1]);
        var reading = reports.Where(r => r.Bytes is not null).ToArray();
        Assert.NotEmpty(reading);
        Assert.Equal(reading.Select(r => r.Bytes).Order(), reading.Select(r => r.Bytes));
        Assert.All(reading, r => Assert.InRange(r.Bytes!.Value, 1, payload.Length));
        // The rows are known ahead only in memory; the bytes are known ahead unless the stream cannot seek.
        Assert.Equal(source == Source.Memory ? 3 * BatchRows : null, reading[0].TotalRows);
        Assert.Equal(source == Source.Trickle ? null : payload.Length, reading[0].TotalBytes);
    }

    [Fact] // ADR-0064: a read yields between its slices, so a browser keeps painting
    public async Task A_read_yields_between_slices()
    {
        var yields = 0;
        var options = new SnapshotLoadOptions { SliceBudget = TimeSpan.Zero, Yield = () => { yields++; return ValueTask.CompletedTask; } };

        await ReadAsync(Payload(), options: options);

        Assert.True(yields >= 6, $"{yields} yields");
    }

    [Theory] // ADR-0064: a read cancelled while it runs throws, and yields no Snapshot
    [InlineData("Memory")]
    [InlineData("Trickle")]
    public async Task A_read_cancelled_while_it_runs_yields_nothing(string from)
    {
        var source = Enum.Parse<Source>(from);
        using var cancel = new CancellationTokenSource();
        var options = new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Progress = new Collected(_ => cancel.Cancel()),
        };
        Snapshot? read = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => read = await (source == Source.Memory
            ? SnapshotArrow.ReadAsync(Payload(), options, cancellationToken: cancel.Token)
            : SnapshotArrow.ReadAsync(new TrickleStream(Payload()), options, cancellationToken: cancel.Token)));

        Assert.Null(read);
    }

    [Fact] // ADR-0064: a read cancelled before it starts reads nothing
    public async Task A_read_cancelled_before_it_starts_reads_nothing()
    {
        using var stream = new MemoryStream(Payload());
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await SnapshotArrow.ReadAsync(stream, cancellationToken: cancel.Token));

        Assert.Equal(0, stream.Position);
    }

    [Fact] // ADR-0065: a read from the Consumer's stream leaves it open, read to the end-of-stream marker and no further
    public async Task A_read_leaves_the_stream_open_after_the_end_of_stream_marker()
    {
        var payload = Payload();
        using var stream = new MemoryStream();
        stream.Write(payload);
        stream.Write("after"u8);
        stream.Position = 0;

        await SnapshotArrow.ReadAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
        Assert.Equal(payload.Length, stream.Position);
    }

    /// <summary>Three record batches of <see cref="BatchRows"/> rows, a little of every kind.</summary>
    private static byte[] Payload()
    {
        RecordBatch Part(int first)
        {
            var numbers = Enumerable.Range(first, BatchRows).Select(i => i % 9 == 0 ? (long?)null : i).ToArray();
            var texts = Enumerable.Range(first, BatchRows).Select(i => i % 11 == 0 ? null : "desk " + (i % 13)).ToArray();
            var pnl = Enumerable.Range(first, BatchRows).Select(i => i % 7 == 0 ? null : Words(i * 25)).ToArray();
            return Batch(("N", Raw(Int64Type.Default, numbers)), ("Desk", Utf8(texts)), ("Pnl", Decimals(new Decimal128Type(18, 2), pnl)));
        }

        return Stream(Part(0), Part(BatchRows), Part(2 * BatchRows));
    }

    /// <summary>Progress collected as it is reported, on the reporting thread.</summary>
    private sealed class Collected(Action<SnapshotProgress>? also = null) : IProgress<SnapshotProgress>
    {
        public List<SnapshotProgress> Reports { get; } = [];

        public void Report(SnapshotProgress value)
        {
            Reports.Add(value);
            also?.Invoke(value);
        }
    }
}
