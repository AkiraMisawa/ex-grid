namespace ExSheet.Engine;

/// <summary>
/// A colour a Number Format names at the start of a section — <c>[Red]</c> and the seven others,
/// in any case (ADR-0047). The text of a Value that section shows is painted in it, and it takes
/// precedence over the cell's Font colour, as Excel's does (ADR-0071; a reading until the eleventh
/// Windows run, case 2). What each name paints is <see cref="NumberFormatColours.Rgb"/>. A numbered
/// colour (<c>[Color10]</c>) is not one of these: it is refused, as Excel refused it.
/// </summary>
public enum NumberFormatColour
{
    /// <summary><c>[Black]</c>.</summary>
    Black,

    /// <summary><c>[Blue]</c>.</summary>
    Blue,

    /// <summary><c>[Cyan]</c>.</summary>
    Cyan,

    /// <summary><c>[Green]</c>.</summary>
    Green,

    /// <summary><c>[Magenta]</c>.</summary>
    Magenta,

    /// <summary><c>[Red]</c>.</summary>
    Red,

    /// <summary><c>[White]</c>.</summary>
    White,

    /// <summary><c>[Yellow]</c>.</summary>
    Yellow,
}

/// <summary>Excel's colour for each name a Number Format may give (ADR-0071).</summary>
public static class NumberFormatColours
{
    /// <summary>
    /// The RGB Excel paints the name in, as <c>0xRRGGBB</c>: Excel's legacy palette, the one table
    /// of it. This is a reading until the eleventh Windows run, case 1, which samples what Excel
    /// draws for each name (ADR-0071).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not one of the eight names.</exception>
    public static int Rgb(this NumberFormatColour colour) => colour switch
    {
        NumberFormatColour.Black => 0x000000,
        NumberFormatColour.Blue => 0x0000FF,
        NumberFormatColour.Cyan => 0x00FFFF,
        NumberFormatColour.Green => 0x00FF00,
        NumberFormatColour.Magenta => 0xFF00FF,
        NumberFormatColour.Red => 0xFF0000,
        NumberFormatColour.White => 0xFFFFFF,
        NumberFormatColour.Yellow => 0xFFFF00,
        _ => throw new ArgumentOutOfRangeException(nameof(colour), colour, "A Number Format names one of eight colours."),
    };

    /// <summary>The colour <paramref name="name"/> names, in any case, or <see langword="null"/> when it names none of the eight.</summary>
    internal static NumberFormatColour? Named(string name)
    {
        foreach (var colour in Enum.GetValues<NumberFormatColour>())
        {
            if (colour.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)) return colour;
        }
        return null;
    }
}
