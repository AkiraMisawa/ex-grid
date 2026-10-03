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
        => ParseBlock(html, text)?.Values;

    /// <summary>
    /// The parsed block with where each field came from (ADR-0050, item 10): a field is
    /// <see cref="PasteFieldOrigin.Invariant"/> when Excel's <c>x:num="…"</c> supplied it, or
    /// when the whole table is ExGrid's own unformatted HTML (marked by
    /// <see cref="ClipboardData.InvariantMarker"/>); every other field — the text flavour, a
    /// cell's text content, Excel's bare <c>x:num</c> — is
    /// <see cref="PasteFieldOrigin.ShownText"/>, written in the source's culture. Null when
    /// neither flavour holds anything, as <see cref="Parse"/>.
    /// </summary>
    public static ClipboardBlock? ParseBlock(string? html, string? text)
    {
        if (!string.IsNullOrEmpty(html) && ParseHtmlTable(html) is { Count: > 0 } table)
            return Rectangular(table, fromTable: true);
        if (!string.IsNullOrEmpty(text) && ParseTsv(text) is { Count: > 0 } block)
            return Rectangular(Shown(block), fromTable: false);
        return null;
    }

    private static List<List<(string Value, PasteFieldOrigin Origin)>> Shown(List<List<string>> rows)
    {
        var fields = new List<List<(string, PasteFieldOrigin)>>(rows.Count);
        foreach (var row in rows)
            fields.Add(row.ConvertAll(value => (value, PasteFieldOrigin.ShownText)));
        return fields;
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
    private static List<List<(string Value, PasteFieldOrigin Origin)>>? ParseHtmlTable(string html)
    {
        var tableStart = html.IndexOf("<table", StringComparison.OrdinalIgnoreCase);
        if (tableStart < 0)
            return null;
        var tableEnd = html.IndexOf("</table", tableStart, StringComparison.OrdinalIgnoreCase);
        if (tableEnd < 0)
            tableEnd = html.Length;
        // ExGrid's own flavour is raw and locale-free throughout (ADR-0005), so every field of
        // it is invariant; it says so on the table, and nothing else on a clipboard does.
        var tableOpenEnd = html.IndexOf('>', tableStart);
        var ownInvariant = tableOpenEnd > 0 && tableOpenEnd < tableEnd &&
            html.AsSpan(tableStart, tableOpenEnd - tableStart).Contains(ClipboardData.InvariantMarker, StringComparison.OrdinalIgnoreCase);

        var rows = new List<List<(string, PasteFieldOrigin)>>();
        var position = tableStart;
        while (true)
        {
            var rowStart = IndexOfTag(html, "tr", position, tableEnd);
            if (rowStart < 0)
                break;
            var rowEnd = html.IndexOf("</tr", rowStart, StringComparison.OrdinalIgnoreCase);
            if (rowEnd < 0 || rowEnd > tableEnd)
                rowEnd = tableEnd;

            var cells = new List<(string, PasteFieldOrigin)>();
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
                cells.Add(NumberAttribute(attributes) is { } number
                    ? (number, PasteFieldOrigin.Invariant)
                    : (DecodeText(content), ownInvariant ? PasteFieldOrigin.Invariant : PasteFieldOrigin.ShownText));
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

    private static ClipboardBlock Rectangular(List<List<(string Value, PasteFieldOrigin Origin)>> rows, bool fromTable)
    {
        var width = 0;
        foreach (var row in rows)
            width = Math.Max(width, row.Count);
        var values = new IReadOnlyList<string>[rows.Count];
        var origins = new IReadOnlyList<PasteFieldOrigin>[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            // A short line is padded with empty cells, as Excel pads; an empty field reads the
            // same under any culture.
            var rowValues = new string[width];
            var rowOrigins = new PasteFieldOrigin[width];
            for (var c = 0; c < width; c++)
                (rowValues[c], rowOrigins[c]) = c < row.Count ? row[c] : ("", PasteFieldOrigin.ShownText);
            values[r] = rowValues;
            origins[r] = rowOrigins;
        }
        return new ClipboardBlock(values, origins, fromTable);
    }
}

/// <summary>Where one pasted field came from, which says how it is read (ADR-0050, item 10).</summary>
public enum PasteFieldOrigin
{
    /// <summary>Text as it was shown where it was copied: the plain-text flavour, or an HTML
    /// cell's text content. Its numbers are written in the source's culture — <c>1.234,5</c>
    /// under <c>de-DE</c> — so a Consumer reads it as typed under its own culture.</summary>
    ShownText = 0,

    /// <summary>An invariant number: Excel's <c>x:num</c> attribute, the raw value at full
    /// precision in invariant notation, or a field of ExGrid's own unformatted HTML, which is
    /// locale-free throughout (ADR-0005). A Consumer reads it under the invariant culture, never
    /// its own — otherwise <c>1234.5</c> from Excel would be misread under <c>de-DE</c>.</summary>
    Invariant,
}

/// <summary>
/// A parsed clipboard block (ADR-0005/0050): the values, rectangular by construction, and
/// beside each one where it came from.
/// </summary>
public sealed class ClipboardBlock
{
    internal ClipboardBlock(
        IReadOnlyList<IReadOnlyList<string>> values, IReadOnlyList<IReadOnlyList<PasteFieldOrigin>> origins,
        bool isFromTable)
    {
        Values = values;
        Origins = origins;
        IsFromTable = isFromTable;
    }

    /// <summary>
    /// Whether the block was read from a table in the <c>text/html</c> flavour — a copy from the
    /// grid, from ExSheet or from Excel — rather than from plain text alone. HTML that holds no
    /// table counts as plain text, since the block was read from the text flavour. It decides
    /// where a single value goes over a range (ADR-0014, amended 2026-09-29): from a table it
    /// fills the range; as plain text it goes into one cell alone.
    /// </summary>
    public bool IsFromTable { get; }

    /// <summary>The fields, row by row; every row is as wide as the widest.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Values { get; }

    /// <summary>Where each field of <see cref="Values"/> came from, at the same position.</summary>
    public IReadOnlyList<IReadOnlyList<PasteFieldOrigin>> Origins { get; }
}
