using Xunit;

namespace ExGrid.Tests;

/// <summary>What one header click does to the Sorts list (ADR-0012).</summary>
public class SortCycleTests
{
    [Fact] // ADR-0012: unsorted → ascending → descending → unsorted
    public void A_click_cycles_one_column_through_three_states()
    {
        IReadOnlyList<SortSpec> sorts = [];

        sorts = SortCycle.Next(sorts, "Amount");
        Assert.Equal([new SortSpec("Amount", SortDirection.Ascending)], sorts);

        sorts = SortCycle.Next(sorts, "Amount");
        Assert.Equal([new SortSpec("Amount", SortDirection.Descending)], sorts);

        sorts = SortCycle.Next(sorts, "Amount");
        Assert.Empty(sorts);
    }

    [Fact] // ADR-0012: clicking another column starts over at ascending on it
    public void A_click_on_another_column_replaces_the_sort()
    {
        IReadOnlyList<SortSpec> sorts = [new SortSpec("Amount", SortDirection.Descending)];

        sorts = SortCycle.Next(sorts, "Book");

        Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], sorts);
    }

    [Fact] // ADR-0012: a multi-column list a Consumer set up is replaced, not amended
    public void A_click_on_a_multi_column_sort_starts_over()
    {
        IReadOnlyList<SortSpec> sorts =
        [
            new SortSpec("Book", SortDirection.Ascending),
            new SortSpec("Amount", SortDirection.Descending),
        ];

        sorts = SortCycle.Next(sorts, "Book");

        Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], sorts);
    }

    [Fact] // Column names are exact: the cycle only advances on the very same column
    public void The_column_name_is_compared_exactly()
    {
        IReadOnlyList<SortSpec> sorts = [new SortSpec("Amount", SortDirection.Ascending)];

        Assert.Equal(
            [new SortSpec("amount", SortDirection.Ascending)],
            SortCycle.Next(sorts, "amount"));
    }
}
