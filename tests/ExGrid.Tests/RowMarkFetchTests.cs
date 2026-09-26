using ExGrid.Rows;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Row Marks on the fetching Source (ADR-0043): identity comes from the Consumer's key,
/// "all" is a snapshot the server marks with its own token, and the counts come from the
/// server — held back, not guessed, until it answers.
/// </summary>
public class RowMarkFetchTests
{
    private sealed record Deal(int Id, int Stamp, decimal Amount);

    private static readonly IReadOnlyList<ColumnInfo<Deal>> Columns =
    [
        new("Id", ColumnType.Number, d => d.Id),
        new("Amount", ColumnType.Number, d => d.Amount),
    ];

    /// <summary>
    /// A server over a list that grows: every answer is a new instance of each row, as a
    /// real fetch returns. A snapshot's token is the highest stamp at the moment; a row
    /// belongs to it when it was stamped by then and matches its filter.
    /// </summary>
    private sealed class Server
    {
        private int _stamp;
        public List<Deal> Rows { get; } = [];
        public TaskCompletionSource<RowMarkCounts>? HeldCount { get; set; }
        public int Counted { get; private set; }

        public Server(int rows)
        {
            for (var i = 0; i < rows; i++)
                Add(i);
        }

        public void Add(decimal amount) => Rows.Add(new Deal(Rows.Count, ++_stamp, amount));

        private IReadOnlyList<Deal> Result(GridFilter? filter)
            => filter is null ? Rows : GridQueryEngine.Apply(Rows, Columns, filter, []);

        public FetchingGridSource<Deal> Source(bool withAdapter = true)
        {
            var adapter = new RowMarkAdapter<Deal>(
                key: d => d.Id,
                openSnapshot: (_, _) => ValueTask.FromResult<object>(_stamp),
                belongsTo: BelongsTo,
                count: CountAsync);
            var source = GridSource.Fetch<Deal>(
                (query, _) =>
                {
                    var result = Result(query.Filter);
                    var start = Math.Min(query.Range.Start, result.Count);
                    var count = Math.Min(query.Range.Count, result.Count - start);
                    var rows = result.Skip(start).Take(count).Select(d => d with { }).ToArray();
                    return ValueTask.FromResult(new GridPage<Deal>(rows, start, result.Count));
                },
                readAheadRows: 0,
                marks: withAdapter ? adapter : null);
            source.OnColumnsChanged(Columns);
            return source;
        }

        private static bool BelongsTo(Deal row, RowMarkSnapshot snapshot)
            => row.Stamp <= (int)snapshot.AsOf
               && (snapshot.Filter is null || GridQueryEngine.Matches(row, Columns, snapshot.Filter));

        private ValueTask<RowMarkCounts> CountAsync(RowMarkState state, GridFilter? filter, CancellationToken _)
        {
            Counted++;
            if (HeldCount is { } held)
                return new ValueTask<RowMarkCounts>(held.Task);
            var result = Result(filter);
            var inResult = result.Count(d => state.IsMarked(d.Id, s => BelongsTo(d, s)));
            var total = Rows.Count(d => state.IsMarked(d.Id, s => BelongsTo(d, s)));
            return ValueTask.FromResult(new RowMarkCounts(inResult, result.Count, total - inResult));
        }
    }

    private static GridFilter AmountAtLeast(decimal amount)
        => new(new Dictionary<string, FilterSpec>
        {
            ["Amount"] = new([new FilterClause(FilterOperator.GreaterThanOrEqual, amount)]),
        });

    [Fact] // ADR-0043: without an adapter the fetching Source keeps no marks — the grid then refuses a Mark Column
    public void Without_an_adapter_there_are_no_marks()
    {
        IGridSource<Deal> source = new Server(5).Source(withAdapter: false);

        Assert.Null(source.Marks);
    }

