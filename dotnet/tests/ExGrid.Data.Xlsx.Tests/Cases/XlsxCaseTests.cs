using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using static ExGrid.Data.Xlsx.Tests.Fixtures;

namespace ExGrid.Data.Xlsx.Tests.Cases;

/// <summary>
/// The shared .xlsx cases (<c>tests/cases/xlsx</c>; ADR-0330, ADR-0370 section 2), run against ExGrid.Data.Xlsx: a file read
/// and what the reader gives or how it refused it, a workbook written step by step and what each step answered and the parts
/// it made, and the cell references, escapes and colours both read and write. A reader and a writer of .xlsx have no
/// differential run, so these cases are what holds <c>@ex-grid/data-xlsx</c> to this package; its runner is
/// <c>packages/data-xlsx/test/xlsx-cases.test.ts</c>, and <c>tests/cases/README.md</c> says what a file holds. With
/// <c>EXGRID_WRITE_XLSX_CASES=1</c>, the runner writes what ExGrid.Data.Xlsx answers into each case of the source files.
/// </summary>
public partial class XlsxCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "xlsx");

    private static bool Writing => Environment.GetEnvironmentVariable("EXGRID_WRITE_XLSX_CASES") == "1";

    private static readonly Lock WriteGate = new();

    private static readonly string[] Kinds = ["xlsxRead", "xlsxWrite", "xlsxReference", "xlsxEscape", "xlsxColor"];

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

    [Theory, MemberData(nameof(Cases))] // ADR-0330, ADR-0370 section 2: an .xlsx case holds on ExGrid.Data.Xlsx
    public async Task Xlsx(string file, string name)
    {
        var document = Read(Path.Combine(Root, file));
        var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        var answer = (string)document["kind"]! switch
        {
            "xlsxRead" => await ReadAnswer(entry),
            "xlsxWrite" => await WriteAnswer(entry),
            "xlsxReference" => ReferenceAnswer(entry),
            "xlsxEscape" => (JsonNode)XString.Decode(Text(Must(entry, "written")))!,
            "xlsxColor" => ColorAnswer(entry),
            var other => throw new InvalidOperationException($"'{other}' is not a kind."),
        };
        if (Writing)
        {
            Write(file, name, answer);
            return;
        }
        Assert.True(entry.TryGetPropertyValue("expect", out var expected), "The case has no answer: write it with EXGRID_WRITE_XLSX_CASES=1.");
        Assert.True(JsonNode.DeepEquals(expected, answer), $"Expected {expected?.ToJsonString() ?? "null"}, answered {answer?.ToJsonString() ?? "null"}.");
    }

    // ---- xlsxRead -----------------------------------------------------------------------------------

    private static async Task<JsonNode> ReadAnswer(JsonObject entry)
    {
        var given = entry["options"]?.AsObject() ?? [];
        Known(given, "maxUncompressedBytes", "maxCells");
        var options = new XlsxReadOptions
        {
            MaxUncompressedBytes = given["maxUncompressedBytes"] is { } bytes ? (long)bytes : XlsxReadOptions.DefaultMaxUncompressedBytes,
            MaxCells = given["maxCells"] is { } cells ? (long)cells : XlsxReadOptions.DefaultMaxCells,
        };
        return await Answer(FileOf(Must(entry, "file").AsObject()), Holds(entry["holds"]), options, (string?)entry["saying"]);
    }

    // ---- xlsxWrite ----------------------------------------------------------------------------------

    private static async Task<JsonNode> WriteAnswer(JsonObject entry)
    {
        var output = new MemoryStream();
        XlsxWriter writer;
        try
        {
            writer = XlsxWriter.Create(output, WriteOptions(entry["options"]?.AsObject()));
        }
        catch (Exception thrown) when (Thrown(thrown) is { } shown)
        {
            return new JsonObject { ["create"] = shown };
        }
        var outcomes = new JsonArray();
        var completed = false;
        await using (writer)
        {
            foreach (var step in Must(entry, "steps").AsArray())
            {
                try
                {
                    completed |= await Step(writer, step!.AsObject());
                    outcomes.Add(null);
                }
                catch (Exception thrown) when (Thrown(thrown) is { } shown)
                {
                    outcomes.Add(shown);
                }
            }
        }
        var answer = new JsonObject { ["create"] = null, ["outcomes"] = outcomes };
        if (entry["parts"] is { } parts && (bool)parts)
            answer["parts"] = completed ? PartsOf(output.ToArray()) : null;
        if (entry["read"] is { } read)
            answer["read"] = completed ? await Answer(output.ToArray(), Holds(read), new XlsxReadOptions(), null) : null;
        return answer;
    }

    /// <summary>How a step threw, as tests/cases/README.md names it; null for what no case expects, which then fails the case.</summary>
    private static JsonObject? Thrown(Exception thrown) => thrown switch
    {
        XlsxException refusal => new JsonObject
        {
            ["kind"] = "refused",
            ["reason"] = Name(refusal.Reason),
            ["part"] = refusal.Part,
            ["sheet"] = refusal.Sheet,
            ["message"] = refusal.Message,
        },
        ArgumentOutOfRangeException e => new JsonObject { ["kind"] = "outOfRange", ["message"] = Plain(e) },
        ArgumentException e => new JsonObject { ["kind"] = "argument", ["message"] = Plain(e) },
        ObjectDisposedException => new JsonObject { ["kind"] = "invalidOperation", ["message"] = null },
        InvalidOperationException e => new JsonObject { ["kind"] = "invalidOperation", ["message"] = e.Message },
        _ => null,
    };

    /// <summary>An argument exception's own message, without the parameter and the value .NET appends to it.</summary>
    private static string Plain(ArgumentException e)
    {
        var message = e.ParamName is null ? e.Message : e.Message.Replace($" (Parameter '{e.ParamName}')", "", StringComparison.Ordinal);
        return e is ArgumentOutOfRangeException { ActualValue: { } actual }
            ? message.Replace($"{Environment.NewLine}Actual value was {actual}.", "", StringComparison.Ordinal)
            : message;
    }

    // ---- xlsxReference, xlsxColor -----------------------------------------------------------------

    private static JsonNode ReferenceAnswer(JsonObject entry)
    {
        if (entry["place"] is JsonArray place)
            return XlsxCellReference.Format((int)place[0]!, (int)place[1]!);
        var text = Text(Must(entry, "text"));
        return XlsxCellReference.TryParse(text, out var row, out var column) ? new JsonArray(row, column) : JsonValue.Create(false);
    }

    private static JsonNode? ColorAnswer(JsonObject entry)
    {
        if (entry["tint"] is { } tint && entry["argb"] is { } argb)
            return Hex(XlsxTheme.ApplyTint(ParseHex((string)argb!), (double)tint));
        var theme = entry["theme"] is JsonArray colors ? new XlsxTheme([.. colors.Select(c => ParseHex((string)c!))]) : XlsxTheme.Office;
        IReadOnlyList<uint>? indexed = entry["indexedColors"] is JsonArray given ? [.. given.Select(c => ParseHex((string)c!))] : null;
        var color = ColorInput(Must(entry, "color")) ?? throw new InvalidOperationException("The case names no colour.");
        return theme.Resolve(color, indexed) is { } resolved ? Hex(resolved) : null;
    }

    // ---- the file a read case reads ---------------------------------------------------------------

    // ---- reading a case ---------------------------------------------------------------------------

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    /// <summary>Writes ExGrid.Data.Xlsx's answer into the case of the source file, under its <c>expect</c>.</summary>
    private static void Write(string file, string name, JsonNode? answer)
    {
        lock (WriteGate)
        {
            var path = Path.Combine(SourceRoot(), file);
            var document = Read(path);
            var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
            entry["expect"] = answer?.DeepClone();
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
                return Path.Combine(dir.FullName, "tests", "cases", "xlsx");
        }
        throw new InvalidOperationException("The repository holding dotnet/ExGrid.slnx is not above the test's directory.");
    }
}
