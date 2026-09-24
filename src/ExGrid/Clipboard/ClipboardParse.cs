using System.Text;

namespace ExGrid.Clipboard;

/// <summary>
/// Turns what the <c>paste</c> event handed over into a rectangular block of cell
/// values, or null when the clipboard holds nothing tabular — the paste rules are
/// never consulted then (ADR-0014).
///
/// The HTML flavour is preferred when it holds a table: Excel puts one on the
/// clipboard when copying, and its <c>x:num</c> attribute carries the raw value at
/// full precision, so reading it preserves what the TSV's display format has already
/// rounded (ADR-0005). Anything else falls back to the plain text, read as Excel's
/// TSV — tab-separated cells, lines as rows, quotes around a cell that holds a tab or
/// a line break.
/// </summary>
public static class ClipboardParse
{
    /// <summary>The parsed block, rectangular by construction — a short line is padded
    /// with empty cells, as Excel pads. Null when neither flavour holds anything.</summary>
    public static IReadOnlyList<IReadOnlyList<string>>? Parse(string? html, string? text)
    {
        if (!string.IsNullOrEmpty(html) && ParseHtmlTable(html) is { Count: > 0 } table)
            return Rectangular(table);
        if (!string.IsNullOrEmpty(text) && ParseTsv(text) is { Count: > 0 } block)
            return Rectangular(block);
        return null;
    }

