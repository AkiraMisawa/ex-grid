using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// <c>GridSource.From</c> with a Row Key (ADR-0141), where only C# can read it: the threads and contexts
/// a change arrives on (LV-16), its Change Highlight's one delegate (LV-9), and its row marks. The live data
/// itself is in the shared cases (tests/cases/source/live.json). The clock is a fake one the tests drive.
/// </summary>
public class LiveSourceTests
{
    private sealed record Deal(string Id, string Book, decimal Amount);

    private static readonly IReadOnlyList<ColumnInfo<Deal>> Columns =
    [
        new("Id", ColumnType.Text, d => d.Id),
        new("Book", ColumnType.Text, d => d.Book),
        new("Amount", ColumnType.Number, d => d.Amount),
    ];

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private static Deal[] Deals() =>
    [
        new("A", "Rates", 3),
        new("B", "Credit", 1),
        new("C", "Rates", 2),
        new("D", "FX", 5),
    ];

    private static InMemoryGridSource<Deal> Live(FakeTimeProvider clock, out Deal[] rows, bool gather = false)
    {
        rows = Deals();
        var source = GridSource.From(rows, d => d.Id, clock);
        source.OnColumnsChanged(Columns);
        if (!gather)
            source.GatherInterval = TimeSpan.Zero;
        return source;
    }

    private static string Ids(IGridSource<Deal> source) => string.Join(",", source.Window.Select(d => d.Id));

    private static GridFilter AmountAtLeast(decimal amount) => new(new Dictionary<string, FilterSpec>
    {
        ["Amount"] = new([new FilterClause(FilterOperator.GreaterThanOrEqual, amount)]),
    });

    private static readonly SortSpec[] ByAmount = [new("Amount", SortDirection.Ascending)];

    // The source's live data itself (batches, whole lists, gathering, the Window it shows) and its Change
    // Highlight are in the shared cases, tests/cases/source/live.json and change-highlight.json. What stays
    // here reads what only C# has: the threads and contexts a change arrives on, one delegate held, and the
    // row marks.

    [Fact] // ADR-0141 / LV-10: the key a keyed source names its rows by is the one it was given
    public void A_keyed_source_names_its_rows_by_its_key()
    {
        IGridSource<Deal> source = Live(new FakeTimeProvider(), out var rows);

        Assert.Equal("C", source.RowKey!(rows[2]));
        Assert.Null(((IGridSource<Deal>)GridSource.From(Deals())).RowKey);
    }

    // ---- LV-7: a row changed in place ----------------------------------------------------------

    private sealed class Mutable(string id, decimal amount)
    {
        public string Id { get; } = id;
        public decimal Amount { get; set; } = amount;
    }

    [Fact] // ADR-0003 / LV-7: a row changed in place, then changed out of the result, moves the Row Sequence Version
    public void A_row_changed_in_place_then_out_of_the_result_moves_the_version()
    {
        // Changed in place, which ADR-0003 forbids, so the search for its old version misses: the whole
        // requery drops it all the same, and the grid must hear that the sequence moved.
        Mutable[] rows = [new("A", 3), new("B", 5), new("C", 7)];
        var source = GridSource.From(rows, r => r.Id, new FakeTimeProvider());
        source.OnColumnsChanged([new ColumnInfo<Mutable>("Amount", ColumnType.Number, r => r.Amount)]);
        source.OnFilterChanged(AmountAtLeast(2));
        source.OnSortChanged(ByAmount);
        source.GatherInterval = TimeSpan.Zero;
        var before = source.RowSequenceVersion;

        rows[0].Amount = 6;
        source.Apply(new(changed: [new Mutable("A", 0)]));

        Assert.Equal(["B", "C"], source.Window.Select(r => r.Id));
        Assert.Equal(before + 1, source.RowSequenceVersion);
    }

    // ---- LV-16: what was gathered is put out before a write is judged --------------------------

    [Fact] // ADR-0141 / LV-16, principle 6: a publication posted to the source's context, which PublishGathered took first, does not run again early
    public async Task A_posted_publication_that_PublishGathered_took_does_not_run_again()
    {
        var context = new QueueContext();
        var previous = SynchronizationContext.Current;
        var clock = new FakeTimeProvider();
        InMemoryGridSource<Deal> source;
        Deal[] rows;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            source = Live(clock, out rows, gather: true);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        var token = TestContext.Current.CancellationToken;

        // After a quiet interval: decided at once, and posted to the source's context.
        await Task.Run(() => source.Apply(new(changed: [rows[0] with { Amount = 7 }])), token);
        source.PublishGathered();
        Assert.Equal(7m, source.Window[0].Amount);
        // Within the interval from that publication: gathered.
        await Task.Run(() => source.Apply(new(changed: [rows[1] with { Amount = 8 }])), token);

        context.RunAll();

        Assert.Equal(1m, source.Window[1].Amount);
        clock.Advance(Interval);
        context.RunAll();
        Assert.Equal(8m, source.Window[1].Amount);
    }

