using System.Globalization;
using System.Text;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// The order in which Excel's comparison operators, and XLOOKUP's approximate matches, put text
/// (ADR-0047). It is a collation, not code points: Excel puts <c>é</c> beside <c>e</c>, so
/// <c>="é"&gt;"z"</c> is FALSE, and case is ignored, so <c>="a"="A"</c> is TRUE.
/// </summary>
/// <remarks>
/// The engine's own, so that it gives the same answer on every platform and in the browser: an
/// operating system's collation differs by platform and version. Text is compared character by
/// character, first on each character's primary weight — punctuation, symbols and spaces before
/// digits, digits before letters, and a letter by its base letter, its accents removed and its
/// case ignored — and only where every primary weight is equal, on the accents: an unaccented
/// letter before an accented one. What reproduces Excel is pinned by the case corpus: <c>!</c>
/// before <c>a</c>, and <c>é</c> between <c>e</c> and <c>z</c>, observed. The order among
/// punctuation marks (here, by code point), letters with no decomposition (<c>ß</c>, <c>æ</c>,
/// <c>ø</c>, after <c>z</c> here), and whether Excel ignores a hyphen or an apostrophe as Windows'
/// word sort does (not here), are <c>uncertain</c> cases until Excel is asked.
/// </remarks>
internal static class TextOrder
{
    public static int Compare(string left, string right)
    {
        var accents = 0;
        var length = Math.Min(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            var a = Weigh(left[i]);
            var b = Weigh(right[i]);
            var primary = a.Class != b.Class ? a.Class.CompareTo(b.Class) : a.Base.CompareTo(b.Base);
            if (primary != 0) return primary;
            if (accents == 0) accents = string.CompareOrdinal(a.Accents, b.Accents);
        }
        if (left.Length != right.Length) return left.Length.CompareTo(right.Length);
        return Math.Sign(accents);
    }

    /// <summary>A character's primary weight — its class and its base character — and its accents.</summary>
    private static (int Class, char Base, string Accents) Weigh(char c)
    {
        if (c < 128) return (ClassOf(c), char.ToUpperInvariant(c), "");
        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
        var baseCharacter = decomposed[0];
        var accents = decomposed.Length > 1 ? decomposed[1..] : "";
        return (ClassOf(baseCharacter), char.ToUpperInvariant(baseCharacter), accents);
    }

    private static int ClassOf(char c) => char.GetUnicodeCategory(c) switch
    {
        UnicodeCategory.DecimalDigitNumber => 1,
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter => 2,
        _ => 0,
    };
}
