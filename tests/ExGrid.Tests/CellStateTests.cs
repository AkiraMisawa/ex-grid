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

    [Fact] // ADR-0006: Normal is painted exactly as a cell was before Cell State existed
    public void Normal_adds_no_class()
    {
        Assert.Equal("ex-cell", CellClasses.For(numeric: false, pinned: false, CellState.Normal));
        Assert.Equal("ex-cell ex-cell-numeric", CellClasses.For(numeric: true, pinned: false, CellState.Normal));
        Assert.Equal("ex-cell ex-pinned", CellClasses.For(numeric: false, pinned: true, CellState.Normal));
        Assert.Equal("ex-cell ex-cell-numeric ex-pinned", CellClasses.For(numeric: true, pinned: true, CellState.Normal));
    }

    [Fact] // ADR-0006: the state rides alongside the presentation classes, it does not replace them
    public void A_state_class_joins_the_presentation_classes()
    {
        Assert.Equal("ex-cell ex-state-stale", CellClasses.For(numeric: false, pinned: false, CellState.Stale));
        Assert.Equal("ex-cell ex-cell-numeric ex-pinned ex-state-error",
            CellClasses.For(numeric: true, pinned: true, CellState.Error));
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

    [Fact] // ADR-0006: a state cast in from an integer is refused, never painted as an ordinary cell
    public void An_undefined_state_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CellClasses.For(numeric: false, pinned: false, (CellState)99));
    }
}
