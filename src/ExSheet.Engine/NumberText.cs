using System.Globalization;
using System.Text;

namespace ExSheet.Engine;

/// <summary>Numbers written as text, as Excel writes them.</summary>
internal static class NumberText
{
    /// <summary>
    /// The most characters General shows, whatever the width, not counting a minus sign: a wide
    /// column shows <c>=1/3</c> as <c>0.333333333</c> and <c>123456789012</c> as
    /// <c>1.23457E+11</c>. Microsoft documents only that twelve or more digits go scientific; the
    /// eleven, and leaving the sign out of them, are uncertain in the case corpus (ADR-0047).
    /// </summary>
    public const int GeneralLimit = 11;

    /// <summary>
    /// Excel's General form in full: at most 15 significant digits (ADR-0047), the culture's
    /// decimal separator, and scientific notation for very large and very small magnitudes. It is
    /// the number as a text concatenation or the Cell Editor writes it, not fitted to any width.
    /// </summary>
    public static string General(double number, CultureInfo culture) => number.ToString("G15", culture);

    /// <summary>The most characters, not counting a minus sign, that a number written into text takes in full (<see cref="Written"/>).</summary>
    internal const int WrittenLongest = 20;

    /// <summary>The most characters a number constant in a Formula takes in full (<see cref="Written"/>).</summary>
    internal const int FormulaConstantLongest = 21;

