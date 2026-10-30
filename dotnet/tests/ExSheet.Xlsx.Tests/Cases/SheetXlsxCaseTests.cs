using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using ExSheet.Engine.Tests.Cases;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Cases.XlsxCaseTests;

namespace ExSheet.Xlsx.Tests.Cases;

/// <summary>
/// The shared cases of opening and saving a Workbook (<c>tests/cases/sheet-xlsx</c>; ADR-0330, ADR-0370 section 2), run against
/// ExSheet.Xlsx: a file written as the .xlsx cases write one, edited part by part, opened, and what the Workbook Document and
/// its Open Report say or how the file was refused; a Workbook built as the Sheet cases build one, saved, and the parts it made
/// or how the save was refused; and the internal pieces both cores share, called one by one. A reader and a writer of .xlsx
/// have no differential run, so these cases are what holds <c>@ex-grid/sheet-xlsx</c> to this package; its runner is
/// <c>packages/sheet-xlsx/test/sheet-xlsx-cases.test.ts</c>, and <c>tests/cases/README.md</c> says what a file holds. With
/// <c>EXGRID_WRITE_SHEET_XLSX_CASES=1</c>, the runner writes what ExSheet.Xlsx answers to each open, save and call into the
/// source files; the Sheet steps' readings are written by hand.
/// </summary>
public class SheetXlsxCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "sheet-xlsx");

    private static bool Writing => Environment.GetEnvironmentVariable("EXGRID_WRITE_SHEET_XLSX_CASES") == "1";

    private static readonly Lock WriteGate = new();

    private static readonly string[] Kinds = ["sheetXlsx", "sheetXlsxCall"];

    // The steps this runner takes itself; every other step is a Sheet case's, taken by its runner.
    private static readonly string[] Verbs = ["open", "save", "workbook", "entries", "keep", "sameAs"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Read(path);
            if (!Kinds.Contains((string?)document["kind"]))
                throw new InvalidOperationException($"{Path.GetFileName(path)} is of no kind this runner reads.");
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0330, ADR-0370 section 2: a case of opening and saving a Workbook holds on ExSheet.Xlsx
    public async Task SheetXlsx(string file, string name)
    {
        var document = Read(Path.Combine(Root, file));
        var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        if ((string)document["kind"]! == "sheetXlsxCall")
        {
            var answer = Escaped(Call(entry));
            if (Writing)
            {
                Write(file, name, e => e["expect"] = answer?.DeepClone());
                return;
            }
            Assert.True(entry.TryGetPropertyValue("expect", out var expected), "The case has no answer: write it with EXGRID_WRITE_SHEET_XLSX_CASES=1.");
            Assert.True(JsonNode.DeepEquals(expected, answer), $"Expected {expected?.ToJsonString() ?? "null"}, answered {answer?.ToJsonString() ?? "null"}.");
            return;
        }

        var culture = SheetCases.Culture((string?)(entry["culture"] ?? document["culture"]) ?? throw new InvalidOperationException("Neither the case nor its file names a culture."));
        var run = new Run(culture);
        if (entry["sheets"] is JsonArray sheets)
            run.Workbook = new Workbook(culture, [.. sheets.Select(s => Text(s!))]);
        var steps = Must(entry, "steps").AsArray();
        var answers = new Dictionary<int, JsonNode?>();
        var kept = new Dictionary<string, Workbook>(StringComparer.Ordinal);
        byte[]? saved = null;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i]!.AsObject();
            var where = $"{name}, step {i + 1}";
            var verbs = step.Select(p => p.Key).Where(Verbs.Contains).ToList();
            if (verbs.Count == 0)
            {
                run.Take(step, where);
                continue;
            }
            if (verbs.Count > 1)
                throw new InvalidOperationException($"{where}: a step takes one verb, not {string.Join(", ", verbs)}.");
            JsonNode? answer;
            switch (verbs[0])
            {
                case "open":
                    Known(step, "open", "culture", "options", "document", "expect", "note");
                    answer = await Open(step, run, saved, culture);
                    break;
                case "save":
                    Known(step, "save", "parts", "file", "expect", "note");
                    (answer, saved) = await Save(step, run.Workbook, saved);
                    break;
                case "workbook":
                    Known(step, "workbook", "note");
                    run.Workbook = Workbook.Open(WorkbookDocument.FromJson(step["workbook"]!.ToJsonString()));
                    continue;
                case "entries":
                    Known(step, "entries", "on", "note");
                    var on = step["on"] is { } sheet ? (string)sheet! : run.Workbook.Shown.Name;
                    // Text as the .xlsx cases write it, which can hold what a JSON string cannot, a constant or {"formula": text}.
                    run.Workbook.Do(on, SheetEdit.SetEntries([.. Must(step, "entries").AsObject().Select(p => KeyValuePair.Create(CellAddress.Parse(p.Key),
                        p.Value is JsonObject { Count: 1 } typed && typed["formula"] is { } formula
                            ? Entry.Parse(Text(formula), culture)
                            : (Entry?)Entry.FromValue(Value.FromText(Text(p.Value!)))))]));
                    continue;
                case "keep":
                    Known(step, "keep", "note");
                    kept[(string)step["keep"]!] = run.Workbook;
                    continue;
                default:
                    Known(step, "sameAs", "note");
                    SameAs(Must(step, "sameAs").AsObject(), kept, run.Workbook, where, run.Differences);
                    continue;
            }
            answer = Escaped(answer);
            if (Writing)
            {
                answers[i] = answer;
                continue;
            }
            if (!step.TryGetPropertyValue("expect", out var expected))
                run.Differences.Add($"{where}: the step has no answer: write it with EXGRID_WRITE_SHEET_XLSX_CASES=1.");
            else if (!JsonNode.DeepEquals(expected, answer))
                run.Differences.Add($"{where}: expected {expected?.ToJsonString() ?? "null"}, answered {answer?.ToJsonString() ?? "null"}.");
        }
        if (Writing)
            Write(file, name, e =>
            {
                var written = e["steps"]!.AsArray();
                foreach (var (index, answer) in answers)
                    written[index]!["expect"] = answer?.DeepClone();
            });
        Assert.True(run.Differences.Count == 0, string.Join("\n", run.Differences));
    }

    // ---- open ---------------------------------------------------------------------------------------

    private static async Task<JsonNode> Open(JsonObject step, Run run, byte[]? saved, CultureInfo culture)
    {
        var bytes = step["open"] is JsonValue named && (string?)named == "saved"
            ? saved ?? throw new InvalidOperationException("The case opens what it saved before it saves.")
            : await FileOf(Must(step, "open").AsObject());
        var given = step["options"]?.AsObject() ?? [];
        Known(given, "maxUncompressedBytes", "maxCells");
        var options = new XlsxReadOptions
        {
            MaxUncompressedBytes = given["maxUncompressedBytes"] is { } limit ? (long)limit : XlsxReadOptions.DefaultMaxUncompressedBytes,
            MaxCells = given["maxCells"] is { } cells ? (long)cells : XlsxReadOptions.DefaultMaxCells,
        };
        var opening = step["culture"] is { } other ? SheetCases.Culture((string)other!) : culture;
        try
        {
            var (document, report) = await XlsxWorkbookReader.ReadAsync(new MemoryStream(bytes, writable: false), opening, options, Token);
            run.Workbook = Workbook.Open(document);
            var answer = new JsonObject();
            if ((bool?)step["document"] ?? true)
                answer["document"] = JsonNode.Parse(document.ToJson());
            answer["report"] = new JsonObject
            {
                ["isEmpty"] = report.IsEmpty,
                ["notCarried"] = new JsonArray([.. report.NotCarried.Select(n => (JsonNode)new JsonObject
                {
                    ["kind"] = SheetCases.Camel(n.Kind),
                    ["sheet"] = n.Sheet,
                    ["detail"] = n.Detail,
                    ["description"] = n.Description,
                })]),
                ["unsupportedFunctions"] = new JsonArray([.. report.UnsupportedFunctions.Select(u => (JsonNode)new JsonObject
                {
                    ["sheet"] = u.Sheet,
                    ["cell"] = u.Cell,
                    ["functions"] = new JsonArray([.. u.Functions.Select(f => (JsonNode)f)]),
                })]),
            };
            return answer;
        }
        catch (XlsxOpenException e)
        {
            return new JsonObject
            {
                ["refused"] = new JsonObject
                {
                    ["reason"] = SheetCases.Camel(e.Reason),
                    ["sheet"] = e.Sheet,
                    ["cell"] = e.Cell,
                    ["held"] = e.Held,
                    ["message"] = e.Message,
                    ["fileRefusal"] = e.FileRefusal is { } refusal ? Name(refusal) : null,
                },
            };
        }
    }

    /// <summary>
    /// The file an open reads: one of the .xlsx cases' files, or <c>{"pages": […], "options": …}</c>, a workbook written by
    /// ExGrid.Data.Xlsx's writer a page at a time; either then edited as <c>"edits"</c> says.
    /// </summary>
    private static async Task<byte[]> FileOf(JsonObject file)
    {
        var bytes = file["pages"] is JsonArray pages
            ? await Pages(file["options"]?.AsObject(), pages)
            : ExGrid.Data.Xlsx.Tests.Cases.XlsxCaseTests.FileOf(file);
        return file["edits"] is JsonArray edits ? Edited(bytes, edits) : bytes;
    }

    /// <summary>A workbook of the pages, each a worksheet: its formatted rows and cells written in the sheet's order.</summary>
    private static async Task<byte[]> Pages(JsonObject? options, JsonArray pages)
    {
        using var stream = new MemoryStream();
        await using (var writer = XlsxWriter.Create(stream, WriteOptions(options)))
        {
            foreach (var node in pages)
            {
                var page = node!.AsObject();
                Known(page, "name", "cells", "columns", "rows", "visibility");
                var columns = page["columns"]?.AsArray().Select(c => ColumnInput(c!.AsObject())).ToList();
                var visibility = page["visibility"] is { } v ? Enum.Parse<XlsxSheetVisibility>((string)v!, ignoreCase: true) : XlsxSheetVisibility.Visible;
                var sheet = writer.AddSheet(Text(Must(page, "name")), columns, visibility);
                var cells = (page["cells"]?.AsArray() ?? []).SelectMany(c => Cells(c!.AsObject())).ToLookup(c => c.Row);
                var rows = (page["rows"]?.AsArray() ?? []).ToDictionary(r => (int)r![0]!, r => new XlsxRow((int)r![0]!, (int)r[1]!));
                foreach (var row in cells.Select(g => g.Key).Union(rows.Keys).Order())
                {
                    if (rows.TryGetValue(row, out var formatted)) sheet.WriteRow(formatted);
                    foreach (var cell in cells[row].OrderBy(c => c.Column)) sheet.Write(cell);
                }
            }
            await writer.CompleteAsync(Token);
        }
        return stream.ToArray();
    }

    /// <summary>A cell as the .xlsx cases write one, or <c>{"run": "A3:GR3", …}</c>, the same cell in each place of a range.</summary>
    private static IEnumerable<XlsxCell> Cells(JsonObject cell)
    {
        if (cell["run"] is not { } run)
            return [CellInput(cell)];
        var range = CellRange.Parse((string)run!);
        var one = cell.DeepClone().AsObject();
        one.Remove("run");
        one["at"] = "A1";
        var input = CellInput(one);
        return range.Cells().Select(a => input with { Row = a.Row, Column = a.Column });
    }

    /// <summary>
    /// The file with each edit made in turn, each of which must change it: <c>{"part", "replace": [[old, new], …]}</c> replaces
    /// every occurrence, <c>{"part", "regex": [[pattern, replacement], …]}</c> every match, and <c>{"add": part, "text"}</c>
    /// adds a part, as another program would leave one.
    /// </summary>
    private static byte[] Edited(byte[] file, JsonArray edits)
    {
        var parts = new List<(string Name, string Text)>();
        using (var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                parts.Add((entry.FullName, reader.ReadToEnd()));
            }
        }
        foreach (var node in edits)
        {
            var edit = node!.AsObject();
            if (edit["add"] is { } added)
            {
                Known(edit, "add", "text");
                parts.Add((Text(added), Text(Must(edit, "text"))));
                continue;
            }
            Known(edit, "part", "replace", "regex");
            var name = (string)Must(edit, "part")!;
            var at = parts.FindIndex(p => p.Name == name);
            if (at < 0) throw new InvalidOperationException($"The file has no part '{name}' to edit.");
            var text = parts[at].Text;
            foreach (var pair in edit["replace"]?.AsArray() ?? [])
                text = text.Replace(Text(pair![0]!), Text(pair[1]!), StringComparison.Ordinal);
            foreach (var pair in edit["regex"]?.AsArray() ?? [])
                text = Regex.Replace(text, (string)pair![0]!, Text(pair[1]!));
            if (text == parts[at].Text) throw new InvalidOperationException($"The edit {edit.ToJsonString()} changes nothing.");
            parts[at] = (name, text);
        }
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in parts)
            {
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(text));
            }
        }
        return memory.ToArray();
    }

    // ---- save ---------------------------------------------------------------------------------------

    private static async Task<(JsonNode Answer, byte[]? Saved)> Save(JsonObject step, Workbook workbook, byte[]? saved)
    {
        var given = step["save"]?.AsObject() ?? [];
        Known(given, "maxUncompressedBytes", "maxCells");
        var options = new XlsxSaveOptions
        {
            MaxUncompressedBytes = given["maxUncompressedBytes"] is { } limit ? (long)limit : XlsxReadOptions.DefaultMaxUncompressedBytes,
            MaxCells = given["maxCells"] is { } cells ? (long)cells : XlsxReadOptions.DefaultMaxCells,
        };
        using var stream = new MemoryStream();
        try
        {
            await XlsxWorkbookWriter.WriteAsync(workbook, stream, options, Token);
        }
        catch (XlsxSaveException e)
        {
            Assert.Equal(0, stream.Length);
            return (new JsonObject
            {
                ["refused"] = new JsonObject
                {
                    ["reason"] = SheetCases.Camel(e.Reason),
                    ["sheet"] = e.Sheet,
                    ["cell"] = e.Cell,
                    ["held"] = e.Held,
                    ["message"] = e.Message,
                },
            }, saved);
        }
        catch (ArgumentOutOfRangeException e)
        {
            Assert.Equal(0, stream.Length);
            return (new JsonObject { ["outOfRange"] = Plain(e) }, saved);
        }
        var bytes = stream.ToArray();
        var all = PartsOf(bytes);
        var answer = new JsonObject { ["partNames"] = new JsonArray([.. all.Select(p => (JsonNode)(string)p![0]!)]) };
        if (step["parts"] is { } parts)
        {
            answer["parts"] = parts is JsonArray names
                ? new JsonArray([.. names.Select(n => all.Single(p => (string)p![0]! == (string)n!)!.DeepClone())])
                : (bool)parts ? all : throw new InvalidOperationException("'parts' is true or a list of parts.");
        }
        if (step["file"] is { } holds)
            answer["file"] = await Answer(bytes, Holds(holds), new XlsxReadOptions(), null);
        return (answer, bytes);
    }

    /// <summary>An argument exception's own message, without the parameter and the value .NET appends to it.</summary>
    private static string Plain(ArgumentOutOfRangeException e)
    {
        var message = e.ParamName is null ? e.Message : e.Message.Replace($" (Parameter '{e.ParamName}')", "", StringComparison.Ordinal);
        return e.ActualValue is { } actual ? message.Replace($"{Environment.NewLine}Actual value was {actual}.", "", StringComparison.Ordinal) : message;
    }

    // ---- sameAs -------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the Workbook shows what a kept one showed, Sheet by Sheet: each cell of the range in the same Cell Format and the
    /// same display, the range's columns at the same widths, and the last cell of the Sheet in the same Cell Format. With
    /// <c>"currencyWritten"</c>, the kept Workbook's built-in currency is taken as written: a code of the file's own spelling
    /// the Workbook's currency (ADR-0330 §4).
    /// </summary>
    private static void SameAs(JsonObject sameAs, Dictionary<string, Workbook> kept, Workbook after, string where, List<string> differences)
    {
        Known(sameAs, "kept", "range", "currencyWritten");
        var before = kept[(string)Must(sameAs, "kept")!];
        var range = CellRange.Parse((string)Must(sameAs, "range")!);
        var written = (bool?)sameAs["currencyWritten"] ?? false;
        if (before.Sheets.Count != after.Sheets.Count)
        {
            differences.Add($"{where}: {after.Sheets.Count} Sheets, not {before.Sheets.Count}.");
            return;
        }
        for (var s = 0; s < before.Sheets.Count; s++)
        {
            var (was, now) = (before.Sheets[s], after.Sheets[s]);
            foreach (var at in range.Cells().Append(new CellAddress(Sheet.RowCount - 1, Sheet.ColumnCount - 1)))
            {
                var format = was.GetCellFormat(at);
                if (written && string.Equals(format.NumberFormat.Code, NumberFormat.BuiltInCurrency(before.Culture).Code, StringComparison.OrdinalIgnoreCase))
                    format = format with { NumberFormat = NumberFormat.Parse(format.NumberFormat.LocalCode(before.Culture)) };
                Same($"{where}: {was.Name}!{at}'s Cell Format", SheetCases.CellFormatJson(format), SheetCases.CellFormatJson(now.GetCellFormat(at)), differences);
                Same($"{where}: {was.Name}!{at}'s display", Display(was.GetDisplay(at)), Display(now.GetDisplay(at)), differences);
            }
            for (var column = range.First.Column; column <= range.Last.Column; column++)
                Same($"{where}: {was.Name}'s column {column}'s width", Width(was.GetColumnWidth(column)), Width(now.GetColumnWidth(column)), differences);
        }
    }

    private static void Same(string what, JsonNode? before, JsonNode? after, List<string> differences)
    {
        if (!JsonNode.DeepEquals(before, after))
            differences.Add($"{what} was {before?.ToJsonString() ?? "null"}, and is {after?.ToJsonString() ?? "null"}.");
    }

    private static JsonObject Display(CellDisplay display) => new()
    {
        ["text"] = display.Text,
        ["cannotShow"] = display.CannotShow,
        ["alignment"] = SheetCases.Camel(display.Alignment),
        ["colour"] = display.Colour is { } colour ? SheetCases.Camel(colour) : null,
        ["isNumber"] = display.IsNumber,
    };

    private static JsonObject? Width(SheetColumnWidth? width) => width is { } w
        ? new JsonObject { ["width"] = w.Width, ["kind"] = SheetCases.Camel(w.Kind) }
        : null;

    /// <summary>
    /// The answer with each text that holds half of a surrogate pair written <c>{"utf16": …}</c>, each half as <c>\uXXXX</c>, as the
    /// cases write such text: JSON's writers here and in JavaScript cannot both carry it as a string.
    /// </summary>
    private static JsonNode? Escaped(JsonNode? answer) => answer switch
    {
        JsonObject o => new JsonObject([.. o.Select(p => KeyValuePair.Create(p.Key, Escaped(p.Value)))]),
        JsonArray a => new JsonArray([.. a.Select(Escaped)]),
        JsonValue v when v.GetValueKind() == JsonValueKind.String && HalfPair((string)v!) is { } escaped => new JsonObject { ["utf16"] = escaped },
        _ => answer?.DeepClone(),
    };

    /// <summary>The text with each half of a surrogate pair written <c>\uXXXX</c>, or null when it holds none.</summary>
    private static string? HalfPair(string text)
    {
        var escaped = new StringBuilder();
        var any = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                escaped.Append(text, i++, 2);
            }
            else if (char.IsSurrogate(text[i]))
            {
                escaped.Append(CultureInfo.InvariantCulture, $"\\u{(int)text[i]:X4}");
                any = true;
            }
            else
            {
                escaped.Append(text[i]);
            }
        }
        return any ? escaped.ToString() : null;
    }

    // ---- calls --------------------------------------------------------------------------------------

    /// <summary>What one of the internal pieces both cores share answers, as tests/cases/README.md names them.</summary>
    private static JsonNode? Call(JsonObject entry)
    {
        Known(entry, "name", "adr", "call", "argument", "expect", "note");
        var argument = entry["argument"];
        switch ((string)Must(entry, "call")!)
        {
            case "storedFormula":
            {
                var read = StoredFormula.Read((string)argument!, out var problem);
                return new JsonObject { ["read"] = read, ["problem"] = SheetCases.Camel(problem) };
            }
            case "writtenFormula":
            {
                var given = argument!.AsObject();
                Known(given, "formula", "sheets", "tables");
                Entry parsed;
                try
                {
                    parsed = Entry.Parse((string)Must(given, "formula")!, CultureInfo.InvariantCulture)!;
                }
                catch (FormulaSyntaxException e)
                {
                    return new JsonObject { ["refused"] = "formulaSyntax", ["beyondLimit"] = e.IsBeyondLimit };
                }
                var sheets = given["sheets"]?.AsArray().Select(s => (string)s!).ToList();
                var tables = given["tables"]?.AsObject();
                var text = WrittenFormula.Write(parsed, sheets is null ? null : n => sheets.Contains(n, StringComparer.OrdinalIgnoreCase), out var problem,
                    tables is null ? null : t => tables.FirstOrDefault(p => string.Equals(p.Key, t, StringComparison.OrdinalIgnoreCase)).Value is JsonArray columns
                        ? [.. columns.Select(c => Text(c!))]
                        : null);
                if (problem == WrittenFormulaProblem.None)
                {
                    // What is written never reads back as another Formula, whatever the case's answer says it is written as:
                    // it reads back as itself, or is refused by name (an Excel Table's reference, ADR-0330 §6, or a 3D Reference's Spill Range).
                    var readBack = StoredFormula.Read(text, out var refused);
                    if (refused == StoredFormulaProblem.None) Assert.Equal(parsed.Formula, readBack);
                }
                return new JsonObject { ["stored"] = problem == WrittenFormulaProblem.None ? text : null, ["problem"] = SheetCases.Camel(problem) };
            }
            case "isoDate":
                return IsoDate.Serial(Text(argument!)) is { } serial ? JsonValue.Create(serial) : null;
            case "codeShowsDate":
                return DateFormats.CodeShowsDate((string)argument!);
            case "builtInShowsDate":
                return DateFormats.ShowsDate((int)argument!, null);
            case "widthToFile":
                return ColumnWidthUnit.ToFile((double)argument!);
            case "widthFromFile":
                return ColumnWidthUnit.FromFile((double)argument!);
            case "builtInId":
                return XlsxNumberFormats.BuiltInId(NumberFormat.Parse((string)argument!)) is { } id ? JsonValue.Create(id) : null;
            case "fileLineStyle":
                return Name(StyleReading.FileStyle(SheetCases.Enum<BorderLineStyle>(argument!)));
            case var other:
                throw new InvalidOperationException($"'{other}' is not a call.");
        }
    }

    // ---- reading a case ---------------------------------------------------------------------------

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    /// <summary>Writes ExSheet.Xlsx's answers into the case of the source file.</summary>
    private static void Write(string file, string name, Action<JsonObject> write)
    {
        lock (WriteGate)
        {
            var path = Path.Combine(SourceRoot(), file);
            var document = Read(path);
            write(document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject());
            File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                IndentSize = 2,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
        }
    }

    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "dotnet", "ExGrid.slnx")))
                return Path.Combine(dir.FullName, "tests", "cases", "sheet-xlsx");
        }
        throw new InvalidOperationException("The repository holding dotnet/ExGrid.slnx is not above the test's directory.");
    }
}
