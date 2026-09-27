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
/// <param name="Fields">What <see cref="ClipboardParse.ParseBlock"/> reads from the two flavours written.</param>
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
internal static class SheetClipboard
{
    private const string EngineTableOpen = "<table>";

    /// <summary>
    /// The answer to <paramref name="request"/>, and the copy to recognise on a later paste, or
    /// null when this copy carries no Entries.
    /// <list type="bullet">
    /// <item>A copy reaching a <c>#GETTING_DATA</c> cell is refused with the engine's sentence
    /// (ADR-0049), and the clipboard is left alone.</item>
    /// <item>One range is the engine's <see cref="Sheet.Copy"/>: the Values as shown for
    /// <c>text/plain</c>, the unformatted Values for <c>text/html</c>, whose table carries
    /// ExGrid's <see cref="ClipboardData.InvariantMarker"/> so that a paste reads its fields as
    /// invariant (ADR-0050 item 10). Its Entries are kept for a paste back into this Sheet.</item>
    /// <item>Several ranges combined into one block, or a copy with the headers, carry the same
    /// Values cell by cell, and no Entries: a block of several ranges was never one place its
    /// References were relative to.</item>
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
            var html = Marked(copy.Html!);
            var fields = ClipboardParse.ParseBlock(html, copy.Text)?.Values
                ?? throw new InvalidOperationException("The engine's copy of a range read back as nothing.");
            return (GridCopyAnswer.Write(copy.Text!, html), new SheetOwnCopy(copy.Block!, fields));
        }

        var (text, combined) = ClipboardData.Assemble(
            plan,
            (row, column) => ShownText(sheet, new CellAddress(row, column)),
            (row, column) => sheet.GetValue(new CellAddress(row, column))?.ToString() ?? "",
            request.WithHeaders ? CellAddress.ColumnName : null);
        return (GridCopyAnswer.Write(text, combined), null);
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
                "The engine's copy no longer opens with a bare <table>, so ExSheet cannot mark it as invariant; a paste would read its numbers as shown text.");
        }
        return "<table " + ClipboardData.InvariantMarker + ">" + html[EngineTableOpen.Length..];
    }

    private static CellRange RangeOf(SelectionRange range) => SheetFormulaAids.RangeOf(range);
}
