namespace ExGrid.Cells;

/// <summary>What a step from a cell or a column of a grid pointed at from outside reached (ADR-0058;
/// DC-55).</summary>
public enum GridPointedStepKind
{
    /// <summary>A cell: <see cref="GridPointedStep{TRow}.Row"/> and
    /// <see cref="GridPointedStep{TRow}.Column"/> name it. Down from a column, the column's first
    /// row.</summary>
    Cell,

    /// <summary>No cell or column that way: up from the first row or down from the last, in the grid's
    /// current order; up from a column; down from a column of a grid with no rows; or left or right
    /// with none of the columns the Consumer names that way. Nothing moves.</summary>
    Edge,

    /// <summary>The row one step up or down has not arrived — or, down from a column, the column's
    /// first row: a Placeholder, whose row the grid does not hold, so it has no instance to name.
    /// <see cref="GridPointedStep{TRow}.Column"/> names the column.</summary>
    RowNotArrived,

    /// <summary>No step was taken: the grid does not hold the cell or the column to step from — no row
    /// it holds is the one named, or it shows no column of that name — or it is not pointed at now, or
    /// the declaration is passed to no grid.</summary>
    NotHeld,

    /// <summary>A whole column, as a step left or right from a column reaches (Part B of the ninth
    /// Windows run): <see cref="GridPointedStep{TRow}.Column"/> names it, and no row is named.</summary>
    Column,
}
