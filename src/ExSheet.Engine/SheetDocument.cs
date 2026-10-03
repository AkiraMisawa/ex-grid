using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExSheet.Engine;

/// <summary>
/// The serialisable form of a Sheet (CONTEXT.md, ADR-0048). It records the Sheet's culture, its
/// name (ADR-0046), the Linked Tables declared on it — each one's name, column names and key, never
/// its rows (ADR-0049) — its Entries — constants already parsed, Formulas in invariant syntax — the
/// Cell Formats recorded on its columns, rows and cells (ADR-0047, ADR-0071) and the widths set on
/// its columns (ADR-0046), and never a Value: opening one
/// computes every Value again. The Consumer persists it; ExSheet never does.
/// </summary>
/// <remarks>
/// The document is a format with a version. A reader meeting a version it does not know refuses
/// the document rather than guess at it (ADR-0048); so does a reader meeting anything it does not
/// understand. This engine writes version 9 and reads versions 1 to 9: version 1 recorded no
/// name and no Linked Table, and a Sheet opened from one is named <see cref="Sheet.DefaultName"/>
/// and declares none; versions 1 and 2 recorded formats on cells only, where General meant the
/// cell set nothing; versions 1 to 3 recorded no column width, and every column of a Sheet
/// opened from one is at the default width; version 4 recorded widths without saying whether the
/// user set them, and each is read as one the user set, which is what version 4 meant; version 5
/// said whether each was custom, and a custom one is read as the user's and any other as widened
/// by entry, which is what version 5 meant by them (ADR-0046, 2026-09-28); versions 2 to 6 recorded
/// no Linked Table's key, and every table of a Sheet opened from one is declared without one
/// (ADR-0049, 2026-09-30); versions 1 to 7 recorded no Font, Fill or Borders, and a Sheet opened
/// from one has none (ADR-0071, ADR-0048); versions 7 and 8 recorded a key of one column only, which
/// version 9 still writes as a name, and a key of several as a list (ADR-0058, amended 2026-10-03).
/// </remarks>
public sealed class SheetDocument
{
    /// <summary>The version this engine writes.</summary>
    public const int CurrentVersion = 9;

    /// <summary>The oldest version this engine reads.</summary>
    public const int OldestReadableVersion = 1;

    internal SheetDocument(string culture, string name, IReadOnlyList<SheetDocumentTable> linkedTables, IReadOnlyList<SheetDocumentCell> cells)
    {
        Culture = culture;
        Name = name;
        LinkedTables = linkedTables;
        Cells = cells;
    }

    /// <summary>The format version the document is written in: <see cref="CurrentVersion"/>, whatever version it was read from.</summary>
    public int Version => CurrentVersion;

    /// <summary>The name of the Sheet's declared culture, such as <c>en-US</c>; empty for the invariant culture.</summary>
    public string Culture { get; }

    /// <summary>The Sheet's name (ADR-0046); <see cref="Sheet.DefaultName"/> for a document read from version 1.</summary>
    public string Name { get; }

    /// <summary>
    /// The Linked Tables declared on the Sheet, in the order they were declared: names, column names
    /// and keys only (ADR-0049). A Sheet opened from the document declares them again, and their
    /// readers show <c>#GETTING_DATA</c> until the Consumer pushes a snapshot.
    /// </summary>
    public IReadOnlyList<SheetDocumentTable> LinkedTables { get; }

    /// <summary>The cells that hold something, in row-major order.</summary>
    public IReadOnlyList<SheetDocumentCell> Cells { get; }

    /// <summary>
    /// The Cell Formats recorded on whole columns, in column order, adjacent columns recorded alike
    /// as one run (ADR-0047, ADR-0071). Empty for a document read from version 1 or 2.
    /// </summary>
    public IReadOnlyList<SheetDocumentAxisFormat> Columns { get; init; } = [];

    /// <summary>
    /// The Cell Formats recorded on whole rows, in row order, adjacent rows recorded alike as one
    /// run (ADR-0047, ADR-0071). Empty for a document read from version 1 or 2.
    /// </summary>
    public IReadOnlyList<SheetDocumentAxisFormat> Rows { get; init; } = [];

    /// <summary>
    /// The widths recorded on columns, in characters, and the kind of each, in column order,
    /// adjacent columns of one width and one kind as one run (ADR-0046). A column at the default
    /// width is not recorded. Empty for a document read from versions 1 to 3; every width of a
    /// document read from version 4 is the user's.
    /// </summary>
    public IReadOnlyList<SheetDocumentColumnWidth> ColumnWidths { get; init; } = [];

