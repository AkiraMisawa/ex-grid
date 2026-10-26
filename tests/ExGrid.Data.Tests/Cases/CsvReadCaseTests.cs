using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExGrid.Data.Csv;
using Xunit;

namespace ExGrid.Data.Tests.Cases;

/// <summary>
/// The shared CSV read cases (<c>tests/cases/data</c>; ADR-0064, ADR-0370 section 3), run against ExGrid.Data:
/// each case's file is read under its Schema, and the Snapshot it made, or how it was refused, is held to the
/// case. The Script Core's runner is <c>src/npm/data/test/csv-read-cases.test.ts</c>; <c>tests/cases/README.md</c>
/// says what a file holds.
///
/// <para>Each file under a mebibyte is read twice — through the default buffer, and through a buffer of sixteen bytes filled a
/// few bytes a read, so that every record is cut across reads — and both must give the same answer: how the
/// bytes arrive is ExGrid.Data's own, and the cases hold only what a reader sees. With
/// <c>EXGRID_WRITE_DATA_CASES=1</c>, the runner writes what ExGrid.Data answers into each case of the source
/// files instead.</para>
/// </summary>
public class CsvReadCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "data");

    private static bool Writing => Environment.GetEnvironmentVariable("EXGRID_WRITE_DATA_CASES") == "1";

    private static readonly Lock WriteGate = new();

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Read(path);
            if ((string?)document["kind"] != "csvRead")
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0064: a CSV read case holds on ExGrid.Data, through any buffer
    public void Csv_read(string file, string name)
    {
        var entry = Read(Path.Combine(Root, file))["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        var bytes = Bytes(Must(entry, "file"));
        var answer = Answer(entry, bytes, CsvLoad.DefaultBufferSize, int.MaxValue);
        // A file of 64 MiB is read once: through sixteen bytes at a time it would take minutes.
        if (bytes.Length < 1 << 20)
            Assert.Equal(answer.ToJsonString(), Answer(entry, bytes, 16, 5).ToJsonString());
        if (Writing)
        {
            Write(file, name, answer);
            return;
        }
        var expected = Must(entry, "expect");
        Assert.True(JsonNode.DeepEquals(expected, answer), $"Expected {expected.ToJsonString()}, answered {answer.ToJsonString()}.");
    }

    /// <summary>The Snapshot the read made, or how it was refused.</summary>
    private static JsonNode Answer(JsonObject entry, byte[] bytes, int bufferSize, int chunk)
    {
        Snapshot snapshot;
        try
        {
            var schema = Schema(Must(entry, "schema").AsObject());
            snapshot = CsvLoad.ReadAsync(schema, new CsvFixtures.Trickle(bytes, chunk), null, TestContext.Current.CancellationToken, bufferSize)
                .AsTask().GetAwaiter().GetResult();
        }
        catch (SnapshotException refusal)
        {
            return new JsonObject
            {
                ["refused"] = new JsonObject
                {
                    ["message"] = refusal.Message,
                    ["row"] = refusal.Row,
                    ["column"] = refusal.Column,
                },
            };
        }
        catch (ArgumentException)
        {
            return new JsonObject { ["refused"] = "argument" };
        }
        return new JsonObject
        {
            ["rows"] = snapshot.RowCount,
            ["recordKey"] = snapshot.RecordKey?.Name,
            ["columns"] = new JsonArray([.. snapshot.Columns.Select(column =>
            {
                var shown = new JsonObject
                {
                    ["name"] = column.Name,
                    ["caption"] = column.Caption,
                    ["kind"] = column.Kind.ToString().ToLowerInvariant(),
                    ["values"] = new JsonArray([.. snapshot.Rows.Select(row => Value(snapshot.ValueAt(row, column)))]),
                };
                if (column is TextColumn text)
                    shown["dictionary"] = new JsonArray([.. text.Dictionary.Select(t => (JsonNode)t)]);
                return (JsonNode)shown;
            })]),
        };
    }

    // ---- reading a case ---------------------------------------------------------------------------

    /// <summary>The case's Schema, as the TypeScript `CsvSchema` writes one: a key left out is left out.</summary>
    private static CsvSchema Schema(JsonObject written)
    {
        var columns = Must(written, "columns").AsArray().Select(node =>
        {
            var column = node!.AsObject();
            var kind = Enum.Parse<SnapshotKind>((string)Must(column, "kind")!, ignoreCase: true);
            return new CsvColumn((string)Must(column, "name")!, kind)
            {
                Header = (string?)column["header"],
                Position = (int?)column["position"],
                Caption = (string?)column["caption"],
                Culture = column["culture"] is { } culture ? CultureInfo.GetCultureInfo((string)culture!) : null,
                DecimalPoint = (string?)column["decimalPoint"],
                ThousandsSeparator = (string?)column["thousandsSeparator"],
                DateFormats = Texts(column["dateFormats"]),
                TrueText = Texts(column["trueText"]),
                FalseText = Texts(column["falseText"]),
                BlankText = Texts(column["blankText"]),
            };
        }).ToArray();
        var schema = new CsvSchema(columns)
        {
            BlankText = Texts(written["blankText"]),
            RecordKey = (string?)written["recordKey"],
        };
        if (written["separator"] is { } separator)
        {
            schema = schema with
            {
                Separator = (string)separator! switch
                {
                    "comma" => CsvSeparator.Comma,
                    "tab" => CsvSeparator.Tab,
                    "semicolon" => CsvSeparator.Semicolon,
                    var other => throw new InvalidOperationException($"\"{other}\" is not a separator the cases name."),
                },
            };
        }
        if (written["hasHeader"] is { } hasHeader)
            schema = schema with { HasHeader = (bool)hasHeader };
        if (written["encoding"] is { } encoding)
        {
            schema = schema with
            {
                Encoding = (string)encoding! switch
                {
                    "utf-8" => CsvEncoding.Utf8,
                    "shift-jis" => CsvEncoding.ShiftJis,
                    var other => throw new InvalidOperationException($"\"{other}\" is not an encoding the cases name."),
                },
            };
        }
        return schema;
    }

    private static string[]? Texts(JsonNode? node) => node?.AsArray().Select(t => (string)t!).ToArray();

    /// <summary>The file, as the case writes it: a list of pieces, each UTF-8 text, <c>{"hex": "ef bb bf"}</c>
    /// bytes, or <c>{"repeat": "1\n", "times": 1024}</c> UTF-8 text written that many times.</summary>
    private static byte[] Bytes(JsonNode file)
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
                    var times = (int)Must(repeat, "times");
                    bytes.Capacity = bytes.Count + (once.Length * times);
                    for (var i = 0; i < times; i++)
                        bytes.AddRange(once);
                    break;
                default:
                    throw new InvalidOperationException($"{piece?.ToJsonString()} is not a piece of a file.");
            }
        }
        return [.. bytes];
    }

    /// <summary>A value as the Arrow cases write it for its kind (<c>tests/cases/README.md</c>), a Blank <c>null</c>.</summary>
    private static JsonNode? Value(object? value) => value switch
    {
        null => null,
        string text => text,
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number when double.IsPositiveInfinity(number) => "Infinity",
        double number when double.IsNegativeInfinity(number) => "-Infinity",
        double number when double.IsNaN(number) || (number == 0 && double.IsNegative(number))
            => new JsonObject { ["bits"] = BitConverter.DoubleToInt64Bits(number).ToString("x16", CultureInfo.InvariantCulture) },
        double number => number,
        long integer => integer is >= -(1L << 53) and <= 1L << 53 ? integer : integer.ToString(CultureInfo.InvariantCulture),
        DateTime date => date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        bool flag => flag,
        _ => throw new InvalidOperationException($"{value.GetType().Name} is not a value the cases write."),
    };

    // ---- the files ----------------------------------------------------------------------------------

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static JsonNode Must(JsonObject node, string key)
        => node[key] ?? throw new InvalidOperationException($"The case names no '{key}'.");

    /// <summary>Writes ExGrid.Data's answer into the case of the source file, under its <c>expect</c>.</summary>
    private static void Write(string file, string name, JsonNode answer)
    {
        lock (WriteGate)
        {
            var path = Path.Combine(SourceRoot(), file);
            var document = Read(path);
            var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
            entry["expect"] = answer.DeepClone();
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
            if (File.Exists(Path.Combine(dir.FullName, "ExGrid.slnx")))
                return Path.Combine(dir.FullName, "tests", "cases", "data");
        }
        throw new InvalidOperationException("The repository holding ExGrid.slnx is not above the test's directory.");
    }
}
