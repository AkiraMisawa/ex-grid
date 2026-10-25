using System.Globalization;
using System.Text.Json;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Arrow.Tests.Fixtures;

namespace ExGrid.Data.Arrow.Tests.Cases;

/// <summary>
/// The shared Arrow cases (<c>tests/cases/arrow</c>; ADR-0370 section 3), run against ExGrid.Data.Arrow:
/// <c>arrowRead</c> reads a stream every way a Consumer hands one in, <c>arrowShape</c> reads a stream's
/// shape, and <c>arrowWrite</c> writes a Snapshot and reads it back, and reads what <c>@ex-grid/data</c>
/// wrote for the same case. The Script Core's runner is <c>src/npm/data/test/arrow-cases.test.ts</c>;
/// <c>tests/cases/README.md</c> says what a file holds.
/// </summary>
public class ArrowCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "arrow");

    public static TheoryData<string, string> ReadCases() => Of("arrowRead");

    public static TheoryData<string, string> ShapeCases() => Of("arrowShape");

    public static TheoryData<string, string> WriteCases() => Of("arrowWrite");

    [Theory, MemberData(nameof(ReadCases))] // ADR-0065: an Arrow read case holds on ExGrid.Data.Arrow
    public async Task Read(string file, string name)
    {
        var entry = Find(file, name);
        var payload = Bytes(entry.GetProperty("stream").GetString()!);
        if (entry.TryGetProperty("refused", out var refused))
        {
            Refused(await RefusalAsync(payload), refused);
            return;
        }
        Same(entry.GetProperty("expect"), await ReadEveryWayAsync(payload));
    }

    [Theory, MemberData(nameof(ShapeCases))] // ADR-0341: an Arrow shape case holds on ExGrid.Data.Arrow
    public async Task Shape(string file, string name)
    {
        var entry = Find(file, name);
        using var stream = new MemoryStream(Bytes(entry.GetProperty("stream").GetString()!), writable: false);
        if (entry.TryGetProperty("refused", out var refused))
        {
            Refused(await Assert.ThrowsAsync<SnapshotException>(async () => await SnapshotArrow.ReadShapeAsync(stream, TestContext.Current.CancellationToken)), refused);
            return;
        }
        var shape = await SnapshotArrow.ReadShapeAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(
            entry.GetProperty("shape").EnumerateArray().Select(c => (c.GetProperty("name").GetString(), c.GetProperty("kind").GetString())),
            shape.Columns.Select(c => ((string?)c.Name, (string?)KindName(c.Kind))));
    }

    [Theory, MemberData(nameof(WriteCases))] // ADR-0065: an Arrow write case holds on ExGrid.Data.Arrow, and what @ex-grid/data wrote for it reads back as the Snapshot it was
    public async Task Write(string file, string name)
    {
        var entry = Find(file, name);
        var written = entry.GetProperty("snapshot");
        var snapshot = Build(written);
        if (entry.TryGetProperty("refused", out var refused))
        {
            using var target = new MemoryStream();
            Refused(await Assert.ThrowsAsync<SnapshotException>(() => SnapshotArrow.WriteAsync(snapshot, target, TestContext.Current.CancellationToken)), refused);
            Assert.Equal(0, target.Length);
            return;
        }
        var expect = entry.GetProperty("expect");
        var payload = await WriteAsync(snapshot);
        Schema(expect, payload);
        Same(written, await ReadEveryWayAsync(payload));

        // What the Script Core wrote for the same Snapshot (ADR-0370 section 3): Apache.Arrow reads it,
        // with the same schema, and this reader reads it back as the Snapshot it was.
        var scripted = Bytes(entry.GetProperty("written").GetString()!);
        Schema(expect, scripted);
        Same(written, await ReadEveryWayAsync(scripted));
    }

    // ---- the files ---------------------------------------------------------------------------

    private static TheoryData<string, string> Of(string kind)
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Parse(path);
            if (document.GetProperty("kind").GetString() != kind)
                continue;
            foreach (var entry in document.GetProperty("cases").EnumerateArray())
                rows.Add(Path.GetFileName(path), entry.GetProperty("name").GetString()!);
        }
        return rows;
    }

    private static JsonElement Find(string file, string name)
    {
        foreach (var entry in Parse(Path.Combine(Root, file)).GetProperty("cases").EnumerateArray())
        {
            if (entry.GetProperty("name").GetString() == name)
                return entry;
        }
        throw new InvalidOperationException($"arrow/{file} holds no case named \"{name}\".");
    }

    private static JsonElement Parse(string path)
    {
        using var stream = System.IO.File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static byte[] Bytes(string relative) => System.IO.File.ReadAllBytes(Path.Combine(Root, relative));

    // ---- what a case says ----------------------------------------------------------------------

    /// <summary>Holds a refusal to what the case says: its whole message, or a text it holds
    /// (<c>saying</c>), and its row, its column and, where named, its key.</summary>
    private static void Refused(SnapshotException refusal, JsonElement expected)
    {
        if (expected.TryGetProperty("message", out var message))
            Assert.Equal(message.GetString(), refusal.Message);
        else
            Assert.Contains(expected.GetProperty("saying").GetString()!, refusal.Message, StringComparison.Ordinal);
        Assert.Equal(expected.GetProperty("row").ValueKind == JsonValueKind.Null ? null : expected.GetProperty("row").GetInt64(), refusal.Row);
        Assert.Equal(expected.GetProperty("column").GetString(), refusal.Column);
        if (expected.TryGetProperty("key", out var key))
            Assert.Equal(key.ValueKind == JsonValueKind.String ? key.GetString() : key.GetInt64(), refusal.Key);
    }

    /// <summary>Holds a Snapshot to what a case says it holds: its version, its Record Key, and each
    /// column's name, caption, kind, values in row order and, for Text where named, its dictionary.</summary>
    private static void Same(JsonElement expected, Snapshot actual)
    {
        Assert.Equal(expected.GetProperty("version").GetInt64(), actual.Version);
        Assert.Equal(expected.GetProperty("recordKey").GetString(), actual.RecordKey?.Name);
        var columns = expected.GetProperty("columns").EnumerateArray().ToArray();
        Assert.Equal(
            columns.Select(c => (c.GetProperty("name").GetString(), c.GetProperty("caption").GetString(), c.GetProperty("kind").GetString())),
            actual.Columns.Select(c => ((string?)c.Name, (string?)c.Caption, (string?)KindName(c.Kind))));
        foreach (var column in columns)
        {
            var columnName = column.GetProperty("name").GetString()!;
            var kind = column.GetProperty("kind").GetString()!;
            Assert.Equal(column.GetProperty("values").EnumerateArray().Select(v => Show(Value(kind, v))), Values(actual, columnName));
            if (column.TryGetProperty("dictionary", out var dictionary))
                Assert.Equal(dictionary.EnumerateArray().Select(e => e.GetString()!), Dictionary(actual, columnName));
        }
    }

    /// <summary>Holds a written stream's schema, as Apache.Arrow reads it, to what a case says: each
    /// field's name, type, nullability and caption, and the schema's metadata, whole.</summary>
    private static void Schema(JsonElement expected, byte[] payload)
    {
        using var reader = new ArrowStreamReader(payload);
        var schema = reader.Schema;
        Assert.Equal(
            expected.GetProperty("fields").EnumerateArray().Select(f => (f.GetProperty("name").GetString(), f.GetProperty("type").GetString(), true, f.GetProperty("caption").GetString())),
            schema.FieldsList.Select(f => ((string?)f.Name, (string?)ArrowTypeNames.Of(f.DataType), f.IsNullable,
                f.HasMetadata && f.Metadata.TryGetValue(SnapshotArrowMetadata.Caption, out var caption) ? caption : null)));
        Assert.True(schema.FieldsList.All(f => f.DataType is not TimestampType stamp || stamp.Timezone is null));
        Assert.Equal(
            expected.GetProperty("metadata").EnumerateObject().Select(p => (p.Name, p.Value.GetString())).Order(),
            schema.Metadata.Select(p => (p.Key, (string?)p.Value)).Order());
        while (reader.ReadNextRecordBatch() is not null)
        {
        }
    }

    /// <summary>The Snapshot a write case gives, every key written: its version, its Record Key, and
    /// each column's name, caption, kind and values.</summary>
    private static Snapshot Build(JsonElement written)
    {
        var builder = new SnapshotColumnsBuilder { Version = written.GetProperty("version").GetInt64() };
        foreach (var column in written.GetProperty("columns").EnumerateArray())
        {
            var columnName = column.GetProperty("name").GetString()!;
            var caption = column.GetProperty("caption").GetString()!;
            var kind = column.GetProperty("kind").GetString()!;
            var values = column.GetProperty("values").EnumerateArray().Select(v => Value(kind, v)).ToArray();
            switch (kind)
            {
                case "text":
                    var text = builder.Text(columnName, caption);
                    foreach (var value in values)
                        text.Append((string?)value);
                    break;
                case "decimal":
                    var number = builder.Decimal(columnName, caption);
                    foreach (var value in values)
                        number.Append((decimal?)value);
                    break;
                case "double":
                    var real = builder.Double(columnName, caption);
                    foreach (var value in values)
                        real.Append((double?)value);
                    break;
                case "integer":
                    var integer = builder.Integer(columnName, caption);
                    foreach (var value in values)
                        integer.Append((long?)value);
                    break;
                case "date":
                    var date = builder.Date(columnName, caption);
                    foreach (var value in values)
                    {
                        if (value is DateTime when)
                            date.Append(when);
                        else
                            date.AppendBlank();
                    }
                    break;
                case "boolean":
                    var flag = builder.Boolean(columnName, caption);
                    foreach (var value in values)
                        flag.Append((bool?)value);
                    break;
                default:
                    throw new InvalidOperationException($"\"{kind}\" is not a kind the cases name.");
            }
        }
        if (written.GetProperty("recordKey").GetString() is { } key)
            builder.Key(key);
        return builder.Build();
    }

    /// <summary>
    /// A value as a case writes it for its column's kind, as the Snapshot holds it: Text a string, or
    /// <c>{"utf16": text}</c> where each <c>\uXXXX</c> of the text is one UTF-16 unit, a lone surrogate
    /// among them, which JSON's own strings cannot hold; Decimal
    /// its text; Double a number, <c>"Infinity"</c>, <c>"-Infinity"</c> or <c>{"bits": hex}</c>; Integer a
    /// number or, past a double's exact integers, its text; Date <c>yyyy-MM-dd</c>, with
    /// <c>THH:mm:ss</c> and up to seven places after; Boolean <c>true</c> or <c>false</c>; a Blank
    /// <c>null</c>.
    /// </summary>
    private static object? Value(string kind, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        return kind switch
        {
            "text" => value.ValueKind == JsonValueKind.Object ? Utf16(value.GetProperty("utf16").GetString()!) : value.GetString(),
            "decimal" => decimal.Parse(value.GetString()!, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
            "double" => value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDouble(),
                JsonValueKind.Object => BitConverter.Int64BitsToDouble((long)ulong.Parse(value.GetProperty("bits").GetString()!, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)),
                _ => value.GetString() switch
                {
                    "Infinity" => double.PositiveInfinity,
                    "-Infinity" => double.NegativeInfinity,
                    var other => throw new FormatException($"\"{other}\" is not a double the cases write."),
                },
            },
            "integer" => value.ValueKind == JsonValueKind.Number ? value.GetInt64() : long.Parse(value.GetString()!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
            "date" => DateTime.ParseExact(value.GetString()!, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None),
            "boolean" => value.GetBoolean(),
            _ => throw new InvalidOperationException($"\"{kind}\" is not a kind the cases name."),
        };
    }

    private static string Utf16(string escaped)
        => System.Text.RegularExpressions.Regex.Replace(escaped, @"\\u([0-9a-fA-F]{4})", m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToString());

    private static string KindName(SnapshotKind kind) => kind switch
    {
        SnapshotKind.Text => "text",
        SnapshotKind.Decimal => "decimal",
        SnapshotKind.Double => "double",
        SnapshotKind.Integer => "integer",
        SnapshotKind.Date => "date",
        SnapshotKind.Boolean => "boolean",
        _ => throw new InvalidOperationException($"{kind} is not a kind the cases name."),
    };
}
