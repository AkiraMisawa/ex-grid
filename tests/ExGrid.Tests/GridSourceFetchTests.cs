using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What the bundled fetching source promises (ADR-0025): it breaks its own cold start,
/// coalesces while scrolling, and never lets an answer to an old question reach the
/// screen. Timing is driven by hand — no delays, no clock.
/// </summary>
public class GridSourceFetchTests
{
    /// <summary>One fetch waiting to be answered.</summary>
    private sealed class Pending
    {
        public required GridQuery Query { get; init; }
        public required TaskCompletionSource<GridPage<Trade>> Completion { get; init; }
        public required CancellationToken Cancellation { get; init; }
    }

    private sealed class Server
    {
        public List<Pending> Calls { get; } = [];

        public FetchingGridSource<Trade> Source(int readAheadRows = 0)
        {
            var source = GridSource.Fetch<Trade>((query, cancellation) =>
            {
                var pending = new Pending
                {
                    Query = query,
                    Completion = new TaskCompletionSource<GridPage<Trade>>(),
                    Cancellation = cancellation,
                };
                Calls.Add(pending);
                return new ValueTask<GridPage<Trade>>(pending.Completion.Task);
            }, readAheadRows);
            source.OnColumnsChanged(TradeColumns.All);
            return source;
        }

        public Pending Last => Calls[^1];
    }

    private static GridPage<Trade> Page(int start, int count, int total)
    {
        var rows = new Trade[count];
        for (var i = 0; i < count; i++)
            rows[i] = new Trade(Book: $"Row {start + i}");
        return new GridPage<Trade>(rows, start, total);
    }

    [Fact] // ADR-0025: the grid cannot ask for rows in an empty result, so the source starts itself
    public async Task The_first_page_is_fetched_at_bind_time()
    {
        var server = new Server();
        var source = server.Source();

        var call = Assert.Single(server.Calls);
        Assert.Equal(0, call.Query.Range.Start);
        Assert.True(source.IsLoading);

        call.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        Assert.False(source.IsLoading);
        Assert.Equal(100, source.Window.Count);
        Assert.Equal(0, source.WindowStart);
        Assert.Equal(5_000, source.TotalCount);
    }

    [Fact] // ADR-0023: the grid is told when something it paints moved, and only then
    public void A_change_is_raised_when_loading_flips_and_when_the_answer_lands()
    {
        var server = new Server();
        var changes = 0;
        var source = server.Source();
        source.StateChanged += () => changes++;

        // Already loading: asking for another range supersedes the fetch but moves
        // nothing on screen, so it is not a change.
        _ = source.OnRangeNeededAsync(new RowRange(500, 20));
        Assert.Equal(0, changes);

        server.Last.Completion.SetResult(Page(475, 70, 5_000));
        Assert.Equal(1, changes);

        // Loading flips back on, which the grid does paint (ADR-0010's loading seam).
        _ = source.OnRangeNeededAsync(new RowRange(2_000, 20));
        Assert.Equal(2, changes);
    }

    [Fact] // ADR-0025: a range the Window already holds is not fetched again
    public async Task A_covered_range_asks_for_nothing()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        await source.OnRangeNeededAsync(new RowRange(10, 20));

