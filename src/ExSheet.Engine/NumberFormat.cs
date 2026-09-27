using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// An Excel number format code, such as <c>#,##0.00</c>, <c>0%</c> or <c>yyyy-mm-dd</c> (ADR-0046).
/// The code is written in Excel's stored, invariant form — <c>,</c> groups thousands and <c>.</c>
/// marks decimals — and shown with the Sheet's culture's separators and month and day names.
/// </summary>
/// <remarks>
/// The subset read: up to four sections (positive; negative; zero; text); the digit placeholders
/// <c>0 # ?</c>, the decimal point, thousands separators and scaling commas, <c>%</c>, scientific
/// <c>E+00</c> with one integer placeholder, <c>@</c>, quoted text, <c>\</c> escapes, <c>_</c>
/// spacing, and the date and time codes <c>y m d h s</c> with <c>AM/PM</c> and <c>A/P</c>. A code
/// outside the subset — colours and conditions in brackets, elapsed time, fractions, fractional
/// seconds, <c>*</c> fill, era codes, unquoted letters — is refused rather than shown some other
/// way.
/// </remarks>
public sealed class NumberFormat : IEquatable<NumberFormat>
{
    private readonly Section[] _sections;

    private NumberFormat(string code, Section[] sections)
    {
        Code = code;
        _sections = sections;
    }

    /// <summary>Excel's <c>General</c>: at most 15 significant digits, the culture's decimal separator.</summary>
    public static NumberFormat General { get; } = new("General", []);

    /// <summary>The code as given, such as <c>#,##0.00</c>.</summary>
    public string Code { get; }

    /// <summary>Whether this is <c>General</c>.</summary>
    public bool IsGeneral => _sections.Length == 0;

    /// <summary>Whether a number shows as a date or time: its first section holds date or time codes.</summary>
    public bool IsDate => _sections.Length > 0 && _sections[0].Kind == SectionKind.Date;

    /// <summary>Whether a number shows as a date with no time of day: date codes and no hour, second or AM/PM.</summary>
    internal bool IsDateOnly => IsDate && !_sections[0].HasTime;

    /// <summary>Whether a number shows as a percentage: its first section holds <c>%</c>.</summary>
    public bool IsPercent => _sections.Length > 0 && _sections[0].Percent > 0;

    /// <summary>Reads a format code.</summary>
    /// <exception cref="FormatException">The code is outside the subset ExSheet shows as Excel does.</exception>
    public static NumberFormat Parse(string code) =>
        TryParse(code, out var format, out var reason) ? format : throw new FormatException($"The number format '{code}' is not supported: {reason}");

