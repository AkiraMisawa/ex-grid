using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// A file's cell formats read into Cell Formats (ADR-0330 §4, §8, ADR-0071, ticket 03): horizontal alignment, the Font's
/// colour, bold, italic, underline and strikethrough, solid fills and the four sides' borders cross; a theme colour is the RGB
/// the file's theme gives it; what a Sheet does not have is listed in the Open Report and opens at its default.
/// </summary>
public class CellFormatTests
{
    /// <summary>The workbook's font, as Excel writes it: Calibri 11 in Text 1.</summary>
    private static readonly XlsxFont Normal = new("Calibri", 11, Color: XlsxColor.FromTheme(1));

    /// <summary>A workbook of one cell, B7, in a cell format of <paramref name="font"/>, <paramref name="fill"/>, <paramref name="border"/> and <paramref name="alignment"/>.</summary>
    private static Task<byte[]> StyledAsync(XlsxFont? font = null, XlsxFill? fill = null, XlsxBorder? border = null, XlsxAlignment? alignment = null)
    {
        var styles = new XlsxStyles(
            new Dictionary<int, string>(),
            [Normal, font ?? Normal],
            [XlsxFill.None, XlsxFill.Gray125, fill ?? XlsxFill.None],
            [XlsxBorder.None, border ?? XlsxBorder.None],
            [new XlsxCellFormat(), new XlsxCellFormat(0, 1, 2, 1, alignment)]);
        return WriteAsync(new XlsxWriteOptions { Styles = styles }, new Page("Trades", [Number("B7", "1", style: 1)]));
    }

    private static async Task<(CellFormat Format, OpenReport Report)> ReadB7Async(byte[] file)
    {
        var (document, report) = await ReadAsync(file);
        return (Workbook.Open(document).Sheets[0].GetCellFormat(CellAddress.Parse("B7")), report);
    }

    // ---- alignment -------------------------------------------------------------------------------------------------------

    [Theory] // ADR-0330 §4: horizontal alignment crosses where a Sheet has it; the others open as General and are listed
    [InlineData(XlsxHorizontalAlignment.General, HorizontalAlignment.General, false)]
    [InlineData(XlsxHorizontalAlignment.Left, HorizontalAlignment.Left, false)]
    [InlineData(XlsxHorizontalAlignment.Center, HorizontalAlignment.Center, false)]
    [InlineData(XlsxHorizontalAlignment.Right, HorizontalAlignment.Right, false)]
    [InlineData(XlsxHorizontalAlignment.Fill, HorizontalAlignment.General, true)]
    [InlineData(XlsxHorizontalAlignment.Justify, HorizontalAlignment.General, true)]
    [InlineData(XlsxHorizontalAlignment.CenterContinuous, HorizontalAlignment.General, true)]
    [InlineData(XlsxHorizontalAlignment.Distributed, HorizontalAlignment.General, true)]
    public async Task ADR0330_horizontal_alignment_crosses_where_a_Sheet_has_it(XlsxHorizontalAlignment file, HorizontalAlignment opened, bool listed)
    {
        var (format, report) = await ReadB7Async(await StyledAsync(alignment: new XlsxAlignment(file)));

        Assert.Equal(opened, format.Alignment);
        Assert.Equal(listed, report.NotCarried.Any(n => n is { Kind: NotCarriedKind.HorizontalAlignment, Sheet: "Trades", Detail: "B7" }));
        Assert.Equal(!listed, report.IsEmpty);
    }

