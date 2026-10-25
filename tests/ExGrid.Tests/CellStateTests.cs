using ExGrid.Cells;
using ExGrid.Components;
using Xunit;

namespace ExGrid.Tests;

public class CellStateTests
{
    private static readonly CellState[] AllStates =
        [CellState.Normal, CellState.Stale, CellState.Missing, CellState.Error, CellState.Modified];

    [Fact] // ADR-0006: a cell nobody annotated is Normal, so the default value has to be Normal
    public void The_default_state_is_normal()
    {
        Assert.Equal(CellState.Normal, default(CellState));
    }

    [Fact] // ADR-0006: every state has its own class — none of the five collapses into another
    public void Every_state_paints_a_distinct_class()
    {
        var classes = AllStates.Select(state => CellClasses.For(numeric: false, pinned: false, state)).ToList();

        Assert.Equal(classes.Count, classes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact] // ADR-0006: all 20 combinations are distinct, so no cell can be painted as another kind
    public void All_twenty_combinations_are_distinct()
    {
        var combinations =
            (from state in AllStates
             from numeric in new[] { false, true }
             from pinned in new[] { false, true }
             select CellClasses.For(numeric, pinned, state)).ToList();

        Assert.Equal(20, combinations.Count);
        Assert.Equal(20, combinations.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact] // ADR-0006: the lookup is called once per cell per render and must not allocate
    public void The_same_combination_returns_the_same_instance()
    {
        Assert.Same(
            CellClasses.For(numeric: true, pinned: true, CellState.Modified),
            CellClasses.For(numeric: true, pinned: true, CellState.Modified));
    }
}
