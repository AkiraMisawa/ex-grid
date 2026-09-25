namespace ExGrid;

/// <summary>
/// A resize drag's result (ADR-0016): the column, and the width the user dragged it
/// to. The grid changes nothing itself — width is View State the Consumer owns, and a
/// drag ends the column's Auto-ness: the Consumer records this as a Fixed width,
/// because the user's intent is what is persisted. It records it as it came —
/// <c>ColumnWidth.Fixed(WidthPx)</c> with the column's own bounds — even past MaxWidth,
/// which bounds only what the grid computes.
/// </summary>
/// <param name="Column">The column's name.</param>
/// <param name="WidthPx">The new width in pixels — where the drag was released, or what
/// the column menu's Size to fit resolved. A drag is bounded below by MinWidth and not
/// above by MaxWidth (ADR-0016).</param>
public sealed record ColumnWidthChange(string Column, double WidthPx);
