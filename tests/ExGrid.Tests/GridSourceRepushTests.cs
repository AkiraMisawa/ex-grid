using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Pins the Commit no-op guard: a binding layer that repushes on every render and
/// re-renders on StateChanged must not spin, and must not re-run the query either
/// (ADR-0023's no-op principle applied to the commit).
/// </summary>
public class GridSourceRepushTests
{
    private static readonly Trade[] Rows =
    [
        new(Book: "B", Amount: 2m),
        new(Book: "A", Amount: 1m),
    ];

    [Fact] // ADR-0023: repushing the same columns is a no-op — no recompute, no event
    public void Repushing_the_same_columns_does_not_recompute_the_window()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(TradeColumns.All);
        var window = source.Window;
        var events = 0;
        source.StateChanged += () => events++;

        source.OnColumnsChanged(TradeColumns.All);

        Assert.Same(window, source.Window);
        Assert.Equal(0, events);
    }

    [Fact] // ADR-0023: element-wise equal columns in a fresh list are still the same columns
    public void Repushing_equal_columns_in_a_new_list_does_not_recompute_the_window()
    {
        var source = GridSource.From(Rows);
        source.OnColumnsChanged(TradeColumns.All);
        source.OnSortChanged([new SortSpec("Book", SortDirection.Ascending)]);
        var window = source.Window;
        var version = source.RowSequenceVersion;

        source.OnColumnsChanged(TradeColumns.All.ToArray());

        Assert.Same(window, source.Window);
        Assert.Equal(version, source.RowSequenceVersion);
    }
}
