using ExGrid.Summarizing;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// ExSheet's answer to a Selection Summary (ADR-0130): the Values of the selected cells — a
/// Formula's result, never its Entry, and every cell an array spills into — folded into the
/// family's one definition of the figures (<see cref="GridSummaryCells"/>). A number is in
/// every figure; text and a Boolean are counted; an Error Value is counted and leaves Count alone;
/// a blank cell is in none. A date is a number here, as it is in the Sheet and in Excel: a Sheet
/// date is a serial number shown with a date format (ADR-0047), so nothing is invented by summing
/// it. It walks only the cells that hold a Value, so a whole column of a million rows is summarised
/// in the time its occupied cells take.
/// </summary>
internal static class SheetSummary
{
    /// <summary>The figures <paramref name="request"/> asks for over <paramref name="sheet"/>.</summary>
    internal static GridSummaryResult Answer(Sheet sheet, GridSummaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);

        // Where each sheet column stands in the request's order; a column it does not name is not
        // summarised.
        var positions = new Dictionary<int, int>(request.Columns.Count);
        for (var i = 0; i < request.Columns.Count; i++)
        {
            if (CellAddress.TryParseColumn(request.Columns[i], out var column))
                positions[column] = i;
        }

        var cells = new GridSummaryCells();
        foreach (var address in sheet.ValueAddresses)
        {
            if (!positions.TryGetValue(address.Column, out var position) || !InRanges(request, address.Row, position))
                continue;
            if (sheet.GetValue(address) is not { } value)
                continue;
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
        return cells.Answer(request.Figures);
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
