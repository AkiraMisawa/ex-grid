using System.Globalization;
using System.Xml;

namespace ExGrid.Data.Xlsx;

/// <summary>Reads the styles part as far as ADR-0330 §4 carries it, and nothing past it.</summary>
internal static class StylesReader
{
    /// <summary>The first Number Format id a styles part defines for itself; the ones below it are built in.</summary>
    internal const int FirstCustomNumberFormat = 164;

    public static XlsxStyles Read(Package package, string part)
    {
        var numberFormats = new Dictionary<int, string>();
        var fonts = new List<XlsxFont>();
        var fills = new List<XlsxFill>();
        var borders = new List<XlsxBorder>();
        var cellFormats = new List<XlsxCellFormat>();
        List<uint>? indexed = null;
        using var xml = package.OpenXml(part);
        if (xml is null)
            return XlsxStyles.Default;
        try
        {
            xml.MoveToContent();
            if (xml.IsEmptyElement)
                return XlsxStyles.Default;
            var depth = xml.Depth;
            xml.Read();
            while (!xml.EOF && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
            {
                if (xml.NodeType != XmlNodeType.Element)
                {
                    xml.Read();
                    continue;
                }
                switch (xml.LocalName)
                {
                    case "numFmts":
                        foreach (var _ in Children(xml, "numFmt"))
                        {
                            if (xml.GetAttribute("numFmtId") is { } id && xml.GetAttribute("formatCode") is { } code)
                                numberFormats[XlsxReader.Int(id, part)] = XString.Decode(code);
                        }
                        break;
                    case "fonts":
                        foreach (var _ in Children(xml, "font"))
                            fonts.Add(ReadFont(xml, part));
                        break;
                    case "fills":
                        foreach (var _ in Children(xml, "fill"))
                            fills.Add(ReadFill(xml, part));
                        break;
                    case "borders":
                        foreach (var _ in Children(xml, "border"))
                            borders.Add(ReadBorder(xml, part));
                        break;
                    case "cellXfs":
                        foreach (var _ in Children(xml, "xf"))
                            cellFormats.Add(ReadCellFormat(xml, part));
                        break;
                    case "colors":
                        indexed = ReadIndexedColors(xml, part);
                        break;
                    default:
                        xml.Skip();
                        break;
                }
            }
        }
        catch (XmlException e)
        {
            throw package.Refuse(e, part);
        }
        if (fonts.Count == 0)
            fonts.AddRange(XlsxStyles.Default.Fonts);
        if (fills.Count == 0)
            fills.AddRange(XlsxStyles.Default.Fills);
        if (borders.Count == 0)
            borders.AddRange(XlsxStyles.Default.Borders);
        if (cellFormats.Count == 0)
            cellFormats.AddRange(XlsxStyles.Default.CellFormats);
        for (var i = 0; i < cellFormats.Count; i++)
        {
            var f = cellFormats[i];
            void Check(string what, int id, int count)
            {
                if (id < 0 || id >= count)
                {
                    throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture,
                        $"The cell format {i} names the {what} {id}, which the styles do not hold."), part);
                }
            }
            Check("font", f.FontId, fonts.Count);
            Check("fill", f.FillId, fills.Count);
            Check("border", f.BorderId, borders.Count);
            // Below 164 a Number Format is built in, or one the part may redefine; from 164 it is the part's own.
            if (f.NumberFormatId < 0 || (f.NumberFormatId >= FirstCustomNumberFormat && !numberFormats.ContainsKey(f.NumberFormatId)))
            {
                throw new XlsxException(XlsxRefusal.Malformed, string.Create(CultureInfo.InvariantCulture,
                    $"The cell format {i} names the Number Format {f.NumberFormatId}, which the styles do not hold."), part);
            }
        }
        return new XlsxStyles(numberFormats, fonts, fills, borders, cellFormats, indexed);
    }

    /// <summary>
    /// Walks the children of the element the reader is on, stopping on each one named <paramref name="name"/> with the
    /// reader on its start; the caller reads it or leaves it, and the walk goes past it. Leaves the reader after the
    /// element's end.
    /// </summary>
    private static IEnumerable<int> Children(XmlReader xml, string name)
    {
        if (xml.IsEmptyElement)
        {
            xml.Read();
            yield break;
        }
        var depth = xml.Depth;
        xml.Read();
        var index = 0;
        while (!xml.EOF && !(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.NodeType == XmlNodeType.Element && xml.Depth == depth + 1 && xml.LocalName == name)
            {
                yield return index++;
                // The caller read the attributes only; the element and its content go now.
                xml.Skip();
                continue;
            }
            if (xml.NodeType == XmlNodeType.Element)
                xml.Skip();
            else
                xml.Read();
        }
        xml.Read();
    }

    private static XlsxFont ReadFont(XmlReader xml, string part)
    {
        string? name = null;
        double? size = null;
        bool bold = false, italic = false, strike = false;
        var underline = XlsxUnderline.None;
        XlsxColor? color = null;
        using var sub = xml.ReadSubtree();
        sub.Read();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element || sub.Depth != 1)
                continue;
            var val = sub.GetAttribute("val");
            switch (sub.LocalName)
            {
                case "name":
                    name = val;
                    break;
                case "sz":
                    size = val is null ? null : XlsxReader.Double(val, part);
                    break;
                case "b":
                    bold = val is null || XlsxReader.Bool(val);
                    break;
                case "i":
                    italic = val is null || XlsxReader.Bool(val);
                    break;
                case "strike":
                    strike = val is null || XlsxReader.Bool(val);
                    break;
                case "u":
                    underline = val switch
                    {
                        null or "single" => XlsxUnderline.Single,
                        "double" => XlsxUnderline.Double,
                        "singleAccounting" => XlsxUnderline.SingleAccounting,
                        "doubleAccounting" => XlsxUnderline.DoubleAccounting,
                        "none" => XlsxUnderline.None,
                        _ => throw new XlsxException(XlsxRefusal.Malformed, $"'{val}' is not an underline.", part),
                    };
                    break;
                case "color":
                    color = ReadColor(sub, part);
                    break;
            }
        }
        return new XlsxFont(name, size, bold, italic, underline, strike, color);
    }

    private static XlsxFill ReadFill(XmlReader xml, string part)
    {
        using var sub = xml.ReadSubtree();
        sub.Read();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element || sub.Depth != 1)
                continue;
            if (sub.LocalName == "gradientFill")
                return new XlsxFill(null, IsGradient: true);
            if (sub.LocalName != "patternFill")
                continue;
            var pattern = sub.GetAttribute("patternType") ?? "none";
            XlsxColor? fg = null, bg = null;
            if (!sub.IsEmptyElement)
            {
                var depth = sub.Depth;
                while (sub.Read() && sub.Depth > depth)
                {
                    if (sub.NodeType != XmlNodeType.Element)
                        continue;
                    if (sub.LocalName == "fgColor")
                        fg = ReadColor(sub, part);
                    else if (sub.LocalName == "bgColor")
                        bg = ReadColor(sub, part);
                }
            }
            return new XlsxFill(pattern, fg, bg);
        }
        return XlsxFill.None;
    }

    private static XlsxBorder ReadBorder(XmlReader xml, string part)
    {
        var up = XlsxReader.Bool(xml.GetAttribute("diagonalUp"));
        var down = XlsxReader.Bool(xml.GetAttribute("diagonalDown"));
        XlsxBorderEdge left = XlsxBorderEdge.None, right = XlsxBorderEdge.None, top = XlsxBorderEdge.None,
            bottom = XlsxBorderEdge.None, diagonal = XlsxBorderEdge.None;
        using var sub = xml.ReadSubtree();
        sub.Read();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element || sub.Depth != 1)
                continue;
            var name = sub.LocalName;
            var style = BorderStyle(sub.GetAttribute("style"), part);
            XlsxColor? color = null;
            if (!sub.IsEmptyElement)
            {
                var depth = sub.Depth;
                while (sub.Read() && sub.Depth > depth)
                {
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "color")
                        color = ReadColor(sub, part);
                }
            }
            var edge = style == XlsxBorderStyle.None && color is null ? XlsxBorderEdge.None : new XlsxBorderEdge(style, color);
            switch (name)
            {
                case "left" or "start":
                    left = edge;
                    break;
                case "right" or "end":
                    right = edge;
                    break;
                case "top":
                    top = edge;
                    break;
                case "bottom":
                    bottom = edge;
                    break;
                case "diagonal":
                    diagonal = edge;
                    break;
            }
        }
        return new XlsxBorder(left, right, top, bottom, diagonal, up, down);
    }

    internal static readonly string[] BorderStyleNames =
    [
        "none", "thin", "medium", "dashed", "dotted", "thick", "double", "hair",
        "mediumDashed", "dashDot", "mediumDashDot", "dashDotDot", "mediumDashDotDot", "slantDashDot",
    ];

    private static XlsxBorderStyle BorderStyle(string? name, string part)
    {
        if (name is null)
            return XlsxBorderStyle.None;
        var at = Array.IndexOf(BorderStyleNames, name);
        return at >= 0 ? (XlsxBorderStyle)at : throw new XlsxException(XlsxRefusal.Malformed, $"'{name}' is not a border style.", part);
    }

    private static XlsxCellFormat ReadCellFormat(XmlReader xml, string part)
    {
        int Id(string attribute) => xml.GetAttribute(attribute) is { } v ? XlsxReader.Int(v, part) : 0;
        var numFmt = Id("numFmtId");
        var font = Id("fontId");
        var fill = Id("fillId");
        var border = Id("borderId");
        XlsxAlignment? alignment = null;
        if (!xml.IsEmptyElement)
        {
            using var sub = xml.ReadSubtree();
            sub.Read();
            while (sub.Read())
            {
                if (sub.NodeType == XmlNodeType.Element && sub.Depth == 1 && sub.LocalName == "alignment")
                    alignment = ReadAlignment(sub, part);
            }
        }
        return new XlsxCellFormat(numFmt, font, fill, border, alignment);
    }

    internal static readonly string[] HorizontalNames = ["general", "left", "center", "right", "fill", "justify", "centerContinuous", "distributed"];

    internal static readonly string[] VerticalNames = ["bottom", "top", "center", "justify", "distributed"];

    private static XlsxAlignment ReadAlignment(XmlReader xml, string part)
    {
        var h = xml.GetAttribute("horizontal") is { } hv ? Array.IndexOf(HorizontalNames, hv) : 0;
        var v = xml.GetAttribute("vertical") is { } vv ? Array.IndexOf(VerticalNames, vv) : 0;
        if (h < 0 || v < 0)
            throw new XlsxException(XlsxRefusal.Malformed, "A cell format names an alignment that is not one.", part);
        return new XlsxAlignment(
            (XlsxHorizontalAlignment)h,
            (XlsxVerticalAlignment)v,
            XlsxReader.Bool(xml.GetAttribute("wrapText")),
            xml.GetAttribute("indent") is { } indent ? XlsxReader.Int(indent, part) : 0,
            xml.GetAttribute("textRotation") is { } rotation ? XlsxReader.Int(rotation, part) : 0,
            XlsxReader.Bool(xml.GetAttribute("shrinkToFit")));
    }

    private static List<uint>? ReadIndexedColors(XmlReader xml, string part)
    {
        List<uint>? colors = null;
        using var sub = xml.ReadSubtree();
        sub.Read();
        while (sub.Read())
        {
            if (sub.NodeType != XmlNodeType.Element)
                continue;
            if (sub.LocalName == "indexedColors")
                colors = [];
            else if (sub.LocalName == "rgbColor" && colors is not null && sub.GetAttribute("rgb") is { } rgb)
                colors.Add(Argb(rgb, part));
        }
        return colors;
    }

    /// <summary>The colour the element the reader is on writes (<c>CT_Color</c>).</summary>
    internal static XlsxColor ReadColor(XmlReader xml, string part)
    {
        var tint = xml.GetAttribute("tint") is { } t ? XlsxReader.Double(t, part) : 0;
        if (XlsxReader.Bool(xml.GetAttribute("auto")))
            return new XlsxColor(XlsxColorKind.Automatic, 0, tint);
        if (xml.GetAttribute("rgb") is { } rgb)
            return new XlsxColor(XlsxColorKind.Rgb, Argb(rgb, part), tint);
        if (xml.GetAttribute("theme") is { } theme)
            return new XlsxColor(XlsxColorKind.Theme, (uint)XlsxReader.Int(theme, part), tint);
        if (xml.GetAttribute("indexed") is { } indexed)
            return new XlsxColor(XlsxColorKind.Indexed, (uint)XlsxReader.Int(indexed, part), tint);
        return new XlsxColor(XlsxColorKind.Automatic, 0, tint);
    }

    private static uint Argb(string text, string part)
    {
        if (!uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) || text.Length is not (6 or 8))
            throw new XlsxException(XlsxRefusal.Malformed, $"'{text}' is not an RGB colour.", part);
        return text.Length == 6 ? 0xFF000000 | value : value;
    }
}
