using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// <c>GridSource.From</c> with a Row Key (ADR-0141), where only C# can read it: its Change Highlight
/// (LV-4, LV-9), the threads and contexts a change arrives on (LV-16), and its row marks. The live data
/// itself is in the shared cases (tests/cases/source/live.json). The clock is a fake one the tests drive.
/// </summary>
public class LiveSourceTests
{
    private sealed record Deal(string Id, string Book, decimal Amount);

    private static readonly GridColumn<Deal> IdColumn = new("Id", ColumnType.Text, d => d.Id);
    private static readonly GridColumn<Deal> BookColumn = new("Book", ColumnType.Text, d => d.Book);
    // Painted with no decimals: a change the format hides (ADR-0067).
    private static readonly GridColumn<Deal> AmountColumn = new("Amount", ColumnType.Number, d => d.Amount,
        format: value => ((decimal)value).ToString("0", CultureInfo.InvariantCulture));

    private static readonly IReadOnlyList<ColumnInfo<Deal>> Columns = [IdColumn.Info, BookColumn.Info, AmountColumn.Info];

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

    // The source's live data itself (batches, whole lists, gathering, the Window it shows) is in the shared
    // cases, tests/cases/source/live.json. What stays here reads what only C# has: the Change Highlight's
    // times, the threads and contexts a change arrives on, and the row marks.

    [Fact] // ADR-0141: without a key the source keeps no change times
    public void Without_a_key_no_change_times_are_kept()
    {
        Assert.Null(GridSource.From(Deals()).CellChangedAt);
    }

    [Fact] // ADR-0141 / LV-10: the key a keyed source names its rows by is the one it was given
    public void A_keyed_source_names_its_rows_by_its_key()
    {
        IGridSource<Deal> source = Live(new FakeTimeProvider(), out var rows);

        Assert.Equal("C", source.RowKey!(rows[2]));
        Assert.Null(((IGridSource<Deal>)GridSource.From(Deals())).RowKey);
    }

    // ---- LV-4: a whole new list, paired by key -----------------------------------------------

