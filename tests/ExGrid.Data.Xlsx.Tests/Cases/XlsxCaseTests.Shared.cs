using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests.Cases;

/// <summary>
/// What the shared .xlsx cases write — a file, a workbook written step by step, text and colours — read into ExGrid.Data.Xlsx's
/// own types, and what the reader gives written as a case expects it. ExSheet.Xlsx's case runner compiles this file too, so
/// that its cases write a file, and read one back, as these do.
/// </summary>
public partial class XlsxCaseTests
{
    internal static readonly string[] AllAspects = ["workbook", "sharedStrings", "styles", "theme", "sheets", "batches"];

    /// <summary>Takes one step of a write case; true when the step completed the workbook.</summary>
    internal static async Task<bool> Step(XlsxWriter writer, JsonObject step)
    {
        var (verb, argument) = step.Single(p => p.Key is not ("columns" or "visibility"));
        switch (verb)
        {
            case "addSheet":
                var columns = step["columns"]?.AsArray().Select(c => ColumnInput(c!.AsObject())).ToList();
                var visibility = step["visibility"] is { } v ? Enum.Parse<XlsxSheetVisibility>((string)v!, ignoreCase: true) : XlsxSheetVisibility.Visible;
                LastSheet.AddOrUpdate(writer, writer.AddSheet(Text(argument!), columns, visibility));
                return false;
            case "write":
                Last(writer).Write(CellInput(argument!.AsObject()));
                return false;
            case "writeAll":
                await Last(writer).WriteAsync([.. argument!.AsArray().Select(c => CellInput(c!.AsObject()))], Token);
                return false;
            case "writeRow":
                var row = argument!.AsArray();
                Last(writer).WriteRow(new XlsxRow((int)row[0]!, (int)row[1]!));
                return false;
            case "addTable":
                Last(writer).AddTable(TableInput(argument!.AsObject()));
                return false;
            case "flush":
                await Last(writer).FlushAsync(Token);
                return false;
            case "complete":
                await writer.CompleteAsync(Token);
                return true;
            default:
                throw new InvalidOperationException($"'{verb}' is not a write step.");
        }
    }

    internal static XlsxSheetWriter Last(XlsxWriter writer)
        => LastSheet.TryGetValue(writer, out var sheet) ? sheet : throw new InvalidOperationException("The case writes before it adds a sheet.");

    // The sheet each writer added last: a step writes to it, as the cases say.
    internal static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XlsxWriter, XlsxSheetWriter> LastSheet = [];

    internal static JsonArray PartsOf(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        return new JsonArray([.. zip.Entries.Select(e =>
        {
            using var reader = new StreamReader(e.Open(), Encoding.UTF8);
            return (JsonNode)new JsonArray(e.FullName, reader.ReadToEnd().ReplaceLineEndings("\n"));
        })]);
    }

    internal static XlsxWriteOptions WriteOptions(JsonObject? options)
    {
        var given = options ?? [];
        Known(given, "styles", "activeSheet", "date1904", "fullCalculationOnLoad", "definedNames", "maxUncompressedBytes");
        return new XlsxWriteOptions
        {
            Styles = given["styles"] is JsonObject styles ? StylesInput(styles) : null,
            ActiveSheet = (int?)given["activeSheet"] ?? 0,
            Date1904 = (bool?)given["date1904"] ?? false,
            FullCalculationOnLoad = (bool?)given["fullCalculationOnLoad"] ?? false,
            DefinedNames = given["definedNames"] is JsonArray names
                ? [.. names.Select(n => new XlsxDefinedName(
                    Text(Must(n!.AsObject(), "name")), Text(Must(n.AsObject(), "text")), (int?)n["localSheet"], (bool?)n["hidden"] ?? false))]
                : null,
            MaxUncompressedBytes = given["maxUncompressedBytes"] is { } max ? (long)max : XlsxReadOptions.DefaultMaxUncompressedBytes,
        };
    }

    /// <summary>Fails a case whose object names a key the cases do not.</summary>
    internal static void Known(JsonObject given, params string[] keys)
    {
        if (given.FirstOrDefault(p => !keys.Contains(p.Key)) is { Key: { } unknown })
            throw new InvalidOperationException($"'{unknown}' is not a key the cases name here.");
    }

