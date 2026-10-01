using ExGrid.Chrome;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;

namespace ExSheet;

/// <summary>Format Cells' tabs, in Excel's order: five of Excel's six, Protection left out (ADR-0071).</summary>
public enum FormatCellsTab
{
    /// <summary>The Number Format, by category.</summary>
    Number,

    /// <summary>The horizontal Alignment.</summary>
    Alignment,

    /// <summary>The Font: its style, underline, strikethrough and colour.</summary>
    Font,

    /// <summary>The Border: a line style and colour, the presets, and each edge.</summary>
    Border,

    /// <summary>The Fill: No Colour, the palette, or More Colours.</summary>
    Fill,
}

/// <summary>The Number tab's categories, in Excel's order (ADR-0071; the eleventh Windows run, case 22).</summary>
public enum NumberFormatCategory
{
    /// <summary>General.</summary>
    General,

    /// <summary>Number: decimal places, a thousands separator, and how a negative number shows.</summary>
    Number,

    /// <summary>Currency: decimal places and how a negative amount shows, with the Sheet culture's symbol.</summary>
    Currency,

    /// <summary>Accounting, which ExSheet does not read: it pads with <c>*</c> to fill the column.</summary>
    Accounting,

    /// <summary>Date: one of the date formats offered.</summary>
    Date,

    /// <summary>Time: one of the time formats offered.</summary>
    Time,

    /// <summary>Percentage: decimal places.</summary>
    Percentage,

    /// <summary>Fraction, which ExSheet does not read.</summary>
    Fraction,

    /// <summary>Scientific: decimal places.</summary>
    Scientific,

    /// <summary>Text: <c>@</c>.</summary>
    Text,

    /// <summary>Special, which ExSheet does not offer: its formats depend on the locale, and one takes a condition.</summary>
    Special,

    /// <summary>Custom: a format code as typed.</summary>
    Custom,
}

/// <summary>The Font tab's styles, as Excel lists them: emphasis as one choice of four.</summary>
public enum FontStyle
{
    /// <summary>Neither bold nor italic.</summary>
    Regular,

    /// <summary>Italic.</summary>
    Italic,

    /// <summary>Bold.</summary>
    Bold,

    /// <summary>Bold and italic.</summary>
    BoldItalic,
}

/// <summary>An edge the Border tab sets, relative to each selected range, as Excel's dialog sets it (ADR-0071).</summary>
public enum BorderEdge
{
    /// <summary>The range's top edge.</summary>
    Top,

    /// <summary>Every edge between two of the range's rows: Excel's Horizontal.</summary>
    InsideHorizontal,

    /// <summary>The range's bottom edge.</summary>
    Bottom,

    /// <summary>The range's left edge.</summary>
    Left,

    /// <summary>Every edge between two of the range's columns: Excel's Vertical.</summary>
    InsideVertical,

    /// <summary>The range's right edge.</summary>
    Right,
}

/// <summary>The Border tab's presets, as Excel's dialog offers them (the eleventh Windows run, case 22).</summary>
public enum BorderPreset
{
    /// <summary>No line on any edge of the range, outer or inner.</summary>
    None,

    /// <summary>The line on the range's four outer edges.</summary>
    Outline,

    /// <summary>The line on every edge inside the range; not offered for one cell.</summary>
    Inside,
}

/// <summary>Which colour a More Colours entry is for.</summary>
public enum ColourTarget
{
    /// <summary>The Font's colour.</summary>
    Font,

    /// <summary>The Fill.</summary>
    Fill,

    /// <summary>The Border's line colour.</summary>
    Border,
}

/// <summary>One colour of the palette, with the name Excel gives it (ADR-0071; the eleventh Windows run, case 23).</summary>
/// <param name="Name">Excel's name for it, such as <c>Dark Blue, Text 2, Lighter 90%</c>.</param>
/// <param name="Colour">The RGB value Excel names it with in Format Cells' Fill tab.</param>
public sealed record PaletteSwatch(string Name, CellColour Colour);

/// <summary>A Number Format category as Format Cells offers it: available, or shown disabled with the reason.</summary>
/// <param name="Category">The category.</param>
/// <param name="DisabledReason">Why it cannot be chosen, or <see langword="null"/> when it can.</param>
public sealed record NumberFormatCategoryOffer(NumberFormatCategory Category, string? DisabledReason)
{
    /// <summary>Whether it can be chosen.</summary>
    public bool Enabled => DisabledReason is null;
}