    [Theory] // ADR-0330 §4: vertical alignment, wrapping, indent, rotation and shrinking are not carried, and are listed
    [InlineData(XlsxVerticalAlignment.Top, false, 0, 0, false, NotCarriedKind.VerticalAlignment)]
    [InlineData(XlsxVerticalAlignment.Center, false, 0, 0, false, NotCarriedKind.VerticalAlignment)]
    [InlineData(XlsxVerticalAlignment.Bottom, true, 0, 0, false, NotCarriedKind.TextLayout)]
    [InlineData(XlsxVerticalAlignment.Bottom, false, 2, 0, false, NotCarriedKind.TextLayout)]
    [InlineData(XlsxVerticalAlignment.Bottom, false, 0, 45, false, NotCarriedKind.TextLayout)]
    [InlineData(XlsxVerticalAlignment.Bottom, false, 0, 255, false, NotCarriedKind.TextLayout)]
    [InlineData(XlsxVerticalAlignment.Bottom, false, 0, 0, true, NotCarriedKind.TextLayout)]
    public async Task ADR0330_vertical_alignment_and_text_layout_are_listed(XlsxVerticalAlignment vertical, bool wrap, int indent, int rotation, bool shrink, NotCarriedKind kind)
    {
        var (format, report) = await ReadB7Async(await StyledAsync(alignment: new XlsxAlignment(XlsxHorizontalAlignment.Right, vertical, wrap, indent, rotation, shrink)));

        Assert.Equal(HorizontalAlignment.Right, format.Alignment);
        var listed = Assert.Single(report.NotCarried);
        Assert.Equal((kind, "Trades", "B7"), (listed.Kind, listed.Sheet, listed.Detail));
    }

    // ---- font ------------------------------------------------------------------------------------------------------------

    [Fact] // ADR-0330 §4: bold, italic, a single underline and strikethrough cross; the workbook's own Text 1 is the Ink, Automatic
    public async Task ADR0330_the_Fonts_emphasis_crosses()
    {
        var (format, report) = await ReadB7Async(await StyledAsync(Normal with { Bold = true, Italic = true, Underline = XlsxUnderline.Single, Strike = true }));

        Assert.Equal(new CellFont(CellColour.Automatic, Bold: true, Italic: true, Underline: true, Strikethrough: true), format.Font);
        Assert.True(report.IsEmpty);
    }

    [Theory] // ADR-0330 §4, ADR-0071: a Sheet underlines once; a double or accounting underline opens single, and is listed
    [InlineData(XlsxUnderline.Double)]
    [InlineData(XlsxUnderline.SingleAccounting)]
    [InlineData(XlsxUnderline.DoubleAccounting)]
    public async Task ADR0330_a_double_or_accounting_underline_opens_single(XlsxUnderline underline)
    {
        var (format, report) = await ReadB7Async(await StyledAsync(Normal with { Underline = underline }));

        Assert.True(format.Font.Underline);
        Assert.Equal(NotCarriedKind.Underline, Assert.Single(report.NotCarried).Kind);
    }

    [Theory] // ADR-0330 §4, ADR-0071: a typeface or a size other than the workbook's is not carried, and is listed
    [InlineData("Arial", 11.0)]
    [InlineData("Calibri", 14.0)]
    [InlineData("calibri", 11.0)]
    public async Task ADR0330_another_typeface_or_size_is_listed(string name, double size)
    {
        var (_, report) = await ReadB7Async(await StyledAsync(Normal with { Name = name, Size = size, Bold = true }));

        var listed = name == "calibri" ? null : Assert.Single(report.NotCarried);
        Assert.Equal(name == "calibri" ? null : NotCarriedKind.FontNamesAndSizes, listed?.Kind);
        Assert.Equal(name == "calibri", report.IsEmpty);
    }

    [Fact] // ADR-0330 §4, §8: a Font's colour is the RGB it names, through the palette or the theme; automatic is Automatic
    public async Task ADR0330_a_Fonts_colour_is_its_RGB()
    {
        Assert.Equal(CellColour.FromRgb(0xC00000), (await ReadB7Async(await StyledAsync(Normal with { Color = XlsxColor.FromArgb(0xFFC00000) }))).Format.Font.Colour);
        Assert.Equal(CellColour.FromRgb(0xFF0000), (await ReadB7Async(await StyledAsync(Normal with { Color = XlsxColor.FromIndexed(10) }))).Format.Font.Colour);
        Assert.Equal(CellColour.Automatic, (await ReadB7Async(await StyledAsync(Normal with { Color = XlsxColor.Automatic }))).Format.Font.Colour);
        Assert.Equal(CellColour.Automatic, (await ReadB7Async(await StyledAsync(Normal with { Color = XlsxColor.FromIndexed(64) }))).Format.Font.Colour);
        Assert.Equal(CellColour.Automatic, (await ReadB7Async(await StyledAsync(Normal with { Color = null }))).Format.Font.Colour);
        // Black chosen as a colour, not the workbook's own Text 1, is black.
        Assert.Equal(CellColour.FromRgb(0x000000), (await ReadB7Async(await StyledAsync(Normal with { Color = XlsxColor.FromArgb(0xFF000000) }))).Format.Font.Colour);
    }

