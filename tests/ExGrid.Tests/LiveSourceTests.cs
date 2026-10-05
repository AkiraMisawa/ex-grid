using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// <c>GridSource.From</c> with a Row Key (ADR-0141): a Change Batch applied whole (LV-3), a whole new
/// list paired by key (LV-4), changes gathered on the source's clock (LV-6), the Row Sequence
/// Version moving exactly when the sequence did (LV-7), the source's half of vouching (LV-10), and
/// its Change Highlight (LV-9). The clock is a fake one the tests drive.
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

    // ---- LV-3: a Change Batch, whole -------------------------------------------------------

    [Fact] // ADR-0141 / LV-3: a changed row keeps its place in the base order, and an added one goes at the end
    public void A_changed_row_keeps_its_place_and_an_added_row_goes_at_the_end()
    {
        var source = Live(new FakeTimeProvider(), out var rows);

        source.Apply(new([new Deal("E", "Rates", 0)], changed: [rows[1] with { Amount = 9 }]));

        Assert.Equal("A,B,C,D,E", Ids(source));
        Assert.Equal(9m, source.Window[1].Amount);
    }

    [Fact] // ADR-0141 / LV-3: the keys removed leave the result
    public void A_removed_key_leaves()
    {
        var source = Live(new FakeTimeProvider(), out _);

        source.Apply(new(removedKeys: ["B", "D"]));

        Assert.Equal("A,C", Ids(source));
        Assert.Equal(2, source.TotalCount);
    }

    [Fact] // ADR-0141 / LV-3: a key named twice in one batch refuses the whole batch by name, and nothing is applied
    public void A_key_repeated_in_a_batch_refuses_the_whole_batch()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var version = source.RowSequenceVersion;

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(
            added: [new Deal("E", "Rates", 0)],
            changed: [rows[0] with { Amount = 7 }, rows[0] with { Amount = 8 }])));

        Assert.Contains("'A'", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
        Assert.Equal(3m, source.Window[0].Amount);
        Assert.Equal(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-3: a key both changed and removed repeats, and is refused
    public void A_key_changed_and_removed_in_one_batch_is_refused()
    {
        var source = Live(new FakeTimeProvider(), out var rows);

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(
            changed: [rows[2] with { Amount = 7 }], removedKeys: ["C"])));

        Assert.Contains("'C'", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
    }

    [Fact] // ADR-0141 / LV-3: an added key the source already holds repeats, and is refused
    public void An_added_key_already_held_is_refused()
    {
        var source = Live(new FakeTimeProvider(), out _);

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(added: [new Deal("B", "Rates", 0)])));

        Assert.Contains("'B'", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
    }

    [Fact] // ADR-0141 / LV-3: a changed key the source does not hold refuses the whole batch by name
    public void A_changed_key_not_held_is_refused_and_nothing_is_applied()
    {
        var source = Live(new FakeTimeProvider(), out var rows);

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(
            added: [new Deal("E", "Rates", 0)],
            changed: [rows[0] with { Amount = 7 }, new Deal("Z", "Rates", 1)])));

        Assert.Contains("'Z'", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
        Assert.Equal(3m, source.Window[0].Amount);
    }

    [Fact] // ADR-0141 / LV-3: a removed key the source does not hold refuses the whole batch by name
    public void A_removed_key_not_held_is_refused_and_nothing_is_applied()
    {
        var source = Live(new FakeTimeProvider(), out _);

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(removedKeys: ["A", "Z"])));

        Assert.Contains("'Z'", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
    }

    [Fact] // ADR-0141 / LV-3, ADR-0140: a null key in a batch is refused by name
    public void A_null_key_is_refused()
    {
        var source = Live(new FakeTimeProvider(), out _);

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(added: [new Deal(null!, "Rates", 0)])));

        Assert.Contains("null Row Key", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
        Assert.Throws<ArgumentException>(() => new GridChangeBatch<Deal>(removedKeys: [null!]));
    }

    [Fact] // ADR-0141 / LV-3, ADR-0003: a changed row that is the instance already held was rewritten in place, and is refused
    public void A_changed_row_that_is_the_instance_held_is_refused()
    {
        var source = Live(new FakeTimeProvider(), out var rows);

        var refusal = Assert.Throws<ArgumentException>(() => source.Apply(new(changed: [rows[1]])));

        Assert.Contains("'B'", refusal.Message);
    }

    [Fact] // ADR-0141 / LV-3, ADR-0023: a value the Query's columns refuse refuses the whole batch, naming the column
    public void A_value_the_query_refuses_refuses_the_whole_batch()
    {
        var clock = new FakeTimeProvider();
        var source = GridSource.From<Holder>([new("A", 1m)], h => h.Id, clock);
        source.OnColumnsChanged([new ColumnInfo<Holder>("Value", ColumnType.Number, h => h.Value)]);
        source.OnSortChanged([new SortSpec("Value", SortDirection.Ascending)]);
        source.GatherInterval = TimeSpan.Zero;

        var refusal = Assert.Throws<InvalidOperationException>(() => source.Apply(new(
            added: [new Holder("B", 2m), new Holder("C", "not a number")])));

        Assert.Contains("'Value'", refusal.Message);
        Assert.Equal(["A"], source.Window.Select(h => h.Id));
    }

    private sealed record Holder(string Id, object? Value);

    [Fact] // ADR-0141 / LV-3: a refused batch leaves the source usable — the next batch applies
    public void After_a_refusal_the_next_batch_applies()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        Assert.Throws<ArgumentException>(() => source.Apply(new(removedKeys: ["Z"])));

        source.Apply(new(changed: [rows[3] with { Book = "Rates" }]));

        Assert.Equal("Rates", source.Window[3].Book);
    }

    [Fact] // ADR-0141 / ADR-0140: rows given with a repeated or null key are refused by name
    public void Rows_with_a_repeated_or_null_key_are_refused_at_construction()
    {
        var repeated = Assert.Throws<ArgumentException>(() =>
            GridSource.From<Deal>([new("A", "Rates", 1), new("B", "Rates", 2), new("A", "FX", 3)], d => d.Id));
        Assert.Contains("'A'", repeated.Message);
        Assert.Contains("0 and 2", repeated.Message);

        var nothing = Assert.Throws<ArgumentException>(() => GridSource.From<Deal>([new(null!, "Rates", 1)], d => d.Id));
        Assert.Contains("null Row Key", nothing.Message);
    }

    [Fact] // ADR-0141: without a key the source takes no live data, and says so by name
    public void Without_a_key_live_data_is_refused_by_name()
    {
        var source = GridSource.From(Deals());

        Assert.Contains("Row Key", Assert.Throws<InvalidOperationException>(() => source.Apply(new(removedKeys: ["A"]))).Message);
        Assert.Contains("Row Key", Assert.Throws<InvalidOperationException>(() => source.ReplaceAll(Deals())).Message);
        Assert.Contains("Row Key", Assert.Throws<InvalidOperationException>(() => source.GatherInterval = TimeSpan.Zero).Message);
        Assert.Null(source.CellChangedAt);
    }

    // ---- LV-10, the source's half: it names its rows and vouches --------------------------------

    [Fact] // ADR-0141 / LV-10: a keyed source names its rows by the key and vouches that no Window holds a row twice
    public void A_keyed_source_names_its_rows_and_vouches()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        IGridSource<Deal> asSource = source;

        Assert.True(asSource.VouchesDistinctRows);
        Assert.Equal("C", asSource.RowKey!(rows[2]));

        IGridSource<Deal> plain = GridSource.From(Deals());
        Assert.False(plain.VouchesDistinctRows);
        Assert.Null(plain.RowKey);
    }

    // ---- LV-4: a whole new list, paired by key -----------------------------------------------

    [Fact] // ADR-0141 / LV-4: a new key is added, a missing one removed, a new instance changed, the same instance unchanged
    public void A_whole_new_list_is_paired_by_key()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var b = rows[1] with { Amount = 9 };

        source.ReplaceAll([new Deal("F", "FX", 0), rows[3], b, rows[0], new Deal("E", "Rates", 4)]);

        // C is gone; B changed in its place; F and E added at the end in the list's order; A and D are
        // the instances they were.
        Assert.Equal("A,B,D,F,E", Ids(source));
        Assert.Same(rows[0], source.Window[0]);
        Assert.Same(b, source.Window[1]);
        Assert.Same(rows[3], source.Window[2]);
    }

    [Fact] // ADR-0141 / LV-4: the result and the Row Sequence Version equal those of the batch that says the same
    public void A_whole_new_list_equals_the_batch_that_says_the_same()
    {
        foreach (var query in new (GridFilter? Filter, SortSpec[] Sorts)[] { (null, []), (AmountAtLeast(2), ByAmount) })
        {
            var byList = Live(new FakeTimeProvider(), out var rows);
            var byBatch = Live(new FakeTimeProvider(), out _);
            foreach (var source in new[] { byList, byBatch })
            {
                source.OnFilterChanged(query.Filter);
                source.OnSortChanged(query.Sorts);
            }
            var e = new Deal("E", "Rates", 4);
            var a = rows[0] with { Amount = 0 };

            byList.ReplaceAll([e, rows[3], a, rows[1]]);
            byBatch.Apply(new(added: [e], changed: [a], removedKeys: ["C"]));

            Assert.Equal(byBatch.Window.Select(d => d.Id), byList.Window.Select(d => d.Id));
            Assert.Equal(byBatch.RowSequenceVersion, byList.RowSequenceVersion);
        }
    }

    [Fact] // ADR-0141 / LV-4: the same list again changes nothing — no event, no new Window
    public void The_same_list_again_is_a_no_op()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var window = source.Window;
        var events = 0;
        source.StateChanged += () => events++;

        source.ReplaceAll(rows);

        Assert.Equal(0, events);
        Assert.Same(window, source.Window);
    }

    [Fact] // ADR-0141 / LV-4: a key that repeats in the list refuses it by name
    public void A_list_with_a_repeated_key_is_refused()
    {
        var source = Live(new FakeTimeProvider(), out var rows);

        var refusal = Assert.Throws<ArgumentException>(() => source.ReplaceAll([rows[0], rows[1], rows[0] with { Amount = 1 }]));

        Assert.Contains("'A'", refusal.Message);
        Assert.Equal("A,B,C,D", Ids(source));
    }

    // ---- LV-6: gathering --------------------------------------------------------------------

    [Fact] // ADR-0141 / LV-6: a change after a quiet interval reaches the grid at once
    public void A_change_after_a_quiet_interval_is_applied_at_once()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        var events = 0;
        source.StateChanged += () => events++;

        source.Apply(new(changed: [rows[0] with { Amount = 7 }]));

        Assert.Equal(1, events);
        Assert.Equal(7m, source.Window[0].Amount);

        // Quiet for longer than the interval: the next change is at once too.
        clock.Advance(TimeSpan.FromSeconds(1));
        source.Apply(new(changed: [rows[1] with { Amount = 8 }]));
        Assert.Equal(2, events);
        Assert.Equal(8m, source.Window[1].Amount);
    }

    [Fact] // ADR-0141 / LV-6: the changes within an interval reach the grid as one, at its end, as the newest version
    public void Changes_within_an_interval_reach_the_grid_as_one_at_its_end()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        source.Apply(new(changed: [rows[0] with { Amount = 7 }]));
        var events = 0;
        source.StateChanged += () => events++;

        clock.Advance(TimeSpan.FromMilliseconds(100));
        source.Apply(new(changed: [rows[1] with { Amount = 8 }]));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        source.Apply(new(changed: [rows[1] with { Amount = 9 }], added: [new Deal("E", "FX", 1)]));

        Assert.Equal(0, events);
        Assert.Equal(1m, source.Window[1].Amount);
        Assert.Equal(4, source.TotalCount);

        clock.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(0, events);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, events);
        // The newest version, whole: B's latest amount and E together.
        Assert.Equal(9m, source.Window[1].Amount);
        Assert.Equal("A,B,C,D,E", Ids(source));
    }

    [Fact] // ADR-0141 / LV-6: the interval is the Consumer's to set
    public void The_interval_is_settable()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        Assert.Equal(Interval, source.GatherInterval);
        source.GatherInterval = TimeSpan.FromSeconds(1);
        source.Apply(new(changed: [rows[0] with { Amount = 7 }]));

        source.Apply(new(changed: [rows[0] with { Amount = 8 }]));
        clock.Advance(TimeSpan.FromMilliseconds(999));
        Assert.Equal(7m, source.Window[0].Amount);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(8m, source.Window[0].Amount);

        Assert.Throws<ArgumentOutOfRangeException>(() => source.GatherInterval = TimeSpan.FromMilliseconds(-1));
    }

    [Fact] // ADR-0141 / LV-6: an interval of 0 passes every change as it comes
    public void Zero_passes_every_change()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        var events = 0;
        source.StateChanged += () => events++;

        source.Apply(new(changed: [rows[0] with { Amount = 7 }]));
        source.Apply(new(changed: [rows[0] with { Amount = 8 }]));
        source.Apply(new(changed: [rows[1] with { Amount = 9 }]));

        Assert.Equal(3, events);
        Assert.Equal(8m, source.Window[0].Amount);
        Assert.Equal(9m, source.Window[1].Amount);
    }

    [Fact] // ADR-0141 / LV-6: ReplaceRow — a user's own edit — is not gathered, and brings what was gathered with it
    public void ReplaceRow_is_not_gathered()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        source.Apply(new(changed: [rows[0] with { Amount = 7 }]));
        source.Apply(new(changed: [rows[1] with { Amount = 8 }]));
        Assert.Equal(1m, source.Window[1].Amount);

        source.ReplaceRow(rows[2], rows[2] with { Book = "Edited" });

        Assert.Equal("Edited", source.Window[2].Book);
        Assert.Equal(8m, source.Window[1].Amount);
        // Nothing is left to gather: the interval's end publishes nothing more.
        var events = 0;
        source.StateChanged += () => events++;
        clock.Advance(Interval);
        Assert.Equal(0, events);
    }

    [Fact] // ADR-0141/0142: under a key, ReplaceRow of a version a live change has replaced is refused, not written over
    public void ReplaceRow_of_a_replaced_version_is_refused()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        source.Apply(new(changed: [rows[3] with { Amount = 7 }]));
        // Gathered, not yet painted: the grid still paints rows[0], and the source holds a newer one.
        source.Apply(new(changed: [rows[0] with { Amount = 8 }]));

        var refusal = Assert.Throws<ArgumentException>(() => source.ReplaceRow(rows[0], rows[0] with { Book = "Edited" }));

        Assert.Contains("'A'", refusal.Message);
        Assert.Throws<ArgumentException>(() => source.ReplaceRow(source.Window[1], source.Window[1] with { Id = "Q" }));
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

    // ---- LV-7: the Row Sequence Version ------------------------------------------------------

    [Fact] // ADR-0141 / LV-7: a batch that moves no row keeps the version
    public void A_batch_that_moves_no_row_keeps_the_version()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        source.OnSortChanged(ByAmount);
        var version = source.RowSequenceVersion;

        // B(1) C(2) A(3) D(5): new amounts that keep the order, and a row the filter... none here.
        source.Apply(new(changed: [rows[1] with { Amount = 1.5m }, rows[3] with { Book = "Rates" }]));

        Assert.Equal("B,C,A,D", Ids(source));
        Assert.Equal(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-7: a batch that moves one row anywhere moves the version
    public void A_batch_that_moves_one_row_moves_the_version()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        source.OnSortChanged(ByAmount);
        var version = source.RowSequenceVersion;

        source.Apply(new(changed: [rows[0] with { Amount = 1.1m }]));

        Assert.Equal("B,A,C,D", Ids(source));
        Assert.Equal(version + 1, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-7: rows that enter or leave the result move it; rows outside it do not
    public void Rows_in_and_out_of_the_result_move_it_and_rows_outside_do_not()
    {
        var source = Live(new FakeTimeProvider(), out var rows);
        source.OnFilterChanged(AmountAtLeast(2));
        var version = source.RowSequenceVersion;

        // B (1) is outside the result: changing, removing or adding outside moves nothing.
        source.Apply(new(changed: [rows[1] with { Amount = 0 }], added: [new Deal("E", "Rates", 1)]));
        Assert.Equal(version, source.RowSequenceVersion);
        source.Apply(new(removedKeys: ["B"]));
        Assert.Equal(version, source.RowSequenceVersion);

        source.Apply(new(added: [new Deal("F", "Rates", 9)]));
        Assert.Equal(version + 1, source.RowSequenceVersion);
        source.Apply(new(removedKeys: ["A"]));
        Assert.Equal(version + 2, source.RowSequenceVersion);
    }

    [Fact] // ADR-0141 / LV-7: a key removed and added again within one gathering, at the same position, has not moved the sequence
    public void A_key_removed_and_added_again_at_its_position_keeps_the_version()
    {
        var clock = new FakeTimeProvider();
        var source = Live(clock, out var rows, gather: true);
        source.OnSortChanged(ByAmount);
        source.Apply(new(changed: [rows[3] with { Book = "Rates" }]));
        var version = source.RowSequenceVersion;

        source.Apply(new(removedKeys: ["C"]));
        source.Apply(new(added: [rows[2] with { Book = "FX" }]));
        clock.Advance(Interval);

        Assert.Equal("B,C,A,D", Ids(source));
        Assert.Equal(version, source.RowSequenceVersion);
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
}
