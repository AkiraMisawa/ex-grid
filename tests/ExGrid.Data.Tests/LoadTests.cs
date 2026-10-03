using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>Every builder takes a CancellationToken, reports progress and yields between slices
/// (ADR-0064, DA-5).</summary>
public class LoadTests
{
    [Fact] // ADR-0064: a build from objects works in slices, yields between them and reports its progress
    public async Task A_build_yields_between_slices_and_reports_its_progress()
    {
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

        var snapshot = await Trades().BuildAsync(Trades(50_000), options, TestContext.Current.CancellationToken);

        Assert.Equal(50_000, snapshot.RowCount);
        Assert.True(yields >= 48, $"The build yielded {yields} times.");
        Assert.Equal(yields + 1, reports.Count);
        Assert.All(reports, p => Assert.Equal(50_000, p.TotalRows));
        Assert.Equal(new SnapshotProgress(50_000, 50_000), reports[^1]);
        Assert.Equal(reports.Select(p => p.Rows).Order(), reports.Select(p => p.Rows));
    }

    [Fact] // ADR-0064: a slice works until its budget is spent, so a generous budget yields seldom
    public async Task A_slice_works_until_its_budget_is_spent()
    {
        var yields = 0;
        var options = new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.FromMinutes(1),
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        };

        await Trades().BuildAsync(Trades(20_000), options, TestContext.Current.CancellationToken);

        Assert.Equal(0, yields);
    }

    [Fact] // ADR-0064: without a Yield of its own, a build yields as the platform does, and finishes
    public async Task The_default_yield_lets_the_build_finish()
    {
        var records = Trades(5_000);

        var snapshot = await Trades().BuildAsync(records, new SnapshotLoadOptions { SliceBudget = TimeSpan.Zero }, TestContext.Current.CancellationToken);

        AssertHolds(records, snapshot);
    }

    [Fact] // ADR-0064: a cancelled build throws, and yields nothing
    public async Task A_cancelled_build_throws_and_yields_nothing()
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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => built = await Trades().BuildAsync(Trades(50_000), options, cancel.Token));

        Assert.Null(built);
        Assert.Equal(3, yields);
    }

    [Fact] // ADR-0064: a build asked for with a cancelled token does not start
    public async Task A_build_cancelled_before_it_starts_does_not_start()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();
        var reports = new List<SnapshotProgress>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Trades().BuildAsync(Trades(10), new SnapshotLoadOptions { Progress = new Told(reports) }, cancel.Token));

        Assert.Empty(reports);
    }

    [Fact] // ADR-0064: a reader's checkpoints yield when the slice is spent, reporting its rows and bytes
    public async Task A_readers_checkpoints_yield_and_report_rows_and_bytes()
    {
        var yields = 0;
        var reports = new List<SnapshotProgress>();
        var builder = new SnapshotColumnsBuilder(
            new SnapshotLoadOptions
            {
                SliceBudget = TimeSpan.Zero,
                Yield = () =>
                {
                    yields++;
                    return ValueTask.CompletedTask;
                },
                Progress = new Told(reports),
            },
            TestContext.Current.CancellationToken)
        {
            TotalRows = 100,
        };
        var id = builder.Integer("Id");
        var name = builder.Text("Name");

        for (var i = 0; i < 100; i++)
        {
            id.Append(i);
            await builder.CheckpointAsync(bytes: i * 10L, totalBytes: 1_000);
            name.Append($"n{i % 3}");
            await builder.CheckpointAsync(bytes: (i * 10L) + 5, totalBytes: 1_000);
        }
        var snapshot = await builder.BuildAsync();

        Assert.Equal(200, yields);
        Assert.Equal(new SnapshotProgress(0, 100, 0, 1_000), reports[0]);
        Assert.Equal(new SnapshotProgress(1, 100, 5, 1_000), reports[1]);
        Assert.Equal(new SnapshotProgress(100, 100, 995, 1_000), reports[199]);
        Assert.Equal(new SnapshotProgress(100, 100), reports[^1]);
        Assert.Equal(100, snapshot.RowCount);
    }

    [Fact] // ADR-0064: a reader's load, cancelled, throws at its next checkpoint and builds nothing
    public async Task A_cancelled_reader_load_throws_at_its_next_checkpoint()
    {
        using var cancel = new CancellationTokenSource();
        var builder = new SnapshotColumnsBuilder(cancellationToken: cancel.Token);
        var id = builder.Integer("Id");
        id.Append(1);
        await builder.CheckpointAsync();

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await builder.CheckpointAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await builder.BuildAsync());
    }

    [Fact] // ADR-0064: the Record Keys of a reader's load are indexed in slices too
    public async Task A_readers_keys_are_indexed_in_slices()
    {
        var yields = 0;
        var builder = new SnapshotColumnsBuilder(new SnapshotLoadOptions
        {
            SliceBudget = TimeSpan.Zero,
            Yield = () =>
            {
                yields++;
                return ValueTask.CompletedTask;
            },
        }, TestContext.Current.CancellationToken);
        var id = builder.Integer("Id");
        id.Append([.. Enumerable.Range(0, 10_000).Select(i => (long)i)]);
        builder.Key("Id");

        var snapshot = await builder.BuildAsync();

        Assert.True(yields >= 9, $"The keys were indexed with {yields} yields.");
        Assert.Equal(10_000, snapshot.RowCount);
    }

    /// <summary>Progress told at once, on the thread that reports it.</summary>
    private sealed class Told(List<SnapshotProgress> reports) : IProgress<SnapshotProgress>
    {
        public void Report(SnapshotProgress value) => reports.Add(value);
    }
}
