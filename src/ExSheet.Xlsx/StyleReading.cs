using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// The parts of a Cell Format one level records — a cell, a row or a column (ADR-0047, ADR-0071) — each null where the level
/// takes it from the level beneath.
/// </summary>
internal readonly record struct FormatParts(NumberFormat? NumberFormat, HorizontalAlignment? Alignment, CellFont? Font, CellFill? Fill, CellBorders? Borders)
{
    public bool IsEmpty => NumberFormat is null && Alignment is null && Font is null && Fill is null && Borders is null;

    /// <summary>This level over <paramref name="under"/>, part by part.</summary>
    public CellFormat Over(CellFormat under) => new(
        NumberFormat ?? under.NumberFormat, Alignment ?? under.Alignment, Font ?? under.Font, Fill ?? under.Fill, Borders ?? under.Borders);

    /// <summary>The parts of <paramref name="format"/> that differ from <paramref name="under"/>: what a level records so that it shows <paramref name="format"/> over it.</summary>
    public static FormatParts Differing(CellFormat format, CellFormat under) => new(
        format.NumberFormat.Equals(under.NumberFormat) ? null : format.NumberFormat,
        format.Alignment == under.Alignment ? null : format.Alignment,
        format.Font == under.Font ? null : format.Font,
        format.Fill == under.Fill ? null : format.Fill,
        format.Borders == under.Borders ? null : format.Borders);
}

/// <summary>A cell format of the file read into the engine's Cell Format: what crosses, what cannot, and what is not carried.</summary>
/// <param name="Format">The Cell Format, every part the file's, or its default where the part cannot cross.</param>
/// <param name="Refusal">Why its Number Format cannot cross, or null; a level that records it refuses the file.</param>
/// <param name="Held">What the refusal names as held: the code, or the id.</param>
/// <param name="NotCarried">What it holds that is not carried, each kind once.</param>
/// <param name="NamesTheme">Whether a colour it carries names a theme slot: carried as the theme's RGB, the link not.</param>
internal sealed record ReadFormat(CellFormat Format, string? Refusal, string? Held, IReadOnlyList<NotCarriedKind> NotCarried, bool NamesTheme);

/// <summary>
/// Reads the styles part's cell formats into the engine's Cell Formats (ADR-0330 §4, §8, ADR-0071), each once. Carried: the
/// Number Format, horizontal alignment (General, left, centre, right), the Font's colour, bold, italic, underline and
/// strikethrough, a solid Fill, and the four sides' borders in Excel's thirteen line styles. A colour is the RGB the file's
/// theme or palette gives it; the default font's own colour, where it is black, is Automatic, the Ink, as a plain Excel
/// workbook shows its text. Not carried, and said: another typeface or size than the workbook's own, vertical alignment,
/// wrapping, indent, rotation and shrinking, the other horizontal alignments, double and accounting underlines (a single one
/// is carried), pattern and gradient fills, and diagonal borders.
/// </summary>
internal sealed class StyleReading(XlsxStyles styles, XlsxTheme theme)
{
    private readonly Dictionary<int, ReadFormat> read = [];
    private readonly XlsxFont workbookFont = styles.Fonts[0];

    /// <summary>The cell format at <paramref name="index"/> of the styles' cell formats, read once.</summary>
    public ReadFormat this[int index]
    {
        get
        {
            if (!read.TryGetValue(index, out var format))
            {
                format = Read(styles.CellFormats[index]);
                read[index] = format;
            }
            return format;
        }
    }

    private ReadFormat Read(XlsxCellFormat xf)
    {
        var notCarried = new List<NotCarriedKind>();
        var namesTheme = false;
        void Note(NotCarriedKind kind)
        {
            if (!notCarried.Contains(kind)) notCarried.Add(kind);
        }
        CellColour Colour(XlsxColor? color)
        {
            if (color is not { } c || theme.Resolve(c, styles.IndexedColors) is not { } argb) return CellColour.Automatic;
            namesTheme |= c.Kind == XlsxColorKind.Theme;
            return CellColour.FromRgb((int)(argb & 0xFFFFFF));
        }

        string? refusal = null, held = null;
        var numberFormat = NumberFormat.General;
        if (XlsxNumberFormats.TryRead(xf.NumberFormatId, styles.NumberFormats.GetValueOrDefault(xf.NumberFormatId), out var parsed, out var why, out var what))
            numberFormat = parsed;
        else
            (refusal, held) = (why, what);

        var a = xf.EffectiveAlignment;
        var alignment = a.Horizontal switch
        {
            XlsxHorizontalAlignment.General => HorizontalAlignment.General,
            XlsxHorizontalAlignment.Left => HorizontalAlignment.Left,
            XlsxHorizontalAlignment.Center => HorizontalAlignment.Center,
            XlsxHorizontalAlignment.Right => HorizontalAlignment.Right,
            _ => (HorizontalAlignment?)null,
        };
        if (alignment is null) Note(NotCarriedKind.HorizontalAlignment);
        if (a.Vertical != XlsxVerticalAlignment.Bottom) Note(NotCarriedKind.VerticalAlignment);
        if (a.WrapText || a.Indent != 0 || a.TextRotation != 0 || a.ShrinkToFit) Note(NotCarriedKind.TextLayout);

        var f = styles.Fonts[xf.FontId];
        if (!SameFace(f, workbookFont)) Note(NotCarriedKind.FontNamesAndSizes);
        if (f.Underline is not (XlsxUnderline.None or XlsxUnderline.Single)) Note(NotCarriedKind.Underline);
        var font = new CellFont(FontColour(f.Color, Colour), f.Bold, f.Italic, f.Underline != XlsxUnderline.None, f.Strike);

        var fill = CellFill.None;
        var x = styles.Fills[xf.FillId];
        if (x.IsGradient || x.Pattern is not ("none" or "solid"))
        {
            Note(NotCarriedKind.PatternFills);
        }
        else if (x.Pattern == "solid")
        {
            // A solid fill whose colour is automatic, or names no colour the palette holds, has no RGB to carry.
            var colour = Colour(x.Foreground);
            if (colour.IsAutomatic) Note(NotCarriedKind.PatternFills);
            else fill = CellFill.Solid(colour);
        }

        var b = styles.Borders[xf.BorderId];
        if ((b.DiagonalUp || b.DiagonalDown) && b.Diagonal.Style != XlsxBorderStyle.None) Note(NotCarriedKind.DiagonalBorders);
        BorderLine Line(XlsxBorderEdge edge) => edge.Style == XlsxBorderStyle.None ? BorderLine.None : new BorderLine(LineStyle(edge.Style), Colour(edge.Color));
        var borders = new CellBorders(Line(b.Top), Line(b.Bottom), Line(b.Left), Line(b.Right));

        return new ReadFormat(new CellFormat(numberFormat, alignment ?? HorizontalAlignment.General, font, fill, borders), refusal, held, notCarried, namesTheme);
    }

