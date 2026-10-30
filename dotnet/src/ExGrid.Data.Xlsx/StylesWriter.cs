using System.Globalization;
using System.Xml;

namespace ExGrid.Data.Xlsx;

/// <summary>Writes the styles part from <see cref="XlsxStyles"/>: what <see cref="StylesReader"/> reads, and the one named style
/// (Normal) every styles part carries.</summary>
internal static class StylesWriter
{
    public static void Write(XmlWriter w, XlsxStyles styles)
    {
        w.WriteStartElement("styleSheet", Names.Main);
        if (styles.NumberFormats.Count > 0)
        {
            w.WriteStartElement("numFmts");
            Count(w, styles.NumberFormats.Count);
            foreach (var (id, code) in styles.NumberFormats.OrderBy(p => p.Key))
            {
                w.WriteStartElement("numFmt");
                w.WriteAttributeString("numFmtId", Text(id));
                w.WriteAttributeString("formatCode", XString.Encode(code));
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }

        w.WriteStartElement("fonts");
        Count(w, styles.Fonts.Count);
        foreach (var font in styles.Fonts)
            WriteFont(w, font);
        w.WriteEndElement();

        w.WriteStartElement("fills");
        Count(w, styles.Fills.Count);
        foreach (var fill in styles.Fills)
            WriteFill(w, fill);
        w.WriteEndElement();

        w.WriteStartElement("borders");
        Count(w, styles.Borders.Count);
        foreach (var border in styles.Borders)
            WriteBorder(w, border);
        w.WriteEndElement();

        w.WriteStartElement("cellStyleXfs");
        Count(w, 1);
        w.WriteStartElement("xf");
        w.WriteAttributeString("numFmtId", "0");
        w.WriteAttributeString("fontId", "0");
        w.WriteAttributeString("fillId", "0");
        w.WriteAttributeString("borderId", "0");
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("cellXfs");
        Count(w, styles.CellFormats.Count);
        foreach (var format in styles.CellFormats)
            WriteCellFormat(w, format);
        w.WriteEndElement();

        w.WriteStartElement("cellStyles");
        Count(w, 1);
        w.WriteStartElement("cellStyle");
        w.WriteAttributeString("name", "Normal");
        w.WriteAttributeString("xfId", "0");
        w.WriteAttributeString("builtinId", "0");
        w.WriteEndElement();
        w.WriteEndElement();

        if (styles.IndexedColors is { Count: > 0 } indexed)
        {
            w.WriteStartElement("colors");
            w.WriteStartElement("indexedColors");
            foreach (var argb in indexed)
            {
                w.WriteStartElement("rgbColor");
                w.WriteAttributeString("rgb", argb.ToString("X8", CultureInfo.InvariantCulture));
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void Count(XmlWriter w, int count) => w.WriteAttributeString("count", Text(count));

    private static string Text(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static void WriteFont(XmlWriter w, XlsxFont font)
    {
        w.WriteStartElement("font");
        // The schema's order: b, i, strike, …, u, …, sz, color, name.
        if (font.Bold)
            w.WriteElementString("b", null);
        if (font.Italic)
            w.WriteElementString("i", null);
        if (font.Strike)
            w.WriteElementString("strike", null);
        if (font.Underline != XlsxUnderline.None)
        {
            w.WriteStartElement("u");
            if (font.Underline != XlsxUnderline.Single)
            {
                w.WriteAttributeString("val", font.Underline switch
                {
                    XlsxUnderline.Double => "double",
                    XlsxUnderline.SingleAccounting => "singleAccounting",
                    _ => "doubleAccounting",
                });
            }
            w.WriteEndElement();
        }
        if (font.Size is { } size)
        {
            w.WriteStartElement("sz");
            w.WriteAttributeString("val", size.ToString("R", CultureInfo.InvariantCulture));
            w.WriteEndElement();
        }
        if (font.Color is { } color)
            WriteColor(w, "color", color);
        if (font.Name is { } name)
        {
            w.WriteStartElement("name");
            w.WriteAttributeString("val", name);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteFill(XmlWriter w, XlsxFill fill)
    {
        w.WriteStartElement("fill");
        if (fill.IsGradient)
        {
            // A gradient's stops are not read, so none is written: an empty gradient.
            w.WriteStartElement("gradientFill");
            w.WriteEndElement();
        }
        else
        {
            w.WriteStartElement("patternFill");
            w.WriteAttributeString("patternType", fill.Pattern ?? "none");
            if (fill.Foreground is { } fg)
                WriteColor(w, "fgColor", fg);
            if (fill.Background is { } bg)
                WriteColor(w, "bgColor", bg);
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteBorder(XmlWriter w, XlsxBorder border)
    {
        w.WriteStartElement("border");
        if (border.DiagonalUp)
            w.WriteAttributeString("diagonalUp", "1");
        if (border.DiagonalDown)
            w.WriteAttributeString("diagonalDown", "1");
        WriteEdge(w, "left", border.Left);
        WriteEdge(w, "right", border.Right);
        WriteEdge(w, "top", border.Top);
        WriteEdge(w, "bottom", border.Bottom);
        WriteEdge(w, "diagonal", border.Diagonal);
        w.WriteEndElement();
    }

    private static void WriteEdge(XmlWriter w, string name, XlsxBorderEdge edge)
    {
        w.WriteStartElement(name);
        if (edge.Style != XlsxBorderStyle.None)
            w.WriteAttributeString("style", StylesReader.BorderStyleNames[(int)edge.Style]);
        if (edge.Color is { } color)
            WriteColor(w, "color", color);
        w.WriteEndElement();
    }

    private static void WriteCellFormat(XmlWriter w, XlsxCellFormat format)
    {
        w.WriteStartElement("xf");
        w.WriteAttributeString("numFmtId", Text(format.NumberFormatId));
        w.WriteAttributeString("fontId", Text(format.FontId));
        w.WriteAttributeString("fillId", Text(format.FillId));
        w.WriteAttributeString("borderId", Text(format.BorderId));
        w.WriteAttributeString("xfId", "0");
        if (format.NumberFormatId != 0)
            w.WriteAttributeString("applyNumberFormat", "1");
        if (format.FontId != 0)
            w.WriteAttributeString("applyFont", "1");
        if (format.FillId != 0)
            w.WriteAttributeString("applyFill", "1");
        if (format.BorderId != 0)
            w.WriteAttributeString("applyBorder", "1");
        if (format.Alignment is { } a)
        {
            w.WriteAttributeString("applyAlignment", "1");
            w.WriteStartElement("alignment");
            if (a.Horizontal != XlsxHorizontalAlignment.General)
                w.WriteAttributeString("horizontal", StylesReader.HorizontalNames[(int)a.Horizontal]);
            if (a.Vertical != XlsxVerticalAlignment.Bottom)
                w.WriteAttributeString("vertical", StylesReader.VerticalNames[(int)a.Vertical]);
            if (a.TextRotation != 0)
                w.WriteAttributeString("textRotation", Text(a.TextRotation));
            if (a.WrapText)
                w.WriteAttributeString("wrapText", "1");
            if (a.Indent != 0)
                w.WriteAttributeString("indent", Text(a.Indent));
            if (a.ShrinkToFit)
                w.WriteAttributeString("shrinkToFit", "1");
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void WriteColor(XmlWriter w, string name, XlsxColor color)
    {
        w.WriteStartElement(name);
        switch (color.Kind)
        {
            case XlsxColorKind.Automatic:
                w.WriteAttributeString("auto", "1");
                break;
            case XlsxColorKind.Rgb:
                w.WriteAttributeString("rgb", color.Value.ToString("X8", CultureInfo.InvariantCulture));
                break;
            case XlsxColorKind.Theme:
                w.WriteAttributeString("theme", color.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case XlsxColorKind.Indexed:
                w.WriteAttributeString("indexed", color.Value.ToString(CultureInfo.InvariantCulture));
                break;
        }
        if (color.Tint != 0)
            w.WriteAttributeString("tint", color.Tint.ToString("R", CultureInfo.InvariantCulture));
        w.WriteEndElement();
    }
}
