namespace ExGrid.Data.Xlsx;

/// <summary>What a colour names (ECMA-376 <c>CT_Color</c>).</summary>
public enum XlsxColorKind
{
    /// <summary>The automatic colour (<c>auto="1"</c>): the window's text or background, as the place it colours decides.</summary>
    Automatic,

    /// <summary>An ARGB value (<c>rgb</c>).</summary>
    Rgb,

    /// <summary>A slot of the workbook's theme (<c>theme</c>), resolved through <see cref="XlsxTheme"/>.</summary>
    Theme,

    /// <summary>An entry of the legacy indexed palette (<c>indexed</c>).</summary>
    Indexed,
}

/// <summary>
/// A colour as the file writes it: an ARGB value, a theme slot, an indexed entry or the automatic colour, each with a
/// tint. <see cref="XlsxTheme.Resolve"/> turns it into the RGB Excel paints (ADR-0330 §8).
/// </summary>
/// <param name="Kind">What the colour names.</param>
/// <param name="Value">The ARGB value for <see cref="XlsxColorKind.Rgb"/>, the theme slot for <see cref="XlsxColorKind.Theme"/>, the
/// palette index for <see cref="XlsxColorKind.Indexed"/>; 0 for <see cref="XlsxColorKind.Automatic"/>.</param>
/// <param name="Tint">How far the colour is lightened (positive, up to 1) or darkened (negative, down to -1); 0 for none.</param>
public readonly record struct XlsxColor(XlsxColorKind Kind, uint Value, double Tint = 0)
{
    /// <summary>The automatic colour.</summary>
    public static XlsxColor Automatic => new(XlsxColorKind.Automatic, 0);

    /// <summary>An ARGB value, as <c>0xFF4472C4</c>.</summary>
    /// <param name="argb">The value, alpha in the high byte.</param>
    public static XlsxColor FromArgb(uint argb) => new(XlsxColorKind.Rgb, argb);

    /// <summary>A theme slot, with a tint.</summary>
    /// <param name="slot">The slot, as a style names it: 0 Background 1, 1 Text 1, 2 Background 2, 3 Text 2, 4 to 9 Accents 1 to 6, 10 Hyperlink, 11 Followed Hyperlink.</param>
    /// <param name="tint">How far it is lightened (positive) or darkened (negative).</param>
    public static XlsxColor FromTheme(int slot, double tint = 0) => new(XlsxColorKind.Theme, (uint)slot, tint);

    /// <summary>An entry of the indexed palette.</summary>
    /// <param name="index">The entry, 0 to 65.</param>
    public static XlsxColor FromIndexed(int index) => new(XlsxColorKind.Indexed, (uint)index);
}

/// <summary>How a font underlines (ECMA-376 <c>ST_UnderlineValues</c>).</summary>
public enum XlsxUnderline
{
    /// <summary>No underline.</summary>
    None,

    /// <summary>A single line.</summary>
    Single,

    /// <summary>Two lines.</summary>
    Double,

    /// <summary>A single line across the cell, as the Accounting format draws it.</summary>
    SingleAccounting,

    /// <summary>Two lines across the cell, as the Accounting format draws them.</summary>
    DoubleAccounting,
}

/// <summary>A font of the styles part.</summary>
/// <param name="Name">The typeface, as <c>Calibri</c>, or null.</param>
/// <param name="Size">The size in points, or null.</param>
/// <param name="Bold">Whether it is bold.</param>
/// <param name="Italic">Whether it is italic.</param>
/// <param name="Underline">How it underlines.</param>
/// <param name="Strike">Whether it is struck through.</param>
/// <param name="Color">Its colour, or null for none written.</param>
public sealed record XlsxFont(string? Name = null, double? Size = null, bool Bold = false, bool Italic = false,
    XlsxUnderline Underline = XlsxUnderline.None, bool Strike = false, XlsxColor? Color = null);

