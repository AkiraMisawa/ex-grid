using System.Globalization;
using System.Text.RegularExpressions;
using ExGrid.Clipboard;
using ExGrid.Selection;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// The copy ExSheet last put on the clipboard (ADR-0048, ADR-0050 item 9): the Entries it copied,
/// and the fields the grid's paste parse reads back from what was written. A later paste whose
/// fields equal these, field for field, is taken to be this copy, and its Entries are pasted. A
/// browser clipboard names no owner, so equal content is the strongest evidence a page has.
/// </summary>
/// <param name="Block">The Entries, formats and alignment copied, and where from.</param>
/// <param name="Fields">What <see cref="ClipboardParse.Parse"/> reads from the two flavours written.</param>
internal sealed record SheetOwnCopy(SheetBlock Block, IReadOnlyList<IReadOnlyList<string>> Fields)
{
    /// <summary>Whether a paste's fields are this copy's, field for field.</summary>
    internal bool IsPastedAs(IReadOnlyList<IReadOnlyList<string>> pasted)
    {
        if (pasted.Count != Fields.Count) return false;
        for (var r = 0; r < pasted.Count; r++)
        {
            if (pasted[r].Count != Fields[r].Count) return false;
            for (var c = 0; c < pasted[r].Count; c++)
            {
                if (!string.Equals(pasted[r][c], Fields[r][c], StringComparison.Ordinal)) return false;
            }
        }
        return true;
    }
}

/// <summary>
/// ExSheet's answer to a copy (ADR-0050 item 9, ADR-0048, ADR-0049): the engine's copy of the
/// range, written exactly as the engine gives it, or the engine's refusal in its own words.
/// </summary>
internal static partial class SheetClipboard
{
    private const string EngineTableOpen = "<table ";

    /// <summary>
    /// The answer to <paramref name="request"/>, and the copy to recognise on a later paste, or
    /// null when this copy carries no Entries.
    /// <list type="bullet">
    /// <item>A copy reaching a <c>#GETTING_DATA</c> cell is refused with the engine's sentence
    /// (ADR-0049), and the clipboard is left alone.</item>
    /// <item>One range is the engine's <see cref="Sheet.Copy"/>: the Values as shown for
    /// <c>text/plain</c>, the unformatted Values for <c>text/html</c> in Excel's markup — a
    /// number's Value in <c>x:num</c>, a format in <c>mso-number-format</c>, so a copy to Excel
    /// carries formats (ADR-0048) — whose table carries
    /// ExGrid's <see cref="ClipboardData.InvariantMarker"/> so that a paste reads its fields as
    /// invariant (ADR-0050 item 10). Its Entries are kept for a paste back into this Sheet.</item>
    /// <item>Several ranges combined into one block, or a copy with the headers, carry the same
    /// Values cell by cell, and no Entries: a block of several ranges was never one place its
    /// References were relative to.</item>
    /// <item>Either way, a refused cell (<see cref="Value.IsRefused"/>) is written as it shows,
    /// <c>#VALUE!</c> or <c>#NAME?</c>, in both flavours, and its cell of the HTML table carries ExSheet's mark
    /// (<see cref="RefusedMarkFor"/> in <see cref="ClipboardData.MarkAttribute"/>), which another
    /// program ignores. A paste into a Sheet that reads the mark is refused by name rather than typed:
    /// as a value, the refusal would become an Error Value a Formula can catch (ADR-0047; 2026-10-12,
    /// decided with the user: a refusal is never caught, anywhere).</item>
    /// </list>
    /// </summary>
    internal static (GridCopyAnswer Answer, SheetOwnCopy? Own) Answer(Sheet sheet, GridCopyRequest request)
    {
        var plan = request.Plan;
        var copies = new List<SheetCopy>(plan.Segments.Count);
        foreach (var segment in plan.Segments)
        {
            var copy = sheet.Copy(RangeOf(segment));
            if (copy.Refusal is { } refusal) return (GridCopyAnswer.Refuse(SheetWords.Refused(refusal)), null);
            copies.Add(copy);
        }

        if (copies.Count == 1 && !request.WithHeaders)
        {
            var copy = copies[0];
            var range = RangeOf(plan.Segments[0]);
            var width = range.Last.Column - range.First.Column + 1;
            // The engine writes its table's cells row by row over the range.
            var html = MarkRefused(sheet, Marked(copy.Html!), checked((int)range.CellCount),
                i => new CellAddress(range.First.Row + i / width, range.First.Column + i % width));
            var fields = ClipboardParse.Parse(html, copy.Text)
                ?? throw new InvalidOperationException("The engine's copy of a range read back as nothing.");
            return (GridCopyAnswer.Write(copy.Text!, html), new SheetOwnCopy(copy.Block!, fields));
        }

        // The cells in the order the table writes them, header cells apart (they are <th>).
        var written = new List<CellAddress>();
        var (text, combined) = ClipboardData.Assemble(
            plan,
            (row, column) => ShownText(sheet, new CellAddress(row, column)),
            (row, column) =>
            {
                var address = new CellAddress(row, column);
                written.Add(address);
                return sheet.GetValue(address)?.ToString() ?? "";
            },
            request.WithHeaders ? CellAddress.ColumnName : null);
        return (GridCopyAnswer.Write(text, MarkRefused(sheet, combined, written.Count, i => written[i])), null);
    }