/// <summary>
/// The context of ExSheet's Format Cells seam (ADR-0071, ADR-0010's note of 2026-09-30). ExSheet
/// decides what is offered and what OK means; the Chrome draws it, in a frame of its own choosing,
/// and calls back.
///
/// <para><see cref="Draft"/> is what Format Cells opens on — the Focus cell's Cell Format, with the
/// parts that differ across the Selection shown as Excel shows them — and records what the user
/// touches. Every Chrome sets its controls through it, so what a choice means, and what OK sets, is
/// ExSheet's under every Chrome. <see cref="Tab"/> is the tab to open on, the last one shown in
/// this Sheet; the Chrome reports each tab it shows through <see cref="TabShown"/>.</para>
///
/// <para><see cref="Ok"/> sets the parts the user touched on the Selection as one undo step, and
/// answers whether Format Cells is done: false while the draft holds a <see cref="FormatCellsDraft.Refusal"/>,
/// which the Chrome shows, and Format Cells stays open. With nothing touched it sets nothing.
/// <see cref="Cancel"/> sets nothing. Either one ends Format Cells: ExSheet stops rendering the
/// Chrome's fragment, or closes the grid's popover under the built-in Chrome.</para>
///
/// <para><see cref="ReturnKeyboard"/> is the core's focus function (ADR-0021's note of 2026-09-30).
/// A Chrome whose frame lies outside the grid — a page-level dialog — calls it once that frame has
/// closed, so the keyboard is the Sheet's again; it is granted only while DOM focus is inside the
/// grid or on nothing. Under the built-in Chrome the grid's popover returns the keyboard itself.</para>
/// </summary>
/// <param name="Draft">What Format Cells opens on, and what the user has touched.</param>
/// <param name="Tab">The tab to open on.</param>
/// <param name="TabShown">The Chrome reports each tab it shows.</param>
/// <param name="Ok">Sets what was touched, and answers whether Format Cells is done.</param>
/// <param name="Cancel">Sets nothing, and ends Format Cells.</param>
/// <param name="ReturnKeyboard">The core's focus function.</param>
public sealed record FormatCellsContext(
    FormatCellsDraft Draft,
    FormatCellsTab Tab,
    Action<FormatCellsTab> TabShown,
    Func<Task<bool>> Ok,
    Action Cancel,
    Func<Task> ReturnKeyboard);

/// <summary>
/// ExSheet's Chrome seam (ADR-0071, ADR-0010's note of 2026-09-30): Format Cells, the one seam whose
/// frame is the Chrome's. A Chrome handed to <c>ExSheet.Chrome</c> that is also an
/// <see cref="ISheetChrome"/> draws Format Cells as well as the grid's seams.
/// </summary>
public interface ISheetChrome : IGridChrome
{
    /// <summary>
    /// Format Cells in a frame of the Chrome's own, such as a page-level dialog. ExSheet renders the
    /// fragment beside the grid, outside its root, from the opening until <see cref="FormatCellsContext.Ok"/>
    /// or <see cref="FormatCellsContext.Cancel"/> ends it. Null falls back to the built-in Format
    /// Cells, shown in the grid's popover frame (ADR-0050, item 16).
    /// </summary>
    RenderFragment? FormatCells(FormatCellsContext context) => null;
}

/// <summary>
/// What Format Cells offers, in Excel's order (ADR-0071; the eleventh Windows run, cases 22 and 23):
/// the same under every Chrome.
/// </summary>
public static class FormatCellsOffer
{
    /// <summary>The tabs, in Excel's order.</summary>
    public static IReadOnlyList<FormatCellsTab> Tabs { get; } =
        [FormatCellsTab.Number, FormatCellsTab.Alignment, FormatCellsTab.Font, FormatCellsTab.Border, FormatCellsTab.Fill];

    /// <summary>The Number tab's categories, in Excel's order, each available or disabled with the reason.</summary>
    public static IReadOnlyList<NumberFormatCategoryOffer> Categories { get; } =
    [
        new(NumberFormatCategory.General, null),
        new(NumberFormatCategory.Number, null),
        new(NumberFormatCategory.Currency, null),
        new(NumberFormatCategory.Accounting,
            "Accounting pads each value with * to fill its column, which ExSheet does not read. Use Currency, or type a code under Custom."),
        new(NumberFormatCategory.Date, null),
        new(NumberFormatCategory.Time, null),
        new(NumberFormatCategory.Percentage, null),
        new(NumberFormatCategory.Fraction, "Fractions are not among the format codes ExSheet reads."),
        new(NumberFormatCategory.Scientific, null),
        new(NumberFormatCategory.Text, null),
        new(NumberFormatCategory.Special,
            "Special's formats depend on the locale, and the phone number's takes a condition, which ExSheet does not read. Type a code under Custom."),
        new(NumberFormatCategory.Custom, null),
    ];

