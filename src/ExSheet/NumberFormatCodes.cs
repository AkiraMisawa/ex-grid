using System.Globalization;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// The format codes the Number tab's categories write, and how a code is recognised as one of them
/// when Format Cells opens (ADR-0063). Every code here is one the engine reads; a code that matches
/// none of them opens under Custom, as it is.
/// </summary>
internal static class NumberFormatCodes
{
    /// <summary>The most decimal places Excel's spinner offers.</summary>
    public const int MostDecimalPlaces = 30;

    /// <summary>The Date category's formats: Excel's built-in short date first, shown in the culture's own pattern.</summary>
    public static IReadOnlyList<string> DateTypes { get; } =
        ["m/d/yyyy", "d-mmm-yy", "d-mmm", "mmm-yy", "mmmm d, yyyy", "d mmmm yyyy", "yyyy-mm-dd", "dd/mm/yyyy"];

    /// <summary>The Time category's formats.</summary>
    public static IReadOnlyList<string> TimeTypes { get; } =
        ["h:mm", "h:mm AM/PM", "h:mm:ss", "h:mm:ss AM/PM", "mm:ss", "m/d/yyyy h:mm"];

    /// <summary>The codes Custom lists to start from.</summary>
    public static IReadOnlyList<string> CustomTypes { get; } =
        ["General", "0", "0.00", "#,##0", "#,##0.00", "0%", "0.00%", "0.00E+00", "@", .. DateTypes, .. TimeTypes];

    /// <summary>How many ways a negative number shows under Number: <c>-1234.10</c>, in red, in brackets, in red brackets.</summary>
    public const int NumberNegativeStyles = 4;

    /// <summary>How many ways a negative amount shows under Currency: with a minus, in red, in red with a minus, in brackets, in red brackets.</summary>
    public const int CurrencyNegativeStyles = 5;

    /// <summary>The digits of a number: with or without the thousands separator, and the decimal places.</summary>
    private static string Digits(int places, bool separator) =>
        (separator ? "#,##0" : "0") + (places > 0 ? "." + new string('0', places) : "");

    /// <summary>The Number category's code.</summary>
    public static string Number(int places, bool separator, int negative)
    {
        var body = Digits(places, separator);
        return negative switch
        {
            0 => body,
            1 => $"{body};[Red]{body}",
            2 => $"{body}_);({body})",
            3 => $"{body}_);[Red]({body})",
            _ => throw new ArgumentOutOfRangeException(nameof(negative), negative, "Not one of Number's negative styles."),
        };
    }

    /// <summary>The Currency category's code, with the culture's symbol on the side and at the distance the culture puts it.</summary>
    public static string Currency(int places, int negative, CultureInfo culture)
    {
        var body = Symbolised(Digits(places, separator: true), culture.NumberFormat);
        return negative switch
        {
            0 => body,
            1 => $"{body};[Red]{body}",
            2 => $"{body};[Red]-{body}",
            3 => $"{body}_);({body})",
            4 => $"{body}_);[Red]({body})",
            _ => throw new ArgumentOutOfRangeException(nameof(negative), negative, "Not one of Currency's negative styles."),
        };
    }

    /// <summary>The Percentage category's code.</summary>
    public static string Percentage(int places) => Digits(places, separator: false) + "%";

    /// <summary>The Scientific category's code.</summary>
    public static string Scientific(int places) => Digits(places, separator: false) + "E+00";

    /// <summary>
    /// The culture's currency symbol beside <paramref name="digits"/>, where the culture puts it. A
    /// symbol of currency signs alone (<c>$</c>, <c>£</c>, <c>¥</c>, <c>€</c>) is written as it is,
    /// as Excel writes it; any other is quoted, so its letters are not read as codes.
    /// </summary>
    private static string Symbolised(string digits, NumberFormatInfo format)
    {
        var symbol = format.CurrencySymbol.All(c => char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol)
            ? format.CurrencySymbol
            : "\"" + format.CurrencySymbol.Replace("\"", "", StringComparison.Ordinal) + "\"";
        return format.CurrencyPositivePattern switch
        {
            1 => digits + symbol,
            2 => symbol + " " + digits,
            3 => digits + " " + symbol,
            _ => symbol + digits,
        };
    }

    /// <summary>The culture's default decimal places for a category, as Excel offers them.</summary>
    public static int DefaultPlaces(NumberFormatCategory category, CultureInfo culture) =>
        category == NumberFormatCategory.Currency ? culture.NumberFormat.CurrencyDecimalDigits : 2;

    /// <summary>What a category shows a code as when Format Cells opens on it.</summary>
    public readonly record struct Recognised(NumberFormatCategory Category, int Places, bool Separator, int Negative, string Type);

    /// <summary>
    /// The category, and its options, that writes <paramref name="format"/>'s code exactly; Custom
    /// with the code as it is when none does.
    /// </summary>
    public static Recognised Recognise(NumberFormat format, CultureInfo culture)
    {
        var code = format.Code;
        if (format.IsGeneral) return new(NumberFormatCategory.General, 2, false, 0, code);
        if (code == "@") return new(NumberFormatCategory.Text, 2, false, 0, code);
        for (var places = 0; places <= MostDecimalPlaces; places++)
        {
            for (var negative = 0; negative < NumberNegativeStyles; negative++)
            {
                if (code == Number(places, false, negative)) return new(NumberFormatCategory.Number, places, false, negative, code);
                if (code == Number(places, true, negative)) return new(NumberFormatCategory.Number, places, true, negative, code);
            }
            for (var negative = 0; negative < CurrencyNegativeStyles; negative++)
            {
                if (code == Currency(places, negative, culture)) return new(NumberFormatCategory.Currency, places, true, negative, code);
            }
            if (code == Percentage(places)) return new(NumberFormatCategory.Percentage, places, false, 0, code);
            if (code == Scientific(places)) return new(NumberFormatCategory.Scientific, places, false, 0, code);
        }
        if (DateTypes.FirstOrDefault(type => string.Equals(type, code, StringComparison.OrdinalIgnoreCase)) is { } date)
            return new(NumberFormatCategory.Date, 2, false, 0, date);
        if (TimeTypes.FirstOrDefault(type => string.Equals(type, code, StringComparison.OrdinalIgnoreCase)) is { } time)
            return new(NumberFormatCategory.Time, 2, false, 0, time);
        return new(NumberFormatCategory.Custom, 2, false, 0, code);
    }
}
