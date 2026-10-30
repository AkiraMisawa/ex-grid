using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExSheet.Engine;

/// <summary>
/// The serialisable form of a Workbook (CONTEXT.md, ADR-0331): its Sheets in order, the Sheet shown,
/// its culture and its Linked Tables' declarations, and for each Sheet what a Sheet Document records
/// of it — its name, Entries, Cell Formats and column widths — never a Value: opening one computes
/// every Value again. The Consumer persists it; ExSheet never does.
/// </summary>
/// <remarks>
/// The document is a format with a version, beside the Sheet Document's. A reader meeting a version it
/// does not know refuses the document rather than guess at it, and so does a reader meeting anything it
/// does not understand (ADR-0048). This engine writes version 1, whose Sheets are recorded as a Sheet
/// Document of version <see cref="SheetDocument.CurrentVersion"/> records a Sheet. <see cref="FromJson"/>
/// also reads a Sheet Document of every version <see cref="SheetDocument.FromJson"/> reads, as a Workbook
/// of that one Sheet.
/// </remarks>
public sealed class WorkbookDocument
{
    /// <summary>The version this engine writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The oldest version this engine reads.</summary>
    public const int OldestReadableVersion = 1;

    internal WorkbookDocument(string culture, string shown, IReadOnlyList<SheetDocumentTable> linkedTables, IReadOnlyList<SheetDocument> sheets)
    {
        Culture = culture;
        Shown = shown;
        LinkedTables = linkedTables;
        Sheets = sheets;
    }

