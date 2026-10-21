using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;

namespace ExSheet.Xlsx;

/// <summary>
/// The styles part a written workbook's Cell Formats need (ADR-0330 §4, §8, ADR-0071), built as the formats are met: each
/// distinct Cell Format one cell format of the file, its index what a cell, a row or a column names. Index 0 is the default
/// Cell Format. Every font is the workbook's own, Calibri 11, with the Font's emphasis; a colour is written as its RGB value,
/// and Automatic as none, which Excel paints as its automatic colour. A Number Format is its built-in id where it is one
/// (<see cref="XlsxNumberFormats.BuiltInId"/>), and a format of the file's own from 164 otherwise, the engine's built-in currency
/// spelled in the Workbook culture's currency (<see cref="XlsxNumberFormats.WrittenCode"/>).
/// </summary>
internal sealed class StyleTable
{
    /// <summary>The workbook's font: the one a written workbook's widths are counted in (<see cref="ColumnWidthUnit"/>).</summary>
    public static XlsxFont WorkbookFont { get; } = new("Calibri", 11);

    private readonly Dictionary<CellFormat, int> formats = [];
    private readonly List<XlsxCellFormat> cellFormats = [];
    private readonly Dictionary<string, int> codes = new(StringComparer.Ordinal);
    private readonly Dictionary<XlsxFont, int> fonts = [];
    private readonly Dictionary<XlsxFill, int> fills = [];
    private readonly Dictionary<XlsxBorder, int> borders = [];

    private readonly CultureInfo culture;

    /// <summary>A table for a Workbook of <paramref name="culture"/>, holding the default Cell Format.</summary>
    public StyleTable(CultureInfo culture)
    {
        this.culture = culture;
        Font(WorkbookFont);
        Fill(XlsxFill.None);
        Fill(XlsxFill.Gray125);
        Border(XlsxBorder.None);
        Index(CellFormat.Default);
    }

    /// <summary>The index of the cell format that writes <paramref name="format"/>, added when it is new.</summary>
    public int Index(CellFormat format)
    {
        if (formats.TryGetValue(format, out var index)) return index;
        var numberFormat = XlsxNumberFormats.BuiltInId(format.NumberFormat) ?? Code(XlsxNumberFormats.WrittenCode(format.NumberFormat, culture));
        var f = format.Font;
        var font = Font(WorkbookFont with
        {
            Bold = f.Bold,
            Italic = f.Italic,
            Underline = f.Underline ? XlsxUnderline.Single : XlsxUnderline.None,
            Strike = f.Strikethrough,
            Color = Color(f.Colour),
        });
        var fill = format.Fill.Colour is { } colour ? Fill(XlsxFill.Solid(Color(colour)!.Value)) : 0;
        var b = format.Borders;
        var border = Border(new XlsxBorder(Edge(b.Left), Edge(b.Right), Edge(b.Top), Edge(b.Bottom), XlsxBorderEdge.None));
        XlsxAlignment? alignment = format.Alignment switch
        {
            HorizontalAlignment.Left => new XlsxAlignment(XlsxHorizontalAlignment.Left),
            HorizontalAlignment.Center => new XlsxAlignment(XlsxHorizontalAlignment.Center),
            HorizontalAlignment.Right => new XlsxAlignment(XlsxHorizontalAlignment.Right),
            _ => null,
        };
        index = cellFormats.Count;
        cellFormats.Add(new XlsxCellFormat(numberFormat, font, fill, border, alignment));
        formats[format] = index;
        return index;
    }

    /// <summary>The styles part, holding every format met so far.</summary>
    public XlsxStyles Build() => new(
        codes.ToDictionary(p => p.Value, p => p.Key),
        [.. fonts.OrderBy(p => p.Value).Select(p => p.Key)],
        [.. fills.OrderBy(p => p.Value).Select(p => p.Key)],
        [.. borders.OrderBy(p => p.Value).Select(p => p.Key)],
        [.. cellFormats]);

    private int Code(string code)
    {
        if (!codes.TryGetValue(code, out var id))
        {
            id = XlsxNumberFormats.FirstCustomId + codes.Count;
            codes[code] = id;
        }
        return id;
    }

    private static XlsxColor? Color(CellColour colour) => colour.IsAutomatic ? null : XlsxColor.FromArgb(0xFF000000 | (uint)colour.Rgb);

    private static XlsxBorderEdge Edge(BorderLine line) =>
        line.IsNone ? XlsxBorderEdge.None : new XlsxBorderEdge(StyleReading.FileStyle(line.Style), Color(line.Colour));

    private int Font(XlsxFont font) => Add(fonts, font);

    private int Fill(XlsxFill fill) => Add(fills, fill);

    private int Border(XlsxBorder border) => Add(borders, border);

    private static int Add<T>(Dictionary<T, int> table, T item)
        where T : notnull
    {
        if (!table.TryGetValue(item, out var index))
        {
            index = table.Count;
            table[item] = index;
        }
        return index;
    }
}
