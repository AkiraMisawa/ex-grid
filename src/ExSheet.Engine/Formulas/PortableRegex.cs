using System.Text;
using System.Text.RegularExpressions;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// XLOOKUP's <c>match_mode</c> 3, regular expressions (ADR-0047, "What the observation settled"
/// and "What the second observation settled"). Excel's expressions are PCRE2's, and .NET's are
/// close but not the same, so a pattern is accepted only when every construct in it means the
/// same in both, and is translated so that .NET's engine gives PCRE2's answer. Any other pattern
/// is refused (<c>#VALUE!</c>).
/// </summary>
/// <remarks>
/// <para>
/// The accepted constructs: literal characters; <c>.</c>; character classes <c>[…]</c> and
/// <c>[^…]</c> of literal characters, ranges between two literal characters, <c>\d \w \s</c> and
/// escaped metacharacters; <c>\d \w \s \b</c>; a Unicode general category <c>\p{L}</c>,
/// <c>\p{Lu}</c>, …; the anchors <c>^ $</c>; the quantifiers <c>* + ? {n} {n,} {n,m}</c> and
/// their lazy forms; groups <c>(…)</c> and alternation <c>|</c>; the lookaheads <c>(?=…)</c> and
/// <c>(?!…)</c>; the lookbehinds <c>(?&lt;=…)</c> and <c>(?&lt;!…)</c> of a bounded length (no
/// quantifier, group, lookaround or backreference inside), which PCRE2 and .NET read alike
/// (XLOOKUP-153, third run); a backreference <c>\1</c> to <c>\9</c> to a group already opened;
/// and a backslash before a metacharacter. Refused: every other <c>(?</c> construct
/// (non-capturing and named groups, inline options), a lookbehind whose length PCRE2 could not
/// bound, a quantified lookaround, possessive
/// quantifiers, a script or any property that is not a general category, <c>\P{…}</c>, POSIX
/// classes, <c>\D \W \S \B</c> and every other escape, a <c>{</c> or <c>}</c> that is not a whole
/// quantifier, an unescaped <c>]</c> outside a class, and characters outside the Basic
/// Multilingual Plane.
/// </para>
/// <para>
/// Excel compiles with Unicode properties (PCRE2's UCP), as it was observed to (XLOOKUP-097:
/// <c>\w</c> matches <c>é</c>): <c>\d</c> is <c>\p{Nd}</c>, <c>\w</c> is <c>\p{L}</c>,
/// <c>\p{N}</c>, <c>\p{Mn}</c>, <c>\p{Pc}</c> (PCRE2 10.43 on), <c>\s</c> is <c>\p{Z}</c> with
/// PCRE2's horizontal and vertical spaces, and <c>\b</c> is a boundary of that <c>\w</c>. PCRE2
/// in UTF mode reads a character outside the Basic Multilingual Plane as one; .NET reads it as
/// two UTF-16 units. <c>.</c> and a negated class are translated to match it as one; a pattern
/// using a Unicode class is refused against a value that holds such a character, where the two
/// would disagree. That the match is case-sensitive, and that a value matches when the pattern
/// matches any part of it (not only the whole), are this engine's reading of PCRE2's defaults.
/// </para>
/// </remarks>
internal static class PortableRegex
{
    private const string Word = "\\p{L}\\p{N}\\p{Mn}\\p{Pc}";
    private const string Digit = "\\p{Nd}";
    private const string Space = "\\t\\n\\v\\f\\r\\u0085\\u180E\\p{Z}";
    private const string Pair = "[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]";
    private const string Metacharacters = ".\\*+?()[]{}|^$/-";

    /// <summary>The Unicode general categories both engines name alike.</summary>
    private static readonly HashSet<string> Categories = new(StringComparer.Ordinal)
    {
        "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No",
        "P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "S", "Sm", "Sc", "Sk", "So",
        "Z", "Zs", "Zl", "Zp", "C", "Cc", "Cf", "Co", "Cn",
    };

    /// <summary>A pathological pattern is refused rather than left to run; Excel's PCRE2 has match limits too.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly Dictionary<string, (Regex Regex, bool Unicode)?> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// The position of the first text, in the order given, that <paramref name="pattern"/> matches
    /// (<see langword="null"/> for none); <see langword="false"/> when the pattern is refused, or
    /// when a text it has to read would be read differently by PCRE2 and .NET.
    /// </summary>
    public static bool TryFirstMatch(string pattern, IEnumerable<(int Index, string Text)> texts, out int? found)
    {
        found = null;
        if (Compile(pattern) is not { } compiled) return false;
        var (regex, unicode) = compiled;
        try
        {
            foreach (var (index, text) in texts)
            {
                if (unicode && text.AsSpan().ContainsAnyInRange('\uD800', '\uDFFF')) return false;
                if (!regex.IsMatch(text)) continue;
                found = index;
                return true;
            }
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            found = null;
            return false;
        }
    }