    /// <summary>
    /// A Workbook Document built from its Sheets, as a reader of another format builds one
    /// (ADR-0330 §3): <paramref name="sheets"/> in the order their tabs stand, the one named
    /// <paramref name="shown"/> shown, under <paramref name="culture"/>, declaring no Linked Table.
    /// </summary>
    /// <param name="culture">The Workbook's culture; one this engine knows by name, and the culture every Sheet records.</param>
    /// <param name="shown">The name of the Sheet shown, one of <paramref name="sheets"/> named without regard to case.</param>
    /// <param name="sheets">The Sheets, in order: at least one, no two named alike without regard to case, each under <paramref name="culture"/> and declaring no Linked Table of its own.</param>
    /// <exception cref="ArgumentException">
    /// The culture is not one this engine knows by name, there is no Sheet, two share a name, a Sheet
    /// records another culture or declares a Linked Table, or no Sheet is named <paramref name="shown"/>.
    /// Nothing is guessed.
    /// </exception>
    public WorkbookDocument(CultureInfo culture, string shown, IEnumerable<SheetDocument> sheets)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(sheets);
        SheetDocument.CheckCulture(culture, nameof(culture));
        var list = new List<SheetDocument>();
        foreach (var sheet in sheets)
        {
            ArgumentNullException.ThrowIfNull(sheet, nameof(sheets));
            if (list.Any(s => string.Equals(s.Name, sheet.Name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Two Sheets are named '{sheet.Name}'.", nameof(sheets));
            if (sheet.Culture != culture.Name)
                throw new ArgumentException($"The Sheet '{sheet.Name}' records the culture '{sheet.Culture}', not the Workbook's '{culture.Name}'.", nameof(sheets));
            if (sheet.LinkedTables.Count > 0)
                throw new ArgumentException($"The Sheet '{sheet.Name}' declares a Linked Table; a Workbook's Linked Tables are declared on the Workbook.", nameof(sheets));
            list.Add(sheet);
        }
        if (list.Count == 0) throw new ArgumentException("A Workbook holds at least one Sheet.", nameof(sheets));
        var shownSheet = list.FirstOrDefault(s => string.Equals(s.Name, shown, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"The Sheet shown, '{shown}', is not one of the Sheets.", nameof(shown));
        Culture = culture.Name;
        Shown = shownSheet.Name;
        LinkedTables = [];
        Sheets = list;
    }

    /// <summary>The format version the document is written in: <see cref="CurrentVersion"/>, whatever it was read from.</summary>
    public int Version => CurrentVersion;

    /// <summary>The name of the Workbook's declared culture, such as <c>en-US</c>; empty for the invariant culture.</summary>
    public string Culture { get; }

    /// <summary>The name of the Sheet shown, one of <see cref="Sheets"/>.</summary>
    public string Shown { get; }

    /// <summary>
    /// The Linked Tables declared on the Workbook, in the order they were declared: names, column names
    /// and keys only (ADR-0049). A Workbook opened from the document declares them again, and their
    /// readers show <c>#GETTING_DATA</c> until the Consumer pushes a snapshot.
    /// </summary>
    public IReadOnlyList<SheetDocumentTable> LinkedTables { get; }

    /// <summary>The Sheets, in the order their tabs stand, each under the Workbook's culture and declaring no Linked Table of its own.</summary>
    public IReadOnlyList<SheetDocument> Sheets { get; }

    /// <summary>
    /// The Workbook's one Sheet as a Sheet Document, declaring the Workbook's Linked Tables: the Sheet Document
    /// that records what this Workbook Document records.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Workbook holds several Sheets, which a Sheet Document cannot
    /// record (ADR-0331).</exception>
    public SheetDocument ToSheetDocument() => Sheets.Count == 1
        ? Sheets[0].With(LinkedTables, Sheets[0].Cells)
        : throw new InvalidOperationException($"The Workbook holds {Sheets.Count} Sheets, which a Sheet Document cannot record (ADR-0331).");

    /// <summary>The document with <paramref name="sheets"/>, in the same order and of the same names, in place of its own.</summary>
    internal WorkbookDocument WithSheets(IReadOnlyList<SheetDocument> sheets) => new(Culture, Shown, LinkedTables, sheets);

    /// <summary>Writes the document as JSON.</summary>
    public string ToJson()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("workbookVersion", CurrentVersion);
            json.WriteString("culture", Culture);
            json.WriteString("shown", Shown);
            SheetDocument.WriteTables(json, LinkedTables);
            json.WriteStartArray("sheets");
            foreach (var sheet in Sheets)
            {
                json.WriteStartObject();
                json.WriteString("name", sheet.Name);
                sheet.WriteBody(json);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Reads a document written by <see cref="ToJson"/>, or a Sheet Document of any version
    /// <see cref="SheetDocument.FromJson"/> reads, as a Workbook of that one Sheet, shown, with its
    /// Linked Tables declared on the Workbook (ADR-0331).
    /// </summary>
    /// <exception cref="SheetDocumentException">
    /// The version is not one this engine reads, the document holds anything its version does not
    /// define, it holds no Sheet or two of one name, or the Sheet it shows is not one of them. Nothing
    /// is guessed.
    /// </exception>
    public static WorkbookDocument FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new SheetDocumentException("The Workbook Document is not JSON.", e);
        }
        using (parsed)
        {
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new SheetDocumentException("A Workbook Document is a JSON object.");
            if (!root.TryGetProperty("workbookVersion", out var version))
            {
                // No Workbook version: a Sheet Document, read as one, or refused as one is.
                var sheet = SheetDocument.FromJson(json);
                return new WorkbookDocument(sheet.Culture, sheet.Name, sheet.LinkedTables, [WithoutTables(sheet)]);
            }
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
            {
                throw new SheetDocumentException("The Workbook Document's version is not a number.");
            }
            if (number is < OldestReadableVersion or > CurrentVersion)
            {
                throw new SheetDocumentException($"The Workbook Document is version {number}; this engine reads versions {OldestReadableVersion} to {CurrentVersion}.") { DocumentVersion = number };
            }

            string? culture = null;
            string? shown = null;
            List<SheetDocumentTable> tables = [];
            JsonElement? sheetsElement = null;
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "workbookVersion":
                        break;
                    case "culture":
                        culture = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : throw new SheetDocumentException("The culture is not a string.");
                        break;
                    case "shown":
                        shown = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : throw new SheetDocumentException("The Sheet shown is not a string.");
                        break;
                    case "linkedTables":
                        tables = SheetDocument.ReadTables(property.Value, SheetDocument.CurrentVersion);
                        break;
                    case "sheets":
                        sheetsElement = property.Value.ValueKind == JsonValueKind.Array ? property.Value : throw new SheetDocumentException("The Sheets are not an array.");
                        break;
                    default:
                        throw new SheetDocumentException($"'{property.Name}' is not part of a version {number} Workbook Document.");
                }
            }
            if (culture is null) throw new SheetDocumentException("The Workbook Document records no culture.");
            SheetDocument.ResolveCulture(culture);
            if (sheetsElement is not { } array) throw new SheetDocumentException("The Workbook Document records no Sheets.");
            var sheets = new List<SheetDocument>();
            foreach (var element in array.EnumerateArray())
            {
                var sheet = SheetDocument.ReadSheetOfWorkbook(element, culture);
                if (sheets.Any(s => string.Equals(s.Name, sheet.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new SheetDocumentException($"Two Sheets of the Workbook Document are named '{sheet.Name}'.");
                }
                sheets.Add(sheet);
            }
            if (sheets.Count == 0) throw new SheetDocumentException("The Workbook Document records no Sheets.");
            if (shown is null) throw new SheetDocumentException("The Workbook Document records no Sheet shown.");
            var shownSheet = sheets.FirstOrDefault(s => string.Equals(s.Name, shown, StringComparison.OrdinalIgnoreCase))
                ?? throw new SheetDocumentException($"The Sheet shown, '{shown}', is not a Sheet of the Workbook Document.");
            return new WorkbookDocument(culture, shownSheet.Name, tables, sheets);
        }
    }

    private static SheetDocument WithoutTables(SheetDocument sheet) => sheet.With([], sheet.Cells);
}
