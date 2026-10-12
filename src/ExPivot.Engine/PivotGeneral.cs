using System.Globalization;
using System.Text;

namespace ExPivot.Engine;

/// <summary>
/// Excel's General as a PivotTable's value cell shows it (ADR-0060, read in the seventeenth Windows
/// run): at most eleven characters besides a minus sign, whatever the column's width. Decimals are
/// rounded to what fits, and the form is scientific where the integer part has twelve or more
/// digits, or where the scientific form is closer to the number than the decimal form that fits:
/// <c>1.414213562</c>, <c>1.23456789</c>, <c>1.23457E+11</c>, <c>1.23457E-05</c>.
///
/// <para>It is the rule ExSheet's General fits a cell to at a column eleven characters wide or wider
/// (<c>ExSheet.Engine.NumberText.General(number, characters, culture)</c>, ADR-0047, SH-20), ported
/// because the engine references no package but <c>ExGrid.Data</c> (PV-1). Its tests pin it to the
/// same cases, ExSheet's general-width corpus included, so the two cannot drift apart silently. A
/// column narrower than the text is ExGrid's <c>####</c> (ADR-0016): a pivot never fits General to a
/// column the way a cell does.</para>
/// </summary>
internal static class PivotGeneral
{
    /// <summary>The most characters General shows, not counting a minus sign.</summary>
    public const int Characters = 11;

    /// <summary>A <c>double</c> in General, under the culture's decimal separator.</summary>
    public static string Text(double number, CultureInfo culture)
    {
        if (!double.IsFinite(number)) return number.ToString(culture);
        var magnitude = Math.Abs(number);
        return Form(number < 0, magnitude.ToString("E14", CultureInfo.InvariantCulture),
            (decimalForm, scientific) => Error(decimalForm, magnitude) <= Error(scientific, magnitude), culture);
    }

    /// <summary>
    /// An exact <c>decimal</c> in General: its own digits are rounded, never a <c>double</c>'s, so a
    /// sum of money is shown from the sum itself (ADR-0060, "Money stays exact").
    /// </summary>
    public static string Text(decimal number, CultureInfo culture)
    {
        var magnitude = Math.Abs(number);
        return Form(number < 0, magnitude.ToString("E14", CultureInfo.InvariantCulture),
            (decimalForm, scientific) => Error(decimalForm, magnitude) <= Error(scientific, magnitude), culture);
    }

    /// <summary>The form of a magnitude written <c>d.ddddddddddddddE±xxx</c>: fifteen significant
    /// digits (ADR-0047), rounded again from there, as ExSheet's General rounds them.</summary>
    private static string Form(bool minus, string e14, Func<string, string, bool> decimalIsCloser, CultureInfo culture)
    {
        var digits = e14[0] + e14[2..16];
        if (digits.All(d => d == '0')) return "0";
        var exponent = int.Parse(e14.AsSpan(17), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        var decimalForm = Decimal(digits, exponent, Characters);
        // Eleven characters always hold a scientific form: the longest exponent, E-324, takes five.
        var scientific = Scientific(digits, exponent, Characters)!;
        var chosen = decimalForm is not null && decimalIsCloser(decimalForm, scientific) ? decimalForm : scientific;

        var separator = culture.NumberFormat.NumberDecimalSeparator;
        if (separator != ".") chosen = chosen.Replace(".", separator, StringComparison.Ordinal);
        return (minus ? "-" : "") + chosen;
    }

    private static double Error(string invariant, double magnitude) =>
        Math.Abs(double.Parse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture) - magnitude);

    private static decimal Error(string invariant, decimal magnitude) =>
        Math.Abs(decimal.Parse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture) - magnitude);

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