    // ---- theme -----------------------------------------------------------------------------------------------------------

    [Theory] // ADR-0330 §8: a theme colour with its tint is the RGB the theme gives it, and the link is listed once, of the workbook
    [InlineData(4, 0.0)]
    [InlineData(5, -0.249977111117893)]
    [InlineData(0, -0.149998474074526)]
    [InlineData(9, 0.399975585192419)]
    [InlineData(1, 0.499984740745262)]
    public async Task ADR0330_a_theme_colour_is_its_RGB_and_the_link_is_listed(int slot, double tint)
    {
        var colour = XlsxColor.FromTheme(slot, tint);
        var expected = CellColour.FromRgb((int)(XlsxTheme.Office.Resolve(colour)!.Value & 0xFFFFFF));

        var (format, report) = await ReadB7Async(await StyledAsync(Normal with { Color = colour }, XlsxFill.Solid(colour), new XlsxBorder(
            new XlsxBorderEdge(XlsxBorderStyle.Thin, colour), XlsxBorderEdge.None, XlsxBorderEdge.None, XlsxBorderEdge.None, XlsxBorderEdge.None)));

        Assert.Equal(expected, format.Font.Colour);
        Assert.Equal(CellFill.Solid(expected), format.Fill);
        Assert.Equal(new BorderLine(BorderLineStyle.Thin, expected), format.Borders.Left);
        var listed = Assert.Single(report.NotCarried);
        Assert.Equal((NotCarriedKind.ThemeColours, (string?)null), (listed.Kind, listed.Sheet));
    }