    internal static XlsxCell CellInput(JsonObject cell)
    {
        Assert.True(XlsxCellReference.TryParse((string)Must(cell, "at")!, out var row, out var column));
        foreach (var key in cell.Select(p => p.Key))
        {
            if (key is not ("at" or "type" or "value" or "text" or "formula" or "style"))
                throw new InvalidOperationException($"'{key}' is not a key of a cell.");
        }
        return new XlsxCell(row, column, Enum.Parse<XlsxCellType>((string)Must(cell, "type")!, ignoreCase: true),
            OptionalText(cell["value"]), OptionalText(cell["text"]),
            cell["formula"] is JsonObject formula ? FormulaInput(formula) : null,
            (int?)cell["style"] ?? 0);
    }

    internal static XlsxFormula FormulaInput(JsonObject formula) => new(
        Text(Must(formula, "text")),
        formula["kind"] is { } kind ? Enum.Parse<XlsxFormulaKind>((string)kind!, ignoreCase: true) : XlsxFormulaKind.Normal,
        OptionalText(formula["ref"]),
        (int?)formula["si"],
        (bool?)formula["dynamic"] ?? false);

    internal static XlsxColumn ColumnInput(JsonObject column) => new(
        (int)Must(column, "first"), (int)Must(column, "last"), (double?)column["width"],
        (bool?)column["hidden"] ?? false, (int?)column["style"] ?? 0, (bool?)column["bestFit"] ?? false, (bool?)column["customWidth"] ?? true);

    internal static XlsxTable TableInput(JsonObject table) => new(
        Text(Must(table, "name")), (int)Must(table, "headerRow"), (int)Must(table, "firstColumn"),
        [.. Must(table, "columns").AsArray().Select(c => Text(c!))], (int)Must(table, "dataRows"))
    {
        AutoFilter = (bool?)table["autoFilter"] ?? true,
    };

    /// <summary>Styles as a case writes them, a list it leaves out taken from <see cref="XlsxStyles.Default"/>.</summary>
    internal static XlsxStyles StylesInput(JsonObject styles)
    {
        var result = XlsxStyles.Default;
        foreach (var (key, value) in styles)
        {
            result = key switch
            {
                "numberFormats" => result with
                {
                    NumberFormats = value!.AsObject().ToDictionary(p => int.Parse(p.Key, CultureInfo.InvariantCulture), p => Text(p.Value!)),
                },
                "fonts" => result with
                {
                    Fonts = [.. value!.AsArray().Select(f => new XlsxFont(
                        OptionalText(f!["name"]), (double?)f["size"], (bool?)f["bold"] ?? false, (bool?)f["italic"] ?? false,
                        f["underline"] is { } u ? Enum.Parse<XlsxUnderline>((string)u!, ignoreCase: true) : XlsxUnderline.None,
                        (bool?)f["strike"] ?? false, ColorInput(f["color"])))],
                },
                "fills" => result with
                {
                    Fills = [.. value!.AsArray().Select(f => new XlsxFill(
                        (string?)f!["pattern"], ColorInput(f["foreground"]), ColorInput(f["background"]), (bool?)f["gradient"] ?? false))],
                },
                "borders" => result with
                {
                    Borders = [.. value!.AsArray().Select(b => new XlsxBorder(
                        EdgeInput(Must(b!.AsObject(), "left")), EdgeInput(Must(b.AsObject(), "right")), EdgeInput(Must(b.AsObject(), "top")),
                        EdgeInput(Must(b.AsObject(), "bottom")), EdgeInput(Must(b.AsObject(), "diagonal")),
                        (bool?)b["diagonalUp"] ?? false, (bool?)b["diagonalDown"] ?? false))],
                },
                "cellFormats" => result with
                {
                    CellFormats = [.. value!.AsArray().Select(x => new XlsxCellFormat(
                        (int?)x!["numberFormat"] ?? 0, (int?)x["font"] ?? 0, (int?)x["fill"] ?? 0, (int?)x["border"] ?? 0,
                        x["alignment"] is JsonObject a ? new XlsxAlignment(
                            a["horizontal"] is { } h ? Enum.Parse<XlsxHorizontalAlignment>((string)h!, ignoreCase: true) : XlsxHorizontalAlignment.General,
                            a["vertical"] is { } v ? Enum.Parse<XlsxVerticalAlignment>((string)v!, ignoreCase: true) : XlsxVerticalAlignment.Bottom,
                            (bool?)a["wrapText"] ?? false, (int?)a["indent"] ?? 0, (int?)a["textRotation"] ?? 0, (bool?)a["shrinkToFit"] ?? false) : null))],
                },
                "indexedColors" => result with { IndexedColors = value is null ? null : [.. value.AsArray().Select(c => ParseHex((string)c!))] },
                _ => throw new InvalidOperationException($"'{key}' is not a key of styles."),
            };
        }
        return result;
    }

