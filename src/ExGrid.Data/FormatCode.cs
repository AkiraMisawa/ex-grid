using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace ExGrid.Data;

/// <summary>
/// An Excel number format code in the invariant spelling (ADR-0120), as a grid built from a source's
/// shape reads it (ADR-0342): <c>#,##0.00</c>, <c>0.00%</c>, <c>0.00E+00</c>, <c>yyyy-mm-dd</c>.
/// <para>The subset read: for numbers, one to three sections (positive; negative; zero) of the digit
/// placeholders <c>0</c> and <c>#</c>, the decimal point, grouping and scaling commas, <c>%</c>,
/// <c>E+</c>/<c>E-</c> exponents, quoted text, <c>\</c> escapes and the symbols Excel shows unquoted;
/// for dates, one section of <c>y m d h s</c> and <c>AM/PM</c>. A section is chosen by the value's
/// sign before rounding, as Excel chooses it, and a negative value that rounds to zero in the first
/// section shows no minus sign (ExSheet's FMT-063). Anything else — colours and conditions in
/// brackets, <c>@</c>, <c>_</c>, <c>*</c>, <c>?</c>, fractions, unquoted letters, fractional seconds —
/// is refused rather than shown some other way.</para>
/// <para>Each section is translated once into a .NET custom format that agrees with Excel's on the
/// subset; a double is formatted through <see cref="decimal"/>, which keeps its 15 significant digits
/// as Excel does, so 1.005 in <c>0.00</c> is <c>1.01</c>.</para>
/// </summary>
public sealed class FormatCode : IEquatable<FormatCode>
{
    private readonly string[] sections;

    private FormatCode(string code, bool isDate, string[] sections)
    {
        Code = code;
        IsDate = isDate;
        this.sections = sections;
    }

    /// <summary>The code as written.</summary>
    public string Code { get; }

    /// <summary>Whether the code formats dates; otherwise it formats numbers.</summary>
    public bool IsDate { get; }

