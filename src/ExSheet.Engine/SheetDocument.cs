using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExSheet.Engine;

/// <summary>
/// The serialisable form of a Sheet (CONTEXT.md, ADR-0048). It records the Sheet's culture, its
/// name (ADR-0046), the Linked Tables declared on it — each one's name and column names, never its
/// rows (ADR-0049) — its Entries — constants already parsed, Formulas in invariant syntax — the
/// formats set on its columns, rows and cells (ADR-0047) and the widths set on its columns
/// (ADR-0046), and never a Value: opening one
/// computes every Value again. The Consumer persists it; ExSheet never does.
/// </summary>
/// <remarks>
/// The document is a format with a version. A reader meeting a version it does not know refuses
/// the document rather than guess at it (ADR-0048); so does a reader meeting anything it does not
/// understand. This engine writes version 5 and reads versions 1 to 5: version 1 recorded no
/// name and no Linked Table, and a Sheet opened from one is named <see cref="Sheet.DefaultName"/>
/// and declares none; versions 1 and 2 recorded formats on cells only, where General meant the
/// cell set nothing; versions 1 to 3 recorded no column width, and every column of a Sheet
/// opened from one is at the default width; version 4 recorded widths without saying whether the
/// user set them, and each is read as one the user set (custom), which is what version 4 meant.
/// </remarks>
public sealed class SheetDocument
{
    /// <summary>The version this engine writes.</summary>
    public const int CurrentVersion = 5;

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
    /// The Linked Tables declared on the Sheet, in the order they were declared: names and column
    /// names only (ADR-0049). A Sheet opened from the document declares them again, and their
    /// readers show <c>#GETTING_DATA</c> until the Consumer pushes a snapshot.
    /// </summary>
    public IReadOnlyList<SheetDocumentTable> LinkedTables { get; }

    /// <summary>The cells that hold something, in row-major order.</summary>
    public IReadOnlyList<SheetDocumentCell> Cells { get; }

    /// <summary>
    /// The formats set on whole columns, in column order, adjacent columns set alike as one run
    /// (ADR-0047). Empty for a document read from version 1 or 2.
    /// </summary>
    public IReadOnlyList<SheetDocumentAxisStyle> Columns { get; init; } = [];

    /// <summary>
    /// The formats set on whole rows, in row order, adjacent rows set alike as one run (ADR-0047).
    /// Empty for a document read from version 1 or 2.
    /// </summary>
    public IReadOnlyList<SheetDocumentAxisStyle> Rows { get; init; } = [];

