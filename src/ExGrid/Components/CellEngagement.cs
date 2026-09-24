namespace ExGrid.Components;

/// <summary>
/// What a row is told about the one cell of it that Space has engaged (ADR-0037), or
/// <see cref="None"/> — which is what every other row is handed, so their memoisation is
/// untouched by it (ADR-0003).
///
/// Two shapes, never both at once, because there is only one Focus:
/// <list type="bullet">
/// <item>an <b>action choice</b> — <see cref="Action"/> is the chosen action of a cell with
/// several, painted as <c>ex-action-chosen</c> for as long as the cell is Interactive; the
/// keyboard itself never leaves the root;</item>
/// <item>a <b>focus request</b> — <see cref="FocusRequest"/> is non-zero on the one render
/// that hands it to a Template cell's content, and the core clears it straight after.</item>
/// </list>
///
/// Public because <see cref="ExGridRow{TRow}"/> is, like <see cref="ColumnStyles"/>; it is
/// the row seam's vocabulary, not something a Consumer constructs.
/// </summary>
public readonly record struct CellEngagement(int Column, int Action, int FocusRequest)
{
    /// <summary>No cell of this row is engaged. <c>default</c> is NOT this — it would name
    /// column 0's first action — which is why the row initialises its parameter to it.</summary>
    public static CellEngagement None { get; } = new(-1, -1, 0);

    /// <summary>The chosen action of an Interactive cell with several actions.</summary>
    public static CellEngagement Chosen(int column, int action) => new(column, action, 0);

    /// <summary>A Template cell's one focus request.</summary>
    public static CellEngagement Request(int column, int focusRequest) => new(column, -1, focusRequest);
}
