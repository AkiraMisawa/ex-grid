using System.Globalization;
using System.Text.Json;
using ExSheet.Engine;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The Excel case corpus under <c>ExcelCases/</c>: one JSON file per area, read both by
/// <see cref="ExcelCaseTests"/> and by <c>ExcelOracle/oracle.ps1</c>, which asks a real Excel the
/// same questions (ADR-0047). This class runs one case against the engine and says how its answer
/// differs from the case's <c>expect</c>.
/// </summary>
internal static class ExcelCorpus
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "ExcelCases");

    private static readonly Lazy<Dictionary<string, JsonElement>> Fixtures = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, "fixtures.json")));
        return document.RootElement.GetProperty("fixtures").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetProperty("cells").Clone(), StringComparer.Ordinal);
    });

    private static readonly Dictionary<string, IReadOnlyList<JsonElement>> Areas = new(StringComparer.Ordinal);

    /// <summary>The area files present, by name, without <c>fixtures.json</c>.</summary>
    public static IEnumerable<string> AreaFiles =>
        System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != "fixtures")
            .Select(n => n!)
            .Order(StringComparer.Ordinal);

    public static IReadOnlyList<JsonElement> Cases(string area)
    {
        lock (Areas)
        {
            if (!Areas.TryGetValue(area, out var cases))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, area + ".json")));
                if (document.RootElement.GetProperty("area").GetString() != area)
                {
                    throw new InvalidDataException($"{area}.json names another area.");
                }
                cases = [.. document.RootElement.GetProperty("cases").EnumerateArray().Select(c => c.Clone())];
                Areas[area] = cases;
            }
            return cases;
        }
    }

    public static JsonElement Case(string area, string id) =>
        Cases(area).Single(c => c.GetProperty("id").GetString() == id);

    /// <summary>Runs the case and returns every way the engine's answer differs from <c>expect</c>; empty when it agrees.</summary>
    public static IReadOnlyList<string> Run(JsonElement c)
    {
        // "refusedEntry": Excel refuses one of the case's entries, and the engine must refuse it too.
        var refusesEntry = c.GetProperty("expect").TryGetProperty("refusedEntry", out var refusedEntry) && refusedEntry.GetBoolean();
        Sheet sheet;
        try
        {
            sheet = Build(c, TypedWidening(c));
        }
        catch (FormulaSyntaxException e)
        {
            return refusesEntry ? [] : [$"an entry was refused: {e.Message}"];
        }
        catch (FormatException e)
        {
            // A format code refused, as Excel refuses one it will not take (the oracle counts it as a refused entry).
            return refusesEntry ? [] : [$"a format was refused: {e.Message}"];
        }
        if (refusesEntry) return ["expected an entry refused, but every entry was taken"];
        var refusal = Act(sheet, c);
        var at = CellAddress.Parse(c.GetProperty("check").GetString()!);
        var expect = c.GetProperty("expect");
        var differences = new List<string>();

        if (expect.TryGetProperty("refused", out var refused))
        {
            if (refusal?.ToString() != refused.GetString())
            {
                differences.Add($"expected the action refused ({refused.GetString()}), but it was {(refusal is null ? "done" : $"refused ({refusal})")}");
            }
        }
        else if (refusal is not null)
        {
            differences.Add($"the action was refused ({refusal})");
        }

        if (expect.TryGetProperty("value2", out var value2))
        {
            var kind = expect.TryGetProperty("kind", out var k) ? k.GetString() : null;
            int? digits = expect.TryGetProperty("digits", out var d) ? d.GetInt32() : null;
            var actual = sheet.GetValue(at);
            if (!SameValue(value2, kind, digits, actual))
            {
                differences.Add($"value2: expected {value2.GetRawText()}{(kind is null ? "" : $" ({kind})")}, got {Describe(actual)}");
            }
        }

        if (expect.TryGetProperty("text", out var text))
        {
            // The oracle reads Excel's Range.Text in a column 100 characters wide unless the case
            // gives the column's width; the engine is asked at the same width.
            var display = sheet.GetDisplay(at, ColumnWidth(c) ?? 100);
            var shown = display.CannotShow ? "####" : display.Text;
            if (shown != text.GetString()) differences.Add($"text: expected \"{text.GetString()}\", got \"{shown}\"");
        }

        if (expect.TryGetProperty("widthOnEntry", out var widthOnEntry))
        {
            int? expected = widthOnEntry.ValueKind == JsonValueKind.Null ? null : widthOnEntry.GetInt32();
            var actual = sheet.GetWidthOnEntry(at);
            if (actual != expected) differences.Add($"widthOnEntry: expected {expected?.ToString(CultureInfo.InvariantCulture) ?? "null"}, got {actual?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
        }

        if (expect.TryGetProperty("widens", out var widens))
        {
            // Whether the check column ends wider than the default width: the typed entries
            // widened it as the component does (TypedWidening).
            var actual = sheet.GetColumnWidth(at.Column) is { } recorded && recorded.Width > Sheet.DefaultColumnWidth;
            if (actual != widens.GetBoolean()) differences.Add($"widens: expected {widens.GetBoolean()}, got {actual}");
        }

        if (expect.TryGetProperty("widthAtMost", out var widthAtMost))
        {
            // The check column's width after every typed entry is no wider than this (characters).
            var actual = sheet.GetColumnWidth(at.Column)?.Width ?? Sheet.DefaultColumnWidth;
            if (actual > widthAtMost.GetDouble()) differences.Add($"widthAtMost: expected at most {widthAtMost.GetDouble().ToString(CultureInfo.InvariantCulture)}, got {actual.ToString(CultureInfo.InvariantCulture)}");
        }

        if (expect.TryGetProperty("widthAtLeast", out var widthAtLeast))
        {
            // The check column's width after every typed entry is at least this (characters): a
            // column a typed entry widened is widened again by a longer one (ADR-0046, 2026-09-28; CW-028).
            var actual = sheet.GetColumnWidth(at.Column)?.Width ?? Sheet.DefaultColumnWidth;
            if (actual < widthAtLeast.GetDouble()) differences.Add($"widthAtLeast: expected at least {widthAtLeast.GetDouble().ToString(CultureInfo.InvariantCulture)}, got {actual.ToString(CultureInfo.InvariantCulture)}");
        }

        if (expect.TryGetProperty("width", out var width))
        {
            // The check column's recorded width in characters; null where none is (Excel's standard width).
            double? expected = width.ValueKind == JsonValueKind.Null ? null : width.GetDouble();
            var actual = sheet.GetColumnWidth(at.Column)?.Width;
            if (actual != expected) differences.Add($"width: expected {expected?.ToString(CultureInfo.InvariantCulture) ?? "null"}, got {actual?.ToString(CultureInfo.InvariantCulture) ?? "null"}");
        }

        if (expect.TryGetProperty("custom", out var custom))
        {
            // Whether the check column's width is marked custom (Excel's customWidth): every recorded
            // width is, whichever its kind; a column with no recorded width is not (ADR-0046, 2026-09-28).
            var actual = sheet.GetColumnWidth(at.Column)?.IsCustom ?? false;
            if (actual != custom.GetBoolean()) differences.Add($"custom: expected {custom.GetBoolean()}, got {actual}");
        }

        if (expect.TryGetProperty("formula", out var formula))
        {
            var entry = sheet.GetEntry(at);
            var written = entry is null ? "" : entry.Formula ?? sheet.GetEntryText(at);
            if (written != formula.GetString()) differences.Add($"formula: expected \"{formula.GetString()}\", got \"{written}\"");
        }

        if (expect.TryGetProperty("numberFormat", out var numberFormat))
        {
            var code = sheet.GetNumberFormat(at).Code;
            if (code != numberFormat.GetString()) differences.Add($"numberFormat: expected \"{numberFormat.GetString()}\", got \"{code}\"");
        }

        if (expect.TryGetProperty("alignment", out var alignment))
        {
            var setting = sheet.GetAlignment(at).ToString().ToLowerInvariant();
            if (setting != alignment.GetString()) differences.Add($"alignment: expected \"{alignment.GetString()}\", got \"{setting}\"");
        }

        return differences;
    }

    /// <summary>The check column's width in characters, when the case sets one (<c>columnWidth</c>).</summary>
    private static double? ColumnWidth(JsonElement c) =>
        c.TryGetProperty("columnWidth", out var width) ? width.GetDouble() : null;

    /// <summary>
    /// Whether the case asks how the typed entries widened the check column (<c>widens</c>,
    /// <c>widthAtMost</c>, <c>widthAtLeast</c>). The oracle types those cases with real keys, and Excel widens the
    /// column as it goes; the engine leaves widening to the component, so the harness does it
    /// the component's way after each of the case's own cells (<see cref="Widen"/>).
    /// </summary>
    private static bool TypedWidening(JsonElement c) =>
        c.GetProperty("expect") is var expect && (expect.TryGetProperty("widens", out _) || expect.TryGetProperty("widthAtMost", out _) || expect.TryGetProperty("widthAtLeast", out _));

    /// <summary>
    /// The component's widening on entry (<c>ExSheet.razor</c>, <c>WidenOnEntry</c>): a column
    /// whose width the user did not set that is narrower than what the entry needs
    /// (<see cref="Sheet.GetWidthOnEntry"/>) is widened to it, and the width is recorded as widened
    /// by entry: marked custom, as Excel's file marks it (CW-018), and widened again by a longer
    /// entry (CW-028; ADR-0046, 2026-09-28). The component converts the width through the grid's
    /// pixels; here it is the engine's characters, which is why the corpus compares widening, not
    /// Excel's exact width.
    /// </summary>
    private static void Widen(Sheet sheet, CellAddress address)
    {
        var recorded = sheet.GetColumnWidth(address.Column);
        if (recorded is { IsSetByUser: true }) return;
        if (sheet.GetWidthOnEntry(address) is not { } needed) return;
        if (needed <= (recorded?.Width ?? Sheet.DefaultColumnWidth)) return;
        sheet.SetAutomaticColumnWidth(CellRange.WholeColumns(address.Column, address.Column), needed);
    }

    private static Sheet Build(JsonElement c, bool widen)
    {
        var culture = CultureInfo.GetCultureInfo(c.TryGetProperty("culture", out var cu) ? cu.GetString()! : "en-US");
        var sheet = c.TryGetProperty("sheetName", out var name) ? new Sheet(culture, name.GetString()!) : new Sheet(culture);

        if (c.TryGetProperty("tables", out var tables))
        {
            foreach (var table in tables.EnumerateObject())
            {
                sheet.DeclareLinkedTable(table.Name, [.. table.Value.GetProperty("columns").EnumerateArray().Select(e => e.GetString()!)]);
                var rows = table.Value.GetProperty("rows");
                if (rows.ValueKind != JsonValueKind.Null)
                {
                    sheet.PushLinkedTable(table.Name, [.. rows.EnumerateArray().Select(r => (IReadOnlyList<Value?>)[.. r.EnumerateArray().Select(ToValue)])]);
                }
            }
        }

        if (c.TryGetProperty("fixture", out var fixture))
        {
            Enter(sheet, Fixtures.Value[fixture.GetString()!]);
        }
        Enter(sheet, c.GetProperty("cells"), widen);

        if (c.TryGetProperty("formats", out var formats))
        {
            foreach (var format in formats.EnumerateObject())
            {
                sheet.SetNumberFormat(CellAddress.Parse(format.Name), NumberFormat.Parse(format.Value.GetString()!));
            }
        }
        return sheet;
    }

    private static void Enter(Sheet sheet, JsonElement cells, bool widen = false)
    {
        foreach (var cell in cells.EnumerateObject())
        {
            var address = CellAddress.Parse(cell.Name);
            sheet.Enter(address, cell.Value.GetString()!);
            if (widen) Widen(sheet, address);
        }
    }

    /// <summary>Does the case's actions in order; the first refused one stops them, and its reason is returned.</summary>
    private static SheetRefusalReason? Act(Sheet sheet, JsonElement c)
    {
        if (!c.TryGetProperty("action", out var action)) return null;
        var steps = new Stack<SheetStep>();
        foreach (var a in action.ValueKind == JsonValueKind.Array ? [.. action.EnumerateArray()] : new[] { action })
        {
            var what = a.GetProperty("do").GetString();
            if (what == "undo")
            {
                steps.Pop().Undo();
                continue;
            }
            var edit = Edit(sheet, a, what!);
            var refusal = sheet.Check(edit);
            if (refusal is not null)
            {
                // Check and Do agree: a refused edit throws with the same reason and changes nothing.
                var thrown = Xunit.Assert.Throws<SheetRefusedException>(() => sheet.Do(edit));
                Xunit.Assert.Equal(refusal.Reason, thrown.Refusal.Reason);
                return refusal.Reason;
            }
            steps.Push(sheet.Do(edit));
        }
        return null;
    }

    private static SheetEdit Edit(Sheet sheet, JsonElement a, string what)
    {
        int Count() => a.TryGetProperty("count", out var n) ? n.GetInt32() : 1;
        int Row() => a.GetProperty("row").GetInt32() - 1;
        int Column() => CellAddress.Parse(a.GetProperty("column").GetString() + "1").Column;
        CellRange Range(string property) => CellRange.Parse(a.GetProperty(property).GetString()!);

        switch (what)
        {
            case "enter":
                return SheetEdit.Enter(a.GetProperty("cells").EnumerateObject()
                    .Select(p => new KeyValuePair<CellAddress, string>(CellAddress.Parse(p.Name), p.Value.GetString()!)));
            case "insertRows": return SheetEdit.InsertRows(Row(), Count());
            case "deleteRows": return SheetEdit.DeleteRows(Row(), Count());
            case "insertColumns": return SheetEdit.InsertColumns(Column(), Count());
            case "deleteColumns": return SheetEdit.DeleteColumns(Column(), Count());
            case "fill":
                var direction = Enum.Parse<FillDirection>(a.GetProperty("direction").GetString()!, ignoreCase: true);
                return SheetEdit.Fill(Range("source"), Range("target"), direction);
            case "copy":
                var block = sheet.Copy(Range("source")).Block!;
                var destination = Range("destination");
                return destination.RowCount == 1 && destination.ColumnCount == 1
                    ? SheetEdit.Paste(block, destination.First)
                    : SheetEdit.Paste(block, destination);
            case "pasteText":
                IReadOnlyList<IReadOnlyList<string>> rows = [.. a.GetProperty("rows").EnumerateArray()
                    .Select(r => (IReadOnlyList<string>)[.. r.EnumerateArray().Select(f => f.GetString()!)])];
                return SheetEdit.PasteText(rows, CellAddress.Parse(a.GetProperty("at").GetString()!));
            case "setColumnWidth":
                var w = a.GetProperty("width");
                // "automatic" is a width an entry widened the columns to (ADR-0046, 2026-09-28); "sizeToFit"
                // is the user's size to fit, a width the user set like any other.
                if (a.TryGetProperty("automatic", out var automatic) && automatic.GetBoolean()) return SheetEdit.SetAutomaticColumnWidth(Range("range"), w.GetDouble());
                return SheetEdit.SetColumnWidth(Range("range"), w.ValueKind == JsonValueKind.Null ? null : w.GetDouble());
            case "rename": return SheetEdit.Rename(a.GetProperty("name").GetString()!);
            case "format" or "align" or "cellFormat":
                // A range written with commas is several ranges formatted in one step (ADR-0046).
                var ranges = a.GetProperty("range").GetString()!.Split(',').Select(CellRange.Parse).ToList();
                NumberFormat? format = a.TryGetProperty("format", out var f) ? NumberFormat.Parse(f.GetString()!) : null;
                HorizontalAlignment? align = a.TryGetProperty("align", out var al) ? Enum.Parse<HorizontalAlignment>(al.GetString()!, ignoreCase: true) : null;
                if (what == "cellFormat" || ranges.Count > 1) return SheetEdit.SetCellFormat(ranges, new CellFormatChange { NumberFormat = format, Alignment = align });
                return what == "format" ? SheetEdit.SetNumberFormat(ranges[0], format) : SheetEdit.SetAlignment(ranges[0], align!.Value);
            default: throw new InvalidDataException($"Unknown action \"{what}\".");
        }
    }

    /// <summary>A value written in the corpus: a number, a boolean, null for a blank, or text — where text that is an Error Value's spelling is that Error Value.</summary>
    private static Value? ToValue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.Number => Value.FromNumber(e.GetDouble()),
        JsonValueKind.True => Value.FromBoolean(true),
        JsonValueKind.False => Value.FromBoolean(false),
        JsonValueKind.String => ErrorFromText(e.GetString()!) is { } error ? Value.FromError(error) : Value.FromText(e.GetString()!),
        _ => throw new InvalidDataException($"Not a value: {e.GetRawText()}"),
    };

    private static ErrorValue? ErrorFromText(string text) =>
        Enum.GetValues<ErrorValue>().Cast<ErrorValue?>().FirstOrDefault(e => e!.Value.ToText() == text);

    private static bool SameValue(JsonElement expected, string? kind, int? digits, Value? actual)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Null:
                return actual is null;
            case JsonValueKind.True or JsonValueKind.False:
                return actual is { Kind: ValueKind.Boolean } b && b.Boolean == expected.GetBoolean();
            case JsonValueKind.Number:
                if (actual is not { Kind: ValueKind.Number } n) return false;
                var e = expected.GetDouble();
                return digits is { } places
                    ? Math.Round(e, places) == Math.Round(n.Number, places)
                    : e == n.Number;
            case JsonValueKind.String:
                var s = expected.GetString()!;
                if (kind != "text" && ErrorFromText(s) is { } error) return actual is { Kind: ValueKind.Error } x && x.Error == error;
                return actual is { Kind: ValueKind.Text } t && t.Text == s;
            default:
                throw new InvalidDataException($"Not a value2: {expected.GetRawText()}");
        }
    }

    private static string Describe(Value? value) => value switch
    {
        null => "blank",
        { Kind: ValueKind.Number } v => v.Number.ToString("R", CultureInfo.InvariantCulture),
        { Kind: ValueKind.Text } v => $"text \"{v.Text}\"",
        { Kind: ValueKind.Boolean } v => v.Boolean ? "TRUE" : "FALSE",
        { } v => v.Error.ToText(),
    };
}
