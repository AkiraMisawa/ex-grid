using System.Text;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;

namespace ExGrid.Data.Tests;

/// <summary>A CSV load takes a CancellationToken, reports its progress in rows and bytes, and yields
/// between slices (ADR-0063, DA-5).</summary>
public class CsvLoadTests
{
    private static readonly CsvSchema Schema = new([new("Id", SnapshotKind.Integer), new("Desk", SnapshotKind.Text)]) { RecordKey = "Id" };

    private static byte[] File(int rows)
    {
        var text = new StringBuilder("Id,Desk\r\n");
        for (var i = 0; i < rows; i++)
            text.Append(i).Append(",D").Append(i % 7).Append("\r\n");
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    [Fact] // ADR-0063: a CSV load works in slices, yields between them and reports rows and bytes, with the total the stream knows
    public async Task A_load_yields_between_slices_and_reports_rows_and_bytes()
    {
        var bytes = File(20_000);
        var yields = 0;
        var reports = new List<SnapshotProgress>();
        var options = new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
            Progress = new Told(reports),
        };

        var snapshot = await Schema.ReadAsync(new MemoryStream(bytes), options, TestContext.Current.CancellationToken);

        Assert.Equal(20_000, snapshot.RowCount);
        Assert.True(yields >= 19, $"The load yielded {yields} times.");
        var reading = reports.Where(p => p.Bytes is not null).ToList();
        Assert.True(reading.Count >= 19);
        Assert.All(reading, p => Assert.Equal(bytes.Length, p.TotalBytes));
        Assert.Equal(reading.Select(p => p.Bytes), reading.Select(p => p.Bytes).Order());
        Assert.Equal(reading.Select(p => p.Rows), reading.Select(p => p.Rows).Order());
        Assert.Contains(new SnapshotProgress(20_000, 20_000, bytes.Length, bytes.Length), reports);
        Assert.Equal(new SnapshotProgress(20_000, 20_000), reports[^1]);
    }

    [Fact] // ADR-0063: a stream that does not know its length reports the bytes read, and no total
    public async Task A_stream_of_unknown_length_reports_no_total()
    {
        var bytes = File(5_000);
        var reports = new List<SnapshotProgress>();
        var options = new SnapshotLoadOptions { SliceBudget = TimeSpan.Zero, Yield = () => ValueTask.CompletedTask, Progress = new Told(reports) };

        var snapshot = await Schema.ReadAsync(new Trickle(bytes, chunk: 4_096, seekable: false), options, TestContext.Current.CancellationToken);

        Assert.Equal(5_000, snapshot.RowCount);
        Assert.Contains(reports, p => p.Bytes is > 0 && p.TotalBytes is null);
    }

    [Fact] // ADR-0063: a cancelled load throws, and yields nothing
    public async Task A_cancelled_load_throws_and_yields_nothing()
    {
        using var cancel = new CancellationTokenSource();
        var yields = 0;
        var options = new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Yield = () =>
            {
                if (++yields == 3)
                    cancel.Cancel();
                return ValueTask.CompletedTask;
            },
        };
        Snapshot? built = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => built = await Schema.ReadAsync(new MemoryStream(File(20_000)), options, cancel.Token));

        Assert.Null(built);
        Assert.Equal(3, yields);
    }

    [Fact] // ADR-0063: a load asked for with a cancelled token reads nothing
    public async Task A_load_cancelled_before_it_starts_reads_nothing()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var stream = new Trickle(File(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Schema.ReadAsync(stream, null, cancel.Token));

        Assert.Equal(0, stream.Reads);
    }

    [Fact] // ADR-0063: the stream is read from where it stands, and left open
    public async Task The_stream_is_read_from_where_it_stands_and_left_open()
    {
        var stream = new MemoryStream([.. "junk"u8, .. File(3)]) { Position = 4 };

        var snapshot = await Schema.ReadAsync(stream, null, TestContext.Current.CancellationToken);

        Assert.Equal(3, snapshot.RowCount);
        Assert.True(stream.CanRead);
    }

    [Fact] // ADR-0063: a file is read by its path as its stream is
    public async Task A_file_is_read_by_its_path()
    {
        var path = Path.Combine(Path.GetTempPath(), $"exgrid-data-{Guid.NewGuid():N}.csv");
        await System.IO.File.WriteAllBytesAsync(path, File(1_000), TestContext.Current.CancellationToken);
        try
        {
            var snapshot = await Schema.ReadAsync(path, null, TestContext.Current.CancellationToken);

            Assert.Equal(1_000, snapshot.RowCount);
            Assert.Equal(["D0", "D1"], Fixtures.Values(snapshot, "Desk").Take(2));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