    internal static XlsxBorderEdge EdgeInput(JsonNode edge) => new(
        Enum.Parse<XlsxBorderStyle>((string)Must(edge.AsObject(), "style")!, ignoreCase: true), ColorInput(edge["color"]));

    internal static XlsxColor? ColorInput(JsonNode? color)
    {
        if (color is null)
            return null;
        var kind = Enum.Parse<XlsxColorKind>((string)Must(color.AsObject(), "kind")!, ignoreCase: true);
        var tint = (double?)color["tint"] ?? 0;
        return kind switch
        {
            XlsxColorKind.Automatic => XlsxColor.Automatic,
            XlsxColorKind.Rgb => new XlsxColor(kind, ParseHex((string)Must(color.AsObject(), "value")!), tint),
            _ => new XlsxColor(kind, (uint)Must(color.AsObject(), "value"), tint),
        };
    }

    /// <summary>The file a read case reads, as tests/cases/README.md writes one.</summary>
    internal static byte[] FileOf(JsonObject file)
    {
        byte[] bytes;
        if (file["bytes"] is { } pieces)
            bytes = Bytes(pieces);
        else if (file["ole"] is { } names)
            bytes = Ole([.. names.AsArray().Select(n => Text(n!))]);
        else if (file["zip"] is { } parts)
            bytes = Zip([.. parts.AsArray().Select(p => (Text(p![0]!), Text(p[1]!)))]);
        else if (file["book"] is JsonObject book)
            bytes = BookOf(book);
        else
            throw new InvalidOperationException($"{file.ToJsonString()} is not a file.");
        if (file["stored"] is { } stored && (bool)stored)
            bytes = Stored(bytes);
        if (file["damage"] is { } damaged)
            bytes = Damage(bytes, (string)damaged!);
        if (file["lie"] is JsonObject lie)
        {
            var part = (string)Must(lie, "part")!;
            Lie(bytes, part, Inflated(bytes, part), (int)Must(lie, "declared"));
        }
        return bytes;
    }

