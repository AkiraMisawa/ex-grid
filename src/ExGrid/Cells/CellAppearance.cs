using System.Globalization;

namespace ExGrid.Cells;

/// <summary>
/// A colour a Consumer records on a cell (ADR-0050, item 15): an RGB value, painted as recorded.
/// It is document data, not a Visual Token — a Sheet's red for a breach is the user's choice and
/// reads as chosen (ADR-0071) — so it is the one kind of colour that travels from C# to the
/// stylesheet, and it travels only as the six hex digits of a class the core generates. The default
/// value is black.
/// </summary>
public readonly record struct RgbColour
{
    private readonly int _rgb;

    private RgbColour(int rgb) => _rgb = rgb;

    /// <summary>Black, <c>#000000</c>: the default value, and Excel's line colour where none is
    /// chosen.</summary>
    public static RgbColour Black => default;

    /// <summary>A colour from its value, as <c>0xRRGGBB</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not <c>0x000000</c> to
    /// <c>0xFFFFFF</c>.</exception>
    public static RgbColour FromRgb(int rgb) => (uint)rgb <= 0xFFFFFF
        ? new(rgb)
        : throw new ArgumentOutOfRangeException(nameof(rgb), rgb, "An RGB colour is 0x000000 to 0xFFFFFF.");

    /// <summary>A colour from its red, green and blue.</summary>
    public static RgbColour FromRgb(byte red, byte green, byte blue) => new(red << 16 | green << 8 | blue);

    /// <summary>The value, as <c>0xRRGGBB</c>.</summary>
    public int Rgb => _rgb;

    /// <summary>The colour as <c>#rrggbb</c>, which is also how a stylesheet reads it.</summary>
    public override string ToString() => "#" + _rgb.ToString("x6", CultureInfo.InvariantCulture);
}

/// <summary>
/// Excel's thirteen line styles for a <see cref="Border"/> (ADR-0071), and <see cref="None"/>. Each
/// is drawn at the eleventh Windows run's geometry (case 9): centred on the gridline, in device
/// pixels, so a thin line is one device pixel at every zoom (ADR-0050, item 15).
/// </summary>
public enum BorderStyle
{
    /// <summary>No line.</summary>
    None = 0,

    /// <summary>Hair: one pixel on the gridline, every other pixel.</summary>
    Hair,

    /// <summary>Thin: one pixel, on the gridline.</summary>
    Thin,

    /// <summary>Medium: two pixels, the gridline and the one above it (left of it).</summary>
    Medium,

    /// <summary>Thick: three pixels, the gridline and one either side.</summary>
    Thick,

    /// <summary>Double: a pixel either side of the gridline, whose own pixel shows the ground.</summary>
    Double,

    /// <summary>Dotted: two on, two off, on the gridline.</summary>
    Dotted,

    /// <summary>Dashed: three on, one off, on the gridline.</summary>
    Dashed,

    /// <summary>Dash-dot, on the gridline.</summary>
    DashDot,

    /// <summary>Dash-dot-dot, on the gridline.</summary>
    DashDotDot,

    /// <summary>Medium dashed: two pixels, as <see cref="Medium"/>.</summary>
    MediumDashed,

    /// <summary>Medium dash-dot: two pixels, as <see cref="Medium"/>.</summary>
    MediumDashDot,

    /// <summary>Medium dash-dot-dot: two pixels, as <see cref="Medium"/>.</summary>
    MediumDashDotDot,

    /// <summary>Slanted dash-dot: two pixels, each row its own pattern.</summary>
    SlantedDashDot,
}

/// <summary>
/// A Border (<c>CONTEXT.md</c>): the line on one side of a cell, in one of Excel's line styles and
/// a colour, or <see cref="None"/>. The default value is <see cref="None"/>.
/// </summary>
public readonly record struct Border
{
    /// <summary>A line in <paramref name="style"/> and <paramref name="colour"/>;
    /// <see cref="BorderStyle.None"/> is no line, and keeps no colour.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The style is not one of
    /// <see cref="BorderStyle"/>'s.</exception>
    public Border(BorderStyle style, RgbColour colour = default)
    {
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style), style, "Not a line style.");
        Style = style;
        Colour = style == BorderStyle.None ? default : colour;
    }

    /// <summary>No line.</summary>
    public static Border None => default;

    /// <summary>The line style; <see cref="BorderStyle.None"/> for no line.</summary>
    public BorderStyle Style { get; }

    /// <summary>The line's colour; black for no line.</summary>
    public RgbColour Colour { get; }

    /// <summary>Whether this is no line.</summary>
    public bool IsNone => Style == BorderStyle.None;
}

