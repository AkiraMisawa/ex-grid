using System.Globalization;

namespace ExSheet.Engine;

/// <summary>Reads a typed constant under a Sheet's culture (ADR-0048).</summary>
internal static class ConstantParser
{
    public static Value Parse(string typed, CultureInfo culture)
    {
        // A leading apostrophe makes the rest text, whatever it looks like, as in Excel.
        if (typed[0] == '\'') return Value.FromText(typed[1..]);
        if (typed.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return Value.FromBoolean(true);
        if (typed.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return Value.FromBoolean(false);
        if (ErrorValues.TryParseTyped(typed, out var error)) return Value.FromError(error);
        if (TryParseNumber(typed, culture, out var number)) return Value.FromNumber(number);
        return Value.FromText(typed);
    }

    public static bool TryParseNumber(string text, CultureInfo culture, out double number) =>
        double.TryParse(text, NumberStyles.Float, culture, out number) && double.IsFinite(number);
}