    [Fact] // ADR-0330 §8: the theme is the file's own: its scheme decides the RGB
    public async Task ADR0330_a_theme_colour_is_resolved_through_the_files_own_theme()
    {
        var file = WithTheme(await StyledAsync(fill: XlsxFill.Solid(XlsxColor.FromTheme(4)), font: Normal with { Color = XlsxColor.FromTheme(3) }),
            "000000", "FFFFFF", "1F497D", "EEECE1", "4F81BD", "C0504D", "9BBB59", "8064A2", "4BACC6", "F79646", "0000FF", "800080");

        var (format, _) = await ReadB7Async(file);

        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x4F81BD)), format.Fill);
        Assert.Equal(CellColour.FromRgb(0x1F497D), format.Font.Colour);
    }

    [Fact] // ADR-0330 §5, §8: the theme's entry is the workbook's, before each Sheet's
    public async Task ADR0330_the_theme_entry_comes_before_each_Sheets()
    {
        var styles = XlsxStyles.Default with
        {
            Fonts = [Normal, Normal with { Name = "Arial" }],
            Fills = [XlsxFill.None, XlsxFill.Gray125, XlsxFill.Solid(XlsxColor.FromTheme(5))],
            CellFormats = [new XlsxCellFormat(), new XlsxCellFormat(0, 1, 2, 0)],
        };
        var file = await WriteAsync(new XlsxWriteOptions { Styles = styles, DefinedNames = [new XlsxDefinedName("Rate", "Trades!$A$1")] },
            new Page("Trades", [Number("A1", "1"), Number("C3", "2", style: 1)]), new Page("Desks", [Number("D4", "3", style: 1)]));

        var report = (await ReadAsync(file)).Report;

        Assert.Equal(
            [(NotCarriedKind.DefinedName, null, "Rate"), (NotCarriedKind.ThemeColours, null, null), (NotCarriedKind.FontNamesAndSizes, "Trades", "C3"), (NotCarriedKind.FontNamesAndSizes, "Desks", "D4")],
            report.NotCarried.Select(n => (n.Kind, n.Sheet, n.Detail)));
    }

    // ---- fill ------------------------------------------------------------------------------------------------------------

    [Fact] // ADR-0330 §4: a solid fill crosses as its RGB; no fill is none
    public async Task ADR0330_a_solid_fill_crosses()
    {
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0xFFFF00)), (await ReadB7Async(await StyledAsync(fill: XlsxFill.Solid(XlsxColor.FromArgb(0xFFFFFF00))))).Format.Fill);
        Assert.Equal(CellFill.Solid(CellColour.FromRgb(0x00FF00)), (await ReadB7Async(await StyledAsync(fill: XlsxFill.Solid(XlsxColor.FromIndexed(11))))).Format.Fill);
        Assert.Equal(CellFill.None, (await ReadB7Async(await StyledAsync(fill: XlsxFill.None))).Format.Fill);
    }

    [Theory] // ADR-0330 §4, ADR-0071: pattern and gradient fills, and a solid one of no colour, open with no Fill, and are listed
    [InlineData("gray125")]
    [InlineData("darkGrid")]
    [InlineData("lightTrellis")]
    [InlineData("gradient")]
    [InlineData("solid-automatic")]
    [InlineData("solid-none")]
    public async Task ADR0330_a_pattern_or_gradient_fill_is_listed(string kind)
    {
        var fill = kind switch
        {
            "gradient" => new XlsxFill(null, IsGradient: true),
            "solid-automatic" => XlsxFill.Solid(XlsxColor.Automatic),
            "solid-none" => new XlsxFill("solid"),
            _ => new XlsxFill(kind, XlsxColor.FromArgb(0xFFFF0000), XlsxColor.FromArgb(0xFF0000FF)),
        };

        var (format, report) = await ReadB7Async(await StyledAsync(fill: fill));

        Assert.Equal(CellFill.None, format.Fill);
        Assert.Equal((NotCarriedKind.PatternFills, "B7"), (Assert.Single(report.NotCarried).Kind, report.NotCarried[0].Detail));
    }

    // ---- borders ---------------------------------------------------------------------------------------------------------

    [Theory] // ADR-0330 §4, ADR-0071: every one of Excel's thirteen line styles crosses as the same, on the side it is on
    [InlineData(XlsxBorderStyle.Hair, BorderLineStyle.Hair)]
    [InlineData(XlsxBorderStyle.Thin, BorderLineStyle.Thin)]
    [InlineData(XlsxBorderStyle.Medium, BorderLineStyle.Medium)]
    [InlineData(XlsxBorderStyle.Thick, BorderLineStyle.Thick)]
    [InlineData(XlsxBorderStyle.Double, BorderLineStyle.Double)]
    [InlineData(XlsxBorderStyle.Dotted, BorderLineStyle.Dotted)]
    [InlineData(XlsxBorderStyle.Dashed, BorderLineStyle.Dashed)]
    [InlineData(XlsxBorderStyle.DashDot, BorderLineStyle.DashDot)]
    [InlineData(XlsxBorderStyle.DashDotDot, BorderLineStyle.DashDotDot)]
    [InlineData(XlsxBorderStyle.MediumDashed, BorderLineStyle.MediumDashed)]
    [InlineData(XlsxBorderStyle.MediumDashDot, BorderLineStyle.MediumDashDot)]
    [InlineData(XlsxBorderStyle.MediumDashDotDot, BorderLineStyle.MediumDashDotDot)]
    [InlineData(XlsxBorderStyle.SlantDashDot, BorderLineStyle.SlantedDashDot)]
    [InlineData(XlsxBorderStyle.None, BorderLineStyle.None)]
    public async Task ADR0330_every_line_style_crosses(XlsxBorderStyle file, BorderLineStyle opened)
    {
        var red = XlsxColor.FromArgb(0xFFFF0000);
        var border = new XlsxBorder(new XlsxBorderEdge(file, red), new XlsxBorderEdge(file), new XlsxBorderEdge(file, XlsxColor.FromIndexed(64)), new XlsxBorderEdge(file, XlsxColor.FromArgb(0xFF00B050)), XlsxBorderEdge.None);

        var (format, report) = await ReadB7Async(await StyledAsync(border: border));

        BorderLine Line(CellColour colour) => opened == BorderLineStyle.None ? BorderLine.None : new BorderLine(opened, colour);
        Assert.Equal(new CellBorders(Top: Line(CellColour.Automatic), Bottom: Line(CellColour.FromRgb(0x00B050)), Left: Line(CellColour.FromRgb(0xFF0000)), Right: Line(CellColour.Automatic)), format.Borders);
        Assert.True(report.IsEmpty);
        Assert.Equal(file, StyleReading.FileStyle(opened));
    }

    [Fact] // ADR-0330 §4, ADR-0071: a diagonal border is not carried, and is listed; the four sides are
    public async Task ADR0330_a_diagonal_border_is_listed()
    {
        var border = new XlsxBorder(XlsxBorderEdge.None, XlsxBorderEdge.None, new XlsxBorderEdge(XlsxBorderStyle.Thick), XlsxBorderEdge.None, new XlsxBorderEdge(XlsxBorderStyle.Thin), DiagonalDown: true);

        var (format, report) = await ReadB7Async(await StyledAsync(border: border));

        Assert.Equal(new CellBorders(Top: new BorderLine(BorderLineStyle.Thick)), format.Borders);
        Assert.Equal(NotCarriedKind.DiagonalBorders, Assert.Single(report.NotCarried).Kind);
    }

    // ---- the three levels ------------------------------------------------------------------------------------------------

    [Fact] // ADR-0330 §4, ADR-0047: a column's format, a row's that stands in place of its columns', and a cell's that stands alone, as Excel shows them
    public async Task ADR0330_cell_row_and_column_formats_show_as_in_Excel()
    {
        var styles = XlsxStyles.Default with
        {
            Fonts = [Normal, Normal with { Bold = true }],
            Fills = [XlsxFill.None, XlsxFill.Gray125, XlsxFill.Solid(XlsxColor.FromArgb(0xFFFFFF00))],
            CellFormats = [new XlsxCellFormat(), new XlsxCellFormat(4, 1), new XlsxCellFormat(10, 0, 2), new XlsxCellFormat(0, 1, 2)],
        };
        var file = await WriteAsync(new XlsxWriteOptions { Styles = styles },
            new Page("Trades",
                [Number("B3", "1"), Number("A3", "2", style: 3), Number("B5", "3"), Number("B6", "4", style: 1), Number("C5", "5")],
                [new XlsxColumn(1, 2, null, StyleIndex: 1)],
                Rows: [new XlsxRow(2, 2)]));

        var (document, report) = await ReadAsync(file);
        var sheet = Workbook.Open(document).Sheets[0];
        CellFormat At(string at) => sheet.GetCellFormat(CellAddress.Parse(at));
        var bold = CellFormat.Default with { NumberFormat = NumberFormat.Parse("#,##0.00"), Font = new CellFont(Bold: true) };
        var yellow = CellFormat.Default with { NumberFormat = NumberFormat.Parse("0.00%"), Fill = CellFill.Solid(CellColour.FromRgb(0xFFFF00)) };

        Assert.Equal(bold, At("B9"));           // empty, in column B
        Assert.Equal(bold, At("C1"));           // empty, in column C
        Assert.Equal(CellFormat.Default, At("B3")); // a cell of its own, in style 0: the cell's format stands alone
        Assert.Equal(yellow, At("C3"));         // empty, in row 3: the row's format stands in place of column C's
        Assert.Equal(yellow, At("Z3"));         // empty, in row 3
        Assert.Equal(CellFormat.Default with { Font = new CellFont(Bold: true), Fill = yellow.Fill }, At("A3"));
        Assert.Equal(CellFormat.Default, At("B5")); // a cell of its own in column B, style 0
        Assert.Equal(bold, At("B6"));           // a cell of its own, in the column's style
        Assert.Equal(CellFormat.Default, At("A1"));
        Assert.True(report.IsEmpty);
    }
}
