using System.Text;

namespace ExGrid.Clipboard;

/// <summary>
/// Assembles an approved copy into the two formats that go on the clipboard in one
/// operation (ADR-0005): the displayed format as TSV for <c>text/plain</c>, and the
/// raw, locale-free value as an HTML table for <c>text/html</c> — Excel prefers the
/// HTML flavour, so it receives precision and type intact while a text editor receives
/// what was on screen.
///
/// Pure: the values are read through the two providers, so the assembly is testable
/// without a component and works the same whether the rows came from the Window or
/// were gathered from the Consumer (ADR-0005's two routes).
/// </summary>
public static class ClipboardData
{
    /// <summary>
    /// The attribute on the table of ExGrid's HTML flavour saying that every field in it is
    /// raw and locale-free, so a paste reads each one as invariant (ADR-0050, item 10).
    /// Another program ignores an attribute it does not know.
    /// </summary>
    public const string InvariantMarker = "data-ex-grid=\"invariant\"";

    /// <summary>
    /// The attribute a cell of a table marked <see cref="InvariantMarker"/> may carry to tell its
    /// own Consumer more than the field's text says: <c>&lt;td data-ex-mark="…"&gt;</c>. A paste
    /// hands its value over beside the field (<see cref="GridPasteIntent.MarkFor"/>) and gives it no
    /// meaning of its own; another program ignores an attribute it does not know and reads the
    /// field. ExSheet marks a cell whose Formula it refused, so that a paste into a Sheet carries
    /// the refusal where another program reads <c>#VALUE!</c>.
    /// </summary>
    public const string MarkAttribute = "data-ex-mark";

    /// <summary>Both flavours of one approved copy: <c>Text</c> is the TSV of the
    /// displayed values, <c>Html</c> a table of the raw ones (ADR-0005).</summary>
    /// <param name="plan">The approved copy (ADR-0005/0011).</param>
    /// <param name="displayAt">The displayed text of (row, column) — what the cell
    /// paints before the overflow decision, never <c>####</c> (ADR-0016).</param>
    /// <param name="rawAt">The raw value of (row, column), unformatted and locale-free.</param>
    /// <param name="headerAt">The declared header of a column, or null for no header row
    /// (ADR-0005). It is the full declared text, never the truncated paint, by the rule
    /// that keeps <c>####</c> off the clipboard — and it goes into both flavours,
    /// because Excel reads the HTML one and that is the case headers exist for.</param>
    /// <param name="headerRawAt">A header's raw form, unformatted and locale-free, for the HTML
    /// flavour, as <paramref name="rawAt"/> is a cell's: null for a column whose header has none,
    /// which writes <paramref name="headerAt"/>'s text into both flavours as before. A header that
    /// shows a number rounded — a pivot's number column Item, <c>1.23457E+11</c> — carries the
    /// number in full here, so Excel reads it whole (ADR-0005's note of 2026-10-13). Read only
    /// when <paramref name="headerAt"/> is given.</param>
    public static (string Text, string Html) Assemble(
        CopyPlan plan,
        Func<int, int, string> displayAt,
        Func<int, int, string> rawAt,
        Func<int, string>? headerAt = null,
        Func<int, string?>? headerRawAt = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(displayAt);
        ArgumentNullException.ThrowIfNull(rawAt);

        var text = new StringBuilder();
        var html = new StringBuilder("<table ").Append(InvariantMarker).Append('>');

        if (headerAt is not null)
            AppendHeaderRow(text, html, plan, headerAt, headerRawAt);

        if (plan.Orientation == CopyOrientation.Vertical)
        {
            // Segments share a column span and stack in position order (ADR-0011).
            foreach (var segment in plan.Segments)
            {
                for (var row = segment.TopRow; row <= segment.BottomRow; row++)
                    AppendLine(text, html, row, segment.LeftColumn, segment.RightColumn, displayAt, rawAt);
            }
        }
        else
        {
            // Segments share a row span and concatenate left to right; every segment
            // has the same rows, so the first names them for all.
            var first = plan.Segments[0];
            for (var row = first.TopRow; row <= first.BottomRow; row++)
            {
                var cell = 0;
                html.Append("<tr>");
                foreach (var segment in plan.Segments)
                {
                    for (var column = segment.LeftColumn; column <= segment.RightColumn; column++)
                    {
                        if (cell++ > 0)
                            text.Append('\t');
                        AppendCell(text, html, row, column, displayAt, rawAt);
                    }
                }
                text.Append("\r\n");
                html.Append("</tr>");
            }
        }

        html.Append("</table>");
        return (text.ToString(), html.ToString());
    }

    /// <summary>One row above the block, whichever orientation the plan has. Vertical
    /// segments share a column span, so the first names them for all; horizontal ones
    /// concatenate, so the names follow the order the cells are emitted in (ADR-0005).
    /// A disjoint selection has a well-defined header row either way.</summary>
    private static void AppendHeaderRow(
        StringBuilder text, StringBuilder html, CopyPlan plan, Func<int, string> headerAt, Func<int, string?>? headerRawAt)
    {
        html.Append("<tr>");
        var cell = 0;
        if (plan.Orientation == CopyOrientation.Vertical)
        {
            var span = plan.Segments[0];
            for (var column = span.LeftColumn; column <= span.RightColumn; column++)
                AppendHeaderCell(text, html, headerAt(column), headerRawAt?.Invoke(column), cell++);
        }
        else
        {
            foreach (var segment in plan.Segments)
            {
                for (var column = segment.LeftColumn; column <= segment.RightColumn; column++)
                    AppendHeaderCell(text, html, headerAt(column), headerRawAt?.Invoke(column), cell++);
            }
        }
        text.Append("\r\n");
        html.Append("</tr>");
    }

    // The TSV takes the header as shown; the HTML its raw form where it has one, else the same text.
    private static void AppendHeaderCell(StringBuilder text, StringBuilder html, string header, string? raw, int index)
    {
        if (index > 0)
            text.Append('\t');
        text.Append(QuoteForTsv(header));
        html.Append("<th>").Append(EscapeHtml(raw ?? header)).Append("</th>");
    }

    private static void AppendLine(
        StringBuilder text, StringBuilder html,
        int row, int leftColumn, int rightColumn,
        Func<int, int, string> displayAt, Func<int, int, string> rawAt)
    {
        html.Append("<tr>");
        for (var column = leftColumn; column <= rightColumn; column++)
        {
            if (column > leftColumn)
                text.Append('\t');
            AppendCell(text, html, row, column, displayAt, rawAt);
        }
        text.Append("\r\n");
        html.Append("</tr>");
    }

    private static void AppendCell(
        StringBuilder text, StringBuilder html,
        int row, int column,
        Func<int, int, string> displayAt, Func<int, int, string> rawAt)
    {
        text.Append(QuoteForTsv(displayAt(row, column)));
        html.Append("<td>").Append(EscapeHtml(rawAt(row, column))).Append("</td>");
    }

    /// <summary>Excel's own TSV convention: a cell holding a tab, a line break or a
    /// quote is wrapped in quotes with inner quotes doubled — the receiving Excel reads
    /// it back as one cell. Copy never truncates, and it does not mangle either.</summary>
    private static string QuoteForTsv(string value)
    {
        if (value.IndexOfAny(['\t', '\n', '\r', '"']) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string EscapeHtml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
