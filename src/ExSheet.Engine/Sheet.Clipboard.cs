using System.Text;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// Copies a rectangle (ADR-0048): its Entries for a paste inside ExSheet, and its Values for
    /// anywhere else. A rectangle holding a <c>#GETTING_DATA</c> Value is refused whole (ADR-0049).
    /// </summary>
    public SheetCopy Copy(CellRange range)
    {
        var area = new Area(range.First.Row, range.First.Column, range.Last.Row, range.Last.Column);
        CellAddress? waiting = null;
        foreach (var address in CellsIn(area))
        {
            if (GetValue(address) is { IsError: true, Error: ErrorValue.GettingData } && (waiting is null || address.CompareTo(waiting.Value) < 0)) waiting = address;
        }
        if (waiting is { } at)
        {
            return new SheetCopy(new SheetRefusal(SheetRefusalReason.WaitingForData,
                $"{at} is waiting for a Linked Table's data (#GETTING_DATA); a copy would carry a wait that means nothing where it lands."));
        }

        var states = new CellState[range.CellCount];
        var text = new StringBuilder();
        var html = new StringBuilder("<table>");
        var i = 0;
        for (var row = range.First.Row; row <= range.Last.Row; row++)
        {
            html.Append("<tr>");
            for (var column = range.First.Column; column <= range.Last.Column; column++)
            {
                var address = new CellAddress(row, column);
                states[i++] = ShownState(address);
                if (column > range.First.Column) text.Append('\t');
                var value = GetValue(address);
                var raw = value?.ToString() ?? "";
                var display = GetDisplay(address);
                // What a cell that cannot be shown holds is its Value, never the #### it paints (ADR-0016).
                text.Append(QuoteForTsv(display.CannotShow ? raw : display.Text));
                html.Append("<td>").Append(EscapeHtml(raw)).Append("</td>");
            }
            text.Append("\r\n");
            html.Append("</tr>");
        }
        html.Append("</table>");
        return new SheetCopy(new SheetBlock(range, states), text.ToString(), html.ToString());
    }

    /// <summary>Excel's TSV: a field holding a tab, a line break or a quote is quoted, inner quotes doubled.</summary>
    private static string QuoteForTsv(string value) =>
        value.AsSpan().IndexOfAny('\t', '\n', '\r') < 0 && !value.Contains('"', StringComparison.Ordinal)
            ? value
            : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string EscapeHtml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// The states a block pasted at <paramref name="origin"/> writes: each cell's Entry with its
    /// relative References shifted by the distance from the block's source, its format and its
    /// alignment; a blank cell of the block clears the cell it lands on, as Excel's paste does.
    /// </summary>
    internal static IEnumerable<(CellAddress Address, CellState State)> Place(SheetBlock block, CellAddress origin)
    {
        var rows = origin.Row - block.Source.First.Row;
        var columns = origin.Column - block.Source.First.Column;
        for (var r = 0; r < block.RowCount; r++)
        {
            for (var c = 0; c < block.ColumnCount; c++)
            {
                var state = block.At(r, c);
                var entry = state.Entry is { } e ? ReferenceShift.Shift(e, rows, columns) : null;
                yield return (new CellAddress(origin.Row + r, origin.Column + c), state with { Entry = entry });
            }
        }
    }

    /// <summary>A block of <paramref name="rows"/> × <paramref name="columns"/> at <paramref name="origin"/> that would run past the Sheet's edge.</summary>
    internal static SheetRefusal? OffSheet(CellAddress origin, int rows, int columns)
    {
        if ((long)origin.Row + rows <= RowCount && (long)origin.Column + columns <= ColumnCount) return null;
        return new SheetRefusal(SheetRefusalReason.BlockWouldLeaveSheet,
            $"A block of {rows} × {columns} at {origin} would run past the Sheet's edge (XFD1048576).");
    }
}
