namespace ExGrid.Cells;

/// <summary>What a step from a cell of a grid pointed at from outside reached (ADR-0058; DC-55).</summary>
public enum GridPointedStepKind
{
    /// <summary>A cell: <see cref="GridPointedStep{TRow}.Row"/> and
    /// <see cref="GridPointedStep{TRow}.Column"/> name it.</summary>
    Cell,

    /// <summary>No cell that way: up from the first row or down from the last, in the grid's current
    /// order, or left or right with none of the columns the Consumer names that way. Nothing
    /// moves.</summary>
    Edge,

    /// <summary>The row one step up or down has not arrived: a Placeholder, whose row the grid does not
    /// hold, so it has no instance to name. <see cref="GridPointedStep{TRow}.Column"/> names the
    /// column.</summary>
    RowNotArrived,

    /// <summary>No step was taken: the grid does not hold the cell to step from — no row it holds is
    /// the one named, or it shows no column of that name — or it is not pointed at now, or the
    /// declaration is passed to no grid.</summary>
    NotHeld,
}
