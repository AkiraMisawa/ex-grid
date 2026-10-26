using System.Buffers;
using System.Globalization;
using System.Text.Json;
using ExGrid.Script;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// What Blazor's ExGrid writes for a cell's value as it crosses to the Script Core (<c>tests/cases/blazor</c>,
/// kind <c>wireValue</c>; ADR-0350 sections 7 and 16), run against <c>ScriptValues.Write</c>. Vitest reads the
/// same JSON with the bridge, so the two halves are held to one text.
/// </summary>
public class WireValueCaseTests
{
    private const string Area = "blazor";

    public static TheoryData<string, string> Cases()
    {
        var rows = new TheoryData<string, string>();
        foreach (var file in SharedCases.FilesOf(Area, "wireValue"))
        {
            foreach (var entry in SharedCases.Read(Area, file).GetProperty("cases").EnumerateArray())
            {
                if (entry.TryGetProperty("dotnet", out _))
                    rows.Add(file, entry.GetProperty("name").GetString()!);
            }
        }
        return rows;
    }

    [Theory, MemberData(nameof(Cases))] // ADR-0350 s16: a value crosses as the case's wire, or is refused
    public void Wire_value(string file, string name)
    {
        var (_, entry) = SharedCases.Find(Area, file, name);
        var value = Value(entry.GetProperty("dotnet"));
        if (entry.TryGetProperty("refused", out var refused))
        {
            Assert.Equal("notSupported", refused.GetString());
            var exception = Assert.Throws<NotSupportedException>(() => Written(value));
            Assert.Contains("'Amount'", exception.Message, StringComparison.Ordinal);
            return;
        }
        using var written = JsonDocument.Parse(Written(value));
        var wire = entry.GetProperty("wire");
        Assert.True(JsonElement.DeepEquals(wire, written.RootElement), $"wrote {written.RootElement.GetRawText()}, the case says {wire.GetRawText()}");
    }

    private static string Written(object? value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
            ScriptValues.Write(json, value, "Amount");
        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static object? Value(JsonElement dotnet)
    {
        var value = dotnet.GetProperty("value");
        string Text() => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        var invariant = CultureInfo.InvariantCulture;
        return dotnet.GetProperty("type").GetString() switch
        {
            "null" => null,
            "string" => value.GetString(),
            "bool" => value.GetBoolean(),
            "int" => value.GetInt32(),
            "long" => long.Parse(Text(), invariant),
            "ulong" => ulong.Parse(Text(), invariant),
            "double" => double.Parse(Text(), invariant),
            "float" => float.Parse(Text(), invariant),
            "decimal" => decimal.Parse(Text(), invariant),
            "dateOnly" => DateOnly.ParseExact(Text(), "yyyy-MM-dd", invariant),
            "dateTime" => DateTime.ParseExact(Text(), "yyyy-MM-dd'T'HH:mm:ss", invariant),
            "dateTimeOffset" => DateTimeOffset.ParseExact(Text(), "yyyy-MM-dd'T'HH:mm:sszzz", invariant),
            "timeOnly" => TimeOnly.ParseExact(Text(), "HH:mm:ss", invariant),
            var other => throw new InvalidOperationException($"A wireValue case names the .NET type '{other}', which the runner does not make."),
        };
    }
}
