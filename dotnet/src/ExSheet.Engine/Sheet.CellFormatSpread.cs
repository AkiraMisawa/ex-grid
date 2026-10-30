namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// The Cell Formats the cells of <paramref name="range"/> show, each once (ADR-0071): what
    /// Format Cells reads to show a part that differs across the Selection as Excel shows it. A
    /// range whose cells all show one Cell Format answers that one alone. Its Borders are each
    /// cell's own four sides, not the edges as shown (<see cref="GetBorders"/>), because Excel's
    /// Format Cells compares what the cells record on an edge inside the Selection: a thick bottom on
    /// A1 over a plain A2 shows the edge between them as differing (the eleventh Windows run, case
    /// 24). A range's outer edges it shows as drawn, which <see cref="GetEdgeLines"/> answers.
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
    /// The lines drawn along each outer edge of <paramref name="range"/>, each once per edge
    /// (ADR-0071; the fourteenth Windows run, case 13): what Format Cells shows on a range's outer
    /// edges. Each is the line <see cref="GetBorders"/> answers on that side of a cell along the
    /// edge, so where the cell records no line there, its neighbour's shows: under A1's thick
    /// bottom, A2's top is thick though A2 records nothing. Where both record one, the upper cell's
    /// shows, or the left cell's. A side on the Sheet's outer edge is the cell's own.
    /// </summary>
    /// <remarks>
    /// Read from what the Sheet records, never cell by cell, as <see cref="GetCellFormats"/> is: the
    /// cells beside the edge that record Borders of their own, one by one, and every other place by
    /// the level across the edge there. The left edge of whole columns costs what the Sheet records
    /// beside it, not a million cells (principle 5).
    /// </remarks>
    public RangeEdgeLines GetEdgeLines(CellRange range)
    {
        var (first, last) = (range.First, range.Last);
        return new RangeEdgeLines(
            LinesBetween(horizontal: true, first.Row - 1, first.Row, first.Column, last.Column),
            LinesBetween(horizontal: true, last.Row, last.Row + 1, first.Column, last.Column),
            LinesBetween(horizontal: false, first.Column - 1, first.Column, first.Row, last.Row),
            LinesBetween(horizontal: false, last.Column, last.Column + 1, first.Row, last.Row));
    }

    /// <summary>
    /// The lines drawn on the edges between two neighbouring lines of cells, each once: rows
    /// <paramref name="before"/> and <paramref name="after"/> for a horizontal edge, columns for a
    /// vertical one, from <paramref name="first"/> to <paramref name="last"/> along it. A line
    /// beyond the Sheet's edge records nothing.
    /// </summary>
    private HashSet<BorderLine> LinesBetween(bool horizontal, int before, int after, int first, int last)
    {
        var lines = new HashSet<BorderLine>();
        var count = horizontal ? RowCount : ColumnCount;
        var hasBefore = before >= 0;
        var hasAfter = after < count;
        // The levels across the edge: each column's along a horizontal edge, each row's along a vertical one.
        var across = horizontal ? _columnFormats : _rowFormats;
        var groups = LevelGroups(across, first, last);
        // Where a cell on either side records Borders of its own, the edge is read there, and that
        // place leaves its level's group.
        var read = new HashSet<int>();
        foreach (var cell in _cells.Values)
        {
            if (cell.Borders is null) continue;
            var (line, position) = horizontal ? (cell.Address.Row, cell.Address.Column) : (cell.Address.Column, cell.Address.Row);
            if ((line != before && line != after) || position < first || position > last || !read.Add(position)) continue;
            lines.Add(Shown(Own(before, position), Own(after, position)));
            groups[across.GetValueOrDefault(position)]--;
        }
        // Every other place shows what the levels record: each cell's row over its column.
        foreach (var (level, places) in groups)
        {
            if (places > 0) lines.Add(Shown(Levelled(before, level), Levelled(after, level)));
        }
        return lines;

        BorderLine Shown(CellBorders upperOrLeft, CellBorders lowerOrRight) => horizontal
            ? Edge(hasBefore ? upperOrLeft.Bottom : BorderLine.None, hasAfter ? lowerOrRight.Top : BorderLine.None)
            : Edge(hasBefore ? upperOrLeft.Right : BorderLine.None, hasAfter ? lowerOrRight.Left : BorderLine.None);

        CellBorders Own(int line, int position) =>
            line < 0 || line >= count ? CellBorders.None : OwnSides(horizontal ? new CellAddress(line, position) : new CellAddress(position, line));

        CellBorders Levelled(int line, AxisFormat level) => horizontal
            ? _rowFormats.GetValueOrDefault(line).Borders ?? level.Borders ?? CellBorders.None
            : level.Borders ?? _columnFormats.GetValueOrDefault(line).Borders ?? CellBorders.None;
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
