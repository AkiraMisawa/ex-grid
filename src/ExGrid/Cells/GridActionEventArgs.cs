namespace ExGrid.Cells;

/// <summary>
/// What the grid reports when an action is pressed (ADR-0020): which row, which column,
/// which action. The grid does nothing else — what a detail action opens happens outside
/// the grid, and the Consumer holds the state (ADR-0013).
/// </summary>
/// <param name="Row">The row instance the pressed cell belongs to.</param>
/// <param name="ColumnName">The Action Column's name.</param>
/// <param name="ActionName">The <see cref="Columns.GridAction.Name"/> that was pressed.</param>
public readonly record struct GridActionEventArgs<TRow>(TRow Row, string ColumnName, string ActionName);
