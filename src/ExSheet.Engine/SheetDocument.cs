using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExSheet.Engine;

/// <summary>
/// The serialisable form of a Sheet (CONTEXT.md, ADR-0048). It records the Sheet's culture, its
/// name (ADR-0046), the Linked Tables declared on it — each one's name and column names, never its
/// rows (ADR-0049) — and its Entries — constants already parsed, Formulas in invariant syntax — and
/// never a Value: opening one computes every Value again. The Consumer persists it; ExSheet never does.
/// </summary>
/// <remarks>
/// The document is a format with a version. A reader meeting a version it does not know refuses
/// the document rather than guess at it (ADR-0048); so does a reader meeting anything it does not
/// understand. This engine writes version 2 and reads versions 1 and 2: version 1 recorded no
/// name and no Linked Table, and a Sheet opened from one is named <see cref="Sheet.DefaultName"/>
/// and declares none.
/// </remarks>
public sealed class SheetDocument
{
    /// <summary>The version this engine writes.</summary>
    public const int CurrentVersion = 2;

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
                if (!cell.Format.IsGeneral) json.WriteString("format", cell.Format.Code);
                if (cell.Alignment != HorizontalAlignment.General) json.WriteString("align", AlignmentName(cell.Alignment));
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
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
            return new SheetDocument(culture, name ?? Sheet.DefaultName, tables, cells);
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
        var format = NumberFormat.General;
        var alignment = HorizontalAlignment.General;
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
                    if (value.ValueKind != JsonValueKind.String || !NumberFormat.TryParse(value.GetString()!, out var parsedFormat, out var why))
                    {
                        throw new SheetDocumentException($"'{value}' is not a number format this version reads.");
                    }
                    format = parsedFormat;
                    break;
                case "align":
                    alignment = value.ValueKind == JsonValueKind.String ? value.GetString() switch
                    {
                        "left" => HorizontalAlignment.Left,
                        "center" => HorizontalAlignment.Center,
                        "right" => HorizontalAlignment.Right,
                        _ => throw new SheetDocumentException($"'{value}' is not an alignment."),
                    } : throw new SheetDocumentException($"'{value}' is not an alignment.");
                    break;
                default:
                    throw new SheetDocumentException($"'{property.Name}' is not part of a version {version} cell.");
            }
        }
        if (address is null) throw new SheetDocumentException("A cell has no address.");
        if (entry is null && format.IsGeneral && alignment == HorizontalAlignment.General) throw new SheetDocumentException($"The cell {address} holds nothing.");
        return new SheetDocumentCell(address.Value, entry, format, alignment);
    }

    private static string AlignmentName(HorizontalAlignment alignment) => alignment switch
    {
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
/// <param name="Format">Its number format.</param>
/// <param name="Alignment">Its horizontal alignment.</param>
public sealed record SheetDocumentCell(CellAddress Address, Entry? Entry, NumberFormat Format, HorizontalAlignment Alignment);

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