/// <summary>
/// A cell's appearance, as a Consumer supplies it (ADR-0050, item 15): its Font (a colour, bold,
/// italic, underline and strikethrough), its Fill, and the Border on each of its four sides. The
/// default value is <see cref="None"/>, which paints the cell exactly as a grid without the
/// declaration does.
///
/// <para>Each part is painted as Excel paints it (ADR-0071). The Font gives way on a Stale or Error
/// cell, whose Cell State must never be the one that disappears (ADR-0006), and outranks a column's
/// tone, which is the column's rule and not the cell's own. A Fill covers the gridlines at the
/// cell's edges. A Border is centred on its gridline, a thick line
/// reaching into both cells, above the Fills and below the Focus, the Selection and the Reference
/// Outlines; the line between two cells is drawn once, and where both record one, the grid's
/// <c>EdgeBorder</c> answer chooses. A bold cell is judged by the bold widths of
/// <see cref="Columns.CellTextMetrics"/> (ADR-0016).</para>
/// </summary>
public readonly record struct CellAppearance
{
    /// <summary>No appearance: the cell is painted as without the declaration.</summary>
    public static CellAppearance None => default;

    /// <summary>The text's colour, or null for the grid's own.</summary>
    public RgbColour? FontColour { get; init; }

    /// <summary>Bold text, judged by the bold widths.</summary>
    public bool Bold { get; init; }

    /// <summary>Italic text, judged by the regular widths: its slant leans past a glyph's advance
    /// by less than the cell's padding (ADR-0071).</summary>
    public bool Italic { get; init; }

    /// <summary>A single underline.</summary>
    public bool Underline { get; init; }

    /// <summary>A line through the text.</summary>
    public bool Strikethrough { get; init; }

    /// <summary>The one solid colour behind the text, or null for none.</summary>
    public RgbColour? Fill { get; init; }

    /// <summary>The line on the top side.</summary>
    public Border Top { get; init; }

    /// <summary>The line on the right side.</summary>
    public Border Right { get; init; }

    /// <summary>The line on the bottom side.</summary>
    public Border Bottom { get; init; }

    /// <summary>The line on the left side.</summary>
    public Border Left { get; init; }
}

/// <summary>
/// How the grid asks for a cell's appearance (ADR-0050, item 15): by (row, column), as it asks for
/// a Cell State (ADR-0006). Asked of value cells only, as the per-cell kind is.
///
/// <para><b>The row's identity and the delegate's are the change signal</b> (ADR-0003/0006). A
/// row whose appearance changed is handed over as a new instance, or a new lookup is handed over
/// for all of them. The grid resolves each painted row's appearance once, outside the row's render,
/// from the row and the rows either side of it — a cell's top line is the edge it shares with the
/// row above — and repaints a row only when what it paints changed. So a new instance repaints its
/// own row, and its neighbours only where a line they share has changed.</para>
///
/// <para><b>Keep it light, and hold it in a field.</b> It is asked once per painted value cell, and
/// once more for each column and row just beyond the painted ones, when a row enters the Viewport
/// or changes.</para>
/// </summary>
public delegate CellAppearance CellAppearanceOf<TRow>(TRow row, GridColumn<TRow> column);

/// <summary>
/// Which line is drawn on an edge whose two cells both record one, and record different lines
/// (ADR-0050, item 15): <paramref name="upperOrLeft"/> is the line the upper (or left) cell
/// records on that edge, and <paramref name="lowerOrRight"/> the line the other records. It is not
/// asked when only one cell records a line, nor when both record the same. Without it, the upper or
/// left cell's line is drawn (<c>CONTEXT.md</c>, Border).
/// </summary>
public delegate Border EdgeBorderOf(Border upperOrLeft, Border lowerOrRight);
