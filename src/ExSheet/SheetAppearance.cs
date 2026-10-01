using ExGrid;
using ExGrid.Cells;
using ExSheet.Engine;

namespace ExSheet;

/// <summary>
/// What ExSheet declares to the core for each cell's look (ADR-0050 item 15, ADR-0071): the Font
/// and the Fill of the cell's Cell Format, cell over row over column, as the engine shows them. The
/// core paints them; nothing here paints.
/// <list type="bullet">
/// <item><b>The Font's colour</b> is a recorded RGB value; Automatic declares none, and the cell's
/// text is the Ink (<c>ex-sheet.css</c>). A colour the Number Format names for the section that shows
/// the Value takes its place, as Excel's does (the eleventh Windows run, case 2), and a
/// <c>####</c> keeps it (case 3c).</item>
/// <item><b>A Fill</b> is a recorded RGB value, painted on a cell that holds nothing as well, when
/// its row or column records it.</item>
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
    /// The look the Cell Editor takes over the cell it edits (ADR-0050 item 15, ticket 88): the
    /// cell's Font and Fill, with the Font's own colour. A Number Format's colour paints the
    /// formatted Value, and the editor shows the Entry, so -5 in <c>0;[Red]-0</c> under a blue Font
    /// is edited in blue. Asked for the one cell an edit is open on, so it reads the engine afresh.
    /// </summary>
    internal static CellAppearanceOf<SheetRow> Editor { get; } =
        (row, column) => row.EditorAppearanceAt(SheetColumns.IndexOf(column));

    /// <summary>
    /// The appearance of the cell at <paramref name="address"/>: its Font and Fill as the Sheet shows
    /// them, with <paramref name="numberFormatColour"/>, the colour of the Number Format's section
    /// that shows its Value, in place of the Font's colour.
    /// </summary>
    internal static CellAppearance Of(Sheet sheet, CellAddress address, NumberFormatColour? numberFormatColour)
    {
        var font = sheet.GetFont(address);
        var fill = sheet.GetFill(address);
        return new CellAppearance
        {
            FontColour = numberFormatColour is { } named ? RgbColour.FromRgb(named.Rgb()) : RgbOf(font.Colour),
            Bold = font.Bold,
            Italic = font.Italic,
            Underline = font.Underline,
            Strikethrough = font.Strikethrough,
            Fill = fill.Colour is { } colour ? RgbOf(colour) : null,
        };
    }

    /// <summary>A recorded colour as the core paints it; null for Automatic.</summary>
    internal static RgbColour? RgbOf(CellColour colour) => colour.IsAutomatic ? null : RgbColour.FromRgb(colour.Rgb);
}
