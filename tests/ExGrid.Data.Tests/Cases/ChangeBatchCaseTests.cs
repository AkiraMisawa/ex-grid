using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExGrid.Data.Storage;
using Xunit;

namespace ExGrid.Data.Tests.Cases;

/// <summary>
/// The shared Change Batch cases (<c>tests/cases/data</c>; ADR-0064, ADR-0067, ADR-0370 section 5), run
/// against ExGrid.Data: each batch of a case is applied to the case's Snapshot, or to the version a step
/// before made, and what it made, or how it was refused, is held to the case. The Script Core's runner is
/// <c>src/npm/data/test/change-batch-cases.test.ts</c>; <c>tests/cases/README.md</c> says what a file holds.
///
/// <para>Each case runs under three storages — one segment, segments of four rows, and segments of four that
/// compact after every batch — and every one must give the same answer: how ExGrid.Data stores a version is
/// its own, and the cases hold only what a reader sees. With <c>EXGRID_WRITE_DATA_CASES=1</c>, the runner
/// writes what ExGrid.Data answers into each case of the source files instead.</para>
/// </summary>
public class ChangeBatchCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "data");

    private static readonly SnapshotTuning[] Storages =
    [
        SnapshotTuning.Default,
        new(SegmentShift: 2),
        new(SegmentShift: 2, MaxBatchSegments: 0, BatchRowsDivisor: 1),
    ];

    private static bool Writing => Environment.GetEnvironmentVariable("EXGRID_WRITE_DATA_CASES") == "1";

    private static readonly Lock WriteGate = new();

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Read(path);
            if ((string?)document["kind"] != "changeBatch")
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0064: a Change Batch case holds on ExGrid.Data, under every storage
    public void Change_batch(string file, string name)
    {
        var entry = Read(Path.Combine(Root, file))["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        var answers = Storages.Select(storage => Answers(entry, storage)).ToArray();
        foreach (var other in answers.Skip(1))
            Assert.Equal(answers[0].ToJsonString(), other.ToJsonString());
        if (Writing)
        {
            Write(file, name, answers[0]);
            return;
        }
        var batches = entry["batches"]!.AsArray();
        for (var i = 0; i < batches.Count; i++)
        {
            var expected = Must(batches[i]!.AsObject(), "expect");
            Assert.True(JsonNode.DeepEquals(expected, answers[0][i]), $"Batch {i}: expected {expected.ToJsonString()}, answered {answers[0][i]!.ToJsonString()}.");
        }
    }

    /// <summary>What each batch of the case makes, or how it is refused, under <paramref name="storage"/>.</summary>
    private static JsonArray Answers(JsonObject entry, SnapshotTuning storage)
    {
        var snapshot = Build(Must(entry, "snapshot").AsObject(), storage);
        var made = new List<Snapshot?>();
        var answers = new JsonArray();
        foreach (var node in Must(entry, "batches").AsArray())
        {
            var step = node!.AsObject();
            var on = step["on"] switch
            {
                null => snapshot,
                JsonValue value when value.TryGetValue(out string? text) && text == "previous" => made[^1],
                JsonValue value => made[value.GetValue<int>()],
                _ => throw new InvalidOperationException("'on' is \"previous\" or a step's index."),
            } ?? throw new InvalidOperationException($"A batch is applied to a step that was refused: {answers.ToJsonString()}");
            var picture = Picture(on);
            try
            {
                var change = on.Apply(Batch(step, storage));
                made.Add(change.After);
                answers.Add(Answer(change));
                Folds(change);
            }
            catch (SnapshotException refusal)
            {
                made.Add(null);
                answers.Add(new JsonObject
                {
                    ["refused"] = new JsonObject
                    {
                        ["message"] = refusal.Message,
                        ["row"] = refusal.Row,
                        ["column"] = refusal.Column,
                        ["key"] = refusal.Key switch
                        {
                            null => null,
                            string text => text,
                            long integer => Integer(integer),
                            var other => throw new InvalidOperationException($"A refusal names the key {other}."),
                        },
                    },
                });
            }
            catch (ArgumentException)
            {
                made.Add(null);
                answers.Add(new JsonObject { ["refused"] = "argument" });
            }
            Assert.Equal(picture, Picture(on));
        }
        return answers;
    }

    /// <summary>The next version, and the rows that left and came, each by its place in its version's order.</summary>
    private static JsonObject Answer(SnapshotChange change)
    {
        return new JsonObject
        {
            ["version"] = change.After.Version,
            ["removed"] = Places(change.Before, change.Removed),
            ["added"] = Places(change.After, change.Added),
            ["after"] = new JsonArray([.. change.After.Columns.Select(column =>
            {
                var shown = new JsonObject
                {
                    ["name"] = column.Name,
                    ["caption"] = column.Caption,
                    ["values"] = new JsonArray([.. change.After.Rows.Select(row => Value(change.After.ValueAt(row, column)))]),
                };
                if (column is TextColumn text)
                    shown["dictionary"] = new JsonArray([.. text.Dictionary.Select(t => (JsonNode)t)]);
                return (JsonNode)shown;
            })]),
        };
    }

    private static JsonArray Places(Snapshot snapshot, IReadOnlyList<SnapshotRow> rows)
    {
        var order = new Dictionary<SnapshotRow, int>();
        var at = 0;
        foreach (var row in snapshot.Rows)
            order.Add(row, at++);
        return new JsonArray([.. rows.Select(r => order[r]).Order().Select(i => (JsonNode)i)]);
    }

    /// <summary>ADR-0067's fold: the rows the next version holds that the batch did not bring are the rows the
    /// version before held less those it removed, in their order.</summary>
    private static void Folds(SnapshotChange change)
    {
        var removed = change.Removed.ToHashSet();
        var added = change.Added.ToHashSet();
        Assert.Equal(change.Before.RowCount - change.Removed.Count + change.Added.Count, change.After.RowCount);
        Assert.Equal(
            change.Before.Rows.Where(r => !removed.Contains(r)).Select(r => Row(change.Before, r)),
            change.After.Rows.Where(r => !added.Contains(r)).Select(r => Row(change.After, r)));
    }

    private static string Row(Snapshot snapshot, SnapshotRow row)
        => string.Join("|", snapshot.Columns.Select(c => Value(snapshot.ValueAt(row, c))?.ToJsonString() ?? "∅"));

    private static string Picture(Snapshot snapshot)
        => $"{snapshot.Version}:" + string.Join("/", snapshot.Rows.Select(r => Row(snapshot, r)))
            + string.Join("/", snapshot.Columns.OfType<TextColumn>().Select(t => string.Join(",", t.Dictionary)));

    // ---- reading a case ---------------------------------------------------------------------------

    /// <summary>The case's Snapshot: its version, Record Key, and each column's name, caption, kind and values.</summary>
    private static Snapshot Build(JsonObject written, SnapshotTuning storage)
    {
        var builder = new SnapshotColumnsBuilder { Tuning = storage, Version = Must(written, "version").GetValue<long>() };
        foreach (var column in Must(written, "columns").AsArray())
            Columns(builder, column!.AsObject(), (string)Must(column.AsObject(), "caption")!);
        if ((string?)written["recordKey"] is { } key)
            builder.Key(key);
        return builder.Build();
    }

    /// <summary>A batch: its added and changed records, columns with no key and no captions of their own, and
    /// the Record Keys it removes.</summary>
    private static ChangeBatch Batch(JsonObject step, SnapshotTuning storage)
    {
        Snapshot? Records(string role)
        {
            if (step[role] is not JsonObject records)
                return null;
            var builder = new SnapshotColumnsBuilder { Tuning = storage };
            foreach (var column in Must(records, "columns").AsArray())
                Columns(builder, column!.AsObject(), null);
            return builder.Build();
        }
        var removed = step["removedKeys"]?.AsArray().Select(RemovedKey).ToArray();
        return ChangeBatch.Of(Records("added"), Records("changed"), removed!);
    }

    /// <summary>A removed key as a case writes it: a string a Text key, a number an Integer key (or a number
    /// that is no integer), <c>{"integer": "…"}</c> an Integer key past a double's exact integers.</summary>
    private static object? RemovedKey(JsonNode? key) => key switch
    {
        null => null,
        JsonObject integer => long.Parse((string)integer["integer"]!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        JsonValue value => value.GetValue<JsonElement>().TryGetInt64(out var whole) ? (object)whole : (double)value,
        _ => throw new InvalidOperationException($"{key.ToJsonString()} is not a removed key."),
    };

    private static void Columns(SnapshotColumnsBuilder builder, JsonObject column, string? caption)
    {
        var columnName = (string)Must(column, "name")!;
        var kind = (string)Must(column, "kind")!;
        var values = Must(column, "values").AsArray().Select(v => Read(kind, v)).ToArray();
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

    /// <summary>A value as the Arrow cases write it for its kind (<c>tests/cases/README.md</c>), a Blank <c>null</c>.</summary>
    private static object? Read(string kind, JsonNode? value)
    {
        if (value is null)
            return null;
        return kind switch
        {
            "text" => (string)value!,
            "decimal" => decimal.Parse((string)value!, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
            "double" => value switch
            {
                JsonObject bits => BitConverter.Int64BitsToDouble((long)ulong.Parse((string)bits["bits"]!, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)),
                JsonValue text when text.GetValueKind() == JsonValueKind.String => (string)text! switch
                {
                    "Infinity" => double.PositiveInfinity,
                    "-Infinity" => double.NegativeInfinity,
                    var other => throw new FormatException($"\"{other}\" is not a double the cases write."),
                },
                _ => (double)value,
            },
            "integer" => value.GetValueKind() == JsonValueKind.Number
                ? value.GetValue<long>()
                : long.Parse((string)value!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
            "date" => DateTime.ParseExact((string)value!, ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None),
            "boolean" => value.GetValue<bool>(),
            _ => throw new InvalidOperationException($"\"{kind}\" is not a kind the cases name."),
        };
    }

    /// <summary>A value written as <see cref="Read"/> reads it.</summary>
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
        long integer => Integer(integer),
        DateTime date => date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        bool flag => flag,
        _ => throw new InvalidOperationException($"{value.GetType().Name} is not a value the cases write."),
    };

    /// <summary>An integer, a number where a double holds it exactly and its text past that.</summary>
    private static JsonNode Integer(long value)
        => value is >= -(1L << 53) and <= 1L << 53 ? value : value.ToString(CultureInfo.InvariantCulture);

    // ---- the files ----------------------------------------------------------------------------------

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static JsonNode Must(JsonObject node, string key)
        => node[key] ?? throw new InvalidOperationException($"The case names no '{key}'.");

    /// <summary>Writes ExGrid.Data's answers into the case of the source file, each under its batch's <c>expect</c>.</summary>
    private static void Write(string file, string name, JsonArray answers)
    {
        lock (WriteGate)
        {
            var path = Path.Combine(SourceRoot(), file);
            var document = Read(path);
            var entry = document["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
            var batches = entry["batches"]!.AsArray();
            for (var i = 0; i < batches.Count; i++)
                batches[i]!.AsObject()["expect"] = answers[i]!.DeepClone();
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
