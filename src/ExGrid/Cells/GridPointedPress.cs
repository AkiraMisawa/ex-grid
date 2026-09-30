namespace ExGrid.Cells;

/// <summary>
/// A press a grid pointed at from outside hands over instead of acting on it (ADR-0058): where it
/// landed, in the grid's own terms. The grid decides nothing about what is written or refused for it;
/// the Consumer that declared the grid pointed at does (<see cref="GridPointedAt{TRow}"/>).
/// </summary>
/// <param name="Kind">What the press landed on.</param>
/// <param name="Row">For <see cref="GridPointedPressKind.Cell"/>, the row instance the pressed cell
/// belongs to — Row Identity, not a position: the Consumer resolves it into its own key. Null for a cell
/// whose row has not arrived (a Placeholder), and for every other kind.</param>
/// <param name="Column">For <see cref="GridPointedPressKind.Cell"/> and
/// <see cref="GridPointedPressKind.ColumnHeader"/>, the column's name, as
/// <see cref="GridColumn{TRow}.Name"/> names it. Null for the kinds that stand over more than one
/// column.</param>
public sealed record GridPointedPress<TRow>(GridPointedPressKind Kind, TRow? Row = null, string? Column = null)
    where TRow : class;