    /// <summary>Writes the document as JSON.</summary>
    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("version", CurrentVersion);
            json.WriteString("culture", Culture);
            json.WriteString("name", Name);
            if (LinkedTables.Count > 0)
            {
                json.WriteStartArray("linkedTables");
                foreach (var table in LinkedTables)
                {
                    json.WriteStartObject();
                    json.WriteString("name", table.Name);
                    json.WriteStartArray("columns");
                    foreach (var column in table.Columns) json.WriteStringValue(column);
                    json.WriteEndArray();
                    // One key column is written as its name, as version 7 wrote it; several as a list (version 9).
                    if (table.KeyColumns.Count == 1)
                    {
                        json.WriteString("key", table.KeyColumns[0]);
                    }
                    else if (table.KeyColumns.Count > 1)
                    {
                        json.WriteStartArray("key");
                        foreach (var column in table.KeyColumns) json.WriteStringValue(column);
                        json.WriteEndArray();
                    }
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }
            WriteAxis(json, "columns", Columns, run => CellAddress.ColumnName(run.First) + ":" + CellAddress.ColumnName(run.Last));
            WriteAxis(json, "rows", Rows, run => (run.First + 1).ToString(CultureInfo.InvariantCulture) + ":" + (run.Last + 1).ToString(CultureInfo.InvariantCulture));
            if (ColumnWidths.Count > 0)
            {
                json.WriteStartArray("columnWidths");
                foreach (var run in ColumnWidths)
                {
                    json.WriteStartObject();
                    json.WriteString("at", CellAddress.ColumnName(run.First) + ":" + CellAddress.ColumnName(run.Last));
                    json.WriteNumber("width", run.Width);
                    json.WriteString("kind", KindName(run.Kind));
                    json.WriteEndObject();
                }
                json.WriteEndArray();
            }
            json.WriteStartArray("cells");
            foreach (var cell in Cells)
            {
                json.WriteStartObject();
                json.WriteString("at", cell.Address.ToString());
                if (cell.Entry is { } entry)
                {
                    if (entry.Formula is { } formula)
                    {
                        json.WriteString("formula", formula);
                    }
                    else
                    {
                        var constant = entry.Constant!.Value;
                        switch (constant.Kind)
                        {
                            case ValueKind.Number: json.WriteNumber("number", constant.Number); break;
                            case ValueKind.Text: json.WriteString("text", constant.Text); break;
                            case ValueKind.Boolean: json.WriteBoolean("boolean", constant.Boolean); break;
                            default: json.WriteString("error", constant.Error.ToText()); break;
                        }
                    }
                }
                WriteCellFormat(json, cell.NumberFormat, cell.Alignment, cell.Font, cell.Fill, cell.Borders);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteAxis(Utf8JsonWriter json, string name, IReadOnlyList<SheetDocumentAxisFormat> runs, Func<SheetDocumentAxisFormat, string> at)
    {
        if (runs.Count == 0) return;
        json.WriteStartArray(name);
        foreach (var run in runs)
        {
            json.WriteStartObject();
            json.WriteString("at", at(run));
            WriteCellFormat(json, run.NumberFormat, run.Alignment, run.Font, run.Fill, run.Borders);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    /// <summary>
    /// The parts of a Cell Format a cell, row or column records, each only where it records one:
    /// <c>"format"</c>, <c>"align"</c>, <c>"font"</c> (an object holding only what differs from the
    /// default Font: <c>"color"</c>, <c>"bold"</c>, <c>"italic"</c>, <c>"underline"</c>,
    /// <c>"strikethrough"</c>), <c>"fill"</c> (<c>"none"</c> or <c>"#RRGGBB"</c>) and
    /// <c>"borders"</c> (an object holding each side that has a line, as <c>"style"</c> in
    /// Excel's file's name for it and <c>"color"</c>). An Automatic colour is not written.
    /// </summary>
    private static void WriteCellFormat(Utf8JsonWriter json, NumberFormat? format, HorizontalAlignment? alignment, CellFont? font, CellFill? fill, CellBorders? borders)
    {
        if (format is not null) json.WriteString("format", format.Code);
        if (alignment is { } a) json.WriteString("align", AlignmentName(a));
        if (font is { } f)
        {
            json.WriteStartObject("font");
            if (!f.Colour.IsAutomatic) json.WriteString("color", ColourText(f.Colour));
            if (f.Bold) json.WriteBoolean("bold", true);
            if (f.Italic) json.WriteBoolean("italic", true);
            if (f.Underline) json.WriteBoolean("underline", true);
            if (f.Strikethrough) json.WriteBoolean("strikethrough", true);
            json.WriteEndObject();
        }
        if (fill is { } solid) json.WriteString("fill", solid.Colour is { } colour ? ColourText(colour) : "none");
        if (borders is { } b)
        {
            json.WriteStartObject("borders");
            WriteSide(json, "top", b.Top);
            WriteSide(json, "bottom", b.Bottom);
            WriteSide(json, "left", b.Left);
            WriteSide(json, "right", b.Right);
            json.WriteEndObject();
        }
    }

    private static void WriteSide(Utf8JsonWriter json, string side, BorderLine line)
    {
        if (line.IsNone) return;
        json.WriteStartObject(side);
        json.WriteString("style", LineStyleName(line.Style));
        if (!line.Colour.IsAutomatic) json.WriteString("color", ColourText(line.Colour));
        json.WriteEndObject();
    }

    private static string ColourText(CellColour colour) => "#" + colour.Rgb.ToString("X6", CultureInfo.InvariantCulture);

    /// <summary>Reads a document written by <see cref="ToJson"/>.</summary>
    /// <exception cref="SheetDocumentException">
    /// The version is not one this engine reads (<see cref="OldestReadableVersion"/> to
    /// <see cref="CurrentVersion"/>), or the document holds anything its version does not define.
    /// Nothing is guessed.
    /// </exception>
    public static SheetDocument FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new SheetDocumentException("The Sheet Document is not JSON.", e);
        }
        using (parsed)
        {
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A Sheet Document is a JSON object.");

            // The version is read before anything else: a document of another version is refused
            // whatever else it holds (ADR-0048).
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
            {
                throw new SheetDocumentException("The Sheet Document has no version.");
            }
            if (number is < OldestReadableVersion or > CurrentVersion)
            {
                throw new SheetDocumentException($"The Sheet Document is version {number}; this engine reads versions {OldestReadableVersion} to {CurrentVersion}.") { DocumentVersion = number };
            }

            string? culture = null;
            string? name = null;
            var tables = new List<SheetDocumentTable>();
            var cells = new List<SheetDocumentCell>();
            var seen = new HashSet<CellAddress>();
            var columns = new List<SheetDocumentAxisFormat>();
            var rows = new List<SheetDocumentAxisFormat>();
            var widths = new List<SheetDocumentColumnWidth>();
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "version":
                        break;
                    case "culture":
                        culture = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : throw new SheetDocumentException("The culture is not a string.");
                        break;
                    case "name" when number >= 2:
                        name = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : throw new SheetDocumentException("The name is not a string.");
                        if (!Sheet.IsValidName(name, out var why)) throw new SheetDocumentException($"'{name}' is not a Sheet's name: {why}");
                        break;
                    case "linkedTables" when number >= 2:
                        if (property.Value.ValueKind != JsonValueKind.Array) throw new SheetDocumentException("The Linked Tables are not an array.");
                        foreach (var element in property.Value.EnumerateArray())
                        {
                            var table = ReadTable(element, number);
                            if (tables.Any(t => string.Equals(t.Name, table.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                throw new SheetDocumentException($"The Linked Table '{table.Name}' is declared twice.");
                            }
                            tables.Add(table);
                        }
                        break;
                    case "columns" when number >= 3:
                        // The whole Sheet is recorded as whole columns (A:XFD), so a column run may span every row and every column.
                        columns = ReadAxis(property.Value, "column", number, text => CellRange.TryParse(text, out var r) && r.IsWholeColumns ? (r.First.Column, r.Last.Column) : null);
                        break;
                    case "rows" when number >= 3:
                        rows = ReadAxis(property.Value, "row", number, text => CellRange.TryParse(text, out var r) && r.IsWholeRows && !r.IsWholeColumns ? (r.First.Row, r.Last.Row) : null);
                        break;
                    case "columnWidths" when number >= 4:
                        widths = ReadWidths(property.Value, number);
                        break;
                    case "cells":
                        if (property.Value.ValueKind != JsonValueKind.Array) throw new SheetDocumentException("The cells are not an array.");
                        foreach (var element in property.Value.EnumerateArray())
                        {
                            var cell = ReadCell(element, number);
                            if (!seen.Add(cell.Address)) throw new SheetDocumentException($"The cell {cell.Address} appears twice.");
                            cells.Add(cell);
                        }
                        break;
                    default:
                        throw new SheetDocumentException($"'{property.Name}' is not part of a version {number} Sheet Document.");
                }
            }
            if (culture is null) throw new SheetDocumentException("The Sheet Document records no culture.");
            if (number >= 2 && name is null) throw new SheetDocumentException("The Sheet Document records no name.");
            ResolveCulture(culture);
            cells.Sort((a, b) => a.Address.CompareTo(b.Address));
            return new SheetDocument(culture, name ?? Sheet.DefaultName, tables, cells) { Columns = columns, Rows = rows, ColumnWidths = widths };
        }
    }

    internal static CultureInfo ResolveCulture(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name, predefinedOnly: true);
        }
        catch (CultureNotFoundException e)
        {
            throw new SheetDocumentException($"The culture '{name}' is not known here.", e);
        }
    }

