using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExSheet.Engine;

/// <summary>
/// Reads a typed constant under a Sheet's culture, so that it can be recorded parsed (ADR-0048):
/// a number with the culture's separators, a percentage, a date in the culture's day order, a
/// time, a boolean, one of Excel's Error Values, or else text.
/// </summary>
internal static partial class ConstantParser
{
    public static Value Parse(string typed, CultureInfo culture) => ParseWithFormat(typed, culture).Value;

    /// <summary>The constant, and the number format Excel applies to a General cell when it is typed (a date's, a percentage's).</summary>
    public static (Value Value, NumberFormat? Implied) ParseWithFormat(string typed, CultureInfo culture)
    {
        // A leading apostrophe makes the rest text, whatever it looks like, as in Excel.
        if (typed.Length > 0 && typed[0] == '\'') return (Value.FromText(typed[1..]), null);
        if (typed.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return (Value.FromBoolean(true), null);
        if (typed.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return (Value.FromBoolean(false), null);
        if (ErrorValues.TryParseTyped(typed, out var error)) return (Value.FromError(error), null);
        var trimmed = typed.Trim(' ');
        if (TryParseNumber(trimmed, culture, out var number, out var percent))
        {
            NumberFormat? implied = null;
            if (percent) implied = NumberFormat.Parse(trimmed.Contains(culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal) ? "0.00%" : "0%");
            return (Value.FromNumber(number), implied);
        }
        if (TryParseDateTime(trimmed, culture, DateTime.Today.Year, out var serial, out var format)) return (Value.FromNumber(serial), format);
        return (Value.FromText(typed), null);
    }

    /// <summary>Text read as a number the way a typed number is: what Excel's arithmetic does with numeric text.</summary>
    public static bool TryParseNumber(string text, CultureInfo culture, out double number)
    {
        var trimmed = text.Trim(' ');
        if (TryParseNumber(trimmed, culture, out number, out _)) return true;
        if (TryParseDateTime(trimmed, culture, DateTime.Today.Year, out number, out _)) return true;
        number = 0;
        return false;
    }

    /// <summary>
    /// A number under the culture: an optional sign or parentheses for a negative, digits with the
    /// culture's group separator in groups of three, the culture's decimal separator, an optional
    /// exponent, an optional trailing <c>%</c>. Digits past the fifteenth significant one become
    /// zeros, as Excel keeps fifteen.
    /// </summary>
    private static bool TryParseNumber(string text, CultureInfo culture, out double number, out bool percent)
    {
        number = 0;
        percent = false;
        if (text.Length == 0) return false;
        var format = culture.NumberFormat;
        var decimalSeparator = format.NumberDecimalSeparator;
        var groupSeparator = format.NumberGroupSeparator;
        var i = 0;
        var negative = false;
        var parenthesised = false;
        if (text[i] == '+' || text[i] == '-')
        {
            negative = text[i] == '-';
            i++;
        }
        else if (text[i] == '(')
        {
            parenthesised = true;
            negative = true;
            i++;
        }

        var integer = new StringBuilder();
        var groupRun = -1; // digits since the last group separator; -1 before any separator
        while (i < text.Length)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                integer.Append(text[i]);
                if (groupRun >= 0) groupRun++;
                i++;
                continue;
            }
            var separatorLength = GroupSeparatorAt(text, i, groupSeparator);
            if (separatorLength > 0 && integer.Length > 0 && (groupRun == -1 ? integer.Length <= 3 : groupRun == 3))
            {
                groupRun = 0;
                i += separatorLength;
                continue;
            }
            break;
        }
        if (groupRun >= 0 && groupRun != 3) return false;

        var fraction = new StringBuilder();
        if (string.CompareOrdinal(text, i, decimalSeparator, 0, decimalSeparator.Length) == 0)
        {
            i += decimalSeparator.Length;
            while (i < text.Length && char.IsAsciiDigit(text[i])) fraction.Append(text[i++]);
        }
        if (integer.Length == 0 && fraction.Length == 0) return false;

        var exponent = 0;
        if (i < text.Length && (text[i] == 'E' || text[i] == 'e'))
        {
            var j = i + 1;
            var exponentNegative = false;
            if (j < text.Length && (text[j] == '+' || text[j] == '-'))
            {
                exponentNegative = text[j] == '-';
                j++;
            }
            var start = j;
            while (j < text.Length && char.IsAsciiDigit(text[j])) j++;
            if (j == start || j - start > 4) return false;
            exponent = int.Parse(text.AsSpan(start, j - start), NumberStyles.None, CultureInfo.InvariantCulture);
            if (exponentNegative) exponent = -exponent;
            i = j;
        }
        if (i < text.Length && text[i] == '%')
        {
            percent = true;
            i++;
        }
        if (parenthesised)
        {
            if (i >= text.Length || text[i] != ')') return false;
            i++;
        }
        if (i != text.Length) return false;

        // Excel keeps fifteen significant digits of what is typed; the rest become zeros.
        var digits = (integer.ToString() + fraction).ToCharArray();
        var significant = 0;
        for (var k = 0; k < digits.Length; k++)
        {
            if (significant == 0 && digits[k] == '0') continue;
            if (++significant > 15) digits[k] = '0';
        }
        var kept = new string(digits);
        var spelled = kept[..integer.Length] + (fraction.Length > 0 ? "." + kept[integer.Length..] : "") + "E" + exponent.ToString(CultureInfo.InvariantCulture);
        if (!double.TryParse(spelled, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || !double.IsFinite(number)) return false;
        if (percent) number /= 100;
        if (negative) number = -number;
        return double.IsFinite(number);
    }

    /// <summary>
    /// A number written in a Formula (invariant digits, an optional point and exponent), read as
    /// Excel reads it: digits past the fifteenth significant one become zeros, so
    /// <c>=123456789012345678</c> holds 123456789012345000, as Excel was observed to.
    /// </summary>
    internal static double ParseFormulaNumber(string invariant)
    {
        var chars = invariant.ToCharArray();
        var significant = 0;
        for (var k = 0; k < chars.Length && chars[k] is not ('e' or 'E'); k++)
        {
            if (!char.IsAsciiDigit(chars[k]) || (significant == 0 && chars[k] == '0')) continue;
            if (++significant > 15) chars[k] = '0';
        }
        return double.Parse(chars, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
    }

    /// <summary>The culture's group separator at <paramref name="at"/>; a space-like separator also accepts a plain space.</summary>
    private static int GroupSeparatorAt(string text, int at, string separator)
    {
        if (string.CompareOrdinal(text, at, separator, 0, separator.Length) == 0) return separator.Length;
        var spaceLike = separator.Length == 1 && char.IsWhiteSpace(separator[0]);
        return spaceLike && (text[at] == ' ' || text[at] == ' ' || text[at] == ' ') ? 1 : 0;
    }

    [GeneratedRegex(@"^(?<a>\d{1,4})(?<s1>[/\-.])(?<b>\d{1,2})(?:\k<s1>(?<c>\d{1,4}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{1,2})(?::(?<s>\d{1,2}))?(?:\s*(?<ampm>[AaPp][Mm]?))?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimePattern();

    /// <summary>
    /// A date in the culture's day order (month-day-year under <c>en-US</c>, year-month-day under
    /// <c>ja-JP</c>, day-month-year under <c>de-DE</c>) with <c>/</c>, <c>-</c> or the culture's own
    /// separator; a four-digit year written first is always year-month-day. Without a year, the
    /// date is in <paramref name="currentYear"/>, as Excel takes it. A two-digit year is 2000–2029
    /// for 00–29 and 1930–1999 for 30–99. A time <c>h:mm[:ss] [AM|PM]</c>, alone or after the date.
    /// </summary>
    internal static bool TryParseDateTime(string text, CultureInfo culture, int currentYear, out double serial, out NumberFormat? format)
    {
        serial = 0;
        format = null;
        string datePart = text;
        string? timePart = null;
        var space = text.IndexOf(' ');
        if (space > 0 && TimePattern().IsMatch(text[(space + 1)..].Trim()))
        {
            datePart = text[..space];
            timePart = text[(space + 1)..].Trim();
        }

        if (timePart is null && TryParseTime(text, out var timeOnly, out var timeFormat))
        {
            serial = timeOnly;
            format = NumberFormat.Parse(timeFormat);
            return true;
        }

        var match = DatePattern().Match(datePart);
        if (!match.Success) return false;
        var separator = match.Groups["s1"].Value[0];
        var cultureSeparator = culture.DateTimeFormat.DateSeparator;
        if (separator == '.' && cultureSeparator != ".") return false;

        var a = match.Groups["a"].Value;
        var b = match.Groups["b"].Value;
        var c = match.Groups["c"].Success ? match.Groups["c"].Value : null;
        int year, month, day;
        var order = DayOrder(culture);
        if (a.Length >= 3 || order == "ymd")
        {
            if (c is null)
            {
                return false;
            }
            year = Year(a);
            month = Parse(b);
            day = Parse(c);
        }
        else if (order == "dmy")
        {
            day = Parse(a);
            month = Parse(b);
            year = c is null ? currentYear : Year(c);
        }
        else
        {
            month = Parse(a);
            day = Parse(b);
            year = c is null ? currentYear : Year(c);
        }
        if (DateSerial.FromDate(year, month, day) is not { } whole) return false;

        var fraction = 0.0;
        if (timePart is not null)
        {
            if (!TryParseTime(timePart, out fraction, out _)) return false;
        }
        serial = whole + fraction;
        var dateCode = DateCode(culture);
        format = NumberFormat.Parse(timePart is null ? dateCode : dateCode + " h:mm");
        return true;

        static int Parse(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

        static int Year(string digits)
        {
            var value = Parse(digits);
            if (digits.Length > 2) return value;
            return value < 30 ? 2000 + value : 1900 + value;
        }
    }

    private static bool TryParseTime(string text, out double fraction, out string format)
    {
        fraction = 0;
        format = "";
        var match = TimePattern().Match(text);
        if (!match.Success) return false;
        var hour = int.Parse(match.Groups["h"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["m"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        var second = match.Groups["s"].Success ? int.Parse(match.Groups["s"].Value, NumberStyles.None, CultureInfo.InvariantCulture) : 0;
        if (minute > 59 || second > 59) return false;
        var ampm = match.Groups["ampm"].Success ? char.ToUpperInvariant(match.Groups["ampm"].Value[0]) : '\0';
        if (ampm != '\0')
        {
            if (hour < 1 || hour > 12) return false;
            hour %= 12;
            if (ampm == 'P') hour += 12;
        }
        else if (hour > 23)
        {
            return false;
        }
        fraction = (hour * 3600 + minute * 60 + second) / 86_400.0;
        format = (match.Groups["s"].Success ? "h:mm:ss" : "h:mm") + (ampm != '\0' ? " AM/PM" : "");
        return true;
    }

    /// <summary>The order of year, month and day in the culture's short date pattern: "mdy", "dmy" or "ymd".</summary>
    internal static string DayOrder(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var order = new StringBuilder();
        foreach (var c in pattern)
        {
            var part = c switch
            {
                'y' => 'y',
                'M' => 'm',
                'd' => 'd',
                _ => '\0',
            };
            if (part != '\0' && order.ToString().IndexOf(part) < 0) order.Append(part);
        }
        var result = order.ToString();
        return result is "mdy" or "dmy" or "ymd" ? result : "mdy";
    }

    /// <summary>The culture's short date pattern as an Excel format code: <c>M/d/yyyy</c> becomes <c>m/d/yyyy</c>.</summary>
    internal static string DateCode(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var code = new StringBuilder();
        var separator = culture.DateTimeFormat.DateSeparator;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case 'y':
                case 'd':
                    code.Append(c);
                    break;
                case 'M':
                    code.Append('m');
                    break;
                case '/':
                    AppendLiteral(code, separator);
                    break;
                case '\'':
                    var close = pattern.IndexOf('\'', i + 1);
                    if (close < 0) close = pattern.Length;
                    AppendLiteral(code, pattern[(i + 1)..close]);
                    i = close;
                    break;
                default:
                    AppendLiteral(code, c.ToString());
                    break;
            }
        }
        return code.ToString();
    }

    /// <summary>Punctuation stands as itself in a format code; anything a code could misread is quoted.</summary>
    private static void AppendLiteral(StringBuilder code, string literal)
    {
        foreach (var c in literal)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '"' or '\\' or ';' or '@' or '*' or '_' or '[' or '#' or '?' or '%' or ',' or 'E' or 'e')
            {
                code.Append(c == '"' ? "\\\"" : "\"" + c + "\"");
            }
            else
            {
                code.Append(c);
            }
        }
    }
}
