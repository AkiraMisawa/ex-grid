using ExGrid.Columns;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// The columns a number widens, as Excel's do: a number or date typed into a column that does not
/// hold it (ADR-0047, second round; SH-20), and the numbers a Number Format is set on that it no
/// longer fits — a formatting key, Format Cells' OK, <c>SetCellFormatAsync</c> (ADR-0071, case 17).
/// </summary>
/// <remarks>
/// A recorded width is one of three kinds (ADR-0046, 2026-09-28; SH-26). A column at the default
/// width, or one an earlier entry or Number Format widened, is widened again by a number that
/// needs more, and the width it records is again the entry's kind; nothing narrows it. A width the
/// user set — a drag, a size to fit, a command — is never widened (case 18), so a widening never
/// turns it back into the entry's kind. Only these widen: not a paste, a fill or a recalculated
/// Formula.
/// </remarks>
internal static class SheetWidening
{
    /// <summary>
    /// The columns the numbers in <paramref name="cells"/> widen, ascending, each with the width in
    /// characters it is widened to. The engine says how many characters each number needs under
    /// its Number Format as it is now (<see cref="Sheet.GetWidthOnEntry"/>); the width in pixels is
    /// what the grid's own estimate charges for the text the cell shows at that many characters,
    /// in the Cell Metrics the grid resolves, so the widened column holds it and the grid does not
    /// hash it (ADR-0016). A column whose width in <paramref name="columns"/> already holds the
    /// widest of its numbers is left as it is. A whole column or row costs what the Sheet holds,
    /// not its million cells.
    /// </summary>
    internal static IReadOnlyList<(int Column, double Characters)> Of(Sheet sheet, IEnumerable<CellRange> cells, SheetColumnList columns, CellTextMetrics metrics)
    {
        var needed = new SortedDictionary<int, double>();
        foreach (var range in cells)
        {
            foreach (var address in sheet.EntryAddressesIn(range))
            {
                var column = address.Column;
                if (sheet.GetColumnWidth(column) is { IsSetByUser: true }) continue;
                if (sheet.GetWidthOnEntry(address) is not { } characters) continue;
                var shown = sheet.GetDisplay(address, characters);
                var px = Math.Ceiling(Math.Max(SheetColumns.PxOf(characters, metrics), metrics.EstimatePx(shown.Text)));
                if (px > columns.WidthPxOf(column) && px > needed.GetValueOrDefault(column)) needed[column] = px;
            }
        }
        var widths = new List<(int Column, double Characters)>(needed.Count);
        foreach (var (column, px) in needed)
        {
            if (SheetColumns.CharactersOfColumn(px, metrics) is { } width) widths.Add((column, width));
        }
        return widths;
    }
}
