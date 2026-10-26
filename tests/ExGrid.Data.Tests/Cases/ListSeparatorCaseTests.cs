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
        if (CsvReadCaseTests.Writing)
        {
            var cultures = new JsonObject();
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures).Where(c => c.Name.Length > 0).OrderBy(c => c.Name, StringComparer.Ordinal))
                cultures[culture.Name] = SeparatorOf(culture);
            CsvReadCaseTests.WriteKey(File, "cultures", cultures);
            return;
        }
        // Judged by the cultures the file names: which cultures .NET lists is its ICU's, and moves with the ICU a
        // machine has, while each one's separator is .NET's own table.
        var expected = CsvReadCaseTests.Must(CsvReadCaseTests.Read(Path.Combine(AppContext.BaseDirectory, "cases", "data", File)), "cultures").AsObject();
        var differing = expected
            .Where(entry => SeparatorOf(CultureInfo.GetCultureInfo(entry.Key)) != (string?)entry.Value)
            .Select(entry => $"{entry.Key}: '{SeparatorOf(CultureInfo.GetCultureInfo(entry.Key))}' where the file says '{(string?)entry.Value}'")
            .ToList();
        Assert.True(differing.Count == 0,
            "The list separators .NET gives differ from list-separators.json; write it again and update the Script Core's copy. " + string.Join("; ", differing));
    }

    // A separator other than a comma or a semicolon is null: a CSV is never split at it.
    private static string? SeparatorOf(CultureInfo culture)
        => culture.TextInfo.ListSeparator is "," or ";" ? culture.TextInfo.ListSeparator : null;
}
