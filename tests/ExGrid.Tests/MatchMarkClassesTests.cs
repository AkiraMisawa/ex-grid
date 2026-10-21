using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Match Mark's classes (ADR-0332 §4): one interned string per combination, as every other
/// cell class is (ADR-0027 P5), and an unmarked cell painted exactly as before the mark existed.
/// </summary>
public class MatchMarkClassesTests
{
    [Fact] // ADR-0332 §4: a match wears ex-match-mark, the current match ex-match-mark-current as well
    public void A_match_and_the_current_match_wear_their_classes()
    {
        Assert.Equal("ex-cell ex-match-mark", CellClasses.For(numeric: false, pinned: false, CellState.Normal, mark: MatchMark.Match));
        Assert.Equal("ex-cell ex-match-mark ex-match-mark-current",
            CellClasses.For(numeric: false, pinned: false, CellState.Normal, mark: MatchMark.Current));
        Assert.Equal("ex-cell ex-cell-numeric ex-pinned ex-align-right ex-tone-negative ex-state-stale ex-changed ex-match-mark",
            CellClasses.For(numeric: true, pinned: true, CellState.Stale, CellAlign.Right, CellTone.Negative, changed: true, mark: MatchMark.Match));
    }

    [Fact] // ADR-0332 §4: an unmarked cell is painted as it was before the mark existed
    public void An_unmarked_cell_adds_nothing()
    {
        Assert.Equal(
            CellClasses.For(numeric: true, pinned: true, CellState.Error, CellAlign.Left, CellTone.Positive),
            CellClasses.For(numeric: true, pinned: true, CellState.Error, CellAlign.Left, CellTone.Positive, mark: MatchMark.None));
        Assert.DoesNotContain("ex-match", CellClasses.For(numeric: false, pinned: false, CellState.Normal), StringComparison.Ordinal);
    }

    [Fact] // ADR-0332 §4 / ADR-0027 P5: the class is interned, never composed on the render path
    public void The_marked_class_is_one_instance()
    {
        Assert.Same(
            CellClasses.For(numeric: false, pinned: true, CellState.Modified, mark: MatchMark.Current),
            CellClasses.For(numeric: false, pinned: true, CellState.Modified, mark: MatchMark.Current));
    }

    [Fact] // ADR-0332 §4: an undefined mark is refused, as an undefined state is (ADR-0006)
    public void An_undefined_mark_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CellClasses.For(false, false, CellState.Normal, mark: (MatchMark)7));
    }
}
