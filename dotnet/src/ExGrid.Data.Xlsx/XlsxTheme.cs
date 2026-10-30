namespace ExGrid.Data.Xlsx;

/// <summary>
/// The colours of a workbook's theme (<c>&lt;a:clrScheme&gt;</c>), and the resolution of any colour a style writes into the
/// RGB Excel paints (ADR-0330 §8): a theme slot through these colours, an indexed entry through the palette, and the tint
/// applied to the result as ECMA-376 Part 1, §18.8.19 says.
/// </summary>
public sealed class XlsxTheme
{
    /// <summary>
    /// The theme of Office 2013–2022, which a workbook with no theme part takes: dark 1 <c>000000</c> (the window's text),
    /// light 1 <c>FFFFFF</c> (the window), <c>44546A</c>, <c>E7E6E6</c>, the accents <c>4472C4</c>, <c>ED7D31</c>,
    /// <c>A5A5A5</c>, <c>FFC000</c>, <c>5B9BD5</c>, <c>70AD47</c>, and the hyperlinks <c>0563C1</c> and <c>954F72</c>.
    /// </summary>
    public static XlsxTheme Office { get; } = new(
    [
        0xFF000000, 0xFFFFFFFF, 0xFF44546A, 0xFFE7E6E6,
        0xFF4472C4, 0xFFED7D31, 0xFFA5A5A5, 0xFFFFC000, 0xFF5B9BD5, 0xFF70AD47,
        0xFF0563C1, 0xFF954F72,
    ]);

    /// <summary>A theme of the twelve colours its scheme lists.</summary>
    /// <param name="schemeColors">The colours as ARGB values, in the scheme's own order: dark 1, light 1, dark 2, light 2,
    /// accents 1 to 6, hyperlink, followed hyperlink.</param>
    public XlsxTheme(IReadOnlyList<uint> schemeColors)
    {
        ArgumentNullException.ThrowIfNull(schemeColors);
        if (schemeColors.Count != 12)
            throw new ArgumentException("A theme's colour scheme has twelve colours: dark 1, light 1, dark 2, light 2, six accents and two hyperlinks.", nameof(schemeColors));
        SchemeColors = [.. schemeColors];
    }

    /// <summary>The scheme's twelve colours as ARGB values, in the scheme's order: dark 1, light 1, dark 2, light 2, accents 1 to 6,
    /// hyperlink, followed hyperlink.</summary>
    public IReadOnlyList<uint> SchemeColors { get; }

