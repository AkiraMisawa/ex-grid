namespace ExGrid.Cells;

/// <summary>
/// A grid pointed at from outside answers where one step from a cell lands (ADR-0058, "The keyboard";
/// DC-55): the cell there, or why there is none. Asked through
/// <see cref="GridPointedAt{TRow}.StepAsync"/>. The grid decides nothing about what is written for the
/// cell; the Consumer that declared it pointed at does.
/// </summary>
/// <param name="Kind">Whether the step reached a cell, and if not, why.</param>
/// <param name="Row">For <see cref="GridPointedStepKind.Cell"/>, the row instance the cell belongs to —
/// Row Identity, not a position. Null for every other kind.</param>
/// <param name="Column">For <see cref="GridPointedStepKind.Cell"/> and
/// <see cref="GridPointedStepKind.RowNotArrived"/>, the name of the column the step reached, as
/// <see cref="GridColumn{TRow}.Name"/> names it. Null for the other kinds.</param>
public sealed record GridPointedStep<TRow>(GridPointedStepKind Kind, TRow? Row = null, string? Column = null)
    where TRow : class;
