using System.Globalization;
using System.Numerics;
using System.Text.Json;
using ExGrid.Data;
using ExGrid.Summarizing;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The reference Selection Summary's shared cases (<c>tests/cases/summary</c>, kind <c>summary</c>;
/// ADR-0350 section 5), run against <see cref="GridSummary.Of{TRow}"/>. A row is the values of its cells in
/// the file's columns. A number is read as the Script Core reads one (ADR-0350 section 16): a JSON number as
/// C# converts a double to a decimal, kept a double only where no decimal holds it, and
/// <c>{"bigint": text}</c> as an integer of any width. The Script Core's runner is
/// <c>packages/core/test/summary.test.ts</c>.
/// </summary>
public class SummaryCaseTests
{
    private const string Area = "summary";

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "summary");

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a summary case holds on the C# core
    public void Summary(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var columns = Strings(entry.TryGetProperty("columns", out var own) ? own : document.GetProperty("columns"));
        var written = (entry.TryGetProperty("rows", out var given) ? given : document.GetProperty("rows"))
            .EnumerateArray().Select(row => row.EnumerateArray().Select(Value).ToArray()).ToArray();
        var rows = entry.TryGetProperty("repeat", out var repeat)
            ? [.. Enumerable.Range(0, repeat.GetInt32()).SelectMany(_ => written)]
            : written;
        var result = GridSummary.Of(rows, Request(entry.GetProperty("ask"), columns, 0), column =>
        {
            var at = Array.IndexOf(columns, column);
            return at < 0 ? null : row => row[at];
        });
        Expect(result, entry.GetProperty("is"), entry.TryGetProperty("texts", out var texts) ? texts : null);
    }

    /// <summary>The request a case writes, under the version given.</summary>
    internal static GridSummaryRequest Request(JsonElement written, IReadOnlyList<string> columns, int version) => new()
    {
        Ranges = SharedCases.Ranges(written.GetProperty("ranges")),
        Columns = written.TryGetProperty("columns", out var own) ? Strings(own) : columns,
        RowSequenceVersion = version,
        Figures = written.GetProperty("figures").EnumerateArray()
            .Aggregate(SummaryFigures.None, (figures, figure) => figures | QueryCaseTests.Member<SummaryFigures>(figure)),
    };

    /// <summary>Holds an answer to what a case says of it: every figure it names and no other, and the
    /// text each named in <paramref name="texts"/> is shown with where no format applies.</summary>
    internal static void Expect(GridSummaryResult result, JsonElement expected, JsonElement? texts)
    {
        Assert.False(result.IsDeclined, $"expected figures, got a decline: {result.DeclineReason}");
        var named = expected.EnumerateObject().Aggregate(SummaryFigures.None, (figures, p) => figures | Figure(p.Name));
        Assert.Equal(named, result.Figures);
        foreach (var property in expected.EnumerateObject())
        {
            var answer = result[Figure(property.Name)] ?? throw new InvalidOperationException($"No answer for {property.Name}.");
            Assert.True(Holds(answer, property.Value), $"{property.Name}: expected {property.Value}, got {Shown(answer)}");
        }
        if (texts is { } shown)
        {
            foreach (var property in shown.EnumerateObject())
            {
                var answer = result[Figure(property.Name)] ?? throw new InvalidOperationException($"A text for {property.Name}, which was not asked.");
                Assert.Equal(property.Value.GetString(), GridSummary.FigureText(Figure(property.Name), answer));
            }
        }
    }

    /// <summary>A value as the Script Core reads it (ADR-0350 section 16).</summary>
    private static object? Value(JsonElement written)
    {
        if (written.ValueKind == JsonValueKind.Object && written.TryGetProperty("bigint", out var big))
            return BigInteger.Parse(big.GetString()!, CultureInfo.InvariantCulture);
        var value = QueryCaseTests.Value(written);
        if (value is not double number || !double.IsFinite(number))
            return value;
        // A double no decimal holds, too large or so small it would read as 0, stays a double.
        if (Math.Abs(number) >= 7.9228162514264337593543950335e28)
            return number;
        var exact = (decimal)number;
        return exact == 0 && number != 0 ? number : exact;
    }

    private static bool Holds(AggregateResult answer, JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.String)
            return expected.GetString() == "empty" ? answer.IsEmpty : throw new InvalidOperationException($"\"{expected}\" is no answer a case writes.");
        if (expected.TryGetProperty("exact", out var exact))
            return answer.Exact is { } held && AggregateArithmetic.Canonical(held).ToString(CultureInfo.InvariantCulture) == exact.GetString();
        if (expected.TryGetProperty("double", out var number))
        {
            var wanted = number.ValueKind == JsonValueKind.String ? double.Parse(number.GetString()!, CultureInfo.InvariantCulture) : number.GetDouble();
            return answer.IsNumber && answer.Exact is null && answer.Number.Equals(wanted);
        }
        if (expected.TryGetProperty("error", out var error))
            return answer.Error == QueryCaseTests.Member<AggregateError>(error);
        throw new InvalidOperationException($"{expected} is no answer a case writes.");
    }

    private static string Shown(AggregateResult answer) => answer.IsEmpty ? "empty"
        : answer.Error != AggregateError.None ? answer.Error.ToString()
        : answer.Exact is { } exact ? $"exact {exact.ToString(CultureInfo.InvariantCulture)}"
        : $"double {answer.Number.ToString("R", CultureInfo.InvariantCulture)}";

    private static SummaryFigures Figure(string name) => QueryCaseTests.Member<SummaryFigures>(JsonSerializer.SerializeToElement(name));

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];
}
