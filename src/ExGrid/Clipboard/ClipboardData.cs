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
    /// <param name="plan">The approved copy (ADR-0005/0011).</param>
    /// <param name="displayAt">The displayed text of (row, column) — what the cell
    /// paints before the overflow decision, never <c>####</c> (ADR-0016).</param>
    /// <param name="rawAt">The raw value of (row, column), unformatted and locale-free.</param>
    public static (string Text, string Html) Assemble(
        CopyPlan plan,
        Func<int, int, string> displayAt,
        Func<int, int, string> rawAt)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(displayAt);
        ArgumentNullException.ThrowIfNull(rawAt);

        var text = new StringBuilder();
        var html = new StringBuilder("<table>");

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