    /// <summary>
    /// A number as Excel writes it into text — <c>=A1&amp;""</c>, and the Cell Editor's text of a
    /// typed number: at most 15 significant digits (ADR-0047) and the culture's decimal separator,
    /// written out in full where that takes at most <paramref name="longest"/> characters (a minus
    /// sign not counted), and in General's scientific spelling (<c>1E+20</c>,
    /// <c>1.23456789012345E-05</c>) where it would take more. Excel was observed to write into
    /// text <c>1E19</c> (20 characters) and <c>1E-10</c> in full, and <c>1E20</c> and
    /// <c>1.23456789012345E-5</c> (21 characters each) in scientific notation; a number constant in
    /// a Formula is written back in full up to 21 characters (<c>=1E20</c> is
    /// <c>=100000000000000000000</c>, <c>=1.23456789012345E-9</c>, 25, is
    /// <c>=1.23456789012345E-09</c>) (ADR-0047, second run). Whether the minus sign counts is
    /// <c>uncertain</c> in the case corpus.
    /// </summary>
    public static string Written(double number, CultureInfo culture, int longest = WrittenLongest)
    {
        if (number == 0) return "0";
        var e14 = Math.Abs(number).ToString("E14", CultureInfo.InvariantCulture);
        var digits = (e14[0] + e14[2..16]).TrimEnd('0');
        var exponent = int.Parse(e14.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var fullLength = exponent < 0
            ? 2 + (-exponent - 1) + digits.Length
            : Math.Max(digits.Length, exponent + 1) + (digits.Length > exponent + 1 ? 1 : 0);
        if (fullLength > longest) return General(number, culture);

        var text = new StringBuilder();
        if (number < 0) text.Append('-');
        if (exponent < 0)
        {
            text.Append('0').Append(culture.NumberFormat.NumberDecimalSeparator).Append('0', -exponent - 1).Append(digits);
        }
        else if (digits.Length <= exponent + 1)
        {
            text.Append(digits).Append('0', exponent + 1 - digits.Length);
        }
        else
        {
            text.Append(digits, 0, exponent + 1).Append(culture.NumberFormat.NumberDecimalSeparator).Append(digits, exponent + 1, digits.Length - exponent - 1);
        }
        return text.ToString();
    }

    /// <summary>
    /// Excel's General form fitted to a column <paramref name="characters"/> wide, each character
    /// charged one digit width (ADR-0047): decimals are rounded to what fits, and the form is
    /// scientific where the integer part does not fit, where it has twelve or more digits, or
    /// where the scientific form that fits is closer to the number than the decimal form that
    /// fits. The decimal form may round a number that is not zero to a bare <c>0</c>, as Excel was
    /// observed to show <c>=1/3</c> in a column one character wide (GW-017); a negative number
    /// rounded so keeps its minus sign, <c>-0</c>, and a column with no room for the sign shows
    /// <c>####</c> (GW-026, GW-027, observed in the second run). <see langword="null"/> when
    /// neither form fits: the cell shows <c>####</c>.
    /// </summary>
    public static string? General(double number, int characters, CultureInfo culture) =>
        GeneralForm(number, characters, culture)?.Text;

    /// <summary>
    /// Whether General, fitted to <paramref name="characters"/>, writes the number in scientific
    /// notation; <see langword="null"/> when it cannot be shown at that width.
    /// </summary>
    public static bool? IsScientific(double number, int characters) =>
        GeneralForm(number, characters, CultureInfo.InvariantCulture)?.Scientific;

    private static (string Text, bool Scientific)? GeneralForm(double number, int characters, CultureInfo culture)
    {
        var minus = number < 0;
        var limit = Math.Min(characters - (minus ? 1 : 0), GeneralLimit);
        if (limit < 1) return null;
        if (number == 0) return ("0", false);

        // Fifteen significant digits (ADR-0047), rounded again from there.
        var magnitude = Math.Abs(number);
        var e14 = magnitude.ToString("E14", CultureInfo.InvariantCulture);
        var digits = e14[0] + e14[2..16];
        var exponent = int.Parse(e14.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        var fixedForm = Decimal(digits, exponent, limit);
        var scientific = Scientific(digits, exponent, limit);

        string chosen;
        bool isScientific;
        if (fixedForm is not null && (scientific is null || Error(fixedForm, magnitude) <= Error(scientific, magnitude)))
        {
            (chosen, isScientific) = (fixedForm, false);
        }
        else if (scientific is not null)
        {
            (chosen, isScientific) = (scientific, true);
        }
        else
        {
            return null;
        }
        // A negative number rounded to a bare 0 keeps its minus sign, -0, as Excel's General was
        // observed to show it (GW-026, verification/2026-09-27-windows-excel-2).
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        if (separator != ".") chosen = chosen.Replace(".", separator, StringComparison.Ordinal);
        return ((minus ? "-" : "") + chosen, isScientific);
    }

    private static double Error(string invariant, double magnitude) =>
        Math.Abs(double.Parse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture) - magnitude);

    /// <summary>The decimal form, invariant, in at most <paramref name="limit"/> characters.</summary>
    private static string? Decimal(string digits, int exponent, int limit)
    {
        var integerDigits = exponent >= 0 ? exponent + 1 : 1;
        if (integerDigits > limit) return null;
        var fraction = Math.Max(0, limit - integerDigits - 1);
        var (rounded, at) = Round(digits, exponent, exponent + 1 + fraction);
        if (rounded.Length == 0) return "0";

        // rounded holds the significant digits, the first at 10^at.
        char DigitAt(int power)
        {
            var i = at - power;
            return i >= 0 && i < rounded.Length ? rounded[i] : '0';
        }

        var text = new StringBuilder();
        if (at < 0) text.Append('0');
        for (var p = at; p >= 0; p--) text.Append(DigitAt(p));
        var fractionText = new StringBuilder();
        for (var p = -1; p >= -fraction; p--) fractionText.Append(DigitAt(p));
        var trimmed = fractionText.ToString().TrimEnd('0');
        if (trimmed.Length > 0) text.Append('.').Append(trimmed);
        return text.Length <= limit ? text.ToString() : null;
    }

    /// <summary>The scientific form, invariant, in at most <paramref name="limit"/> characters.</summary>
    private static string? Scientific(string digits, int exponent, int limit)
    {
        for (var mantissaFraction = Math.Max(0, limit - 6); mantissaFraction >= 0; mantissaFraction--)
        {
            var (rounded, at) = Round(digits, exponent, 1 + mantissaFraction);
            var mantissa = rounded.TrimEnd('0');
            var text = new StringBuilder().Append(mantissa[0]);
            if (mantissa.Length > 1) text.Append('.').Append(mantissa, 1, mantissa.Length - 1);
            var power = Math.Abs(at);
            text.Append(at < 0 ? "E-" : "E+").Append(power.ToString(power >= 100 ? "000" : "00", CultureInfo.InvariantCulture));
            if (text.Length <= limit) return text.ToString();
        }
        return null;
    }

    /// <summary>
    /// <paramref name="digits"/> (the first at 10^<paramref name="exponent"/>) rounded half away
    /// from zero to <paramref name="keep"/> significant digits: the digits kept, and the power of
    /// ten of the first. Empty when the number rounds to zero.
    /// </summary>
    private static (string Digits, int At) Round(string digits, int exponent, int keep)
    {
        if (keep >= digits.Length) return (digits, exponent);
        if (keep < 0) return ("", exponent);
        if (keep == 0) return digits[0] >= '5' ? ("1", exponent + 1) : ("", exponent);
        var kept = digits[..keep].ToCharArray();
        if (digits[keep] >= '5')
        {
            var i = keep - 1;
            while (i >= 0 && kept[i] == '9')
            {
                kept[i] = '0';
                i--;
            }
            if (i < 0) return ("1" + new string(kept), exponent + 1);
            kept[i]++;
        }
        return (new string(kept), exponent);
    }
}