    [Fact] // ADR-0141 / LV-4 (D7), LV-9: a row added inside the list stands where the list puts it, and is marked whole; the rows it moved are not marked
    public void A_row_added_inside_the_list_stands_where_the_list_puts_it()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows);
        var changedAt = source.CellChangedAt!;
        var version = source.RowSequenceVersion;

        source.ReplaceAll([rows[0], new Deal("E", "FX", 6), rows[1], rows[2], rows[3]]);

        Assert.Equal("A,E,B,C,D", Ids(source));
        Assert.Equal(version + 1, source.RowSequenceVersion);
        Assert.All(new[] { IdColumn, BookColumn, AmountColumn }, column => Assert.Equal(clock.GetUtcNow(), changedAt(source.Window[1], column)));
        foreach (var row in source.Window.Where(d => d.Id != "E"))
            Assert.All(new[] { IdColumn, BookColumn, AmountColumn }, column => Assert.Null(changedAt(row, column)));
    }

    [Fact] // ADR-0141 / LV-4 (D7), LV-6, LV-9: a new order is gathered like any change; a change gathered after it marks only its own cells
    public void A_new_order_is_gathered_and_a_later_change_marks_only_its_cells()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        var changedAt = source.CellChangedAt!;
        source.Apply(new(changed: [rows[3] with { Book = "Rates" }]));
        var d = source.Window[3];

        source.ReplaceAll([d, rows[2], rows[1], rows[0]]);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        var b = rows[1] with { Amount = 9 };
        source.Apply(new(changed: [b]));
        Assert.Equal("A,B,C,D", Ids(source));

        clock.Advance(TimeSpan.FromMilliseconds(150));

        Assert.Equal("D,C,B,A", Ids(source));
        Assert.Same(b, source.Window[2]);
        Assert.Equal(clock.GetUtcNow(), changedAt(b, AmountColumn));
        Assert.Null(changedAt(b, BookColumn));
        foreach (var row in source.Window.Where(r => r.Id != "B"))
            Assert.Null(changedAt(row, AmountColumn));
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

    [Fact] // ADR-0141 / LV-9: a cell is marked when a changed row's painted text differs; other cells are not
    public void A_changed_cell_is_marked_and_the_others_are_not()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows);
        var changedAt = source.CellChangedAt!;

        clock.Advance(TimeSpan.FromSeconds(3));
        var now = clock.GetUtcNow();
        source.Apply(new(changed: [rows[1] with { Amount = 9 }]));

        var b = source.Window[1];
        Assert.Equal(now, changedAt(b, AmountColumn));
        Assert.Null(changedAt(b, BookColumn));
        Assert.Null(changedAt(source.Window[0], AmountColumn));
    }

    [Fact] // ADR-0141 / LV-9, ADR-0067: a change the format hides is not marked
    public void A_change_the_format_hides_is_not_marked()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var changedAt = source.CellChangedAt!;

        source.Apply(new(changed: [rows[0] with { Amount = 3.2m }]));

        Assert.Null(changedAt(source.Window[0], AmountColumn));
    }

    [Fact] // ADR-0141 / LV-9: every cell of an added row is marked
    public void Every_cell_of_an_added_row_is_marked()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out _);
        var changedAt = source.CellChangedAt!;
        var now = clock.GetUtcNow();

        source.Apply(new(added: [new Deal("E", "Rates", 4)]));

        var e = source.Window[^1];
        Assert.All(new[] { IdColumn, BookColumn, AmountColumn }, column => Assert.Equal(now, changedAt(e, column)));
    }

    [Fact] // ADR-0141 / LV-9: a sort, a filter or a column change marks nothing
    public void A_sort_a_filter_or_a_column_change_marks_nothing()
    {
        var source = Live(new FakeTimeProvider(), out _);
        var changedAt = source.CellChangedAt!;

        source.OnSortChanged(ByAmount);
        source.OnFilterChanged(AmountAtLeast(2));
        source.OnColumnsChanged([IdColumn.Info, AmountColumn.Info]);

        foreach (var row in source.Window)
        {
            foreach (var column in new[] { IdColumn, BookColumn, AmountColumn })
                Assert.Null(changedAt(row, column));
        }
    }

    [Fact] // ADR-0141 / LV-9: the gathered changes a sort brings with it are marked; the sort itself marks nothing
    public void Changes_a_sort_brings_are_marked()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        var changedAt = source.CellChangedAt!;
        source.Apply(new(changed: [rows[3] with { Book = "Rates" }]));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        var now = clock.GetUtcNow();
        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        source.OnSortChanged(ByAmount);

        Assert.Equal("B,C,D,A", Ids(source));
        Assert.Equal(now, changedAt(source.Window[3], AmountColumn));
        Assert.Null(changedAt(source.Window[0], AmountColumn));
    }

    [Fact] // ADR-0141 / LV-9: the user's own edit marks nothing
    public void The_users_own_edit_marks_nothing()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var changedAt = source.CellChangedAt!;

        source.ReplaceRow(rows[0], rows[0] with { Amount = 30 });

        Assert.Null(changedAt(source.Window[0], AmountColumn));
    }

    [Fact] // ADR-0141 / LV-9: one delegate for the source's life
    public void One_delegate_is_held_across_versions()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var first = source.CellChangedAt;

        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        Assert.Same(first, source.CellChangedAt);
    }

    [Fact] // ADR-0141 / LV-9: a removed key takes its times with it; added again, it is a row that appears
    public void A_removed_key_is_forgotten()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows);
        var changedAt = source.CellChangedAt!;
        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));
        source.Apply(new(removedKeys: ["A"]));

        clock.Advance(TimeSpan.FromSeconds(2));
        var now = clock.GetUtcNow();
        source.Apply(new(added: [rows[0]]));

        var a = source.Window[^1];
        Assert.Equal(now, changedAt(a, AmountColumn));
        Assert.Equal(now, changedAt(a, BookColumn));
    }

    [Fact] // ADR-0141 / LV-9: a time older than ChangeTimesKeptFor answers nothing; memory is bounded by it
    public void A_change_time_is_kept_for_a_bounded_time()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows);
        var changedAt = source.CellChangedAt!;
        Assert.Equal(TimeSpan.FromMinutes(1), source.ChangeTimesKeptFor);
        source.ChangeTimesKeptFor = TimeSpan.FromSeconds(10);
        var then = clock.GetUtcNow();
        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(then, changedAt(source.Window[0], AmountColumn));
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Null(changedAt(source.Window[0], AmountColumn));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ChangeTimesKeptFor = TimeSpan.Zero);
    }

    [Fact] // ADR-0141 / LV-9, ADR-0068: nothing is compared until the delegate has been asked for
    public void Nothing_is_recorded_before_the_delegate_is_asked_for()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        source.Apply(new(changed: [rows[0] with { Amount = 9 }]));

        var changedAt = source.CellChangedAt!;

        Assert.Null(changedAt(source.Window[0], AmountColumn));
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
