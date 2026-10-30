using ExGrid.Columns;

namespace ExGrid.Cells;

/// <summary>
/// How the grid asks for a cell's painted text (ADR-0050, item 11): by (row, column), with
/// the column's resolved content width and the Cell Metrics the grid measures with, so the
/// Consumer can fit its text to the column the way Excel fits General to a cell.
///
/// <para>The answer is the text to paint, or null to paint the value's own text — the
/// column's <c>Format</c>, or without one a date's ISO form or the value's <c>ToString()</c>
/// (ADR-0006). A Number or Date cell still
/// becomes <c>####</c> when the painted text does not fit (ADR-0016), so an answer that
/// overshoots is shown unreadably, never cut. Where what is painted differs from the
/// value's own text, the cell's accessible name is the value's text, as behind
/// <c>####</c> (ADR-0016/0033).</para>
///
/// <para>Only the painted cell changes. Copy, the editor, the value list and an Auto
/// width all read the value's own text: an Auto width measured from a text that is itself
/// fitted to the width would chase itself.</para>
///
/// <para><b>Keep it light, and hold it in a field.</b> It is asked once per painted value
/// cell on every render of its row, and its identity is the change signal, exactly as
/// <see cref="CellStateOf{TRow}"/>'s is (ADR-0003/0006). A row is asked again when the
/// columns' widths move, because the content width it was given has changed.</para>
/// </summary>
/// <param name="row">The row being painted.</param>
/// <param name="column">The column of the cell.</param>
/// <param name="contentWidthPx">The column's resolved width less the cell's horizontal
/// padding on both sides; zero or negative in a crushed column.</param>
/// <param name="metrics">The Cell Metrics the <c>####</c> decision is made with.</param>
/// <returns>The text to paint, or null for the value's own text.</returns>
public delegate string? PaintedTextOf<TRow>(
    TRow row, GridColumn<TRow> column, double contentWidthPx, CellTextMetrics metrics);
