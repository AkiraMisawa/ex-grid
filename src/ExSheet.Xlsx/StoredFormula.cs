using System.Text;
using System.Text.RegularExpressions;

namespace ExSheet.Xlsx;

/// <summary>Why a Formula as the file stores it is refused before the engine reads it.</summary>
internal enum StoredFormulaProblem
{
    None,

    /// <summary>A Reference into another workbook: <c>[1]Sheet1!A1</c>, <c>'[1]Sheet 1'!A1</c>, <c>[1]!Rate</c>.</summary>
    OtherWorkbook,

    /// <summary>A structured reference into an Excel Table: <c>Table1[Amount]</c>, <c>Table1[[#This Row],[Amount]]</c>.</summary>
    ExcelTable,

    /// <summary>Excel's implicit intersection operator, <c>@</c>, stored as <c>_xlfn.SINGLE</c>.</summary>
    ImplicitIntersection,

    /// <summary>A Spill Range reference (<c>_xlfn.ANCHORARRAY</c>) whose argument is not one cell.</summary>
    SpillRange,
}

/// <summary>
/// A Formula's text as an <c>.xlsx</c> stores it, read into the text ExSheet's engine reads (ADR-0330 §4): Excel's
/// stored prefixes taken off — <c>_xlfn.</c> before a function newer than Excel 2007, <c>_xlws.</c> before a worksheet-only
/// one, and <c>_xlpm.</c> before a name a <c>LET</c> binds — and <c>_xlfn.ANCHORARRAY(A1)</c> written as the Spill Range
/// reference <c>A1#</c> it stands for (ADR-0125). What the engine cannot reach is refused here, by name, before it would
/// read as a plausible <c>#REF!</c> or <c>#NAME?</c>: another workbook, an Excel Table, and the implicit intersection
/// operator. Text in quotes, and a Sheet's name in quotes, are never touched.
/// </summary>
internal static partial class StoredFormula
{
    private static readonly string[] Prefixes = ["_xlfn.", "_xlws.", "_xlpm."];

    /// <summary>The Formula ExSheet reads, with its <c>=</c>, or the problem that refuses it.</summary>
    public static string Read(string stored, out StoredFormulaProblem problem)
    {
        problem = Refusal(stored);
        if (problem != StoredFormulaProblem.None) return "=" + stored;
        var text = Strip(stored);
        text = AnchorArrays(text, out var anchored);
        if (!anchored) problem = StoredFormulaProblem.SpillRange;
        return "=" + text;
    }

    /// <summary>The text with every stored prefix taken off, outside quotes.</summary>
    public static string Strip(string stored)
    {
        var text = new StringBuilder(stored.Length);
        var i = 0;
        while (i < stored.Length)
        {
            var c = stored[i];
            if (c is '"' or '\'')
            {
                var end = SkipQuoted(stored, i);
                text.Append(stored, i, end - i);
                i = end;
                continue;
            }
            if (c == '_' && (text.Length == 0 || !IsNameChar(text[^1])) && Prefixes.FirstOrDefault(p => string.Compare(stored, i, p, 0, p.Length, StringComparison.OrdinalIgnoreCase) == 0) is { } prefix)
            {
                i += prefix.Length;
                continue;
            }
            text.Append(c);
            i++;
        }
        return text.ToString();
    }

    /// <summary>Why the stored text is refused before it is read, outside quotes; <see cref="StoredFormulaProblem.None"/> when it is not.</summary>
    private static StoredFormulaProblem Refusal(string stored)
    {
        for (var i = 0; i < stored.Length; i++)
        {
            var c = stored[i];
            if (c == '"')
            {
                i = SkipQuoted(stored, i) - 1;
                continue;
            }
            if (c == '\'')
            {
                // A Sheet's name cannot hold '[' (Excel's rule, and ExSheet's): one that starts with it names a workbook.
                if (i + 1 < stored.Length && stored[i + 1] == '[') return StoredFormulaProblem.OtherWorkbook;
                i = SkipQuoted(stored, i) - 1;
                continue;
            }
            if (c == '[')
            {
                var before = i > 0 ? stored[i - 1] : '\0';
                if (IsNameChar(before)) return StoredFormulaProblem.ExcelTable;
                return WorkbookIndex().IsMatch(stored.AsSpan(i)) ? StoredFormulaProblem.OtherWorkbook : StoredFormulaProblem.ExcelTable;
            }
            if (c == '@') return StoredFormulaProblem.ImplicitIntersection;
            if (c == '_' && (i == 0 || !IsNameChar(stored[i - 1])) && SinglePattern().IsMatch(stored.AsSpan(i))) return StoredFormulaProblem.ImplicitIntersection;
        }
        return StoredFormulaProblem.None;
    }

    /// <summary>
    /// Each <c>ANCHORARRAY(ref)</c> written as <c>ref#</c>; <paramref name="read"/> is false when one names anything but
    /// one cell, which the engine's grammar does not spell.
    /// </summary>
    private static string AnchorArrays(string text, out bool read)
    {
        read = true;
        const string Name = "ANCHORARRAY(";
        StringBuilder? written = null;
        var at = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                i = SkipQuoted(text, i) - 1;
                continue;
            }
            if (c is not ('A' or 'a') || (i > 0 && IsNameChar(text[i - 1])) || string.Compare(text, i, Name, 0, Name.Length, StringComparison.OrdinalIgnoreCase) != 0) continue;
            var open = i + Name.Length;
            var close = open;
            while (close < text.Length && text[close] != ')')
            {
                close = text[close] is '"' or '\'' ? SkipQuoted(text, close) : close + 1;
            }
            var inner = close < text.Length ? text[open..close].Trim() : "";
            if (!CellPattern().IsMatch(inner))
            {
                read = false;
                return text;
            }
            written ??= new StringBuilder(text.Length);
            written.Append(text, at, i - at).Append(inner).Append('#');
            at = close + 1;
            i = close;
        }
        if (written is null) return text;
        written.Append(text, at, text.Length - at);
        return written.ToString();
    }

    /// <summary>Where the quoted run that starts at <paramref name="start"/> ends: past its closing quote, a doubled quote read as one.</summary>
    private static int SkipQuoted(string text, int start)
    {
        var quote = text[start];
        var i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == quote)
            {
                if (i + 1 < text.Length && text[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return text.Length;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '\\' or '?';

    [GeneratedRegex(@"\G\[[0-9]+\]")]
    private static partial Regex WorkbookIndex();

    [GeneratedRegex(@"\G_xlfn\.SINGLE\(", RegexOptions.IgnoreCase)]
    private static partial Regex SinglePattern();

    /// <summary>One cell, as a Spill Range reference names its anchor: <c>A1</c>, <c>$B$2</c>, <c>Sheet2!A1</c>, <c>'My Sheet'!A1</c>.</summary>
    [GeneratedRegex(@"^(?:'(?:[^']|'')+'!|[A-Za-z0-9_.\\]+!)?\$?[A-Za-z]{1,3}\$?[0-9]+\z")]
    private static partial Regex CellPattern();
}
