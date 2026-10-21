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
/// rows is searched in the time its occupied cells take. <see cref="Matches"/> is the one rule a
/// step and the Match Mark judge a cell by (ADR-0332 §4), so the mark never shows a match a step
/// does not reach.
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

        var positions = PositionsOf(request.Columns);
        var columnCount = (long)request.Columns.Count;
        var total = (long)Sheet.RowCount * columnCount;
        // The ordinal of the cell From names, in row-major order of the request's columns; the
        // walk starts one step past it and ends on it, as the reference's does.
        var start = request.From is { } from && from.Row >= 0 && from.Row < Sheet.RowCount
            && from.Column >= 0 && from.Column < columnCount
            ? from.Row * columnCount + from.Column
            : request.Backward ? 0 : total - 1;
        var pattern = request.Pattern;

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
            if (distance >= best || !Matches(sheet, address, pattern))
                continue;
            best = distance;
            found = GridFindResult.Found(address.Row, request.Columns[position]);
        }
        return found;
    }

    /// <summary>
    /// One step over every Sheet of <paramref name="workbook"/> (ADR-0331 §8, ADR-0332 §5), as Excel's Within:
    /// Workbook steps: from the cell after the Focus to the end of <paramref name="shown"/> (before it to its
    /// start, backward), then each other Sheet whole in tab order (the reverse, backward), wrapping past the
    /// last, and then <paramref name="shown"/> again up to the Focus, which is considered last. Each Sheet is
    /// walked by rows across the request's columns, as <see cref="Step"/> walks one. The Selection is no scope
    /// here. Answers the Sheet, the row and the column's place among the request's columns, or null when
    /// nothing matches anywhere.
    /// </summary>
    internal static (Sheet Sheet, int Row, int Position)? StepWorkbook(Workbook workbook, Sheet shown, GridFindRequest request)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(request.Text) || request.Columns.Count == 0)
            return null;
        var positions = PositionsOf(request.Columns);
        var columnCount = (long)request.Columns.Count;
        var total = (long)Sheet.RowCount * columnCount;
        var backward = request.Backward;
        var start = request.From is { } from && from.Row >= 0 && from.Row < Sheet.RowCount
            && from.Column >= 0 && from.Column < columnCount
            ? from.Row * columnCount + from.Column
            : backward ? total : -1;
        var pattern = request.Pattern;

        // The Sheet shown, past the Focus.
        if (Nearest(shown, positions, columnCount, pattern, backward,
                ordinal => backward ? ordinal < start : ordinal > start) is { } here)
        {
            return (shown, here.Row, here.Position);
        }
        // Every other Sheet whole, in tab order from the one shown, wrapping.
        var sheets = workbook.Sheets;
        var at = -1;
        for (var i = 0; i < sheets.Count; i++)
        {
            if (sheets[i] == shown)
                at = i;
        }
        for (var k = 1; k < sheets.Count; k++)
        {
            var sheet = sheets[(((at + (backward ? -k : k)) % sheets.Count) + sheets.Count) % sheets.Count];
            if (Nearest(sheet, positions, columnCount, pattern, backward, static _ => true) is { } there)
                return (sheet, there.Row, there.Position);
        }
        // The Sheet shown again, up to the Focus, which is considered last.
        return Nearest(shown, positions, columnCount, pattern, backward,
            ordinal => backward ? ordinal >= start : ordinal <= start) is { } back
            ? (shown, back.Row, back.Position)
            : null;
    }

    /// <summary>
    /// Whether the cell at <paramref name="address"/> of <paramref name="sheet"/> matches
    /// <paramref name="pattern"/> as a step finds it: a cell that holds an Entry, whose displayed text
    /// matches by <see cref="GridFind.Matches(string, GridFindPattern)"/>. The Match Mark judges the painted
    /// cells by it (ADR-0332 §4).
    /// </summary>
    internal static bool Matches(Sheet sheet, CellAddress address, GridFindPattern pattern)
        => sheet.GetEntry(address) is not null && GridFind.Matches(ShownText(sheet, address), pattern);

    /// <summary>
    /// What a Replace All writes (ADR-0332 §2): over <paramref name="sheets"/>, every cell whose Entry — the
    /// text the Cell Editor opens with, a Formula's text — matches <paramref name="pattern"/>, with the match
    /// replaced, in the order of the Sheets and then by rows; inside <paramref name="scope"/> and the columns
    /// <paramref name="positions"/> names when they are given. A replaced text that is a Formula the engine
    /// cannot read is refused, with the reason a typed one is refused with: then nothing may be written, and
    /// every such cell is named.
    /// </summary>
    internal static SheetReplacePlan PlanReplace(
        IEnumerable<Sheet> sheets, GridFindPattern pattern, string replacement,
        IReadOnlyDictionary<int, int>? positions = null, IReadOnlyList<SelectionRange>? scope = null)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(replacement);
        var writes = new List<SheetReplaceWrite>();
        var refused = new List<SheetReplaceRefusal>();
        if (string.IsNullOrEmpty(pattern.Text))
            return new SheetReplacePlan(writes, refused);
        foreach (var sheet in sheets)
        {
            var addresses = sheet.EntryAddresses.ToList();
            addresses.Sort(static (a, b) => a.Row != b.Row ? a.Row.CompareTo(b.Row) : a.Column.CompareTo(b.Column));
            foreach (var address in addresses)
            {
                var position = address.Column;
                if (positions is not null && !positions.TryGetValue(address.Column, out position))
                    continue;
                if (scope is not null && !InScope(scope, address.Row, position))
                    continue;
                if (GridReplace.Replaced(sheet.GetEntryText(address), pattern, replacement) is not { } text)
                    continue;
                var verdict = new SheetRow(sheet, address.Row).Judge(text);
                if (verdict.Kind == global::ExGrid.Cells.EditVerdictKind.Reject)
                {
                    refused.Add(new SheetReplaceRefusal(sheet, address, position, verdict.Message ?? ""));
                    continue;
                }
                writes.Add(new SheetReplaceWrite(sheet, address, text));
            }
        }
        return new SheetReplacePlan(writes, refused);
    }

    /// <summary>Where each sheet column stands among <paramref name="columns"/>; a column they do not
    /// name is not searched.</summary>
    internal static Dictionary<int, int> PositionsOf(IReadOnlyList<string> columns)
    {
        var positions = new Dictionary<int, int>(columns.Count);
        for (var i = 0; i < columns.Count; i++)
        {
            if (CellAddress.TryParseColumn(columns[i], out var column))
                positions[column] = i;
        }
        return positions;
    }

    // The matching cell of a Sheet nearest the walk's start among those admit lets through: the first by
    // rows forward, the last backward.
    private static (int Row, int Position)? Nearest(Sheet sheet, Dictionary<int, int> positions, long columnCount,
        GridFindPattern pattern, bool backward, Func<long, bool> admit)
    {
        var best = backward ? long.MinValue : long.MaxValue;
        (int Row, int Position)? found = null;
        foreach (var address in sheet.EntryAddresses)
        {
            if (!positions.TryGetValue(address.Column, out var position))
                continue;
            var ordinal = address.Row * columnCount + position;
            if ((backward ? ordinal <= best : ordinal >= best) || !admit(ordinal) || !Matches(sheet, address, pattern))
                continue;
            best = ordinal;
            found = (address.Row, position);
        }
        return found;
    }

    /// <summary>What the cell is painted with: the engine's display, as the grid is handed it.</summary>
    private static string ShownText(Sheet sheet, CellAddress address) =>
        SheetCellText.From(sheet.GetDisplay(address), sheet.GetValue(address), sheet.GetAlignment(address))?.Text ?? "";

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

/// <summary>One cell a Replace All writes: its Sheet, its address and the text it is entered with.</summary>
internal sealed record SheetReplaceWrite(Sheet Sheet, CellAddress Address, string Text);

/// <summary>One cell a Replace All cannot write, and why; <paramref name="Position"/> is its column's place
/// among the request's columns.</summary>
internal sealed record SheetReplaceRefusal(Sheet Sheet, CellAddress Address, int Position, string Reason);

/// <summary>What a Replace All writes, or the cells that refuse it all.</summary>
internal sealed record SheetReplacePlan(IReadOnlyList<SheetReplaceWrite> Writes, IReadOnlyList<SheetReplaceRefusal> Refused);