    /// <summary>Reads a format code, saying why when it cannot.</summary>
    public static bool TryParse(string code, [NotNullWhen(true)] out NumberFormat? format, [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(code);
        format = null;
        if (code.Equals("General", StringComparison.OrdinalIgnoreCase))
        {
            format = General;
            reason = null;
            return true;
        }
        var parts = SplitSections(code, out reason);
        if (parts is null) return Refuse(out reason, reason);
        if (parts.Count > 4)
        {
            reason = "a format has at most four sections.";
            return false;
        }
        var sections = new Section[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            var section = Section.Parse(parts[i], out reason);
            if (section is null) return Refuse(out reason, reason);
            if (section.Kind == SectionKind.Text && i < 3 && parts.Count > 1)
            {
                reason = "@ belongs in the fourth section, or in a format of one section.";
                return false;
            }
            if (i == 3 && section.Kind is not (SectionKind.Text or SectionKind.Literal))
            {
                reason = "the fourth section is for text.";
                return false;
            }
            sections[i] = section;
        }
        format = new NumberFormat(code, sections);
        reason = null;
        return true;
    }

    /// <summary>Whether a number shows in Excel's General form: the format is General, or holds only a text section (<c>@</c>).</summary>
    internal bool ShowsNumbersAsGeneral => _sections.Length == 0 || (_sections.Length == 1 && _sections[0].Kind == SectionKind.Text);

    /// <summary>
    /// A Value as this format shows it in a column <paramref name="characters"/> wide, each
    /// character charged one digit width (ADR-0047): a number in General is fitted to the width
    /// as Excel's General fits it, and any other number whose text is longer than the width
    /// cannot be shown (<c>####</c>, ADR-0016). Text, booleans and Error Values are never fitted.
    /// </summary>
    internal (string Text, bool CannotShow) Format(Value value, CultureInfo culture, int characters)
    {
        if (value.Kind != ValueKind.Number) return Format(value, culture);
        if (ShowsNumbersAsGeneral)
        {
            return NumberText.General(value.Number, characters, culture) is { } fitted ? (fitted, false) : ("", true);
        }
        var (text, cannotShow) = Format(value, culture);
        return cannotShow || text.Length > characters ? ("", true) : (text, false);
    }

    /// <summary>A Value as this format shows it under <paramref name="culture"/>; booleans and Error Values show as themselves.</summary>
    internal (string Text, bool CannotShow) Format(Value value, CultureInfo culture)
    {
        switch (value.Kind)
        {
            case ValueKind.Boolean:
            case ValueKind.Error:
                return (value.ToString(), false);
            case ValueKind.Text:
                var textSection = _sections.Length == 4 ? _sections[3] : _sections.Length == 1 && _sections[0].Kind == SectionKind.Text ? _sections[0] : null;
                return (textSection is null ? value.Text : textSection.FormatText(value.Text), false);
        }

        var number = value.Number;
        if (ShowsNumbersAsGeneral)
        {
            return (NumberText.General(number, culture), false);
        }
        Section chosen;
        var automaticMinus = false;
        if (number < 0 && _sections.Length >= 2)
        {
            chosen = _sections[1];
        }
        else if (number == 0 && _sections.Length >= 3)
        {
            chosen = _sections[2];
        }
        else
        {
            chosen = _sections[0];
            automaticMinus = number < 0;
        }
        return chosen.FormatNumber(Math.Abs(number), automaticMinus, culture);
    }

    /// <summary>Two formats are equal when their codes are (ordinal).</summary>
    public bool Equals(NumberFormat? other) => other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NumberFormat);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    /// <summary>The code.</summary>
    public override string ToString() => Code;

    private static bool Refuse(out string reason, string? why)
    {
        reason = why ?? "the code cannot be read.";
        return false;
    }

