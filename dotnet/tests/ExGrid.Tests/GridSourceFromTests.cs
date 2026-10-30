using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// What C# alone can say about <c>GridSource.From</c>: that the lists and dictionaries a Consumer hands it
/// are copied. Its semantics are shared cases (<c>tests/cases/source/from.json</c>), which the Script Core
/// runs too.
/// </summary>
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

    [Fact] // ADR-0011: the source snapshots the query lists — a mutated caller list must not desync or replay
    public void Mutating_the_callers_sorts_list_does_not_leak_into_the_source()
    {
        var source = Bound();
        var sorts = new List<SortSpec> { new("Amount", SortDirection.Ascending) };
        source.OnSortChanged(sorts);
        var version = source.RowSequenceVersion;

        sorts[0] = new("Amount", SortDirection.Descending);

        Assert.Equal(SortDirection.Ascending, source.Sorts[0].Direction);
        source.OnFilterChanged(null); // an unrelated change must not replay the mutation
        Assert.Equal([1, 2, 3], source.Window.Select(t => t.Amount));
        Assert.Equal(version, source.RowSequenceVersion);
    }

    [Fact] // ADR-0011: the filter is snapshotted too — a mutated filter must not be replayed
    public void Mutating_the_callers_filter_does_not_leak_into_the_source()
    {
        var source = Bound();
        var columns = new Dictionary<string, FilterSpec>
        {
            ["Book"] = new([new FilterClause(FilterOperator.Equals, "Rates")]),
        };
        source.OnFilterChanged(new GridFilter(columns));
        Assert.Equal(2, source.TotalCount);

        columns["Book"] = new FilterSpec([new FilterClause(FilterOperator.Equals, "Credit")]);
        source.OnSortChanged([]); // an unrelated change must not replay the mutation

        Assert.Equal(2, source.TotalCount); // still the Rates rows, not the mutated filter's
        Assert.Equal(["Rates", "Rates"], source.Window.Select(t => t.Book));
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
}
