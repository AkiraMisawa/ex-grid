namespace ExSheet.Xlsx;

/// <summary>
/// Whether a Number Format shows a date (ADR-0330 §4): the cells whose serials a workbook on the 1904 date system moves to
/// 1900's. A format that shows only a time of day, or a duration (<c>[h]:mm</c>), is not one: its number is the same in both
/// systems, and moving it would change an elapsed time.
/// </summary>
internal static class DateFormats
{
    /// <summary>The built-in formats that show a date (ECMA-376 Part 1, §18.8.30), East Asian ones included.</summary>
    private static readonly HashSet<int> BuiltInDates = [14, 15, 16, 17, 22, 27, 28, 29, 30, 31, 34, 35, 36, 50, 51, 52, 53, 54, 55, 56, 57, 58];

    /// <summary>Whether the format <paramref name="id"/> shows a date; a custom one by its <paramref name="code"/>.</summary>
    public static bool ShowsDate(int id, string? code) => code is null ? BuiltInDates.Contains(id) : CodeShowsDate(code);

    /// <summary>
    /// Whether a format code's first section shows a part of a date: a year (<c>y</c>, <c>e</c>), a day (<c>d</c>), or a
    /// month (<c>m</c> in a section with no hour or second, where Excel reads it as minutes). An elapsed time in brackets
    /// (<c>[h]</c>, <c>[mm]</c>, <c>[ss]</c>) makes the section a duration, so an <c>m</c> beside it is minutes. Text in
    /// quotes, an escaped character, what else stands in brackets and <c>General</c> are not read as parts.
    /// </summary>
    public static bool CodeShowsDate(string code)
    {
        bool hasMonthOrMinute = false, hasTime = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            switch (c)
            {
                case ';':
                    return !hasTime && hasMonthOrMinute;
                case '"':
                    i = code.IndexOf('"', i + 1) is var end and >= 0 ? end : code.Length;
                    continue;
                case '\\' or '_' or '*':
                    i++;
                    continue;
                case '[':
                    var close = code.IndexOf(']', i + 1);
                    if (close < 0) close = code.Length;
                    // [h], [mm], [ss]: an elapsed time, so the section is a duration, and an m beside it is minutes.
                    if (IsElapsed(code.AsSpan(i + 1, close - i - 1))) hasTime = true;
                    i = close;
                    continue;
            }
            if (string.Compare(code, i, "General", 0, 7, StringComparison.OrdinalIgnoreCase) == 0)
            {
                i += 6;
                continue;
            }
            if (string.Compare(code, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                hasTime = true;
                i += 4;
                continue;
            }
            if (string.Compare(code, i, "A/P", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
            {
                hasTime = true;
                i += 2;
                continue;
            }
            switch (char.ToLowerInvariant(c))
            {
                case 'y' or 'd':
                    return true;
                case 'e' when i + 1 >= code.Length || code[i + 1] is not ('+' or '-'):
                    return true;
                case 'm':
                    hasMonthOrMinute = true;
                    break;
                case 'h' or 's':
                    hasTime = true;
                    break;
            }
        }
        return !hasTime && hasMonthOrMinute;
    }

    /// <summary>Whether a bracket's content is an elapsed time: one letter of <c>h</c>, <c>m</c> or <c>s</c>, repeated.</summary>
    private static bool IsElapsed(ReadOnlySpan<char> inside)
    {
        if (inside.Length == 0) return false;
        var letter = char.ToLowerInvariant(inside[0]);
        if (letter is not ('h' or 'm' or 's')) return false;
        foreach (var c in inside)
        {
            if (char.ToLowerInvariant(c) != letter) return false;
        }
        return true;
    }
}