    /// <summary>
    /// Excel-shaped TSV: cells split on tabs, rows on line breaks, and a cell that was
    /// quoted (because it holds a tab, a break or a quote) read back as one cell with
    /// inner doubled quotes undone. One trailing line break — Excel always appends it —
    /// does not become an extra empty row.
    /// </summary>
    private static List<List<string>> ParseTsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var cellStart = true;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when cellStart:
                    quoted = true;
                    cellStart = false;
                    break;
                case '\t':
                    row.Add(cell.ToString());
                    cell.Clear();
                    cellStart = true;
                    break;
                case '\n':
                case '\r':
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(cell.ToString());
                    cell.Clear();
                    cellStart = true;
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    cell.Append(c);
                    cellStart = false;
                    break;
            }
        }

        // Whatever follows the last break is a final row — unless it is nothing at all,
        // which is the trailing break Excel appends, not an empty row the user copied.
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// A deliberately small scan for the one shape that matters — the table Excel and
    /// this grid put on the clipboard — not an HTML parser. A cell's value is its
    /// <c>x:num</c> attribute when Excel supplied one (the raw number, full precision),
    /// otherwise its text content with tags stripped and entities decoded.
    /// </summary>
    private static List<List<string>>? ParseHtmlTable(string html)
    {
        var tableStart = html.IndexOf("<table", StringComparison.OrdinalIgnoreCase);
        if (tableStart < 0)
            return null;
        var tableEnd = html.IndexOf("</table", tableStart, StringComparison.OrdinalIgnoreCase);
        if (tableEnd < 0)
            tableEnd = html.Length;

        var rows = new List<List<string>>();
        var position = tableStart;
        while (true)
        {
            var rowStart = IndexOfTag(html, "tr", position, tableEnd);
            if (rowStart < 0)
                break;
            var rowEnd = html.IndexOf("</tr", rowStart, StringComparison.OrdinalIgnoreCase);
            if (rowEnd < 0 || rowEnd > tableEnd)
                rowEnd = tableEnd;

            var cells = new List<string>();
            var cellPosition = rowStart;
            while (true)
            {
                var cellStart = IndexOfTag(html, "td", cellPosition, rowEnd);
                if (cellStart < 0)
                    cellStart = IndexOfTag(html, "th", cellPosition, rowEnd);
                if (cellStart < 0)
                    break;
                var openEnd = html.IndexOf('>', cellStart);
                if (openEnd < 0 || openEnd > rowEnd)
                    break;
                var closeStart = html.IndexOf("</t", openEnd, StringComparison.OrdinalIgnoreCase);
                if (closeStart < 0 || closeStart > rowEnd)
                    closeStart = rowEnd;

                var attributes = html[cellStart..openEnd];
                var content = html[(openEnd + 1)..closeStart];
                cells.Add(NumberAttribute(attributes) ?? DecodeText(content));
                cellPosition = closeStart + 1;
            }
            if (cells.Count > 0)
                rows.Add(cells);
            position = rowEnd + 1;
        }
        return rows;
    }

    /// <summary>Finds <c>&lt;tag</c> as a real tag open — followed by whitespace, '>',
    /// or '/'—between <paramref name="from"/> and <paramref name="until"/>.</summary>
    private static int IndexOfTag(string html, string tag, int from, int until)
    {
        var needle = "<" + tag;
        var i = from;
        while (i >= 0 && i < until)
        {
            i = html.IndexOf(needle, i, StringComparison.OrdinalIgnoreCase);
            if (i < 0 || i >= until)
                return -1;
            var after = i + needle.Length;
            if (after < html.Length && (char.IsWhiteSpace(html[after]) || html[after] is '>' or '/'))
                return i;
            i = after;
        }
        return -1;
    }

    /// <summary>Excel's <c>x:num="…"</c> — the raw number behind the displayed text.
    /// The bare form (<c>x:num</c> with no value) says only "this is a number", so the
    /// text content stands.</summary>
    private static string? NumberAttribute(string attributes)
    {
        var i = attributes.IndexOf("x:num=\"", StringComparison.OrdinalIgnoreCase);
        if (i < 0)
            return null;
        var start = i + "x:num=\"".Length;
        var end = attributes.IndexOf('"', start);
        return end < 0 ? null : attributes[start..end];
    }

    private static string DecodeText(string content)
    {
        var text = new StringBuilder(content.Length);
        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '<')
            {
                var end = content.IndexOf('>', i);
                if (end < 0)
                    break;
                // A break inside a cell is part of its value; every other tag only wraps.
                var tag = content[(i + 1)..end].TrimStart('/').TrimEnd('/').Trim();
                if (tag.StartsWith("br", StringComparison.OrdinalIgnoreCase))
                    text.Append('\n');
                i = end;
                continue;
            }
            if (c == '&')
            {
                var end = content.IndexOf(';', i);
                if (end > i && end - i <= 8)
                {
                    var entity = content[(i + 1)..end];
                    var decoded = entity switch
                    {
                        "amp" => "&",
                        "lt" => "<",
                        "gt" => ">",
                        "quot" => "\"",
                        "apos" or "#39" => "'",
                        // Kept distinct from a plain space until after the trim below:
                        // &nbsp; is how Excel's HTML flavour preserves a value's own
                        // leading/trailing spaces, so trimming it would silently alter
                        // the copied value (ADR-0005's principle).
                        "nbsp" or "#160" => "\u00A0",
                        _ => null,
                    };
                    if (decoded is not null)
                    {
                        text.Append(decoded);
                        i = end;
                        continue;
                    }
                }
            }
            text.Append(c);
        }
        // Trim only the markup's own pretty-printing whitespace; a no-break space is
        // value, not markup, and becomes an ordinary space only once it is safe.
        return text.ToString().Trim(' ', '\t', '\n', '\r').Replace('\u00A0', ' ');
    }

    private static IReadOnlyList<IReadOnlyList<string>> Rectangular(List<List<string>> rows)
    {
        var width = 0;
        foreach (var row in rows)
            width = Math.Max(width, row.Count);
        var block = new IReadOnlyList<string>[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count < width)
            {
                var padded = new string[width];
                row.CopyTo(padded);
                for (var c = row.Count; c < width; c++)
                    padded[c] = "";
                block[r] = padded;
            }
            else
            {
                block[r] = row;
            }
        }
        return block;
    }
}
