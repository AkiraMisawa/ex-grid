using Xunit;

namespace ExGrid.Tests;

public class GridSourceFromTests
{
    private static readonly IReadOnlyList<Trade> Rows =
    [
        new(Book: "Rates", Amount: 2),
        new(Book: "Credit", Amount: 3),
        new(Book: "Rates", Amount: 1),
    ];

    private static InMemoryGridSource<Trade> Bound()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(TradeColumns.All);
        return source;
    }

    [Fact] // ADR-0001: everything is in hand — the Window is the whole result and nothing loads
    public void Window_holds_everything_and_is_loading_is_false()
    {
        var source = Bound();
        Assert.Equal(Rows, source.Window);
        Assert.False(source.IsLoading);
        source.OnRangeNeeded(new RowRange(0, 2)); // already pushed; nothing to answer
        Assert.Equal(Rows, source.Window);
    }

    [Fact] // ADR-0023: before columns arrive the Window is the input order unchanged
    public void Window_is_the_input_order_before_columns_arrive()
    {
        var source = GridSource.From(Rows);
        Assert.Equal(Rows, source.Window);
        Assert.Equal(0, source.RowSequenceVersion);
    }

    [Fact] // ADR-0023: a query change before columns is a Consumer bug — refuse it
    public void A_sort_or_filter_change_before_columns_throws()
    {
        var source = GridSource.From(Rows);
        Assert.Throws<InvalidOperationException>(
            () => source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]));
        Assert.Throws<InvalidOperationException>(
            () => source.OnFilterChanged(TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.IsNotBlank))));
    }

    [Fact] // ADR-0001: From performs the sorting itself and repushes the Window
    public void A_sort_change_reorders_the_window()
    {
        var source = Bound();
        source.OnSortChanged([
            new SortSpec("Book", SortDirection.Ascending),
            new SortSpec("Amount", SortDirection.Ascending),
        ]);

        Assert.Equal([3, 1, 2], source.Window.Select(t => t.Amount));
    }

    [Fact] // ADR-0015: TotalCount is the post-filter count — it feeds the pager
    public void Total_count_is_the_post_filter_count()
    {
        var source = Bound();
        Assert.Equal(3, source.TotalCount);

        source.OnFilterChanged(TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.Equals, "Rates")));
        Assert.Equal(2, source.TotalCount);
    }

    [Fact] // ADR-0011: the Row Sequence Version identifies the order — it bumps when the order changes
    public void Row_sequence_version_bumps_when_sort_changes_the_order()
    {
        var source = Bound();
        var before = source.RowSequenceVersion;

        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);
        Assert.Equal(before + 1, source.RowSequenceVersion);
    }

    [Fact] // ADR-0011
    public void Row_sequence_version_bumps_when_filter_changes_the_rows()
    {
        var source = Bound();
        var before = source.RowSequenceVersion;

        source.OnFilterChanged(TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.Equals, "Credit")));
        Assert.Equal(before + 1, source.RowSequenceVersion);
    }

    [Fact] // ADR-0011: a change that leaves the sequence as it was must not clear the selection
    public void Row_sequence_version_does_not_bump_when_the_query_is_a_no_op()
    {
        var source = Bound();
        Assert.Equal(0, source.RowSequenceVersion); // receiving columns changed nothing

        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);
        var after = source.RowSequenceVersion;

        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]); // same query again
        Assert.Equal(after, source.RowSequenceVersion);

        // A filter that excludes nothing leaves the sequence intact too.
        source.OnFilterChanged(TradeColumns.FilterOn("Book", new FilterClause(FilterOperator.IsNotBlank)));
        Assert.Equal(after, source.RowSequenceVersion);
    }

    [Fact] // ADR-0023: a refused change leaves the source exactly as it was — and usable
    public void A_refused_change_leaves_the_source_usable()
    {
        var source = Bound();
        Assert.Throws<InvalidOperationException>(
            () => source.OnSortChanged([new SortSpec("Notional", SortDirection.Ascending)]));

        Assert.Equal(Rows, source.Window);
        Assert.Empty(source.Sorts);
        Assert.Equal(0, source.RowSequenceVersion);

        source.OnFilterChanged(null); // must not rethrow the refused sort
        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);
        Assert.Equal([1, 2, 3], source.Window.Select(t => t.Amount));
    }

    [Fact] // ADR-0023: re-binding columns that break the current query is refused, keeping the old binding
    public void Rebinding_columns_that_break_the_current_query_throws_and_keeps_state()
    {
        var source = Bound();
        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);
        var window = source.Window;

        Assert.Throws<InvalidOperationException>(
            () => source.OnColumnsChanged([TradeColumns.Amount])); // "Book" no longer exists

        Assert.Equal(window, source.Window);
        source.OnFilterChanged(null); // the old columns still serve the query
        Assert.Equal(window, source.Window);
    }

    [Fact] // ADR-0023: the base is a snapshot — mutating the Consumer's list must not leak into the Window
    public void The_window_is_a_snapshot_of_the_rows_at_construction()
    {
        var rows = new List<Trade> { new(Book: "Rates", Amount: 1) };
        var source = GridSource.From(rows);

        rows.Add(new Trade(Book: "Credit", Amount: 2));

        Assert.Equal(1, source.TotalCount);
        Assert.Single(source.Window);
    }

    [Fact] // ADR-0018: several instances must not interfere with each other
    public void Two_sources_over_the_same_rows_are_independent()
    {
        var first = Bound();
        var second = Bound();

        first.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]);

        Assert.Equal(Rows, second.Window);
        Assert.Equal(0, second.RowSequenceVersion);
        Assert.Empty(second.Sorts);
    }

    [Fact] // ADR-0001: the binding layer repushes on StateChanged
    public void State_changed_is_raised_when_a_change_is_applied()
    {
        var source = Bound();
        var raised = 0;
        source.StateChanged += () => raised++;

        source.OnSortChanged([new SortSpec("Amount", SortDirection.Ascending)]);
        Assert.Equal(1, raised);
    }
}
