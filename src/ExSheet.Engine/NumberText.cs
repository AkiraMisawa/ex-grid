using System.Globalization;

namespace ExSheet.Engine;

/// <summary>Numbers written as text, as Excel writes them.</summary>
internal static class NumberText
{
    /// <summary>
    /// Excel's General form: at most 15 significant digits (ADR-0047), the culture's decimal
    /// separator, and scientific notation for very large and very small magnitudes.
    /// </summary>
    public static string General(double number, CultureInfo culture) => number.ToString("G15", culture);
}