    [Fact] // ADR-0043: a mark follows the row's key to the next answer's new instance
    public async Task A_mark_follows_the_key_to_a_new_instance()
    {
        var server = new Server(5);
        var source = server.Source();
        var marks = source.Marks!;

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(source.Window[2], true));
        source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]);

        var again = source.Window.Single(d => d.Id == 2);
        Assert.True(marks.IsMarked(again));
        Assert.False(marks.IsMarked(source.Window.Single(d => d.Id == 3)));
    }

    [Fact] // ADR-0043 (MK-6): after "all", a row fetched for the first time answers marked
    public async Task After_all_a_row_fetched_later_answers_marked()
    {
        var server = new Server(1_000);
        var source = server.Source();
        var marks = source.Marks!;

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));
        await source.OnRangeNeededAsync(new RowRange(900, 20));

        Assert.All(source.Window, row => Assert.True(marks.IsMarked(row)));
        Assert.Equal(new RowMarkCounts(1_000, 1_000, 0), marks.Counts);
    }

    [Fact] // ADR-0043 (MK-8): a row the server adds after "all" is not marked, and the counts turn to "some"
    public async Task A_row_added_after_all_is_not_marked()
    {
        var server = new Server(10);
        var source = server.Source();
        var marks = source.Marks!;
        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));

        server.Add(99);
        await marks.RecountAsync();
        await source.OnRangeNeededAsync(new RowRange(10, 1));

        Assert.False(marks.IsMarked(source.Window.Single(d => d.Id == 10)));
        Assert.Equal(RowMarkHeaderState.Some, RowMarkRules.HeaderState(marks.Counts!.Value));
    }

    [Fact] // ADR-0043 (MK-7): narrowing the filter recounts, naming the marks outside it
    public async Task A_filter_change_recounts_the_marks_outside_it()
    {
        var server = new Server(10);
        var source = server.Source();
        var marks = source.Marks!;
        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));

        source.OnFilterChanged(AmountAtLeast(6));
        await marks.RecountAsync();

        Assert.Equal(new RowMarkCounts(4, 4, 6), marks.Counts);
    }

    [Fact] // ADR-0043: until the server has counted, the counts are unknown rather than guessed
    public async Task The_counts_are_unknown_until_the_server_answers()
    {
        var server = new Server(10);
        var source = server.Source();
        var marks = source.Marks!;
        server.HeldCount = new TaskCompletionSource<RowMarkCounts>();

        var marking = marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(source.Window[0], true));

        Assert.Null(marks.Counts);
        server.HeldCount.SetResult(new RowMarkCounts(1, 10, 0));
        await marking;
        Assert.Equal(new RowMarkCounts(1, 10, 0), marks.Counts);
    }

    [Fact] // ADR-0043 (MK-3): positions under another order are refused
    public async Task Positions_under_a_stale_version_mark_nothing()
    {
        var server = new Server(10);
        var source = server.Source();
        var marks = source.Marks!;
        var version = source.RowSequenceVersion;
        source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]);

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(0, 3)], version));

        Assert.Equal(RowMarkState.Empty, marks.State);
    }

    [Fact] // ADR-0043 (MK-1): positions beyond the Window are fetched and lined up
    public async Task Positions_beyond_the_window_are_fetched_and_lined_up()
    {
        var server = new Server(1_000);
        var source = server.Source();
        var marks = source.Marks!;
        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(source.Window[5], true));

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(0, 10), new RowRange(500, 5)], source.RowSequenceVersion));

        Assert.Equal(new RowMarkCounts(15, 1_000, 0), marks.Counts);
        await source.OnRangeNeededAsync(new RowRange(500, 5));
        Assert.All(source.Window, row => Assert.True(marks.IsMarked(row)));
    }

    [Fact] // ADR-0043: the marks an action runs over are keys and snapshots, never positions
    public async Task The_state_is_keys_and_snapshots()
    {
        var server = new Server(10);
        var source = server.Source();
        var marks = source.Marks!;

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.AllRows(true, source.RowSequenceVersion));
        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(source.Window[3], false));

        Assert.Collection(marks.State.Steps,
            step => Assert.Equal(new RowMarkStep.AllOf(new RowMarkSnapshot(null, 10), true), step),
            step => Assert.Equal(new RowMarkStep.OneKey(3, false), step));
    }

    [Fact] // ADR-0043: a key marked many times over is one step, and marking many keys stays linear
    public async Task Marking_many_rows_keeps_one_step_per_key()
    {
        var server = new Server(50_000);
        var source = server.Source();
        var marks = source.Marks!;

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(0, 49_000)], source.RowSequenceVersion));
        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.Positions([new RowRange(0, 49_000)], source.RowSequenceVersion));

        // One step per key, whatever it went through — never a step per gesture.
        Assert.Equal(49_000, marks.State.Steps.Count);
        Assert.All(marks.State.Steps, step => Assert.False(step.Marked));
    }

    [Fact] // ADR-0043/0025: a failed count reaches FetchFailed, never the gesture that caused it
    public async Task A_failed_count_after_a_gesture_is_reported_to_fetch_failed()
    {
        var server = new Server(10);
        var source = server.Source();
        var marks = source.Marks!;
        var failures = new List<Exception>();
        source.FetchFailed += failures.Add;
        server.HeldCount = new TaskCompletionSource<RowMarkCounts>();
        server.HeldCount.SetException(new InvalidOperationException("the count failed"));

        await marks.OnMarkIntentAsync(new RowMarkIntent<Deal>.OneRow(source.Window[0], true));

        Assert.Equal("the count failed", Assert.Single(failures).Message);
        Assert.Same(failures[0], source.LastError);
        Assert.True(marks.IsMarked(source.Window[0]));
        Assert.Null(marks.Counts);
    }
}
