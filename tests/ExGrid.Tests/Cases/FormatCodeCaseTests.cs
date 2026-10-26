using System.Globalization;
using System.Text.Json;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// An Excel number format code as a grid built from a source's shape reads it (<c>tests/cases/shaped</c>,
/// kind <c>formatCode</c>; ADR-0342, ADR-0120, ADR-0370 sections 5 and 10), run against
/// <see cref="FormatCode.TryParse"/> and <see cref="FormatCode.Format"/>.
/// </summary>
public class FormatCodeCaseTests
{
    private const string Area = "shaped";

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "formatCode");

    [Theory, MemberData(nameof(Cases))] // ADR-0342: a format code case holds on the C# core
    public void FormatCodeCase(string file, string name)
    {
        var (_, entry) = SharedCases.Find(Area, file, name);
        var code = entry.GetProperty("code").GetString()!;
        if (entry.TryGetProperty("refused", out var refused))
        {
            Assert.False(FormatCode.TryParse(code, out _, out var reason));
            Assert.Equal(refused.GetString(), reason);
            return;
        }
        Assert.True(FormatCode.TryParse(code, out var format, out var why), why);
        Assert.Equal(code, format.Code);
        Assert.Equal(entry.GetProperty("isDate").GetBoolean(), format.IsDate);
        var texts = entry.GetProperty("texts");
        Assert.True(texts.GetArrayLength() > 0, "A case that reads a code writes a value with it.");
        foreach (var text in texts.EnumerateArray())
        {
            var locale = text.GetProperty("locale").GetString()!;
            var culture = locale == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
            Assert.Equal(text.GetProperty("is").GetString(), format.Format(Value(text.GetProperty("value")), culture));
        }
    }

    /// <summary>A value as a format code case writes it, as a Snapshot holds it: a JSON number a double,
    /// <c>{"decimal": text}</c> a decimal, <c>{"integer": text}</c> a long, <c>{"double": text}</c> a double
    /// JSON cannot hold, <c>{"date": text}</c> a DateTime.</summary>
    private static object Value(JsonElement written)
    {
        if (written.ValueKind == JsonValueKind.Number)
            return written.GetDouble();
        var member = written.EnumerateObject().Single();
        var text = member.Value.GetString()!;
        return member.Name switch
        {
            "decimal" => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture),
            "integer" => long.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
            "double" => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
            "date" => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => throw new InvalidOperationException($"{written} is not a value a format code case writes."),
        };
    }
}
