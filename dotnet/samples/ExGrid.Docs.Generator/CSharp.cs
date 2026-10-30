namespace ExGrid.Docs.Generator;

/// <summary>
/// Steps over C# inside a Razor page, so that what the page reader takes for markup is markup: a
/// bracket inside a string, a comment or a nested expression does not end the expression it is in.
/// </summary>
internal static class CSharp
{
    /// <summary>
    /// From the opening bracket at <paramref name="i"/>, moves past the bracket that closes it.
    /// Returns <see langword="false"/>, with <paramref name="i"/> at the end of the text, when none does.
    /// </summary>
    public static bool SkipBalanced(string text, ref int i, char open, char close)
    {
        var depth = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '/' && At(text, i + 1, '/'))
            {
                var end = text.IndexOf('\n', i);
                i = end < 0 ? text.Length : end + 1;
                continue;
            }
            if (c == '/' && At(text, i + 1, '*'))
            {
                var end = text.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                if (end < 0)
                {
                    i = text.Length;
                    return false;
                }
                i = end + 2;
                continue;
            }
            if (c is '"' or '$' or '@' && StartsString(text, i))
            {
                if (!SkipString(text, ref i))
                    return false;
                continue;
            }
            if (c == '\'')
            {
                SkipCharacter(text, ref i);
                continue;
            }
            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    return true;
                }
            }
            i++;
        }
        return false;
    }

    private static bool At(string text, int i, char c) => i < text.Length && text[i] == c;

    // "…", @"…", $"…", $@"…", @$"…", and raw strings with any number of $ and at least three quotes.
    private static bool StartsString(string text, int i)
    {
        while (i < text.Length && text[i] is '$' or '@')
            i++;
        return At(text, i, '"');
    }

    private static bool SkipString(string text, ref int i)
    {
        var interpolated = false;
        var verbatim = false;
        while (text[i] is '$' or '@')
        {
            interpolated |= text[i] == '$';
            verbatim |= text[i] == '@';
            i++;
        }

        var quotes = 0;
        while (At(text, i + quotes, '"'))
            quotes++;
        if (quotes >= 3)
        {
            var closing = new string('"', quotes);
            var end = text.IndexOf(closing, i + quotes, System.StringComparison.Ordinal);
            if (end < 0)
            {
                i = text.Length;
                return false;
            }
            i = end + quotes;
            return true;
        }
        if (quotes == 2 && !verbatim)
        {
            i += 2;
            return true;
        }

        i++;
        while (i < text.Length)
        {
            var c = text[i];
            if (verbatim && c == '"' && At(text, i + 1, '"'))
            {
                i += 2;
                continue;
            }
            if (c == '"')
            {
                i++;
                return true;
            }
            if (!verbatim && c == '\\')
            {
                i += 2;
                continue;
            }
            if (interpolated && c == '{')
            {
                if (At(text, i + 1, '{'))
                {
                    i += 2;
                    continue;
                }
                if (!SkipBalanced(text, ref i, '{', '}'))
                    return false;
                continue;
            }
            if (!verbatim && c == '\n')
            {
                // A regular string never spans lines: what looked like one was not.
                i++;
                return true;
            }
            i++;
        }
        return false;
    }

    // 'x', '\n', 'A': a quote that closes within a few characters. A lone apostrophe in
    // markup text is stepped over as itself.
    private static void SkipCharacter(string text, ref int i)
    {
        for (var k = i + 1; k < text.Length && k <= i + 8; k++)
        {
            if (text[k] == '\\')
            {
                k++;
                continue;
            }
            if (text[k] == '\'')
            {
                i = k + 1;
                return;
            }
            if (text[k] == '\n')
                break;
        }
        i++;
    }
}
