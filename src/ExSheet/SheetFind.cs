using ExGrid.Finding;
using ExGrid.Selection;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// ExSheet's answer to a Find step (ADR-0055): the Sheet's cells, every row of them, searched by
/// their displayed text — the text the cell is painted with, a Formula's Value as shown, never
/// its Entry. It follows <see cref="GridFind.Step{TRow}"/>, the reference, clause for clause: by
/// rows, across a row in the order of the request's columns, from the cell after the Focus
/// (before it, backward), wrapping, <c>OrdinalIgnoreCase</c> unless Match case, containment
/// unless Match entire cell contents, inside the scope when there is one. It walks only the cells
/// that hold an Entry, since a Blank shows no text a search could match, so a Sheet of a million
/// rows is searched in the time its occupied cells take.
/// </summary>
internal static class SheetFind
{
    /// <summary>One step over <paramref name="sheet"/>, or not found.</summary>
    internal static GridFindResult Step(Sheet sheet, GridFindRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.Text) || request.Columns.Count == 0)
            return GridFindResult.NotFound;

        // Where each sheet column stands in the request's order; a column it does not name is
        // not searched.
        var positions = new Dictionary<int, int>(request.Columns.Count);
        for (var i = 0; i < request.Columns.Count; i++)
        {
            if (CellAddress.TryParseColumn(request.Columns[i], out var column))
                positions[column] = i;
        }

        var columnCount = (long)request.Columns.Count;
        var total = (long)Sheet.RowCount * columnCount;
        // The ordinal of the cell From names, in row-major order of the request's columns; the
        // walk starts one step past it and ends on it, as the reference's does.
        var start = request.From is { } from && from.Row >= 0 && from.Row < Sheet.RowCount
            && from.Column >= 0 && from.Column < columnCount
            ? from.Row * columnCount + from.Column
            : request.Backward ? 0 : total - 1;
        var comparison = request.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var best = long.MaxValue;
        GridFindResult found = GridFindResult.NotFound;
        foreach (var address in sheet.EntryAddresses)
        {
            if (!positions.TryGetValue(address.Column, out var position))
                continue;
            if (request.Scope is { } scope && !InScope(scope, address.Row, position))
                continue;
            var ordinal = address.Row * columnCount + position;
            // How many steps the walk takes to reach this cell: 1 for the cell after From, and
            // the most — the whole Sheet — for From's own cell, which is considered last.
            var distance = request.Backward ? start - ordinal : ordinal - start;
            distance = ((distance % total) + total) % total;
            if (distance == 0)
                distance = total;
            if (distance >= best || !Matches(ShownText(sheet, address), request, comparison))
                continue;
            best = distance;
            found = GridFindResult.Found(address.Row, request.Columns[position]);
        }
        return found;
    }

    /// <summary>What the cell is painted with: the engine's display, as the grid is handed it.</summary>
    private static string ShownText(Sheet sheet, CellAddress address) =>
        SheetCellText.From(sheet.GetDisplay(address), sheet.GetValue(address), sheet.GetAlignment(address))?.Text ?? "";

    private static bool Matches(string shown, GridFindRequest request, StringComparison comparison) =>
        request.WholeCell
            ? string.Equals(shown, request.Text, comparison)
            : shown.Contains(request.Text, comparison);

    private static bool InScope(IReadOnlyList<SelectionRange> scope, int row, int column)
    {
        foreach (var range in scope)
        {
            if (range.Contains(new CellPosition(row, column)))
                return true;
        }
        return false;
    }
}