    /// <summary>
    /// The colour a style's theme slot names. A style numbers the first four slots in another order than the scheme lists
    /// them — 0 is light 1 (Background 1), 1 dark 1 (Text 1), 2 light 2, 3 dark 2 — and the rest as listed.
    /// </summary>
    /// <param name="slot">The slot, as a style writes it, 0 to 11.</param>
    public uint SlotColor(int slot) => slot switch
    {
        0 => SchemeColors[1],
        1 => SchemeColors[0],
        2 => SchemeColors[3],
        3 => SchemeColors[2],
        >= 4 and < 12 => SchemeColors[slot],
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "A theme has twelve slots, 0 to 11."),
    };

    /// <summary>
    /// The RGB value, as <c>0xFFRRGGBB</c>, that <paramref name="color"/> paints, or null for the automatic colour (and the
    /// indexed entries 64 and 65, which name the window's text and background), whose meaning the place it colours decides.
    /// </summary>
    /// <param name="color">The colour as a style writes it.</param>
    /// <param name="indexedColors">The styles part's own indexed palette (<see cref="XlsxStyles.IndexedColors"/>), or null for the default one.</param>
    public uint? Resolve(XlsxColor color, IReadOnlyList<uint>? indexedColors = null)
    {
        uint? argb = color.Kind switch
        {
            XlsxColorKind.Rgb => color.Value,
            XlsxColorKind.Theme => color.Value < 12 ? SlotColor((int)color.Value) : null,
            XlsxColorKind.Indexed => Indexed((int)color.Value, indexedColors),
            _ => null,
        };
        return argb is { } value && color.Tint != 0 ? ApplyTint(value, color.Tint) : argb;
    }

    /// <summary>The default indexed palette's 64 colours (ECMA-376 Part 1, §18.8.27), as ARGB values.</summary>
    public static IReadOnlyList<uint> DefaultIndexedColors { get; } =
    [
        0xFF000000, 0xFFFFFFFF, 0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFF00, 0xFFFF00FF, 0xFF00FFFF,
        0xFF000000, 0xFFFFFFFF, 0xFFFF0000, 0xFF00FF00, 0xFF0000FF, 0xFFFFFF00, 0xFFFF00FF, 0xFF00FFFF,
        0xFF800000, 0xFF008000, 0xFF000080, 0xFF808000, 0xFF800080, 0xFF008080, 0xFFC0C0C0, 0xFF808080,
        0xFF9999FF, 0xFF993366, 0xFFFFFFCC, 0xFFCCFFFF, 0xFF660066, 0xFFFF8080, 0xFF0066CC, 0xFFCCCCFF,
        0xFF000080, 0xFFFF00FF, 0xFFFFFF00, 0xFF00FFFF, 0xFF800080, 0xFF800000, 0xFF008080, 0xFF0000FF,
        0xFF00CCFF, 0xFFCCFFFF, 0xFFCCFFCC, 0xFFFFFF99, 0xFF99CCFF, 0xFFFF99CC, 0xFFCC99FF, 0xFFFFCC99,
        0xFF3366FF, 0xFF33CCCC, 0xFF99CC00, 0xFFFFCC00, 0xFFFF9900, 0xFFFF6600, 0xFF666699, 0xFF969696,
        0xFF003366, 0xFF339966, 0xFF003300, 0xFF333300, 0xFF993300, 0xFF993366, 0xFF333399, 0xFF333333,
    ];

    private static uint? Indexed(int index, IReadOnlyList<uint>? palette)
    {
        var colors = palette ?? DefaultIndexedColors;
        return index >= 0 && index < colors.Count ? colors[index] : null;
    }

    /// <summary>
    /// <paramref name="argb"/> lightened or darkened by <paramref name="tint"/>, as ECMA-376 Part 1, §18.8.19 says: the
    /// colour taken to hue, luminance and saturation, its luminance scaled toward black (<c>L × (1 + tint)</c>) or toward
    /// white (<c>L × (1 − tint) + tint</c>), and taken back, each channel rounded to the nearest of 0 to 255. Alpha is kept.
    /// </summary>
    /// <param name="argb">The colour, as <c>0xAARRGGBB</c>.</param>
    /// <param name="tint">From -1 (black) to 1 (white).</param>
    public static uint ApplyTint(uint argb, double tint)
    {
        tint = Math.Clamp(tint, -1, 1);
        var r = ((argb >> 16) & 0xFF) / 255.0;
        var g = ((argb >> 8) & 0xFF) / 255.0;
        var b = (argb & 0xFF) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        double h = 0, s = 0;
        if (max != min)
        {
            var d = max - min;
            s = l <= 0.5 ? d / (max + min) : d / (2 - max - min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0)
                : max == g ? (b - r) / d + 2
                : (r - g) / d + 4;
            h /= 6;
        }
        l = tint < 0 ? l * (1 + tint) : l * (1 - tint) + tint;
        double r2, g2, b2;
        if (s == 0)
        {
            r2 = g2 = b2 = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r2 = Hue(p, q, h + 1.0 / 3);
            g2 = Hue(p, q, h);
            b2 = Hue(p, q, h - 1.0 / 3);
        }
        return (argb & 0xFF000000) | (Channel(r2) << 16) | (Channel(g2) << 8) | Channel(b2);
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0)
            t += 1;
        if (t > 1)
            t -= 1;
        if (t < 1.0 / 6)
            return p + (q - p) * 6 * t;
        if (t < 0.5)
            return q;
        if (t < 2.0 / 3)
            return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    private static uint Channel(double v) => (uint)Math.Clamp(Math.Round(v * 255, MidpointRounding.AwayFromZero), 0, 255);
}