    private static List<SheetDocumentAxisFormat> ReadAxis(JsonElement array, string what, int version, Func<string?, (int First, int Last)?> parse)
    {
        if (array.ValueKind != JsonValueKind.Array) throw new SheetDocumentException($"The {what} formats are not an array.");
        var runs = new List<SheetDocumentAxisFormat>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException($"A {what} format is not a JSON object.");
            (int First, int Last)? at = null;
            NumberFormat? format = null;
            HorizontalAlignment? alignment = null;
            CellFont? font = null;
            CellFill? fill = null;
            CellBorders? borders = null;
            foreach (var property in element.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "at":
                        at = property.Value.ValueKind == JsonValueKind.String ? parse(property.Value.GetString()) : null;
                        if (at is null) throw new SheetDocumentException($"'{property.Value}' is not a {what} or a run of them.");
                        break;
                    case "format":
                        format = ReadFormat(property.Value);
                        break;
                    case "align":
                        alignment = ReadAlignment(property.Value, allowGeneral: true);
                        break;
                    case "font" when version >= 8:
                        font = ReadFont(property.Value);
                        break;
                    case "fill" when version >= 8:
                        fill = ReadFill(property.Value);
                        break;
                    case "borders" when version >= 8:
                        borders = ReadBorders(property.Value);
                        break;
                    default:
                        throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} {what} format.");
                }
            }
            if (at is not { } span) throw new SheetDocumentException($"A {what} format says no {what}.");
            if (format is null && alignment is null && font is null && fill is null && borders is null) throw new SheetDocumentException($"The {what} format at {span.First} sets nothing.");
            if (runs.Any(r => r.First <= span.Last && span.First <= r.Last)) throw new SheetDocumentException($"A {what} is formatted twice.");
            runs.Add(new SheetDocumentAxisFormat(span.First, span.Last, format, alignment, font, fill, borders));
        }
        runs.Sort((a, b) => a.First.CompareTo(b.First));
        return runs;
    }

    private static List<SheetDocumentColumnWidth> ReadWidths(JsonElement array, int version)
    {
        if (array.ValueKind != JsonValueKind.Array) throw new SheetDocumentException("The column widths are not an array.");
        var runs = new List<SheetDocumentColumnWidth>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A column width is not a JSON object.");
            (int First, int Last)? at = null;
            double? width = null;
            // Version 4 recorded only widths the user set (ADR-0046).
            SheetColumnWidthKind? kind = version < 5 ? SheetColumnWidthKind.SetByUser : null;
            foreach (var property in element.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "at":
                        at = property.Value.ValueKind == JsonValueKind.String && CellRange.TryParse(property.Value.GetString(), out var r) && r.IsWholeColumns
                            ? (r.First.Column, r.Last.Column)
                            : throw new SheetDocumentException($"'{property.Value}' is not a column or a run of them.");
                        break;
                    case "width":
                        width = property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var w) && w is > 0 and <= Sheet.MaxColumnWidth
                            ? w
                            : throw new SheetDocumentException($"'{property.Value}' is not a column's width: more than 0 and at most {Sheet.MaxColumnWidth} characters.");
                        break;
                    case "custom" when version == 5:
                        // Version 5's custom width stopped widening; any other was widened again (ADR-0046, 2026-09-28).
                        kind = property.Value.ValueKind switch
                        {
                            JsonValueKind.True => SheetColumnWidthKind.SetByUser,
                            JsonValueKind.False => SheetColumnWidthKind.WidenedByEntry,
                            _ => throw new SheetDocumentException($"'{property.Value}' does not say whether a column's width is custom."),
                        };
                        break;
                    case "kind" when version >= 6:
                        kind = property.Value.ValueKind == JsonValueKind.String && KindOf(property.Value.GetString()!) is { } named
                            ? named
                            : throw new SheetDocumentException($"'{property.Value}' is not a kind of column width: \"{KindName(SheetColumnWidthKind.WidenedByEntry)}\" or \"{KindName(SheetColumnWidthKind.SetByUser)}\".");
                        break;
                    default:
                        throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} column width.");
                }
            }
            if (at is not { } span) throw new SheetDocumentException("A column width says no column.");
            if (width is not { } set) throw new SheetDocumentException($"The column width at {CellAddress.ColumnName(span.First)} gives no width.");
            if (kind is not { } recorded) throw new SheetDocumentException($"The column width at {CellAddress.ColumnName(span.First)} does not say its kind.");
            if (runs.Any(r => r.First <= span.Last && span.First <= r.Last)) throw new SheetDocumentException("A column is given a width twice.");
            runs.Add(new SheetDocumentColumnWidth(span.First, span.Last, set, recorded));
        }
        runs.Sort((a, b) => a.First.CompareTo(b.First));
        return runs;
    }

    /// <summary>How a Sheet Document writes a width's kind.</summary>
    private static string KindName(SheetColumnWidthKind kind) => kind == SheetColumnWidthKind.SetByUser ? "setByUser" : "widenedByEntry";

    private static SheetColumnWidthKind? KindOf(string name) => name switch
    {
        "setByUser" => SheetColumnWidthKind.SetByUser,
        "widenedByEntry" => SheetColumnWidthKind.WidenedByEntry,
        _ => null,
    };

    private static NumberFormat ReadFormat(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && NumberFormat.TryParse(value.GetString()!, out var parsed, out _)
            ? parsed
            : throw new SheetDocumentException($"'{value}' is not a number format this version reads.");

    /// <summary>
    /// A colour as version 8 records it: <c>"automatic"</c> or <c>"#RRGGBB"</c>. Any other kind —
    /// an <c>.xlsx</c> theme colour, a colour's name — is refused by name, never guessed at
    /// (ADR-0048, 2026-09-30).
    /// </summary>
    private static CellColour ReadColour(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (text == "automatic") return CellColour.Automatic;
            if (CellColour.FromHex(text) is { } rgb) return rgb;
        }
        throw new SheetDocumentException($"'{value}' is not a colour a Sheet Document records: a colour is \"automatic\" or \"#RRGGBB\", and no other kind is read.");
    }

    private static CellFont ReadFont(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new SheetDocumentException($"'{value}' is not a Font.");
        var font = CellFont.Default;
        foreach (var property in value.EnumerateObject())
        {
            font = property.Name switch
            {
                "color" => font with { Colour = ReadColour(property.Value) },
                "bold" => font with { Bold = ReadEmphasis(property) },
                "italic" => font with { Italic = ReadEmphasis(property) },
                "underline" => font with { Underline = ReadEmphasis(property) },
                "strikethrough" => font with { Strikethrough = ReadEmphasis(property) },
                _ => throw new SheetDocumentException($"'{property.Name}' is not part of a Font: its colour, bold, italic, a single underline and strikethrough."),
            };
        }
        return font;

        static bool ReadEmphasis(JsonProperty property) =>
            property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.Value.GetBoolean()
                : throw new SheetDocumentException($"'{property.Value}' does not say whether a Font is {property.Name}.");
    }

    private static CellFill ReadFill(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (text == "none") return CellFill.None;
            if (CellColour.FromHex(text) is { } rgb) return CellFill.Solid(rgb);
        }
        throw new SheetDocumentException($"'{value}' is not a Fill a Sheet Document records: a Fill is \"none\" or \"#RRGGBB\", and no other kind of colour is read.");
    }

    private static CellBorders ReadBorders(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new SheetDocumentException($"'{value}' is not a cell's Borders.");
        var borders = CellBorders.None;
        foreach (var property in value.EnumerateObject())
        {
            borders = property.Name switch
            {
                "top" => borders with { Top = ReadSide(property) },
                "bottom" => borders with { Bottom = ReadSide(property) },
                "left" => borders with { Left = ReadSide(property) },
                "right" => borders with { Right = ReadSide(property) },
                _ => throw new SheetDocumentException($"'{property.Name}' is not a side of a cell: top, bottom, left or right."),
            };
        }
        return borders;
    }

    private static BorderLine ReadSide(JsonProperty side)
    {
        if (side.Value.ValueKind != JsonValueKind.Object) throw new SheetDocumentException($"'{side.Value}' is not the line on a cell's {side.Name} side.");
        BorderLineStyle? style = null;
        var colour = CellColour.Automatic;
        foreach (var property in side.Value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "style":
                    style = property.Value.ValueKind == JsonValueKind.String && LineStyleOf(property.Value.GetString()!) is { } named
                        ? named
                        : throw new SheetDocumentException($"'{property.Value}' is not one of Excel's thirteen line styles.");
                    break;
                case "color":
                    colour = ReadColour(property.Value);
                    break;
                default:
                    throw new SheetDocumentException($"'{property.Name}' is not part of a Border: its line style and its colour.");
            }
        }
        if (style is not { } recorded) throw new SheetDocumentException($"The line on a cell's {side.Name} side says no line style.");
        return new BorderLine(recorded, colour);
    }

    /// <summary>The names Excel's own file gives the thirteen line styles (<c>ST_BorderStyle</c>).</summary>
    private static readonly (BorderLineStyle Style, string Name)[] LineStyleNames =
    [
        (BorderLineStyle.Hair, "hair"),
        (BorderLineStyle.Thin, "thin"),
        (BorderLineStyle.Medium, "medium"),
        (BorderLineStyle.Thick, "thick"),
        (BorderLineStyle.Double, "double"),
        (BorderLineStyle.Dotted, "dotted"),
        (BorderLineStyle.Dashed, "dashed"),
        (BorderLineStyle.DashDot, "dashDot"),
        (BorderLineStyle.DashDotDot, "dashDotDot"),
        (BorderLineStyle.MediumDashed, "mediumDashed"),
        (BorderLineStyle.MediumDashDot, "mediumDashDot"),
        (BorderLineStyle.MediumDashDotDot, "mediumDashDotDot"),
        (BorderLineStyle.SlantedDashDot, "slantDashDot"),
    ];

    private static string LineStyleName(BorderLineStyle style) => LineStyleNames.First(n => n.Style == style).Name;

    private static BorderLineStyle? LineStyleOf(string name) =>
        LineStyleNames.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.Ordinal)) is { Name: not null } found ? found.Style : null;

    private static HorizontalAlignment ReadAlignment(JsonElement value, bool allowGeneral) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() switch
        {
            "general" when allowGeneral => HorizontalAlignment.General,
            "left" => HorizontalAlignment.Left,
            "center" => HorizontalAlignment.Center,
            "right" => HorizontalAlignment.Right,
            _ => throw new SheetDocumentException($"'{value}' is not an alignment."),
        } : throw new SheetDocumentException($"'{value}' is not an alignment.");

    private static SheetDocumentTable ReadTable(JsonElement element, int version)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A Linked Table is not a JSON object.");
        string? name = null;
        List<string>? columns = null;
        List<string> key = [];
        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value;
            switch (property.Name)
            {
                case "name":
                    name = value.ValueKind == JsonValueKind.String ? value.GetString() : throw new SheetDocumentException($"'{value}' is not a Linked Table's name.");
                    break;
                case "columns":
                    if (value.ValueKind != JsonValueKind.Array) throw new SheetDocumentException("A Linked Table's columns are not an array.");
                    columns = [.. value.EnumerateArray().Select(c => c.ValueKind == JsonValueKind.String ? c.GetString()! : throw new SheetDocumentException($"'{c}' is not a column name."))];
                    break;
                case "key" when version >= 7 && value.ValueKind == JsonValueKind.String:
                    key = [value.GetString()!];
                    break;
                case "key" when version >= 9 && value.ValueKind == JsonValueKind.Array:
                    key = [.. value.EnumerateArray().Select(c => c.ValueKind == JsonValueKind.String ? c.GetString()! : throw new SheetDocumentException($"'{c}' is not a Linked Table's key column."))];
                    if (key.Count < 2) throw new SheetDocumentException($"A key of several columns lists at least two; '{value}' does not.");
                    break;
                case "key" when version >= 7:
                    throw new SheetDocumentException($"'{value}' is not a Linked Table's key column.");
                default:
                    throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} Linked Table's declaration.");
            }
        }
        if (name is null) throw new SheetDocumentException("A Linked Table has no name.");
        if (columns is null) throw new SheetDocumentException($"The Linked Table '{name}' has no columns.");
        if (Sheet.WhyNotALinkedTable(name, columns) is { } why) throw new SheetDocumentException(why);
        if (Sheet.WhyNotAKey(name, columns, key) is { } whyNotKey) throw new SheetDocumentException(whyNotKey);
        return new SheetDocumentTable(name, columns, key.Count == 1 ? key[0] : null) { KeyColumns = key };
    }

    private static SheetDocumentCell ReadCell(JsonElement element, int version)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A cell is not a JSON object.");
        CellAddress? address = null;
        Entry? entry = null;
        NumberFormat? format = null;
        HorizontalAlignment? alignment = null;
        CellFont? font = null;
        CellFill? fill = null;
        CellBorders? borders = null;
        foreach (var property in element.EnumerateObject())
        {
            var value = property.Value;
            switch (property.Name)
            {
                case "at":
                    if (value.ValueKind != JsonValueKind.String || !CellAddress.TryParse(value.GetString(), out var at))
                    {
                        throw new SheetDocumentException($"'{value}' is not a cell address.");
                    }
                    address = at;
                    break;
                case "number":
                    entry = Single(entry, value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) && double.IsFinite(n)
                        ? Entry.FromValue(Value.FromNumber(n))
                        : throw new SheetDocumentException($"'{value}' is not a number."));
                    break;
                case "text":
                    entry = Single(entry, value.ValueKind == JsonValueKind.String
                        ? Entry.FromValue(Value.FromText(value.GetString()!))
                        : throw new SheetDocumentException($"'{value}' is not text."));
                    break;
                case "boolean":
                    entry = Single(entry, value.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? Entry.FromValue(Value.FromBoolean(value.GetBoolean()))
                        : throw new SheetDocumentException($"'{value}' is not a boolean."));
                    break;
                case "error":
                    entry = Single(entry, value.ValueKind == JsonValueKind.String && ErrorValues.TryParseTyped(value.GetString(), out var error)
                        ? Entry.FromValue(Value.FromError(error))
                        : throw new SheetDocumentException($"'{value}' is not an Error Value a cell can hold."));
                    break;
                case "formula":
                    if (value.ValueKind != JsonValueKind.String) throw new SheetDocumentException($"'{value}' is not a Formula.");
                    try
                    {
                        entry = Single(entry, Entry.FromFormula(value.GetString()!));
                    }
                    catch (FormulaSyntaxException e)
                    {
                        throw new SheetDocumentException(e.Message, e);
                    }
                    break;
                case "format":
                    format = ReadFormat(value);
                    // Before version 3 a cell's General was no format of its own: no level could show through it.
                    if (version < 3 && format.IsGeneral) format = null;
                    break;
                case "align":
                    alignment = ReadAlignment(value, allowGeneral: version >= 3);
                    break;
                case "font" when version >= 8:
                    font = ReadFont(value);
                    break;
                case "fill" when version >= 8:
                    fill = ReadFill(value);
                    break;
                case "borders" when version >= 8:
                    borders = ReadBorders(value);
                    break;
                default:
                    throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} cell.");
            }
        }
        if (address is null) throw new SheetDocumentException("A cell has no address.");
        if (entry is null && format is null && alignment is null && font is null && fill is null && borders is null) throw new SheetDocumentException($"The cell {address} holds nothing.");
        return new SheetDocumentCell(address.Value, entry, format, alignment, font, fill, borders);
    }

    private static string AlignmentName(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.General => "general",
        HorizontalAlignment.Left => "left",
        HorizontalAlignment.Center => "center",
        _ => "right",
    };

    private static Entry Single(Entry? existing, Entry entry) =>
        existing is null ? entry : throw new SheetDocumentException("A cell holds more than one Entry.");
}

