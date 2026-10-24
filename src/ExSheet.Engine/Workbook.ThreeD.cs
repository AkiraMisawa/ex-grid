using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// What the Workbook's Sheet operations do to its 3D References (ADR-0331 §7), as Excel answered each
/// (exsheet-workbook ticket 12, verification/2026-10-24-windows-workbook-12). A 3D Reference reads the
/// Sheets between its two end Sheets in tab order, so a Sheet inserted, copied or moved between them is
/// read, and one moved out of them is not, without a word of it changing. Its text changes only where an
/// end does: an end moved past the other end, or deleted, hands its end to the Sheet next to it inward, and
/// a span left with one Sheet is written as a Reference to that Sheet. A rename renames the end it names.
/// </summary>
public sealed partial class Workbook
{
    /// <summary>
    /// A 3D Reference as the move of the Sheet named <paramref name="moved"/> leaves it: <paramref name="before"/>
    /// names the Sheets in their order before the move, and <paramref name="after"/> after it. An end that stays
    /// on its side of the other is kept; one moved past the other hands its end to the Sheet that stood next
    /// to it inward.
    /// </summary>
    internal static Reference Moved(Reference reference, IReadOnlyList<string> before, IReadOnlyList<string> after, string moved)
    {
        if (Ends(reference, before) is not var (a, b)) return reference;
        var first = before[a];
        var last = before[b];
        var (na, nb) = (IndexOfName(after, first), IndexOfName(after, last));
        if (na > nb)
        {
            if (Sheet.Names(first, moved)) first = before[a + 1];
            else last = before[b - 1];
        }
        return Spanning(reference, first, last);
    }

    /// <summary>
    /// A 3D Reference as the deletion of the Sheet named <paramref name="deleted"/> leaves it: an end it names
    /// is handed to the Sheet next to it inward, in <paramref name="before"/>'s order. <see langword="null"/>
    /// (<c>#REF!</c>) for one whose two ends were that Sheet, which a span of one never is once written.
    /// </summary>
    internal static Reference? Deleted(Reference reference, IReadOnlyList<string> before, string deleted)
    {
        if (Ends(reference, before) is not var (a, b)) return reference;
        if (a == b) return Sheet.Names(before[a], deleted) ? null : reference;
        var first = Sheet.Names(before[a], deleted) ? before[a + 1] : before[a];
        var last = Sheet.Names(before[b], deleted) ? before[b - 1] : before[b];
        return Spanning(reference, first, last);
    }

    /// <summary>
    /// A 3D Reference as a Formula is entered with it (ADR-0331 §7): its ends in tab order, each written in
    /// its Sheet's own name, and a span of one Sheet written as a Reference to that Sheet, as Excel writes
    /// <c>=sum(sheet3:sheet1!b2)</c> as <c>=SUM(Sheet1:Sheet3!B2)</c> and <c>Sheet2:Sheet2!B2</c> as
    /// <c>Sheet2!B2</c>. One naming a Sheet the Workbook lacks is left as typed: it is <c>#NAME?</c>.
    /// </summary>
    internal Reference InTabOrder(Reference reference)
    {
        var names = _sheets.Select(s => s.Name).ToList();
        return Ends(reference, names) is var (a, b) ? Spanning(reference, names[a], names[b]) : reference;
    }

    /// <summary>
    /// An Entry with every 3D Reference in it written as <see cref="InTabOrder(Reference)"/> writes one; the same instance
    /// when it holds none, or nothing changes.
    /// </summary>
    internal Entry InTabOrder(Entry entry) =>
        entry.Parsed is { } parsed && parsed.References.Any(r => r.IsThreeD)
            ? ReferenceRewriter.Rewrite(entry, r => r.IsThreeD ? InTabOrder(r) : r, q => q)
            : entry;

    /// <summary>Whether <paramref name="reference"/> is a 3D Reference whose span holds <paramref name="sheet"/>, as the Workbook stands.</summary>
    internal bool Spans(Reference reference, Sheet sheet) =>
        reference.IsThreeD && Span(reference.SheetName!, reference.LastSheetName!) is { } span && span.Contains(sheet);

    /// <summary>A 3D Reference's two ends by where they stand in <paramref name="names"/>, the first first; <see langword="null"/> for any other Reference, or one an end of which no Sheet has.</summary>
    private static (int First, int Last)? Ends(Reference reference, IReadOnlyList<string> names)
    {
        if (!reference.IsThreeD) return null;
        var a = IndexOfName(names, reference.SheetName!);
        var b = IndexOfName(names, reference.LastSheetName!);
        return a < 0 || b < 0 ? null : (Math.Min(a, b), Math.Max(a, b));
    }

    /// <summary>The Reference over the Sheets from <paramref name="first"/> to <paramref name="last"/>; one to that Sheet alone when they are one.</summary>
    private static Reference Spanning(Reference reference, string first, string last) =>
        Sheet.Names(first, last)
            ? reference with { SheetName = first, LastSheetName = null }
            : reference with { SheetName = first, LastSheetName = last };

    private static int IndexOfName(IReadOnlyList<string> names, string name)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