    /// <summary>Whether <paramref name="other"/> is the same code, as written: a code is what it writes.</summary>
    public bool Equals(FormatCode? other) => other is not null && Code == other.Code;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as FormatCode);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    /// <summary>Reads <paramref name="code"/>, or says why it cannot be read, as a phrase that
    /// completes "the format code …".</summary>
    public static bool TryParse(string code, [NotNullWhen(true)] out FormatCode? format, [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(code);
        format = null;
        if (code.Length == 0)
        {
            reason = "is empty";
            return false;
        }
        if (code.Length > 64)
        {
            reason = "is longer than 64 characters";
            return false;
        }
        if (!Split(code, out var parts, out reason))
            return false;
        var isDate = parts.Any(HasDateCode);
        if (isDate && parts.Count > 1)
        {
            reason = "has more than one section for a date";
            return false;
        }
        if (parts.Count > 3)
        {
            reason = "has more than three sections";
            return false;
        }
        var translated = new string[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            if (!(isDate ? TranslateDate(parts[i], out translated[i], out reason) : TranslateNumber(parts[i], out translated[i], out reason)))
                return false;
        }
        format = new FormatCode(code, isDate, translated);
        return true;
    }

    /// <summary>The text of <paramref name="value"/>: a number for a number code, a date for a date
    /// code.</summary>
    /// <exception cref="ArgumentException">A value of the other kind, which a checked column never hands
    /// it: refused rather than shown as some other text, as the Script Core's reader refuses it.</exception>
    public string Format(object value, CultureInfo culture)
    {
        if (IsDate)
        {
            return value switch
            {
                DateTime date => date.ToString(sections[0], culture),
                DateOnly date => date.ToDateTime(TimeOnly.MinValue).ToString(sections[0], culture),
                DateTimeOffset date => date.DateTime.ToString(sections[0], culture),
                _ => throw new ArgumentException($"The date code \"{Code}\" writes a date, and was given a {value.GetType().Name}.", nameof(value)),
            };
        }
        return value switch
        {
            decimal number => Number(number, culture),
            long number => Number(number, culture),
            int number => Number(number, culture),
            short or byte or sbyte or ushort or uint or ulong => Number(Convert.ToDecimal(value, CultureInfo.InvariantCulture), culture),
            float number => Format((double)number, culture),
            double number when number == 0 || Math.Abs(number) is >= 1e-13 and < 7.9e27 => Number((decimal)number, culture),
            double number when double.IsFinite(number) => NumberOutsideDecimal(number, culture),
            double number => number.ToString(culture),
            _ => throw new ArgumentException($"The number code \"{Code}\" writes a number, and was given a {value.GetType().Name}.", nameof(value)),
        };
    }

    // An empty section shows nothing, as Excel shows it; .NET would read "" as "G".
    private static string Apply(IFormattable value, string section, CultureInfo culture) =>
        section.Length == 0 ? "" : value.ToString(section, culture);

    private string Number(decimal number, CultureInfo culture)
    {
        if (number < 0 && sections.Length >= 2)
            return Apply(-number, sections[1], culture);
        if (number == 0 && sections.Length >= 3)
            return Apply(0m, sections[2], culture);
        // One section takes the sign itself, and a decimal that rounds to zero drops it, as Excel does.
        return Apply(number, sections[0], culture);
    }

    private string NumberOutsideDecimal(double number, CultureInfo culture)
    {
        if (number < 0 && sections.Length >= 2)
            return Apply(-number, sections[1], culture);
        return Apply(number, sections[0], culture);
    }

    private static bool Split(string code, out List<string> parts, [NotNullWhen(false)] out string? reason)
    {
        parts = [];
        reason = null;
        var current = new StringBuilder();
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"')
            {
                var end = code.IndexOf('"', i + 1);
                if (end < 0)
                {
                    reason = "opens a quotation that it does not close";
                    return false;
                }
                current.Append(code, i, end - i + 1);
                i = end;
            }
            else if (c == '\\')
            {
                if (i + 1 >= code.Length)
                {
                    reason = "ends in an escape with nothing after it";
                    return false;
                }
                current.Append(code, i, 2);
                i++;
            }
            else if (c == ';')
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        parts.Add(current.ToString());
        return true;
    }

    // Walks a section, handing each unquoted character to `plain` and each quoted or escaped one to
    // `literal`; false as soon as `plain` refuses.
    private static bool Walk(string section, Func<int, bool> plain, Action<char> literal)
    {
        for (var i = 0; i < section.Length; i++)
        {
            switch (section[i])
            {
                case '"':
                    var end = section.IndexOf('"', i + 1);
                    foreach (var q in section.AsSpan(i + 1, end - i - 1))
                        literal(q);
                    i = end;
                    break;
                case '\\':
                    literal(section[++i]);
                    break;
                default:
                    if (!plain(i))
                        return false;
                    break;
            }
        }
        return true;
    }

    private static bool HasDateCode(string section)
    {
        var found = false;
        Walk(section, i =>
        {
            found |= char.ToLowerInvariant(section[i]) is 'y' or 'm' or 'd' or 'h' or 's';
            return true;
        }, _ => { });
        return found;
    }

    private static string? RefusalOf(char c) => c switch
    {
        '[' => "holds a part in brackets (a colour, a condition or a locale), which the reader does not take",
        '@' => "holds a text placeholder '@', which the reader does not take",
        '_' => "holds '_' spacing, which the reader does not take",
        '*' => "holds '*' fill, which the reader does not take",
        '?' => "holds the placeholder '?', which the reader does not take",
        '/' => "holds a fraction, which the reader does not take",
        _ when char.IsLetter(c) => $"holds the letter '{c}' unquoted; quote text as \"text\"",
        _ => null,
    };

    private static bool TranslateNumber(string section, out string translated, [NotNullWhen(false)] out string? reason)
    {
        var output = new StringBuilder();
        string? refused = null;
        var points = 0;
        var ok = Walk(section, i =>
        {
            var c = section[i];
            if (c == '.')
                points++;
            switch (c)
            {
                case '0' or '#' or '.' or ',' or '%':
                    output.Append(c);
                    return true;
                case 'E' or 'e':
                    if (i + 2 < section.Length && section[i + 1] is '+' or '-' && section[i + 2] is '0' or '#')
                    {
                        output.Append(c);
                        return true;
                    }
                    refused = "holds an 'E' that is no exponent such as E+00";
                    return false;
                case '+' or '-' when i > 0 && section[i - 1] is 'E' or 'e':
                    output.Append(c);
                    return true;
                default:
                    if (RefusalOf(c) is { } why)
                    {
                        refused = why;
                        return false;
                    }
                    output.Append('\\').Append(c);
                    return true;
            }
        }, c => output.Append('\\').Append(c));
        translated = output.ToString();
        if (!ok)
        {
            reason = refused!;
            return false;
        }
        if (points > 1)
        {
            reason = "holds two decimal points in a section";
            return false;
        }
        reason = null;
        return true;
    }

    private static bool TranslateDate(string section, out string translated, [NotNullWhen(false)] out string? reason)
    {
        // Tokens first, so that an 'm' can be read as minutes after an hour or before a second.
        var tokens = new List<(char Code, int Count, string Text)>();
        string? refused = null;
        var ok = Walk(section, i =>
        {
            var c = section[i];
            var lower = char.ToLowerInvariant(c);
            // The rest of AM/PM, already taken.
            if (tokens.Count > 0 && tokens[^1].Code == 't' && IsInsideAmPm(section, i))
                return true;
            if (lower is 'a' && string.Compare(section, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                tokens.Add(('t', 5, ""));
                return true;
            }
            if (lower is 'y' or 'm' or 'd' or 'h' or 's')
            {
                if (tokens.Count > 0 && tokens[^1].Code == lower && i > 0 && char.ToLowerInvariant(section[i - 1]) == lower)
                    tokens[^1] = (lower, tokens[^1].Count + 1, "");
                else
                    tokens.Add((lower, 1, ""));
                return true;
            }
            if (c == '0' && i > 0 && section[i - 1] is '.' or '0')
            {
                refused = "holds fractional seconds, which the reader does not take";
                return false;
            }
            if (RefusalOf(c) is { } why && c != '/')
            {
                refused = why;
                return false;
            }
            tokens.Add(('\0', 0, c.ToString()));
            return true;
        }, c => tokens.Add(('\0', 0, c.ToString())));
        if (!ok)
        {
            translated = "";
            reason = refused!;
            return false;
        }

        var twelveHour = tokens.Any(t => t.Code == 't');
        var output = new StringBuilder();
        for (var k = 0; k < tokens.Count; k++)
        {
            var (code, count, text) = tokens[k];
            switch (code)
            {
                case '\0':
                    output.Append('\\').Append(text);
                    break;
                case 'y':
                    output.Append(count <= 2 ? "yy" : "yyyy");
                    break;
                case 'd':
                    output.Append('d', Math.Min(count, 4));
                    break;
                case 'h':
                    output.Append(twelveHour ? 'h' : 'H', Math.Min(count, 2));
                    break;
                case 's':
                    output.Append('s', Math.Min(count, 2));
                    break;
                case 't':
                    output.Append("tt");
                    break;
                case 'm':
                    if (count > 4)
                    {
                        translated = "";
                        reason = "holds 'mmmmm', which the reader does not take";
                        return false;
                    }
                    output.Append(IsMinute(tokens, k) && count <= 2 ? 'm' : 'M', count);
                    break;
            }
        }
        // A custom format of one character is read by .NET as a standard one ("d" the short date)
        // unless it is marked as custom.
        translated = output.Length == 1 ? "%" + output : output.ToString();
        reason = null;
        return true;
    }

    private static bool IsInsideAmPm(string section, int i)
    {
        for (var start = Math.Max(0, i - 4); start < i; start++)
        {
            if (string.Compare(section, start, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0 && i < start + 5)
                return true;
        }
        return false;
    }

    // An 'm' is minutes when the nearest code before it is an hour or the nearest after it a second.
    private static bool IsMinute(List<(char Code, int Count, string Text)> tokens, int at)
    {
        for (var k = at - 1; k >= 0; k--)
        {
            if (tokens[k].Code != '\0')
            {
                if (tokens[k].Code == 'h')
                    return true;
                break;
            }
        }
        for (var k = at + 1; k < tokens.Count; k++)
        {
            if (tokens[k].Code != '\0')
                return tokens[k].Code == 's';
        }
        return false;
    }
}