    /// <summary>
    /// A Font's colour: the workbook font's own colour, where it paints black, is Automatic, the Ink, as Excel's own default
    /// text (Text 1 of the Office theme) is; any other is the RGB it resolves to.
    /// </summary>
    private CellColour FontColour(XlsxColor? color, Func<XlsxColor?, CellColour> colour)
    {
        if (color is { } c && c == workbookFont.Color && theme.Resolve(c, styles.IndexedColors) is null or 0xFF000000 or 0x00000000)
            return CellColour.Automatic;
        return colour(color);
    }

    /// <summary>Whether a font has the workbook font's typeface and size; one that names none takes the workbook's.</summary>
    private static bool SameFace(XlsxFont font, XlsxFont workbook) =>
        (font.Name is null || string.Equals(font.Name, workbook.Name, StringComparison.OrdinalIgnoreCase))
        && (font.Size is null || font.Size == workbook.Size);

    /// <summary>The engine's line style for the file's: the same thirteen (ADR-0071).</summary>
    public static BorderLineStyle LineStyle(XlsxBorderStyle style) => style switch
    {
        XlsxBorderStyle.Thin => BorderLineStyle.Thin,
        XlsxBorderStyle.Medium => BorderLineStyle.Medium,
        XlsxBorderStyle.Dashed => BorderLineStyle.Dashed,
        XlsxBorderStyle.Dotted => BorderLineStyle.Dotted,
        XlsxBorderStyle.Thick => BorderLineStyle.Thick,
        XlsxBorderStyle.Double => BorderLineStyle.Double,
        XlsxBorderStyle.Hair => BorderLineStyle.Hair,
        XlsxBorderStyle.MediumDashed => BorderLineStyle.MediumDashed,
        XlsxBorderStyle.DashDot => BorderLineStyle.DashDot,
        XlsxBorderStyle.MediumDashDot => BorderLineStyle.MediumDashDot,
        XlsxBorderStyle.DashDotDot => BorderLineStyle.DashDotDot,
        XlsxBorderStyle.MediumDashDotDot => BorderLineStyle.MediumDashDotDot,
        XlsxBorderStyle.SlantDashDot => BorderLineStyle.SlantedDashDot,
        _ => BorderLineStyle.None,
    };

    /// <summary>The file's line style for the engine's.</summary>
    public static XlsxBorderStyle FileStyle(BorderLineStyle style) => style switch
    {
        BorderLineStyle.Thin => XlsxBorderStyle.Thin,
        BorderLineStyle.Medium => XlsxBorderStyle.Medium,
        BorderLineStyle.Dashed => XlsxBorderStyle.Dashed,
        BorderLineStyle.Dotted => XlsxBorderStyle.Dotted,
        BorderLineStyle.Thick => XlsxBorderStyle.Thick,
        BorderLineStyle.Double => XlsxBorderStyle.Double,
        BorderLineStyle.Hair => XlsxBorderStyle.Hair,
        BorderLineStyle.MediumDashed => XlsxBorderStyle.MediumDashed,
        BorderLineStyle.DashDot => XlsxBorderStyle.DashDot,
        BorderLineStyle.MediumDashDot => XlsxBorderStyle.MediumDashDot,
        BorderLineStyle.DashDotDot => XlsxBorderStyle.DashDotDot,
        BorderLineStyle.MediumDashDotDot => XlsxBorderStyle.MediumDashDotDot,
        BorderLineStyle.SlantedDashDot => XlsxBorderStyle.SlantDashDot,
        _ => XlsxBorderStyle.None,
    };
}
