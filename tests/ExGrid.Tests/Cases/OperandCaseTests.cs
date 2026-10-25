using System.Globalization;
using System.Text.Json;
using ExGrid.Chrome;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// What a filter condition's typed operand reads as under a culture, and the text an operand reopens in
/// (<c>tests/cases/chrome</c>, kind <c>operand</c>; ADR-0023, ADR-0350 section 5, ADR-0370 section 10), run
/// against <see cref="FilterPanelChoices.ReadOperand"/>, <see cref="FilterPanelChoices.OperandText"/> and
/// <see cref="FilterPanelChoices.RefusalText"/>.
/// </summary>
public class OperandCaseTests
{
    private const string Area = "chrome";

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "operand");

    [Theory, MemberData(nameof(Cases))] // ADR-0023: an operand case holds on the C# core
    public void Operand(string file, string name)
    {
        var (_, entry) = SharedCases.Find(Area, file, name);
        var culture = CultureOf(entry.GetProperty("culture").GetString()!);
        var dateType = entry.TryGetProperty("dateType", out var declared) ? QueryCaseTests.Member<DateType>(declared) : DateType.DateTime;
        if (entry.TryGetProperty("operandText", out var operand))
        {
            var value = Value(operand);
            var text = FilterPanelChoices.OperandText(value, culture);
            Assert.Equal(entry.GetProperty("is").GetString(), text);
            if (entry.TryGetProperty("readsBack", out var type))
            {
                var back = FilterPanelChoices.ReadOperand(QueryCaseTests.Member<ColumnType>(type), text, culture, dateType);
                Assert.Equal(OperandRefusal.None, back.Refusal);
                AssertSame(value, back.Value);
            }
            return;
        }
        var written = entry.GetProperty("text").GetString()!;
        var reading = FilterPanelChoices.ReadOperand(QueryCaseTests.Member<ColumnType>(entry.GetProperty("type")), written, culture, dateType);
        if (entry.TryGetProperty("reads", out var reads))
        {
            Assert.Equal(OperandRefusal.None, reading.Refusal);
            AssertSame(Value(reads), reading.Value);
        }
        else
        {
            Assert.Equal(QueryCaseTests.Member<OperandRefusal>(entry.GetProperty("refused")), reading.Refusal);
            Assert.Null(reading.Value);
            if (entry.TryGetProperty("readings", out var readings))
            {
                var (first, second) = reading.OtherValue!.Value;
                AssertSame(Value(readings[0]), first);
                AssertSame(Value(readings[1]), second);
            }
            if (entry.TryGetProperty("read", out var read))
                AssertSame(Value(read), reading.OtherValue!.Value.CultureReading);
        }
        var says = FilterPanelChoices.RefusalText(reading, written, culture);
        if (entry.TryGetProperty("says", out var words))
            Assert.Equal(words.GetString(), says);
    }

    /// <summary>A culture as a case names it: a BCP 47 tag, or <c>invariant</c>.</summary>
    private static CultureInfo CultureOf(string name)
        => name == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);

    /// <summary>A value as an operand case writes it: a number as its decimal text, anything else as a query
    /// case writes it.</summary>
    private static object? Value(JsonElement written)
        => written.ValueKind == JsonValueKind.Object && written.TryGetProperty("number", out var number)
            ? decimal.Parse(number.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)
            : QueryCaseTests.Value(written);

    /// <summary>The same value, a moment with the same offset: two moments are equal when they are the same
    /// instant, and the offset is part of what was typed.</summary>
    private static void AssertSame(object? expected, object? actual)
    {
        Assert.Equal(expected, actual);
        if (expected is DateTimeOffset moment)
            Assert.Equal(moment.Offset, Assert.IsType<DateTimeOffset>(actual).Offset);
    }
}