    private const string RefusedMarkPrefix = "exsheet-refused:";

    /// <summary>
    /// ExSheet's mark on a refused cell of its clipboard table (<see cref="ClipboardData.MarkAttribute"/>):
    /// the cell it was copied from, so that the refusal of a paste can name it.
    /// </summary>
    internal static string RefusedMarkFor(CellAddress copiedFrom) => RefusedMarkPrefix + copiedFrom;

    /// <summary>The cell a pasted field was copied from when ExSheet marked it refused
    /// (<see cref="RefusedMarkFor"/>), or null for any other field.</summary>
    internal static CellAddress? RefusedIn(string? mark) =>
        mark is not null && mark.StartsWith(RefusedMarkPrefix, StringComparison.Ordinal)
        && CellAddress.TryParse(mark[RefusedMarkPrefix.Length..], out var address)
            ? address
            : null;

    /// <summary>
    /// <paramref name="html"/> with ExSheet's mark on the <c>&lt;td&gt;</c> of every refused cell:
    /// <paramref name="cellAt"/> names the table's <paramref name="count"/> cells in the order it writes them. Every cell of the
    /// table is a <c>&lt;td</c> and every other <c>&lt;</c> in it is escaped, so the n-th <c>&lt;td</c> is
    /// the n-th cell; a table that holds another count is a defect said by name, never a mark on the
    /// wrong cell.
    /// </summary>
    private static string MarkRefused(Sheet sheet, string html, int count, Func<int, CellAddress> cellAt)
    {
        var marked = new System.Text.StringBuilder(html.Length);
        var from = 0;
        var cell = 0;
        while (true)
        {
            var at = html.IndexOf("<td", from, StringComparison.Ordinal);
            if (at < 0) break;
            if (cell >= count)
                throw new InvalidOperationException("The Sheet's copy holds more cells than it was written from, so ExSheet cannot mark its refused cells.");
            var open = at + "<td".Length;
            marked.Append(html, from, open - from);
            if (sheet.GetValue(cellAt(cell)) is { IsRefused: true })
                marked.Append(' ').Append(ClipboardData.MarkAttribute).Append("=\"").Append(RefusedMarkFor(cellAt(cell))).Append('"');
            from = open;
            cell++;
        }
        if (cell != count)
            throw new InvalidOperationException("The Sheet's copy holds fewer cells than it was written from, so ExSheet cannot mark its refused cells.");
        return marked.Append(html, from, html.Length - from).ToString();
    }

