using System.Text.Json;

namespace ExSheet.Engine;

/// <summary>
/// Reads the Workbook Documents a Script Core Sheet raises from what changed in each (ADR-0370 section 1's note of
/// 2026-10-30), as the Script Core's <c>DocumentChanges</c> writes them, for an adapter over it: every part of the
/// Workbook whole but the cells, and of each Sheet either all its cells, or only those that changed since the Sheet
/// of its name in the document read before, with the addresses of the cells emptied since. A Sheet of thousands of
/// cells changes a few at an edit, and only those are read.
/// </summary>
/// <remarks>
/// The writer numbers each document it writes, and a document written as changes names the one it was written
/// against: a reader that missed one, or failed to read one, refuses every document written as changes until one
/// written whole comes, rather than build a document from a base the writer has moved past.
/// </remarks>
public sealed class DocumentChangesReader
{
    private WorkbookDocument? _read;
    private long _number;

    /// <summary>The document <paramref name="json"/> describes, read against the one read before it.</summary>
    /// <param name="json">What changed, as the Script Core's <c>DocumentChanges</c> wrote it.</param>
    /// <exception cref="FormatException">The JSON is not changes so written; or they are written against a document
    /// other than the one read before, a Sheet it does not hold, or a cell it does not hold. Nothing is read after it
    /// but a document written whole.</exception>
    /// <exception cref="SheetDocumentException">The Workbook Document they carry is not one this engine reads.</exception>
    public WorkbookDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var before = _read;
        var number = _number;
        // Forgotten until this one is read: a failure leaves nothing to read the next against.
        _read = null;
        var read = Read(json, before, number, out var written);
        _read = read;
        _number = written;
        return read;
    }

    private static WorkbookDocument Read(string json, WorkbookDocument? before, long number, out long written)
    {
        JsonDocument parsed;
        try
        {
            parsed = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Not JSON: {e.Message}", e);
        }
        using (parsed)
        {
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("A Workbook Document's changes are a JSON object.");
            written = Member(root, "number") is { ValueKind: JsonValueKind.Number } n && n.TryGetInt64(out var at) && at > 0
                ? at
                : throw new FormatException("The changes' number is not a whole number from 1.");
            var workbook = Member(root, "workbook");
            if (workbook.ValueKind != JsonValueKind.Object)
                throw new FormatException("The changes' Workbook Document is not a JSON object.");
            var changes = WorkbookDocument.FromJson(workbook.GetRawText());
            var emptied = Member(root, "emptied");
            if (emptied.ValueKind != JsonValueKind.Array || emptied.GetArrayLength() != changes.Sheets.Count)
                throw new FormatException($"The changes do not say, for each of the Workbook's {changes.Sheets.Count} Sheets, which cells were emptied.");
            var sheets = new List<SheetDocument>(changes.Sheets.Count);
            var i = 0;
            foreach (var gone in emptied.EnumerateArray())
            {
                var sheet = changes.Sheets[i++];
                if (gone.ValueKind == JsonValueKind.Null)
                {
                    sheets.Add(sheet);
                    continue;
                }
                if (before is null || written != number + 1)
                {
                    throw new FormatException(before is null
                        ? $"The changes numbered {written} are written against a document this reader has not read."
                        : $"The changes numbered {written} are written against document {written - 1}, and this reader read {number} last.");
                }
                var basis = before.Sheets.FirstOrDefault(s => string.Equals(s.Name, sheet.Name, StringComparison.Ordinal))
                    ?? throw new FormatException($"The changes are made to a Sheet '{sheet.Name}' that the document before does not hold.");
                if (gone.ValueKind != JsonValueKind.Array)
                    throw new FormatException($"The cells emptied on the Sheet '{sheet.Name}' are not a list.");
                var addresses = new HashSet<CellAddress>();
                foreach (var address in gone.EnumerateArray())
                {
                    addresses.Add(address.ValueKind == JsonValueKind.String && CellAddress.TryParse(address.GetString(), out var parsedAddress)
                        ? parsedAddress
                        : throw new FormatException($"{address.GetRawText()} is no cell's address."));
                }
                sheets.Add(sheet.With(sheet.LinkedTables, Changed(basis.Cells, sheet.Cells, addresses, sheet.Name)));
            }
            return changes.WithSheets(sheets);
        }
    }

    private static JsonElement Member(JsonElement element, string name) =>
        element.TryGetProperty(name, out var member) ? member : throw new FormatException($"'{name}' is missing from the changes.");

    // The cells of a Sheet, both lists in row-major order: those before, each changed one in its place, and none emptied.
    private static List<SheetDocumentCell> Changed(IReadOnlyList<SheetDocumentCell> before, IReadOnlyList<SheetDocumentCell> changed, HashSet<CellAddress> emptied, string sheet)
    {
        var cells = new List<SheetDocumentCell>(before.Count + changed.Count);
        var next = 0;
        var found = 0;
        foreach (var cell in before)
        {
            while (next < changed.Count && changed[next].Address.CompareTo(cell.Address) < 0)
                cells.Add(changed[next++]);
            if (next < changed.Count && changed[next].Address == cell.Address)
                cells.Add(changed[next++]);
            else if (emptied.Contains(cell.Address))
                found++;
            else
                cells.Add(cell);
        }
        while (next < changed.Count)
            cells.Add(changed[next++]);
        if (found != emptied.Count)
            throw new FormatException($"The changes empty a cell of the Sheet '{sheet}' that the document before does not hold, or one they change.");
        return cells;
    }
}
