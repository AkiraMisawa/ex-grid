using System.Globalization;
using System.Text.Json.Nodes;
using Xunit;

namespace ExGrid.Data.Tests.Cases;

/// <summary>
/// Every culture's list separator as .NET gives it (<c>tests/cases/data/list-separators.json</c>), which a suggestion
/// takes to break a tie between two separators (ADR-0064 Q33). .NET on ICU keeps it in a table of its own, which the
/// browser's <c>Intl</c> does not hold, so <c>@ex-grid/data</c> carries a copy; this holds the file to .NET, and
/// <c>src/npm/data/test/list-separators.test.ts</c> holds the copy to the file. A separator other than a comma or a
/// semicolon is written <c>null</c>, since a CSV is never split at it. With <c>EXGRID_WRITE_DATA_CASES=1</c>, the
/// runner writes .NET's answers into the source file instead.
/// </summary>
public class ListSeparatorCaseTests
{
    private const string File = "list-separators.json";

    [Fact] // ADR-0064 (Q33): the list separators the Script Core carries are .NET's
    public void Every_cultures_list_separator_is_dotnets()
    {
        var cultures = new JsonObject();
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures).Where(c => c.Name.Length > 0).OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            var separator = culture.TextInfo.ListSeparator;
            cultures[culture.Name] = separator is "," or ";" ? separator : null;
        }
        if (CsvReadCaseTests.Writing)
        {
            CsvReadCaseTests.WriteKey(File, "cultures", cultures);
            return;
        }
        var expected = CsvReadCaseTests.Must(CsvReadCaseTests.Read(Path.Combine(AppContext.BaseDirectory, "cases", "data", File)), "cultures");
        Assert.True(JsonNode.DeepEquals(expected, cultures), "The list separators .NET gives differ from list-separators.json; write it again and update the Script Core's copy.");
    }
}