    private static List<string>? SplitSections(string code, out string? reason)
    {
        reason = null;
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '"':
                    var close = code.IndexOf('"', i + 1);
                    if (close < 0)
                    {
                        reason = "a quotation is not closed.";
                        return null;
                    }
                    i = close;
                    break;
                case '\\':
                case '_':
                case '*':
                    i++;
                    break;
                case ';':
                    parts.Add(code[start..i]);
                    start = i + 1;
                    break;
            }
        }
        parts.Add(code[start..]);
        return parts;
    }

    private enum SectionKind
    {
        Number,
        Date,
        Text,
        Literal,
    }

    private enum PartKind
    {
        Literal,
        Digit,
        DecimalPoint,
        Comma,
        Percent,
        Exponent,
        DateCode,
        AmPm,
        TextValue,
    }

    private sealed record Part(PartKind Kind, string Text, char Code = '\0', int Count = 0);

    private sealed class Section
    {
        private readonly List<Part> _parts;

        private Section(List<Part> parts, SectionKind kind)
        {
            _parts = parts;
            Kind = kind;
        }

        public SectionKind Kind { get; }

        public int Percent { get; private set; }

        /// <summary>Whether a date section shows a time of day.</summary>
        public bool HasTime => _parts.Any(p => p.Kind == PartKind.AmPm || (p.Kind == PartKind.DateCode && p.Code is 'h' or 's'));

        private int _scale;
        private bool _thousands;
        private int _integerPlaceholders;
        private int _fractionPlaceholders;
        private int _exponentPlaceholders;
        private bool _twelveHour;

        public static Section? Parse(string text, out string? reason)
        {
            reason = null;
            var parts = new List<Part>();
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case '"':
                        var close = text.IndexOf('"', i + 1);
                        parts.Add(new Part(PartKind.Literal, text[(i + 1)..close]));
                        i = close;
                        continue;
                    case '\\':
                        if (i + 1 >= text.Length)
                        {
                            reason = "\\ escapes nothing.";
                            return null;
                        }
                        parts.Add(new Part(PartKind.Literal, text[i + 1].ToString()));
                        i++;
                        continue;
                    case '_':
                        // Excel pads by the width of the next character; a space stands in for it.
                        if (i + 1 >= text.Length)
                        {
                            reason = "_ pads by nothing.";
                            return null;
                        }
                        parts.Add(new Part(PartKind.Literal, " "));
                        i++;
                        continue;
                    case '*':
                        reason = "* repeats a character to fill the column, which depends on its width.";
                        return null;
                    case '[':
                        reason = "colours, conditions, locales and elapsed time in brackets are not supported.";
                        return null;
                    case '@':
                        parts.Add(new Part(PartKind.TextValue, "@"));
                        continue;
                    case '0' or '#' or '?':
                        parts.Add(new Part(PartKind.Digit, c.ToString(), c));
                        continue;
                    case '.':
                        parts.Add(new Part(PartKind.DecimalPoint, "."));
                        continue;
                    case ',':
                        parts.Add(new Part(PartKind.Comma, ","));
                        continue;
                    case '%':
                        parts.Add(new Part(PartKind.Percent, "%"));
                        continue;
                }

                if ((c is 'E' or 'e') && i + 1 < text.Length && text[i + 1] is '+' or '-')
                {
                    parts.Add(new Part(PartKind.Exponent, text.Substring(i, 2), text[i + 1]));
                    i++;
                    continue;
                }
                if (string.Compare(text, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    parts.Add(new Part(PartKind.AmPm, text.Substring(i, 5)));
                    i += 4;
                    continue;
                }
                if (string.Compare(text, i, "A/P", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    parts.Add(new Part(PartKind.AmPm, text.Substring(i, 3)));
                    i += 2;
                    continue;
                }
                var lower = char.ToLowerInvariant(c);
                if (lower is 'y' or 'm' or 'd' or 'h' or 's')
                {
                    var run = 1;
                    while (i + run < text.Length && char.ToLowerInvariant(text[i + run]) == lower) run++;
                    parts.Add(new Part(PartKind.DateCode, text.Substring(i, run), lower, run));
                    i += run - 1;
                    continue;
                }
                if (char.IsAsciiLetter(c))
                {
                    reason = $"'{c}' is not a format code; quote text that should show as it is.";
                    return null;
                }
                parts.Add(new Part(PartKind.Literal, c.ToString()));
            }
            return Classify(parts, out reason);
        }

        private static Section? Classify(List<Part> parts, out string? reason)
        {
            reason = null;
            var hasDate = parts.Any(p => p.Kind is PartKind.DateCode or PartKind.AmPm);
            var hasDigits = parts.Any(p => p.Kind == PartKind.Digit);
            var hasText = parts.Any(p => p.Kind == PartKind.TextValue);
            if (hasText && (hasDate || hasDigits))
            {
                reason = "@ cannot share a section with number or date codes.";
                return null;
            }
            if (hasText) return new Section(Literalise(parts, PartKind.Comma, PartKind.DecimalPoint, PartKind.Percent), SectionKind.Text);

            if (hasDate)
            {
                if (hasDigits || parts.Any(p => p.Kind is PartKind.Exponent or PartKind.Percent))
                {
                    reason = "fractional seconds and numbers inside a date or time format are not supported.";
                    return null;
                }
                var section = new Section(Literalise(parts, PartKind.Comma, PartKind.DecimalPoint), SectionKind.Date)
                {
                    _twelveHour = parts.Any(p => p.Kind == PartKind.AmPm),
                };
                return section;
            }

            if (!hasDigits)
            {
                return new Section(Literalise(parts, PartKind.Comma, PartKind.DecimalPoint), SectionKind.Literal)
                {
                    Percent = parts.Count(p => p.Kind == PartKind.Percent),
                };
            }

            // A number section: decide what each comma and point means.
            var result = new List<Part>();
            var number = new Section(result, SectionKind.Number);
            var seenPoint = false;
            var seenExponent = false;
            var lastDigit = parts.FindLastIndex(p => p.Kind == PartKind.Digit);
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                switch (part.Kind)
                {
                    case PartKind.Digit:
                        if (seenExponent) number._exponentPlaceholders++;
                        else if (seenPoint) number._fractionPlaceholders++;
                        else number._integerPlaceholders++;
                        result.Add(part);
                        break;
                    case PartKind.DecimalPoint:
                        if (seenPoint || seenExponent)
                        {
                            reason = "a number format has one decimal point.";
                            return null;
                        }
                        seenPoint = true;
                        result.Add(part);
                        break;
                    case PartKind.Comma:
                        var before = i > 0 && parts[i - 1].Kind is PartKind.Digit or PartKind.Comma;
                        var digitAfterInInteger = parts.Skip(i + 1).TakeWhile(p => p.Kind is not (PartKind.DecimalPoint or PartKind.Exponent)).Any(p => p.Kind == PartKind.Digit);
                        if (!seenPoint && !seenExponent && before && digitAfterInInteger)
                        {
                            number._thousands = true;
                        }
                        else if (!seenExponent && before && !digitAfterInInteger && (!seenPoint || i > lastDigit))
                        {
                            number._scale++;
                        }
                        else
                        {
                            result.Add(new Part(PartKind.Literal, ","));
                        }
                        break;
                    case PartKind.Percent:
                        number.Percent++;
                        result.Add(part);
                        break;
                    case PartKind.Exponent:
                        if (seenExponent || number._integerPlaceholders != 1)
                        {
                            reason = "scientific notation is supported with one integer placeholder, as in 0.00E+00.";
                            return null;
                        }
                        seenExponent = true;
                        result.Add(part);
                        break;
                    case PartKind.Literal when part.Text == "/" && parts.Skip(i + 1).Any(p => p.Kind == PartKind.Digit):
                        reason = "fractions are not supported.";
                        return null;
                    default:
                        result.Add(part);
                        break;
                }
            }
            if (seenExponent && number._exponentPlaceholders == 0)
            {
                reason = "the exponent needs a digit placeholder.";
                return null;
            }
            return number;
        }

        private static List<Part> Literalise(List<Part> parts, params PartKind[] kinds) =>
            [.. parts.Select(p => Array.IndexOf(kinds, p.Kind) >= 0 ? new Part(PartKind.Literal, p.Text) : p)];

        public string FormatText(string text)
        {
            var output = new StringBuilder();
            foreach (var part in _parts) output.Append(part.Kind == PartKind.TextValue ? text : part.Text);
            return output.ToString();
        }

        public (string Text, bool CannotShow) FormatNumber(double magnitude, bool minus, CultureInfo culture) => Kind switch
        {
            SectionKind.Date => FormatDate(magnitude, minus, culture),
            SectionKind.Literal => ((minus ? "-" : "") + string.Concat(_parts.Select(p => p.Text)), false),
            _ => (FormatDigits(magnitude, minus, culture), false),
        };

        private string FormatDigits(double magnitude, bool minus, CultureInfo culture)
        {
            var value = magnitude * Math.Pow(100, Percent) / Math.Pow(1000, _scale);
            string integerDigits;
            string fractionDigits;
            var exponent = 0;
            if (_exponentPlaceholders > 0)
            {
                (integerDigits, fractionDigits, exponent) = Scientific(value, _fractionPlaceholders);
            }
            else
            {
                var rounded = FunctionLibrary.RoundHalfAwayFromZero(value, _fractionPlaceholders);
                (integerDigits, fractionDigits) = Digits(rounded, _fractionPlaceholders);
            }

            var format = culture.NumberFormat;
            var output = new StringBuilder();
            if (minus) output.Append('-');

            // The integer placeholders take the digits right-aligned; extra digits go with the first.
            var integerOutput = new string[_integerPlaceholders];
            var digitIndex = integerDigits.Length;
            var integerParts = new List<Part>();
            foreach (var part in _parts)
            {
                if (part.Kind is PartKind.DecimalPoint or PartKind.Exponent) break;
                if (part.Kind == PartKind.Digit) integerParts.Add(part);
            }
            for (var k = integerParts.Count - 1; k >= 0; k--)
            {
                digitIndex--;
                string rendered;
                if (digitIndex >= 0)
                {
                    rendered = k == 0 ? integerDigits[..(digitIndex + 1)] : integerDigits[digitIndex].ToString();
                }
                else
                {
                    rendered = integerParts[k].Code switch
                    {
                        '0' => "0",
                        '?' => " ",
                        _ => "",
                    };
                }
                integerOutput[k] = rendered;
            }
            if (_thousands)
            {
                var joined = Group(string.Concat(integerOutput), format);
                for (var k = 0; k < integerOutput.Length; k++) integerOutput[k] = k == 0 ? joined : "";
            }

            // The fraction placeholders take the digits left-aligned; trailing zeros under # vanish, under ? become spaces.
            var fractionOutput = new string[_fractionPlaceholders];
            var fractionParts = new List<Part>();
            var afterPoint = false;
            foreach (var part in _parts)
            {
                if (part.Kind == PartKind.Exponent) break;
                if (part.Kind == PartKind.DecimalPoint) afterPoint = true;
                else if (afterPoint && part.Kind == PartKind.Digit) fractionParts.Add(part);
            }
            var significant = true;
            for (var k = fractionParts.Count - 1; k >= 0; k--)
            {
                var digit = fractionDigits[k];
                if (significant && digit == '0' && fractionParts[k].Code != '0')
                {
                    fractionOutput[k] = fractionParts[k].Code == '?' ? " " : "";
                    continue;
                }
                significant = false;
                fractionOutput[k] = digit.ToString();
            }

            var integerAt = 0;
            var fractionAt = 0;
            var exponentAt = 0;
            var exponentDigits = Math.Abs(exponent).ToString(CultureInfo.InvariantCulture).PadLeft(_exponentPlaceholders, '0');
            var inExponent = false;
            afterPoint = false;
            foreach (var part in _parts)
            {
                switch (part.Kind)
                {
                    case PartKind.Digit when inExponent:
                        // All exponent digits come out at the first exponent placeholder.
                        if (exponentAt++ == 0) output.Append(exponentDigits);
                        break;
                    case PartKind.Digit when afterPoint:
                        output.Append(fractionOutput[fractionAt++]);
                        break;
                    case PartKind.Digit:
                        output.Append(integerOutput[integerAt++]);
                        break;
                    case PartKind.DecimalPoint:
                        afterPoint = true;
                        output.Append(format.NumberDecimalSeparator);
                        break;
                    case PartKind.Exponent:
                        inExponent = true;
                        output.Append(part.Text[0]);
                        if (exponent < 0) output.Append('-');
                        else if (part.Code == '+') output.Append('+');
                        break;
                    default:
                        output.Append(part.Text);
                        break;
                }
            }
            return output.ToString();
        }

        private static string Group(string integer, NumberFormatInfo format)
        {
            var digits = integer.TrimStart(' ');
            var padding = integer[..(integer.Length - digits.Length)];
            if (digits.Length == 0) return integer;
            var sizes = format.NumberGroupSizes;
            var separator = format.NumberGroupSeparator;
            var groups = new List<string>();
            var end = digits.Length;
            var sizeIndex = 0;
            while (end > 0)
            {
                var size = sizes.Length == 0 ? 3 : sizes[Math.Min(sizeIndex, sizes.Length - 1)];
                if (size <= 0)
                {
                    groups.Add(digits[..end]);
                    break;
                }
                var start = Math.Max(0, end - size);
                groups.Add(digits[start..end]);
                end = start;
                sizeIndex++;
            }
            groups.Reverse();
            return padding + string.Join(separator, groups);
        }

        /// <summary>The integer and fraction digits of a value already rounded, from its 15 significant digits; zero's integer part is empty.</summary>
        private static (string Integer, string Fraction) Digits(double value, int fractionDigits)
        {
            if (value == 0) return ("", new string('0', fractionDigits));
            var spelled = value.ToString("E14", CultureInfo.InvariantCulture);
            var mantissa = spelled[0] + spelled[2..16];
            var exponent = int.Parse(spelled.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            string integer;
            string fraction;
            if (exponent >= 0)
            {
                integer = exponent + 1 >= 15 ? mantissa + new string('0', exponent + 1 - 15) : mantissa[..(exponent + 1)];
                fraction = exponent + 1 >= 15 ? "" : mantissa[(exponent + 1)..];
            }
            else
            {
                integer = "";
                fraction = new string('0', -exponent - 1) + mantissa;
            }
            fraction = fraction.Length >= fractionDigits ? fraction[..fractionDigits] : fraction.PadRight(fractionDigits, '0');
            return (integer, fraction);
        }

        /// <summary>One integer digit, the fraction digits rounded half away from zero, and the exponent.</summary>
        private static (string Integer, string Fraction, int Exponent) Scientific(double value, int fractionDigits)
        {
            if (value == 0) return ("0", new string('0', fractionDigits), 0);
            var spelled = value.ToString("E14", CultureInfo.InvariantCulture);
            var mantissa = spelled[0] + spelled[2..16];
            var exponent = int.Parse(spelled.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var keep = Math.Min(1 + fractionDigits, 15);
            var digits = mantissa[..keep].ToCharArray();
            if (keep < 15 && mantissa[keep] >= '5')
            {
                var i = keep - 1;
                while (i >= 0)
                {
                    if (digits[i] == '9')
                    {
                        digits[i] = '0';
                        i--;
                        continue;
                    }
                    digits[i]++;
                    break;
                }
                if (i < 0)
                {
                    digits = ['1', .. digits[..^1]];
                    exponent++;
                }
            }
            var all = new string(digits).PadRight(1 + fractionDigits, '0');
            return (all[..1], all[1..], exponent);
        }

        private (string Text, bool CannotShow) FormatDate(double magnitude, bool minus, CultureInfo culture)
        {
            if (minus || magnitude >= DateSerial.Maximum + 1) return ("", true);
            // To the second: a time is shown rounded to the nearest second.
            var totalSeconds = (long)Math.Round(magnitude * 86_400, MidpointRounding.AwayFromZero);
            var serial = (int)(totalSeconds / 86_400);
            if (serial > DateSerial.Maximum) return ("", true);
            var secondOfDay = (int)(totalSeconds % 86_400);
            var (year, month, day) = DateSerial.ToDate(serial);
            var hour = secondOfDay / 3600;
            var minute = secondOfDay / 60 % 60;
            var second = secondOfDay % 60;
            var names = culture.DateTimeFormat;

            var output = new StringBuilder();
            for (var i = 0; i < _parts.Count; i++)
            {
                var part = _parts[i];
                switch (part.Kind)
                {
                    case PartKind.DateCode:
                        switch (part.Code)
                        {
                            case 'y':
                                output.Append(part.Count <= 2 ? (year % 100).ToString("00", CultureInfo.InvariantCulture) : year.ToString("0000", CultureInfo.InvariantCulture));
                                break;
                            case 'm' when IsMinute(i):
                                output.Append(part.Count == 1 ? minute.ToString(CultureInfo.InvariantCulture) : minute.ToString("00", CultureInfo.InvariantCulture));
                                break;
                            case 'm':
                                output.Append(part.Count switch
                                {
                                    1 => month.ToString(CultureInfo.InvariantCulture),
                                    2 => month.ToString("00", CultureInfo.InvariantCulture),
                                    3 => names.AbbreviatedMonthNames[month - 1],
                                    4 => names.MonthNames[month - 1],
                                    _ => names.MonthNames[month - 1][..1],
                                });
                                break;
                            case 'd':
                                output.Append(part.Count switch
                                {
                                    1 => day.ToString(CultureInfo.InvariantCulture),
                                    2 => day.ToString("00", CultureInfo.InvariantCulture),
                                    3 => names.AbbreviatedDayNames[DateSerial.DayOfWeek(serial)],
                                    _ => names.DayNames[DateSerial.DayOfWeek(serial)],
                                });
                                break;
                            case 'h':
                                var shown = _twelveHour ? (hour % 12 == 0 ? 12 : hour % 12) : hour;
                                output.Append(part.Count == 1 ? shown.ToString(CultureInfo.InvariantCulture) : shown.ToString("00", CultureInfo.InvariantCulture));
                                break;
                            default:
                                output.Append(part.Count == 1 ? second.ToString(CultureInfo.InvariantCulture) : second.ToString("00", CultureInfo.InvariantCulture));
                                break;
                        }
                        break;
                    case PartKind.AmPm:
                        var afternoon = hour >= 12;
                        output.Append(part.Text.Length == 5
                            ? (afternoon ? part.Text[3..5] : part.Text[..2])
                            : (afternoon ? part.Text[2..3] : part.Text[..1]));
                        break;
                    default:
                        output.Append(part.Text);
                        break;
                }
            }
            return (output.ToString(), false);
        }

        /// <summary>An <c>m</c> is minutes right after an hour code or right before a seconds code, as in Excel.</summary>
        private bool IsMinute(int index)
        {
            for (var i = index - 1; i >= 0; i--)
            {
                if (_parts[i].Kind != PartKind.DateCode) continue;
                if (_parts[i].Code == 'h') return true;
                break;
            }
            for (var i = index + 1; i < _parts.Count; i++)
            {
                if (_parts[i].Kind != PartKind.DateCode) continue;
                return _parts[i].Code == 's';
            }
            return false;
        }
    }
}