    /// <summary>The Alignment tab's horizontal alignments, as Excel lists them.</summary>
    public static IReadOnlyList<HorizontalAlignment> Alignments { get; } =
        [HorizontalAlignment.General, HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Right];

    /// <summary>The Font tab's styles, as Excel lists them.</summary>
    public static IReadOnlyList<FontStyle> FontStyles { get; } =
        [FontStyle.Regular, FontStyle.Italic, FontStyle.Bold, FontStyle.BoldItalic];

    /// <summary>
    /// The Border tab's line styles in Excel's two columns, each from the top: None, Hair, Dotted,
    /// Dash-dot-dot, Dash-dot, Dashed and Thin; then Medium Dash-dot-dot, Slanted Dash-dot, Medium
    /// Dash-dot, Medium Dashed, Medium, Thick and Double (case 22).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<BorderLineStyle>> LineStyleColumns { get; } =
    [
        [BorderLineStyle.None, BorderLineStyle.Hair, BorderLineStyle.Dotted, BorderLineStyle.DashDotDot, BorderLineStyle.DashDot, BorderLineStyle.Dashed, BorderLineStyle.Thin],
        [BorderLineStyle.MediumDashDotDot, BorderLineStyle.SlantedDashDot, BorderLineStyle.MediumDashDot, BorderLineStyle.MediumDashed, BorderLineStyle.Medium, BorderLineStyle.Thick, BorderLineStyle.Double],
    ];

    /// <summary>The Border tab's presets, in Excel's order.</summary>
    public static IReadOnlyList<BorderPreset> Presets { get; } = [BorderPreset.None, BorderPreset.Outline, BorderPreset.Inside];

    /// <summary>The Border tab's edges, in Excel's order, without its diagonals (ADR-0071).</summary>
    public static IReadOnlyList<BorderEdge> Edges { get; } =
        [BorderEdge.Top, BorderEdge.InsideHorizontal, BorderEdge.Bottom, BorderEdge.Left, BorderEdge.InsideVertical, BorderEdge.Right];

    /// <summary>
    /// The Office theme's colours (case 23): ten across, in six rows — the theme colours, then five
    /// rows of their tints and shades. Each records the RGB value Excel names it with in Format
    /// Cells' Fill tab, not the colour sampled from the screen; the two named "Grey" are
    /// <c>#7F7F7F</c>.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<PaletteSwatch>> ThemeColours { get; } = Theme();

    /// <summary>Excel's ten standard colours (case 23).</summary>
    public static IReadOnlyList<PaletteSwatch> StandardColours { get; } =
    [
        Swatch("Dark Red", 0xC00000), Swatch("Red", 0xFF0000), Swatch("Orange", 0xFFC000), Swatch("Yellow", 0xFFFF00),
        Swatch("Light Green", 0x92D050), Swatch("Green", 0x00B050), Swatch("Light Blue", 0x00B0F0), Swatch("Blue", 0x0070C0),
        Swatch("Dark Blue", 0x002060), Swatch("Purple", 0x7030A0),
    ];

    /// <summary>Excel's name for a line style, as its Border tab gives it.</summary>
    public static string NameOf(BorderLineStyle style) => style switch
    {
        BorderLineStyle.None => "None",
        BorderLineStyle.Hair => "Hair",
        BorderLineStyle.Dotted => "Dotted",
        BorderLineStyle.DashDotDot => "Dash-dot-dot",
        BorderLineStyle.DashDot => "Dash-dot",
        BorderLineStyle.Dashed => "Dashed",
        BorderLineStyle.Thin => "Thin",
        BorderLineStyle.MediumDashDotDot => "Medium Dash-dot-dot",
        BorderLineStyle.SlantedDashDot => "Slanted Dash-dot",
        BorderLineStyle.MediumDashDot => "Medium Dash-dot",
        BorderLineStyle.MediumDashed => "Medium Dashed",
        BorderLineStyle.Medium => "Medium",
        BorderLineStyle.Thick => "Thick",
        BorderLineStyle.Double => "Double",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a line style."),
    };

    /// <summary>Excel's name for a Number Format category.</summary>
    public static string NameOf(NumberFormatCategory category) => category.ToString();