/// <summary>One cell of a Sheet Document: where it is, its Entry and its own Cell Format. Never its Value (ADR-0048).</summary>
/// <param name="Address">Where the cell is.</param>
/// <param name="Entry">What the user put into it; <see langword="null"/> for a cell that holds only a Cell Format.</param>
/// <param name="NumberFormat">The Number Format the cell records itself; <see langword="null"/> when it takes its row's or column's (ADR-0047).</param>
/// <param name="Alignment">The horizontal alignment the cell records itself; <see langword="null"/> when it takes its row's or column's (ADR-0047).</param>
/// <param name="Font">The Font the cell records itself; <see langword="null"/> when it takes its row's or column's (ADR-0071).</param>
/// <param name="Fill">The Fill the cell records itself; <see langword="null"/> when it takes its row's or column's (ADR-0071).</param>
/// <param name="Borders">The four sides the cell records itself; <see langword="null"/> when it takes its row's or column's (ADR-0071).</param>
public sealed record SheetDocumentCell(CellAddress Address, Entry? Entry, NumberFormat? NumberFormat, HorizontalAlignment? Alignment, CellFont? Font, CellFill? Fill, CellBorders? Borders);

/// <summary>
/// A Cell Format recorded on whole columns or whole rows, as a Sheet Document records it: one
/// entry for a run of adjacent columns (rows) recorded alike, never one per cell (ADR-0047,
/// ADR-0071).
/// </summary>
/// <param name="First">The first column (row) of the run, from 0.</param>
/// <param name="Last">The last column (row) of the run.</param>
/// <param name="NumberFormat">The Number Format recorded on them, or <see langword="null"/>.</param>
/// <param name="Alignment">The horizontal alignment recorded on them, or <see langword="null"/>.</param>
/// <param name="Font">The Font recorded on them, or <see langword="null"/>.</param>
/// <param name="Fill">The Fill recorded on them, or <see langword="null"/>.</param>
/// <param name="Borders">The four sides recorded on them, or <see langword="null"/>.</param>
public sealed record SheetDocumentAxisFormat(int First, int Last, NumberFormat? NumberFormat, HorizontalAlignment? Alignment, CellFont? Font, CellFill? Fill, CellBorders? Borders)
{
    /// <summary>The level each row (column) of the run records.</summary>
    internal AxisFormat Level => new(NumberFormat, Alignment, Font, Fill, Borders);
}

