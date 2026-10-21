using System.Globalization;
using System.Text;

namespace ExGrid.Data.Xlsx;

/// <summary>
/// The escaping ECMA-376 gives text (<c>ST_Xstring</c>, Part 1, §22.9.2.19): a character XML cannot carry is written
/// <c>_xHHHH_</c>, and a literal <c>_x</c> that would read as one is written <c>_x005F_</c> first. Excel writes a
/// carriage return in a cell as <c>_x000D_</c>.
/// </summary>
internal static class XString
{
    public static string Decode(string text)
    {
        var at = text.IndexOf("_x", StringComparison.Ordinal);
        if (at < 0)
            return text;
        var builder = new StringBuilder(text.Length);
        var start = 0;
        while (at >= 0)
        {
            if (at + 7 <= text.Length && text[at + 6] == '_'
                && int.TryParse(text.AsSpan(at + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
            {
                builder.Append(text, start, at - start).Append((char)code);
                start = at + 7;
                at = text.IndexOf("_x", start, StringComparison.Ordinal);
            }
            else
            {
                at = text.IndexOf("_x", at + 2, StringComparison.Ordinal);
            }
        }
        return builder.Append(text, start, text.Length - start).ToString();
    }

    public static string Encode(string text)
    {
        var needs = false;
        foreach (var c in text)
        {
            if (c == '_' || Escaped(c))
            {
                needs = true;
                break;
            }
        }
        if (!needs)
            return text;
        var builder = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (Escaped(c))
                builder.Append("_x").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture)).Append('_');
            else if (c == '_' && LooksEscaped(text, i))
                builder.Append("_x005F_");
            else
                builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>A character XML 1.0 cannot carry, or a carriage return, which XML would turn into a line feed.</summary>
    private static bool Escaped(char c) => (c < 0x20 && c != '\t' && c != '\n') || c == '￾' || c == '￿';

    private static bool LooksEscaped(string text, int at)
        => at + 7 <= text.Length && text[at + 1] == 'x' && text[at + 6] == '_'
           && int.TryParse(text.AsSpan(at + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
}
