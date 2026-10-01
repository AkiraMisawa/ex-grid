using ExGrid;
using ExGrid.Cells;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What ExSheet declares to the core for each cell's look (ADR-0050 item 15, ADR-0071): the Font,
/// the Fill and the four Border sides of the cell's Cell Format, cell over row over column, as the
/// engine shows them. The core paints them; nothing here paints.
/// <list type="bullet">
/// <item><b>The Font's colour</b> is a recorded RGB value; Automatic declares none, and the cell's
/// text is the Ink (<c>ex-sheet.css</c>). A colour the Number Format names for the section that shows
/// the Value takes its place, as Excel's does (the eleventh Windows run, case 2), and a
/// <c>####</c> keeps it (case 3c).</item>
/// <item><b>A Fill</b> is a recorded RGB value, painted on a cell that holds nothing as well, when
/// its row or column records it.</item>
/// <item><b>Each side is the edge as shown</b> (<see cref="Sheet.GetBorders"/>): where both cells
/// record a line on an edge, the upper or left cell's, otherwise whichever records one (the twelfth
/// Windows run). Both cells of an edge therefore name the same line, so the core is never left to
/// draw one cell's record of it on its own; <see cref="EdgeBorder"/> gives the same rule for the
/// case it would be asked. An Automatic line is Excel's black.</item>
/// </list>
/// </summary>
internal static class SheetAppearance
{
    /// <summary>
    /// The lookup the grid is handed for one reading of the Sheet's formatting. Each row keeps what
    /// it was asked for that reading, so a row the engine does not name answers from its cache.
    /// A change that may reformat rows the engine does not name — a whole row's or column's Cell
    /// Format, or rows or columns inserted or deleted (<see cref="SheetChange.ReformatsUnnamedRows"/>)
    /// — starts a new reading with a new lookup: the grid then asks every painted row again, and
    /// repaints only the rows whose look changed (ADR-0050 item 15, ADR-0003).
    /// </summary>
    internal static CellAppearanceOf<SheetRow> Lookup(int reading) =>
        (row, column) => row.AppearanceAt(SheetColumns.IndexOf(column), reading);

    /// <summary>
    /// Which line an edge draws when its two cells name different ones: the upper or left cell's,
    /// as the engine shows it (<see cref="Sheet.GetBorders"/>). ExSheet names each edge as shown from
    /// both of its cells, so the core asks this only of a row whose answer is out of date.
    /// </summary>
    internal static EdgeBorderOf EdgeBorder { get; } = static (upperOrLeft, _) => upperOrLeft;

    /// <summary>
    /// The appearance of the cell at <paramref name="address"/>: its Font, Fill and Borders as the
    /// Sheet shows them, with <paramref name="numberFormatColour"/>, the colour of the Number
    /// Format's section that shows its Value, in place of the Font's colour.
    /// </summary>
    internal static CellAppearance Of(Sheet sheet, CellAddress address, NumberFormatColour? numberFormatColour)
    {
        var font = sheet.GetFont(address);
        var fill = sheet.GetFill(address);
        var borders = sheet.GetBorders(address);
        return new CellAppearance
        {
            FontColour = numberFormatColour is { } named ? RgbColour.FromRgb(named.Rgb()) : RgbOf(font.Colour),
            Bold = font.Bold,
            Italic = font.Italic,
            Underline = font.Underline,
            Strikethrough = font.Strikethrough,
            Fill = fill.Colour is { } colour ? RgbOf(colour) : null,
            Top = LineOf(borders.Top),
            Right = LineOf(borders.Right),
            Bottom = LineOf(borders.Bottom),
            Left = LineOf(borders.Left),
        };
    }

    /// <summary>A recorded colour as the core paints it; null for Automatic.</summary>
    internal static RgbColour? RgbOf(CellColour colour) => colour.IsAutomatic ? null : RgbColour.FromRgb(colour.Rgb);

    /// <summary>A Border side as the core draws it: Automatic is Excel's black.</summary>
    internal static Border LineOf(BorderLine line) =>
        line.IsNone ? Border.None : new Border(StyleOf(line.Style), RgbOf(line.Colour) ?? RgbColour.Black);

    /// <summary>The engine's line style as the core's: Excel's thirteen, by name.</summary>
    internal static BorderStyle StyleOf(BorderLineStyle style) => style switch
    {
        BorderLineStyle.None => BorderStyle.None,
        BorderLineStyle.Hair => BorderStyle.Hair,
        BorderLineStyle.Thin => BorderStyle.Thin,
        BorderLineStyle.Medium => BorderStyle.Medium,
        BorderLineStyle.Thick => BorderStyle.Thick,
        BorderLineStyle.Double => BorderStyle.Double,
        BorderLineStyle.Dotted => BorderStyle.Dotted,
        BorderLineStyle.Dashed => BorderStyle.Dashed,
        BorderLineStyle.DashDot => BorderStyle.DashDot,
        BorderLineStyle.DashDotDot => BorderStyle.DashDotDot,
        BorderLineStyle.MediumDashed => BorderStyle.MediumDashed,
        BorderLineStyle.MediumDashDot => BorderStyle.MediumDashDot,
        BorderLineStyle.MediumDashDotDot => BorderStyle.MediumDashDotDot,
        BorderLineStyle.SlantedDashDot => BorderStyle.SlantedDashDot,
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a line style."),
    };
}
