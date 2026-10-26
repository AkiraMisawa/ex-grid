using Xunit;

namespace ExSheet.Engine.Tests.Cases;

/// <summary>The shared cases of <c>tests/cases/sheet/</c>, one theory row each (ADR-0370 sections 2 and 5).</summary>
public class SheetCaseTests
{
    public static TheoryData<string, string> Sheet()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (file, name) in SheetCases.Of("sheet"))
            rows.Add(file, name);
        return rows;
    }

    [Theory, MemberData(nameof(Sheet))] // ADR-0370: the facts ExSheet.Engine was tested on, held as shared cases
    public void Sheet_case(string file, string name)
    {
        var (document, entry) = SheetCases.Find(file, name);
        var differences = SheetCases.Run(document, entry);
        Assert.True(differences.Count == 0, $"sheet/{file}, \"{name}\":\n  {string.Join("\n  ", differences)}");
    }
}
