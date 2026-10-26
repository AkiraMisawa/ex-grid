using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ExGrid.Data.Tests.Cases;

/// <summary>
/// The shared Board Input cases (<c>tests/cases/data</c>, kind <c>boardInput</c>; ADR-0341, ADR-0370 section 3),
/// run against ExGrid.Data: a declaration read, a source's shape checked against it, and JSON rows made a
/// Snapshot, or the refusal, held to the case. The Script Core's runner is
/// <c>src/npm/data/test/board-input-cases.test.ts</c>; <c>tests/cases/README.md</c> says what a file holds.
/// </summary>
public class BoardInputCaseTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "cases", "data");

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var path in Directory.GetFiles(Root, "*.json").Order(StringComparer.Ordinal))
        {
            var document = Read(path);
            if ((string?)document["kind"] != "boardInput")
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(Path.GetFileName(path), (string)entry!["name"]!);
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0341: a Board Input case holds on ExGrid.Data
    public async Task Board_input(string file, string name)
    {
        var entry = Read(Path.Combine(Root, file))["cases"]!.AsArray().Single(c => (string?)c!["name"] == name)!.AsObject();
        JsonNode? answer;
        try
        {
            answer = await Answer(entry);
        }
        catch (BoardRefusedException refused)
        {
            var expected = Must(entry, "refused")!.AsObject();
            Assert.Equal((string)Must(expected, "path")!, refused.Path);
            if (expected["saying"] is { } saying)
                Assert.StartsWith(saying.GetValue<string>(), refused.Message, StringComparison.Ordinal);
            else
                Assert.Equal((string)Must(expected, "message")!, refused.Message);
            return;
        }
        Assert.False(entry.ContainsKey("refused"), $"Expected a refusal, answered {answer?.ToJsonString()}.");
        var key = entry.ContainsKey("columns") ? "columns" : entry.ContainsKey("snapshot") ? "snapshot" : "binds";
        var want = Must(entry, key);
        Assert.True(JsonNode.DeepEquals(want, answer), $"Expected {want!.ToJsonString()}, answered {answer?.ToJsonString()}.");
    }

    private static async Task<JsonNode?> Answer(JsonObject entry)
    {
        if (entry["read"] is { } read)
        {
            using var document = JsonDocument.Parse((string)read!);
            return Columns(BoardInput.Read("trades", document.RootElement, "inputs.trades").Columns);
        }
        var input = new BoardInput("trades", ColumnsOf(Must(entry, "declared")!.AsArray()));
        if (entry.TryGetPropertyValue("check", out var check))
        {
            input.Check(new DataShape(ColumnsOf(check!.AsArray())));
            return true;
        }
        if (entry.TryGetPropertyValue("bind", out var bind))
        {
            var builder = new SnapshotColumnsBuilder();
            foreach (var column in ColumnsOf(bind!.AsArray()))
            {
                _ = column.Kind switch
                {
                    SnapshotKind.Text => (object)builder.Text(column.Name),
                    SnapshotKind.Decimal => builder.Decimal(column.Name),
                    SnapshotKind.Double => builder.Double(column.Name),
                    SnapshotKind.Integer => builder.Integer(column.Name),
                    SnapshotKind.Date => builder.Date(column.Name),
                    _ => builder.Boolean(column.Name),
                };
            }
            var snapshot = builder.Build();
            Assert.Same(snapshot, input.Bind(snapshot));
            return true;
        }
        if (entry.TryGetPropertyValue("checkSource", out var shape))
        {
            var answered = shape is null ? null : new DataShape(ColumnsOf(shape.AsArray()));
            var source = new Answering(answered);
            Assert.Same(answered, await input.CheckAsync(source, TestContext.Current.CancellationToken));
            Assert.Equal(1, source.Asked);
            return true;
        }
        var rows = input.FromJson((string)Must(entry, "fromJson")!);
        return new JsonArray([.. rows.Columns.Select(column => (JsonNode)new JsonObject
        {
            ["name"] = column.Name,
            ["kind"] = BoardInput.NameOf(column.Kind),
            ["values"] = new JsonArray([.. rows.Rows.Select(row => Value(rows.ValueAt(row, column)))]),
        })]);
    }

    private static List<DataShapeColumn> ColumnsOf(JsonArray list) =>
        [.. list.Select(c => new DataShapeColumn((string)c!["name"]!, KindOf((string)c["type"]!)))];

    private static SnapshotKind KindOf(string name) => Enum.GetValues<SnapshotKind>().Single(k => BoardInput.NameOf(k) == name);

    private static JsonArray Columns(IEnumerable<DataShapeColumn> columns) =>
        new([.. columns.Select(c => (JsonNode)new JsonObject { ["name"] = c.Name, ["type"] = BoardInput.NameOf(c.Kind) })]);

    // As tests/cases/README.md writes a value under boardInput: a double that is no number by its name.
    private static JsonNode? Value(object? value) => value switch
    {
        null => null,
        string text => text,
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number when double.IsPositiveInfinity(number) => "Infinity",
        double number when double.IsNegativeInfinity(number) => "-Infinity",
        double number when double.IsNaN(number) => "NaN",
        double number => number,
        long integer => integer is >= -(1L << 53) and <= 1L << 53 ? integer : integer.ToString(CultureInfo.InvariantCulture),
        DateTime date => date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        bool flag => flag,
        _ => throw new InvalidOperationException($"{value.GetType().Name} is not a value the cases write."),
    };

    private sealed class Answering(DataShape? shape) : IShapedSource
    {
        public int Asked { get; private set; }

        public ValueTask<DataShape?> ShapeAsync(CancellationToken cancellationToken = default)
        {
            Asked++;
            return ValueTask.FromResult(shape);
        }
    }

    private static JsonNode? Must(JsonObject entry, string key) =>
        entry.TryGetPropertyValue(key, out var value) ? value : throw new InvalidOperationException($"The case leaves out '{key}'.");

    private static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();
}
