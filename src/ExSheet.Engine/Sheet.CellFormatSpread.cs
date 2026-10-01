namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// The Cell Formats the cells of <paramref name="range"/> show, each once (ADR-0071): what
    /// Format Cells reads to show a part that differs across the Selection as Excel shows it. A
    /// range whose cells all show one Cell Format answers that one alone. Its Borders are each
    /// cell's own four sides, not the edges as shown (<see cref="GetBorders"/>), because Excel's
    /// Format Cells compares what the cells record: a thick bottom on A1 over a plain A2 shows the
    /// edge between them as differing (the eleventh Windows run, case 24).
    /// </summary>
    /// <remarks>
    /// Read from what the Sheet records, never cell by cell: the cells that record a Cell Format of
    /// their own, and the rows and columns of the range grouped by what their level records. A cell
    /// recording nothing of its own shows its row's level over its column's, so every pair of a row
    /// group and a column group that holds such a cell adds that one Cell Format. A range of whole
    /// columns costs what the Sheet records in it, not a million cells each (principle 5).
    /// </remarks>
    public IReadOnlySet<CellFormat> GetCellFormats(CellRange range)
    {
        var shown = new HashSet<CellFormat>();
        // How many cells recording a Cell Format of their own lie in each block of a row group
        // and a column group: a block they fill shows nothing of its levels.
        var recorded = new Dictionary<(AxisFormat Row, AxisFormat Column), long>();
        foreach (var cell in _cells.Values)
        {
            if (!cell.IsFormatted || !range.Contains(cell.Address)) continue;
            shown.Add(OwnFormat(cell.Address));
            var block = (_rowFormats.GetValueOrDefault(cell.Address.Row), _columnFormats.GetValueOrDefault(cell.Address.Column));
            recorded[block] = recorded.GetValueOrDefault(block) + 1;
        }
        var rows = LevelGroups(_rowFormats, range.First.Row, range.Last.Row);
        var columns = LevelGroups(_columnFormats, range.First.Column, range.Last.Column);
        foreach (var (row, rowCount) in rows)
        {
            foreach (var (column, columnCount) in columns)
            {
                if (rowCount * columnCount > recorded.GetValueOrDefault((row, column))) shown.Add(row.Over(column.Over(CellFormat.Default)));
            }
        }
        return shown;
    }

    /// <summary>
    /// The levels recorded on the rows or columns <paramref name="first"/> to <paramref name="last"/>,
    /// each with how many of them record it; those recording nothing are counted under the empty
    /// level.
    /// </summary>
    private static Dictionary<AxisFormat, long> LevelGroups(Dictionary<int, AxisFormat> levels, int first, int last)
    {
        var groups = new Dictionary<AxisFormat, long>();
        long counted = 0;
        foreach (var (index, level) in levels)
        {
            if (index < first || index > last) continue;
            groups[level] = groups.GetValueOrDefault(level) + 1;
            counted++;
        }
        var unrecorded = last - first + 1 - counted;
        if (unrecorded > 0) groups[default] = groups.GetValueOrDefault(default) + unrecorded;
        return groups;
    }
}
