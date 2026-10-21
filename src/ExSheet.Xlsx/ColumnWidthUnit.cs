using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// A column's width between the engine's unit and the file's (ADR-0330 §4, ADR-0047). The engine counts characters of the
/// default font's digit, as Excel's Column Width box shows them (8.43 by default). The file counts the same characters with the
/// cell's padding added, five pixels at the default font's maximum digit width of seven, truncated to 1/256 of a character, as
/// ECMA-376 Part 1, §18.3.1.13 gives it: 8.43 is <c>9.140625</c>. Read back, a width is rounded to hundredths, as Excel shows it,
/// so a width written from the engine reads back as it was to the hundredth.
/// </summary>
internal static class ColumnWidthUnit
{
    /// <summary>The default font's maximum digit width in pixels: Calibri 11's at 96 DPI, which every width is counted in (ticket 07 checks another default font against Excel).</summary>
    public const double MaxDigitWidth = 7;

    /// <summary>The cell's padding, in pixels: two on each side and one for the gridline.</summary>
    public const double Padding = 5;

    /// <summary>The file's width for <paramref name="characters"/>.</summary>
    public static double ToFile(double characters) =>
        Math.Truncate((characters * MaxDigitWidth + Padding) / MaxDigitWidth * 256) / 256;

    /// <summary>The characters a file's <paramref name="width"/> holds, to the hundredth; 0 or less for a width too narrow to hold one.</summary>
    public static double FromFile(double width) =>
        Math.Truncate((width * MaxDigitWidth - Padding) / MaxDigitWidth * 100 + 0.5) / 100;

    /// <summary>The narrowest width the engine holds, in characters: a file's narrower width, above 0, opens at it.</summary>
    public const double Narrowest = 0.01;

    /// <summary>
    /// The engine's width for a file's <paramref name="width"/>, or null where it records none: a width of 0 or less (a column
    /// Excel shows hidden, which the Open Report lists), or the default. A width too narrow to hold a hundredth of a character
    /// opens at the narrowest the engine holds, and one wider than the engine holds at the widest.
    /// </summary>
    public static double? Read(double width)
    {
        if (width <= 0) return null;
        var characters = Math.Clamp(FromFile(width), Narrowest, Sheet.MaxColumnWidth);
        return characters == Sheet.DefaultColumnWidth ? null : characters;
    }
}