    /// <summary>
    /// The widths recorded on columns, in characters, and whether the user set each, in column
    /// order, adjacent columns of one width and one origin as one run (ADR-0046). A column at the
    /// default width is not recorded. Empty for a document read from versions 1 to 3; every width
    /// of a document read from version 4 is custom.
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
                    json.WriteBoolean("custom", run.IsCustom);
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
                WriteStyle(json, cell.Format, cell.Alignment);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteAxis(Utf8JsonWriter json, string name, IReadOnlyList<SheetDocumentAxisStyle> runs, Func<SheetDocumentAxisStyle, string> at)
    {
        if (runs.Count == 0) return;
        json.WriteStartArray(name);
        foreach (var run in runs)
        {
            json.WriteStartObject();
            json.WriteString("at", at(run));
            WriteStyle(json, run.Format, run.Alignment);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteStyle(Utf8JsonWriter json, NumberFormat? format, HorizontalAlignment? alignment)
    {
        if (format is not null) json.WriteString("format", format.Code);
        if (alignment is { } a) json.WriteString("align", AlignmentName(a));
    }

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
            var columns = new List<SheetDocumentAxisStyle>();
            var rows = new List<SheetDocumentAxisStyle>();
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
                            var table = ReadTable(element);
                            if (tables.Any(t => string.Equals(t.Name, table.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                throw new SheetDocumentException($"The Linked Table '{table.Name}' is declared twice.");
                            }
                            tables.Add(table);
                        }
                        break;
                    case "columns" when number >= 3:
                        // The whole Sheet is recorded as whole columns (A:XFD), so a column run may span every row and every column.
                        columns = ReadAxis(property.Value, "column", text => CellRange.TryParse(text, out var r) && r.IsWholeColumns ? (r.First.Column, r.Last.Column) : null);
                        break;
                    case "rows" when number >= 3:
                        rows = ReadAxis(property.Value, "row", text => CellRange.TryParse(text, out var r) && r.IsWholeRows && !r.IsWholeColumns ? (r.First.Row, r.Last.Row) : null);
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

    private static List<SheetDocumentAxisStyle> ReadAxis(JsonElement array, string what, Func<string?, (int First, int Last)?> parse)
    {
        if (array.ValueKind != JsonValueKind.Array) throw new SheetDocumentException($"The {what} formats are not an array.");
        var runs = new List<SheetDocumentAxisStyle>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException($"A {what} format is not a JSON object.");
            (int First, int Last)? at = null;
            NumberFormat? format = null;
            HorizontalAlignment? alignment = null;
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
                    default:
                        throw new SheetDocumentException($"'{property.Name}' is not part of a {what} format.");
                }
            }
            if (at is not { } span) throw new SheetDocumentException($"A {what} format says no {what}.");
            if (format is null && alignment is null) throw new SheetDocumentException($"The {what} format at {span.First} sets nothing.");
            if (runs.Any(r => r.First <= span.Last && span.First <= r.Last)) throw new SheetDocumentException($"A {what} is formatted twice.");
            runs.Add(new SheetDocumentAxisStyle(span.First, span.Last, format, alignment));
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
            bool? custom = version < 5 ? true : null;
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
                    case "custom" when version >= 5:
                        custom = property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
                            ? property.Value.GetBoolean()
                            : throw new SheetDocumentException($"'{property.Value}' does not say whether a column's width is custom.");
                        break;
                    default:
                        throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} column width.");
                }
            }
            if (at is not { } span) throw new SheetDocumentException("A column width says no column.");
            if (width is not { } set) throw new SheetDocumentException($"The column width at {CellAddress.ColumnName(span.First)} gives no width.");
            if (custom is not { } isCustom) throw new SheetDocumentException($"The column width at {CellAddress.ColumnName(span.First)} does not say whether it is custom.");
            if (runs.Any(r => r.First <= span.Last && span.First <= r.Last)) throw new SheetDocumentException("A column is given a width twice.");
            runs.Add(new SheetDocumentColumnWidth(span.First, span.Last, set, isCustom));
        }
        runs.Sort((a, b) => a.First.CompareTo(b.First));
        return runs;
    }

    private static NumberFormat ReadFormat(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && NumberFormat.TryParse(value.GetString()!, out var parsed, out _)
            ? parsed
            : throw new SheetDocumentException($"'{value}' is not a number format this version reads.");

    private static HorizontalAlignment ReadAlignment(JsonElement value, bool allowGeneral) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() switch
        {
            "general" when allowGeneral => HorizontalAlignment.General,
            "left" => HorizontalAlignment.Left,
            "center" => HorizontalAlignment.Center,
            "right" => HorizontalAlignment.Right,
            _ => throw new SheetDocumentException($"'{value}' is not an alignment."),
        } : throw new SheetDocumentException($"'{value}' is not an alignment.");

    private static SheetDocumentTable ReadTable(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A Linked Table is not a JSON object.");
        string? name = null;
        List<string>? columns = null;
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
                default:
                    throw new SheetDocumentException($"'{property.Name}' is not part of a Linked Table's declaration.");
            }
        }
        if (name is null) throw new SheetDocumentException("A Linked Table has no name.");
        if (columns is null) throw new SheetDocumentException($"The Linked Table '{name}' has no columns.");
        if (Sheet.WhyNotALinkedTable(name, columns) is { } why) throw new SheetDocumentException(why);
        return new SheetDocumentTable(name, columns);
    }

    private static SheetDocumentCell ReadCell(JsonElement element, int version)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A cell is not a JSON object.");
        CellAddress? address = null;
        Entry? entry = null;
        NumberFormat? format = null;
        HorizontalAlignment? alignment = null;
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
                default:
                    throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} cell.");
            }
        }
        if (address is null) throw new SheetDocumentException("A cell has no address.");
        if (entry is null && format is null && alignment is null) throw new SheetDocumentException($"The cell {address} holds nothing.");
        return new SheetDocumentCell(address.Value, entry, format, alignment);
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

/// <summary>One cell of a Sheet Document: where it is, its Entry and its formatting. Never its Value (ADR-0048).</summary>
/// <param name="Address">Where the cell is.</param>
/// <param name="Entry">What the user put into it; <see langword="null"/> for a cell that holds only formatting.</param>
/// <param name="Format">The number format the cell sets itself; <see langword="null"/> when it takes its row's or column's (ADR-0047).</param>
/// <param name="Alignment">The horizontal alignment the cell sets itself; <see langword="null"/> when it takes its row's or column's (ADR-0047).</param>
public sealed record SheetDocumentCell(CellAddress Address, Entry? Entry, NumberFormat? Format, HorizontalAlignment? Alignment);

/// <summary>
/// A format set on whole columns or whole rows, as a Sheet Document records it: one entry for a
/// run of adjacent columns (rows) set alike, never one per cell (ADR-0047).
/// </summary>
/// <param name="First">The first column (row) of the run, from 0.</param>
/// <param name="Last">The last column (row) of the run.</param>
/// <param name="Format">The number format set on them, or <see langword="null"/>.</param>
/// <param name="Alignment">The horizontal alignment set on them, or <see langword="null"/>.</param>
public sealed record SheetDocumentAxisStyle(int First, int Last, NumberFormat? Format, HorizontalAlignment? Alignment);

/// <summary>
/// A width recorded on columns, as a Sheet Document records it: one entry for a run of adjacent
/// columns of one width and one origin (ADR-0046). A column at the default width has none.
/// </summary>
/// <param name="First">The first column of the run, from 0.</param>
/// <param name="Last">The last column of the run.</param>
/// <param name="Width">The width, in characters of the default font (Excel's unit, ADR-0047).</param>
/// <param name="IsCustom">
/// Whether the user set it (<see cref="SheetColumnWidth.IsCustom"/>); <see langword="false"/> for
/// a width an entry widened the columns to, which a longer entry widens again.
/// </param>
public sealed record SheetDocumentColumnWidth(int First, int Last, double Width, bool IsCustom = true);

/// <summary>A Linked Table's declaration as a Sheet Document records it: its name and its column names, never its rows (ADR-0049).</summary>
/// <param name="Name">The name Formulas read it by.</param>
/// <param name="Columns">The column names, in order.</param>
public sealed record SheetDocumentTable(string Name, IReadOnlyList<string> Columns);

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