    /// <summary>
    /// A cell's text as the engine's copy writes it: as the cell shows it at no particular width,
    /// and a Value its format cannot show as the Value itself, never <c>####</c> (ADR-0016).
    /// </summary>
    private static string ShownText(Sheet sheet, CellAddress address)
    {
        var display = sheet.GetDisplay(address);
        return display.CannotShow ? sheet.GetValue(address)?.ToString() ?? "" : display.Text;
    }

    /// <summary>
    /// The engine's table, marked as ExGrid marks its own unformatted HTML (ADR-0050, fourth
    /// round), so the grid's paste parse reads every field of it as invariant.
    /// </summary>
    private static string Marked(string html)
    {
        if (!html.StartsWith(EngineTableOpen, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The engine's copy no longer opens with <table and its attributes, so ExSheet cannot mark it as invariant; a paste would read its numbers as shown text.");
        }
        return EngineTableOpen + ClipboardData.InvariantMarker + " " + html[EngineTableOpen.Length..];
    }

    /// <summary>
    /// The number an invariant pasted field is (ADR-0048, ADR-0050 item 10): Excel's
    /// <c>x:num</c>, or ExGrid's and ExSheet's own unformatted HTML, holding a finite number. It is
    /// pasted as that exact double, every digit it holds — not typed, which would cut it to the
    /// fifteen significant digits a typed number keeps — and so reads the same under every
    /// culture: <c>1234.5</c> from Excel stays 1234.5 under <c>de-DE</c>. Null for any other field,
    /// which <see cref="TypedFor"/> types.
    /// </summary>
    internal static double? ExactNumberFor(string field, PasteFieldOrigin origin) =>
        origin == PasteFieldOrigin.Invariant
        && double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
        && double.IsFinite(number)
            ? number
            : null;

    /// <summary>
    /// Whether a pasted field is a run of <c>#</c> in shown text: Excel showing a value too wide
    /// for its column, with the value itself missing from the clipboard (observed,
    /// verification/2026-09-27-windows-excel, behaviours item 18). Such a paste is refused by name,
    /// not taken as the text <c>########</c> (ADR-0048). An invariant field — ExGrid's or
    /// ExSheet's own unformatted table — holds the Value itself, so a run of <c>#</c> there is text.
    /// </summary>
    internal static bool IsTooNarrowToShow(string field, PasteFieldOrigin origin) =>
        origin == PasteFieldOrigin.ShownText && field.Length > 0 && field.AsSpan().IndexOfAnyExcept('#') < 0;

    /// <summary>
    /// What a pasted field that is not an exact number (<see cref="ExactNumberFor"/>) is typed as
    /// under <paramref name="culture"/> (ADR-0048, ADR-0050 item 10). Shown text is typed as it
    /// is. An invariant field is its value as it is: an ISO date as the date; <c>TRUE</c> and
    /// <c>FALSE</c> as booleans. Any other invariant field is text or an Error Value, and is typed
    /// as it is. Null when an invariant date would not read back under the culture as a date: the
    /// paste is refused rather than write a different value.
    /// </summary>
    internal static string? TypedFor(string field, PasteFieldOrigin origin, CultureInfo culture)
    {
        if (origin != PasteFieldOrigin.Invariant) return field;
        if (field.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || field.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return field;
        if (IsoDate().IsMatch(field))
        {
            // The engine reads a year-first date, and a time after a space, in every culture.
            var typed = field.Replace('T', ' ');
            return NumberOf(typed, culture) is not null ? typed : null;
        }
        return field;
    }

    private static double? NumberOf(string typed, CultureInfo culture) =>
        Entry.Parse(typed, culture)?.Constant is { Kind: ValueKind.Number } value ? value.Number : null;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();

    private static CellRange RangeOf(SelectionRange range) => SheetFormulaAids.RangeOf(range);
}