/// <summary>A fill of the styles part.</summary>
/// <param name="Pattern">The pattern, as ECMA-376 <c>ST_PatternType</c> names it: <c>none</c>, <c>solid</c>, <c>gray125</c>, …; null for a gradient fill.</param>
/// <param name="Foreground">The pattern's foreground colour — a solid fill's colour — or null.</param>
/// <param name="Background">The pattern's background colour, or null.</param>
/// <param name="IsGradient">Whether it is a gradient fill, which carries no pattern.</param>
public sealed record XlsxFill(string? Pattern, XlsxColor? Foreground = null, XlsxColor? Background = null, bool IsGradient = false)
{
    /// <summary>No fill: the first fill of every styles part.</summary>
    public static XlsxFill None { get; } = new("none");

    /// <summary>The 12.5% grey pattern Excel reserves as every styles part's second fill.</summary>
    public static XlsxFill Gray125 { get; } = new("gray125");

    /// <summary>A solid fill of <paramref name="color"/>.</summary>
    /// <param name="color">The fill's colour.</param>
    public static XlsxFill Solid(XlsxColor color) => new("solid", color);
}

/// <summary>A border line's style: none, or one of Excel's thirteen (ECMA-376 <c>ST_BorderStyle</c>).</summary>
public enum XlsxBorderStyle
{
    /// <summary>No line.</summary>
    None,

    /// <summary><c>thin</c>.</summary>
    Thin,

    /// <summary><c>medium</c>.</summary>
    Medium,

    /// <summary><c>dashed</c>.</summary>
    Dashed,

    /// <summary><c>dotted</c>.</summary>
    Dotted,

    /// <summary><c>thick</c>.</summary>
    Thick,

    /// <summary><c>double</c>.</summary>
    Double,

    /// <summary><c>hair</c>.</summary>
    Hair,

    /// <summary><c>mediumDashed</c>.</summary>
    MediumDashed,

    /// <summary><c>dashDot</c>.</summary>
    DashDot,

    /// <summary><c>mediumDashDot</c>.</summary>
    MediumDashDot,

    /// <summary><c>dashDotDot</c>.</summary>
    DashDotDot,

    /// <summary><c>mediumDashDotDot</c>.</summary>
    MediumDashDotDot,

    /// <summary><c>slantDashDot</c>.</summary>
    SlantDashDot,
}

/// <summary>One edge of a border: its line style and colour.</summary>
/// <param name="Style">The line's style.</param>
/// <param name="Color">The line's colour, or null for none written.</param>
public sealed record XlsxBorderEdge(XlsxBorderStyle Style, XlsxColor? Color = null)
{
    /// <summary>No line.</summary>
    public static XlsxBorderEdge None { get; } = new(XlsxBorderStyle.None);
}

/// <summary>A border of the styles part: four edges and a diagonal.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Diagonal">The diagonal line, drawn where <paramref name="DiagonalUp"/> or <paramref name="DiagonalDown"/> says.</param>
/// <param name="DiagonalUp">Whether the diagonal rises from bottom left to top right.</param>
/// <param name="DiagonalDown">Whether the diagonal falls from top left to bottom right.</param>
public sealed record XlsxBorder(XlsxBorderEdge Left, XlsxBorderEdge Right, XlsxBorderEdge Top, XlsxBorderEdge Bottom,
    XlsxBorderEdge Diagonal, bool DiagonalUp = false, bool DiagonalDown = false)
{
    /// <summary>No border: the first border of every styles part.</summary>
    public static XlsxBorder None { get; } = new(XlsxBorderEdge.None, XlsxBorderEdge.None, XlsxBorderEdge.None, XlsxBorderEdge.None, XlsxBorderEdge.None);
}

/// <summary>A cell's horizontal alignment (ECMA-376 <c>ST_HorizontalAlignment</c>).</summary>
public enum XlsxHorizontalAlignment
{
    /// <summary><c>general</c>: text to the left, numbers to the right.</summary>
    General,

    /// <summary><c>left</c>.</summary>
    Left,

    /// <summary><c>center</c>.</summary>
    Center,

    /// <summary><c>right</c>.</summary>
    Right,

    /// <summary><c>fill</c>: the text repeated across the cell.</summary>
    Fill,

    /// <summary><c>justify</c>.</summary>
    Justify,

    /// <summary><c>centerContinuous</c>: centred across the selection.</summary>
    CenterContinuous,

    /// <summary><c>distributed</c>.</summary>
    Distributed,
}

