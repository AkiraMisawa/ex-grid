using ExGrid.Summarizing;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// ExSheet's answer to a Selection Summary (ADR-0130): the Values of the selected cells — a
/// Formula's result, never its Entry, and every cell an array spills into — folded into the
/// family's one definition of the figures (<see cref="GridSummaryCells"/>). A number is in
/// every figure; text and a Boolean are counted and are never a number; an Error Value is counted,
/// is never a number, and leaves only Count and Numerical Count, as Excel's status bar does; a blank
/// cell is in none. A date is a number here, as it is in the Sheet and in Excel: a Sheet
/// date is a serial number shown with a date format (ADR-0047), so nothing is invented by summing
/// it. It walks only the cells that hold a Value, so a whole column of a million rows is summarised
/// in the time its occupied cells take.
/// <para>
/// A refused cell among them (<see cref="Value.IsRefused"/>, a Refused Formula's <c>#VALUE!</c>) declines
/// the whole summary, its reason in ExSheet's words in place of the figures (2026-10-13, decided with the
/// user: no numeric figure stands over a refused cell, and the line says why). Excel would answer that
/// Formula, so a Sum, an Average, a Min, a Max or a Numerical Count over it would not be Excel's; nor would
/// Count, where Excel's answer spills and a refused Formula of unknown shape spills nothing. A partial summary is never
/// shown (ADR-0130), so Count goes with the rest.
/// </para>
/// </summary>
internal static class SheetSummary
{
    /// <summary>The figures <paramref name="request"/> asks for over <paramref name="sheet"/>, or the
    /// summary declined with <paramref name="refused"/>'s sentence for the first refused cell, in reading
    /// order, when the Selection holds one.</summary>
    internal static GridSummaryResult Answer(Sheet sheet, GridSummaryRequest request, Func<CellAddress, string> refused)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(refused);

        // Where each sheet column stands in the request's order; a column it does not name is not
        // summarised.
        var positions = new Dictionary<int, int>(request.Columns.Count);
        for (var i = 0; i < request.Columns.Count; i++)
        {
            if (CellAddress.TryParseColumn(request.Columns[i], out var column))
                positions[column] = i;
        }

        var cells = new GridSummaryCells();
        CellAddress? firstRefused = null;
        foreach (var address in sheet.ValueAddresses)
        {
            if (!positions.TryGetValue(address.Column, out var position) || !InRanges(request, address.Row, position))
                continue;
            if (sheet.GetValue(address) is not { } value)
                continue;
            if (value.IsRefused)
            {
                if (firstRefused is not { } first || address.CompareTo(first) < 0) firstRefused = address;
                continue;
            }
            switch (value.Kind)
            {
                case ValueKind.Number:
                    cells.AddNumber(value.Number);
                    break;
                case ValueKind.Error:
                    cells.AddError();
                    break;
                default:
                    cells.AddOther();
                    break;
            }
        }
        if (firstRefused is { } refusedAt) return GridSummaryResult.Declined(refused(refusedAt));
        // Average, Min, Max and Sum are shown in the Focus cell's Number Format, whatever the other
        // cells' formats, as Excel's status bar shows them in the active cell's; the counts are
        // whole numbers (read in the Windows run of 2026-10-11).
        var focus = request.Focus is { } at && at.Column < request.Columns.Count
            && CellAddress.TryParseColumn(request.Columns[at.Column], out var focusColumn)
            ? new CellAddress(at.Row, focusColumn)
            : (CellAddress?)null;
        return cells.Answer(request.Figures, focus is { } cell
            ? figure => sheet.FormatAs(cell, Convert.ToDouble(figure, System.Globalization.CultureInfo.InvariantCulture))
            : null);
    }

    // A cell covered by two ranges is one address, visited once.
    private static bool InRanges(GridSummaryRequest request, int row, int column)
    {
        foreach (var range in request.Ranges)
        {
            if (range.Contains(new ExGrid.Selection.CellPosition(row, column)))
                return true;
        }
        return false;
    }
}
