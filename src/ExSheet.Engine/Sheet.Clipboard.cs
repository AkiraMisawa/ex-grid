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
        var html = new StringBuilder("<table ").Append(ExcelNamespace).Append('>');
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
                AppendCell(html, value, raw, GetFormat(address));
            }
            text.Append("\r\n");
            html.Append("</tr>");
        }
        html.Append("</table>");
        return new SheetCopy(new SheetBlock(range, states), text.ToString(), html.ToString());
    }

    /// <summary>The namespace Excel's own HTML declares for its <c>x:</c> attributes.</summary>
    private const string ExcelNamespace = "xmlns:x=\"urn:schemas-microsoft-com:office:excel\"";

    /// <summary>
    /// One cell of the <c>text/html</c> flavour, in Excel's own markup (ADR-0048, "What the
    /// observation settled"): a number carries its unformatted Value in <c>x:num</c>, and a cell
    /// whose format is not General carries the format code in <c>mso-number-format</c>, so a date
    /// arrives in Excel as a date and <c>#,##0.00</c> as itself. The content stays the unformatted
    /// Value (ADR-0016). Whether Excel takes both as intended is verified in the next Windows run.
    /// </summary>
    private static void AppendCell(StringBuilder html, Value? value, string raw, NumberFormat format)
    {
        html.Append("<td");
        if (value is { Kind: ValueKind.Number }) html.Append(" x:num=\"").Append(raw).Append('"');
        if (!format.IsGeneral) html.Append(" style='mso-number-format:\"").Append(CssFormatCode(format.Code)).Append("\"'");
        html.Append('>').Append(EscapeHtml(raw)).Append("</td>");
    }

    /// <summary>
    /// A format code as Excel's HTML writes it inside <c>mso-number-format:"…"</c>: letters,
    /// digits, characters outside ASCII, the space and <c>_ * ? % -</c> as they are, as Excel
    /// leaves them (<c>_\(\0022$\0022* \#\,\#\#0\.00_\)</c> is Excel's own accounting format);
    /// every other ASCII character escaped with a backslash (<c>#,##0.00</c> is
    /// <c>\#\,\#\#0\.00</c>, <c>m/d/yyyy</c> is <c>m\/d\/yyyy</c>); and the characters that would
    /// end the CSS string or the single-quoted attribute around it — <c>"</c>, <c>'</c>,
    /// <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c> — as CSS hex escapes, as Excel writes a quote
    /// <c>\0022</c>.
    /// </summary>
    internal static string CssFormatCode(string code)
    {
        var css = new StringBuilder(code.Length * 2);
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (char.IsAsciiLetterOrDigit(c) || c > '\x7F' || c is '_' or '*' or '?' or '%' or '-' or ' ')
            {
                css.Append(c);
            }
            else if (c is '"' or '\'' or '&' or '<' or '>')
            {
                css.Append('\\').Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
                // A hex escape runs on through hex digits and swallows one space after it.
                if (i + 1 < code.Length && (char.IsAsciiHexDigit(code[i + 1]) || code[i + 1] == ' ')) css.Append(' ');
            }
            else
            {
                css.Append('\\').Append(c);
            }
        }
        return css.ToString();
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