/// <summary>A cell's vertical alignment (ECMA-376 <c>ST_VerticalAlignment</c>).</summary>
public enum XlsxVerticalAlignment
{
    /// <summary><c>bottom</c>, Excel's default.</summary>
    Bottom,

    /// <summary><c>top</c>.</summary>
    Top,

    /// <summary><c>center</c>.</summary>
    Center,

    /// <summary><c>justify</c>.</summary>
    Justify,

    /// <summary><c>distributed</c>.</summary>
    Distributed,
}

/// <summary>A cell's alignment, as an <c>&lt;alignment&gt;</c> writes it.</summary>
/// <param name="Horizontal">The horizontal alignment.</param>
/// <param name="Vertical">The vertical alignment.</param>
/// <param name="WrapText">Whether the text wraps.</param>
/// <param name="Indent">The indent, in steps.</param>
/// <param name="TextRotation">The rotation, as the file writes it: 0 to 180, or 255 for stacked text.</param>
/// <param name="ShrinkToFit">Whether the text shrinks to fit.</param>
public sealed record XlsxAlignment(XlsxHorizontalAlignment Horizontal = XlsxHorizontalAlignment.General,
    XlsxVerticalAlignment Vertical = XlsxVerticalAlignment.Bottom, bool WrapText = false, int Indent = 0, int TextRotation = 0, bool ShrinkToFit = false)
{
    /// <summary>The alignment of a cell that writes none.</summary>
    public static XlsxAlignment Default { get; } = new();
}

/// <summary>A cell format (<c>&lt;xf&gt;</c> of <c>cellXfs</c>): what a cell's <see cref="XlsxCell.StyleIndex"/> points at.</summary>
/// <param name="NumberFormatId">The Number Format's id: a built-in one below 164, or one of <see cref="XlsxStyles.NumberFormats"/>.</param>
/// <param name="FontId">The index into <see cref="XlsxStyles.Fonts"/>.</param>
/// <param name="FillId">The index into <see cref="XlsxStyles.Fills"/>.</param>
/// <param name="BorderId">The index into <see cref="XlsxStyles.Borders"/>.</param>
/// <param name="Alignment">The alignment.</param>
public sealed record XlsxCellFormat(int NumberFormatId = 0, int FontId = 0, int FillId = 0, int BorderId = 0, XlsxAlignment? Alignment = null)
{
    /// <summary>The alignment, the default one when the format writes none.</summary>
    public XlsxAlignment EffectiveAlignment => Alignment ?? XlsxAlignment.Default;
}

/// <summary>
/// The styles part as far as ADR-0330 §4 carries it: the custom Number Formats, the fonts, fills and borders, and the cell
/// formats that cells point at. What else the part holds (named cell styles, differential formats for conditional
/// formatting, table styles) is not read.
/// </summary>
/// <param name="NumberFormats">The custom Number Formats by id (164 and above, or a built-in id written over), each its format code.</param>
/// <param name="Fonts">The fonts, by index.</param>
/// <param name="Fills">The fills, by index.</param>
/// <param name="Borders">The borders, by index.</param>
/// <param name="CellFormats">The cell formats, by index.</param>
/// <param name="IndexedColors">The indexed palette written over (<c>&lt;colors&gt;&lt;indexedColors&gt;</c>), as ARGB values, or null when the file
/// keeps the default one.</param>
public sealed record XlsxStyles(
    IReadOnlyDictionary<int, string> NumberFormats,
    IReadOnlyList<XlsxFont> Fonts,
    IReadOnlyList<XlsxFill> Fills,
    IReadOnlyList<XlsxBorder> Borders,
    IReadOnlyList<XlsxCellFormat> CellFormats,
    IReadOnlyList<uint>? IndexedColors = null)
{
    /// <summary>The styles of a workbook that styles nothing: Calibri 11, no fill (and the reserved grey), no border, and one
    /// cell format of all four.</summary>
    public static XlsxStyles Default { get; } = new(
        new Dictionary<int, string>(),
        [new XlsxFont("Calibri", 11, Color: XlsxColor.FromTheme(1))],
        [XlsxFill.None, XlsxFill.Gray125],
        [XlsxBorder.None],
        [new XlsxCellFormat()]);
}
