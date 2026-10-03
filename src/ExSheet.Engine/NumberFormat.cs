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
/// The subset read: up to four sections (positive; negative; zero; text), where a text section
/// (<c>@</c>) may also end a format of two or three, as in <c>0;[Red]@</c>; the digit placeholders
/// <c>0 # ?</c>, the decimal point, thousands separators and scaling commas, <c>%</c>, scientific
/// <c>E+00</c> with one integer placeholder, <c>@</c>, quoted text, <c>\</c> escapes, <c>_</c>
/// spacing, and the date and time codes <c>y m d h s</c> with <c>AM/PM</c> and <c>A/P</c>. A
/// colour named at the start of a section (<c>[Red]</c>, <c>[Blue]</c>, …) is kept in the code,
/// so the format goes back to Excel intact, and formatting a Value answers it with the text when
/// that section shows the Value, to be painted over the Font colour
/// (<see cref="NumberFormatColour"/>, ADR-0071). A numbered colour (<c>[Color10]</c>, in any case
/// and any section) is refused, as Excel refused it (FMT-075..077, ADR-0047 third run). A code
/// outside the subset — conditions,
/// locales and elapsed time in brackets, fractions, fractional seconds, <c>*</c> fill, era codes,
/// unquoted letters — is refused rather than shown some other way.
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
        code = UpperCaseAmPm(code);
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
            if (section.Kind == SectionKind.Text && i < parts.Count - 1)
            {
                reason = "@ belongs in the last section.";
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
    internal bool ShowsNumbersAsGeneral => NumberSections == 0;

    /// <summary>
    /// How many sections show numbers. A text section (<c>@</c>) that ends a format of fewer than
    /// four shows none, so <c>0;[Red]@</c> shows every number by its first section, as a format
    /// of one section does (case 3b of the eleventh Windows run, ADR-0071).
    /// </summary>
    private int NumberSections => _sections.Length is > 0 and < 4 && _sections[^1].Kind == SectionKind.Text ? _sections.Length - 1 : _sections.Length;

    /// <summary>The section that shows text: the fourth, or a text section that ends a shorter format; <see langword="null"/> when there is none.</summary>
    private Section? TextSection => _sections.Length == 4 ? _sections[3] : _sections.Length > 0 && _sections[^1].Kind == SectionKind.Text ? _sections[^1] : null;

    /// <summary>
    /// A Value as this format shows it in a column <paramref name="characters"/> wide, each
    /// character charged one digit width (ADR-0047): a number in General is fitted to the width
    /// as Excel's General fits it, and any other number whose text is longer than the width
    /// cannot be shown (<c>####</c>, ADR-0016). Text, booleans and Error Values are never fitted.
    /// The colour is the section's, as <see cref="Format(Value, CultureInfo)"/> answers it, and a
    /// number that cannot be shown keeps it; General fitted to the width uses no section and has none.
    /// </summary>
    internal (string Text, bool CannotShow, NumberFormatColour? Colour) Format(Value value, CultureInfo culture, int characters)
    {
        if (value.Kind != ValueKind.Number) return Format(value, culture);
        if (ShowsNumbersAsGeneral)
        {
            return NumberText.General(value.Number, characters, culture) is { } fitted ? (fitted, false, null) : ("", true, null);
        }
        var (text, cannotShow, colour) = Format(value, culture);
        return cannotShow || text.Length > characters ? ("", true, colour) : (text, false, colour);
    }

    /// <summary>
    /// Excel's built-in short date, <c>m/d/yyyy</c> in the invariant codes: what a typed date
    /// records under every culture, as Excel was observed to record it (en-US, en-GB, de-DE and
    /// ja-JP alike). It is not the pattern it spells: it shows a date in the Sheet culture's own
    /// short-date pattern (<c>2026/09/26</c> under ja-JP, <c>26.09.2026</c> under de-DE), as
    /// Excel's built-in format 14 shows in the system's pattern.
    /// </summary>
    internal static NumberFormat ShortDate { get; } = Parse(ShortDateCode);

    /// <summary>Excel's built-in short date with a time (<c>m/d/yyyy h:mm</c>, format 22), which shows the culture's short date and then the time.</summary>
    internal static NumberFormat ShortDateTime { get; } = Parse(ShortDateTimeCode);

    private const string ShortDateCode = "m/d/yyyy";
    private const string ShortDateTimeCode = "m/d/yyyy h:mm";

    private static readonly Dictionary<(string Culture, bool Time), NumberFormat> ShortDates = [];

    /// <summary>Excel's built-in day and month name (<c>d-mmm</c>, format 16), which a date typed without its year records.</summary>
    private const string DayMonthCode = "d-mmm";

    /// <summary>
    /// The built-in <c>d-mmm</c> as it shows under de-DE: <c>dd. mmm</c>, as Excel was observed to
    /// show <c>26-Okt</c> and <c>5-Okt</c> typed there, <c>26. Okt</c> and <c>05. Okt</c>
    /// (TYPED-040, TYPED-050; ADR-0047 second and third runs), German Excel's <c>TT. MMM</c>.
    /// </summary>
    private static readonly NumberFormat GermanDayMonth = Parse("dd. mmm");

    /// <summary>
    /// The built-in <c>d-mmm</c> under a culture whose short date writes the day with two digits:
    /// <c>dd-mmm</c>, as Excel was observed to show <c>5-Oct</c> typed under en-GB,
    /// <c>05-Oct</c> (TYPED-051, ADR-0047 third run).
    /// </summary>
    private static readonly NumberFormat TwoDigitDayMonth = Parse("dd-mmm");

    /// <summary>
    /// The built-in <c>d-mmm</c> in the culture's own form (ADR-0047, third run): German Excel's
    /// under de-DE, the day with two digits where the culture's short date writes it so (en-GB),
    /// and as it is spelled otherwise (observed under en-US); <see langword="null"/> for any other
    /// format, or where the culture's form is the code itself.
    /// </summary>
    private NumberFormat? DayMonthIn(CultureInfo culture)
    {
        if (!string.Equals(Code, DayMonthCode, StringComparison.OrdinalIgnoreCase)) return null;
        if (culture.Name == "de-DE") return GermanDayMonth;
        return culture.DateTimeFormat.ShortDatePattern.Contains("dd", StringComparison.Ordinal) ? TwoDigitDayMonth : null;
    }

    /// <summary>
    /// The pattern the built-in short date (and short date with time, and <c>d-mmm</c>) shows in under
    /// <paramref name="culture"/>; <see langword="null"/> for any other format, or where the
    /// culture's pattern is the code itself.
    /// </summary>
    private NumberFormat? ShortDateIn(CultureInfo culture)
    {
        if (DayMonthIn(culture) is { } dayMonth) return dayMonth;
        var time = string.Equals(Code, ShortDateTimeCode, StringComparison.OrdinalIgnoreCase);
        if (!time && !string.Equals(Code, ShortDateCode, StringComparison.OrdinalIgnoreCase)) return null;
        lock (ShortDates)
        {
            if (!ShortDates.TryGetValue((culture.Name, time), out var local))
            {
                var code = ConstantParser.DateCode(culture) + (time ? " h:mm" : "");
                local = string.Equals(code, Code, StringComparison.Ordinal) || !TryParse(code, out var parsed, out _) ? this : parsed;
                ShortDates[(culture.Name, time)] = local;
            }
            return ReferenceEquals(local, this) || string.Equals(local.Code, Code, StringComparison.Ordinal) ? null : local;
        }
    }

    /// <summary>
    /// Excel's built-in date with the month's name (<c>d-mmm-yy</c>, format 15): what Ctrl+#
    /// records under every culture, and what a date typed with a month name and a year records.
    /// </summary>
    private const string DayMonthYearCode = "d-mmm-yy";

    /// <summary>Excel's built-in hour and minute (<c>h:mm</c>, format 20): what Ctrl+Shift+@ records where the culture's own time is 24-hour.</summary>
    private const string HourMinuteCode = "h:mm";

    private static readonly Dictionary<(string Culture, string Code), NumberFormat> DatesAndTimes = [];

    /// <summary>
    /// The built-ins 15 and 20 in <paramref name="culture"/>'s own form, as the twelfth Windows run
    /// read them (ADR-0071, case 19); <see langword="null"/> for any other format, or where the
    /// culture's form is the code itself. Like the built-in short date and currency, each is
    /// recorded in its invariant code and is not the pattern it spells.
    /// <list type="bullet">
    /// <item><c>d-mmm-yy</c> writes the day with two digits where the culture's short date does, as
    /// the built-in <c>d-mmm</c> does: <c>05-Jan-26</c> under en-GB, <c>5-Jan-26</c> under en-US,
    /// and <c>05-1-26</c> under ja-JP, where <c>mmm</c> is the month's number
    /// (<see cref="AbbreviatedMonthNamesOf"/>).</item>
    /// <item><c>h:mm</c> writes the hour with two digits where the culture's short time does:
    /// <c>09:05</c> under en-GB, <c>9:05</c> under en-US and ja-JP.</item>
    /// </list>
    /// </summary>
    private NumberFormat? DateOrTimeIn(CultureInfo culture)
    {
        string code;
        if (string.Equals(Code, DayMonthYearCode, StringComparison.OrdinalIgnoreCase))
        {
            var day = culture.DateTimeFormat.ShortDatePattern.Contains("dd", StringComparison.Ordinal) ? "dd" : "d";
            code = $"{day}-mmm-yy";
        }
        else if (string.Equals(Code, HourMinuteCode, StringComparison.OrdinalIgnoreCase))
        {
            var time = culture.DateTimeFormat.ShortTimePattern;
            code = time.Contains("HH", StringComparison.Ordinal) || time.Contains("hh", StringComparison.Ordinal) ? "hh:mm" : HourMinuteCode;
        }
        else
        {
            return null;
        }
        if (string.Equals(code, Code, StringComparison.Ordinal)) return null;
        lock (DatesAndTimes)
        {
            if (!DatesAndTimes.TryGetValue((culture.Name, code), out var local))
            {
                local = Parse(code);
                DatesAndTimes[(culture.Name, code)] = local;
            }
            return local;
        }
    }

    /// <summary>
    /// Excel's built-in currency format under <paramref name="culture"/>: what Ctrl+Shift+$
    /// records (ADR-0071; the eleventh Windows run, cases 18 and 20). It is format 8,
    /// <c>$#,##0.00_);[Red]($#,##0.00)</c> in the invariant codes, or format 6,
    /// <c>$#,##0_);[Red]($#,##0)</c>, where the culture's currency has no decimals, as Excel chose
    /// under ja-JP. Like the built-in short date, it is recorded in those codes and is not the
    /// pattern they spell: it shows in the culture's own currency, <c>£1,234.50</c> under en-GB
    /// and <c>¥1,235</c> under ja-JP, its negative section red.
    /// </summary>
    public static NumberFormat BuiltInCurrency(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.NumberFormat.CurrencyDecimalDigits == 0 ? WholeCurrency : Currency;
    }

    private const string CurrencyCode = "$#,##0.00_);[Red]($#,##0.00)";
    private const string WholeCurrencyCode = "$#,##0_);[Red]($#,##0)";
    private static readonly NumberFormat Currency = Parse(CurrencyCode);
    private static readonly NumberFormat WholeCurrency = Parse(WholeCurrencyCode);
    private static readonly Dictionary<(string Culture, bool Decimals), NumberFormat> Currencies = [];

    /// <summary>
    /// The built-in currency format (<see cref="BuiltInCurrency"/>) in <paramref name="culture"/>'s
    /// own form; <see langword="null"/> for any other format, or where the culture's form is the
    /// code itself (en-US).
    /// </summary>
    private NumberFormat? CurrencyIn(CultureInfo culture)
    {
        var decimals = string.Equals(Code, CurrencyCode, StringComparison.OrdinalIgnoreCase);
        if (!decimals && !string.Equals(Code, WholeCurrencyCode, StringComparison.OrdinalIgnoreCase)) return null;
        lock (Currencies)
        {
            if (!Currencies.TryGetValue((culture.Name, decimals), out var local))
            {
                var code = LocalCurrencyCode(culture, decimals);
                local = string.Equals(code, Code, StringComparison.Ordinal) || !TryParse(code, out var parsed, out _) ? this : parsed;
                Currencies[(culture.Name, decimals)] = local;
            }
            return ReferenceEquals(local, this) || string.Equals(local.Code, Code, StringComparison.Ordinal) ? null : local;
        }
    }

    /// <summary>
    /// The built-ins shown in the Sheet culture's own form (<see cref="LocalIn"/>): the short date
    /// and the short date with a time (14 and 22), the day and month (16), the day, month and year
    /// (15), the hour and minute (20), and the currency (6 and 8).
    /// </summary>
    private static readonly NumberFormat[] LocalisedBuiltIns =
        [ShortDate, ShortDateTime, Parse(DayMonthCode), Parse(DayMonthYearCode), Parse(HourMinuteCode), Currency, WholeCurrency];

    /// <summary>
    /// The form this format shows in under <paramref name="culture"/> where it is a built-in shown in
    /// the culture's own form; <see langword="null"/> for any other format, or where the culture's
    /// form is the code itself.
    /// </summary>
    private NumberFormat? LocalIn(CultureInfo culture) => ShortDateIn(culture) ?? DateOrTimeIn(culture) ?? CurrencyIn(culture);

    /// <summary>
    /// The code as Format Cells' Custom box spells it under <paramref name="culture"/>, as Excel's
    /// local code does (ADR-0071; the fourteenth Windows run, case 11). A built-in shown in the
    /// culture's own form is spelled in that form: the built-in <c>d-mmm-yy</c> is
    /// <c>dd-mmm-yy</c> under ja-JP and en-GB. A code of its own that spells such a built-in's code
    /// (<see cref="TryParseLocal"/>) is spelled as it was typed, and any other code as it is.
    /// <see cref="TryParseLocal"/> reads the spelling back as this format under the same culture.
    /// The one exception is a code of its own under a culture that spells the built-in the same way,
    /// where the two show alike: typed again, it is the built-in, as the rule reads it.
    /// </summary>
    public string LocalCode(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return LocalIn(culture)?.Code ?? BuiltInSpelled(Code) ?? Code;
    }

    /// <summary>
    /// Reads a code typed into Format Cells' Custom box under <paramref name="culture"/> (ADR-0071;
    /// the fourteenth Windows run, case 11). A code is a built-in only when it spells that
    /// built-in's code under the culture (<see cref="LocalCode"/>), in any case. Under ja-JP,
    /// <c>dd-mmm-yy</c> is the built-in <c>d-mmm-yy</c> and shows <c>05-1-26</c>, and
    /// <c>d-mmm-yy</c> is a code of its own and shows <c>5-1-26</c>, as Excel showed them. Under
    /// en-US the two spellings are one, and <c>d-mmm-yy</c> is the built-in.
    /// </summary>
    /// <remarks>
    /// A code of its own that spells a built-in's invariant code is recorded with its separators
    /// escaped, <c>d\-mmm\-yy</c>: the same code to Excel, shown as it is spelled under every
    /// culture, and no built-in's, so a Sheet Document keeps it apart from the built-in. Any other
    /// code is read as <see cref="TryParse"/> reads it.
    /// </remarks>
    public static bool TryParseLocal(string code, CultureInfo culture, [NotNullWhen(true)] out NumberFormat? format, [NotNullWhen(false)] out string? reason)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (!TryParse(code, out format, out reason)) return false;
        foreach (var builtIn in LocalisedBuiltIns)
        {
            if (string.Equals(format.Code, builtIn.LocalCode(culture), StringComparison.OrdinalIgnoreCase))
            {
                format = builtIn;
                return true;
            }
        }
        // The engine would show it as the built-in it spells, in the culture's own form.
        if (format.LocalIn(culture) is not null) format = Parse(OwnSpelling(format.Code));
        return true;
    }

    /// <summary>
    /// Reads a code as <c>TEXT</c> reads it (ADR-0120): the invariant spelling under every culture,
    /// and as written — a code that spells a built-in shown in the culture's own form
    /// (<see cref="LocalIn"/>) is a code of its own, so <c>m/d/yyyy</c> shows <c>9/26/2026</c> under
    /// ja-JP, where the built-in shows <c>2026/09/26</c>. <see langword="false"/> where
    /// <see cref="TryParse"/> refuses the code.
    /// </summary>
    internal static bool TryParseAsWritten(string code, [NotNullWhen(true)] out NumberFormat? format)
    {
        if (!TryParse(code, out format, out _)) return false;
        var written = format.Code;
        if (LocalisedBuiltIns.Any(builtIn => string.Equals(builtIn.Code, written, StringComparison.OrdinalIgnoreCase))) format = Parse(OwnSpelling(written));
        return true;
    }

    /// <summary>
    /// A built-in's invariant code as a code of its own: each separator escaped, so that it shows
    /// as it is spelled and is no built-in's. A built-in's code holds no quote or backslash.
    /// </summary>
    private static string OwnSpelling(string builtIn)
    {
        var spelled = new StringBuilder(builtIn.Length * 2);
        foreach (var c in builtIn)
        {
            if (c is '-' or '/' or ':' or '$') spelled.Append('\\');
            spelled.Append(c);
        }
        return spelled.ToString();
    }

    /// <summary>The built-in's code a code of its own spells (<see cref="OwnSpelling"/>), or <see langword="null"/> when it is not one.</summary>
    private static string? BuiltInSpelled(string code)
    {
        if (!code.Contains('\\')) return null;
        var unescaped = code.Replace("\\", "", StringComparison.Ordinal);
        return string.Equals(OwnSpelling(unescaped), code, StringComparison.Ordinal)
            && LocalisedBuiltIns.Any(builtIn => string.Equals(builtIn.Code, unescaped, StringComparison.OrdinalIgnoreCase))
            ? unescaped
            : null;
    }

    /// <summary>
    /// The abbreviated month names Excel shows under <paramref name="culture"/>, which <c>mmm</c>
    /// shows: the culture's own, with the two differences found between the ICU data .NET reads and
    /// Windows' regional settings, which Excel reads. Windows' are taken everywhere, so a Sheet's
    /// text does not differ by the machine it ran on.
    /// <list type="bullet">
    /// <item>ICU on Linux (Ubuntu 24.04's) abbreviates September as <c>Sept</c> under en-GB and its
    /// kin, where Windows and macOS write <c>Sep</c> (the twelfth Windows run, case 19).</item>
    /// <item>ICU abbreviates a Japanese month as its full name, <c>1月</c>, on every platform, where
    /// Windows writes the month's number alone, <c>1</c>. So under ja-JP <c>mmm</c> shows the month as
    /// a number with no leading zero, in every code, and <c>mmmm</c> shows <c>1月</c> (the fourteenth
    /// Windows run, cases 10 and 11).</item>
    /// </list>
    /// </summary>
    public static string[] AbbreviatedMonthNamesOf(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var names = (string[])culture.DateTimeFormat.AbbreviatedMonthNames.Clone();
        var japanese = culture.TwoLetterISOLanguageName == "ja";
        for (var i = 0; i < names.Length; i++)
        {
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);
            if (names[i] == "Sept") names[i] = "Sep";
            else if (japanese && names[i] == number + "月") names[i] = number;
        }
        return names;
    }

    /// <summary>
    /// The currency symbol Excel shows under <paramref name="culture"/>: the culture's own, with
    /// the one difference the runs found between the data .NET reads and Windows' regional
    /// settings. Windows' ja-JP symbol, which Excel shows (the eleventh Windows run, case 20), is
    /// the yen sign U+00A5. The ICU data .NET reads on Linux (Ubuntu 24.04's) gives the full-width
    /// U+FFE5, and macOS's gives U+00A5, so a Sheet's text would differ by the machine it ran on.
    /// Windows' sign is taken everywhere.
    /// </summary>
    public static string CurrencySymbolOf(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.NumberFormat.CurrencySymbol.Replace('\uFFE5', '\u00A5');
    }

    /// <summary>
    /// The built-in currency format as Excel spells it under <paramref name="culture"/>, built from
    /// the culture's currency symbol and where it puts the symbol and the sign, as Windows' regional
    /// settings give Excel the same three: <c>£#,##0.00;[Red]-£#,##0.00</c> under en-GB and
    /// <c>¥#,##0;[Red]-¥#,##0</c> under ja-JP, as the eleventh Windows run read them (case 20). A
    /// negative amount in parentheses pads the positive one by a parenthesis, as the invariant code
    /// does. A symbol holding anything but currency signs is quoted, so its letters are not read as
    /// codes.
    /// </summary>
    private static string LocalCurrencyCode(CultureInfo culture, bool decimals)
    {
        var info = culture.NumberFormat;
        var currencySymbol = CurrencySymbolOf(culture);
        var symbol = currencySymbol.All(c => char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol)
            ? currencySymbol
            : "\"" + currencySymbol.Replace("\"", "", StringComparison.Ordinal) + "\"";
        var n = decimals ? "#,##0.00" : "#,##0";
        // Windows' regional default for en-US writes a negative amount in parentheses, and Excel
        // follows it (case 20: $#,##0.00_);[Red]($#,##0.00)). .NET's ICU data writes it with a
        // minus. Elsewhere .NET's data is read, and it agreed with Excel under en-GB and ja-JP.
        var negativePattern = culture.Name == "en-US" ? 0 : info.CurrencyNegativePattern;
        var negative = negativePattern switch
        {
            0 => $"({symbol}{n})",
            2 => $"{symbol}-{n}",
            3 => $"{symbol}{n}-",
            4 => $"({n}{symbol})",
            5 => $"-{n}{symbol}",
            6 => $"{n}-{symbol}",
            7 => $"{n}{symbol}-",
            8 => $"-{n} {symbol}",
            9 => $"-{symbol} {n}",
            10 => $"{n} {symbol}-",
            11 => $"{symbol} {n}-",
            12 => $"{symbol} -{n}",
            13 => $"{n}- {symbol}",
            14 => $"({symbol} {n})",
            15 => $"({n} {symbol})",
            16 => $"{symbol}- {n}",
            _ => $"-{symbol}{n}",
        };
        var positive = info.CurrencyPositivePattern switch
        {
            0 => $"{symbol}{n}",
            1 => $"{n}{symbol}",
            2 => $"{symbol} {n}",
            _ => $"{n} {symbol}",
        };
        if (negative.EndsWith(')')) positive += "_)";
        return $"{positive};[Red]{negative}";
    }

    /// <summary>
    /// A Value as this format shows it under <paramref name="culture"/>, and the colour the
    /// section that showed it names (ADR-0071, SH-40), or <see langword="null"/> where it names
    /// none or no section showed the Value: General, booleans and Error Values, which show as
    /// themselves, and text in a format with no text section.
    /// </summary>
    internal (string Text, bool CannotShow, NumberFormatColour? Colour) Format(Value value, CultureInfo culture)
    {
        if (value.Kind == ValueKind.Number && LocalIn(culture) is { } local) return local.Format(value, culture);
        switch (value.Kind)
        {
            case ValueKind.Boolean:
            case ValueKind.Error:
                return (value.ToString(), false, null);
            case ValueKind.Text:
                var textSection = TextSection;
                return textSection is null ? (value.Text, false, null) : (textSection.FormatText(value.Text), false, textSection.Colour);
        }

        var number = value.Number;
        if (ShowsNumbersAsGeneral)
        {
            return (NumberText.General(number, culture), false, null);
        }
        Section chosen;
        var automaticMinus = false;
        if (number < 0 && NumberSections >= 2)
        {
            chosen = _sections[1];
        }
        else if (number == 0 && NumberSections >= 3)
        {
            chosen = _sections[2];
        }
        else
        {
            chosen = _sections[0];
            automaticMinus = number < 0;
        }
        var (text, cannotShow) = chosen.FormatNumber(Math.Abs(number), automaticMinus, culture);
        return (text, cannotShow, chosen.Colour);
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

    /// <summary>
    /// <c>AM/PM</c> written in any case is <c>AM/PM</c>: Excel rewrites <c>h:mm am/pm</c> to
    /// <c>h:mm AM/PM</c> and shows <c>6:00 PM</c> (FMT-065). Quoted and escaped text is left alone.
    /// </summary>
    private static string UpperCaseAmPm(string code)
    {
        StringBuilder? rewritten = null;
        for (var i = 0; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '"':
                    var close = code.IndexOf('"', i + 1);
                    if (close < 0) return rewritten?.ToString() ?? code;
                    i = close;
                    continue;
                case '[':
                    var end = code.IndexOf(']', i + 1);
                    if (end < 0) return rewritten?.ToString() ?? code;
                    i = end;
                    continue;
                case '\\':
                case '_':
                case '*':
                    i++;
                    continue;
            }
            if (string.Compare(code, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) != 0) continue;
            if (string.CompareOrdinal(code, i, "AM/PM", 0, 5) != 0)
            {
                rewritten ??= new StringBuilder(code);
                rewritten.Remove(i, 5).Insert(i, "AM/PM");
            }
            i += 4;
        }
        return rewritten?.ToString() ?? code;
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
                case '[':
                    var end = code.IndexOf(']', i + 1);
                    if (end < 0)
                    {
                        reason = "a bracket is not closed.";
                        return null;
                    }
                    i = end;
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

        /// <summary>The colour named at the start of the section, or <see langword="null"/>.</summary>
        public NumberFormatColour? Colour { get; private set; }

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
            NumberFormatColour? colour = null;
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
                        var bracketEnd = text.IndexOf(']', i + 1);
                        if (bracketEnd > i && text[(i + 1)..bracketEnd].StartsWith("Color", StringComparison.OrdinalIgnoreCase))
                        {
                            reason = "a numbered colour ([Color n]) is refused, as Excel refuses it; name one of the eight colours instead.";
                            return null;
                        }
                        if (bracketEnd > i && NumberFormatColours.Named(text[(i + 1)..bracketEnd]) is { } named)
                        {
                            // The section's colour (ADR-0071). Excel reads one at the start of a
                            // section; anywhere else it is refused.
                            if (parts.Count > 0 || colour is not null)
                            {
                                reason = "a colour is read only once, at the start of its section.";
                                return null;
                            }
                            colour = named;
                            i = bracketEnd;
                            continue;
                        }
                        reason = "conditions, locales and elapsed time in brackets are not supported.";
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
            var section = Classify(parts, out reason);
            if (section is not null) section.Colour = colour;
            return section;
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
                // A negative number that rounds to zero shows no minus sign: -0.001 in 0.00 is
                // 0.00, as Excel was observed to show it (FMT-063).
                if (rounded == 0) minus = false;
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
                                    3 => AbbreviatedMonthNamesOf(culture)[month - 1],
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
