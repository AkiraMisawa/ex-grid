using System.Globalization;

namespace ExSheet.Engine;

/// <summary>
/// A colour a Cell Format records (ADR-0071): <see cref="Automatic"/>, or an RGB value. A colour
/// picked from the theme part of a palette is recorded as its RGB value; Excel's theme colours (a
/// theme slot and a tint) come with <c>.xlsx</c> reading. Automatic is not a recorded colour: a
/// Font's Automatic is the Ink (<c>CONTEXT.md</c>). The default value is <see cref="Automatic"/>.
/// </summary>
public readonly record struct CellColour
{
    // 0 is Automatic; an RGB value is kept above it, so 0x000000 stays black.
    private const int RgbFlag = 0x1000000;

    private readonly int _value;

    private CellColour(int value) => _value = value;

    /// <summary>Automatic: no colour recorded, so what is painted is decided where it is painted.</summary>
    public static CellColour Automatic => default;

    /// <summary>An RGB colour, as <c>0xRRGGBB</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not <c>0x000000</c> to <c>0xFFFFFF</c>.</exception>
    public static CellColour FromRgb(int rgb) =>
        (uint)rgb <= 0xFFFFFF ? new(RgbFlag | rgb) : throw new ArgumentOutOfRangeException(nameof(rgb), rgb, "An RGB colour is 0x000000 to 0xFFFFFF.");

    /// <summary>An RGB colour, from its red, green and blue.</summary>
    public static CellColour FromRgb(byte red, byte green, byte blue) => new(RgbFlag | red << 16 | green << 8 | blue);

    /// <summary>Whether the colour is <see cref="Automatic"/>.</summary>
    public bool IsAutomatic => _value == 0;

    /// <summary>The RGB value, as <c>0xRRGGBB</c>.</summary>
    /// <exception cref="InvalidOperationException">The colour is Automatic, which records no RGB value.</exception>
    public int Rgb => IsAutomatic ? throw new InvalidOperationException("An Automatic colour records no RGB value.") : _value & 0xFFFFFF;

    /// <summary><c>Automatic</c>, or the RGB value as <c>#RRGGBB</c>.</summary>
    public override string ToString() => IsAutomatic ? "Automatic" : "#" + Rgb.ToString("X6", CultureInfo.InvariantCulture);

    /// <summary>A colour written <c>#RRGGBB</c>, in either case, or <see langword="null"/> for any other text.</summary>
    internal static CellColour? FromHex(string? text) =>
        text is ['#', _, _, _, _, _, _] && int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? FromRgb(rgb)
            : null;
}
