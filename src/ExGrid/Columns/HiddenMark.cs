using ExGrid.Cells;

namespace ExGrid.Columns;

/// <summary>
/// The mark a collapsed Header Group wears when a column it hides holds a Cell State
/// (ADR-0400 section 6): painted as a cell in that state is, with <see cref="Reason"/> as its
/// accessible description and its tooltip. It covers the group, not a row, and the Consumer
/// decides it: the grid does not look into a column it was not given.
/// </summary>
/// <param name="State">The Cell State the hidden columns hold, such as
/// <see cref="CellState.Modified"/> for a value the user set; never <see cref="CellState.Normal"/>.</param>
/// <param name="Reason">What the mark says, such as "2 user-set fields in hidden columns". Never
/// empty: the mark says what it hides.</param>
public sealed record HiddenMark(CellState State, string Reason);