/// <summary>
/// A width recorded on columns, as a Sheet Document records it: one entry for a run of adjacent
/// columns of one width and one kind (ADR-0046). A column at the default width has none.
/// </summary>
/// <param name="First">The first column of the run, from 0.</param>
/// <param name="Last">The last column of the run.</param>
/// <param name="Width">The width, in characters of the default font (Excel's unit, ADR-0047).</param>
/// <param name="Kind">What recorded it (<see cref="SheetColumnWidth.Kind"/>): the user, or an entry that widened the columns, which a longer entry widens again (ADR-0046, 2026-09-28).</param>
public sealed record SheetDocumentColumnWidth(int First, int Last, double Width, SheetColumnWidthKind Kind = SheetColumnWidthKind.SetByUser)
{
    /// <summary>Whether Excel's file marks the width custom (<see cref="SheetColumnWidth.IsCustom"/>): every recorded width is.</summary>
    public bool IsCustom => true;
}

/// <summary>A Linked Table's declaration as a Sheet Document records it: its name, its column names and its key, never its rows (ADR-0049).</summary>
/// <param name="Name">The name Formulas read it by.</param>
/// <param name="Columns">The column names, in order.</param>
/// <param name="Key">
/// The key column, one of <paramref name="Columns"/>, when the key is one column; <see langword="null"/>
/// for a table declared without one, for every table of a document read from versions 2 to 6
/// (ADR-0049, 2026-09-30), and for a key of several columns, which <see cref="KeyColumns"/> names.
/// </param>
public sealed record SheetDocumentTable(string Name, IReadOnlyList<string> Columns, string? Key = null)
{
    /// <summary>The key's columns: one, several for a key of several columns (version 9; ADR-0058, amended 2026-10-03), or none.</summary>
    public IReadOnlyList<string> KeyColumns { get; init; } = Key is null ? [] : [Key];
}

/// <summary>A Sheet Document that cannot be read: an unknown version, or anything the version does not define (ADR-0048).</summary>
public sealed class SheetDocumentException : FormatException
{
    /// <summary>Creates the refusal.</summary>
    public SheetDocumentException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the refusal, with what caused it.</summary>
    public SheetDocumentException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>The version the document declared, when that is why it was refused.</summary>
    public int? DocumentVersion { get; init; }
}
