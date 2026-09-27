using System.Text;
using System.Text.RegularExpressions;

namespace ExSheet.Engine.Formulas;

/// <summary>
/// XLOOKUP's <c>match_mode</c> 3, regular expressions (ADR-0047, "What the observation settled").
/// Excel's expressions are PCRE2's, and .NET's are close but not the same, so a pattern is
/// accepted only when every construct in it means the same in both, and is translated so that
/// .NET's engine gives PCRE2's answer. Any other pattern is refused (<c>#VALUE!</c>).
/// </summary>
/// <remarks>
/// <para>
/// The accepted constructs: literal characters; <c>.</c>; character classes <c>[…]</c> and
/// <c>[^…]</c> of literal characters, ranges between two literal characters, <c>\d \w \s</c> and
/// escaped metacharacters; <c>\d \w \s \b</c>; the anchors <c>^ $</c>; the quantifiers
/// <c>* + ? {n} {n,} {n,m}</c> and their lazy forms; groups <c>(…)</c> and alternation <c>|</c>;
/// and a backslash before a metacharacter. Refused: every <c>(?</c> construct (lookaround,
/// non-capturing and named groups, inline options), backreferences, possessive quantifiers,
/// Unicode properties, POSIX classes, <c>\D \W \S \B</c> and every other escape, a <c>{</c> or
/// <c>}</c> that is not a whole quantifier, an unescaped <c>]</c> outside a class, and characters
/// outside the Basic Multilingual Plane.
/// </para>
/// <para>
/// PCRE2's defaults are taken where the two engines differ: <c>\d</c>, <c>\w</c>, <c>\s</c> and
/// <c>\b</c> are ASCII (PCRE2 without UCP), and <c>.</c> and a negated class match one code point,
/// a surrogate pair included (PCRE2 in UTF mode), where .NET would match one UTF-16 unit. That
/// Excel compiles without UCP, that the match is case-sensitive, and that a value matches when
/// the pattern matches any part of it (not only the whole) are this engine's reading of PCRE2's
/// defaults, and each is an <c>uncertain</c> case in the corpus until Excel is asked.
/// </para>
/// </remarks>
internal static class PortableRegex
{
    private const string Word = "A-Za-z0-9_";
    private const string Space = "\\t\\n\\v\\f\\r ";
    private const string Pair = "[\\uD800-\\uDBFF][\\uDC00-\\uDFFF]";
    private const string Metacharacters = ".\\*+?()[]{}|^$/-";

    /// <summary>A pathological pattern is refused rather than left to run; Excel's PCRE2 has match limits too.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly Dictionary<string, Regex?> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// The position of the first text, in the order given, that <paramref name="pattern"/> matches
    /// (<see langword="null"/> for none); <see langword="false"/> when the pattern is refused.
    /// </summary>
    public static bool TryFirstMatch(string pattern, IEnumerable<(int Index, string Text)> texts, out int? found)
    {
        found = null;
        if (Compile(pattern) is not { } regex) return false;
        try
        {
            foreach (var (index, text) in texts)
            {
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

    private static Regex? Compile(string pattern)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(pattern, out var cached)) return cached;
            if (Cache.Count >= 256) Cache.Clear();
            var translated = Translate(pattern);
            Regex? regex = null;
            if (translated is not null)
            {
                try
                {
                    regex = new Regex(translated, RegexOptions.CultureInvariant, Timeout);
                }
                catch (ArgumentException)
                {
                    regex = null;
                }
            }
            Cache[pattern] = regex;
            return regex;
        }
    }

    /// <summary>The pattern in .NET's syntax, meaning what it means to PCRE2; <see langword="null"/> when it uses a construct outside the accepted set.</summary>
    internal static string? Translate(string pattern)
    {
        var output = new StringBuilder();
        var depth = 0;
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
                            output.Append("[0-9]");
                            quantifiable = true;
                            break;
                        case 'w':
                            output.Append('[').Append(Word).Append(']');
                            quantifiable = true;
                            break;
                        case 's':
                            output.Append('[').Append(Space).Append(']');
                            quantifiable = true;
                            break;
                        case 'b':
                            output.Append($"(?:(?<=[{Word}])(?![{Word}])|(?<![{Word}])(?=[{Word}]))");
                            quantifiable = false;
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
                    var end = Class(pattern, i, output);
                    if (end < 0) return null;
                    i = end;
                    quantifiable = true;
                    break;
                case '(':
                    if (i + 1 < pattern.Length && pattern[i + 1] == '?') return null;
                    output.Append('(');
                    depth++;
                    quantifiable = false;
                    break;
                case ')':
                    if (depth == 0) return null;
                    output.Append(')');
                    depth--;
                    quantifiable = true;
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
                    if (!quantifiable) return null;
                    output.Append(c);
                    if (!Lazy(pattern, ref i, output)) return null;
                    quantifiable = false;
                    break;
                case '{':
                    if (!quantifiable) return null;
                    var close = pattern.IndexOf('}', i);
                    if (close < 0 || !Bounds(pattern[(i + 1)..close])) return null;
                    output.Append(pattern, i, close - i + 1);
                    i = close;
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
        return depth == 0 ? output.ToString() : null;
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
    private static int Class(string pattern, int open, StringBuilder output)
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
                        items.Append("0-9");
                        previous = null;
                        break;
                    case 'w':
                        items.Append(Word);
                        previous = null;
                        break;
                    case 's':
                        items.Append(Space);
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
