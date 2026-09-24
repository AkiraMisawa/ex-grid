namespace ExGrid;

/// <summary>
/// A resize drag's result (ADR-0016): the column, and the width the user dragged it
/// to. The grid changes nothing itself — width is View State the Consumer owns, and a
/// drag ends the column's Auto-ness: the Consumer records this as a Fixed width,
/// because the user's intent is what is persisted.
/// </summary>
public sealed record ColumnWidthChange(string Column, double WidthPx);
