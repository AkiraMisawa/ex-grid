using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// What a worksheet writes for a Sheet's Cell Formats and column widths (ADR-0330 §4, ADR-0046, ADR-0047, ADR-0071): its
/// columns' widths and formats, its rows' formats, and each cell's format.
/// </summary>
/// <remarks>
/// A Sheet records a format part by part at three levels, cell over row over column; a file records one whole format at
/// each, and a cell element's format stands alone. So a column writes its own level over the default, a row its own level
/// over the default (in the file a row's format stands in place of its columns'), and every cell the Sheet records, and every
/// cell where a formatted row crosses a formatted column and shows another format than the row's, writes the format the Sheet
/// shows there. A width is written in the file's unit (<see cref="ColumnWidthUnit"/>), marked fitted (<c>bestFit</c>) where
/// an entry widened it; a column that records a format and no width is written at the default width, not marked as set.
/// </remarks>
/// <param name="Columns">The column ranges, in order, each with its width and its format's index.</param>
/// <param name="Rows">The rows that record a format, in order, each with its format's index.</param>
/// <param name="Cells">Every cell that writes an element, by address, with its format's index: the Sheet's cells, and the crossings.</param>
internal sealed record SheetFormatPlan(IReadOnlyList<XlsxColumn> Columns, IReadOnlyList<XlsxRow> Rows, IReadOnlyDictionary<CellAddress, int> Cells)
{
    /// <summary>The plan for <paramref name="sheet"/>, its formats added to <paramref name="styles"/>.</summary>
    public static SheetFormatPlan For(SheetDocument sheet, StyleTable styles)
    {
        var columnLevels = new Dictionary<int, FormatParts>();
        foreach (var run in sheet.Columns)
        {
            for (var c = run.First; c <= run.Last; c++) columnLevels[c] = Parts(run);
        }
        var rowLevels = new SortedDictionary<int, FormatParts>();
        foreach (var run in sheet.Rows)
        {
            for (var r = run.First; r <= run.Last; r++) rowLevels[r] = Parts(run);
        }

        CellFormat Shown(FormatParts own, int row, int column)
        {
            var under = columnLevels.TryGetValue(column, out var c) ? c.Over(CellFormat.Default) : CellFormat.Default;
            if (rowLevels.TryGetValue(row, out var r)) under = r.Over(under);
            return own.Over(under);
        }

        var cells = new Dictionary<CellAddress, int>();
        foreach (var cell in sheet.Cells)
            cells[cell.Address] = styles.Index(Shown(new FormatParts(cell.NumberFormat, cell.Alignment, cell.Font, cell.Fill, cell.Borders), cell.Address.Row, cell.Address.Column));

        var rows = new List<XlsxRow>(rowLevels.Count);
        foreach (var (row, level) in rowLevels)
        {
            var rowFormat = level.Over(CellFormat.Default);
            var rowIndex = styles.Index(rowFormat);
            rows.Add(new XlsxRow(row, rowIndex));
            foreach (var column in columnLevels.Keys)
            {
                var at = new CellAddress(row, column);
                if (cells.ContainsKey(at)) continue;
                var shown = Shown(default, row, column);
                if (shown != rowFormat) cells[at] = styles.Index(shown);
            }
        }

        return new SheetFormatPlan(ColumnRanges(sheet, columnLevels, styles), rows, cells);
    }

    private static FormatParts Parts(SheetDocumentAxisFormat run) => new(run.NumberFormat, run.Alignment, run.Font, run.Fill, run.Borders);

    /// <summary>The columns' widths and formats as ranges: a range wherever both stay the same.</summary>
    private static List<XlsxColumn> ColumnRanges(SheetDocument sheet, Dictionary<int, FormatParts> levels, StyleTable styles)
    {
        var widths = new Dictionary<int, SheetDocumentColumnWidth>();
        foreach (var run in sheet.ColumnWidths)
        {
            for (var c = run.First; c <= run.Last; c++) widths[c] = run;
        }
        var ranges = new List<XlsxColumn>();
        foreach (var column in widths.Keys.Union(levels.Keys).Order())
        {
            // A column that records a format and no width of its own is written at the default width, not marked as set: a <col>
            // with no width is believed to be read by Excel as a width of 0, which hides the column (ticket 07 asks Excel).
            var custom = widths.TryGetValue(column, out var w);
            var width = ColumnWidthUnit.ToFile(custom ? w!.Width : Sheet.DefaultColumnWidth);
            var bestFit = w is { Kind: SheetColumnWidthKind.WidenedByEntry };
            var style = levels.TryGetValue(column, out var level) ? styles.Index(level.Over(CellFormat.Default)) : 0;
            var next = new XlsxColumn(column, column, width, StyleIndex: style, BestFit: bestFit, CustomWidth: custom);
            if (ranges.Count > 0 && ranges[^1] is var last && last.Last == column - 1 && last with { Last = column } == next with { First = last.First })
                ranges[^1] = last with { Last = column };
            else
                ranges.Add(next);
        }
        return ranges;
    }
}
