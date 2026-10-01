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
/// operating system's collation differs by platform and version. Text is compared first on each
/// character's primary weight — punctuation, symbols and spaces before digits, digits before
/// letters, and a letter by its base letter, its accents removed and its case ignored — with a
/// hyphen and an apostrophe passed over and <c>ß</c> read as <c>ss</c>; where every primary weight
/// is equal, on the accents (an unaccented letter before an accented one); and where those are
/// equal too, on the hyphens and apostrophes passed over (the text with fewer first). What
/// reproduces Excel is pinned by the case corpus: <c>!</c> before <c>a</c>, <c>é</c> between
/// <c>e</c> and <c>z</c>, <c>co-op</c> not before <c>coop</c>, and <c>ß</c> not after <c>z</c>
/// (ADR-0047, second run); <c>it's</c> not before <c>its</c>, and <c>ß</c> equal to <c>ss</c>
/// (third run). The order among punctuation marks (here, by code point), letters with no
/// decomposition other than <c>ß</c> (<c>æ</c>, <c>ø</c>, after <c>z</c> here), and how text
/// differing only by an apostrophe orders, are <c>uncertain</c> cases until Excel is asked.
/// </remarks>
internal static class TextOrder
{
    public static int Compare(string left, string right)
    {
        var a = Weigh(left, out var leftHyphens);
        var b = Weigh(right, out var rightHyphens);
        var length = Math.Min(a.Count, b.Count);
        for (var i = 0; i < length; i++)
        {
            var primary = a[i].Class != b[i].Class ? a[i].Class.CompareTo(b[i].Class) : a[i].Base.CompareTo(b[i].Base);
            if (primary != 0) return primary;
        }
        if (a.Count != b.Count) return a.Count.CompareTo(b.Count);
        for (var i = 0; i < length; i++)
        {
            var accents = string.CompareOrdinal(a[i].Accents, b[i].Accents);
            if (accents != 0) return Math.Sign(accents);
        }
        return leftHyphens.CompareTo(rightHyphens);
    }

    /// <summary>
    /// A text two texts share exactly when <see cref="Compare"/> gives 0 for them: their primary
    /// weights, their accents, and how many hyphens and apostrophes were passed over. A set of keys
    /// finds the texts <c>XLOOKUP</c>'s exact match cannot tell apart without comparing every pair
    /// (ADR-0049, 2026-09-30).
    /// </summary>
    public static string EqualityKey(string text)
    {
        var weights = Weigh(text, out var hyphens);
        var key = new StringBuilder(text.Length * 3 + 4);
        // Each weight is its class, its base character and its accents behind their length, so no
        // two lists of weights are written alike.
        foreach (var (@class, baseCharacter, accents) in weights)
        {
            key.Append((char)('0' + @class)).Append(baseCharacter).Append((char)accents.Length).Append(accents);
        }
        return key.Append('|').Append(hyphens).ToString();
    }

    /// <summary>The primary weights and accents of the text's characters, a hyphen or an apostrophe passed over and counted, <c>ß</c> as <c>ss</c>.</summary>
    private static List<(int Class, char Base, string Accents)> Weigh(string text, out int hyphens)
    {
        hyphens = 0;
        var weights = new List<(int, char, string)>(text.Length);
        foreach (var c in text)
        {
            if (c is '-' or '\'')
            {
                hyphens++;
                continue;
            }
            if (c is 'ß' or 'ẞ')
            {
                weights.Add((2, 'S', ""));
                weights.Add((2, 'S', ""));
                continue;
            }
            weights.Add(Weigh(c));
        }
        return weights;
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