    [Fact] // ADR-0141 / LV-6: changes may arrive on any thread; every one lands, and the Window is the newest version
    public async Task Changes_from_many_threads_all_land()
    {
        var clock = new FakeTimeProvider();
        var source = GridSource.From<Deal>([], d => d.Id, clock);
        source.OnColumnsChanged(Columns);
        source.OnSortChanged(ByAmount);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(thread => Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                source.Apply(new(added: [new Deal($"{thread}-{i}", "Rates", (thread * 1000) + i)]));
                if (i % 50 == 0)
                    clock.Advance(TimeSpan.FromMilliseconds(100));
            }
        }, TestContext.Current.CancellationToken)));
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1600, source.TotalCount);
        var expected = Enumerable.Range(0, 8).SelectMany(t => Enumerable.Range(0, 200).Select(i => (t * 1000) + i)).Order();
        Assert.Equal(expected.Select(a => (decimal)a), source.Window.Select(d => d.Amount));
    }

    [Fact] // ADR-0141 / LV-6: the Window and the version are replaced on the context the source was built on
    public async Task A_change_from_another_thread_is_published_on_the_sources_context()
    {
        var context = new QueueContext();
        var previous = SynchronizationContext.Current;
        InMemoryGridSource<Deal> source;
        Deal[] rows;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            source = Live(new FakeTimeProvider(), out rows);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await Task.Run(() => source.Apply(new(changed: [rows[0] with { Amount = 7 }])), TestContext.Current.CancellationToken);

        Assert.Equal(3m, source.Window[0].Amount);
        context.RunAll();
        Assert.Equal(7m, source.Window[0].Amount);
    }

    private sealed class QueueContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_posted)
                _posted.Enqueue((d, state));
        }

        public void RunAll()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) next;
                lock (_posted)
                {
                    if (!_posted.TryDequeue(out next))
                        return;
                }
                next.Callback(next.State);
            }
        }
    }

    // ---- LV-9: the Change Highlight, the source's half ----------------------------------------

    [Fact] // ADR-0141 / LV-9: one delegate for the source's life
    public void One_delegate_is_held_across_versions()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var first = source.CellChangedAt;

        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        Assert.Same(first, source.CellChangedAt);
    }

    [Fact] // ADR-0141 / ADR-0043: under a key, a mark follows the row through its versions and leaves with it
    public async Task A_row_mark_follows_the_key()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        await source.Marks.OnMarkIntentAsync(new Rows.RowMarkIntent<Deal>.OneRow(rows[1], true));

        source.Apply(new(changed: [rows[1] with { Amount = 9 }]));
        Assert.True(source.Marks.IsMarked(source.Window[1]));
        Assert.Equal(new Rows.RowMarkCounts(1, 4, 0), source.Marks.Counts);

        source.Apply(new(removedKeys: ["B"]));
        source.Apply(new(added: [rows[1]]));
        Assert.False(source.Marks.IsMarked(source.Window[^1]));
        Assert.Empty(source.Marks.MarkedRows);
    }

    [Fact] // ADR-0141 / ADR-0043: a removed row's mark goes when the removal is shown, not when it is applied — Apply may run on any thread, and the marks are read on the grid's context
    public async Task A_removed_rows_mark_goes_when_the_removal_is_shown()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        await source.Marks.OnMarkIntentAsync(new Rows.RowMarkIntent<Deal>.OneRow(rows[1], true));
        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        // Within the interval: gathered, the row still shown, and its mark with it.
        source.Apply(new(removedKeys: ["B"]));
        Assert.Equal("A,B,C,D", Ids(source));
        Assert.True(source.Marks.IsMarked(source.Window[1]));

        clock.Advance(TimeSpan.FromMilliseconds(250));

        Assert.Equal("A,C,D", Ids(source));
        Assert.Empty(source.Marks.MarkedRows);
    }

    [Fact] // ADR-0141 / ADR-0043: a key removed and added again within one gathering is a changed row, and keeps its mark
    public async Task A_key_removed_and_added_again_within_one_gathering_keeps_its_mark()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        await source.Marks.OnMarkIntentAsync(new Rows.RowMarkIntent<Deal>.OneRow(rows[1], true));
        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        source.Apply(new(removedKeys: ["B"]));
        source.Apply(new(added: [rows[1] with { Amount = 7 }]));
        clock.Advance(TimeSpan.FromMilliseconds(250));

        Assert.True(source.Marks.IsMarked(source.Window.Single(d => d.Id == "B")));
    }
}
