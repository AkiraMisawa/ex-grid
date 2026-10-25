using System.Globalization;
using System.Text.Json;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The text a value cell displays (<c>tests/cases/cells</c>, kind <c>displayText</c>; ADR-0006, ADR-0350
/// section 5), run against <see cref="ColumnInfo{TRow}.TextFor"/>, which reads <see cref="DisplayText"/>
/// where the column has no format.
/// </summary>
public class DisplayTextCaseTests
{
    private const string Area = "cells";

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "displayText");

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a display-text case holds on the C# core
    public void DisplayText(string file, string name)
    {
        var (_, entry) = SharedCases.Find(Area, file, name);
        foreach (var text in entry.GetProperty("texts").EnumerateArray())
        {
            var column = new ColumnInfo<object?>("Value", ColumnType.Text, row => row,
                Format: text.TryGetProperty("format", out var format) ? FormatOf(format.GetString()!) : null);
            Assert.Equal(text.GetProperty("is").GetString(), column.TextFor(QueryCaseTests.Value(text.GetProperty("value"))));
        }
    }

    /// <summary>A format as a case names it (tests/cases/README.md): <c>whole</c> shows a number rounded to
    /// a whole one, as the custom format <c>0</c> writes it.</summary>
    internal static Func<object, string> FormatOf(string name) => name switch
    {
        "whole" => value => System.Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0", CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"\"{name}\" is not a format a case names."),
    };
}