        Assert.Single(server.Calls);
    }

    [Fact] // ADR-0001: read-ahead belongs to the source, and it is the number the Consumer gave
    public async Task Read_ahead_widens_what_is_asked_for()
    {
        var server = new Server();
        var source = server.Source(readAheadRows: 25);
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        _ = source.OnRangeNeededAsync(new RowRange(500, 20));

        var asked = server.Last.Query.Range;
        Assert.Equal(475, asked.Start);
        Assert.Equal(70, asked.Count); // 25 + 20 + 25
    }

    [Fact] // ADR-0025: the same range while it is in flight is one fetch, not two
    public async Task The_same_range_in_flight_is_coalesced()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        var first = source.OnRangeNeededAsync(new RowRange(500, 20));
        var second = source.OnRangeNeededAsync(new RowRange(500, 20));

        Assert.Equal(2, server.Calls.Count); // the bind-time page, and this one
        Assert.Same(first, second);
        server.Last.Completion.SetResult(Page(500, 20, 5_000));
        await first;
    }

    [Fact] // ADR-0025: a superseded question is cancelled, and its answer never reaches the screen
    public async Task An_answer_to_a_superseded_question_is_discarded()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        var stale = source.OnRangeNeededAsync(new RowRange(500, 20));
        var staleCall = server.Last;
        var fresh = source.OnRangeNeededAsync(new RowRange(900, 20));
        var freshCall = server.Last;

        Assert.True(staleCall.Cancellation.IsCancellationRequested);
        Assert.False(freshCall.Cancellation.IsCancellationRequested);

        // The newer answer lands first, then the older one arrives anyway.
        freshCall.Completion.SetResult(Page(900, 20, 5_000));
        await fresh;
        staleCall.Completion.SetResult(Page(500, 20, 5_000));
        await stale;

        Assert.Equal(900, source.WindowStart);
        Assert.Equal("Row 900", source.Window[0].Book);
    }

    [Fact] // ADR-0025: a source may answer short, but not with a different part of the result
    public async Task An_answer_that_misses_the_asked_position_is_refused()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        var wanted = source.OnRangeNeededAsync(new RowRange(500, 20));
        server.Last.Completion.SetResult(Page(0, 20, 5_000)); // the wrong part of the result

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => wanted);
        Assert.Contains("500", error.Message);
        // Nothing moved: the Window is still what it was.
        Assert.Equal(0, source.WindowStart);
        Assert.Equal(100, source.Window.Count);
    }

    [Fact] // ADR-0025: answering with none because the result shrank past that position is legitimate
    public async Task An_answer_past_the_end_of_a_shrunken_result_is_accepted()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        var wanted = source.OnRangeNeededAsync(new RowRange(500, 20));
        server.Last.Completion.SetResult(new GridPage<Trade>([], 0, 10));
        await wanted;

        Assert.Equal(10, source.TotalCount);
        Assert.Empty(source.Window);
    }

    [Fact] // ADR-0011 / ADR-0025: a Sort change reorders everything, so it starts again from the top
    public async Task A_sort_change_drops_the_window_and_refetches_from_the_top()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();
        var scrolled = source.OnRangeNeededAsync(new RowRange(500, 20));
        server.Last.Completion.SetResult(Page(500, 20, 5_000));
        await scrolled;
        var version = source.RowSequenceVersion;

        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);

        Assert.NotEqual(version, source.RowSequenceVersion);
        Assert.Empty(source.Window);
        Assert.Equal(0, source.WindowStart);
        Assert.True(source.IsLoading);
        Assert.Equal(0, server.Last.Query.Range.Start);
        Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], server.Last.Query.Sorts);
    }

    [Fact] // ADR-0025: a restart IS the cold start — binding afterwards must not fetch again
    public void A_query_set_before_binding_is_fetched_once()
    {
        var server = new Server();
        // The shape of a Consumer restoring a saved View State in OnInitialized: the sort
        // is set before the grid has bound and pushed its columns.
        var source = GridSource.Fetch<Trade>((query, cancellation) =>
        {
            var pending = new Pending
            {
                Query = query,
                Completion = new TaskCompletionSource<GridPage<Trade>>(),
                Cancellation = cancellation,
            };
            server.Calls.Add(pending);
            return new ValueTask<GridPage<Trade>>(pending.Completion.Task);
        }, readAheadRows: 0);

        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);
        source.OnColumnsChanged(TradeColumns.All);

        Assert.Single(server.Calls);
        Assert.False(server.Last.Cancellation.IsCancellationRequested);
    }

    [Fact] // ADR-0011: scrolling to another slice of the same query is not a reorder
    public async Task Scrolling_to_another_slice_does_not_bump_the_sequence_version()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();
        var version = source.RowSequenceVersion;

        var scrolled = source.OnRangeNeededAsync(new RowRange(500, 20));
        server.Last.Completion.SetResult(Page(500, 20, 5_000));
        await scrolled;

        Assert.Equal(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0011 / ADR-0025: a query change is visible even when one was already in flight
    public void A_sort_change_during_a_fetch_still_reports_itself()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        var changes = 0;
        source.StateChanged += () => changes++;
        // A fetch is in flight when the sort changes — the ordinary case while scrolling.
        _ = source.OnRangeNeededAsync(new RowRange(500, 20));
        var beforeSort = changes;

        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);

        // The Window emptied and the order's version moved. Neither depends on whether a
        // fetch happened to be running, and a grid told nothing goes on painting the old
        // rows under the new sort, with a selection ADR-0011 says must be dropped.
        Assert.Equal(beforeSort + 1, changes);
        Assert.Empty(source.Window);
    }

    [Fact] // ADR-0025: the answer has to cover the range the GRID needed, not the widened one
    public async Task An_answer_that_covers_only_the_read_ahead_is_refused()
    {
        var server = new Server();
        var source = server.Source(readAheadRows: 60);
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        // The grid needs (100, 20); read-ahead widens it to (40, 140). A server that caps
        // its pages at 50 answers rows 40-89 — which covers the widened start and none of
        // what the grid asked about. Accepted, the Viewport stays on Placeholders with no
        // load in flight and no error, and the grid's own dedupe stops it asking again.
        var wanted = source.OnRangeNeededAsync(new RowRange(100, 20));
        server.Last.Completion.SetResult(Page(40, 50, 5_000));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => wanted);
        Assert.Contains("100", error.Message);
    }

    [Fact] // ADR-0011: a result that shrank means the positions mean something else
    public async Task A_smaller_total_drops_the_selection()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();
        var version = source.RowSequenceVersion;

        var wanted = source.OnRangeNeededAsync(new RowRange(200, 20));
        server.Last.Completion.SetResult(Page(200, 20, 4_000));
        await wanted;

        Assert.NotEqual(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0023: the Filter is compared structurally and kept as a copy
    public void The_filter_is_snapshotted_and_compared_by_content()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        var clauses = new List<FilterClause> { new(FilterOperator.IsBlank) };
        var filter = new GridFilter(new Dictionary<string, FilterSpec> { ["Book"] = new(clauses) });
        source.OnFilterChanged(filter);
        var fetches = server.Calls.Count;

        // Mutating the Consumer's own object does not rewrite the query that ran: what
        // was applied is what the source kept, and it kept a copy.
        clauses.Add(new FilterClause(FilterOperator.IsNotBlank));
        Assert.Single(source.Filter!.Columns["Book"].Clauses);

        // A structurally equal but different instance is not a change — the record's own
        // equality would call it one, because it compares the collections by reference.
        source.OnFilterChanged(new GridFilter(new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.IsBlank)]),
        }));
        Assert.Equal(fetches, server.Calls.Count);

        // And the mutated one now differs in content, so it IS a change.
        source.OnFilterChanged(filter);
        Assert.Equal(fetches + 1, server.Calls.Count);
    }

    [Fact] // ADR-0025: a disposed source stops working — nothing in flight reaches anyone
    public void Disposing_the_source_cancels_what_is_in_flight()
    {
        var server = new Server();
        var source = server.Source();
        var changes = 0;
        source.StateChanged += () => changes++;

        source.Dispose();

        Assert.True(server.Last.Cancellation.IsCancellationRequested);
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        Assert.Equal(0, changes);
    }

    [Fact] // ASY-2 / MEM-3: each fetch's token source is disposed once that fetch is over, however it ended
    public async Task Every_fetchs_token_source_is_disposed_when_its_fetch_ends()
    {
        var server = new Server();
        var source = server.Source();
        source.FetchFailed += _ => { };
        server.Last.Completion.SetResult(Page(0, 100, 5_000));          // answered
        await Task.Yield();

        var stale = source.OnRangeNeededAsync(new RowRange(500, 20));
        var fresh = source.OnRangeNeededAsync(new RowRange(900, 20));
        server.Calls[^2].Completion.SetResult(Page(500, 20, 5_000));    // superseded, answering late
        server.Last.Completion.SetException(new TimeoutException());   // failed
        await stale;
        await fresh;

        var cutOff = source.OnRangeNeededAsync(new RowRange(2_000, 20));
        source.Dispose();
        server.Last.Completion.SetResult(Page(2_000, 20, 5_000));       // cut off by disposal
        await cutOff;

        // A token's wait handle is the one thing that says its source was disposed.
        Assert.Equal(4, server.Calls.Count);
        Assert.All(server.Calls, call =>
            Assert.Throws<ObjectDisposedException>(() => call.Cancellation.WaitHandle));
    }

    [Fact] // ADR-0025: a failure is reported, and the rows already on screen are left alone
    public async Task A_failure_is_reported_and_leaves_the_window_standing()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();
        Exception? reported = null;
        source.FetchFailed += ex => reported = ex;

        var wanted = source.OnRangeNeededAsync(new RowRange(500, 20));
        server.Last.Completion.SetException(new TimeoutException("the server did not answer"));
        await wanted;

        Assert.IsType<TimeoutException>(reported);
        Assert.Same(reported, source.LastError);
        Assert.False(source.IsLoading);
        Assert.Equal(100, source.Window.Count);
    }

    [Fact] // ADR-0025: with nobody listening the failure is rethrown, never swallowed
    public async Task An_unsubscribed_failure_reaches_the_caller()
    {
        var server = new Server();
        var source = server.Source();
        server.Last.Completion.SetResult(Page(0, 100, 5_000));
        await Task.Yield();

        var wanted = source.OnRangeNeededAsync(new RowRange(500, 20));
        server.Last.Completion.SetException(new TimeoutException("the server did not answer"));

        await Assert.ThrowsAsync<TimeoutException>(() => wanted);
    }

    [Fact] // ADR-0001: a page that contradicts itself is refused where it is built
    public void A_page_claiming_rows_past_its_own_total_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridPage<Trade>([new Trade(Book: "A"), new Trade(Book: "B")], start: 9, totalCount: 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GridPage<Trade>([], start: -1, totalCount: 10));
    }
}
