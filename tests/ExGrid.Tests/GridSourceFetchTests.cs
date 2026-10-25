using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What the bundled fetching source promises (ADR-0025) beyond its shared cases
/// (<c>tests/cases/source/fetch.json</c>, run by <see cref="Cases.FetchCaseTests"/>): what JSON
/// cannot write — a Filter the Consumer changes after handing it over, the token sources
/// .NET disposes, a range asked for from inside a handler, and a page refused where it is built. Timing is driven by hand — no
/// delays, no clock.
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

    [Fact] // ADR-0025: a range asked for while the grid hears a fetch start supersedes it, and is the one in flight
    public void A_range_asked_while_a_fetch_is_heard_starting_is_the_one_in_flight()
    {
        // A render that StateHasChanged completes at once can ask for a range from inside StateChanged.
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var server = new Server();
            var source = server.Source();
            server.Last.Completion.SetResult(Page(0, 100, 5_000));
            Task? inner = null;
            void Ask()
            {
                if (inner is null && source.IsLoading)
                    inner = source.OnRangeNeededAsync(new RowRange(900, 20));
            }
            source.StateChanged += Ask;
            _ = source.OnRangeNeededAsync(new RowRange(500, 20));
            source.StateChanged -= Ask;

            Assert.Equal([0, 500, 900], server.Calls.Select(call => call.Query.Range.Start));
            Assert.Same(inner, source.OnRangeNeededAsync(new RowRange(900, 20)));
            server.Calls[1].Completion.SetResult(Page(500, 20, 5_000));
            Assert.True(source.IsLoading);
            Assert.Equal(0, source.WindowStart);
            server.Calls[2].Completion.SetResult(Page(900, 20, 5_000));
            Assert.False(source.IsLoading);
            Assert.Equal(900, source.WindowStart);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
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
