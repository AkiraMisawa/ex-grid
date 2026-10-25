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
    [Fact] // ADR-0332 §4 / ADR-0027 P5: the class is interned, never composed on the render path
    public void The_marked_class_is_one_instance()
    {
        Assert.Same(
            CellClasses.For(numeric: false, pinned: true, CellState.Modified, mark: MatchMark.Current),
            CellClasses.For(numeric: false, pinned: true, CellState.Modified, mark: MatchMark.Current));
    }
}