    internal static int Inflated(byte[] zip, string part)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return checked((int)(archive.GetEntry(part) ?? throw new InvalidOperationException($"The zip has no part '{part}'.")).Length);
    }

    /// <summary>A workbook of one worksheet, laid out as <see cref="Fixtures.Book"/> lays it out.</summary>
    internal static byte[] BookOf(JsonObject book)
    {
        foreach (var key in book.Select(p => p.Key))
        {
            if (key is not ("sheet" or "sheetData" or "head" or "tail" or "sharedStrings" or "styles" or "theme" or "metadata" or "workbook"))
                throw new InvalidOperationException($"'{key}' is not a key of a book.");
        }
        var sheet = book["sheet"] is { } whole
            ? Text(whole)
            : Sheet(Text(Must(book, "sheetData")), OptionalText(book["head"]) ?? "", OptionalText(book["tail"]) ?? "");
        return Book(sheet, OptionalText(book["sharedStrings"]), OptionalText(book["styles"]), OptionalText(book["theme"]),
            OptionalText(book["metadata"]), OptionalText(book["workbook"]));
    }

    /// <summary>Text as a case writes it: a string, <c>{"utf16": …}</c> with <c>\uXXXX</c> escapes for text JSON cannot hold, or a
    /// list of pieces, each one of those or <c>{"each": …, "from": m, "to": n}</c>, text written once for each number from
    /// <c>m</c> to <c>n</c> with <c>{i}</c> standing for it. <c>{Main}</c> and <c>{Rel}</c> stand for SpreadsheetML's two
    /// namespaces.</summary>
    internal static string Text(JsonNode node)
    {
        var text = node switch
        {
            JsonValue value => (string)value!,
            JsonObject { Count: 1 } utf16 when utf16["utf16"] is { } escaped => Utf16((string)escaped!),
            JsonArray pieces => string.Concat(pieces.Select(Piece)),
            _ => throw new InvalidOperationException($"{node.ToJsonString()} is not text."),
        };
        return text.Replace("{Main}", Main, StringComparison.Ordinal).Replace("{Rel}", Rel, StringComparison.Ordinal);
    }

    internal static string Piece(JsonNode? piece)
    {
        if (piece is JsonObject each && each["each"] is { } template)
        {
            var once = (string)template!;
            var builder = new StringBuilder();
            for (var i = (int)Must(each, "from"); i <= (int)Must(each, "to"); i++)
                builder.Append(once.Replace("{i}", i.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
            return builder.ToString();
        }
        return Text(piece!);
    }

    internal static string? OptionalText(JsonNode? node) => node is null ? null : Text(node);

    internal static string Utf16(string escaped)
        => System.Text.RegularExpressions.Regex.Replace(escaped, @"\\u([0-9a-fA-F]{4})",
            m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());

    /// <summary>The bytes of a file that is not a zip, as the CSV cases write them: UTF-8 text, <c>{"hex": …}</c> or
    /// <c>{"repeat": …, "times": n}</c>.</summary>
    internal static byte[] Bytes(JsonNode file)
    {
        var bytes = new List<byte>();
        foreach (var piece in file.AsArray())
        {
            switch (piece)
            {
                case JsonValue text:
                    bytes.AddRange(Encoding.UTF8.GetBytes((string)text!));
                    break;
                case JsonObject hex when hex["hex"] is { } written:
                    bytes.AddRange(Convert.FromHexString(((string)written!).Replace(" ", "", StringComparison.Ordinal)));
                    break;
                case JsonObject repeat:
                    var once = Encoding.UTF8.GetBytes((string)Must(repeat, "repeat")!);
                    for (var i = 0; i < (int)Must(repeat, "times"); i++)
                        bytes.AddRange(once);
                    break;
                default:
                    throw new InvalidOperationException($"{piece?.ToJsonString()} is not a piece of a file.");
            }
        }
        return [.. bytes];
    }

    internal static string Name<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    internal static string Hex(uint argb) => argb.ToString("X8", CultureInfo.InvariantCulture);

    internal static uint ParseHex(string hex) => uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    internal static JsonNode Must(JsonObject node, string key)
        => node[key] ?? throw new InvalidOperationException($"The case names no '{key}'.");

    /// <summary>The aspects of a read a case holds, <c>sheets</c> when it names none.</summary>
    internal static List<string> Holds(JsonNode? listed)
    {
        List<string> holds = listed is null ? ["sheets"] : [.. listed.AsArray().Select(a => (string)a!)];
        if (holds.FirstOrDefault(a => !AllAspects.Contains(a)) is { } unknown)
            throw new InvalidOperationException($"'{unknown}' is not an aspect of a read.");
        return holds;
    }

    /// <summary>What reading <paramref name="bytes"/> gives, in the aspects <paramref name="holds"/> names, or how it was refused.</summary>
    internal static async Task<JsonNode> Answer(byte[] bytes, List<string> holds, XlsxReadOptions options, string? saying)
    {
        try
        {
            using var reader = await XlsxReader.OpenAsync(new MemoryStream(bytes, writable: false), options, Token);
            var answer = new JsonObject();
            if (holds.Contains("workbook"))
            {
                answer["workbook"] = new JsonObject
                {
                    ["part"] = reader.WorkbookPartName,
                    ["sheets"] = new JsonArray([.. reader.Sheets.Select(s => (JsonNode)new JsonObject
                    {
                        ["name"] = s.Name,
                        ["sheetId"] = s.SheetId,
                        ["visibility"] = Name(s.Visibility),
                        ["part"] = s.Part,
                        ["kind"] = Name(s.Kind),
                    })]),
                    ["activeSheet"] = reader.ActiveSheet,
                    ["date1904"] = reader.Date1904,
                    ["definedNames"] = new JsonArray([.. reader.DefinedNames.Select(DefinedName)]),
                    ["hasTheme"] = reader.HasTheme,
                    ["parts"] = new JsonArray([.. reader.Parts.Order(StringComparer.Ordinal).Select(p => (JsonNode)p)]),
                };
            }
            if (holds.Contains("sharedStrings"))
            {
                answer["sharedStrings"] = new JsonArray([.. Enumerable.Range(0, reader.SharedStrings.Count)
                    .Select(i => (JsonNode)new JsonArray(reader.SharedStrings[i], reader.SharedStrings.IsRich(i)))]);
            }
            if (holds.Contains("styles"))
                answer["styles"] = StylesAnswer(reader.Styles, reader.Theme);
            if (holds.Contains("theme"))
                answer["theme"] = new JsonArray([.. reader.Theme.SchemeColors.Select(c => (JsonNode)Hex(c))]);
            if (holds.Contains("sheets") || holds.Contains("batches"))
            {
                var sheets = new JsonArray();
                var everyBatch = new JsonArray();
                for (var i = 0; i < reader.Sheets.Count; i++)
                {
                    if (reader.Sheets[i].Part is null)
                    {
                        sheets.Add(null);
                        everyBatch.Add(null);
                        continue;
                    }
                    using var sheet = reader.OpenSheet(i);
                    var cells = new JsonArray();
                    var rows = new JsonArray();
                    var batches = new JsonArray();
                    while (await sheet.ReadAsync())
                    {
                        batches.Add(new JsonArray(sheet.Cells.Count, sheet.Rows.Count));
                        foreach (var cell in sheet.Cells)
                            cells.Add(CellAnswer(cell));
                        foreach (var row in sheet.Rows)
                            rows.Add(new JsonArray(row.Row, row.StyleIndex));
                    }
                    var shown = new JsonObject
                    {
                        ["columns"] = new JsonArray([.. sheet.Columns.Select(ColumnAnswer)]),
                        ["rows"] = rows,
                        ["cells"] = cells,
                        ["otherElements"] = new JsonArray([.. sheet.OtherElements.Select(ElementAnswer)]),
                        ["customRowHeights"] = sheet.CustomRowHeights,
                        ["hiddenRows"] = sheet.HiddenRows,
                        ["frozenPanes"] = sheet.FrozenPanes,
                    };
                    sheets.Add(shown);
                    everyBatch.Add(batches);
                }
                if (holds.Contains("sheets"))
                    answer["sheets"] = sheets;
                if (holds.Contains("batches"))
                    answer["batches"] = everyBatch;
            }
            if (saying is not null)
                throw new InvalidOperationException($"The case expects a refusal saying \"{saying}\", and the file was read.");
            return answer;
        }
        catch (XlsxException refusal)
        {
            var message = refusal.Message;
            if (saying is not null)
            {
                Assert.StartsWith(saying, message, StringComparison.Ordinal);
                message = saying;
            }
            return new JsonObject
            {
                ["refused"] = new JsonObject
                {
                    ["reason"] = Name(refusal.Reason),
                    ["part"] = refusal.Part,
                    ["sheet"] = refusal.Sheet,
                    ["cell"] = refusal.Cell,
                    ["message"] = message,
                },
            };
        }
    }

    internal static JsonNode CellAnswer(XlsxCell cell)
    {
        var shown = new JsonObject
        {
            ["at"] = cell.Reference,
            ["type"] = Name(cell.Type),
            ["value"] = cell.Value,
        };
        if (cell.Text is not null)
            shown["text"] = cell.Text;
        if (cell.Formula is { } formula)
            shown["formula"] = FormulaAnswer(formula);
        if (cell.StyleIndex != 0)
            shown["style"] = cell.StyleIndex;
        if (cell.IsRichText)
            shown["rich"] = true;
        return shown;
    }

    internal static JsonNode FormulaAnswer(XlsxFormula formula)
    {
        var shown = new JsonObject { ["text"] = formula.Text };
        if (formula.Kind != XlsxFormulaKind.Normal)
            shown["kind"] = Name(formula.Kind);
        if (formula.Reference is not null)
            shown["ref"] = formula.Reference;
        if (formula.SharedIndex is { } index)
            shown["si"] = index;
        if (formula.IsDynamicArray)
            shown["dynamic"] = true;
        return shown;
    }

    internal static JsonNode ColumnAnswer(XlsxColumn column) => new JsonObject
    {
        ["first"] = column.First,
        ["last"] = column.Last,
        ["width"] = column.Width,
        ["hidden"] = column.Hidden,
        ["style"] = column.StyleIndex,
        ["bestFit"] = column.BestFit,
        ["customWidth"] = column.CustomWidth,
    };

    internal static JsonNode ElementAnswer(XlsxElement element)
    {
        var shown = new JsonObject { ["name"] = element.Name };
        if (element.Attributes.Count > 0)
            shown["attributes"] = new JsonObject(element.Attributes.Select(a => KeyValuePair.Create(a.Key, (JsonNode?)a.Value)));
        if (element.Children.Count > 0)
            shown["children"] = new JsonArray([.. element.Children.Select(ElementAnswer)]);
        if (element.Text is not null)
            shown["text"] = element.Text;
        if (element.ChildrenCut)
            shown["cut"] = true;
        return shown;
    }

    internal static JsonNode DefinedName(XlsxDefinedName name) => new JsonObject
    {
        ["name"] = name.Name,
        ["text"] = name.Text,
        ["localSheet"] = name.LocalSheet,
        ["hidden"] = name.Hidden,
    };

    internal static JsonNode StylesAnswer(XlsxStyles styles, XlsxTheme theme)
    {
        JsonNode? Color(XlsxColor? color)
        {
            if (color is not { } c)
                return null;
            var shown = new JsonObject { ["kind"] = Name(c.Kind) };
            if (c.Kind == XlsxColorKind.Rgb)
                shown["value"] = Hex(c.Value);
            else if (c.Kind != XlsxColorKind.Automatic)
                shown["value"] = c.Value;
            if (c.Tint != 0)
                shown["tint"] = c.Tint;
            shown["resolved"] = theme.Resolve(c, styles.IndexedColors) is { } argb ? Hex(argb) : null;
            return shown;
        }
        JsonNode Edge(XlsxBorderEdge edge)
        {
            var shown = new JsonObject { ["style"] = Name(edge.Style) };
            if (edge.Color is not null)
                shown["color"] = Color(edge.Color);
            return shown;
        }
        return new JsonObject
        {
            ["numberFormats"] = new JsonObject(styles.NumberFormats.OrderBy(p => p.Key)
                .Select(p => KeyValuePair.Create(p.Key.ToString(CultureInfo.InvariantCulture), (JsonNode?)p.Value))),
            ["fonts"] = new JsonArray([.. styles.Fonts.Select(font =>
            {
                var shown = new JsonObject();
                if (font.Name is not null)
                    shown["name"] = font.Name;
                if (font.Size is { } size)
                    shown["size"] = size;
                if (font.Bold)
                    shown["bold"] = true;
                if (font.Italic)
                    shown["italic"] = true;
                if (font.Underline != XlsxUnderline.None)
                    shown["underline"] = Name(font.Underline);
                if (font.Strike)
                    shown["strike"] = true;
                if (font.Color is not null)
                    shown["color"] = Color(font.Color);
                return (JsonNode)shown;
            })]),
            ["fills"] = new JsonArray([.. styles.Fills.Select(fill =>
            {
                var shown = new JsonObject { ["pattern"] = fill.Pattern };
                if (fill.Foreground is not null)
                    shown["foreground"] = Color(fill.Foreground);
                if (fill.Background is not null)
                    shown["background"] = Color(fill.Background);
                if (fill.IsGradient)
                    shown["gradient"] = true;
                return (JsonNode)shown;
            })]),
            ["borders"] = new JsonArray([.. styles.Borders.Select(border =>
            {
                var shown = new JsonObject
                {
                    ["left"] = Edge(border.Left),
                    ["right"] = Edge(border.Right),
                    ["top"] = Edge(border.Top),
                    ["bottom"] = Edge(border.Bottom),
                    ["diagonal"] = Edge(border.Diagonal),
                };
                if (border.DiagonalUp)
                    shown["diagonalUp"] = true;
                if (border.DiagonalDown)
                    shown["diagonalDown"] = true;
                return (JsonNode)shown;
            })]),
            ["cellFormats"] = new JsonArray([.. styles.CellFormats.Select(format =>
            {
                var shown = new JsonObject
                {
                    ["numberFormat"] = format.NumberFormatId,
                    ["font"] = format.FontId,
                    ["fill"] = format.FillId,
                    ["border"] = format.BorderId,
                };
                if (format.Alignment is { } alignment)
                {
                    var a = new JsonObject();
                    if (alignment.Horizontal != XlsxHorizontalAlignment.General)
                        a["horizontal"] = Name(alignment.Horizontal);
                    if (alignment.Vertical != XlsxVerticalAlignment.Bottom)
                        a["vertical"] = Name(alignment.Vertical);
                    if (alignment.WrapText)
                        a["wrapText"] = true;
                    if (alignment.Indent != 0)
                        a["indent"] = alignment.Indent;
                    if (alignment.TextRotation != 0)
                        a["textRotation"] = alignment.TextRotation;
                    if (alignment.ShrinkToFit)
                        a["shrinkToFit"] = true;
                    shown["alignment"] = a;
                }
                return (JsonNode)shown;
            })]),
            ["indexedColors"] = styles.IndexedColors is { } indexed ? new JsonArray([.. indexed.Select(c => (JsonNode)Hex(c))]) : null,
        };
    }
}
