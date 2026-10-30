namespace ExGrid.Selection;

/// <summary>
/// The bounds a selection transition is valid against: the post-filter row count
/// (TotalCount) and the visible-column count. Passed into every transition rather than
/// held in the state — both change independently of the selection, and a held copy would
/// go stale exactly when positions become dangerous (ADR-0011).
/// </summary>
/// <param name="RowCount">The post-filter row count (TotalCount).</param>
/// <param name="ColumnCount">The visible-column count.</param>
public readonly record struct GridExtent(int RowCount, int ColumnCount);