    private static (Regex Regex, bool Unicode)? Compile(string pattern)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(pattern, out var cached)) return cached;
            if (Cache.Count >= 256) Cache.Clear();
            var translated = Translate(pattern, out var unicode);
            (Regex, bool)? compiled = null;
            if (translated is not null)
            {
                try
                {
                    compiled = (new Regex(translated, RegexOptions.CultureInvariant, Timeout), unicode);
                }
                catch (ArgumentException)
                {
                    compiled = null;
                }
            }
            Cache[pattern] = compiled;
            return compiled;
        }
    }

    /// <summary>The pattern in .NET's syntax, meaning what it means to PCRE2; <see langword="null"/> when it uses a construct outside the accepted set.</summary>
    internal static string? Translate(string pattern) => Translate(pattern, out _);

    /// <param name="pattern">The pattern, in PCRE2's syntax.</param>
    /// <param name="unicode">Whether it uses a Unicode class (<c>\d \w \s \b \p{…}</c>), whose reading of a character outside the Basic Multilingual Plane differs between the two engines.</param>
    private static string? Translate(string pattern, out bool unicode)
    {
        unicode = false;
        var output = new StringBuilder();
        var groups = new Stack<Group>();
        var captures = 0;
        var quantifiable = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case '\\':
                    if (++i >= pattern.Length) return null;
                    var escaped = pattern[i];
                    switch (escaped)
                    {
                        case 'd':
                            output.Append('[').Append(Digit).Append(']');
                            unicode = true;
                            quantifiable = true;
                            break;
                        case 'w':
                            output.Append('[').Append(Word).Append(']');
                            unicode = true;
                            quantifiable = true;
                            break;
                        case 's':
                            output.Append('[').Append(Space).Append(']');
                            unicode = true;
                            quantifiable = true;
                            break;
                        case 'b':
                            output.Append($"(?:(?<=[{Word}])(?![{Word}])|(?<![{Word}])(?=[{Word}]))");
                            unicode = true;
                            quantifiable = false;
                            break;
                        case 'p':
                            var close = i + 1 < pattern.Length && pattern[i + 1] == '{' ? pattern.IndexOf('}', i + 2) : -1;
                            if (close < 0 || !Categories.Contains(pattern[(i + 2)..close])) return null;
                            output.Append("\\p{").Append(pattern, i + 2, close - i - 2).Append('}');
                            i = close;
                            unicode = true;
                            quantifiable = true;
                            break;
                        case >= '1' and <= '9':
                            if (groups.Contains(Group.Lookbehind)) return null;
                            // A backreference to a group already opened; \10 and up, and a digit after, read differently.
                            if (escaped - '0' > captures || (i + 1 < pattern.Length && char.IsAsciiDigit(pattern[i + 1]))) return null;
                            output.Append('\\').Append(escaped);
                            quantifiable = true;
                            break;
                        default:
                            if (!Metacharacters.Contains(escaped, StringComparison.Ordinal)) return null;
                            output.Append('\\').Append(escaped);
                            quantifiable = true;
                            break;
                    }
                    break;
                case '.':
                    output.Append("(?:").Append(Pair).Append("|[^\\n])");
                    quantifiable = true;
                    break;
                case '[':
                    var end = Class(pattern, i, output, ref unicode);
                    if (end < 0) return null;
                    i = end;
                    quantifiable = true;
                    break;
                case '(':
                    // Inside a lookbehind nothing opens: PCRE2 wants each of its branches a fixed length.
                    if (groups.Contains(Group.Lookbehind)) return null;
                    if (i + 1 < pattern.Length && pattern[i + 1] == '?')
                    {
                        if (i + 2 < pattern.Length && pattern[i + 2] is '=' or '!')
                        {
                            output.Append("(?").Append(pattern[i + 2]);
                            i += 2;
                            groups.Push(Group.Lookahead);
                        }
                        else if (i + 3 < pattern.Length && pattern[i + 2] == '<' && pattern[i + 3] is '=' or '!')
                        {
                            output.Append("(?<").Append(pattern[i + 3]);
                            i += 3;
                            groups.Push(Group.Lookbehind);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        output.Append('(');
                        captures++;
                        groups.Push(Group.Capture);
                    }
                    quantifiable = false;
                    break;
                case ')':
                    if (groups.Count == 0) return null;
                    output.Append(')');
                    // A quantified lookaround is read differently by the two engines' versions: refused.
                    quantifiable = groups.Pop() == Group.Capture;
                    break;
                case '|':
                case '^':
                case '$':
                    output.Append(c);
                    quantifiable = false;
                    break;
                case '*':
                case '+':
                case '?':
                    if (!quantifiable || groups.Contains(Group.Lookbehind)) return null;
                    output.Append(c);
                    if (!Lazy(pattern, ref i, output)) return null;
                    quantifiable = false;
                    break;
                case '{':
                    if (!quantifiable || groups.Contains(Group.Lookbehind)) return null;
                    var closeBrace = pattern.IndexOf('}', i);
                    if (closeBrace < 0 || !Bounds(pattern[(i + 1)..closeBrace])) return null;
                    output.Append(pattern, i, closeBrace - i + 1);
                    i = closeBrace;
                    if (!Lazy(pattern, ref i, output)) return null;
                    quantifiable = false;
                    break;
                case ']':
                case '}':
                    return null;
                default:
                    if (char.IsSurrogate(c)) return null;
                    output.Append(Regex.Escape(c.ToString()));
                    quantifiable = true;
                    break;
            }
        }
        return groups.Count == 0 ? output.ToString() : null;
    }

    /// <summary>What an open group is: one that captures, or a lookaround.</summary>
    private enum Group
    {
        Capture,
        Lookahead,
        Lookbehind,
    }

    /// <summary>A lazy <c>?</c> after a quantifier is copied; a possessive <c>+</c> is refused.</summary>
    private static bool Lazy(string pattern, ref int i, StringBuilder output)
    {
        if (i + 1 >= pattern.Length) return true;
        if (pattern[i + 1] == '+') return false;
        if (pattern[i + 1] == '?')
        {
            output.Append('?');
            i++;
        }
        return true;
    }

    /// <summary><c>n</c>, <c>n,</c> or <c>n,m</c> with n ≤ m: the inside of a quantifier both engines read alike.</summary>
    private static bool Bounds(string inside)
    {
        var parts = inside.Split(',');
        if (parts.Length > 2 || !Count(parts[0], out var low)) return false;
        if (parts.Length == 1 || parts[1].Length == 0) return true;
        return Count(parts[1], out var high) && low <= high;

        static bool Count(string digits, out int value)
        {
            value = 0;
            return digits.Length is > 0 and <= 5 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, out value);
        }
    }

    /// <summary>
    /// A character class from <paramref name="open"/>, translated into <paramref name="output"/>;
    /// the index of its closing <c>]</c>, or −1 when it is refused.
    /// </summary>
    private static int Class(string pattern, int open, StringBuilder output, ref bool unicode)
    {
        var i = open + 1;
        var negated = i < pattern.Length && pattern[i] == '^';
        if (negated) i++;
        var items = new StringBuilder();
        var count = 0;
        char? previous = null; // the last single literal character, which may start a range
        while (true)
        {
            if (i >= pattern.Length) return -1;
            var c = pattern[i];
            if (c == ']')
            {
                if (count == 0) return -1;
                break;
            }
            if (c == '[' || char.IsSurrogate(c)) return -1;
            if (c == '\\')
            {
                if (i + 1 >= pattern.Length) return -1;
                var escaped = pattern[i + 1];
                i += 2;
                switch (escaped)
                {
                    case 'd':
                        items.Append(Digit);
                        unicode = true;
                        previous = null;
                        break;
                    case 'w':
                        items.Append(Word);
                        unicode = true;
                        previous = null;
                        break;
                    case 's':
                        items.Append(Space);
                        unicode = true;
                        previous = null;
                        break;
                    default:
                        if (!Metacharacters.Contains(escaped, StringComparison.Ordinal)) return -1;
                        items.Append('\\').Append(escaped);
                        previous = escaped;
                        break;
                }
                count++;
                continue;
            }
            if (c == '-')
            {
                var last = i + 1 < pattern.Length && pattern[i + 1] == ']';
                if (count == 0 || last)
                {
                    items.Append("\\-");
                    previous = '-';
                    count++;
                    i++;
                    continue;
                }
                // A range: a single literal character on each side, in order.
                if (previous is not { } low || i + 1 >= pattern.Length) return -1;
                var high = pattern[i + 1];
                if (high is '\\' or '[' or ']' or '-' || char.IsSurrogate(high) || high < low) return -1;
                items.Append('-').Append(Literal(high));
                previous = null;
                i += 2;
                count++;
                continue;
            }
            items.Append(Literal(c));
            previous = c;
            count++;
            i++;
        }

        var set = "[" + (negated ? "^" : "") + items + "]";
        output.Append(negated ? "(?:" + Pair + "|" + set + ")" : set);
        return i;

        static string Literal(char c) => c is '^' or '\\' or ']' or '[' or '-' ? "\\" + c : c.ToString();
    }
}
