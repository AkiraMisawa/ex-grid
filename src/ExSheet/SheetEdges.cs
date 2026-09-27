using ExGrid.Selection;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// Where Ctrl+arrow stops on a Sheet (ADR-0050, item 2), answered as Excel answers it from the
/// Sheet's blanks. A cell is filled when it holds an Entry — a Formula whose Value is empty text
/// is filled, as in Excel — and blank otherwise; formatting alone does not fill a cell.
/// <list type="bullet">
/// <item>From a filled cell whose neighbour in that direction is filled: to the last filled cell of
/// that block.</item>
/// <item>Otherwise — from a blank cell, or from the last cell of a block: to the next filled cell
/// along the line, or to the Sheet's edge when there is none.</item>
/// <item>At the Sheet's edge: nowhere.</item>
/// </list>
/// The filled cells are indexed per column and per row, and kept in step with every change the
/// Sheet reports, so an answer never walks the blanks between two blocks.
/// </summary>
internal sealed class SheetEdges
{
    private readonly Dictionary<int, SortedSet<int>> _rowsByColumn = [];
    private readonly Dictionary<int, SortedSet<int>> _columnsByRow = [];

    /// <summary>The index of every filled cell of <paramref name="sheet"/>.</summary>
    internal static SheetEdges Of(Sheet sheet)
    {
        var edges = new SheetEdges();
        foreach (var address in sheet.EntryAddresses) edges.Set(address, filled: true);
        return edges;
    }

    /// <summary>
    /// Brings the index up to date after <paramref name="change"/>. Every cell that became filled
    /// or blank is among the change's Value changes: a blank cell has no Value and a filled one
    /// has one.
    /// </summary>
    internal void Update(Sheet sheet, SheetChange change)
    {
        foreach (var address in change.ValueChanges) Set(address, sheet.GetEntry(address) is not null);
    }

    /// <summary>The cell Ctrl+arrow reaches from <paramref name="from"/> in <paramref name="direction"/>.</summary>
    internal CellAddress Find(CellAddress from, GridDirection direction) => direction switch
    {
        GridDirection.Up => new CellAddress(Along(Line(_rowsByColumn, from.Column), from.Row, -1, Sheet.RowCount - 1), from.Column),
        GridDirection.Down => new CellAddress(Along(Line(_rowsByColumn, from.Column), from.Row, +1, Sheet.RowCount - 1), from.Column),
        GridDirection.Left => new CellAddress(from.Row, Along(Line(_columnsByRow, from.Row), from.Column, -1, Sheet.ColumnCount - 1)),
        GridDirection.Right => new CellAddress(from.Row, Along(Line(_columnsByRow, from.Row), from.Column, +1, Sheet.ColumnCount - 1)),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    private static SortedSet<int>? Line(Dictionary<int, SortedSet<int>> index, int line) =>
        index.GetValueOrDefault(line);

    private static int Along(SortedSet<int>? filled, int at, int step, int last)
    {
        var next = at + step;
        if (next < 0 || next > last) return at;
        if (filled is null || filled.Count == 0) return step > 0 ? last : 0;
        if (filled.Contains(at) && filled.Contains(next))
        {
            while (next + step >= 0 && next + step <= last && filled.Contains(next + step)) next += step;
            return next;
        }
        // The first filled cell past this one, read off the view's first element: a view's
        // Count walks the whole subtree, its first element does not.
        var beyond = step > 0 ? filled.GetViewBetween(next, last) : filled.GetViewBetween(0, next).Reverse();
        foreach (var position in beyond) return position;
        return step > 0 ? last : 0;
    }

    private void Set(CellAddress address, bool filled)
    {
        Mark(_rowsByColumn, address.Column, address.Row, filled);
        Mark(_columnsByRow, address.Row, address.Column, filled);
    }

    private static void Mark(Dictionary<int, SortedSet<int>> index, int line, int position, bool filled)
    {
        if (filled)
        {
            if (!index.TryGetValue(line, out var set)) index[line] = set = [];
            set.Add(position);
        }
        else if (index.TryGetValue(line, out var set) && set.Remove(position) && set.Count == 0)
        {
            index.Remove(line);
        }
    }
}