    /// <summary>Excel's name for a tab.</summary>
    public static string NameOf(FormatCellsTab tab) => tab.ToString();

    /// <summary>Excel's name for a font style.</summary>
    public static string NameOf(FontStyle style) => style == FontStyle.BoldItalic ? "Bold Italic" : style.ToString();

    /// <summary>Excel's name for an edge.</summary>
    public static string NameOf(BorderEdge edge) => edge switch
    {
        BorderEdge.InsideHorizontal => "Horizontal",
        BorderEdge.InsideVertical => "Vertical",
        _ => edge.ToString(),
    };

    /// <summary>Excel's name for a horizontal alignment.</summary>
    public static string NameOf(HorizontalAlignment alignment) => alignment == HorizontalAlignment.Center ? "Centre" : alignment.ToString();

    private static PaletteSwatch Swatch(string name, int rgb) => new(name, CellColour.FromRgb(rgb));

    private static PaletteSwatch[][] Theme()
    {
        // The column's base name, then its RGB values from the theme row down, as the Fill tab names
        // them (case 22's "tab-fill"), and the steps its tooltips name (case 23).
        (string Name, int[] Rgb, string[] Steps)[] columns =
        [
            ("White, Background 1", [0xFFFFFF, 0xF2F2F2, 0xD9D9D9, 0xBFBFBF, 0xA6A6A6, 0x7F7F7F], ["Darker 5%", "Darker 15%", "Darker 25%", "Darker 35%", "Darker 50%"]),
            ("Black, Text 1", [0x000000, 0x7F7F7F, 0x595959, 0x404040, 0x262626, 0x0D0D0D], ["Lighter 50%", "Lighter 35%", "Lighter 25%", "Lighter 15%", "Lighter 5%"]),
            ("Light Grey, Background 2", [0xE8E8E8, 0xD0D0D0, 0xADADAD, 0x747474, 0x393939, 0x161616], ["Darker 10%", "Darker 25%", "Darker 50%", "Darker 75%", "Darker 90%"]),
            ("Dark Blue, Text 2", [0x0E2841, 0xDAE9F8, 0xA6C9EC, 0x4D93D9, 0x215C98, 0x153D64], ["Lighter 90%", "Lighter 75%", "Lighter 50%", "Lighter 25%", "Lighter 10%"]),
            ("Dark Teal, Accent 1", [0x156082, 0xC0E6F5, 0x83CCEB, 0x44B3E1, 0x104861, 0x0B3040], ["Lighter 80%", "Lighter 60%", "Lighter 40%", "Darker 25%", "Darker 50%"]),
            ("Orange, Accent 2", [0xE97132, 0xFBE2D5, 0xF7C7AC, 0xF1A983, 0xBE5014, 0x7E350E], ["Lighter 80%", "Lighter 60%", "Lighter 40%", "Darker 25%", "Darker 50%"]),
            ("Dark Green, Accent 3", [0x196B24, 0xC1F0C8, 0x83E28E, 0x47D359, 0x12501A, 0x0D3512], ["Lighter 80%", "Lighter 60%", "Lighter 40%", "Darker 25%", "Darker 50%"]),
            ("Turquoise, Accent 4", [0x0F9ED5, 0xCAEDFB, 0x94DCF8, 0x61CBF3, 0x0C769E, 0x074F69], ["Lighter 80%", "Lighter 60%", "Lighter 40%", "Darker 25%", "Darker 50%"]),
            ("Plum, Accent 5", [0xA02B93, 0xF2CEEF, 0xE49EDD, 0xD86DCD, 0x782170, 0x50154A], ["Lighter 80%", "Lighter 60%", "Lighter 40%", "Darker 25%", "Darker 50%"]),
            ("Green, Accent 6", [0x4EA72E, 0xDAF2D0, 0xB5E6A2, 0x8ED973, 0x3C7D22, 0x275317], ["Lighter 80%", "Lighter 60%", "Lighter 40%", "Darker 25%", "Darker 50%"]),
        ];
        var rows = new PaletteSwatch[6][];
        for (var row = 0; row < 6; row++)
        {
            rows[row] = new PaletteSwatch[columns.Length];
            for (var column = 0; column < columns.Length; column++)
            {
                var (name, rgb, steps) = columns[column];
                rows[row][column] = Swatch(row == 0 ? name : $"{name}, {steps[row - 1]}", rgb[row]);
            }
        }
        return rows;
    }
}
