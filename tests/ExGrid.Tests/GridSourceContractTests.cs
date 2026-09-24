using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The in-memory source seen through <see cref="IGridSource{TRow}"/> — what the grid
/// will read once it is bound (ADR-0001).
/// </summary>
public class GridSourceContractTests
{
    private static readonly Trade[] Rows =
    [
        new(Book: "Rates", Amount: 2m),
        new(Book: "Credit", Amount: 1m),
    ];

    private static IGridSource<Trade> Bound()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(TradeColumns.All);
        return source;
    }

    [Fact] // ADR-0001: everything is in hand, so the Window is the whole result at position 0
    public void The_window_is_the_whole_result_and_starts_at_zero()
    {
        var source = Bound();

        Assert.Equal(2, source.Window.Count);
        Assert.Equal(0, source.WindowStart);
        Assert.Equal(2, source.TotalCount);
        Assert.False(source.IsLoading);
    }

    [Fact] // ADR-0001: nothing is ever in flight, so nothing is ever stale
    public async Task A_range_request_needs_no_answer()
    {
        var source = Bound();
        var window = source.Window;
        var events = 0;
        source.StateChanged += () => events++;

        // Well past the data: requests race with data updates, and answering nothing is
        // the answer. Awaiting it must not block and must not change anything.
        await source.OnRangeNeededAsync(new RowRange(1_000_000, 20));

        Assert.Same(window, source.Window);
        Assert.Equal(0, events);
    }

    [Fact] // ADR-0011: the version names the order, and the grid drops the selection on it
    public void The_sequence_version_moves_only_when_the_order_does()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(TradeColumns.All);
        var bound = (IGridSource<Trade>)source;
        var version = bound.RowSequenceVersion;

        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);
        var afterSort = bound.RowSequenceVersion;
        source.OnColumnsChanged(TradeColumns.All);

        Assert.NotEqual(version, afterSort);
        Assert.Equal(afterSort, bound.RowSequenceVersion);
    }
}
